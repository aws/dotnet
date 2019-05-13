Import-Module .\Helpers\Helpers.psm1

<# 
.Synopsis
   Constructs the specific document required for the DEV202 demo and uses
   the Set-InstanceConfiguration cmdlet to apply it.
.Notes
   This command assumes a default region in which to make calls to AWS has been set using 
   Set-DefaultAWSRegion.
#>
function BuildAndApplyInstanceConfiguration
{
    [CmdletBinding()]
    Param
    (
        # Common name applied to all resources created by this cmdlet. The text is used
        # in the resource name, or will be applied as a tag with key 'Name'.
        # This value is also assumed to be the name of the IAM Role that was used when
        # launching the instance(s) to be configured.
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroupName,

        # The name of a directory registered with AWS Directory Service via a Simple AD
        # or AD Connector instance.
        [Parameter(Mandatory=$true)]
        [string]$DirectoryName
    )
    
    Process
    {
        _testDefaultRegionIsSet

        # first we need to add a policy to the role used at instance launch so that
        # SSM can work with the instance(s)
        _applySSMRolePolicy $ResourceGroupName 

        # now we can build our core config - for the demo, we'll do this inline
        # but we could equally pass a filename to the Set-InstanceConfiguration 
        # cmdlet.
        $ssmRuntimeConfig = _constructSSMRuntimeConfig $DirectoryName
        
        Set-InstanceConfiguration -ResourceGroupName $ResourceGroupName -RuntimeConfig $ssmRuntimeConfig
    }
}

<#
.Synopsis
   Applies an Amazon SSM configuration document, held in a disk file or constructed
   dynamically, to one or more running Amazon EC2 instances.
.DESCRIPTION
   Applies an Amazon SSM configuration document, held in a disk file or constructed
   dynamically, to one or more running Amazon EC2 instances.
.Notes
   This cmdlet assumes a default region in which to make calls to AWS has been set using 
   Set-DefaultAWSRegion.
.EXAMPLE
   Example of how to use this cmdlet
#>
function Set-InstanceConfiguration
{
    [CmdletBinding()]
    Param
    (
        # Common name applied to all resources created by this cmdlet. The text is used
        # in the resource name, or will be applied as a tag with key 'Name'.
        # It is also used to discover the EC2 instances the configuration will be
        # applied to.
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroupName,

        # The path and name of a pre-built configurationd document to be applied.
        [Parameter(Mandatory=$true, ParameterSetName="FileConfig")]
        [string]$ConfigurationDocumentFile,

        # Used when building a configuration document dynamically.
        # A collection of one more runtime configuration objects to be assembled into the 
        # the document. 
        [Parameter(Mandatory=$true, ParameterSetName="InlineConfig")]
        [Hashtable]$RuntimeConfig
    )

    Process
    {
        _testDefaultRegionIsSet

        # now form up the json document to apply our configuration and send it to SSM
        # to dispatch to our instance(s)
        $configName = $ResourceGroupName + "_SSMConfig"
        if ($PSCmdlet.ParameterSetName -eq "FromFileDocument")
        {
            $configDoc = Get-Content -Path $ConfigurationDocumentFile
        }
        else
        {
            $config = @{
                "schemaVersion"="1.0"
                "description"="Auto-constructed configuration for $ResourceGroupName"
                "runtimeConfig"=$RuntimeConfig
            }

            # only 2 levels convert by default, so increase depth
            $configDoc = $config | ConvertTo-JSON -Depth 100
        }

        $ssmConfig = New-SSMDocument -Content $configDoc -Name $configName

        # we need to wait a second or two for SSM to prepare our doc for use
        while ($ssmConfig.Status -ne 'Active')
        {
            Start-Sleep -Seconds 1
            $ssmConfig = Get-SSMDocumentDescription -Name $configName
        }

        # Identify the instances to be associated with the document using the
        # common resource tag
        $filters = @{Name="instance-state-name";Values="running"},@{Name="tag-value";Values=$ResourceGroupName}
        $instances = (Get-EC2Instance -Filter $filters).Instances

        # we could equally use the batch api here to do the assignment
        $instances | % { New-SSMAssociation -InstanceId $_.InstanceId -Name $configName }
    }
}

function _applySSMRolePolicy([string]$instanceLaunchRolename)
{
    # the policy we need applied to the launch role used with the instances
    # so that SSM can access apis and resources necessary for the config
    $policyDocument = @"
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "AllowAccessToSSM",
            "Effect": "Allow",
            "Action": [
                "ssm:DescribeAssociation",
                "ssm:ListAssociations",
                "ssm:GetDocument",
                "ssm:UpdateAssociationStatus",
                "ds:CreateComputer",
                "cloudwatch:PutMetricData",
                "logs:CreateLogGroup",
                "logs:CreateLogStream",
                "logs:DescribeLogGroups",
                "logs:DescribeLogStreams",
                "logs:PutLogEvents"
            ],
            "Resource": [
                "*"
            ]
        }
    ]
}
"@
    
    Write-IAMRolePolicy -RoleName $instanceLaunchRolename -PolicyName "SSMPolicy" -PolicyDocument $policyDocument
}

# Builds the runtimeConfig object for the inline construction of an SSM configuration
# document. The data in here is specific to the demo requirements.
function _constructSSMRuntimeConfig([string]$directoryName)
{
    # determine directory settings dynamically by filtering on the name (directory
    # objects do not have tags). If we had the id, we could simply pass the 
    # -DirectoryId parameter.
    $directory = Get-DSDirectory | ? { $_.Name -eq $directoryName }

    # OU data is not returned in the above call, so we need to construct it to satisfy SSM's domain join plugin
    $ouParts = $directory.Name.Split('.')
    $ou = ""
    foreach ($oup in $ouParts)
    {
        if ($ou.Length -eq 0)
        {
            $ou = "OU=$oup"
        }
        else
        {
            $ou += ",DC=$oup"
        }
    }

    # Construct the specific plugin configurations we want
    $domainparams = @{
        "DirectoryId"=$directory.DirectoryId
        "DomainName"=$directory.Name
        "OrganizationalUnit"=$ou
        "DnsIpAddress"=$directory.DnsIpAddrs
    }
    $domainJoinPlugin = New-DomainJoinPlugin @domainparams

    # ...source components for logs and metrics
    $systemEventLog = New-EventLogComponent -Id "systemeventlogid" -LogName System -LogLevel 7
    $appEventLog = New-EventLogComponent -Id "appeventlogid" -LogName Application -LogLevel 7
    $etwLog = New-EventLogComponent -id "etwlogid" -LogName Microsoft-Windows-WinINet/Analytic -LogLevel 7
    
    $memperfparams = @{
        "Id"="memorycounterid"
        "CategoryName"="Memory"
        "CounterName"="Available MBytes"
        "MetricName"="AvailableMemory"
        "Unit"="Megabytes"
    }
    $memPerf = New-PerformanceCounterComponent @memperfparams

    # ...destination components for logs and metrics
    $region = Get-DefaultAWSRegion
    $logdestparams = @{
        "Id"="cloudwatchlogsid"
        "LogGroup"="reinvent2015"
        "LogStream"="{hostname}"
        "Region"=$region
    }
    $logs = New-CloudWatchLogsComponent @logdestparams

    $metricdestparams = @{
        "Id"="cloudwatchid"
        "Namespace"="reinvent2015/default"
        "Region"=$region
    }
    $metrics = New-CloudWatchComponent @metricdestparams

    # Wire up the log and metric sources and destinations with flows
    $logsflow = New-FlowDeclaration -Source $systemEventLog.Id,$appEventLog.Id -Destination $logs.Id
    $perfflow = New-FlowDeclaration -Source $memPerf.Id -Destination $metrics.Id

    # assemble the configuration components into one runtime object and return
    $runtimeConfig = @{
        "aws:domainJoin"=$domainJoinPlugin
        "aws:cloudWatch"=@{
            "properties"=@{
                "EngineConfiguration"=@{
                    "PollInterval"="00:00:15"
                    "Components"=$systemEventLog,$appEventLog,$etwLog,$memPerf,$logs,$metrics
                    "Flows"=@{
                        "Flows"=$logsflow,$perfflow
                    }
                }
            }
        }        
    }

    $runtimeConfig
}

<#
.Synopsis
   Returns an object hash representing an 'aws:domainJoin' plugin
   definition for SSM.
.DESCRIPTION
   Returns a JSON snippet representing an 'aws:domainJoin' plugin
   definition for SSM
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-DomainJoinPlugin
{
    [CmdletBinding()]
    Param
    (
        # The identity of the directory registered with 
        # AWS Directory Service
        [Parameter(Mandatory=$true)]
        [string]$DirectoryId,

        # The name of the domain to join
        [Parameter(Mandatory=$true)]
        [string]$DomainName,

        # The organizational unit (OU). 
        # Example: "OU=test,DC=example,DC=com"
        [Parameter(Mandatory=$true)]
        [string]$OrganizationalUnit,

        # The IP addresses of the DNS servers
        [Parameter()]
        [string[]]$DnsIpAddress
    )

    Process
    {
        $template = @{
		    "properties"=@{
		        "directoryId"=$DirectoryId
		        "directoryName"=$DomainName
                "directoryOU"=$OrganizationalUnit
		        "dnsIpAddresses"=$DnsIpAddress
            }
        }

        $template
    }
}

<#
.Synopsis
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure
   an event log source to be published to CloudWatch Logs.
.DESCRIPTION
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure
   an event log source to be published to CloudWatch Logs.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-EventLogComponent
{
    [CmdletBinding()]
    [OutputType([string])]
    Param
    (
        # ID to assign to the component entry. This must be unique within the
        # combined SSM configuration document.
        [Parameter(Mandatory=$true)]
        [string]$Id,

        # The name of the Winndows log to be published
        [Parameter(Mandatory=$true)]
        [ValidateSet("Application", "Security", "System", "Microsoft-Windows-WinINet/Analytic")]
        [string]$LogName,

        # The types of messages to send to Amazon CloudWatch.
        # Valid values:
        #    1 - Only error messages uploaded.
        #    2 - Only warning messages uploaded.
        #    4 - Only information messages uploaded.
        # Note that you can add values together to include more than one type of message. For example, 3 means 
        # that error messages (1) and warning messages (2) are included. A value of 7 means that error 
        # messages (1), warning messages (2), and informational messages (4) are included.
        [Parameter(Mandatory=$true)]
        [int]$LogLevel
    )

    Process
    {
        $template = @{
          "Id"=$Id
          "FullName"="AWS.EC2.Windows.CloudWatch.EventLog.EventLogInputComponent,AWS.EC2.Windows.CloudWatch"
          "Parameters"=@{
            "LogName"=$LogName
            "Levels"=$LogLevel
          }
        }            

        $template
    }
}

<#
.Synopsis
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure 
   a CloudWatch destination for performance metrics.
.DESCRIPTION
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure 
   a CloudWatch destination for performance metrics.
.Note
   Assumes the instances to which the configuration will be applied were 
   launched with an IAM role so the access key and secret key members will
   be left empty.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-CloudWatchComponent
{
    [CmdletBinding()]
    [OutputType([string])]
    Param
    (
        # ID to assign to the component entry. This must be unique within the
        # combined SSM configuration document.
        [Parameter(Mandatory=$true)]
        [string]$Id,

        # The region where you want to send log data. Although you can send performance counters 
        # to a different region from where you send your log data, we recommend that you set this 
        # parameter to the same region where your instance is running.
        [Parameter(Mandatory=$true)]
        [ValidateSet("us-east-1", "us-west-2", "eu-west-1", "eu-central-1", "ap-southeast-1", 
                     "ap-southeast-2", "ap-northeast-1")]
        [string]$Region,

        # The metric namespace where you want performance counter data to be written.
        [Parameter(Mandatory=$true)]
        [string]$Namespace
    )

    Process
    {
        $template = @{
          "Id"=$Id
          "FullName"="AWS.EC2.Windows.CloudWatch.CloudWatch.CloudWatchOutputComponent,AWS.EC2.Windows.CloudWatch"
          "Parameters"=@{
            "AccessKey"=""
            "SecretKey"=""
            "Region"=$Region
            "NameSpace"=$Namespace
          }
        }

        $template
    }
}

<#
.Synopsis
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure a
   CloudWatch Logs entry.
.DESCRIPTION
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure a
   CloudWatch Logs entry.
.Note
   Assumes the instances to which the configuration will be applied were 
   launched with an IAM role so the access key and secret key members will
   be left empty.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-CloudWatchLogsComponent
{
    [CmdletBinding()]
    [OutputType([string])]
    Param
    (
        # ID to assign to the component entry. This must be unique within the
        # combined SSM configuration document.
        [Parameter(Mandatory=$true)]
        [string]$Id,

        # The region where you want to send log data. Although you can send performance counters 
        # to a different region from where you send your log data, we recommend that you set this 
        # parameter to the same region where your instance is running.
        [Parameter(Mandatory=$true)]
        [ValidateSet("us-east-1", "us-west-2", "eu-west-1", "eu-central-1", "ap-southeast-1", 
                     "ap-southeast-2", "ap-northeast-1")]
        [string]$Region,

        # The name for your log group. This name is displayed on the Log Groups screen in the CloudWatch 
        # console.
        [Parameter(Mandatory=$true)]
        [string]$LogGroup,

        # The destination log stream. If you use {instance_id}, the default, the instance ID of this instance is used 
        # as the log stream name.
        # Valid values: {instance_id} | {hostname} | {ip_address} <log_stream_name>
        # If you enter a log stream name that doesn't already exist, CloudWatch Logs automatically creates it for you. 
        # You can use a literal string or predefined variables ({instance_id}, {hostname}, {ip_address}, or a combination 
        # of all three to define a log stream name.
        # The log stream name specified in this parameter appears on the Log Groups > Streams for <YourLogStream> screen 
        # in the CloudWatch console.
        [Parameter()]
        [string]$LogStream
    )

    Process
    {
        $template = @{
          "Id"=$Id
          "FullName"="AWS.EC2.Windows.CloudWatch.CloudWatchLogsOutput,AWS.EC2.Windows.CloudWatch"
          "Parameters"=@{
            "AccessKey"=""
            "SecretKey"=""
            "Region"=$Region
            "LogGroup"=$LogGroup
            "LogStream"=$LogStream
          }
        }

        $template
    }
}
<#
.Synopsis
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure 
   a custom log source.
.DESCRIPTION
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure 
   a custom log source.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-CustomLogsComponent
{
    [CmdletBinding()]
    [OutputType([string])]
    Param
    (
        # ID to assign to the component entry. This must be unique within the
        # combined SSM configuration document.
        [Parameter(Mandatory=$true)]
        [string]$Id,

        # For CustomLogs, the path where logs are stored on your Amazon EC2 instance. For IIS logs, 
        # the folder where IIS logs are stored for an individual site (for example, 
        # C:\\inetpub\\logs\\LogFiles\\W3SVCn). 
        # Note: For IIS logs, only W3C log format is supported. IIS, NCSA, and Custom formats are not 
        # supported.
        [Parameter(Mandatory=$true)]
        [string]$LogDirectoryPath,

        # The timestamp format you want to use.
        [Parameter(Mandatory=$true)]
        [string]$TimestampFormat,

        # The file encoding to use (for example, UTF-8). Use the encoding name, not the display 
        # name.
        [Parameter(Mandatory=$true)]
        [string]$Encoding,

        # The prefix of log names. Leave this parameter blank to monitor all files.
        [Parameter()]
        [string]$Filter = "",

        # The locale where the timestamp is logged. If CultureName is blank, it defaults to 
        # the same locale currently used by your Windows instance.
        [Parameter()]
        [string]$CultureName,

        # Provides time zone information when no time zone information is included in your log’s timestamp. 
        # If this parameter is left blank and if your timestamp doesn’t include time zone information, 
        # CloudWatch Logs defaults to the local time zone. This parameter is ignored if your timestamp already 
        # contains time zone information.
        [Parameter(Mandatory=$true)]
        [ValidateSet("Local", "UTC")]
        [string]$TimeZoneKind,

        # The number of lines in the header to identify the log file. For example, IIS log files 
        # have virtually identical headers. You could enter 3, which would read the first three lines 
        # of the log file's header to identify it. In IIS log files, the third line is the date and time 
        # stamp, which is different between log files.
        [Parameter(Mandatory=$true)]
        [int]$LineCount
    )

    Process
    {
        $template = @{
          "Id"=$Id
          "FullName"="AWS.EC2.Windows.CloudWatch.CustomLog.CustomLogInputComponent,AWS.EC2.Windows.CloudWatch"
          "Parameters"=@{
            "LogDirectoryPath"=$LogDirectoryPath
            "TimestampFormat"=$TimestampFormat
            "Encoding"=$Encoding
            "Filter"=$Filter
            "CultureName"=$CultureName
            "TimeZoneKind"=$TimeZoneKind
            "LineCount"=$LineCount
          }
        }

        $template
    }
}

<#
.Synopsis
   Returns a populated EC2 SSM 'aws:cloudWatch' component entry to configure 
   a performance counter to be sent to CloudWatch.
.DESCRIPTION
   Returns a populated EC2 SSM 'aws:cloudWatch' component hashtable to configure 
   a performance counter to be sent to CloudWatch. This object can be converted
   to JSON using ConvertTo-JSON.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-PerformanceCounterComponent
{
    [CmdletBinding()]
    Param
    (
        # ID to assign to the component entry. This must be unique within the
        # combined SSM configuration document.
        [Parameter(Mandatory=$true)]
        [string]$Id,

        [Parameter()]
        [string]$CategoryName,

        # The name of the performance counter from Performance Monitor.
        [Parameter(Mandatory=$true)]
        [string]$CounterName,

        # The name of the performance counter instance. Do not use an asterisk (*) to indicate all instances because 
        # each performance counter component only supports one metric. You can, however use _Total.
        [Parameter()]
        [string]$InstanceName = "",

        # The CloudWatch metric that you want performance data to appear under.
        [Parameter(Mandatory=$true)]
        [string]$MetricName,

        # The appropriate unit of measure for the metric.
        [Parameter(Mandatory=$true)]
        [ValidateSet("Seconds", "Microseconds", "Milliseconds", "Bytes", "Kilobytes", "Megabytes",
                     "Gigabytes", "Terabytes", "Bits", "Kilobits", "Megabits",  "Gigabits", "Terabits",
                     "Percent", "Count", "Bytes/Second", "Kilobytes/Second", "Megabytes/Second",
                     "Gigabytes/Second", "Terabytes/Second", "Bits/Second", "Kilobits/Second",
                     "Megabits/Second", "Gigabits/Second", "Terabits/Second", "Count/Second", "None")]
        [string]$Unit,

        # A dimension for your Amazon CloudWatch metric. If you specify DimensionName, you must specify 
        # DimensionValue. These parameters provide another view when listing metrics. You can also use the 
        # same dimension for multiple metrics so that you can view all metrics belonging to a specific 
        # dimension.
        [Parameter()]
        [string]$DimensionName,

        # A dimension value for your Amazon CloudWatch metric.
        [Parameter()]
        [string]$DimensionValue
    )

    Process
    {
        $template = @{
          "Id"=$Id
          "FullName"="AWS.EC2.Windows.CloudWatch.PerformanceCounterComponent.PerformanceCounterInputComponent,AWS.EC2.Windows.CloudWatch"
          "Parameters"= @{
            "CategoryName"=$CategoryName
            "CounterName"=$CounterName
            "InstanceName"=$InstanceName
            "MetricName"=$MetricName
            "Unit"=$Unit
            "DimensionName"=$DimensionName
            "DimensionValue"=$DimensionValue
          }
        }

        $template
    }
}


<#
.Synopsis
   Constructs an SSM aws:cloudWatch flow entry comprising one or more sources
   and the destination(s) they should flow to.
.DESCRIPTION
   Constructs an SSM aws:cloudWatch flow entry comprising one or more sources
   and the destination(s) they should flow to. You can wire one source to one
   destination, one source to multiple destinations or multiple sources to
   one destination
.EXAMPLE
   New-FlowDeclaration -Source source1ID -Destination destination1ID
.EXAMPLE
   New-FlowDeclaration -Source source1ID -Destination destination1ID,destination2ID
.EXAMPLE
   New-FlowDeclaration -Source source1ID,source2ID -Destination destination1ID
#>
function New-FlowDeclaration
{
    [CmdletBinding()]
    [OutputType([string])]
    Param
    (
        # The id(s) of one or more sources for monitoring/log data
        [Parameter(Mandatory=$true)]
        [string[]]$Source,

        # The id(s) of one or more destinations for monitoring/log data
        [string[]]$Destination
    )

    Process
    {
        $template = @()

        if ($Source.Count -eq 1)
        {
            if ($Destination.Count -eq 1)
            {
                $template += "$Source,$Destination"
            }
            else
            {
                $dest = $Destination -join ","
                $template += "$Source,($dest)"
            }

            return $template
        }

        $src = $Source -join ","
        if ($Destination.Count -eq 1)
        {
            $template += "($src),$Destination"
            return $template
        }

        # multi source to multi destination is not (currently) supported by SSM
        throw "Multiple source to multiple destination flow is not valid"
    }
}
Import-Module .\Helpers\Helpers.psm1

<#
.Synopsis
  Coordinates the import and launch of a local VM image into the AWS cloud
  using the helper cmdlets contained in this module.
.Description
  Coordinates the import and launch of a local VM image into the AWS cloud
  using the helper cmdlets contained in this module.
  The process requires 4 steps - import, create network, launch instance of
  the image and finally perform post-launch configuration of the instance.

  This command was mentioned in the DEV202 breakout session at AWS re:Invent 2015
  but not used; the session demo used the individual helper cmdlets to perform
  the process steps.
#>
function ImportAndLaunch
{
    [CmdletBinding()]
    Param
    (
         # Common name applied to all resources created by this cmdlet. The text 
        # is used in the resource name, or will be applied as a tag with key 
        # 'Name'.
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroupName,

        # The path and filename of the VM to be imported.
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [string]$VMFile,

        # The name of the bucket that will contain the uploaded VM. Bucket names 
        # must be globally unique. The bucket will be created if it does not exist. 
        # The VM file will be uploaded to the bucket using an object key comprising 
        # the value of the ResourceGroupName as a key prefix.
        [Parameter(Mandatory=$true, ValueFromPipelineByPropertyName=$true)]
        [string]$BucketName,

        # The base address range for the VPC network that will host the running image.
        # The VPC will be created with a /16 netmask for the address. One subnet per 
        # availability zone will be created, each with a /24 netmask.
        [Parameter(Mandatory=$true)]
        [string]$VpcBaseAddress,

        # The 'Name' tag that was applied to the vpc we want to peer with. Details of the 
        # vpc that are required to initiate peering will be retrieved automatically.
        [Parameter(Mandatory=$true)]
        [string]$PeerVpcNameTag,

        # One or more availability zones, associated with VPC subnets, to launch instance(s) 
        # into. $InstanceCount instances will be launched into the subnet associated with
        # each specified zone.
        [Parameter(Mandatory=$true)]
        [string[]]$SubnetZones,

        # The size of instance type to launch. Defaults to a t2.small if not
        # specified. Note that the type must be compatible with launching into a VPC.
        [Parameter()]
        [string]$InstanceType = "t2.small",

        # The number of instances to launch. Default is 1.
        [Parameter()]
        [int]$InstanceCount = 1,

        # Optional. Specify in conjunction with the IngressPort parameter to modify the security 
        # group created for the launch so that certain tcp ports are open to IP addresses in this CIDR block.
        [Parameter()]
        [string]$IngressIp,

        # Optional. Specify in conjunction with the IngressIp parameter to modify the new security group created 
        # for the launch so that certain tcp ports are open to the IP address range specified in the block.
        [Parameter()]
        [int[]]$IngressPort,

        # Optional. If specified contains the name of a managed policy. 
        # If the parameter is not specified the instance(s) will be launched with the "PowerUserAccess" 
        # managed policy.
        [Parameter()]
        [string]$ManagedPolicyName = "PowerUserAccess",

        # Optional. If specified PowerShell remoting will be configured for the specified
        # port by passing a custom PowerShell script configuring the firewall on the instance(s)
        # in user data.
        [Parameter()]
        [int]$PSRemotingPort = -1,

        # The name of a directory registered with AWS Directory Service via a Simple AD
        # or AD Connector instance.
        [Parameter(Mandatory=$true)]
        [string]$DirectoryName
   )

    Process
    {
        # step 1: import the VM to Amazon S3 and request import conversion to an
        #         Amazon EC2 image (AMI)
        $importTask = Import-VMToAWS -ResourceGroupName $ResourceGroupName -VMFile $VMFile -Bucket $BucketName

        #....wait until the VM import is complete (this can take some time)
        $importTask = Get-EC2ImportImageTask -ImportTaskId $importTask.ImportTaskId
        while ($importTask.Status -ne 'completed')
        {
            Write-Verbose "Import task: status $($importTask.Status) - $($importTask.StatusMessage), -- sleeping 1 min"
            Start-Sleep -Seconds 60

            $importTask = Get-EC2ImportImageTask -ImportTaskId $importTask.ImportTaskId
            # should probably check for error on conversion here, and break out of loop...
        }

        #...then check for failed conversion here and throw an exception
        Write-Verbose "Import conversion completed"

        # step 1a: tag the image that got created
        New-EC2Tag -Resource $importTask.ImageId -Tag @{ Key="Name";Value=$ResourceGroupName }

        # step 2: create the private network to host the image we created
        New-VPCNetwork -ResourceGroupName $ResourceGroupName -VpcBaseAddress $VpcBaseAddress -PeerVpcNameTag $PeerVpcNameTag

        # step 3: launch an instance of the image into one subnet of the private
        #         network we created earlier
        $launchParams = @{
            "SubnetZones"=$SubnetZones
            "InstanceType"=$InstanceType
            "InstanceCount"=$InstanceCount
            "IngressIp"=$IngressIp
            "IngressPort"=$IngressPort
            "ManagedPolicyName"=$ManagedPolicyName
            "PSRemotingPort"=$PSRemotingPort
        }
        $instances = New-InstanceFromImage -ResourceGroupName $ResourceGroupName -ImageId $importTask.ImageId @launchParams

        # step 3a: wait for the instances to reach fully launched status, 
        #          then we can initiate post-launch configuration
        $filters = @{Name="instance-state-name";Values="running"},@{Name="tag-value";Values=$ResourceGroupName}
        $readyInstances = (Get-EC2Instance -Filter $filters).Instances
        while ($readyInstances -eq $null -Or ($readyInstances.Count -ne $instances.Count))
        {
            Write-Verbose "Waiting 10s for all instances to complete launch"
            Start-Sleep -Seconds 10
        }

        Write-Verbose "All instances launched; proceeding with post-launch configuration"

        # step 4: configure the new instance to join our domain and to push logs
        #         and metrics to Amazon CloudWatch Logs and Amazon CloudWatch
        BuildAndApplyInstanceConfiguration -ResourceGroupName $ResourceGroupName -DirectoryName $DirectoryName

        Write-Verbose "Import and launch completed"
    }
}

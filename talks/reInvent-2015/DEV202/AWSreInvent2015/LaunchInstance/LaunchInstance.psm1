Import-Module .\Helpers\Helpers.psm1

<#
.Synopsis
   Launches one or more instances of an Amazon EC2 Machine Image (AMI) into a VPC. 
   The launched instance(s) can also have tags applied to them for future use.
.DESCRIPTION
   Launches one or more instances of an Amazon EC2 Machine Image (AMI) into a VPC. 
   The launched instance(s) can also have tags applied to them for future use. This module formed 
   the third stage of the demo in the DEV202 breakout session at AWS re:Invent 2015.
   The cmdlet emits the instances launched to the pipeline on exit.
.Notes
   This cmdlet assumes a default region in which to make calls to AWS has been set using 
   Set-DefaultAWSRegion and all resources it needs to discover have been tagged or named
   with the value supplied to the $ResourceGroupName parameter.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-InstanceFromImage
{
    [CmdletBinding()]
    Param
    (
        # Common name applied to all resources created by this cmdlet. The text is used
        # in the resource name, or will be applied as a tag with key 'Name'.
        # If a VPC exists with this tag and a single subnet we assume the new instance
        # is to be launched into the subnet.
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroupName,

        # One or more availability zones, associated with VPC subnets, to launch instance(s) 
        # into. $InstanceCount instances will be launched into the subnet associated with
        # each specified zone.
        [Parameter(Mandatory=$true)]
        [string[]]$SubnetZones,

        # The ID of the image to launch. If not specified the cmdlet will
        # attempt to find an self-owned AMI with a Name tag corresponding to 
        # the value of the ResourceGroupName parameter.
        [Parameter(ValueFromPipelineByPropertyName=$true)]
        [string]$ImageId,

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
        [int]$PSRemotingPort = -1
    )

    Process
    {
        _testDefaultRegionIsSet

        $vpc = _testForVPCLaunch $ResourceGroupName

        # find the ID of the image to launch
        $imageIdToLaunch = _discoverImageFromIDOrTag $ImageId $ResourceGroupName

        # create a role for the instance launch, attaching the specified managed policy
        # or the default of 'PowerUserAccess'.
        $roleName = _setupRoleForInstanceLaunch $ResourceGroupName $ManagedPolicyName

        # EC2 uses an 'instance profile' container to specify the role on launch, so
        # create one and associate with the role - the console and toolkits do this step 
        # for you
        $instanceProfileName = $roleName + "Profile"
        $instanceProfile = Get-IAMInstanceProfiles | ? { $_.InstanceProfileName -eq $instanceProfileName }
        if ($instanceProfile -eq $null)
        {
            New-IAMInstanceProfile -InstanceProfileName $instanceProfileName
            Add-IAMRoleToInstanceProfile -InstanceProfileName $instanceProfileName -RoleName $roleName
        }

        # Setup a security group, granting acccess from addresses in the specified cidr block
        # to a set of ports. As we may we launching into a VPC, grab the ID of the security group
        # to specify in the launch parameters (as opposed to group name, which is not supported
        # in VPCs).
        $securityGroupId = _setupSecurityGroup $ResourceGroupName $vpc.VpcId $IngressIp $IngressPort
         
	    # set up the initial launch parameters for New-EC2Instance
        $launchParams = @{
            "ImageId"=$imageIdToLaunch
            "InstanceType"=$InstanceType
            "SecurityGroupId"=$securityGroupId
            "InstanceProfile_Name"=$instanceProfileName
            "MaxCount"=$InstanceCount
        }

        # if the user requested that PowerShell remoting use a non-default port, craft 
        # the user data script to configure WinRM on the instance(s) at launch
        if ($PSRemotingPort -ne -1)
        {
            $userData = _configurePSRemotingUserDataScript $PSRemotingPort
            $launchParams.Add("UserData", $userData)
        }

        $vpcSubnets = Get-EC2Subnet -Filter @{Name="vpc-id";Values=$vpc.VpcId}
        foreach ($subnetZone in $SubnetZones)
        {
            $subnet = $vpcSubnets | ? { $_.AvailabilityZone -eq $subnetZone }
            if ($subnet -ne $null)
            {
                $reservation = New-EC2Instance @launchParams -SubnetId $subnet.SubnetId
                # apply the resource group name as a tag with the key 'Name' so we can easily find them
                # later
                $reservation.Instances | 
                    % { New-EC2Tag -Resource $_.InstanceId -Tag @{Key="Name";Value=$ResourceGroupName} }

                Write-Verbose "Launched $InstanceCount instances in subnet for zone $subnetZone, reservation id $reservation.ReservationId"
                $reservation.Instances
            }
            else
            {
                throw "Failed to launch in zone $subnetZone, unable to find matching subnet"
            }
        }
    }
}

# Creates a new security group, or updates an existing group, for the launch and 
# optionally opens one of more ports to access from the specified cidr block.
function _setupSecurityGroup([string]$resourceGroupName, 
                             [string]$vpcId, 
                             [string]$ingressIp, 
                             [int[]]$ingressPort)
{
    $existingGroup = Get-EC2SecurityGroup | ? { $_.GroupName -eq $resourceGroupName }

    if ($existingGroup -eq $null)
    {
        $groupDescription = "Automatically created security group " + (Get-Date)
        Write-Verbose "Creating security group $resourceGroupName for the instance launch"

        $params = @{
            "GroupName"=$resourceGroupName
            "GroupDescription"=$groupDescription
            "VpcId"=$vpcId
        }

	    $securityGroupId = New-EC2SecurityGroup @params
    }
    else
    {
        if ([string]::IsNullOrEmpty($existingGroup.VpcId))
        {
            throw "Security group discovered using resource group name does not belong to a VPC!"
        }

        $securityGroupId = $existingGroup.GroupId
    }

    if ($ingressPort -ne $null -And $ingressPort.Count -gt 0)
    {
        Write-Verbose "Opening ports for access in security group $securityGroupId ($resourceGroupName)"

        $ipPermissions = @()
        foreach ($p in $ingressPort)
        {
            $ipPermission = New-Object Amazon.EC2.Model.IpPermission
            $ipPermission.FromPort = $p
            $ipPermission.ToPort = $p
            $ipPermission.IpProtocol = "tcp"
            $ipPermission.IpRange = $ingressIp

            $ipPermissions += $ipPermission
        }

        # grant ingress to the defined ports; if the group already existed EC2 will raise
        # an error if any of the ports are already set
        Grant-EC2SecurityGroupIngress -GroupId $securityGroupId -IpPermission $ipPermissions
    }

    $securityGroupId
}

# Creates a role for the instance launch and attaches the specified managed
# policy. Returns the name of the new role.
function _setupRoleForInstanceLaunch([string]$resourceGroupName, 
                                     [string]$managedPolicyName)
{
    $roleName = _setupRoleWithAssumeRolePolicy $resourceGroupName

    Write-Verbose "Attaching managed policy $managedPolicyName to role $roleName"
    Register-IAMRolePolicy -RoleName $roleName -PolicyArn "arn:aws:iam::aws:policy/$managedPolicyName"

    # need to pause briefly to allow propagation
    Write-Verbose "Pausing for a few seconds to allow for role update propagation"
    Start-Sleep -Seconds 2

    $roleName
}

# If an image id has been supplied, return it as the image to launch otherwise
# attempt to find the image by looking for an AMI with a Name tag corresponding
# to the value of the resourceGroupName parameter.
function _discoverImageFromIDOrTag([string]$imageId, [string]$resourceGroupName)
{
    if (!([string]::IsNullOrEmpty($imageId)))
    {
        return $imageId
    }

    # just in case we've applied the tag multiple times, filter down to the first available
    $image = Get-EC2Image -Owner self -Filter @{Name="tag-value";Values=$resourceGroupName} | select -first 1
    $image.ImageId
}

# Returns the VPC with a Name tag corresponding to the value specified in $resourceGroupName. 
function _testForVPCLaunch([string]$resourceGroupName)
{
    $vpc = Get-EC2Vpc -Filter @{Name="tag-value";Values=$resourceGroupName}
    if ($vpc -eq $null)
    {
        throw "Unable to find a VPC belonging to the specified resource group"
    }

    $vpc
}

function _configurePSRemotingUserDataScript([int]$requestedPort)
{
	# To use PowerShell remoting with the server instance we pass a PowerShell script as user data 
    # to have the instance configure the firewall to enable PSRemoting on a specific port. The user 
    # data has to be passed as a base64 encoded string.
    $userdata = @"
<powershell>
Enable-NetFirewallRule FPS-ICMP4-ERQ-In
Set-NetFirewallRule -Name WINRM-HTTP-In-TCP-PUBLIC -RemoteAddress Any
New-NetFirewallRule -Name "WinR$requestedPort" -DisplayName "WinR$requestedPort" -Protocol TCP -LocalPort $requestedPort
Set-Item WSMan:\localhost\Service\EnableCompatibilityHttpListener -Value true
</powershell>
"@

	$userdataBytes = [System.Text.Encoding]::UTF8.GetBytes($userdata)
    return [System.Convert]::ToBase64String($userdataBytes)
}

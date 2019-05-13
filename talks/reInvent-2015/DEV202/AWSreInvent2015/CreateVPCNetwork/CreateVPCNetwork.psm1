Import-Module .\Helpers\Helpers.psm1

<#
.Synopsis
   Creates and configures a VPC with a single private subnet and vpc peering 
   to another VPC that is simulating an on-premises corporate network for demo
   purposes.
.DESCRIPTION
   Creates and configures a VPC with a single private subnet and vpc peering 
   to another VPC that is simulating an on-premises corporate network for demo
   purposes.
   This module formed the second stage of the demo in the DEV202 breakout session 
   at AWS re:Invent 2015. 
   The address range for the new VPC is created with a /16 netmask. The VPC is then
   further subdivided into one or more /24 subnets, with one subnet per availability
   zone in the region the VPC was created in.
.Notes
   This cmdlet assumes a default region in which to make calls to AWS has been set using 
   Set-DefaultAWSRegion.
.EXAMPLE
   Example of how to use this cmdlet
#>
function New-VPCNetwork
{
    [CmdletBinding()]
    Param
    (
        # Common name applied to all resources created by this cmdlet. The text is used
        # in the resource name, or will be applied as a tag with key 'Name'.
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroupName,

        # The base address range for the new VPC.The VPC will be created with a /16
        # netmask for the address. One subnet per availability zone will be created,
        # each with a /24 netmask.
        [Parameter(Mandatory=$true)]
        [string]$VpcBaseAddress,

        # The 'Name' tag that was applied to the vpc we want to peer with. Details of the 
        # vpc that are required to initiate peering will be retrieved automatically.
        [Parameter(Mandatory=$true)]
        [string]$PeerVpcNameTag
    )

    Process
    {
        _testDefaultRegionIsSet

        # create the vpc and apply a tag for identification (tagging can be done
        # before the vpc is fully initialized)
        $vpcCidr = "$VpcBaseAddress/16"
        $vpc = New-EC2Vpc -CidrBlock $vpcCidr

        New-EC2Tag -Resource $vpc.VpcId -Tag @{Key="Name";Value=$ResourceGroupName}
        
        # need to wait until the vpc is fully initialized before creating
        # the subnet(s)
        $vpc = Get-EC2Vpc -VpcId $vpc.VpcId
        while ($vpc.State -ne "available")
        {
            Start-Sleep -Seconds 1
            $vpc = Get-EC2Vpc -VpcId $vpc.VpcId
        }

        $ipParts = $VpcBaseAddress.Split('.')

        # create one subnet in each availability zone in the current region
        $zones = Get-EC2AvailabilityZone | select -ExpandProperty ZoneName
        $i = 0
        foreach ($zone in $zones)
        {
            $subnetCidr = [string]::Format("{0}.{1}.{2}.0/24", $ipParts[0], $ipParts[1], $i)

            $subnet = New-EC2Subnet -VpcId $vpc.VpcId -AvailabilityZone $zone -CidrBlock $subnetCidr
            New-EC2Tag -Resource $subnet.SubnetId -Tag @{Key="Name";Value=$ResourceGroupName}

            $i++
        }

        # wait until all subnets are provisioned
        $subnetFilter = @{Name="vpc-id";Values=$vpc.VpcId},@{Name="state";Values="available"}
        $availableSubnets = Get-EC2Subnet -Filter $subnetFilter
        while ($availableSubnets.Count -ne $zones.Count)
        {
            Start-Sleep -Seconds 1
            $availableSubnets = Get-EC2Subnet -Filter $subnetFilter
        }

        # ...in the real world, we'd set up a virtual private gateway linking it to our 
        # customer gateway here...

        # ...in the demo world though we're going to use vpc peering to simulate linking back
        # to a corporate network, hosted in the peer vpc.
        _configureDemoNetworkWithVPCPeering $vpc.VpcId $vpcCidr $PeerVpcNameTag
    }
}

# Configures our new VPC so that it can access the simulated corporate network which
# is hosted in another VPC. This will be done using VPC peering.
function _configureDemoNetworkWithVPCPeering([string]$ourVpcId, 
                                             [string]$vpcCidr, 
                                             [string]$peerVpcNameTag)
{
    # retrieve details of the VPC we are going to peer with
    $peerVpc = Get-EC2Vpc -Filter @{Name="tag-value";Values=$peerVpcNameTag}

    $peeringConnection = New-EC2VpcPeeringConnection -PeerVpcId $peerVpc.VpcId -VpcId $ourVpcId

    $peeringConnectionId = $peeringConnection.VpcPeeringConnectionId

    # accept the request programmatically so we can avoid a trip to the console - we do though
    # need to wait for the request to go pending acceptance first
    while ($peeringConnection.Status.Code -ne 'pending-acceptance')
    {
        Start-Sleep -Seconds 1

        $peeringConnection = Get-EC2VpcPeeringConnections -VpcPeeringConnectionId $peeringConnectionId
    }
     
    Approve-EC2VpcPeeringConnection -VpcPeeringConnectionId $peeringConnectionId

    # enable the flow of traffic between the two VPCs by adding entries to the route tables
    # first, handle the route table of the peer vpc. The first route table returned is
    # the main route table for the vpc -- which is not associated with an internet
    # gateway in our demo and therefore the one we want to update
    $mainTableFilters = @{Name="vpc-id";Value=$peerVpc.VpcId},@{Name="association.main";Value="true"}
    $mainPeerRouteTable = Get-EC2RouteTable -Filter $mainTableFilters
    $params = @{
        "RouteTableId"=$mainPeerRouteTable.RouteTableId
        "DestinationCidrBlock"=$vpcCidr
        "VpcPeeringConnectionId"=$peeringConnectionId
    }
    New-EC2Route @params

    # now update the single route table of the vpc hosting our imported server
    $ourRouteTable = Get-EC2RouteTable -Filter @{Name="vpc-id";Value=$ourVpcId}
    $params = @{
        "RouteTableId"=$ourRouteTable.RouteTableId
        "DestinationCidrBlock"=$peerVpc.CidrBlock
        "VpcPeeringConnectionId"=$peeringConnectionId
    }
    New-EC2Route @params

}

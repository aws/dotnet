
function _LaunchCloudFormationStack([string]$instanceType, [string]$keyPair, [string]$ecsCluster)
{
    Write-Host "Creating CloudFormation Stack to create the AWS resources that ECS will use to deploy are application to"
	
    $templatePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "./EnvironmentSetupCloudFormation/compute-resources.template"))
    $templateBody = [System.IO.File]::ReadAllText($templatePath)

    $imageId = "ami-cb584dfb"

    $tag = New-Object -TypeName Amazon.CloudFormation.Model.Tag
    $tag.Key = "Application"
    $tag.Value = "Pollster-DockerECS"

    $param1 = New-Object  -TypeName Amazon.CloudFormation.Model.Parameter
    $param1.ParameterKey = "ImageId"
    $param1.ParameterValue = $imageId

    $param2 = New-Object  -TypeName Amazon.CloudFormation.Model.Parameter
    $param2.ParameterKey = "InstanceType"
    $param2.ParameterValue = $instanceType

    $param3 = New-Object  -TypeName Amazon.CloudFormation.Model.Parameter
    $param3.ParameterKey = "EC2KeyName"
    $param3.ParameterValue = $keyPair


    $param4 = New-Object  -TypeName Amazon.CloudFormation.Model.Parameter
    $param4.ParameterKey = "ECSClusterName"
    $param4.ParameterValue = $ecsCluster


    $parameters = $param1, $param2, $param3, $param4


    $stackId = New-CFNStack -StackName "Pollster-DockerECS" -Capability "CAPABILITY_IAM" -Parameter $parameters -Tag $tag -TemplateBody $templateBody
    $stackId
}


function _setupTaskDefinition([string]$clusterName,[string]$loadBalancerName,[string]$loadBalancerRole,[string]$baseName,[string]$port,[int]$desiredCount,[int]$memory)
{
    $cd = New-Object -TypeName Amazon.ECS.Model.ContainerDefinition
    $cd.Name = $baseName
    $cd.Essential = $true
    $cd.Image = ("socketnorm/" + $baseName + ":initial")
    $cd.Memory = $memory

    if($port -ne 0)
    {
        $portMapping = New-Object -TypeName Amazon.ECS.Model.PortMapping
        $portMapping.ContainerPort = $port
        if($port -eq 5000)
        {
            $portMapping.HostPort = 80
        }
        else
        {
            $portMapping.HostPort = $port
        }
        $portMapping.Protocol = "tcp"

        $cd.PortMappings.Add($portMapping)
    }

    $envMapping = New-Object -TypeName Amazon.ECS.Model.KeyValuePair
    $envMapping.Name = "ASPNET_ENV"
    $envMapping.Value = "ElasticBeanstalk-Docker"
    $cd.Environment.Add($envMapping)	

    $taskDefintion = Register-ECSTaskDefinition -Family $baseName -ContainerDefinition $cd
    $taskRevision = ($taskDefintion.Family + ":" + $taskDefintion.Revision)

    Write-Host ("Registered task definition " + $taskRevision)

    if(((Get-ECSService -Cluster pollster -Service $baseName).Services.Count) -eq 0)
    {
        $loadBalancer = $null
        if([string]::IsNullOrEmpty($loadBalancerName) -eq $false)
        {
            $loadBalancer = New-Object -TypeName Amazon.ECS.Model.LoadBalancer
            $loadBalancer.LoadBalancerName = $loadBalancerName
            $loadBalancer.ContainerPort = $port
            $loadBalancer.ContainerName = $baseName

            $service = New-ECSService -Cluster $clusterName -ServiceName $baseName -TaskDefinition $taskRevision -LoadBalancer $loadBalancer -Role $loadBalancerRole -DesiredCount $desiredCount
            Write-Host "Created Service for new Task Definition with load balancer"
        }
        else
        {
            $service = New-ECSService -Cluster $clusterName -ServiceName $baseName -TaskDefinition $taskRevision -DesiredCount $desiredCount
            Write-Host "Created Service for new Task Definition"
        }

    
    }
    else
    {
        $service = Update-ECSService -Cluster $clusterName -Service $baseName -TaskDefinition $taskRevision -DesiredCount $desiredCount
        Write-Host  "Updated service to latest task revision"
    }
}


function ProcessInput([string]$instanceType,[string]$keyPair)
{

    $clusterName = (New-ECSCluster -ClusterName "pollster").ClusterName
    $stackId = _LaunchCloudFormationStack $instanceType $keyPair $clusterName
    $stack = Get-CFNStack -StackName $stackId

    while ($stack.StackStatus.Value.toLower().EndsWith('in_progress'))
    {
        $stack = Get-CFNStack -StackName $stackId
        "Waiting for CloudFormation Stack to be created"
        Start-Sleep -Seconds 10
    }

    if ($stack.StackStatus -ne "CREATE_COMPLETE") 
    {
        "CloudFormation Stack was not successfully created, view the stack events for further information on the failure"
        Exit
    }


    $clusterName = "pollster"
    $loadBalancerRole = "ecsServiceRole"
    $desiredCount = 3

    _setupTaskDefinition $clusterName $null $null pollworkflow 0 $desiredCount 128
    _setupTaskDefinition $clusterName "ecs-pollster-feed" $loadBalancerRole pollfeed 5002 $desiredCount 512
    _setupTaskDefinition $clusterName "ecs-pollster-voter" $loadBalancerRole pollvoter 5003 $desiredCount 256
    _setupTaskDefinition $clusterName "ecs-pollster-frontend" $loadBalancerRole pollwebfrontend 5000 $desiredCount 512
    _setupTaskDefinition $clusterName "ecs-pollster-writer" $loadBalancerRole pollwriter 5001 $desiredCount 128

}


ProcessInput "t2.large" "pollster"
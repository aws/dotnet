
function _LaunchCloudFormationStack([string]$instanceType, [string]$stackName, [string]$keyPair, [bool]$openRDP)
{
    Write-Host "Creating CloudFormation Stack to create the AWS resources that CodeDeploy will use to deploy are application to"

    $templatePath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "./EnvironmentSetupCloudFormation/codedeploy.autoscaling.template"))
    $templateBody = [System.IO.File]::ReadAllText($templatePath)

    $imageId = (Get-EC2ImageByName WINDOWS_2012R2_BASE).ImageId

    $tag = New-Object -TypeName Amazon.CloudFormation.Model.Tag
    $tag.Key = "Application"
    $tag.Value = "Pollster-CodeDeploy"

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
    $param4.ParameterKey = "OpenRemoteDesktopPort"

    if ($openRDP) 
    {
        $param4.ParameterValue = "Yes"
    }
    else 
    {
        $param4.ParameterValue = "No"
    }

    $parameters = $param1, $param2, $param3, $param4


    $stackId = New-CFNStack -StackName $stackName -Capability "CAPABILITY_IAM" -Parameter $parameters -Tag $tag -TemplateBody $templateBody
    $stackId
}

function _SetupCodeDeployResources([string]$serviceRole, [string]$autoScalingGroupName)
{    
    _SetupCodeDeployApplication "Pollster-PollFeed" $serviceRole $autoScalingGroupName
    _SetupCodeDeployApplication "Pollster-PollVoter" $serviceRole $autoScalingGroupName
    _SetupCodeDeployApplication "Pollster-PollWebFrontend" $serviceRole $autoScalingGroupName
    _SetupCodeDeployApplication "Pollster-PollWorkflow" $serviceRole $autoScalingGroupName
    _SetupCodeDeployApplication "Pollster-PollWriter" $serviceRole $autoScalingGroupName
}


function _SetupCodeDeployApplication([string]$applicationName,[string]$serviceRole, [string]$autoScalingGroupName)
{
    $deploymentGroupName = ($applicationName + "-Fleet")

    $existingApplication = (Get-CDApplicationList) | Where {$_ -eq $applicationName}

    if ($existingApplication -ne $null)
    {
        ("CodeDeploy application " + $applicationName + " already existing and is being removed before recreating it")
        Remove-CDApplication -ApplicationName $applicationName -Force    
    }


    $applicationId = New-CDApplication -ApplicationName $applicationName
    $deploymentGroupId = New-CDDeploymentGroup -ApplicationName $applicationName -DeploymentGroupName $deploymentGroupName -AutoScalingGroup $autoScalingGroupName -DeploymentConfigName "CodeDeployDefault.AllAtOnce" -ServiceRoleArn $serviceRole

    ("CodeDeploy " + $applicationName + " application created")
}


function ProcessInput([string]$instanceType, [string]$stackName,[string]$keyPair,[bool]$openRDPPort)
{
    $stackId = _LaunchCloudFormationStack $instanceType $stackName $keyPair $openRDPPort
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

    $serviceRoleARN = ""
    $loadBalancerDNS = ""
    $autoScalingGroupName = (Get-CFNStackResource -StackName $stackId -LogicalResourceId "AutoScalingGroup").PhysicalResourceId
    ForEach($output in $stack.Outputs)
    {
        if($output.OutputKey -eq "CodeDeployTrustRoleARN")
        {
            $serviceRoleARN = $output.OutputValue
        }
        elseif($output.OutputKey -eq "LoadBalancerDNSName")
        {
            $loadBalancerDNS = $output.OutputValue        
        }
    }


    _SetupCodeDeployResources $serviceRoleARN $autoScalingGroupName
    ("CodeDeploy environment setup complete")
    ("Load Balancer DNS: " + $loadBalancerDNS)
}


ProcessInput "t2.large" "Pollster-CodeDeploy1" "pollster-codedeploy" $true
function _deleteStack()
{
    $stack = (Get-CFNStack -StackName "Pollster-DockerECS" | Where-Object {$_.StackName -eq "Pollster-DockerECS"})
    if($stack -ne $null)
    {
        Write-Host "Deleting existing stack"
        Remove-CFNStack $stack.StackName -Force
    }
}

function _deleteCodeDeployPrimitives()
{
    $applications = Get-CDApplicationList | Where-Object {$_.StartsWith("Pollster-")}
    foreach($application in $applications)
    {
        $deploymentGroups = Get-CDDeploymentGroupList -ApplicationName $application
        foreach($deploymentGroup in $deploymentGroups.DeploymentGroups)
        {
            Write-Host ("Deleting deployment group " + $deploymentGroup)
            Remove-CDDeploymentGroup -ApplicationName $application -DeploymentGroupName $deploymentGroup -Force
        }

        Write-Host ("Deleting DockerECS application " + $application)
        Remove-CDApplication -ApplicationName  $application -Force
    }
}

_deleteCodeDeployPrimitives
_deleteStack
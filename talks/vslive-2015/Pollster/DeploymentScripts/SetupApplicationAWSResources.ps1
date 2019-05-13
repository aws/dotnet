# This script sets up the DynamoDB tables and Simple Workflow primitives for Pollster to use

$existingTables = Get-DDBTables

if (($existingTables | Where-Object {$_ -eq "PollDefinition"}) -eq $null)
{
    $schema = New-DDBTableSchema 
    $schema = Add-DDBKeySchema -Schema $schema -KeyName "Id" -KeyDataType "S" -KeyType "HASH"
    $schema = Add-DDBIndexSchema -Schema $schema -IndexName "AuthorEmail" -HashKeyName "AuthorEmail" -HashKeyDataType "S" -Global -ReadCapacity 5 -WriteCapacity 1 -ProjectionType KEYS_ONLY
    $table = New-DDBTable -TableName "PollDefinition" -Schema $schema -ReadCapacity 5 -WriteCapacity 1
    Write-Host "Created PollDefinition table"
}

if (($existingTables | Where-Object {$_ -eq "ActivePoll"}) -eq $null)
{
    $schema = New-DDBTableSchema 
    $schema = Add-DDBKeySchema -Schema $schema -KeyName "Id" -KeyDataType "S" -KeyType "HASH"
    $table = New-DDBTable -TableName "ActivePoll" -Schema $schema -ReadCapacity 5 -WriteCapacity 1
    Write-Host "Created ActivePoll table"
}

$credentials = New-Object -TypeName Amazon.Runtime.StoredProfileAWSCredentials -ArgumentList $StoredAWSCredentials
$region = [Amazon.RegionEndpoint]::GetBySystemName($StoredAWSRegion)
$swfClient = New-Object -TypeName Amazon.SimpleWorkflow.AmazonSimpleWorkflowClient -ArgumentList ($credentials,$region)

$domainName = "Pollster"
$workflowTypeName = "PollScheduler"
$workflowVersion = "3.0"
$taskListName = "schedule"
$defaultTimeout = "604800"

$startActivtyName = "StartTimerExpired"
$startActivityVersion = "1.0"

$endActivtyName = "EndTimerExpired"
$endActivityVersion = "1.0"


$listRequest = (New-Object -TypeName Amazon.SimpleWorkflow.Model.ListDomainsRequest)
$listRequest.RegistrationStatus = [Amazon.SimpleWorkflow.RegistrationStatus]::REGISTERED
$existing = $swfClient.ListDomains($listRequest).DomainInfos.Infos | Where-Object {$_.Name -eq $domainName}
if ($existing -eq $null)
{
    $registerRequest = New-Object -TypeName Amazon.SimpleWorkflow.Model.RegisterDomainRequest
    $registerRequest.Name = $domainName
    $registerRequest.WorkflowExecutionRetentionPeriodInDays = "7"
    $registerResponse = $swfClient.RegisterDomain($registerRequest)
    Write-Host ("Registered " + $registerRequest.Name + " SWF Domain")
}

$listRequest = New-Object -TypeName Amazon.SimpleWorkflow.Model.ListWorkflowTypesRequest
$listRequest.Domain = $domainName
$listRequest.RegistrationStatus = [Amazon.SimpleWorkflow.RegistrationStatus]::REGISTERED
$existing = $swfClient.ListWorkflowTypes($listRequest).WorkflowTypeInfos.TypeInfos | Where-Object {$_.WorkflowType.Name -eq $workflowTypeName}
if ($existing -eq $null)
{
    $registerRequest = New-Object -TypeName Amazon.SimpleWorkflow.Model.RegisterWorkflowTypeRequest
    $registerRequest.Domain = $domainName
    $registerRequest.Version = $workflowVersion
    $registerRequest.Name = $workflowTypeName
    $registerRequest.DefaultChildPolicy = [Amazon.SimpleWorkflow.ChildPolicy]::TERMINATE
    $registerRequest.DefaultTaskList = New-Object -TypeName Amazon.SimpleWorkflow.Model.TaskList
    $registerRequest.DefaultTaskList.Name = $taskListName
    $registerRequest.DefaultTaskStartToCloseTimeout = $defaultTimeout
    $registerRequest.DefaultExecutionStartToCloseTimeout = $defaultTimeout
    $registerResponse = $swfClient.RegisterWorkflowType($registerRequest)
    Write-Host ("Registered " + $registerRequest.Name + " SWF Workflow Type")
}

$listRequest = New-Object -TypeName Amazon.SimpleWorkflow.Model.ListActivityTypesRequest
$listRequest.Domain = $domainName
$listRequest.RegistrationStatus = [Amazon.SimpleWorkflow.RegistrationStatus]::REGISTERED
$existing = $swfClient.ListActivityTypes($listRequest).ActivityTypeInfos.TypeInfos | Where-Object {$_.ActivityType.Name -eq $startActivtyName}
if ($existing -eq $null)
{
    $registerRequest = New-Object -TypeName Amazon.SimpleWorkflow.Model.RegisterActivityTypeRequest
    $registerRequest.Domain = $domainName
    $registerRequest.Version = $startActivityVersion
    $registerRequest.Name = $startActivtyName
    $registerResponse = $swfClient.RegisterActivityType($registerRequest)
    Write-Host ("Registered " + $registerRequest.Name + " Activity")
}

$existing = $swfClient.ListActivityTypes($listRequest).ActivityTypeInfos.TypeInfos | Where-Object {$_.ActivityType.Name -eq $endActivtyName}
if ($existing -eq $null)
{
    $registerRequest = New-Object -TypeName Amazon.SimpleWorkflow.Model.RegisterActivityTypeRequest
    $registerRequest.Domain = $domainName
    $registerRequest.Version = $endActivityVersion
    $registerRequest.Name = $endActivtyName
    $registerResponse = $swfClient.RegisterActivityType($registerRequest)
    Write-Host ("Registered " + $registerRequest.Name + " Activity")
}

# Checks for an existing role with the specified name and creates the role if
# it does not already exist. Once the role has been established, an assume
# role policy is attached.
function _setupRoleWithAssumeRolePolicy([string]$roleName)
{
    $assumeRolePolicy =	@"
{
    "Version":"2012-10-17",
    "Statement":[
        {
            "Effect":"Allow",
            "Principal":{
                "Service":["ec2.amazonaws.com"]
                },
                "Action":["sts:AssumeRole"]
            }
        ]
}
"@

    $role = (Get-IAMRoles | ? { $_.RoleName -eq $roleName })
    if ($role -eq $null)
    {
        $role = New-IAMRole -RoleName $roleName -AssumeRolePolicyDocument $assumeRolePolicy
    }

    $roleName
}

# Tests to make sure the user has set a default AWS region
function _testDefaultRegionIsSet()
{
    if ((Get-DefaultAWSRegion) -eq $null)
    {
        throw "You must set a region via Set-DefaultAWSRegion before running this cmdlet."
    }
}


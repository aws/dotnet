Import-Module .\Helpers\Helpers.psm1

<#
.Synopsis
   Uploads and imports a virtual machine to AWS using the AWS Tools for Windows PowerShell.
.DESCRIPTION
   Uploads a VM in a supported format to Amazon S3 and then requests import conversion to an Amazon EC2 
   'AMI' using Import-EC2Image.

   This cmdlet formed the first stage of the demo in the DEV202 breakout session at AWS re:Invent 2015. 
   The output from the function is a ConversionTask instance from EC2 that details the task id and other 
   information about the requested conversion. 
   
   All resources created using this cmdlet use the value specified in the ResourceGroupName parameter in 
   either the name or applied as a 'Name' tag for later identification except for the AMI resulting from
   conversion - the tag needs to be applied manually once AMI creation is complete.
.Notes
   This cmdlet assumes a default region in which to make calls to AWS has been set using 
   Set-DefaultAWSRegion.
.EXAMPLE
   Import-VMToAWS -ResourceGroupName myvm -VMFile C:\myVM.vhd -BucketName mybucket
.EXAMPLE
   Import-VMToAWS -ResourceGroupName myvm -VMFile C:\myVM.vhd -BucketName mybucket -ForceUpload
#>
function Import-VMToAWS
{
    [CmdletBinding()]
    [OutputType([Amazon.EC2.Model.ImportImageTask])]
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
        [string]$BucketName
    )

    Process
    {
        _testDefaultRegionIsSet

        if (!(Test-Path -Path $VMFile))
        {
            throw "The specified VM file cannot be found"
        }

        if (!(Test-S3Bucket -BucketName $BucketName))
        {
            Write-Verbose "Bucket $BucketName does not exist, creating"
            New-S3Bucket -BucketName $BucketName
        }

        # first we upload the VM file, if necessary, to S3
        $objectkey = _uploadVMToS3 $ResourceGroupName $VMFile $BucketName

        # next we grant EC2 credentials and access to the uploaded file
        $roleName = _grantEC2AccessToS3Object $ResourceGroupName $BucketName $objectkey $RoleSuffix
        
        # final step is to request EC2 fetch and convert the VM. We use the file
        # extension of the VM to determine the type. This also assumes the
        # license type (bring your own) and that the VM consists of a single
        # disk (use Import-EC2Snapshot to upload additional disk images).
        $vmfileextension = ([System.IO.Path]::GetExtension($VMFile)).TrimStart('.')

        $windowsContainer = New-Object Amazon.EC2.Model.ImageDiskContainer
        $windowsContainer.Format=$vmfileextension

        $userBucket = New-Object Amazon.EC2.Model.UserBucket
        $userBucket.S3Bucket = $BucketName
        $userBucket.S3Key = $objectkey

        $windowsContainer.UserBucket = $userBucket

        $params = @{
            "Description"="Imported via Import-VMToAWS on " + (Get-Date)
            "Platform"="Windows"
            "LicenseType"="BYOL"
            "RoleName"=$roleName
        }

        Import-EC2Image -DiskContainer $windowsContainer @params
    }
}

# Helper function to upload the VM file to Amazon S3. The bucket is assumed to exist
# at this point. Returns the key of the uploaded object.
# If the object exists in S3, a check is made on file size and date to determine
# if the local copy is newer/different - if they are the same, no upload is
# performed
function _uploadVMToS3([string]$resourceGroupName, 
                       [string]$vmFile, 
                       [string]$bucketName)
{
    # construct the key we'll use to identify the object in the S3 bucket
    $vmfilename = [System.IO.Path]::GetFileName($vmFile)
    $s3objectkey = $resourceGroupName + "/" + $vmfilename

    # if an object already exists with that key, don't upload again unless
    # the user has told us to
    $s3Object = Get-S3Object -BucketName $bucketName -Key $s3objectkey
    $doUpload = $true
    if ($s3Object -ne $null)
    {
        $file = Get-ChildItem $vmFile
        if ($file.Length -eq $s3Object.Size -And ([DateTime]::Compare($file.LastWriteTime, $s3Object.LastModified) -le 0))
        {
            $doUpload = $false
        }
    }

    if ($doUpload)
    {
        Write-Verbose "VM does not exist in Amazon S3 with key $s3objectkey, or local copy is different - uploading..."
        Write-S3Object -BucketName $bucketName -Key $s3objectkey -File $vmFile
    }

    $s3objectkey
}

# Ensures a role with associated policies is set up so that EC2 can access the uploaded VM
# image in S3. The role name consists of the value of the resourceGroupName parameter suffixed
# with 'VMImport'. If a role with the name exists it is updated with a new policy granting 
# access to the object in S3 otherwise a new role is created and policy attached.
# The function returns the name of the created/modified role.
function _grantEC2AccessToS3Object([string]$resourceGroupName,
                                   [string]$bucketName, 
                                   [string]$s3ObjectKey, 
                                   [string]$roleSuffix)
{
    $roleName = $resourceGroupName + "VMImport"

    # add an assume role policy granting credentials to EC2 to work on our behalf; the
    # external id must be 'vmimport'.
    $assumeRolePolicy = @"
{
    "Version":"2012-10-17",
    "Statement":[
        {
            "Sid":"",
            "Effect":"Allow",
            "Principal":{
                "Service":"vmie.amazonaws.com"
            },
            "Action":"sts:AssumeRole",
            "Condition":{
                "StringEquals":{
                    "sts:ExternalId":"vmimport"
                }
            }
        }
    ]
}
"@

    $role = (Get-IAMRoles | ? { $_.RoleName -eq $roleName })
    if ($role -eq $null)
    {
        $role = New-IAMRole -RoleName $roleName -AssumeRolePolicyDocument $assumeRolePolicy
    }

    # attach a policy granting EC2, acting under the credentials it receives courtesy of
    # the assume role policy attached to the role, access to the uploaded VM asset in S3
    # and the service APIs (both in S3 and EC2) to perform the import
    $rolePolicy = @"
{
   "Version":"2012-10-17",
   "Statement":[
      {
         "Effect":"Allow",
         "Action":[
            "s3:ListBucket",
            "s3:GetBucketLocation"
         ],
         "Resource":[
            "arn:aws:s3:::$bucketName"
         ]
      },
      {
         "Effect":"Allow",
         "Action":[
            "s3:GetObject"
         ],
         "Resource":[
            "arn:aws:s3:::$bucketName/$s3ObjectKey"
         ]
      },
      {
         "Effect":"Allow",
         "Action":[
            "ec2:ModifySnapshotAttribute",
            "ec2:CopySnapshot",
            "ec2:RegisterImage",
            "ec2:Describe*"
         ],
         "Resource":"*"
      }
   ]
}
"@

    $policyName = $roleName + "InlinePolicy"
    Write-Verbose "Adding inline role policy $policyName to role $roleName, granting EC2 access to resources and apis."
    $params = @{
        "RoleName"=$roleName
        "PolicyName"=$policyName
        "PolicyDocument"=$rolePolicy
    }
    Write-IAMRolePolicy @params

    # need to pause briefly to allow propagation
    Write-Verbose "Pausing to allow for role update propagation..."
    Start-Sleep -Seconds 2

    $roleName
}

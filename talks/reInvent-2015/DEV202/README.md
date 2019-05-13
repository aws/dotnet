# Under the Desk to the AWS Cloud with Windows PowerShell

This folder contains the module we used during the AWS re:Invent 2015 presentation. It contains the cmdlets and functions we stepped through as well as a single 'do everything' function that will run and coordinate the upload, import conversion, VPC setup, instance launch and post-launch configuration steps.

To use the module, place it where PowerShell can locate it (or add the location to your PSModulePath environment variable) and run 'Import-Module AWSreInvent2015'. The module requires that the AWS Tools for Windows PowerShell are installed.


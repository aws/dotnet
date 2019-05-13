$archiveRoot = [System.IO.Path]::Combine($PSScriptRoot, "..\..\..\")
$dnxPath = [System.IO.Path]::Combine($archiveRoot, "runtimes\dnx-clr-win-x64.1.0.0-beta8\bin\dnx.exe")
$arguments = "--project .\src\PollWorkflow --configuration Release PollWorkflow"

# -RedirectStandardError ($PSScriptRoot + "\stderr.log") -RedirectStandardOutput ($PSScriptRoot + "\stdout.log")

(Start-Process $dnxPath $arguments -PassThru -WorkingDirectory $archiveRoot ).Id | 
    Out-File ($archiveRoot + "\last-pid.txt")
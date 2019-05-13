$archiveRoot = [System.IO.Path]::Combine($PSScriptRoot, "..\..\..\..\")
$batchFilePath = (Get-ChildItem -Path $archiveRoot -Filter Poll*.cmd).FullName
$content = Get-Content $batchFilePath -Raw
$content = $content.Trim().Substring(1)
$content = $content.Replace("%~dp0", "./")
$content = $content.Replace("%*", "")
$pos = $content.IndexOf("--appbase")

$command = $content.Substring(1, $pos - 3).Trim()
$arguments = $content.Substring($pos).Trim()
$fullPath = [System.IO.Path]::Combine($archiveRoot, $command)

# -RedirectStandardError ($PSScriptRoot + "\stderr.log") -RedirectStandardOutput ($PSScriptRoot + "\stdout.log")

(Start-Process $fullPath $arguments -PassThru -WorkingDirectory $archiveRoot ).Id | 
    Out-File ($archiveRoot + "\last-pid.txt")
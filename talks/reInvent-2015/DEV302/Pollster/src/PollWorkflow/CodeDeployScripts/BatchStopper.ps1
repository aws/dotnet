$archiveRoot = [System.IO.Path]::Combine($PSScriptRoot, "..\..\..\..\")
$lastPidFilePath = (Get-ChildItem -Path $archiveRoot -Filter last-pid.txt).FullName
if (!$lastPidFilePath) 
{ 
    Write-Output "no last-pid.txt file so nothing to shutdown" 
}
else
{
    $lastPid = (Get-Content $lastPidFilePath -Raw).Trim()
    $process = Get-Process -Id $lastPid
    if($process) 
    {
        Write-Output ("Stopping existing process: " + $lastPid)
        Stop-Process -Id $lastPid
    }
    else
    {
        Write-Output "Last running process appears to no longer be running"
    }
}
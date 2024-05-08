#Requires -RunAsAdministrator

$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
Set-Location $scriptPath

$serviceName = "SdHostApi"

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if($null -ne $service)
{
    if($service.Status -eq "Running")
    {
        $service.Stop()
    }
    Remove-Service -Name $serviceName
}

& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" /property:Configuration=Release

New-Service -Name $serviceName -BinaryPathName "$scriptPath\bin\Release\net8.0\SdHostApi.exe" -StartupType Automatic
Start-Service -Name $serviceName
param($ProjectDir, $ConfigurationName, $TargetDir, $TargetFileName, $SolutionDir)

$documentsFolder = [environment]::getfolderpath("mydocuments");
if($ConfigurationName -like "Debug15")
{
    $DestinationFolder = "$documentsFolder\WindowsPowerShell\Modules\SharePointPnPPowerShell2013"
} elseif($ConfigurationName -like "Debug16")
{
    $DestinationFolder = "$documentsFolder\WindowsPowerShell\Modules\SharePointPnPPowerShell2016"
} else {
    $DestinationFolder = "$documentsFolder\WindowsPowerShell\Modules\SharePointPnPPowerShellOnline"
}
    

Write-Host "Creating target folder: $DestinationFolder"
New-Item -Path $DestinationFolder -ItemType Directory -Force >$null # Suppress output


Write-Host "Copying files from $TargetDir to $DestinationFolder"
Try {
    Copy-Item "$TargetDir\SoSP.PnPProvisioningExtensions.Core.*" -Destination "$DestinationFolder"
}
Catch
{
    exit 1
}

$currentDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$VerbosePreference = "Continue"

@(
    "$currentDir\Build\SharePointPnPPowerShell2013"
    "$currentDir\Build\SharePointPnPPowerShell2016"
    "$currentDir\Build\SharePointPnPPowerShellOnline"
) | Remove-Item -Force -Recurse -Confirm:$false


Copy-item -Recurse "$currentDir\Src\SoSP.PnPProvisioningExtensions\SoSP.PnPProvisioningExtensions.Core\bin\Release15"  "$currentDir\Build\SharePointPnPPowerShell2013"
Copy-item -Recurse "$currentDir\Src\SoSP.PnPProvisioningExtensions\SoSP.PnPProvisioningExtensions.Core\bin\Release16"  "$currentDir\Build\SharePointPnPPowerShell2016"
Copy-item -Recurse "$currentDir\Src\SoSP.PnPProvisioningExtensions\SoSP.PnPProvisioningExtensions.Core\bin\Release"    "$currentDir\Build\SharePointPnPPowerShellOnline"

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("2013","2016","Online")]
    [string]$Version
)

$CurrentDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$binRoot = "$CurrentDirectory\SharePointPnPPowerShell$Version"
$moduleAssembly = [System.Reflection.Assembly]::LoadFrom("$binRoot\SharePointPnP.PowerShell.$Version.Commands.dll")

Import-Module -Assembly $moduleAssembly


Add-Type -Path "$binRoot\SoSP.PnPProvisioningExtensions.Core.dll"

$extHandlers = @(
    New-PnPExtensibilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.MetadataNavigationHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensibilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.DocumentSetHomePageHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensibilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.SearchNavigationHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensibilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.WebPartPagesHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"

)

Write-Host "Custom SoSP handlers available in variable $$extHandlers"

$extHandlers | ft -AutoSize Type
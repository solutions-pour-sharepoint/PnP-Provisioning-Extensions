$moduleAssembly = [System.Reflection.Assembly]::LoadFrom("$pwd\SharePointPnP.PowerShell.Online.Commands.dll")

Import-Module -Assembly $moduleAssembly


Add-Type -Path SoSP.PnPProvisioningExtensions.Core.dll

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




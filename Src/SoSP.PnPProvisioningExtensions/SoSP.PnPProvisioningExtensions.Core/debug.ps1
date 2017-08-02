$moduleAssembly = [System.Reflection.Assembly]::LoadFrom("$([Environment]::GetFolderPath('MyDocuments'))\WindowsPowerShell\Modules\SharePointPnPPowerShellOnline\OfficeDevPnP.Core.dll")

Import-Module -Assembly $moduleAssembly

Connect-PnPOnline http://tenant.sharepoint.com/sites/somesite

Add-Type -Path SoSP.PnPProvisioningExtensions.Core.dll

$extHandlers = @(
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.MetadataNavigationHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.DocumentSetHomePageHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"

)




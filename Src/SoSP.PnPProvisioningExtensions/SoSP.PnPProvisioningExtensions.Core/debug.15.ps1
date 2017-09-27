$moduleAssembly = [System.Reflection.Assembly]::LoadFrom("$([Environment]::GetFolderPath('MyDocuments'))\WindowsPowerShell\Modules\SharePointPnPPowerShell2013\OfficeDevPnP.Core.dll")

Import-Module -Assembly $moduleAssembly

Connect-PnPOnline -CurrentCredentials http://sbdev02/sites/pnpsrc03

Add-Type -Path SoSP.PnPProvisioningExtensions.Core.dll


$listContentConfig = '[
{
    "ListName":  "Une liste avec espaces",
    "KeyFieldName":  "Title",
    "UpdateBehavior":  0
}
]'

$extHandlers = @(
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.ListContentHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" `
                                     -Configuration $listContentConfig
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.MetadataNavigationHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.DocumentSetHomePageHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.WebPartPagesHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"

)




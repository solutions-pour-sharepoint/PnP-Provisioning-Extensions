$moduleAssembly = [System.Reflection.Assembly]::LoadFrom("$pwd\SharePointPnP.PowerShell.2013.Commands.dll")

Import-Module -Assembly $moduleAssembly

Add-Type -Path SoSP.PnPProvisioningExtensions.Core.dll

<#
$listContentConfig = '[
{
    "ListName":  "Une liste avec espaces",
    "KeyFieldName":  "Title",
    "UpdateBehavior":  0
}
]'#>

$extHandlers = @(
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.ListContentHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.MetadataNavigationHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.DocumentSetHomePageHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.SearchNavigationHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
    New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.WebPartPagesHandler `
                                     -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"

)




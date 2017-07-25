$moduleAssembly = [System.Reflection.Assembly]::LoadFrom("$([Environment]::GetFolderPath('MyDocuments'))\WindowsPowerShell\Modules\SharePointPnPPowerShell2016\OfficeDevPnP.Core.dll")

Import-Module -Assembly $moduleAssembly

Connect-PnPOnline -CurrentCredentials http://someserver/sites/pnpsource

Add-Type -Path SoSP.PnPProvisioningExtensions.Core.dll

$extHandler = New-PnPExtensbilityHandlerObject -Type SoSP.PnPProvisioningExtensions.Core.MetadatanavigationProvider `
                                               -Assembly "SoSP.PnPProvisioningExtensions.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" 

$extHandlers = @(
    $extHandler
)




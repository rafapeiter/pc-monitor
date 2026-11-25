param([switch]$InterfaceOnly)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskCsc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskBase=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskCommon=@('/nologo','/platform:x64','/optimize+','/codepage:65001','/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Management.dll')
if(-not $InterfaceOnly){
& $taskCsc @taskCommon '/target:library' ('/out:'+(Join-Path $taskRoot 'PCMonitor.Core.dll')) (Join-Path $taskRoot 'src\BuildInfo.cs') (Join-Path $taskRoot 'src\Core.cs') (Join-Path $taskRoot 'src\Disks.cs') (Join-Path $taskRoot 'src\Bridge.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o núcleo.'}
& $taskCsc @taskCommon '/target:exe' ('/out:'+(Join-Path $taskRoot 'PCMonitor.Mcp.exe')) ('/reference:'+(Join-Path $taskRoot 'PCMonitor.Core.dll')) (Join-Path $taskRoot 'src\Mcp.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar MCP.'}
}
if(Test-Path (Join-Path $taskRoot 'src\Dashboard.cs')){
 $taskWpf=@(('/reference:'+(Join-Path $taskBase 'WPF\PresentationFramework.dll')),('/reference:'+(Join-Path $taskBase 'WPF\PresentationCore.dll')),('/reference:'+(Join-Path $taskBase 'WPF\WindowsBase.dll')),('/reference:'+(Join-Path $taskBase 'System.Xaml.dll')),'/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll',('/reference:'+(Join-Path $taskRoot 'ModernWpf.dll')),('/reference:'+(Join-Path $taskRoot 'ModernWpf.Controls.dll')),('/reference:'+(Join-Path $taskRoot 'PCMonitor.Core.dll')))
 & $taskCsc @taskCommon @taskWpf '/target:winexe' ('/out:'+(Join-Path $taskRoot 'PCMonitor.exe')) ('/win32icon:'+(Join-Path $taskRoot 'src\app.ico')) ('/win32manifest:'+(Join-Path $taskRoot 'src\app.manifest')) (Join-Path $taskRoot 'src\Program.cs') (Join-Path $taskRoot 'src\Dashboard.cs') (Join-Path $taskRoot 'src\Appearance.cs') (Join-Path $taskRoot 'src\Tests.cs')
 if($LASTEXITCODE -ne 0){throw 'Falha ao compilar interface.'}
 Copy-Item -LiteralPath (Join-Path $taskRoot 'src\Dashboard.xaml') -Destination (Join-Path $taskRoot 'Dashboard.xaml') -Force
}
Write-Output 'Compilação concluída.'

# Atualiza o servidor distribuído com o pacote de integração.
$taskPluginServer = Join-Path $taskRoot 'integration\pc-monitor-plugin\server'
New-Item -ItemType Directory -Path $taskPluginServer -Force | Out-Null
foreach ($taskPluginFile in @('PCMonitor.Mcp.exe', 'PCMonitor.Mcp.exe.config', 'PCMonitor.Core.dll')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskPluginFile) -Destination $taskPluginServer -Force
}

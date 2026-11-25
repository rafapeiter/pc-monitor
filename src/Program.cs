using System;using System.Diagnostics;using System.IO;using System.Linq;using System.Security.Cryptography;using System.Text;using System.Threading;using System.Windows;using ModernWpf;
[assembly:System.Reflection.AssemblyTitle("PC Monitor")]
[assembly:System.Reflection.AssemblyVersion("2.0.2.0")]
[assembly:System.Reflection.AssemblyFileVersion("2.0.2.0")]
namespace PCMonitor {
 public static class Program {
  public const string Version=BuildInfo.Version;
  [STAThread] public static int Main(string[] args){string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PCMonitor");int at=Array.IndexOf(args,"--data-dir");if(at>=0&&at+1<args.Length)root=Path.GetFullPath(args[at+1]);
   if(args.Contains("--self-test"))return SelfTest.Run(root);
   if(args.Contains("--enable-startup")){Startup.Set(true,Process.GetCurrentProcess().MainModule.FileName);return 0;}
   if(args.Contains("--disable-startup")){Startup.Set(false,Process.GetCurrentProcess().MainModule.FileName);return 0;}
   if(args.Contains("--shutdown")){try{BridgeClient.Call(root,"shutdown",new{});return 0;}catch{return 2;}}
   if(args.Contains("--status-json")){try{Console.WriteLine(Json.Write(BridgeClient.Call(root,"status",new{})));return 0;}catch(Exception ex){Console.WriteLine(Json.Write(new{online=false,error=Util.Clean(ex.Message)}));return 2;}}
   if(args.Contains("--collect-once")){try{using(var engine=new Engine(root)){Thread.Sleep(12500);File.WriteAllText(Path.Combine(root,"collection-result.json"),Json.Write(new{success=engine.Latest!=null,metrics=engine.Latest}));return engine.Latest==null?1:0;}}catch(Exception ex){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"collection-error.txt"),ex.ToString());return 1;}}
   if(args.Contains("--bridge-test")){using(var engine=new Engine(root))using(var bridge=new MonitorBridge(engine,null,null)){Thread.Sleep(18000);return 0;}}
   bool trayOnly=args.Contains("--tray");
   int delayAt=Array.IndexOf(args,"--startup-delay");if(trayOnly&&delayAt>=0){int seconds;if(delayAt+1>=args.Length||!Int32.TryParse(args[delayAt+1],out seconds)||seconds<0||seconds>300)return 2;Thread.Sleep(seconds*1000);}
   bool preview=args.Contains("--render-preview");bool created=true;Mutex mutex=null;
   if(!preview){string token;using(var sha=SHA256.Create())token=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(root))).Replace("-","").Substring(0,24);mutex=new Mutex(true,"Local\\PCMonitor-"+token,out created);if(!created){if(trayOnly){mutex.Dispose();return 0;}try{BridgeClient.Call(root,"open",new{});}catch{MessageBox.Show("O PC Monitor já está em execução. Abra-o pelo ícone na bandeja.","PC Monitor");}mutex.Dispose();return 0;}}
   Engine live=null;try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(@"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:ui=""http://schemas.modernwpf.com/2019""><ResourceDictionary.MergedDictionaries><ui:ThemeResources/><ui:XamlControlsResources/></ResourceDictionary.MergedDictionaries></ResourceDictionary>");ThemeManager.Current.ApplicationTheme=Settings.Read(root).theme=="light"?ApplicationTheme.Light:ApplicationTheme.Dark;ThemeManager.Current.AccentColor=System.Windows.Media.Color.FromRgb(88,224,187);
    live=new Engine(root);var engine=live;if(args.Contains("--updated"))engine.Mark("application_updated","info","Atualização solicitada para PC Monitor "+BuildInfo.Version,new{note="O monitor anterior foi encerrado para aplicar a atualização; um aviso de término anterior nesta transição não significa falha do computador."});app.DispatcherUnhandledException+=(s,e)=>{engine.Mark("ui_error","error","Falha na interface",new{error=Util.Clean(e.Exception.Message)});e.Handled=true;};var window=new Dashboard(engine,trayOnly,preview);app.MainWindow=window;
    if(preview){window.RenderPreview(root);return 0;}
    // Run the dispatcher without ever showing a window during automatic startup.
    return trayOnly?app.Run():app.Run(window);
   }catch(Exception ex){Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"startup-error.txt"),Util.Clean(ex.ToString()));if(!preview&&!trayOnly)MessageBox.Show("Não foi possível iniciar o PC Monitor.\n"+Util.Clean(ex.Message),"PC Monitor");return 1;}finally{if(live!=null)live.Dispose();if(mutex!=null)mutex.Dispose();}
  }
 }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PCMonitor {
 public static class Json {
  public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=4*1024*1024,RecursionLimit=80};}
  public static string Write(object x){return Serializer().Serialize(x);}
  public static Dictionary<string,object> Read(string s){var d=Serializer().DeserializeObject(s) as Dictionary<string,object>;if(d==null)throw new ArgumentException("Esperado objeto JSON.");return d;}
  public static object Get(Dictionary<string,object> d,string k){object v;return d!=null&&d.TryGetValue(k,out v)?v:null;}
  public static string Text(Dictionary<string,object> d,string k,string fallback=""){object v=Get(d,k);return v==null?fallback:Convert.ToString(v,Util.Inv);}
  public static string ReadLine(TextReader r,int max){var s=new StringBuilder();int c;while((c=r.Read())!=-1){if(c==10)return s.ToString().TrimEnd('\r');if(s.Length>=max)throw new ArgumentException("Mensagem excede o limite.");s.Append((char)c);}return s.Length==0?null:s.ToString();}
 }
 public sealed class LogRepository {
  public readonly string Root;public LogRepository(string root){Root=Path.GetFullPath(root);}
  public object Metadata(){try{return Json.Read(File.ReadAllText(Path.Combine(Root,"system.json"),Encoding.UTF8));}catch{return new{available=false};}}
  public Dictionary<string,object> Latest(){return Rows("metrics",DateTime.MinValue,1,null).FirstOrDefault();}
  public List<Dictionary<string,object>> Rows(string kind,DateTime since,int limit,string provider,DateTime? until=null){
   if(!new[]{"metrics","incidents","windows-events"}.Contains(kind))throw new ArgumentException("Tipo de registro inválido.");
   limit=Math.Max(1,Math.Min(6000,limit));string dir=Path.Combine(Root,"logs");var result=new List<Dictionary<string,object>>();if(!Directory.Exists(dir))return result;
   long budget=32*1024*1024;foreach(var f in new DirectoryInfo(dir).GetFiles(kind+"-*.jsonl").OrderByDescending(f=>f.LastWriteTimeUtc)){
    if(budget<=0)break;long allowance=Math.Min(4*1024*1024,budget);budget-=Math.Min(f.Length,allowance);
    foreach(string line in Tail(f.FullName,allowance).Reverse()){
     Dictionary<string,object> row;try{row=Json.Read(line);}catch{continue;}
     DateTime stamp;if(!DateTime.TryParse(Json.Text(row,"timestamp_utc"),null,System.Globalization.DateTimeStyles.RoundtripKind,out stamp))continue;
     if(stamp.ToUniversalTime()<since||(until.HasValue&&stamp.ToUniversalTime()>until.Value))continue;if(!String.IsNullOrEmpty(provider)&&Json.Text(row,"provider").IndexOf(provider,StringComparison.OrdinalIgnoreCase)<0)continue;
     result.Add(row);if(result.Count>=limit)break;
    }if(result.Count>=limit)break;
   }return result.OrderByDescending(r=>Json.Text(r,"timestamp_utc")).ToList();
  }
  static string[] Tail(string path,long max){try{using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){long start=Math.Max(0,f.Length-max);f.Position=start;using(var r=new StreamReader(f,new UTF8Encoding(false,false))){if(start>0)r.ReadLine();return r.ReadToEnd().Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);}}}catch{return new string[0];}}
 }
 public static class BridgeClient {
  public static string PipeName(string root){using(var sha=SHA256.Create()){string id=WindowsIdentity.GetCurrent().User.Value+"|"+Path.GetFullPath(root).TrimEnd('\\').ToLowerInvariant();return "PCMonitor-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-","").Substring(0,28);}}
  public static object Call(string root,string command,object args){using(var pipe=new NamedPipeClientStream(".",PipeName(root),PipeDirection.InOut,PipeOptions.Asynchronous)){
   pipe.Connect(1200);using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true))using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true)){
    writer.WriteLine(Json.Write(new{command=command,arguments=args}));writer.Flush();var task=reader.ReadLineAsync();if(!task.Wait(4000))throw new TimeoutException("O monitor não respondeu a tempo.");if(task.Result==null)throw new IOException("Resposta local vazia.");var reply=Json.Read(task.Result);if(!Object.Equals(Json.Get(reply,"ok"),true))throw new InvalidOperationException(Json.Text(reply,"error","Falha no monitor."));return Json.Get(reply,"data");
   }
  }}
 }
 public sealed class MonitorBridge:IDisposable {
  readonly Engine engine;readonly Action show,shutdown;readonly Thread worker;volatile bool stopped;NamedPipeServerStream active;public string LastCallUtc="";public string Status="iniciando";
  public MonitorBridge(Engine engine,Action show,Action shutdown){this.engine=engine;this.show=show;this.shutdown=shutdown;worker=new Thread(Loop){IsBackground=true,Name="PCMonitor local bridge"};worker.Start();}
  void Loop(){while(!stopped){try{
   var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
   using(var pipe=new NamedPipeServerStream(BridgeClient.PipeName(engine.DataRoot),PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,65536,65536,security)){
    active=pipe;Status="pronto";pipe.WaitForConnection();using(var deadline=new System.Threading.Timer(o=>{try{pipe.Dispose();}catch{}},null,5000,Timeout.Infinite))using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true))using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true)){
     object reply;try{var req=Json.Read(Json.ReadLine(reader,65536)??"{}");LastCallUtc=Util.Utc();reply=new{ok=true,data=Handle(Json.Text(req,"command"),Json.Get(req,"arguments") as Dictionary<string,object>)};}catch(Exception ex){reply=new{ok=false,error=Util.Clean(ex.Message)};}
     writer.WriteLine(Json.Write(reply));writer.Flush();
    }
   }
  }catch(Exception ex){if(!stopped){Status="indisponível: "+ex.GetType().Name;Thread.Sleep(500);}}finally{active=null;}}
   Status="encerrado";
  }
  object Handle(string command,Dictionary<string,object> args){switch(command){
   case "status":return new{online=true,version=BuildInfo.Version,startup_enabled=Startup.Current()!=null,session_id=engine.Store.Session,paused=engine.Paused,last_sample_utc=engine.Latest==null?null:engine.Latest.timestamp_utc,log_error=engine.Store.LastError,events_status=engine.Events.Status,log_size_bytes=engine.Store.Size(),capabilities=engine.Latest==null?null:engine.Latest.capabilities};
   case "metrics":return engine.Latest;
   case "mark":string note=Json.Text(args,"note");if(note.Length>1000)throw new ArgumentException("Observação deve ter até 1000 caracteres.");engine.Mark("user_marker","info","Travamento marcado via MCP",new{note=Util.Clean(note),source="mcp",user_reported=true});return new{recorded=true,timestamp_utc=Util.Utc()};
   case "open":if(show!=null)show();return new{opened=true};
   case "shutdown":if(shutdown!=null)ThreadPool.QueueUserWorkItem(o=>{Thread.Sleep(200);shutdown();});return new{stopping=true};
   default:throw new ArgumentException("Comando local não reconhecido.");
  }}
  public void Dispose(){stopped=true;try{if(active!=null)active.Dispose();}catch{}worker.Join(1500);}
 }
 public static class Startup {
  const string keyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public static string Current(){try{using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))return k==null?null:k.GetValue("PCMonitor") as string;}catch{return null;}}
  public static void Set(bool enabled,string executable){using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath)){if(enabled)key.SetValue("PCMonitor","\""+Path.GetFullPath(executable)+"\" --tray --startup-delay 30");else key.DeleteValue("PCMonitor",false);}}
 }
}

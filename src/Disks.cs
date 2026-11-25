using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace PCMonitor {
 internal interface IDiskQuery:IDisposable {int Count{get;} List<DiskActivity> Read();}
 internal sealed class DiskCollector:IDisposable {
  readonly Func<IDiskQuery> factory;readonly Func<DateTime> clock;IDiskQuery query;DateTime retryAt=DateTime.MinValue;
  public string Status="warming_up";public string LastError="";
  public DiskCollector():this(()=>new NativeDiskQuery(),()=>DateTime.UtcNow){}
  internal DiskCollector(Func<IDiskQuery> factory,Func<DateTime> clock){this.factory=factory;this.clock=clock;}
  public List<DiskActivity> Read(){
   if(query==null&&clock()<retryAt)return new List<DiskActivity>();
   try{
    if(query==null){query=factory();Status="warming_up";LastError="";return new List<DiskActivity>();}
    var values=query.Read();if(values.Count==0)throw new InvalidOperationException("PDH: nenhuma amostra válida dos discos.");
    Status=values.Count==query.Count?"ok":"partial";LastError="";return values;
   }catch(Exception ex){LastError=Util.Clean(ex.Message);Status="unavailable";if(query!=null)query.Dispose();query=null;retryAt=clock().AddSeconds(30);return new List<DiskActivity>();}
  }
  public void Dispose(){if(query!=null){query.Dispose();query=null;}}
 }
 internal sealed class NativeDiskQuery:IDiskQuery {
  const uint MoreData=0x800007D2,DoubleFormat=0x00000200|0x00008000;
  [StructLayout(LayoutKind.Sequential)] internal struct Formatted {public uint Status;public double Value;}
  // Prefix of PDH_COUNTER_INFO_W, including its version DWORD and native pointer alignment.
  [StructLayout(LayoutKind.Sequential)] internal struct CounterInfo {public uint Length,Type,Version,Status;public int Scale,DefaultScale;public UIntPtr UserData,QueryUserData;public IntPtr FullPath,Machine,Object;}
  [DllImport("pdh.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern uint PdhOpenQueryW(string source,UIntPtr user,out IntPtr query);
  [DllImport("pdh.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern uint PdhAddEnglishCounterW(IntPtr query,string path,UIntPtr user,out IntPtr counter);
  [DllImport("pdh.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern uint PdhGetCounterInfoW(IntPtr counter,[MarshalAs(UnmanagedType.Bool)] bool explain,ref uint bytes,IntPtr info);
  [DllImport("pdh.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern uint PdhEnumObjectItemsW(string source,string machine,string name,IntPtr counters,ref uint counterChars,IntPtr instances,ref uint instanceChars,uint detail,uint flags);
  [DllImport("pdh.dll",ExactSpelling=true)] static extern uint PdhRemoveCounter(IntPtr counter);
  [DllImport("pdh.dll",ExactSpelling=true)] static extern uint PdhCollectQueryData(IntPtr query);
  [DllImport("pdh.dll",ExactSpelling=true)] static extern uint PdhGetFormattedCounterValue(IntPtr counter,uint format,out uint type,out Formatted value);
  [DllImport("pdh.dll",ExactSpelling=true)] static extern uint PdhCloseQuery(IntPtr query);
  IntPtr handle;readonly List<Tuple<string,IntPtr,IntPtr,IntPtr>> counters=new List<Tuple<string,IntPtr,IntPtr,IntPtr>>();
  public int Count{get{return counters.Count;}}
  public NativeDiskQuery(){try{
   Check(PdhOpenQueryW(null,UIntPtr.Zero,out handle),"abrir consulta");
   IntPtr probe=Add(@"\PhysicalDisk(_Total)\% Idle Time");string objectName;
   try{objectName=LocalizedObject(probe);}finally{PdhRemoveCounter(probe);}
   foreach(string name in Instances(objectName).Where(n=>n!="_Total")){
    string prefix=@"\PhysicalDisk("+name+@")\";
    counters.Add(Tuple.Create(name,Add(prefix+"% Idle Time"),Add(prefix+"Disk Read Bytes/sec"),Add(prefix+"Disk Write Bytes/sec")));
   }
   if(counters.Count==0)throw new InvalidOperationException("PDH: nenhum disco físico enumerado.");
   Check(PdhCollectQueryData(handle),"primeira amostra");
  }catch{Dispose();throw;}}
  IntPtr Add(string path){IntPtr h;Check(PdhAddEnglishCounterW(handle,path,UIntPtr.Zero,out h),"adicionar contador "+path);return h;}
  static void Check(uint status,string stage){if(status!=0)throw new InvalidOperationException("PDH: "+stage+" (0x"+status.ToString("X8",Util.Inv)+")");}
  static string LocalizedObject(IntPtr h){uint bytes=0;uint status=PdhGetCounterInfoW(h,false,ref bytes,IntPtr.Zero);if(status!=MoreData)Check(status,"tamanho do nome localizado");
   if(bytes<Marshal.SizeOf(typeof(CounterInfo))||bytes>1024*1024)throw new InvalidOperationException("PDH: tamanho de informação inválido.");
   IntPtr b=Marshal.AllocHGlobal((int)bytes);try{Check(PdhGetCounterInfoW(h,false,ref bytes,b),"nome localizado");var info=(CounterInfo)Marshal.PtrToStructure(b,typeof(CounterInfo));string name=Marshal.PtrToStringUni(info.Object);if(String.IsNullOrEmpty(name))throw new InvalidOperationException("PDH: nome de objeto vazio.");return name;}finally{Marshal.FreeHGlobal(b);}}
  static string[] Instances(string name){for(int attempt=0;attempt<3;attempt++){
   uint nc=0,ni=0;uint status=PdhEnumObjectItemsW(null,null,name,IntPtr.Zero,ref nc,IntPtr.Zero,ref ni,100,0);if(status!=MoreData)Check(status,"enumerar discos (tamanho)");
   if(nc>1024*1024||ni>1024*1024)throw new InvalidOperationException("PDH: lista excessiva de contadores.");
   IntPtr c=Marshal.AllocHGlobal((int)Math.Max(2,nc)*2),i=Marshal.AllocHGlobal((int)Math.Max(2,ni)*2);
   try{status=PdhEnumObjectItemsW(null,null,name,c,ref nc,i,ref ni,100,0);if(status==MoreData)continue;Check(status,"enumerar discos");return ni>1?Marshal.PtrToStringUni(i,(int)ni).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries):new string[0];}finally{Marshal.FreeHGlobal(c);Marshal.FreeHGlobal(i);}
  }throw new InvalidOperationException("PDH: instâncias alteradas durante a enumeração.");}
  static double? Value(IntPtr counter){uint type;Formatted f;uint status=PdhGetFormattedCounterValue(counter,DoubleFormat,out type,out f);if(status!=0||(f.Status!=0&&f.Status!=1)||Double.IsNaN(f.Value)||Double.IsInfinity(f.Value))return null;return f.Value;}
  public List<DiskActivity> Read(){Check(PdhCollectQueryData(handle),"coletar amostra");var list=new List<DiskActivity>();foreach(var d in counters){double? idle=Value(d.Item2),read=Value(d.Item3),write=Value(d.Item4);if(!idle.HasValue||!read.HasValue||!write.HasValue)continue;list.Add(new DiskActivity{disk=d.Item1,active_percent=Util.Clamp(100-idle.Value),read_bytes_sec=Math.Max(0,read.Value),write_bytes_sec=Math.Max(0,write.Value)});}return list;}
  public void Dispose(){if(handle!=IntPtr.Zero){PdhCloseQuery(handle);handle=IntPtr.Zero;}counters.Clear();}
 }
 internal sealed class FakeDiskQuery:IDiskQuery {
  internal bool fail;public int Count{get{return 1;}}public bool Disposed;
  public List<DiskActivity> Read(){if(fail)throw new InvalidOperationException("simulated read failure");return new List<DiskActivity>{new DiskActivity{disk="0 C:",active_percent=42,read_bytes_sec=1024,write_bytes_sec=2048}};}
  public void Dispose(){Disposed=true;}
 }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace KeyLearner.Studio;
/// <summary>Session-only accessibility shortcut suppression with crash restoration.</summary>
public sealed class AccessibilitySession : IDisposable
{
    public sealed record Lease(uint Get,uint Set,uint[] Original,uint[] Applied);
    private readonly List<Lease> leases=new();
    private readonly string file;
    public AccessibilitySession(string root)
    {
        file=Path.Combine(root,"accessibility-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            foreach(var (get,set,size) in new[]{(0x3Au,0x3Bu,2),(0x32u,0x33u,6),(0x34u,0x35u,2)})
            {
                var values=Read(get,size);var applied=(uint[])values.Clone();
                applied[1]&=~12u; // Only shortcut and confirmation flags; preserve enabled features.
                leases.Add(new(get,set,values,applied));
            }
            File.WriteAllText(file,JsonSerializer.Serialize(leases));
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
            if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))info.ArgumentList.Add(typeof(AccessibilitySession).Assembly.Location);
            info.ArgumentList.Add("--restore-accessibility");info.ArgumentList.Add(file);
            info.ArgumentList.Add(Environment.ProcessId.ToString());info.ArgumentList.Add(Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks.ToString());
            using var guardian=Process.Start(info)??throw new IOException("Could not start accessibility restoration helper.");
            foreach(var lease in leases)Write(lease.Set,lease.Applied);
        }
        catch{Dispose();throw;}
    }
    private static uint[] Read(uint action,int size)
    {
        var values=new uint[size];values[0]=(uint)(size*4);
        if(!SystemParametersInfo(action,values[0],values,0))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return values;
    }
    private static void Write(uint action,uint[] values){if(!SystemParametersInfo(action,values[0],values,0))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
    public static void RestoreFile(string path)
    {
        if(!File.Exists(path))return;
        var leases=JsonSerializer.Deserialize<List<Lease>>(File.ReadAllText(path))??new();
        foreach(var lease in leases)
        {
            var current=Read(lease.Get,lease.Original.Length);
            if((current[1]&12)==(lease.Applied[1]&12)){current[1]=(current[1]&~12u)|(lease.Original[1]&12u);Write(lease.Set,current);}
        }
        File.Delete(path);
    }
    public static void Watch(string[] args)
    {
        try
        {
            using var parent=Process.GetProcessById(int.Parse(args[2]));
            if(parent.StartTime.ToUniversalTime().Ticks==long.Parse(args[3]))parent.WaitForExit();
        }
        catch(ArgumentException){}catch(InvalidOperationException){}
        RestoreFile(args[1]);
    }
    public void Dispose(){RestoreFile(file);}
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SystemParametersInfo(uint action,uint param,[In,Out]uint[] values,uint flags);
}

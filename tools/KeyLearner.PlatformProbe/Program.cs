using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using KeyLearner.Studio;

var normalRoot=KeyLearner.Unity.Platform.ProfileLocation.Resolve(false,"");
var expectedRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KeyLearner");
if(normalRoot!=expectedRoot)throw new InvalidOperationException("Normal/standalone studio profile changed.");
var previewA=KeyLearner.Unity.Platform.ProfileLocation.Resolve(true,"");
var previewB=KeyLearner.Unity.Platform.ProfileLocation.Resolve(true,"");
if(previewA==normalRoot||previewA==previewB||!previewA.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Preview profile is not unique and isolated.");
var explicitRoot=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"KeyLearner-profile-contract-probe"));
if(KeyLearner.Unity.Platform.ProfileLocation.Resolve(true,explicitRoot)!=explicitRoot)throw new InvalidOperationException("Explicit preview profile was ignored.");
if(KeyLearner.Unity.Platform.ProfileLocation.Resolve(true,expectedRoot)!=expectedRoot)throw new InvalidOperationException("Installer studio real-profile override was ignored.");
Console.WriteLine("PASS: normal/studio identity, unique preview isolation, explicit disposable override, installer real-profile override; no profile files written.");

// Disarmed only: installs native callbacks but never activates capture or changes accessibility flags.
using(var guard=new KeyboardGuard(IntPtr.Zero,false))
{
    Thread.Sleep(100);
    if(!guard.TryReadDesktopInput(out _))throw new InvalidOperationException("Desktop input query failed.");
    if(guard.Snapshot().Count!=0 || guard.TryRead(out _))throw new InvalidOperationException("Disarmed probe retained key input.");
}
Console.WriteLine("PASS: native foreground/desktop hooks installed and released; disarmed input stayed empty.");
if(args.Length==0)return;
var helperPath=Path.GetFullPath(args[0]);
var name="KeyLearner-probe-"+Guid.NewGuid().ToString("N");
using var current=Process.GetCurrentProcess();
using var helper=Process.Start(new ProcessStartInfo(helperPath,$"--speech {name} {current.Id} {current.StartTime.ToUniversalTime().Ticks}"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}) ?? throw new InvalidOperationException("Speech helper did not launch.");
using var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
pipe.Connect(15000);
using var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);
using var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};
using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
var ready=await reader.ReadLineAsync(timeout.Token);
if(ready==null||!ready.StartsWith("READY\t"))throw new InvalidOperationException("Speech helper did not warm.");
int voices=Encoding.UTF8.GetString(Convert.FromBase64String(ready.Split('\t')[1])).Split('\n',StringSplitOptions.RemoveEmptyEntries).Length;
// Zero volume verifies actual synthesizer completion without playing audio in the user's session.
writer.WriteLine("SPEAK\t0\t42\t0\t0\t\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes("a")));
var done=await reader.ReadLineAsync(timeout.Token);
if(done!="DONE\t0\t42")throw new InvalidOperationException("Speech completion ticket did not match.");
writer.WriteLine("CANCEL");writer.WriteLine("QUIT");
if(!helper.WaitForExit(5000)||helper.ExitCode!=0)throw new InvalidOperationException("Speech helper failed clean shutdown.");
Console.WriteLine($"PASS: {voices} offline Windows voices; local current-user pipe; zero-volume synthesis completion; clean shutdown.");

using Microsoft.Xna.Framework;
using System.Diagnostics;
namespace KeyLearner.Studio;
public sealed partial class StudioGame
{
    bool benchmarking;double benchmarkStart,benchmarkEmission;
    private Settings? benchmarkSettings;
    private FlightModel benchmarkFlight=new();
    PlayMode benchmarkMode;readonly List<double> benchmarkFrames=new();
    string benchmarkResult="Run a six-second GPU workload to recommend settings.";
    string recommendation="";
    void StartBenchmark()
    {
        if(benchmarking)return;benchmarkSettings=System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(S));benchmarkFlight=new();benchmarkMode=S.Mode;S.RenderScale=1;S.LiquidScale=1;S.TerrainDetail=64;S.ExtrudedAssets=true;S.GlassShader=true;S.SmoothEdges=true;S.GentleMotion=false;S.ParticleLimit=650;benchmarking=true;benchmarkStart=KeyboardGuard.Now;benchmarkEmission=0;benchmarkFrames.Clear();canvas.Clear();parent=false;S.Mode=PlayMode.BirdFlight;benchmarkFlight.SetWord("sky");
        graphics.SynchronizeWithVerticalRetrace=false;graphics.ApplyChanges();IsFixedTimeStep=false;renderSize=default;
    }
    void UpdateBenchmark()
    {
        if(!benchmarking)return;var elapsed=KeyboardGuard.Now-benchmarkStart;
        if(elapsed>3){S.Mode=PlayMode.SmashGarden;if(elapsed-benchmarkEmission>.08){benchmarkEmission=elapsed;var c=new InputContext(Gesture.Sweep,1,random.NextDouble(),random.NextDouble(),.3,.2,1,1,[]);canvas.Emit(((char)('A'+random.Next(26))).ToString(),c,W,H);canvas.GestureEffect(c,W,H);canvas.GestureEffect(c with{Gesture=Gesture.BroadMash},W,H);}}
        if(elapsed>6)FinishBenchmark();
    }
    void FinishBenchmark()
    {
        if(!benchmarking)return;benchmarking=false;RestoreBenchmarkSettings();parent=true;tab=8;canvas.Clear();graphics.SynchronizeWithVerticalRetrace=S.VSync;graphics.ApplyChanges();IsFixedTimeStep=true;
        var timings=benchmarkFrames.OrderBy(x=>x).ToArray();if(timings.Length<30){benchmarkResult="Test interrupted before enough frames were collected.";return;}
        double p95=timings[(int)((timings.Length-1)*.95)];recommendation=p95<9?"High":p95<16?"Balanced":"Performance";
        benchmarkResult=$"95% of frames: {p95:0.0} ms. Suggested: {recommendation}.";
    }
    void RestoreBenchmarkSettings(){if(benchmarkSettings==null)return;foreach(var property in typeof(Settings).GetProperties())if(property.CanWrite)property.SetValue(S,property.GetValue(benchmarkSettings));benchmarkSettings=null;}
    void DrawBenchmarkControls()
    {

        Button(new(1130,390,245,48),"Run benchmark",StartBenchmark);
        Text(benchmarkResult.Split(" Suggested:")[0],1125,455,canvas.Palette[0],.34f,maxWidth:260);
        if(recommendation.Length>0)Text("Suggested: "+recommendation,1125,484,Color.White,.38f,maxWidth:260);
        if(recommendation.Length>0)Button(new(1130,515,245,48),"Apply "+recommendation,()=>{S.RenderScale=recommendation=="Performance"?.75:1;S.LiquidScale=recommendation=="High"?1:recommendation=="Balanced"?.7:.5;S.TerrainDetail=recommendation=="High"?80:recommendation=="Balanced"?56:32;notice="Recommendation applied. Save & return to keep it.";});
        Text("Flight assist gently guides",1130,610,Color.White*.6f,.36f);Text("toward the next letter.",1130,637,Color.White*.6f,.36f);
        Text("Measured with GPU readback;",1130,696,Color.White*.5f,.34f);Text("not a guaranteed frame rate.",1130,719,Color.White*.5f,.34f);
    }
}

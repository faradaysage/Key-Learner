using KeyLearner.Studio;
int checks=0;
void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); checks++; Console.WriteLine("PASS "+name); }
ParentAction Attempt(int[] keys,double duration=.8,int? extra=null)
{
    var chord=new ParentChord();double t=0;ParentAction action=ParentAction.None;
    foreach(var key in keys) { chord.Feed(new(key,true,t)); t+=.01; }
    if(extra!=null) {chord.Feed(new(extra.Value,true,t+.1));chord.Feed(new(extra.Value,false,t+.2));}
    foreach(var key in keys.Reverse()) {var a=chord.Feed(new(key,false,t+duration)); if(a!=ParentAction.None)action=a; }
    return action;
}
Check(Attempt([162,164,27])==ParentAction.Exit,"exact exit chord");
Check(Attempt([163,165,79])==ParentAction.Options,"right-side options chord");
Check(Attempt([162,164,27],.1)==ParentAction.None,"short accidental chord rejected");
Check(Attempt([162,164,27],.8,160)==ParentAction.None,"extra Shift rejects entire chord");
for(var key=8;key<256;key++) if(key is not (162 or 164 or 27))
    Check(Attempt([162,164,27],.8,key)==ParentAction.None,"extra key "+key+" cannot exit");
Check(Attempt([65,162,164,27])==ParentAction.None,"mash before chord poisons attempt");
var releaseChord=new ParentChord();
releaseChord.Feed(new(162,true,0));releaseChord.Feed(new(164,true,.1));releaseChord.Feed(new(27,true,.2));
Check(releaseChord.Feed(new(27,false,1))==ParentAction.None,"partial release never exits");
releaseChord.Feed(new(160,true,1.1));releaseChord.Feed(new(160,false,1.2));releaseChord.Feed(new(164,false,1.3));
Check(releaseChord.Feed(new(162,false,1.4))==ParentAction.None,"extra key during release cancels");
Check(Attempt([162,163,164,27])==ParentAction.None,"both Control keys not accepted");
var root=Path.Combine(Path.GetTempPath(),"KeyLearner-tests-"+Guid.NewGuid());
var store=new Store(root);store.Words.Clear();
foreach(var word in new[]{"milk","mom","mommy","dad","daddy","cat","cot","cart"})store.Words.Add(new(){Word=word,Adventure=true});
var context=new InputContext(Gesture.Deliberate,.8,.5,.5,0,0,.2,1,new double[6]);
var rapid=context with{Gesture=Gesture.Rapid};
var recognizer=new WordRecognizer(store);
List<string> Type(string text,double start=0,double interval=.2,InputContext? input=null,string? target=null)
{
    var results=new List<string>();
    foreach(var c in text){var result=recognizer.Add(c,start,input??context,target);if(result!=null)results.Add(result.Word);start+=interval;}
    return results;
}
Check(Type("milk",0,.07,rapid).Contains("milk"),"fast exact typing recognizes milk immediately");
recognizer.Flush();
Check(Type("mom",1).SequenceEqual(new[]{"mom"}),"unlearned mom speaks immediately");
Check(Type("my",1.7).SequenceEqual(new[]{"mommy"}),"spoken mom retains prefix for mommy");
recognizer.Flush();
var firstDelay=recognizer.DelayFor("mom");
Check(firstDelay>0,"one mommy completion starts learning a mom delay");
for(var n=0;n<5;n++){Type("mommy",3+n*2);recognizer.Flush();}
var learnedDelay=recognizer.DelayFor("mom");
Check(learnedDelay>firstDelay && learnedDelay<=store.Settings.PrefixPause,"repeated mommy progressively lengthens mom wait");
Check(recognizer.DelayFor("dad")==0,"mom habit does not delay dad");
Check(Type("mom",20,.12).Count==0,"learned mom is held pending");
Check(recognizer.Update(20.25)==null,"prefix timer waits");
Check(Type("my",20.35,.12).SequenceEqual(new[]{"mommy"}),"continuation cancels pending mom and speaks mommy immediately");
Check(recognizer.Update(21)==null,"no stale mom notification after mommy");
recognizer.Flush();
Check(Type("mom",23,.1).Count==0,"learned prefix waits on next attempt");
Check(recognizer.Add('d',23.31,context)?.Word=="mom","incompatible next letter immediately resolves pending mom");
Check(Type("addy",23.5,.1).Contains("daddy"),"daddy can immediately follow mom without separator");
recognizer.Flush();
for(var n=0;n<12;n++){Type("mom",30+n*4,.15);recognizer.Flush();}
Check(recognizer.DelayFor("mom")==0,"repeated standalone mom reverses learning to zero");
Check(Type("mom",82).Contains("mom"),"mom is immediate again");
recognizer.Flush();
Check(Type("mommy",85,.1,rapid,"mommy").SequenceEqual(new[]{"mommy"}),"guided target suppresses mom and completes mommy immediately");
Check(recognizer.Buffer=="","guided completion resets for next target");
Check(Type("daddy",87,.1,rapid,"daddy").SequenceEqual(new[]{"daddy"}),"guided daddy immediate without dad interruption");
Type("mom",89,.1,context,"mommy");
Check(recognizer.Add('d',89.4,context,"mommy")==null,"wrong guided prefix never announces mom");
recognizer.Reset();
Type("miolk",92);
Check(recognizer.Update(94)?.Word=="milk","extra letter typo corrected after pause");
Type("mlik",95);
Check(recognizer.Update(98)?.Word=="milk","transposed typo corrected");
Type("cort",99);
Check(recognizer.Update(102)==null,"ambiguous corrections rejected");
recognizer.Reset();
recognizer.Add('m',103,context);recognizer.Add('i',103.1,context with{Gesture=Gesture.BroadMash,Held=5});
recognizer.Add('l',103.2,context);recognizer.Add('k',103.3,context);
Check(recognizer.Update(106)==null,"actual concurrent mashing clears the spelling buffer");
recognizer.Reset();
Type("mil",107);recognizer.Backspace();
Check(Type("lk",108).Contains("milk"),"backspace works before word completion");
recognizer.Flush();
store.Settings.AdaptiveLearning=false;
Check(Type("mom",110).Contains("mom"),"disabling adaptation restores immediate exact matches");
recognizer.Flush();store.Settings.AdaptiveLearning=true;
var blobs=new BlobWorld();
blobs.Add(new(100,100),new(10,0),10,new(1,0,0),5,50);
blobs.Add(new(110,100),new(10,0),10,new(0,0,1),5,50);
blobs.Step(.001f,1000,800,0,0,50);
Check(blobs.Drops.Count==1 && blobs.Fusions==1,"slow touching droplets fuse");
Check(Math.Abs(blobs.Drops[0].Mass-200)<.01,"fusion conserves droplet area");
Check(Math.Abs(blobs.Drops[0].Velocity.X-10)<.1,"fusion conserves momentum");
var split=new BlobWorld();
split.Add(new(100,100),new(200,0),20,new(1,0,0),5,50);
split.Add(new(125,100),new(-200,0),10,new(0,0,1),5,50);
split.Step(.001f,1000,800,0,.5f,50);
Check(split.Splits==1 && split.Drops.Count==3,"strong collision separates a large drop");
Check(Math.Abs(split.Drops.Sum(d=>d.Mass)-500)<.1,"splitting conserves droplet area");
recognizer.Reset();store.Profile.PrefixHabits["mom"]=4;
Type("mom",120,.2);
Check(recognizer.Update(121.5)?.Word=="mom","standalone learned prefix eventually speaks");
Check(recognizer.Update(124)==null && store.Profile.PrefixHabits["mom"]==3,"idle standalone use reduces delay exactly once");
recognizer.Update(130);
Check(store.Profile.PrefixHabits["mom"]==3,"later idle frames do not retrain the same episode");
store.Settings.KeyIcons[112]="cat";
var counter=new CountingRecognizer();
for(var i=1;i<=9;i++) Check(counter.Add((char)('0'+i),i)==i,"count "+i);
Check(counter.Add('1',10)==null,"ten waits for zero");
Check(counter.Pending=="1" && counter.Expected==10,"ten retains pending first digit");
Check(counter.Add('0',10.4)==10,"ten commits as one number");
Check(counter.Add('1',11)==null && counter.Add('1',11.3)==11,"eleven follows ten");
var analyzer=new GestureAnalyzer();
foreach(var key in new[]{81,87,69,82}) analyzer.Add(key,key*.05,1,store.Gestures.Network,false);
Check(analyzer.Current.Gesture==Gesture.Sweep,"horizontal keyboard sweep");
analyzer.Reset();
foreach(var pair in new[]{(65,1),(76,2),(81,3),(80,4),(77,5)}) analyzer.Add(pair.Item1,.01*pair.Item2,pair.Item2,store.Gestures.Network,false);
Check(analyzer.Current.Gesture==Gesture.BroadMash,"broad simultaneous mashing");
var network=new TinyNetwork();
for(var i=0;i<1000;i++) {network.Train([0,0,0,0,0,0],0);network.Train([1,1,1,0,0,0],3);}
Check(network.Predict([0,0,0,0,0,0]).Label==0 && network.Predict([1,1,1,0,0,0]).Label==3,"tiny neural model learns labeled examples");
store.Settings.Theme=Mood.Lagoon;store.Settings.Gravity=double.NaN;store.Gestures.Network=network;
Check(store.Save(),"atomic store save");
var restored=new Store(root);
Check(restored.Settings.KeyIcons.GetValueOrDefault(112)=="cat","parent icon remapping persists");
Check(restored.Settings.Theme==Mood.Lagoon && double.IsFinite(restored.Settings.Gravity),"settings roundtrip and validation");
Check(restored.Profile.PrefixHabits.Count>0,"prefix habits persist across launches");
Check(restored.Words.Any(w=>w.Word=="milk"),"dictionary roundtrip");
Check(restored.Gestures.Network.Samples==network.Samples,"neural weights persist");
File.WriteAllText(Path.Combine(root,"settings.json"),"{broken");
Check(new Store(root).Settings.Theme==Mood.PrimaryColors,"corrupt settings recover");
var switched=new ParentChord();
switched.Feed(new(162,true,0));switched.Feed(new(164,true,.1));switched.Feed(new(27,true,.2));
switched.Feed(new(27,false,1));switched.Feed(new(79,true,1.1));switched.Feed(new(79,false,2));switched.Feed(new(164,false,2.1));
Check(switched.Feed(new(162,false,2.2))==ParentAction.None,"changing target during release cannot authorize");
var balloon=new BalloonMotion();balloon.Inflate();balloon.Step(.008f);
Check(balloon.Size<1,"balloon squeezes before inflating");
for(int i=0;i<360;i++)balloon.Step(1f/120);
Check(Math.Abs(balloon.Target-1)<.001f,"one puff deflates in three seconds");
var measured=new BalloonMotion();
for(int tap=0;tap<18 && !measured.Popped;tap++){measured.Inflate();for(int i=0;i<120;i++)measured.Step(1f/120);}
Check(measured.Popped,"measured one-second taps reach a pop reward");
var slow=new BalloonMotion();
for(int tap=0;tap<12;tap++){slow.Inflate();for(int i=0;i<3600;i++)slow.Step(1f/120);}
Check(!slow.Popped && Math.Abs(slow.Target-1)<.001f,"thirty-second taps cannot accumulate pressure");
var retired=new BalloonMotion();for(int i=0;i<5;i++)retired.Inflate();retired.Release();var pressure=retired.Target;retired.Inflate();
Check(retired.Target==pressure,"retired balloon cannot reinflate");
for(int i=0;i<480;i++)retired.Step(1f/120);
Check(Math.Abs(retired.Size-1)<.01f && !retired.Popped,"retired balloon returns to original size without popping");
var lowLimit=new BalloonMotion();for(int i=0;i<4;i++)lowLimit.Inflate();for(int i=0;i<120;i++)lowLimit.Step(1f/120,3,1.5f);
Check(lowLimit.Popped,"parent pop-size setting changes reward threshold");
Check(new Settings().Theme==Mood.PrimaryColors && new Settings().Backdrop==Backdrop.Starfield,"primary colors and flight starfield are defaults");
var starfield=new StarfieldMotion();for(int frame=0;frame<600;frame++)starfield.Step(1f/60,3,false);
Check(Enumerable.Range(0,2400).All(i=>{var p=starfield.Project(i,false);return float.IsFinite(p.X) && float.IsFinite(p.Y) && p.Z>0;}),"flight stars recycle with finite perspective");
Check(Enumerable.Range(0,2400).All(i=>{var p=starfield.Project(i,true);return float.IsFinite(p.X) && float.IsFinite(p.Y) && p.Z>0;}),"rotating star cloud stays in front of the camera");
File.WriteAllText(Path.Combine(root,"settings.json"),"{\"Theme\":0,\"Backdrop\":1}");
var migrated=new Store(root);
Check(migrated.Settings.Theme==Mood.PrimaryColors && migrated.Settings.Backdrop==Backdrop.Starfield,"older default profile migrates to primary colors and stars");
migrated.Settings.Theme=Mood.Aurora;migrated.Settings.Backdrop=Backdrop.Plasma;migrated.Save();
var chosen=new Store(root);
Check(chosen.Settings.Theme==Mood.Aurora && chosen.Settings.Backdrop==Backdrop.Plasma,"later explicit theme choice survives restart");
Console.WriteLine($"All {checks} checks passed. Test data: {root}");
var exitTaps=new EscapeExit();for(int i=0;i<9;i++){Check(!exitTaps.Feed(new(27,true,i)),"Escape down cannot quit");Check(!exitTaps.Feed(new(27,false,i)),"fewer than ten taps cannot quit");}exitTaps.Feed(new(27,true,10));Check(exitTaps.Feed(new(27,false,10)),"ten released Escape taps quit");
exitTaps.Reset();for(int i=0;i<100;i++)exitTaps.Feed(new(27,true,i));exitTaps.Feed(new(27,false,101));Check(exitTaps.Count==1,"held Escape repeats count once");exitTaps.Feed(new(65,true,102));Check(exitTaps.Count==0,"another key resets emergency exit");
var transitions=new KeyTransitionBuffer();for(int i=0;i<50000;i++)transitions.Push(new(65,true,i));Check(transitions.Pending==1 && transitions.Recoveries==0,"autorepeat storm stays bounded");transitions.Push(new(65,false,50001));while(transitions.TryRead(out _)){}
for(int i=0;i<2000;i++){transitions.Push(new(65,true,i));transitions.Push(new(65,false,i));}transitions.Push(new(162,true,2001));Check(transitions.TryRead(out var recovered)&&recovered.Key==-1,"overflow emits state reset instead of exiting");Check(transitions.TryRead(out recovered)&&recovered.Key==162&&recovered.Down,"overflow rebuilds current held keys");Check(!transitions.TryRead(out _),"released storm keys do not stick");transitions.Push(new(162,false,2002));Check(transitions.TryRead(out recovered)&&!recovered.Down,"release after recovery survives");
foreach(int n in new[]{1,2,10,50,100}){var schedule=new FireworkSchedule();schedule.Add(n,10);Check(schedule.Due(9)==0,"rockets never launch before count");int launched=0;for(double t=10;t<=15.1;t+=.017)launched+=schedule.Due(t);Check(launched==n&&schedule.Pending==0,"exact rocket count within five seconds: "+n);}
var overlapping=new FireworkSchedule();overlapping.Add(10,0);overlapping.Add(20,1);Check(overlapping.Due(6)==30,"new count preserves earlier scheduled rockets");
var rewardsTest=new BalloonReward();rewardsTest.Start(5);for(int i=0;i<4;i++)Check(!rewardsTest.Pop(),"bonus waits for last balloon");Check(rewardsTest.Score==40&&rewardsTest.Pop()&&rewardsTest.Score==75,"last pop awards round bonus once");Check(!rewardsTest.Pop()&&rewardsTest.Score==75,"empty round cannot score again");
var glassTest=new GlassDamage();glassTest.Hit(1);for(int i=0;i<180;i++)glassTest.Step(1f/60);Check(glassTest.Amount==0,"quiet heals glass");int broken=0;for(int i=0;i<16;i++){if(glassTest.Hit(1))broken++;glassTest.Step(.02f);}Check(broken==1,"sustained mash shatters once, with cooldown");
var gestureTest=new GestureAnalyzer();var gp=new TinyNetwork();InputContext gc=default;int gi=0;foreach(var k in "ASDF")gc=gestureTest.Add(k,gi++*.03,3,gp,false);Check(gc.Gesture==Gesture.Cluster,"overlap produces paint cluster");gestureTest.Reset();gi=0;foreach(var k in "QAZPLM")gc=gestureTest.Add(k,gi++*.025,6,gp,false);Check(gc.Gesture==Gesture.BroadMash,"broad overlapping mash cracks glass");gestureTest.Reset();gi=0;foreach(var k in "QWERTY")gc=gestureTest.Add(k,gi++*.09,1,gp,false);Check(gc.Gesture==Gesture.Sweep,"straight trace produces liquid");gc=gestureTest.Add(65,3,1,gp,false);Check(gc.Gesture==Gesture.Deliberate,"quiet restores deliberate typing");
Console.WriteLine($"All {checks} checks passed including gesture playground regressions.");
Check(Attempt([162,164,79],.04)==ParentAction.Options,"options opens on quick exact chord release");
Check(Attempt([162,164,79],.04,160)==ParentAction.None,"quick options chord still rejects extra Shift");
var rollover=new KeyTransitionBuffer();for(int i=65;i<85;i++)rollover.Push(new(i,true,0));int simultaneous=0;while(rollover.TryRead(out var transition)){if(transition.Down)simultaneous++;}Check(simultaneous==20,"all twenty distinct held keys survive transport");for(int i=65;i<85;i++)rollover.Push(new(i,false,1));int releases=0;while(rollover.TryRead(out var transition)){if(!transition.Down)releases++;}Check(releases==20,"all twenty releases survive transport");
var biased=new TinyNetwork();for(int i=0;i<1000;i++)biased.Train(new double[]{.4,.1,.5,.4,.2,.3},2);var typing=new GestureAnalyzer();for(int i=0;i<100;i++){var intent=typing.Add("MILK"[i%4],i*.13,1,biased,true);Check(intent.Gesture is Gesture.Deliberate or Gesture.Rapid or Gesture.Sweep,"single-key typing cannot become learned Cluster");}
var paneTest=new GlassSheet();float sheetArea=1440*900;for(int i=0;i<12;i++){paneTest.Hit(new(300+i*50,300),1);Check(Math.Abs(paneTest.Panes.Sum(p=>p.Area)-sheetArea)<2,"fracture conserves sheet area");Check(paneTest.Panes.All(p=>p.Area>0 && p.Points.All(v=>v.X>=-.01f && v.Y>=-.01f&&v.X<=1440.01f&&v.Y<=900.01f)),"fractures stay within sheet");}
var paneVertices=paneTest.Panes.SelectMany(p=>p.Points).ToArray();int attempts=0;while(!paneTest.Falling&&attempts++<100)paneTest.Hit(new(720,450),1);Check(paneTest.Falling,"distributed pressure eventually compromises sheet");var atBreak=paneTest.Panes.Select(p=>p.Points.ToArray()).ToArray();paneTest.Step(.1f,false);Check(atBreak.SelectMany(p=>p).SequenceEqual(paneTest.Panes.SelectMany(p=>p.Points)),"falling shards preserve exact fracture vertices");
var quietSheet=new GlassSheet();quietSheet.Hit(new(100,100),1);for(int i=0;i<120;i++)quietSheet.Step(1f/60,false);Check(quietSheet.Panes.Count==1,"quiet pressure heals sheet");
var flying=new FlightModel();float startZ=flying.Position.Z;for(int i=0;i<60;i++)flying.Step(1f/60,0,0,false);Check(flying.Position.Z<startZ-20,"bird flies continuously without input");var downBird=new FlightModel();for(int i=0;i<60;i++)downBird.Step(1f/60,0,-1,true);Check(downBird.Pitch<0&&downBird.Speed>flying.Speed,"dive and boost increase speed");var spellBird=new FlightModel();for(int i=0;i<3600&&spellBird.Completed==0;i++)spellBird.Step(1f/60,0,0,false,true);Check(spellBird.Completed==1 && spellBird.Score==60,"assisted flight collects cat and awards word bonus");
Console.WriteLine($"All {checks} checks passed including native-resolution 3D regressions.");
var partition=new GlassSheet();for(int i=0;i<14;i++)partition.Hit(new(320+i*11,350),1);
var segments=partition.Panes.SelectMany(p=>p.Points.Select((a,i)=>(A:a,B:p.Points[(i+1)%p.Points.Length]))).ToArray();
float Cross(System.Numerics.Vector2 a,System.Numerics.Vector2 b)=>a.X*b.Y-a.Y*b.X;
bool crossed=false;foreach(var a in segments)foreach(var b in segments){float c1=Cross(a.B-a.A,b.A-a.A),c2=Cross(a.B-a.A,b.B-a.A),c3=Cross(b.B-b.A,a.A-b.A),c4=Cross(b.B-b.A,a.B-b.A);float ea=(a.B-a.A).Length()*.001f,eb=(b.B-b.A).Length()*.001f;if(((c1>ea&&c2< -ea)||(c1< -ea&&c2>ea))&&((c3>eb&&c4< -eb)||(c3< -eb&&c4>eb)))crossed=true;}
Check(!crossed,"fractures stop at existing edges without crossing");
Console.WriteLine($"All {checks} checks passed.");
// Reproduce the old failure with translated keypad releases, Pause, and an overrun packet.
var nativeQueue=new KeyTransitionBuffer();var physical=new PhysicalKeyboard(nativeQueue);var nativeHeld=new HashSet<int>();var nativeChord=new ParentChord();
void DrainPhysical(){while(nativeQueue.TryRead(out var e)){if(e.Key==-1){nativeHeld.Clear();nativeChord.Reset();continue;}if(e.Down)nativeHeld.Add(e.Key);else nativeHeld.Remove(e.Key);nativeChord.Feed(e);}}
foreach(var entry in new[]{(96,82,45),(97,79,35),(98,80,40),(99,81,34),(100,75,37)}){physical.Feed(entry.Item1,entry.Item2,false,true,false,0);physical.Feed(entry.Item3,entry.Item2,false,false,false,.1);}
physical.Feed(19,69,false,true,false,.2);physical.Feed(255,255,false,true,false,.3);DrainPhysical();
Check(physical.Held==0&&nativeHeld.Count==0,"translated releases, Pause and overrun never leave phantom held keys");Check(physical.RemappedReleases==5,"changed virtual-key labels are repaired by scan identity");
physical.Feed(16,42,true,true,false,.4);DrainPhysical();Check(nativeHeld.Count==0,"extended synthetic Shift never poisons chords");
var mapperChord=new ParentChord();ParentAction repairedAction=ParentAction.None;foreach(var e in new[]{(17,29,true,1d),(18,56,true,1.01),(79,24,true,1.02),(79,24,false,1.03),(18,56,false,1.04),(17,29,false,1.05)}){physical.Feed(e.Item1,e.Item2,false,e.Item3,false,e.Item4);while(nativeQueue.TryRead(out var k)){var action=mapperChord.Feed(k);if(action!=ParentAction.None)repairedAction=action;}}
Check(repairedAction==ParentAction.Options,"exact options chord works immediately after scan-code mash regression");
physical.Feed(13,28,false,true,false,2);physical.Feed(13,28,true,true,false,2);physical.Feed(13,28,true,false,false,2.1);DrainPhysical();Check(nativeHeld.Contains(13),"keypad/main Enter share a label without losing held reference");physical.Feed(13,28,false,false,false,2.2);DrainPhysical();Check(nativeHeld.Count==0,"last physical Enter releases its virtual label");
physical.Feed(162,29,false,true,false,3);DrainPhysical();physical.Feed(162,29,false,false,true,3.1);Check(nativeQueue.TryRead(out var repair)&&repair.Key==-1,"injected release repairs state through reset, never authorizes a chord");
var tenO=new TapSequence(79);for(int i=0;i<9;i++){tenO.Feed(new(79,true,i));Check(!tenO.Feed(new(79,false,i)),"nine O taps do not open options");}tenO.Feed(new(79,true,10));Check(tenO.Feed(new(79,false,10)),"ten complete O taps open options");tenO.Reset();for(int i=0;i<99;i++)tenO.Feed(new(79,true,i));tenO.Feed(new(79,false,100));Check(tenO.Count==1,"holding O counts only once");tenO.Feed(new(65,true,101));Check(tenO.Count==0,"another key resets O shortcut");
var separateRoot=Path.Combine(root,"separate-learning");var separate=new Store(separateRoot);separate.Profile.WordCounts["mommy"]=12;separate.Profile.PrefixHabits["mom"]=6;separate.Gestures.Network.Train(new double[6],2);separate.Save();separate.ResetGestureTraining();Check(separate.Profile.WordCounts["mommy"]==12&&separate.Profile.PrefixHabits["mom"]==6,"reset gesture training preserves word habits");separate.Gestures.Network.Train(new double[6],3);separate.ResetWordLearning();Check(separate.Gestures.Network.Samples==1&&separate.Profile.WordCounts.Count==0,"reset word learning preserves gesture calibration");separate.Save();Check(!File.ReadAllText(Path.Combine(separateRoot,"profile.json")).Contains("Network")&&File.Exists(Path.Combine(separateRoot,"gesture-training.json")),"gesture weights persist separately from word model");
var wordOnly=new Store(Path.Combine(root,"word-only"));var weightBefore=System.Text.Json.JsonSerializer.Serialize(wordOnly.Gestures);var wordOnlyRecognizer=new WordRecognizer(wordOnly);for(int i=0;i<40;i++){double t=i*5;foreach(char c in "mommy"){wordOnlyRecognizer.Add(c,t,context);t+=.2;}wordOnlyRecognizer.Update(t+4);}Check(weightBefore==System.Text.Json.JsonSerializer.Serialize(wordOnly.Gestures),"in-game spelling never trains gesture weights");
var staleGesture=new GestureAnalyzer();InputContext typed=default;for(int i=0;i<10;i++)typed=staleGesture.Add("MILK"[i%4],i*.25,9,null,false);Check(typed.Gesture==Gesture.Deliberate,"old held keys alone cannot turn paced typing into clusters");
Console.WriteLine($"All {checks} checks passed.");

var legacyRoot=Path.Combine(root,"legacy-learning");Directory.CreateDirectory(legacyRoot);
File.WriteAllText(Path.Combine(legacyRoot,"profile.json"),"""{"WordCounts":{"mommy":7},"PrefixHabits":{"mom":3},"Network":{"Samples":999}}""");
var legacyLearning=new Store(legacyRoot);Check(legacyLearning.Profile.WordCounts["mommy"]==7 && legacyLearning.Profile.PrefixHabits["mom"]==3 && legacyLearning.Gestures.Network.Samples==0,"legacy word habits survive while embedded gesture weights are retired");
Console.WriteLine($"All {checks} checks passed.");

// Current state is independent of event delivery, model history and lock toggle bits.
Check(!KeySnapshot.WindowsDown(1)&&KeySnapshot.WindowsDown(unchecked((short)0x8001)),"Caps Lock toggled is not held; only high bit is down");
var liveQueue=new KeyTransitionBuffer();foreach(int k in Enumerable.Range(65,20))liveQueue.Push(new(k,true,0));liveQueue.DiscardEvents();Check(liveQueue.Pending==0&&liveQueue.Snapshot().Count==20,"discarding history cannot swallow current twenty-key state");
foreach(int k in Enumerable.Range(65,20))liveQueue.Push(new(k,false,.1));Check(liveQueue.Snapshot().Count==0,"snapshot sees releases before consumer drains events");
var parentLive=new ParentHold();var combo=KeySnapshot.From(new[]{162,164,79});ParentAction heldAction=ParentAction.None;
for(int i=0;i<=130;i++){var a=parentLive.Update(combo,i/60d);if(a!=ParentAction.None)heldAction=a;}
Check(heldAction==ParentAction.Options,"two-second current-state hold opens without a release event");Check(parentLive.Update(combo,4)==ParentAction.None,"continued hold cannot toggle options repeatedly");
parentLive.Reset();for(int i=0;i<60;i++)parentLive.Update(combo,i/60d);parentLive.Update(KeySnapshot.From(new[]{162,164,79,20}),1);
Check(parentLive.Update(combo,1.01)==ParentAction.None,"physically held Caps Lock invalidates exact chord");heldAction=ParentAction.None;for(int i=0;i<130;i++){var a=parentLive.Update(combo,1.02+i/60d);if(a!=ParentAction.None)heldAction=a;}Check(heldAction==ParentAction.Options,"clean current chord recovers without releasing and rebuilding old history");
parentLive.Reset();parentLive.Update(combo,0);Check(parentLive.Update(combo,5)==ParentAction.None,"stalled frames never satisfy hold duration");
parentLive.Reset();heldAction=ParentAction.None;for(int i=0;i<130;i++){var a=parentLive.Update(KeySnapshot.From(new[]{163,161,79}),i/60d);if(a!=ParentAction.None)heldAction=a;}Check(heldAction==ParentAction.Options,"right Ctrl Shift O alias uses exact live hold");
parentLive.Reset();for(int i=0;i<200;i++)Check(parentLive.Update(KeySnapshot.From(new[]{162,164,160,79}),i/60d)==ParentAction.None,"additional modifier rejects live shortcut");
var resumeQueue=new KeyTransitionBuffer();var resumePhysical=new PhysicalKeyboard(resumeQueue);resumePhysical.Feed(20,58,false,true,false,0);resumePhysical.Feed(65,30,false,true,false,.1);resumePhysical.Clear();Check(resumeQueue.Pending==0&&resumeQueue.Snapshot().Count==0&&resumePhysical.Held==0,"visibility reset clears native ledger, current snapshot, and queued keys together");resumePhysical.Feed(65,30,false,true,false,1);Check(resumeQueue.Snapshot().IsDown(65)&&resumeQueue.Pending==1,"fresh key works immediately after resume reset");
Console.WriteLine($"All {checks} checks passed.");

parentLive.Reset();for(int i=0;i<110;i++)parentLive.Update(combo,i/60d);parentLive.Observe(KeySnapshot.From(new[]{162,164,79,65}));parentLive.Observe(combo);Check(parentLive.Update(combo,1.85)==ParentAction.None&&parentLive.Update(combo,2)==ParentAction.None,"extra press between frames restarts continuous hold");
Console.WriteLine($"All {checks} checks passed.");

parentLive.Reset();heldAction=ParentAction.None;for(int i=0;i<130;i++){var a=parentLive.Update(KeySnapshot.From(new[]{162,164,27}),i/60d);if(a!=ParentAction.None)heldAction=a;}Check(heldAction==ParentAction.Exit,"two-second live exit hold needs no release sequence");
Console.WriteLine($"All {checks} checks passed.");
// Patient guided words preserve progress across long pauses and fat fingers.
var patient=new GuidedSpelling();patient.Start("red");patient.Add('r',0);Check(!patient.Update(120)&&patient.Progress==1,"beginner can search for next key for two minutes");patient.Add('v',120);Check(patient.Progress==1,"stray key preserves correct guided prefix");patient.Add('e',150);Check(patient.Add('d',240)&&patient.Completed==1&&patient.Score==55,"slow red completes immediately and scores");
for(int i=0;i<7;i++){patient.Start("red");foreach(char c in "red")patient.Add(c,300+i);}
patient.Start("red");patient.Add('r',400);patient.Add('e',401);Check(patient.LetterSeconds==45,"timed challenge starts gently after eight successes");Check(patient.Update(447)&&patient.Progress==1,"timeout moves back exactly one letter");Check(!patient.Update(900)&&patient.Progress==1,"timeout does not repeatedly erase a word while child waits");patient.Add('e',901);patient.Add('d',902);Check(patient.Completed==9,"retyping last timed-out letter completes word");
var loopBird=new FlightModel();loopBird.Position=new(0,350,0);float minForward=1;for(int i=0;i<180;i++){loopBird.Step(1f/60,0,1,false);minForward=Math.Min(minForward,loopBird.Forward.Z);Check(float.IsFinite(loopBird.Up.Y)&&Math.Abs(System.Numerics.Vector3.Dot(loopBird.Up,loopBird.Forward))<.001,"camera basis remains orthogonal through loop");}Check(loopBird.Pitch>0&&loopBird.Pitch<1,"full loop returns past level without pitch clamp");
var boostBird=new FlightModel();for(int i=0;i<180;i++)boostBird.Step(1f/60,0,0,true);Check(boostBird.Speed>120,"space boost exceeds old speed cap");boostBird.TapTurn(-1);boostBird.Step(.15f,0,0,false);boostBird.TapTurn(-1);Check(boostBird.Rolls==1,"double tap initiates one barrel roll");boostBird.TapTurn(-1);Check(boostBird.Rolls==1,"extra tap cannot stack active rolls");
var callBird=new FlightModel();float initialDistance=System.Numerics.Vector3.Distance(callBird.Position,callBird.Gate);Check(callBird.Signal()&&!callBird.Signal(),"squawk magnet has a cooldown");callBird.Step(.1f,0,0,false);Check(System.Numerics.Vector3.Distance(callBird.Position,callBird.Gate)<initialDistance-10,"squawk pulls next letter toward child");
Check(Enumerable.Range(0,7).Select(i=>ExplorerWorld.Area(-i*950-400)).Distinct().Count()==7,"journey visits seven distinct regions");for(int i=1;i<7;i++)Check(Math.Abs(ExplorerWorld.Height(200,-i*950-.01f)-ExplorerWorld.Height(200,-i*950+.01f))<.1,"terrain boundaries are continuous");
var racer=new FlightModel();racer.Configure(ExplorerKind.Racer);for(int i=0;i<300;i++)racer.Step(1f/60,1,0,true);Check(racer.Position.Z< -100&&racer.OffRoad>0 && racer.OffRoad<55,"racer accelerates and can push through the soft shoulder");
var dolphin=new FlightModel();dolphin.Configure(ExplorerKind.Dolphin);for(int i=0;i<240;i++)dolphin.Step(1f/60,0,-1,true);Check(dolphin.Position.Y<=-4&&dolphin.Position.Y>=ExplorerWorld.Bed(dolphin.Position.X,dolphin.Position.Z)+6.9,"dolphin stays between seafloor and surface");
Console.WriteLine($"All {checks} checks passed including explorer and patient spelling regressions.");

Check(ProjectileMath.HitFraction(new(0,50),10,new(0,100),new(0,0)) is {} hitAt&&Math.Abs(hitAt-.4f)<.001,"fast cannon hits first surface instead of frame endpoint");
Check(ProjectileMath.HitFraction(new(30,50),10,new(0,100),new(0,0))==null,"cannon misses objects outside its flight path");
Console.WriteLine($"All {checks} checks passed.");

foreach(var kind in new[]{ExplorerKind.Racer,ExplorerKind.Dolphin}){var traveler=new FlightModel();traveler.Configure(kind);for(int i=0;i<3600&&traveler.Completed==0;i++)traveler.Step(1f/60,0,0,false,true);Check(traveler.Completed==1&&traveler.Score==60,"assisted "+kind+" collects all word letters and bonus");}
Console.WriteLine($"All {checks} checks passed.");

// Native callback focus policy: missing key-up on the desktop cannot poison the next session.
var focusQueue=new KeyTransitionBuffer();var focusPhysical=new PhysicalKeyboard(focusQueue);var focusLease=new InputFocus(123,focusPhysical.Clear);
Check(!focusLease.Accepts(123),"keyboard capture starts disarmed");focusLease.SetActive(true);
if(focusLease.Accepts(123))focusPhysical.Feed(65,30,false,true,false,0);
Check(focusQueue.Snapshot().IsDown(65),"focused physical down reaches current state");
Check(!focusLease.Accepts(456)&&focusQueue.Pending==0&&focusQueue.Snapshot().Count==0,"desktop focus revokes capture and clears missing-release state");
foreach(int key in new[]{20,65,80,162})if(focusLease.Accepts(456))focusPhysical.Feed(key,key,false,true,false,1);
Check(focusQueue.Pending==0&&!focusLease.Accepts(123),"desktop Caps Lock and password input stay out of history, even before game reactivation");
focusLease.SetActive(true);Check(!focusLease.Accepts(0),"secure desktop or unknown foreground immediately passes through");
focusLease.SetActive(true);Check(focusLease.Accepts(123)&&focusQueue.Snapshot().Count==0,"explicit foreground resume starts with clean input");
var unboundFocus=new InputFocus(0,()=>{});unboundFocus.SetActive(true);Check(!unboundFocus.Accepts(0),"unknown game HWND never captures keys");
var gg=new GameShortcut();Check(!gg.Feed(new(71,true,0)),"first G press stays in play");for(int i=0;i<10;i++)gg.Feed(new(71,true,.1));Check(!gg.Feed(new(71,false,.2)),"G autorepeat only counts as one tap");gg.Feed(new(71,true,.4));Check(gg.Feed(new(71,false,.5)),"two released G taps open game picker");
gg.Feed(new(71,true,1));gg.Feed(new(71,false,1.1));gg.Feed(new(65,true,1.2));gg.Feed(new(71,true,1.3));Check(!gg.Feed(new(71,false,1.4)),"other key interrupts GG command");gg.Reset();gg.Feed(new(71,true,2));gg.Feed(new(71,false,2.1));gg.Feed(new(71,true,4));Check(!gg.Feed(new(71,false,4.1)),"unrelated G taps do not open picker");
Check(GameCatalog.All.Select(g=>g.Mode).Distinct().Count()==Enum.GetValues<PlayMode>().Length && GameCatalog.All.All(g=>g.Topics.Length>0&&g.MinimumAge>0),"every game advertises selection and filter metadata");
var courseReward=new LetterCourse();courseReward.Start("a");courseReward.Collect();int rewardScore=courseReward.Score;Check(courseReward.RewardRemaining==2&&!courseReward.Collect()&&courseReward.Score==rewardScore,"completion bonus fires once with a two-second reward window");courseReward.Step(2);Check(courseReward.RewardRemaining==0,"completion interval ends on schedule");
foreach(float pitch in new[]{0f,.8f,2.5f,3.14f,-2f}){var oriented=new FlightModel{Yaw=.7f,Pitch=pitch};var right=System.Numerics.Vector3.TransformNormal(System.Numerics.Vector3.UnitX,oriented.LetterFacing);Check(System.Numerics.Vector3.Dot(right,oriented.Right)>.999f,"glyph local right stays screen-right through loops");}
var highBird=new FlightModel{Position=new(0,1500,0),Pitch=2.8f};for(int i=0;i<1200;i++){highBird.Step(1f/60,0,0,false,true);if(highBird.Collected==highBird.Word.Length&&highBird.RewardRemaining==0)highBird.SetWord("cat");}
Check(Math.Abs(highBird.Position.Y-ExplorerWorld.Height(highBird.Position.X,highBird.Position.Z)-45)<35 && highBird.Up.Y>.8f,"idle inverted high bird returns to upright treetop flight");
float valleyZ=-2350;float valleyX=ExplorerWorld.Valley(valleyZ);Check(ExplorerWorld.Height(valleyX+230,valleyZ)>ExplorerWorld.Height(valleyX,valleyZ)+100&&ExplorerWorld.Height(valleyX-230,valleyZ)>ExplorerWorld.Height(valleyX,valleyZ)+100,"mountain region has tall banks on both sides of flyable valley");
foreach(int columns in new[]{12,50}){bool outside=true;for(float z=-2600;z<-1800;z+=10)foreach(int side in new[]{-1,1})for(int i=0;i<=columns;i++){var p=ExplorerWorld.RoadTerrainPoint(side,i,z,columns);outside&=(p.X-ExplorerWorld.Road(p.Z))*side>=19.999f;}Check(outside,"road terrain tessellation stops at shoulder for detail "+columns);}
var shoreCar=new FlightModel();shoreCar.Configure(ExplorerKind.Racer);shoreCar.Position=new(ExplorerWorld.Road(-1200)+19,7,-1200);bool dry=true;for(int i=0;i<3000;i++){shoreCar.Step(1f/60,1,0,true);dry&=ExplorerWorld.Driveable(shoreCar.Position.X,shoreCar.Position.Z);}Check(dry,"sustained boosted off-road steering cannot enter lake or river water");
Console.WriteLine($"All {checks} checks passed including focus isolation, game picker and explorer rewards.");

foreach(var kind in Enum.GetValues<ExplorerKind>()){
    var journey=new FlightModel();journey.Configure(kind);
    for(int i=0;i<7200;i++){journey.Step(1f/60,0,0,false,true);if(journey.Collected==journey.Word.Length&&journey.RewardRemaining==0)journey.SetWord("cat");}
    Console.WriteLine($"Journey {kind}: {journey.Completed} words at {journey.Position}");
    Check(journey.Completed>=5,"hands-off "+kind+" sustains spelling across a two-minute journey");
}
Console.WriteLine($"All {checks} checks passed.");

gg.Reset();gg.Feed(new(71,true,5),true);gg.Feed(new(71,false,5.1),true);gg.Feed(new(71,true,5.3),true);Check(!gg.Feed(new(71,false,5.4),true),"modified G taps never open child picker");
var clearReward=new LetterCourse();clearReward.Start("a");clearReward.Collect();clearReward.DismissReward();Check(clearReward.RewardRemaining==0&&clearReward.Score==20,"resume clears completion effects without erasing earned points");
Console.WriteLine($"All {checks} checks passed.");

focusLease.SetActive(true);Check(!focusLease.Accepts(123,false)&&!focusLease.Active,"lock-screen desktop revokes capture even if the foreground HWND is stale");
Console.WriteLine($"All {checks} checks passed.");

// Dot Pop: all 2^9 patterns, balanced quantities and no immediate repeats.
Check(Enumerable.Range(0,512).Select(DotPatterns.Cells).Select(c=>string.Join(",",c)).Distinct().Count()==512,"3x3 occupancy represents exactly 512 different patterns including zero");
var patternDeck=new DotPatterns(301);var allDots=new DotDifficulty(0,9,false,1,double.PositiveInfinity);var seenDots=new HashSet<int>();int previousDots=-1;bool repeats=false;var histogram=new int[10];
for(int i=0;i<4000;i++){int mask=patternDeck.Next(allDots,previousDots);repeats|=mask==previousDots;seenDots.Add(mask);histogram[DotPatterns.Count(mask)]++;previousDots=mask;}
Check(!repeats&&seenDots.Count==512,"pattern deck explores every occupancy without consecutive repeats");Check(histogram.Max()-histogram.Min()<=2,"zero and nine appear as often as quantities with many more masks");
foreach(int mastery in new[]{0,12,28,44,60,79,80,100,200}){var tier=DotDifficulty.At(mastery);var deck=new DotPatterns(mastery);int last=-1;bool valid=true;for(int i=0;i<180;i++){int mask=deck.Next(tier,last);int n=DotPatterns.Count(mask);valid&=n>=tier.Minimum&&n<=tier.Maximum&&mask!=last&&(!tier.Structured||DotPatterns.IsStructured(mask));last=mask;}Check(valid,"dot tier respects quantity, grouping and repeat limits at mastery "+mastery);}
Check(double.IsPositiveInfinity(DotDifficulty.At(79).DisplaySeconds)&&DotDifficulty.At(80).DisplaySeconds==4&&DotDifficulty.At(200).DisplaySeconds>=.4,"display timing begins gently and has a readable minimum");
void ReachDotAnswer(SubitizingGame game){for(int i=0;i<100&&game.Phase!=DotPhase.Answer;i++)game.Step(.02);}
var dotGame=new SubitizingGame(12);Check(!dotGame.Answer(dotGame.Quantity)&&dotGame.Stage==1,"countdown ignores even the correct answer");ReachDotAnswer(dotGame);for(int i=0;i<2000;i++)dotGame.Step(.1);Check(dotGame.DotsVisible&&dotGame.CanAnswer&&dotGame.Stage==1,"beginner dots stay visible without a timeout");int originalDotMask=dotGame.Mask;
Check(!dotGame.Answer((dotGame.Quantity+1)%10)&&dotGame.Mask==originalDotMask&&dotGame.Stage==1,"wrong answer retains the exact dots and stage");Check(!dotGame.CanAnswer&&dotGame.WrongShake>0,"wrong answer only causes a short quiet shake and ignores rapid taps");dotGame.Step(.25);Check(dotGame.Answer(dotGame.Quantity)&&dotGame.Mastery==0,"retry succeeds without increasing difficulty");Check(!dotGame.Answer(dotGame.Quantity),"reward cannot be awarded twice");for(int i=0;i<120;i++)dotGame.Step(.02);Check(dotGame.Stage==2&&dotGame.Mask!=originalDotMask,"reward automatically advances to a different pattern");
var flashed=new SubitizingGame(34,90);ReachDotAnswer(flashed);for(int i=0;i<50;i++)flashed.Step(.1);Check(!flashed.DotsVisible&&flashed.CanAnswer,"timed dots disappear while answers remain available");flashed.Answer((flashed.Quantity+1)%10);flashed.Step(.25);Check(flashed.DotsVisible&&flashed.CanAnswer,"wrong memory answer reveals the same pattern for a patient retry");flashed.RestartRound();Check(flashed.Phase==DotPhase.Ready&&flashed.WrongShake==0,"focus return restarts the round without stale shake or timers");
foreach(int number in new[]{0,9}){SubitizingGame? rewardDots=null;for(int seed=0;seed<100;seed++){var g=new SubitizingGame(seed,number==0?0:44);if(g.Quantity==number){rewardDots=g;break;}}Check(rewardDots!=null,"zero and nine can be selected");var g2=rewardDots!;ReachDotAnswer(g2);Check(g2.Answer(number),"correct number starts reward for "+number);for(int i=0;i<50;i++)g2.Step(.02);Check(g2.Popped==number&&g2.Phase==DotPhase.Reward,"volley pops exactly "+number+" dots in the same time window");g2.RestartRound();Check(g2.Stage==2&&g2.Phase==DotPhase.Ready,"resume after correct answer keeps one stage advance");}
foreach(var size in new[]{(720f,1080f),(1920f,1080f),(1152f,720f),(1080f,1920f)}){var fit=DotLayout.Fit(size.Item1,size.Item2);bool hits=true;for(int n=0;n<10;n++){var p=DotLayout.Number(n).Center*fit.Scale+fit.Offset;hits&=DotLayout.HitNumber(DotLayout.Unproject(p,size.Item1,size.Item2))==n;}Check(hits,"all ten touch targets map correctly at "+size);Check(DotLayout.HitNumber(DotLayout.Unproject(new(-10,-10),size.Item1,size.Item2))==-1,"outside portrait area never answers at "+size);}
Check(DotPatterns.Cells(0).Count()==0&&DotPatterns.Cells(511).Count()==9,"zero is genuinely empty; nine fills every grid cell");
Console.WriteLine($"All {checks} checks passed including subitizing.");

foreach(var size in new[]{(720f,1080f),(1920f,1080f),(1152f,720f),(1080f,1920f)}){float rw=Math.Max(576,size.Item1*.5f),rh=Math.Max(360,size.Item2*.5f);var render=DotLayout.RenderFit(size.Item1,size.Item2,rw,rh);bool valid=true;for(int n=0;n<10;n++){var p=(DotLayout.Number(n).Center*render.Scale+render.Offset)*new System.Numerics.Vector2(size.Item1/rw,size.Item2/rh);valid&=DotLayout.HitNumber(DotLayout.Unproject(p,size.Item1,size.Item2))==n;}Check(valid,"render-resolution clamping preserves portrait proportions and touch mapping at "+size);}
Console.WriteLine($"All {checks} checks passed.");

// Visual math: seed-stable constrained generation, adaptive progression, and non-blocking retry flow.
foreach(var activity in Enum.GetValues<MathActivity>())foreach(int level in Enumerable.Range(1,8)){
    var d=MathDifficulty.At(level);var deck=new MathRounds(947);var twin=new MathRounds(947);MathRound? last=null;
    bool bounds=true,choices=true,answers=true,deterministic=true,alternate=true,frame=true;int zero=0;
    for(int i=0;i<400;i++){
        var r=deck.Next(activity,d);var s=twin.Next(activity,d);
        deterministic&=r.A==s.A&&r.B==s.B&&r.Subtract==s.Subtract&&r.Answer==s.Answer&&r.Choices.SequenceEqual(s.Choices);
        bounds&=r.A>=0&&r.A<=d.Limit&&r.B>=0&&r.B<=d.Limit;
        choices&=r.Choices.Distinct().Count()==r.Choices.Length&&r.Choices.Count(n=>n==r.Answer)==1&&r.Choices.Length<=4;
        if(activity is MathActivity.HowManyNow or MathActivity.CannonHop){
            answers&=r.Answer==(r.Subtract?r.A-r.B:r.A+r.B)&&r.Answer>=0&&r.Answer<=d.Limit;
            frame&=r.Subtract||d.Capacity-r.A>=r.B;
            if(last!=null)alternate&=last.Subtract!=r.Subtract;
        }else if(activity==MathActivity.Hiding)answers&=r.B==r.Answer&&r.A-r.B>=0&&(r.A-r.B)+r.Answer==r.A;
        else if(activity==MathActivity.MakeNumber)answers&=r.A<r.B&&r.Answer==r.B&&r.B<=d.Capacity;
        else answers&=r.Answer==(r.A==r.B?2:(r.Fewer?r.A<r.B:r.A>r.B)?0:1);
        if(r.Answer==0)zero++;last=r;
    }
    Check(bounds&&answers&&frame,$"math {activity} level {level}: valid operands, answers, nonnegative subtraction, and frame capacity");
    Check(choices&&deterministic&&alternate,$"math {activity} level {level}: unique choices, seed reproducibility, balanced operations");
    if(activity==MathActivity.HowManyNow&&level>=2)Check(zero>0&&zero<180,"zero is present without dominating math level "+level);
}
void ReachMath(MathGame g,MathPhase phase){for(int i=0;i<1800&&g.Phase!=phase;i++)g.Step(.02);Check(g.Phase==phase,"math reaches "+phase+" without blocking");}
foreach(var activity in Enum.GetValues<MathActivity>()){
    var progress=new MathProgress();var g=new MathGame(activity,progress,120,level:4);
    Check(!g.Answer(g.Round.Answer)&&!g.Cell(0),activity+" ignores input during countdown");ReachMath(g,MathPhase.AwaitAnswer);
    for(int i=0;i<4000;i++)g.Step(.1);
    Check(g.CanAnswer&&g.Stage==1,activity+" never times out a slow answer");
    if(activity!=MathActivity.MakeNumber){
        int wrong=g.Round.Choices.First(n=>n!=g.Round.Answer);g.Answer(wrong);
        Check(g.Phase==MathPhase.Incorrect&&g.Stage==1&&!g.Answer(g.Round.Answer),activity+" error locks input without losing stage");
        ReachMath(g,MathPhase.AwaitAnswer);g.Answer(wrong);ReachMath(g,MathPhase.Count);
        Check(g.Errors==2&&g.Replaying,activity+" second error counts concrete objects");ReachMath(g,MathPhase.AwaitAnswer);g.Answer(g.Round.Answer);
        ReachMath(g,MathPhase.Reward);
    }else{
        int before=g.BuiltCount;g.Cell(0);Check(g.BuiltCount==before-1,"builder permits removing occupied cells");
        for(int i=0;i<g.Difficulty.Capacity&&g.CanAnswer;i++)if((g.BuiltMask&(1<<i))==0)g.Cell(i);
        Check(g.Phase==MathPhase.Reward,"builder completes automatically at target cardinality");
    }
    Check(g.Stage==2&&!g.Answer(g.Round.Answer)&&!g.Cell(0),activity+" awards one stage and rejects reward-period input");
    g.Restart();Check(g.Stage==2&&g.Phase==MathPhase.Ready,activity+" focus resume preserves exactly one earned stage");
}
var adaptation=new MathSkillProgress();for(int i=0;i<4;i++)adaptation.Complete(true);Check(adaptation.Level==2&&adaptation.Stage==5,"four accurate rounds advance one difficulty variable");
adaptation.Complete(false);adaptation.Complete(true);adaptation.Complete(false);Check(adaptation.Level==1&&adaptation.Stage==8,"repeated errors ease difficulty without removing stages");
for(int i=0;i<100;i++)adaptation.Complete(true);Check(adaptation.Level==8,"difficulty cannot exceed eight");for(int i=0;i<100;i++)adaptation.Complete(false);Check(adaptation.Level==1,"difficulty cannot fall below one");
var mathMemory=new MathProgress();Check(!mathMemory.HopUnlocked,"number-line game starts gated by visual learning");
for(int i=0;i<6;i++){var g=new MathGame(MathActivity.HowManyNow,mathMemory,level:4,a:3,b:1,subtract:i%2==0);ReachMath(g,MathPhase.AwaitAnswer);g.Answer(g.Round.Answer);}
Check(mathMemory.HopUnlocked,"three joining and three separating rounds unlock hops");
var retainedStage=mathMemory.For(MathActivity.HowManyNow).Stage;
store.MathLearning.Skills=mathMemory.Skills;store.MathLearning.JoiningCompleted=3;store.MathLearning.SeparatingCompleted=3;store.MathLearning.LastActivity=MathActivity.Hiding;store.Save();
var restoredMath=new Store(root);Check(restoredMath.MathLearning.HopUnlocked&&restoredMath.MathLearning.For(MathActivity.HowManyNow).Stage==retainedStage&&restoredMath.MathLearning.LastActivity==MathActivity.Hiding,"local save restores math progress and last activity");
var confirmed=new MathGame(MathActivity.HowManyNow,new(),level:8,a:5,b:2,subtract:true);ReachMath(confirmed,MathPhase.AwaitAnswer);confirmed.Answer(3);Check(confirmed.Phase==MathPhase.Confirm&&confirmed.Stage==1,"equation-first answer explains visually before reward");ReachMath(confirmed,MathPhase.Reward);Check(confirmed.Stage==2,"confirmation awards stage once");
foreach(var size in new[]{(1366f,768f),(1920f,1080f),(1152f,720f)}){
    var ratio=new System.Numerics.Vector2(size.Item1/1440,size.Item2/900);bool correct=true;
    foreach(var r in Enumerable.Range(0,10).Select(i=>MathLayout.CellButton(i,10)).Concat(Enumerable.Range(0,4).Select(i=>MathLayout.Choice(i,4))).Concat(Enumerable.Range(0,11).Select(MathLayout.Track)))correct&=r.Contains(MathLayout.Unproject(r.Center*ratio,size.Item1,size.Item2));
    Check(correct,"math pointer targets agree with suite rendering at "+size);
}
Console.WriteLine($"All {checks} checks passed including visual math.");

var memoryRound=new MathGame(MathActivity.HowManyNow,new(),level:6,a:5,b:2,subtract:false);
ReachMath(memoryRound,MathPhase.Observe);Check(!memoryRound.Hidden,"advanced final quantity has a stable observation interval before hiding");ReachMath(memoryRound,MathPhase.AwaitAnswer);Check(memoryRound.Hidden,"memory stage hides only after observing the result");memoryRound.Answer(memoryRound.Round.Choices.First(n=>n!=7));ReachMath(memoryRound,MathPhase.AwaitAnswer);Check(!memoryRound.Hidden&&memoryRound.Replaying,"error restores persistent concrete support instead of another memory test");
var resumedMath=new MathGame(MathActivity.Hiding,new(),level:4);ReachMath(resumedMath,MathPhase.AwaitAnswer);int originalHidden=resumedMath.Round.B;resumedMath.Answer(resumedMath.Round.Choices.First(n=>n!=originalHidden));resumedMath.Restart();Check(resumedMath.Round.B==originalHidden&&resumedMath.Errors==1&&resumedMath.Phase==MathPhase.Ready,"focus return replays the same math problem and preserves assistance");
var heldNarration=new MathGame(MathActivity.HowManyNow,new());for(int i=0;i<7;i++)heldNarration.Step(.1,true);Check(heldNarration.Phase==MathPhase.Ready,"countdown allows the current spoken cue to finish");for(int i=0;i<200;i++)heldNarration.Step(.1,true);Check(heldNarration.Phase!=MathPhase.Ready,"unavailable speech cannot stall the round forever");
Console.WriteLine($"All {checks} checks passed including math replay and narration.");

// Immersion rewards never change learning mastery or remove earned points.
var streakBonus=new LearningBonus();
for(int i=0;i<5;i++){streakBonus.BeginRound();Check(!streakBonus.Active,"five ordinary rounds precede a pearl bonus");streakBonus.Complete(true);}
streakBonus.BeginRound();Check(streakBonus.Active&&streakBonus.Score==50,"a five-answer streak earns a pearl round");
streakBonus.Complete(true);Check(streakBonus.LastPoints==30&&streakBonus.Score==80,"pearl correct answer awards triple points exactly once");
streakBonus.BeginRound();Check(!streakBonus.Active,"bonus does not chain indefinitely");
streakBonus.Miss();streakBonus.Complete(false);Check(streakBonus.Streak==0,"retry breaks the streak without taking earned points");
var numberedShow=new FireworkSchedule();numberedShow.Add(5,10);
Check(numberedShow.DueNumbers(10).SequenceEqual(new[]{1}),"firework label starts with one");
Check(numberedShow.DueNumbers(14).SequenceEqual(new[]{2,3,4,5}),"dropped frames preserve all scheduled counting labels");
Check(numberedShow.DueNumbers(15).Length==0,"count labels are consumed once");
foreach(var kind in new[]{ExplorerKind.Bird,ExplorerKind.Dolphin})
foreach(float distance in new[]{0f,2100f,5500f})
foreach(float heading in new[]{0f,2.8f}){
    var flight=new FlightModel();flight.Configure(kind);flight.Yaw=heading;flight.Response=.6f;
    float x=kind==ExplorerKind.Bird?160:0,z=-distance;
    flight.Position=new(x,kind==ExplorerKind.Bird?ExplorerWorld.Height(x,z)+100:-8,z);
    flight.Pitch=1.1f;flight.Speed=100;flight.SetWord("cat");
    for(int i=0;i<7200&&flight.Completed==0;i++)flight.Step(1f/60,0,0,false,true);
    Check(flight.Completed==1,"assisted "+kind+" reaches displaced gates at "+distance+" heading "+heading);
}
var breaching=new FlightModel();breaching.Configure(ExplorerKind.Dolphin);breaching.Position=new(0,-2,0);breaching.Pitch=.8f;
bool reachedAir=false,returnedToWater=false;
for(int i=0;i<360;i++){breaching.Step(1f/60,0,1,true);reachedAir|=breaching.Position.Y>1;returnedToWater|=reachedAir&&breaching.Position.Y<0;}
Check(reachedAir&&returnedToWater,"dolphin breaches and falls back into water with climb held");
var bonusDriver=new FlightModel();bonusDriver.Configure(ExplorerKind.Racer);bonusDriver.AwardBubble();bonusDriver.AwardTreasure();
Check(bonusDriver.Score==30&&bonusDriver.Treasures==1,"explorer bonuses share the score without collecting a letter");
Check(bonusDriver.HitObstacle()&&!bonusDriver.HitObstacle(),"harmless obstacles have a cooldown");
for(int i=0;i<120;i++)bonusDriver.Step(1f/60,0,0,false);
Check(bonusDriver.ObstacleRemaining==0&&bonusDriver.Score==30,"obstacle slowdown expires without losing points");
Console.WriteLine("Immersion reward and navigation checks passed.");

var surfaceDolphin = new FlightModel(); surfaceDolphin.Configure(ExplorerKind.Dolphin); surfaceDolphin.SetWord("cat");
bool emerged=false, returned=false;
for(int i=0;i<1000;i++){surfaceDolphin.Step(.01f,0,1,false,false);emerged|=surfaceDolphin.Position.Y>0;returned|=emerged && surfaceDolphin.Position.Y < -5;}
Check(emerged && returned,"Holding climb from normal dolphin spawn breaches and returns without boost");

foreach(var explorer in new[]{ExplorerKind.Bird,ExplorerKind.Dolphin})
foreach(float response in new[]{.5f,1f,2f})
foreach(float distance in new[]{0f,2100f,5500f})
foreach(float headingError in new[]{-2.9f,0f,2.9f})
{
    var pursuit=new FlightModel();pursuit.Configure(explorer);pursuit.Response=response;pursuit.TopSpeed=240;
    float x=ExplorerWorld.Valley(-distance);
    pursuit.Position=new System.Numerics.Vector3(x,explorer==ExplorerKind.Bird?ExplorerWorld.Height(x,-distance)+100:ExplorerWorld.Bed(x,-distance)+20,-distance);
    pursuit.SetWord("cat");pursuit.Yaw+=headingError;pursuit.Pitch=headingError;pursuit.Speed=220;
    for(int i=0;i<6000 && pursuit.Completed==0;i++)pursuit.Step(.01f,0,0,false,true);
    Check(pursuit.Completed>0,$"Assisted {explorer} completes after inverted/fast approach, response {response}, chapter {distance}, angle {headingError}");
}

(float Height,float Range) BreachArc(float speed){
    var dolphin=new FlightModel();dolphin.Configure(ExplorerKind.Dolphin);dolphin.Position=new(0,-.25f,0);dolphin.Pitch=.8f;dolphin.Speed=speed;
    bool air=false;float highest=0;var exit=System.Numerics.Vector3.Zero;
    for(int i=0;i<1000;i++){
        dolphin.Step(.01f,0,1,false);
        if(!air && dolphin.Position.Y>0){air=true;exit=dolphin.Position;}
        highest=Math.Max(highest,dolphin.Position.Y);
        if(air && dolphin.Position.Y<=0)return(highest,System.Numerics.Vector2.Distance(new(exit.X,exit.Z),new(dolphin.Position.X,dolphin.Position.Z)));
    }
    throw new Exception("Dolphin did not complete its breach arc.");
}
var cruiseBreach=BreachArc(30);var fastBreach=BreachArc(80);
Check(fastBreach.Height>cruiseBreach.Height*2 && fastBreach.Range>cruiseBreach.Range*2,"faster dolphin takeoff carries farther and higher before safe water entry");

var prehistoric=new DinosaurModel();prehistoric.SetWord("dino");
for(int i=0;i<5000&&prehistoric.Course.Completed==0;i++)prehistoric.Step(.02f,0,0,false,true);
Check(prehistoric.Course.Completed==1 && prehistoric.Course.Score==80,"dinosaur assist collects a full word using shared spelling score");
Check(prehistoric.Footfalls>0 && Math.Abs(prehistoric.Position.Y-DinosaurWorld.Ground(prehistoric.Position.X,prehistoric.Position.Z))<.001,"dinosaur footfalls follow distance and grounded terrain");
Check(prehistoric.Roar()&&!prehistoric.Roar(),"dinosaur roar is edge-triggered and cooldown bounded");
for(int i=0;i<100;i++)prehistoric.Step(.02f,0,-1,false,false);
Check(prehistoric.Speed<.1f,"dinosaur braking settles to a stop");
Check((int)PlayMode.Dinosaur==12 && GameCatalog.Legacy.Length==12 && GameCatalog.All.Length==13,"new dinosaur mode appends without changing legacy IDs or picker");

var shyAnimal=new WildlifeMotion(System.Numerics.Vector3.Zero,71);
var distantObserver=new System.Numerics.Vector3(1000,1000,1000);
for(int i=0;i<700;i++)shyAnimal.Step(.02f,distantObserver);
Check(shyAnimal.Position.Length()>1,"wildlife wanders without player interaction");
var beforeThreat=shyAnimal.Position;var threat=beforeThreat+new System.Numerics.Vector3(0,0,2);
for(int i=0;i<240;i++)shyAnimal.Step(.02f,threat);
Check(shyAnimal.Reactions>0 && System.Numerics.Vector3.Distance(shyAnimal.Position,threat)>10,"nearby wildlife flees the player with smooth travel");
var boundedAnimal=new WildlifeMotion(System.Numerics.Vector3.Zero,19);
for(int i=0;i<10000;i++)boundedAnimal.Step(.02f,distantObserver,p=>p.X<5 && p.Z<5);
Check(boundedAnimal.Position.X<5 && boundedAnimal.Position.Z<5 && boundedAnimal.Position.Length()<85,"wildlife respects habitat barriers and remains near its home");
var startled=new WildlifeMotion(System.Numerics.Vector3.Zero,21);startled.Startle();startled.Step(.02f,new(0,0,40));
Check(startled.State==WildlifeState.Flee,"roar startles a creature outside normal proximity radius");

var followingCub=new WildlifeMotion(new(0,0,-20),36,speed:2.5f);
for(int i=0;i<1800;i++){followingCub.Follow(new(0,0,i*.01f));followingCub.Step(.02f,distantObserver);}
Check(System.Numerics.Vector3.Distance(followingCub.Position,new(0,0,18))<15,"cub follows its mother's moving habitat rather than wandering independently");

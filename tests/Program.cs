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

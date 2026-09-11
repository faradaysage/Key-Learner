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
foreach(var key in new[]{81,87,69,82}) analyzer.Add(key,key*.05,1,store.Profile,false);
Check(analyzer.Current.Gesture==Gesture.Sweep,"horizontal keyboard sweep");
analyzer.Reset();
foreach(var pair in new[]{(65,1),(76,2),(81,3),(80,4),(77,5)}) analyzer.Add(pair.Item1,.01*pair.Item2,pair.Item2,store.Profile,false);
Check(analyzer.Current.Gesture==Gesture.BroadMash,"broad simultaneous mashing");
var network=new TinyNetwork();
for(var i=0;i<1000;i++) {network.Train([0,0,0,0,0,0],0);network.Train([1,1,1,0,0,0],3);}
Check(network.Predict([0,0,0,0,0,0]).Label==0 && network.Predict([1,1,1,0,0,0]).Label==3,"tiny neural model learns labeled examples");
store.Settings.Theme=Mood.Lagoon;store.Settings.Gravity=double.NaN;store.Profile.Network=network;
Check(store.Save(),"atomic store save");
var restored=new Store(root);
Check(restored.Settings.KeyIcons.GetValueOrDefault(112)=="cat","parent icon remapping persists");
Check(restored.Settings.Theme==Mood.Lagoon && double.IsFinite(restored.Settings.Gravity),"settings roundtrip and validation");
Check(restored.Profile.PrefixHabits.Count>0,"prefix habits persist across launches");
Check(restored.Words.Any(w=>w.Word=="milk"),"dictionary roundtrip");
Check(restored.Profile.Network.Samples==network.Samples,"neural weights persist");
File.WriteAllText(Path.Combine(root,"settings.json"),"{broken");
Check(new Store(root).Settings.Theme==Mood.PrimaryColors,"corrupt settings recover");
var switched=new ParentChord();
switched.Feed(new(162,true,0));switched.Feed(new(164,true,.1));switched.Feed(new(27,true,.2));
switched.Feed(new(27,false,1));switched.Feed(new(79,true,1.1));switched.Feed(new(79,false,2));switched.Feed(new(164,false,2.1));
Check(switched.Feed(new(162,false,2.2))==ParentAction.None,"changing target during release cannot authorize");
var balloon=new BalloonMotion();balloon.Inflate();balloon.Step(.008f);
Check(balloon.Size<1,"balloon squeezes before inflating");
for(int i=0;i<240;i++)balloon.Step(1f/120);
Check(Math.Abs(balloon.Size-1.28f)<.01f,"balloon settles at inflated size");
for(int i=0;i<100;i++){balloon.Inflate();for(int j=0;j<12;j++)balloon.Step(1f/120);}
Check(balloon.Target<=3.8f && balloon.Size<=4.2f,"keyboard storm cannot grow balloon without bound");
Console.WriteLine($"All {checks} checks passed. Test data: {root}");

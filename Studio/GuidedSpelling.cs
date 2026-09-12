namespace KeyLearner.Studio;
/// <summary>Guided spelling has its own patient progress; Smash's phrase-gap timer is never used.</summary>
public sealed class GuidedSpelling
{
    public string Target {get;private set;}="";
    public int Progress {get;private set;}
    public int Completed {get;private set;}
    public int Score {get;private set;}
    public bool Timed {get;set;}=true;
    public double FeedbackUntil {get;private set;}
    double deadline=double.PositiveInfinity;
    public double LetterSeconds=>!Timed||Completed<8?double.PositiveInfinity:Math.Max(15,45-(Completed-8)*.5);
    public int MaxWordLength=>Math.Min(10,3+Completed/5);
    public void Restart(){Completed=Score=0;Start(Target);}
    public void Start(string word){Target=word;Progress=0;deadline=double.PositiveInfinity;FeedbackUntil=0;}
    public bool Update(double now){if(!Timed){deadline=double.PositiveInfinity;return false;}if(Progress==0||now<deadline)return false;Progress--;deadline=double.PositiveInfinity;FeedbackUntil=now+.65;return true;}
    public bool Add(char key,double now){
        if(Target.Length==0||Progress>=Target.Length)return false;
        if(char.ToLowerInvariant(key)!=Target[Progress])return false; // A stray neighboring key never deletes a correct prefix.
        Progress++;deadline=now+LetterSeconds;
        if(Progress<Target.Length)return false;
        Score+=Target.Length*10+25;Completed++;deadline=double.PositiveInfinity;return true;
    }
    public void Backspace(){if(Progress>0)Progress--;deadline=double.PositiveInfinity;}
    public double Remaining(double now)=>double.IsPositiveInfinity(deadline)?1:Math.Clamp((deadline-now)/LetterSeconds,0,1);
}

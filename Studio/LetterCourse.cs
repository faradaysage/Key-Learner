namespace KeyLearner.Studio;
/// <summary>Reusable spelling progression and a bounded completion interval for every explorer.</summary>
public sealed class LetterCourse
{
    public string Word {get;private set;}="cat";
    public int Collected {get;private set;}
    public int Score {get;private set;}
    public int Completed {get;private set;}
    public float RewardRemaining {get;private set;}
    public void Start(string word){Word=string.IsNullOrWhiteSpace(word)?"cat":word.ToLowerInvariant();Collected=0;RewardRemaining=0;}
    public void DismissReward()=>RewardRemaining=0;
    public void Step(float dt)=>RewardRemaining=Math.Max(0,RewardRemaining-Math.Max(0,dt));
    public bool Collect(){
        if(Collected>=Word.Length)return false;
        Collected++;Score+=10;
        if(Collected==Word.Length){Score+=Word.Length*10;Completed++;RewardRemaining=2;}
        return true;
    }
}

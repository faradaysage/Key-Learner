namespace KeyLearner.Studio;
/// <summary>Optional, reusable reward rules; never changes the question or mastery.</summary>
public sealed class LearningBonus
{
    public int Streak { get; private set; }
    public int Score { get; private set; }
    public int LastPoints { get; private set; }
    public bool Active { get; private set; }
    public void BeginRound() { Active = Streak >= 5; LastPoints = 0; }
    public void Miss() { Streak = 0; }
    public void Complete(bool firstTry)
    {
        LastPoints = Active ? 30 : 10;
        Score += LastPoints;
        Streak = firstTry ? (Active ? 0 : Streak + 1) : 0;
    }
}

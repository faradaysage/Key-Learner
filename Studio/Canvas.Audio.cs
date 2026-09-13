using Microsoft.Xna.Framework;
namespace KeyLearner.Studio;
public sealed partial class Canvas
{
    public void ExplorerComplete(){sounds.Play("powerup",settings,.75f);shake=1;}
    public void SpellingTimeout()=>sounds.Play("retry",settings,.45f);
    public void ExplorerSignal(ExplorerKind kind)=>sounds.Play(kind==ExplorerKind.Dolphin?"sonar":kind==ExplorerKind.Racer?"horn":"squawk",settings,.65f);
    public void Squawk()=>sounds.Play("squawk",settings,.7f);
}

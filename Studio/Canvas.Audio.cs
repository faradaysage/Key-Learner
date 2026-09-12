using Microsoft.Xna.Framework;
namespace KeyLearner.Studio;
public sealed partial class Canvas
{
    public void Squawk()=>sounds.Play("squawk",settings,.7f);
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace KeyLearner.Studio;
public static class RenderSpace
{
    public static void Begin(SpriteBatch b,BlendState? blend=null)=>b.Begin(SpriteSortMode.Deferred,blend??BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullNone,null,Matrix.CreateScale(b.GraphicsDevice.Viewport.Width/1440f,b.GraphicsDevice.Viewport.Height/900f,1));
}

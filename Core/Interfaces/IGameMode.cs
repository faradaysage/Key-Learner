using KeyLearner.Core.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyLearner.Core.Interfaces
{
    public interface IGameMode
    {
        string Name { get; }
        GameLevel Level { get; }
        void Initialize(Game game, GraphicsDeviceManager graphics, SpriteManager spriteManager, IVoiceService voiceService, ParticleManager particleManager);
        public void HandleKeyPress(Keys[] keys, GameTime gameTime);
        void Update(GameTime gameTime);
        void Draw(SpriteBatch spriteBatch, GameTime gameTime);
        void Cleanup();
    }
}
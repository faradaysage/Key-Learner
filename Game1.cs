using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using KeyLearner.Core.Services;
using KeyLearner.Core.Interfaces;
using KeyLearner.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using static System.Net.Mime.MediaTypeNames;
using System.Resources;
using Microsoft.Xna.Framework.Content;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Vector2 = System.Numerics.Vector2;

namespace KeyLearner
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Services
        private ModeDiscoveryService _modeDiscovery;
        private IVoiceService _voiceService;
        private SpriteManager _spriteManager;


        // Game State
        private enum MenuState
        {
            StartQuit,
            LevelSelection,
            ModeSelection,
            ActiveMode
        }

        private MenuState _menuState = MenuState.StartQuit;
        private List<string> _menuOptions = new List<string>();
        private int _selectedOption = 0;
        private GameLevel? _selectedLevel;
        private ModeMetadata _selectedMode;
        private List<ModeMetadata> _availableModes;
        private IGameMode _currentMode;

        private KeyboardState _currentKeyboardState;
        private KeyboardState _previousKeyboardState;

        private ParticleManager _particleManager;
        private Texture2D _particleTexture;

        // Constructor with DI
        public Game1(
            IVoiceService voiceService,
            ModeDiscoveryService modeDiscovery)
        {
            _graphics = new GraphicsDeviceManager(this);
            _voiceService = voiceService ?? throw new ArgumentNullException(nameof(voiceService));
            _modeDiscovery = modeDiscovery ?? throw new ArgumentNullException(nameof(modeDiscovery));

            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _graphics.HardwareModeSwitch = true; // Enable exclusive fullscreen
            _graphics.IsFullScreen = true;
            _graphics.PreferredBackBufferWidth = 1920;
            _graphics.PreferredBackBufferHeight = 1080;
            _graphics.PreferMultiSampling = true;
            //_graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
        }

        protected override void Initialize()
        {
            // Create SpriteManager after GraphicsDevice is initialized
            _spriteManager = new SpriteManager(Content, GraphicsDevice);

            // Ensure the voice service is supported
            if (!_voiceService.IsSupported)
            {
                throw new PlatformNotSupportedException("Voice service is not supported on this platform.");
            }

            // Discover available modes
            _availableModes = _modeDiscovery.DiscoverModes();
            LoadStartQuitMenu();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteManager.LoadContent();
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            Resources.Fonts["MenuFont"] = Content.Load<SpriteFont>("Fonts/MenuFont");
            Resources.Fonts["QuitFont"] = Content.Load<SpriteFont>("Fonts/DefaultFont");

            _particleTexture = Content.Load<Texture2D>("Textures/plasma");
            //_particleTexture = Content.Load<Texture2D>("Textures/particle"); // Load a small circular texture
            _particleManager = new ParticleManager(_particleTexture);

            // Add a generator for mouse movement
            var mouseParticleGenerator = new ParticleGenerator(TimeSpan.FromMilliseconds(250), 500);
            _particleManager.AddGenerator(mouseParticleGenerator);
        }

        protected override void Update(GameTime gameTime)
        {
            _currentKeyboardState = Keyboard.GetState(); // Get current state

            if (_menuState == MenuState.ActiveMode)
            {
                HandleActiveModeInput(gameTime);
                _currentMode?.Update(gameTime);
                _spriteManager.Update(gameTime);
            }
            else
            {
                HandleMenuInput(_currentKeyboardState);
            }

            _previousKeyboardState = _currentKeyboardState; // Update previous state

            _particleManager.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(0, 0, 0, 0)); // Black background with transparency

            DrawParticles();
            DrawSprites(gameTime);

            base.Draw(gameTime);
        }

        private void DrawParticles()
        {
            // Draw particles with additive blending
            _spriteBatch.Begin(
                blendState: BlendState.Additive, // Additive blending for particles
                samplerState: SamplerState.PointClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone);

            _particleManager.Draw(_spriteBatch); // Only draws particles

            _spriteBatch.End(); // End the batch for particles
        }

        private void DrawSprites(GameTime gameTime)
        {
            _spriteBatch.Begin(
                            blendState: BlendState.AlphaBlend,
                            samplerState: SamplerState.PointClamp,
                            depthStencilState: DepthStencilState.None,
                            rasterizerState: RasterizerState.CullNone);

            //_spriteBatch.Begin(blendState: BlendState.AlphaBlend);
            //_spriteBatch.Begin();

            if (_menuState == MenuState.ActiveMode)
            {
                _currentMode?.Draw(_spriteBatch, gameTime);
                _spriteManager.Draw(_spriteBatch, gameTime);
            }
            else
            {
                DrawMenu();
            }

            _spriteBatch.End(); // End the batch for regular sprites
        }

        private void HandleMenuInput(KeyboardState keyboardState)
        {
            // Navigate menu with Up/Down arrows
            if (keyboardState.IsKeyDown(Keys.Up) && !_previousKeyboardState.IsKeyDown(Keys.Up))
            {
                if (_menuOptions.Count > 0)
                {
                    _selectedOption = (_selectedOption - 1 + _menuOptions.Count) % _menuOptions.Count;
                }
            }
            else if (keyboardState.IsKeyDown(Keys.Down) && !_previousKeyboardState.IsKeyDown(Keys.Down))
            {
                if (_menuOptions.Count > 0)
                {
                    _selectedOption = (_selectedOption + 1) % _menuOptions.Count;
                }
            }
            else if (keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
            {
                switch (_menuState)
                {
                    case MenuState.StartQuit:
                        HandleStartQuitSelection();
                        break;

                    case MenuState.LevelSelection:
                        HandleLevelSelection();
                        break;

                    case MenuState.ModeSelection:
                        HandleModeSelection();
                        break;
                }
            }
            else if (keyboardState.IsKeyDown(Keys.Escape))
            {
                HandleEscape();
            }
        }


        private void HandleStartQuitSelection()
        {
            if (_menuOptions[_selectedOption] == "Start")
            {
                _menuState = MenuState.LevelSelection;
                LoadLevelSelectionMenu();
            }
            else if (_menuOptions[_selectedOption] == "Quit")
            {
                Exit();
            }
        }

        private void HandleLevelSelection()
        {
            if (_menuOptions.Count == 0)
            {
                Console.WriteLine("No levels available.");
                _menuState = MenuState.StartQuit;
                LoadStartQuitMenu();
                return;
            }

            try
            {
                _selectedLevel = (GameLevel)Enum.Parse(typeof(GameLevel), _menuOptions[_selectedOption]);
                _menuState = MenuState.ModeSelection;
                LoadModeSelectionMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error selecting level: {ex.Message}");
                _menuState = MenuState.StartQuit;
                LoadStartQuitMenu();
            }
        }

        private void HandleModeSelection()
        {
            if (_menuOptions.Count == 0)
            {
                Console.WriteLine("No modes available for the selected level.");
                _menuState = MenuState.LevelSelection;
                LoadLevelSelectionMenu();
                return;
            }

            _selectedMode = _availableModes.FirstOrDefault(m => m.Name == _menuOptions[_selectedOption]);

            if (_selectedMode == null)
            {
                Console.WriteLine($"No mode found for {_menuOptions[_selectedOption]}.");
                return;
            }

            StartMode(_selectedMode);
        }

        private void HandleEscape()
        {
            switch (_menuState)
            {
                case MenuState.LevelSelection:
                    _menuState = MenuState.StartQuit;
                    LoadStartQuitMenu();
                    break;

                case MenuState.ModeSelection:
                    _menuState = MenuState.LevelSelection;
                    LoadLevelSelectionMenu();
                    break;
            }
        }


        private void HandleActiveModeInput(GameTime gameTime)
        {
            var pressedKeys = _currentKeyboardState.GetPressedKeys();

            // Define the specific keys required for the combination
            var requiredKeys = new[] { Keys.LeftControl, Keys.LeftAlt, Keys.LeftShift, Keys.D0 };

            // Check if the pressed keys match the required keys exactly
            if (pressedKeys.Length == requiredKeys.Length && requiredKeys.All(pressedKeys.Contains))
            {
                ExitMode();
            }
            else
            {
                var newKeys = pressedKeys.Where(key => _previousKeyboardState.IsKeyUp(key)).ToArray();

                if (newKeys.Any())
                {
                    // Pass all newly pressed keys to the current mode
                    _currentMode?.HandleKeyPress(newKeys, gameTime);
                }
            }
        }


        private void StartMode(ModeMetadata mode)
        {
            _currentMode = (IGameMode)Activator.CreateInstance(mode.ModeType);
            _currentMode.Initialize(this, _graphics, _spriteManager, _voiceService, _particleManager);
            _menuState = MenuState.ActiveMode;
        }

        private void ExitMode()
        {
            _currentMode?.Cleanup();
            _currentMode = null;
            _menuState = MenuState.StartQuit;
            LoadStartQuitMenu();
        }

        private void LoadStartQuitMenu()
        {
            _menuOptions = new List<string> { "Start", "Quit" };
            _selectedOption = 0;
        }

        private void LoadLevelSelectionMenu()
        {
            // Ensure _availableModes is already populated
            if (_availableModes == null || !_availableModes.Any())
            {
                throw new InvalidOperationException("No available game modes found. Ensure modes are discovered before loading menus.");
            }

            // Extract unique levels directly from each mode's Level property
            var levels = _availableModes
                .Select(mode => mode.Level.ToString())
                .Distinct()
                .ToList();

            // Populate menu options with levels and the "Back" option
            _menuOptions = levels;
            _menuOptions.Add("Back");

            _selectedOption = 0; // Reset selection to the first option
        }


        private void LoadModeSelectionMenu()
        {
            _menuOptions = _availableModes
                .Where(m => m.Level == _selectedLevel)
                .Select(m => m.Name)
                .ToList();
            _selectedOption = 0;
        }

        private void DrawMenu()
        {
            var center = new Vector2(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);

            // Use a larger font for the menu options
            var menuFont = Resources.Fonts["MenuFont"]; // Load a larger font for the menu options
            if (menuFont == null)
            {
                throw new InvalidOperationException("Font for 'MenuFont' not found in resources.");
            }

            var stringForHeightMeasurement = menuFont.MeasureString("THE QUICK BROWN FOX JUMPED OVER THE LAZY DOG");
            var stringForWidthMeasurement = menuFont.MeasureString("OPTIONS");
            var lineHeight = stringForHeightMeasurement.Y;
            var paddingY = lineHeight * 0.05f;
            var startX = center.X - (stringForWidthMeasurement.X / 2f);
            var startY = center.Y - ((_menuOptions.Count * lineHeight) / 2f);

            for (int i = 0; i < _menuOptions.Count; i++)
            {
                var color = i == _selectedOption ? Color.Yellow : Color.White;
                var textSize = menuFont.MeasureString(_menuOptions[i]);
                var position = new Vector2(startX, startY + (i * (lineHeight + paddingY))); // Adjust spacing for larger text
                _spriteManager.DrawText(_spriteBatch, menuFont, _menuOptions[i].ToUpper(), position, color);
            }

            DrawQuitMessage(_spriteBatch);
        }

        private void DrawQuitMessage(SpriteBatch spriteBatch)
        {
            // Load the font for the quit message
            var font = Resources.Fonts["QuitFont"]; // Keep the existing smaller font for the quit message

            if (font == null)
            {
                throw new InvalidOperationException("Font for 'QuitMessage' not found in resources.");
            }

            var screenWidth = GraphicsDevice.Viewport.Width;
            var screenHeight = GraphicsDevice.Viewport.Height;

            var message = "Press Escape to Quit";
            var fontSize = font.MeasureString(message);

            // Position the text slightly above the bottom-left corner
            var position = new Vector2(10, screenHeight - fontSize.Y - 10);

            spriteBatch.DrawString(font, message, position, Color.White);
        }


        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            _particleManager.Dispose();
            base.OnExiting(sender, args);
        }
    }
}

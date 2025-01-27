using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using KeyLearner.Core.Interfaces;
using System;
using KeyLearner.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Data;
using KeyLearner.Elements;

namespace KeyLearner.Modes
{
    public class ToddlerSmashMode : IGameMode
    {
        private Random _random = new Random();
        private SpriteManager _spriteManager;
        private ParticleManager _particleManager;
        private IVoiceService _voiceService;
        private GraphicsDeviceManager _graphics;
        private SpriteFont _fontCharacter;
        private SpriteFont _fontWord;

        private FixedLengthCircularBuffer _keyBuffer;
        private int _minWordLength;
        private int _maxWordLength;
        private Dictionary<string, (string ImagePath, string WavPath)> _wordDictionary = new();

        private readonly List<TextElement> _textElements = new();

        public string Name => "Smash";
        public GameLevel Level => GameLevel.Toddler;

        public ToddlerSmashMode()
        {
        }
        private void LoadWordDictionaries(string directoryPath = "data")
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directoryPath);

            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"The directory {directoryPath} was not found at {fullPath}.");
            }

            _wordDictionary = new Dictionary<string, (string ImagePath, string WavPath)>();
            _maxWordLength = 0;
            _minWordLength = int.MaxValue;

            // Get all dictionary files matching *_dictionary.csv
            var dictionaryFiles = Directory.GetFiles(fullPath, "*_dictionary.csv");

            foreach (var filePath in dictionaryFiles)
            {
                Console.WriteLine($"Loading dictionary file: {Path.GetFileName(filePath)}");

                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    bool skipHeaderFlag = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (skipHeaderFlag)
                        {
                            skipHeaderFlag = false;
                            continue;
                        }

                        var parts = line.Split(',');
                        var word = parts[0].Trim().ToUpper();

                        //skip whitespace
                        if (string.IsNullOrWhiteSpace(word)) continue;

                        // Skip duplicate words
                        if (_wordDictionary.ContainsKey(word)) continue;

                        var imagePath = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : null;
                        var wavPath = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : null;

                        _wordDictionary[word] = (ImagePath: imagePath, WavPath: wavPath);

                        // Track min and max word lengths
                        _maxWordLength = Math.Max(_maxWordLength, word.Length);
                        _minWordLength = Math.Min(_minWordLength, word.Length);
                    }
                }
            }

            Console.WriteLine($"Loaded {_wordDictionary.Count} unique words from {dictionaryFiles.Length} files.");
        }



        public void Initialize(Game game, GraphicsDeviceManager graphics, SpriteManager spriteManager, IVoiceService voiceService, ParticleManager particleManager)
        {
            _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
            _spriteManager = spriteManager ?? throw new ArgumentNullException(nameof(spriteManager));
            _voiceService = voiceService ?? throw new ArgumentNullException(nameof(voiceService));
            _particleManager = particleManager ?? throw new ArgumentNullException(nameof(particleManager));

            // Load a large font specific to this mode
            _fontCharacter = game.Content.Load<SpriteFont>("Fonts/SmashCharacterFont");
            _fontWord = game.Content.Load<SpriteFont>("Fonts/SmashWordFont");

            // Load the word dictionary
            LoadWordDictionaries();

            _keyBuffer = new FixedLengthCircularBuffer(_maxWordLength);
        }

        public void HandleKeyPress(Keys[] keys, GameTime gameTime)
        {
            if (keys == null || keys.Length == 0) return;

            // Combine the pressed keys into a garbled word
            var keyCharactersPressed = string.Empty;

            foreach (var key in keys)
            {
                if (key == Keys.None) continue;

                string keyText = GetKeyText(key);
                if (!string.IsNullOrEmpty(keyText) && keyText.Length == 1 && char.IsLetterOrDigit(keyText[0]))
                {
                    // Add each key to the garbled word
                    keyCharactersPressed += keyText;
                }
            }

            if (!string.IsNullOrEmpty(keyCharactersPressed))
            {
                var textElement = CreateTextElement(keyCharactersPressed, gameTime);

                if (keyCharactersPressed.Length == 1)
                {
                    // only want to treat it as part of spelling a word if the action was deliberate (typing a single character)
                    // Add the key to the buffer (convert to lowercase for case-insensitive matching)
                    _keyBuffer.Add(char.ToLower(keyCharactersPressed[0]), textElement);
                }

                // Convert buffer contents to uppercase for case-insensitive search
                var bufferContents = _keyBuffer.GetStringContents().ToUpper();

                // Search for matching words in the buffer
                for (int length = Math.Min(bufferContents.Length, _maxWordLength); length >= _minWordLength; length--)
                {
                    var substring = bufferContents.Substring(bufferContents.Length - length);

                    if (_wordDictionary.ContainsKey(substring))
                    {
                        RemoveAllElements();
                        AddWord(substring, gameTime);

                        Task.Run(async () =>
                        {
                            await Task.Delay(500); // 2-second delay
                            await _voiceService.SpeakAsync(substring);
                        });

                        break; // Stop searching after the first match
                    }
                }

                // Speak the word/letter
                Task.Run(async () => await _voiceService.SpeakAsync(keyCharactersPressed));
            }
        }


        public void RemoveAllElements()
        {
            // Clear the text elements list
            _textElements.Clear();

            // Notify the SpriteManager to remove all text elements
            _spriteManager.ClearAllTextElements();
        }


        private TextElement CreateTextElement(string keyText, GameTime gameTime)
        {
            var color = new Color(_random.Next(256), _random.Next(256), _random.Next(256));
            var textSize = _spriteManager.MeasureString(_fontCharacter, keyText);

            var x = _random.Next(0, _graphics.PreferredBackBufferWidth - (int)textSize.X);
            var y = _random.Next(0, _graphics.PreferredBackBufferHeight - (int)textSize.Y);
            var position = new Vector2(x, y);

            var textElement = _spriteManager.AddText(
                keyText,
                position,
                color,
                TimeSpan.FromSeconds(5),
                gameTime,
                _fontCharacter,
                particleManager: _particleManager,
                hasOutline: true,
                outlineThickness: 5f,
                outlineColorScheme: ColorScheme.RainbowBlended
            );
            _textElements.Add(textElement);

            return textElement;
        }

        private void AddWord(string word, GameTime gameTime)
        {
            var color = Color.White;
            var textSize = _spriteManager.MeasureString(_fontWord, word);

            var x = (_graphics.PreferredBackBufferWidth - (int)textSize.X) / 2;
            var y = (_graphics.PreferredBackBufferHeight - (int)textSize.Y) / 2;
            var position = new Vector2(x, y);

            var textElement = _spriteManager.AddText(
                word,
                position,
                color,
                TimeSpan.FromSeconds(5),
                gameTime,
                _fontWord,
                hasOutline: true,
                outlineThickness: 10f,
                outlineColorScheme: ColorScheme.Solid,
                solidOutlineColor: Color.Black,
                particleManager: _particleManager,
                isWord: true
            );
            _textElements.Add(textElement);

            AddPlasmaEffect(position, textSize, color.A / 255f, TimeSpan.FromSeconds(5), gameTime);
        }

        private void AddPlasmaEffect(Vector2 position, Vector2 textSize, float initialAlpha, TimeSpan lifetime, GameTime gameTime)
        {
            // Plasma particle generation parameters
            int particlesPerFrame = 250; // Number of particles emitted per frame
            float particleVelocity = 500f; // Velocity of particles
            float particleLifetime = (float)lifetime.TotalSeconds; // Particles fade with the word

            // Offset area for plasma to emit around the word
            var emissionBounds = new Rectangle(
                (int)(position.X - textSize.X / 2),
                (int)(position.Y - textSize.Y / 2),
                (int)(textSize.X * 2),
                (int)(textSize.Y * 2)
            );

            for (int i = 0; i < particlesPerFrame; i++)
            {
                // Randomize particle position within bounds
                var particlePosition = new Vector2(
                    _random.Next(emissionBounds.Left, emissionBounds.Right),
                    _random.Next(emissionBounds.Top, emissionBounds.Bottom)
                );

                // Randomize velocity
                var velocity = new Vector2(
                    _random.NextFloat(-particleVelocity, particleVelocity),
                    _random.NextFloat(-particleVelocity, particleVelocity)
                );

                // Add particle to the particle system
                _particleManager.AddParticle(
                    gameTime,
                    new System.Numerics.Vector3(particlePosition.X, particlePosition.Y, 0),
                    new System.Numerics.Vector3(velocity.X, velocity.Y, 0),
                    particleLifetime,
                    initialAlpha // Match the word's initial alpha
                );
            }
        }


        public void Update(GameTime gameTime)
        {
            _textElements.RemoveAll(te => te.ExpirationTime < gameTime.TotalGameTime);

            // Apply physics: unpin and make text fall after 0.5 seconds
            foreach (var textElement in _textElements)
            {
                if (textElement.IsPinned && gameTime.TotalGameTime > textElement.ExpirationTime - TimeSpan.FromSeconds(4.5))
                {
                    textElement.IsPinned = false; // Unpin to allow falling
                }
            }
        }


        private string GetKeyText(Keys key)
        {
            return key switch
            {
                // Function keys
                >= Keys.F1 and <= Keys.F12 => key.ToString(),

                // Number keys
                >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(),
                >= Keys.NumPad0 and <= Keys.NumPad9 => ((int)key - (int)Keys.NumPad0).ToString(),

                // Alphabet keys
                >= Keys.A and <= Keys.Z => ((char)((int)key - (int)Keys.A + 'A')).ToString(),

                // Special keys and symbols
                Keys.Escape => "ESC",
                Keys.Space => "Space",
                Keys.Enter => "Enter",
                Keys.Back => "Backspace",
                Keys.Tab => "Tab",
                Keys.CapsLock => "Caps Lock",

                // Modifier keys
                Keys.LeftShift or Keys.RightShift => "Shift",
                Keys.LeftControl or Keys.RightControl => "Ctrl",
                Keys.LeftAlt or Keys.RightAlt => "Alt",

                // Arrow keys
                Keys.Left => "Left Arrow",
                Keys.Right => "Right Arrow",
                Keys.Up => "Up Arrow",
                Keys.Down => "Down Arrow",

                // Numeric and symbol keys
                Keys.OemPlus => "+",
                Keys.OemMinus => "-",
                Keys.OemPeriod => ".",
                Keys.OemComma => ",",
                Keys.OemQuestion => "/",
                Keys.OemOpenBrackets => "[",
                Keys.OemCloseBrackets => "]",
                Keys.OemPipe => "\\",
                Keys.OemSemicolon => ";",
                Keys.OemQuotes => "'",

                // Additional common keys
                Keys.Delete => "Delete",
                Keys.Home => "Home",
                Keys.End => "End",
                Keys.PageUp => "Page Up",
                Keys.PageDown => "Page Down",
                Keys.Insert => "Insert",

                // Default fallback for other keys
                _ => key.ToString()
            };
        }

        public void Draw(SpriteBatch spriteBatch, GameTime gameTime) { /* No-op */ }

        public void Cleanup() { /* No-op */ }
    }
}
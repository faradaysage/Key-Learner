using KeyLearner.Core.Interfaces;
using System.Speech.Synthesis;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace KeyLearner.Platforms.Windows
{
    [SupportedOSPlatform("windows")]
    public class WindowsVoiceService : IVoiceService
    {
        public bool IsSupported => true;

        public WindowsVoiceService()
        {
            // Initialization logic, if needed
        }

        public void Speak(string text)
        {
            Task.Run(() => SpeakAsync(text)); // Fire and forget for immediate playback
        }

        public async Task SpeakAsync(string text)
        {
            await Task.Run(() =>
            {
                using var synthesizer = CreateSpeechSynthesizer();
                synthesizer.Speak(text);
            });
        }

        public void Stop()
        {
            // It's difficult to cancel individual async operations without maintaining
            // a collection of active SpeechSynthesizer instances.
            // You may want to implement a more sophisticated tracking system if needed.
        }

        private SpeechSynthesizer CreateSpeechSynthesizer()
        {
            var synthesizer = new SpeechSynthesizer
            {
                Rate = 2 // Adjust as needed
            };
            synthesizer.SelectVoiceByHints(VoiceGender.Neutral, VoiceAge.Adult);
            return synthesizer;
        }
    }
}

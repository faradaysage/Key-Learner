using KeyLearner.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace KeyLearner.Platforms.Linux
{
    public class LinuxVoiceService : IVoiceService
    {
        public bool IsSupported => false;

        public void Speak(string text)
        {
            Console.WriteLine($"[LinuxVoiceService] Speak: {text}");
        }

        public async Task SpeakAsync(string text)
        {
            Console.WriteLine($"[LinuxVoiceService] SpeakAsync: {text}");
        }

        public void Stop()
        {
            Console.WriteLine("[LinuxVoiceService] Stop");
        }
    }
}

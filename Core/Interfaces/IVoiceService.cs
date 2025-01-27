using System.Threading.Tasks;

namespace KeyLearner.Core.Interfaces
{
    public interface IVoiceService
    {
        void Speak(string text);
        Task SpeakAsync(string text);
        void Stop();
        bool IsSupported { get; }
    }
}
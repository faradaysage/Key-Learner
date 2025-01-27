using KeyLearner.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.InteropServices;

namespace KeyLearner.Core.Services
{
    public static class VoiceServiceFactory
    {
        public static void RegisterVoiceService(IServiceCollection services)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                services.AddSingleton<IVoiceService, Platforms.Windows.WindowsVoiceService>();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                services.AddSingleton<IVoiceService, Platforms.Linux.LinuxVoiceService>();
            }
            else
            {
                throw new PlatformNotSupportedException("Voice services are not supported on this platform.");
            }
        }

        //public static IVoiceService CreateVoiceService()
        //{
        //    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        //    {
        //        return new Platforms.Windows.WindowsVoiceService();
        //    }
        //    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        //    {
        //        return new Platforms.Linux.LinuxVoiceService(); // Placeholder for Linux
        //    }
        //    else
        //    {
        //        throw new PlatformNotSupportedException("Voice services are not supported on this platform.");
        //    }
        //}
    }
}

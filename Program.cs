using KeyLearner.Core.Interfaces;
using KeyLearner.Core.Services;
using KeyLearner.Modes;
using KeyLearner.Platforms.Linux;
using KeyLearner.Platforms.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;

namespace KeyLearner
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Create Game1 instance
            var game = serviceProvider.GetRequiredService<Game1>();                        
            game.Run();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Register platform-specific voice service
            VoiceServiceFactory.RegisterVoiceService(services);

            // Register core services
            services.AddSingleton<ModeDiscoveryService>();

            // Register all game modes
            services.AddTransient<ToddlerSmashMode>(); // Example game mode

            // Register Game1 as a singleton to ensure a consistent instance
            services.AddSingleton<Game1>();
        }
    }
}

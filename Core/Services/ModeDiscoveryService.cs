using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KeyLearner.Core.Interfaces;
using KeyLearner.Core.Models;

namespace KeyLearner.Core.Services
{
    public class ModeDiscoveryService
    {
        private readonly IServiceProvider _serviceProvider;

        public ModeDiscoveryService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public List<ModeMetadata> DiscoverModes()
        {
            var modes = new List<ModeMetadata>();

            // Get the directory of the currently executing assembly
            var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Look for DLLs in the current directory
            var modeDlls = Directory.GetFiles(currentDirectory, "*.dll");

            foreach (var dllPath in modeDlls)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var modeTypes = assembly.GetTypes()
                        .Where(t => typeof(IGameMode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var modeType in modeTypes)
                    {
                        // Use DI to create an instance
                        var mode = (IGameMode)_serviceProvider.GetService(modeType);
                        if (mode == null)
                        {
                            throw new InvalidOperationException($"Unable to resolve service for mode type: {modeType.FullName}");
                        }

                        modes.Add(new ModeMetadata
                        {
                            Name = mode.Name,
                            Level = mode.Level,
                            ModeType = modeType
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log or handle mode discovery errors
                    Console.WriteLine($"Error discovering modes in {dllPath}: {ex.Message}");
                }
            }

            return modes;
        }
    }
}

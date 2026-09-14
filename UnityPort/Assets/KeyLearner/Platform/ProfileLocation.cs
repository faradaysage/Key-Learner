using System;
using System.IO;
namespace KeyLearner.Unity.Platform
{
    /// <summary>The explicit profile contract shared by preview, parent shortcut, and normal player startup.</summary>
    public static class ProfileLocation
    {
        public static string Resolve(bool isolatedPreview, string explicitDataRoot)
        {
            if (!string.IsNullOrWhiteSpace(explicitDataRoot))
                return Path.GetFullPath(explicitDataRoot);
            return isolatedPreview ? Path.Combine(Path.GetTempPath(), "KeyLearner-Unity-preview-" + Guid.NewGuid().ToString("N")) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyLearner");
        }
    }
}

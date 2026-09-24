#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace KeyLearner.Unity.Editor
{
    // The offline player has no network features. Remove Unity's inferred permission
    // from the generated Android project, leaving Windows and source templates alone.
    public sealed class AndroidManifestPolicy : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = XDocument.Load(manifest);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            foreach (var permission in document.Root.Elements("uses-permission")
                .Where(e => (string)e.Attribute(android + "name") == "android.permission.INTERNET").ToArray())
                permission.Remove();
            document.Save(manifest);
        }
    }
}
#endif

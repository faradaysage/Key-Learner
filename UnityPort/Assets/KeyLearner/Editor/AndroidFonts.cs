using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEditor;
using UnityEngine;

namespace KeyLearner.Unity.Editor
{
    public sealed class AndroidFonts : AssetPostprocessor
    {
        const string Characters = "Assets/KeyLearner/Editor/android-font-characters.json";
        public static void Validate()
        {
            foreach (var name in new[] { "Fredoka-Play", "BalooBhai2-Play" })
            {
                var font = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/KeyLearnerAndroidBuild/Resources/AndroidContent/Fonts/" + name + ".ttf");
                if (!font || font.dynamic || font.fontSize != 96 || !font.HasCharacter('A') || !font.HasCharacter('0'))
                    throw new System.InvalidOperationException("Invalid Android font atlas: " + name);
                var texture = font.material.mainTexture;
                // Font atlases store black RGB with coverage in alpha. GUI textures
                // multiply RGB by the label color, unlike the legacy font shader.
                // Bake a white-RGB UI copy once; no runtime readback or rasterization.
                if (name == "Fredoka-Play") BakeUiAtlas(font, name);
                UnityEngine.Debug.Log("KEYLEARNER_ANDROID_FONT " + name + " characters=" + font.characterInfo.Length + " atlas=" + texture.width + "x" + texture.height);
            }
        }
        static void BakeUiAtlas(Font font, string name)
        {
            const int width = 1024, padding = 2;
            string root = "Assets/KeyLearnerAndroidBuild/Resources/AndroidContent/Fonts/" + name;
            var atlas = (Texture2D)font.material.mainTexture;
            var serialized = new SerializedObject(atlas);
            var readable = serialized.FindProperty("m_IsReadable");
            bool wasReadable = atlas.isReadable;
            if (!wasReadable && readable != null) { readable.boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            Texture2D texture = null;
            try
            {
                var source = atlas.GetPixels32();
                var placements = new Dictionary<int, RectInt>();
                var characters = font.characterInfo.Where(g => g.maxX > g.minX && g.maxY > g.minY)
                    .OrderByDescending(g => g.maxY - g.minY).ThenBy(g => g.index).ToArray();
                int x = padding, y = padding, rowHeight = 0;
                foreach (var glyph in characters)
                {
                    int w = glyph.maxX - glyph.minX, h = glyph.maxY - glyph.minY;
                    if (w + padding * 2 > width) throw new System.InvalidOperationException("Oversized UI glyph");
                    if (x + w + padding > width) { x = padding; y += rowHeight + padding * 2; rowHeight = 0; }
                    placements.Add(glyph.index, new RectInt(x,y,w,h));
                    x += w + padding * 2; rowHeight = Mathf.Max(rowHeight,h);
                }
                int height = Mathf.NextPowerOfTwo(y + rowHeight + padding);
                if (height > 4096) throw new System.InvalidOperationException("Android UI atlas exceeds its memory budget");
                var pixels = new Color32[width * height];
                for (int i=0;i<pixels.Length;i++) pixels[i] = new Color32(255,255,255,0);
                var coordinates = new Dictionary<int,float[]>();
                foreach (var glyph in characters)
                {
                    var r = placements[glyph.index];
                    // Repack upright, sampling the imported glyph's complete UV basis.
                    // Runtime GUI drawing then needs no per-character matrix changes.
                    for (int py=0;py<r.height;py++) for (int px=0;px<r.width;px++)
                    {
                        var uv = glyph.uvTopLeft + (glyph.uvTopRight-glyph.uvTopLeft)*((px+.5f)/r.width)
                            + (glyph.uvBottomLeft-glyph.uvTopLeft)*(1-(py+.5f)/r.height);
                        int sx = Mathf.Clamp(Mathf.FloorToInt(uv.x*atlas.width),0,atlas.width-1);
                        int sy = Mathf.Clamp(Mathf.FloorToInt(uv.y*atlas.height),0,atlas.height-1);
                        pixels[(r.y+py)*width+r.x+px] = new Color32(255,255,255,source[sy*atlas.width+sx].a);
                    }
                    coordinates.Add(glyph.index,new[] {r.x/(float)width,r.y/(float)height,r.width/(float)width,r.height/(float)height});
                }
                texture = new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                texture.SetPixels32(pixels);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.name = name + " upright UI atlas";
                texture.Apply(false,true);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(root+"-UI.asset")) AssetDatabase.DeleteAsset(root+"-UI.asset");
                AssetDatabase.CreateAsset(texture,root+"-UI.asset");texture=null;
                File.WriteAllText(root+"-UI-UV.json",JsonSerializer.Serialize(coordinates));
                AssetDatabase.ImportAsset(root+"-UI-UV.json");
                Debug.Log("KEYLEARNER_ANDROID_UI_ATLAS upright="+characters.Length+" size="+width+"x"+height);
            }
            finally
            {
                if (!wasReadable && readable != null) { readable.boolValue=false;serialized.ApplyModifiedPropertiesWithoutUndo(); }
                if (texture) Object.DestroyImmediate(texture);
            }
        }
        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/KeyLearnerAndroidBuild/Resources/AndroidContent/Fonts/") || !(assetImporter is TrueTypeFontImporter importer)) return;
            var sets = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Characters));
            if (!sets.TryGetValue(Path.GetFileNameWithoutExtension(assetPath), out var characters)) return;
            context.DependsOnSourceAsset(Characters);
            importer.fontTextureCase = FontTextureCase.CustomSet;
            importer.customCharacters = characters;
            importer.fontSize = 96;
            importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
            importer.includeFontData = false;
        }
    }
}

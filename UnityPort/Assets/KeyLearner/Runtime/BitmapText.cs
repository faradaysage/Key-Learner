using System.Collections.Generic;
using UnityEngine;

namespace KeyLearner.Unity
{
    // IMGUI in Unity 6.6 converts GUIStyle fonts to runtime TextCore faces.
    // Draw the imported atlas explicitly to avoid runtime rasterization on Android.
    internal static class BitmapText
    {
        static Texture uiAtlas;
        static Material uiMaterial;
        static Dictionary<int,float[]> uiCoordinates;
        sealed class Line { public string Text; public float Width; }
        static readonly Dictionary<(Font, string, int, float), Line[]> layouts = new Dictionary<(Font, string, int, float), Line[]>();
        static readonly Dictionary<Font, Dictionary<char, CharacterInfo>> glyphs = new Dictionary<Font, Dictionary<char, CharacterInfo>>();
        static Dictionary<char, CharacterInfo> Glyphs(Font font)
        {
            if (!glyphs.TryGetValue(font, out var map))
            {
                map = new Dictionary<char, CharacterInfo>();
                foreach (var glyph in font.characterInfo) map[(char)glyph.index] = glyph;
                glyphs.Add(font, map);
            }
            return map;
        }
        static CharacterInfo Glyph(Dictionary<char, CharacterInfo> map, char c)
        {
            if (map.TryGetValue(c, out var glyph)) return glyph;
            return map.TryGetValue('?', out glyph) ? glyph : default;
        }
        static Line[] Layout(Font font, string text, int size, float width)
        {
            var key = (font, text ?? "", size, width);
            if (layouts.TryGetValue(key, out var found)) return found;
            var map = Glyphs(font);
            float scale = size / (float)font.fontSize;
            var lines = new List<Line>();
            foreach (string paragraph in (text ?? "").Replace("\r", "").Split('\n'))
            {
                int start = 0;
                while (start < paragraph.Length)
                {
                    int end = start, space = -1;
                    float length = 0, beforeSpace = 0;
                    while (end < paragraph.Length)
                    {
                        float next = Glyph(map, paragraph[end]).advance * scale;
                        if (length + next > width && end > start) break;
                        if (paragraph[end] == ' ') { space = end; beforeSpace = length; }
                        length += next; end++;
                    }
                    int resume = end;
                    if (end < paragraph.Length && space > start) { end = space; resume = space + 1; length = beforeSpace; }
                    lines.Add(new Line { Text = paragraph.Substring(start, end - start), Width = length });
                    start = resume;
                    while (start < paragraph.Length && paragraph[start] == ' ') start++;
                }
                if (paragraph.Length == 0) lines.Add(new Line { Text = "", Width = 0 });
            }
            if (layouts.Count >= 256) layouts.Clear();
            var result = lines.ToArray(); layouts[key] = result; return result;
        }
        static float LineHeight(Font font, int size) => Mathf.Max(font.lineHeight, font.fontSize) * size / (float)font.fontSize;
        public static float Height(Font font, string text, int size, float width) => Layout(font, text, size, width).Length * LineHeight(font, size);
        public static string[] Pages(Font font, string text, int size, float width, float height)
        {
            var lines = Layout(font, text, size, width);
            int perPage = Mathf.Max(1, Mathf.FloorToInt(height / LineHeight(font, size)));
            var pages = new List<string>();
            for (int start = 0; start < lines.Length; start += perPage)
            {
                var page = new List<string>();
                for (int i = start; i < Mathf.Min(start + perPage, lines.Length); i++) page.Add(lines[i].Text);
                pages.Add(string.Join("\n", page));
            }
            return pages.Count == 0 ? new[] { "" } : pages.ToArray();
        }
        public static void Draw(Font font, Rect rect, string text, int size, Color color, TextAnchor anchor)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            var lines = Layout(font, text, size, rect.width);
            var map = Glyphs(font);
            float scale = size / (float)font.fontSize, lineHeight = LineHeight(font, size);
            int horizontal = (int)anchor % 3, vertical = (int)anchor / 3;
            float y = rect.y + (rect.height - lines.Length * lineHeight) * vertical * .5f;
            if (!uiAtlas) uiAtlas = Resources.Load<Texture2D>("AndroidContent/Fonts/Fredoka-Play-UI");
            var atlas = uiAtlas;
            var oldColor = GUI.color;
            GUI.color = color;
            try
            {
                foreach (var line in lines)
                {
                    float x = rect.x + (rect.width - line.Width) * horizontal * .5f;
                    foreach (char c in line.Text)
                    {
                        var glyph = Glyph(map, c);
                        var target = new Rect(x + glyph.minX * scale, y + (font.ascent - glyph.maxY) * scale,
                            (glyph.maxX - glyph.minX) * scale, (glyph.maxY - glyph.minY) * scale);
                        if (target.width > 0 && target.height > 0) DrawGlyph(atlas, target, glyph);
                        x += glyph.advance * scale;
                    }
                    y += lineHeight;
                }
            }
            finally { GUI.color = oldColor; }
        }
        static void DrawGlyph(Texture atlas, Rect target, CharacterInfo glyph)
        {
            if (uiCoordinates == null)
            {
                var data = Resources.Load<TextAsset>("AndroidContent/Fonts/Fredoka-Play-UI-UV");
                if (!data) throw new System.InvalidOperationException("Android UI glyph coordinates are missing");
                try { uiCoordinates = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int,float[]>>(data.text); }
                finally { Resources.UnloadAsset(data); }
            }
            if (!uiCoordinates.TryGetValue(glyph.index,out var uv)) return;
            // Keep glyph rendering independent of IMGUI's shared blend material.
            // In particular, UI glyphs must never be depth-tested against game effects.
            if (!uiMaterial)
            {
                var shader = Resources.Load<Shader>("Shaders/BitmapUi");
                if (!shader) throw new System.InvalidOperationException("Android UI shader is missing");
                uiMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            Graphics.DrawTexture(target, atlas, new Rect(uv[0],uv[1],uv[2],uv[3]),
                0, 0, 0, 0, GUI.color, uiMaterial);
        }
    }
}

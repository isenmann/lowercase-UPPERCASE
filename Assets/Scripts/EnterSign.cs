using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace LowercaseUppercase
{
    /// <summary>
    /// The Enter sign (↵) for text: Space Mono has no such glyph, so it is drawn here once and handed to TextMeshPro
    /// as a one-sprite asset, sized and spaced like one letter of the font it stands in. Where that can't be done,
    /// <see cref="Text"/> falls back to a plain arrow.
    /// </summary>
    public static class EnterSign
    {
        const string SpriteName = "enter";
        const string Fallback = "←";
        // Texture height, which TextMeshPro maps onto the font's ascent.
        const int Height = 96;

        static TMP_SpriteAsset asset;
        static bool tried;

        /// <summary>Lets the text show the sign; call before giving it <see cref="Text"/>.</summary>
        public static void Use(TMP_Text text)
        {
            if (text == null) return;
            TMP_SpriteAsset sprites = Asset(text.font);
            if (sprites != null) text.spriteAsset = sprites;
        }

        /// <summary>The sign as rich text, taking the colour of the text around it.</summary>
        public static string Text => asset != null ? $"<sprite name=\"{SpriteName}\" tint=1>" : Fallback;

        static TMP_SpriteAsset Asset(TMP_FontAsset font)
        {
            if (tried) return asset;
            tried = true;
            if (font == null) return null;

            Material material = SpriteMaterial();
            if (material == null) return null;

            // One letter wide, so the name stays in its columns.
            FaceInfo face = font.faceInfo;
            float advance = face.ascentLine * 0.6f;
            if (font.HasCharacter('A', false, true) && font.characterLookupTable.TryGetValue('A', out TMP_Character a))
                advance = a.glyph.metrics.horizontalAdvance;
            int width = Mathf.Max(8, Mathf.RoundToInt(Height * advance / face.ascentLine));
            float capHeight = face.capLine > 0 ? Height * face.capLine / face.ascentLine : Height * 0.7f;

            Texture2D texture = Draw(width, capHeight);
            material.SetTexture(ShaderUtilities.ID_MainTex, texture);

            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(width, Height, 0, Height, width),
                new GlyphRect(0, 0, width, Height), 1f, 0);
            var character = new TMP_SpriteCharacter(0xE000, glyph) { name = SpriteName };

            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "Enter Sign";
            asset.hideFlags = HideFlags.DontSave;
            asset.spriteSheet = texture;
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(character);
            // Before the material is set: with a material and no version, TextMeshPro would "upgrade" the asset from
            // its old sprite list and lose the tables.
            asset.UpdateLookupTables();
            asset.material = material;
            return asset;
        }

        static Material SpriteMaterial()
        {
            TMP_SpriteAsset builtIn = TMP_Settings.defaultSpriteAsset;
            if (builtIn != null && builtIn.material != null) return new Material(builtIn.material) { hideFlags = HideFlags.DontSave };
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            return shader != null ? new Material(shader) { hideFlags = HideFlags.DontSave } : null;
        }

        /// <summary>
        /// White ↵ on transparent, standing on the baseline (the bottom row) and as tall as a capital: a stem coming
        /// down on the right, turning left into a line that ends in an arrowhead.
        /// </summary>
        static Texture2D Draw(int width, float capHeight)
        {
            float stroke = Height * 0.085f;
            float half = stroke * 0.5f;
            float right = width - width * 0.18f;
            float left = width * 0.14f;
            float top = capHeight;
            float line = capHeight * 0.28f;
            float head = capHeight * 0.28f;
            var segments = new List<Vector4>
            {
                new Vector4(right, top, right, line),
                new Vector4(right, line, left, line),
                new Vector4(left, line, left + head, line + head),
                new Vector4(left, line, left + head, line - head),
            };

            var texture = new Texture2D(width, Height, TextureFormat.RGBA32, false)
            {
                name = "Enter Sign",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float distance = float.MaxValue;
                foreach (Vector4 s in segments)
                    distance = Mathf.Min(distance, DistanceToSegment(p, new Vector2(s.x, s.y), new Vector2(s.z, s.w)));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(half - distance + 0.5f) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}

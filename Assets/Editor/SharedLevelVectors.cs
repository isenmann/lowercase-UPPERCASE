using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LowercaseUppercase.Levels;
using TMPro;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Writes Web/test/vectors.json for the shared levels page: level codes with what this game makes of them, what
    /// every piece of a few levels looks like once built, and the font measurements the page lays text out with. The
    /// page's JavaScript is tested against it, so the game's C# code stays the reference. Rerun it after changing
    /// level codes, CustomLevelData or how pieces are built.
    /// It works in Play mode, entering it if need be and leaving it again: enemies lift their glyphs as they wake, and
    /// components only wake in Play mode, as they do when the game builds a level or its picture.
    /// </summary>
    [InitializeOnLoad]
    public static class SharedLevelVectors
    {
        const string OutputPath = "Web/test/vectors.json";
        const string SampleLevelPath = "Assets/Levels/SampleCustomLevel.json";
        const string PendingKey = "LowercaseUppercase.SharedLevelVectors.Pending";
        const float PixelsPerUnit = 72f;
        const float MeasureFontPx = 100f;
        static readonly string[] FontNames = { "SpaceMono-Regular", "SpaceMono-Bold", "ArchivoBlack-Regular" };

        // Characters measured beyond those in the levels: everything a maker is likely to type.
        const string CommonCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "\u00a1\u00a7\u00ab\u00b0\u00b7\u00bb\u00c4\u00d6\u00dc\u00df\u00e4\u00f6\u00fc\u2195";

        static SharedLevelVectors()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
                SessionState.EraseBool(PendingKey);
                try
                {
                    WriteNow();
                }
                finally
                {
                    EditorApplication.ExitPlaymode();
                }
            };
        }

        [MenuItem("lowercase UPPERCASE/Shared Levels/Write Test Vectors")]
        public static void Write()
        {
            if (EditorApplication.isPlaying)
            {
                WriteNow();
                return;
            }

            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        static void WriteNow()
        {
            CustomLevelData sample = CustomLevelData.FromJson(File.ReadAllText(SampleLevelPath));
            sample.name = "Sample";
            sample.MarkFinished();
            CustomLevelData everyPiece = EveryPieceLevel();
            CustomLevelData awkward = AwkwardTextLevel();

            var file = new VectorFile
            {
                limits = new Limits
                {
                    currentVersion = CustomLevelData.CurrentVersion,
                    maxJsonBytes = LevelShareCode.MaxJsonBytes,
                    maxNameLength = LevelSettings.MaxNameLength,
                    maxSentenceLength = LevelSettings.MaxSentenceLength,
                    maxWordLength = LevelEditor.MaxWordLength,
                    minSeconds = LevelSettings.MinSeconds,
                    maxSeconds = LevelSettings.MaxSeconds
                },
                codes = Codes(sample, everyPiece, awkward),
                layouts = new List<LayoutVector>()
            };

            foreach ((string label, CustomLevelData level) in new[]
                     { ("sample", sample), ("every piece", everyPiece), ("awkward text", awkward) })
                file.layouts.Add(Layout(label, level));

            var characters = new HashSet<char>(CommonCharacters);
            foreach (LayoutVector layout in file.layouts)
                foreach (PartVector part in layout.pieces.SelectMany(piece => piece.parts).Concat(layout.others))
                    characters.UnionWith(StripTags(part.text).Where(c => c != '\n'));
            file.fonts = FontNames.Select(name => Measure(name, characters)).ToList();

            File.WriteAllText(OutputPath, JsonUtility.ToJson(file, true) + "\n", new UTF8Encoding(false));
            Debug.Log($"Wrote {file.codes.Count} codes, {file.layouts.Count} layouts and {file.fonts.Count} fonts to {OutputPath}.");
        }

        // ------------------------------------------------ the levels

        /// <summary>One of every palette item, each on a word of its own, under a ceiling so drips have one to hang from.</summary>
        static CustomLevelData EveryPieceLevel()
        {
            var level = new CustomLevelData { name = "Every piece", ceiling = true };
            float x = 200f;
            foreach (PaletteItem item in EditorPalette.All)
            {
                bool spark = item.Type == CustomPieceType.Enemy && item.Kind == CustomPieceType.Spark;
                if (!EditorPalette.IsGround(new CustomPiece(item.Type, 0f, 0f)))
                {
                    var ground = new CustomPiece(CustomPieceType.Word, x - 60f, CustomLevelData.GroundPx, "ground",
                        spark ? CustomPieceType.Hot : CustomPieceType.Solid);
                    level.pieces.Add(ground);
                }

                CustomPiece piece = EditorPalette.NewPiece(item, x, CustomLevelData.GroundPx);
                if (piece.type == CustomPieceType.Word)
                    piece.text = piece.kind == CustomPieceType.Plank ? "--------" : $"{piece.kind} word";
                else if (piece.type == CustomPieceType.Kerned) piece.text = "b¡etwe¡en";
                else if (!EditorPalette.Stands(piece) && piece.type != CustomPieceType.CapsLock &&
                         piece.type != CustomPieceType.Wind &&
                         piece.type != CustomPieceType.Hill && piece.type != CustomPieceType.Lift)
                    piece.y = 360f;
                level.pieces.Add(piece);

                x += piece.type == CustomPieceType.CapsLock ? 1000f : piece.type == CustomPieceType.Wind ? 800f : 360f;
            }

            level.MarkFinished();
            return level;
        }

        /// <summary>Quotes, backslashes and letters past ASCII, which JSON escapes or not, in the name and the words.</summary>
        static CustomLevelData AwkwardTextLevel()
        {
            var level = new CustomLevelData
            {
                name = "say \"hi\" \\ ü \U0001F600",
                sentenceBefore = "über die B",
                missingLetter = "A",
                sentenceAfter = "NK",
                seconds = LevelSettings.MaxSeconds,
                ceiling = true,
                startX = 90.5f,
            };
            level.pieces.Add(new CustomPiece(CustomPieceType.Word, 40f, CustomLevelData.GroundPx, "a \"quoted\" back\\slash",
                CustomPieceType.Solid));
            level.pieces.Add(new CustomPiece(CustomPieceType.Word, 900f, 440.25f, "grüße", CustomPieceType.Ice));
            level.pieces.Add(new CustomPiece(CustomPieceType.Exit, 1400f, CustomLevelData.GroundPx, "B_NK", "A"));
            level.pieces.Add(new CustomPiece(CustomPieceType.Word, 1300f, CustomLevelData.GroundPx, "bench", CustomPieceType.Solid));
            level.MarkFinished();
            return level;
        }

        static List<CodeVector> Codes(CustomLevelData sample, CustomLevelData everyPiece, CustomLevelData awkward)
        {
            CustomLevelData unfinished = Copy(sample);
            unfinished.finishedHash = "";

            CustomLevelData edited = Copy(sample);
            edited.pieces[0].x += 8f;

            CustomLevelData renamed = Copy(sample);
            renamed.name = "Renamed after";

            string sampleCode = LevelShareCode.Encode(sample);
            string plain = LevelShareCode.PlainPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(sample.ToJson(false)));
            string spaced = sampleCode.Substring(0, 20) + "\n  " + sampleCode.Substring(20, 30) + "\r\n" + sampleCode.Substring(50);

            return new List<CodeVector>
            {
                Vector("sample", sampleCode),
                Vector("every piece", LevelShareCode.Encode(everyPiece)),
                Vector("awkward text", LevelShareCode.Encode(awkward)),
                Vector("unfinished", LevelShareCode.Encode(unfinished)),
                Vector("edited after finishing", LevelShareCode.Encode(edited)),
                Vector("renamed after finishing", LevelShareCode.Encode(renamed)),
                Vector("plain base64", plain),
                Vector("broken over lines", spaced),
                Vector("cut short", sampleCode.Substring(0, sampleCode.Length / 2)),
                Vector("not a code", "hello letters"),
            };
        }

        static CustomLevelData Copy(CustomLevelData level) => CustomLevelData.FromJson(level.ToJson(false));

        static CodeVector Vector(string label, string code)
        {
            var vector = new CodeVector { label = label, code = code };
            try
            {
                CustomLevelData level = LevelShareCode.Decode(code);
                vector.gameReads = true;
                vector.finished = level.FinishedByMaker;
                vector.contentHash = level.ContentHash();
            }
            catch (FormatException)
            {
                vector.gameReads = false;
            }
            return vector;
        }

        // ------------------------------------------------ layouts

        static LayoutVector Layout(string label, CustomLevelData level)
        {
            var root = new GameObject("Vector Level");
            try
            {
                var builtBy = new Dictionary<Transform, CustomPiece>();
                CustomLevelLayout layout = FrozenLevel.Build(root.transform, level,
                    Font("SpaceMono-Regular"), Font("SpaceMono-Bold"), Font("ArchivoBlack-Regular"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/White.png"),
                    (piece, part) => builtBy[part] = piece);

                var vector = new LayoutVector
                {
                    label = label,
                    code = LevelShareCode.Encode(level),
                    rightPx = Round(layout.RightPx),
                    pieces = level.pieces.Select((piece, index) => new PieceVector
                        { index = index, type = piece.type, kind = piece.kind, parts = new List<PartVector>() }).ToList(),
                    others = new List<PartVector>()
                };

                foreach (Transform child in root.transform)
                {
                    List<PartVector> parts = builtBy.TryGetValue(child, out CustomPiece piece)
                        ? vector.pieces[level.pieces.IndexOf(piece)].parts
                        : vector.others;
                    parts.AddRange(Parts(child));
                }
                return vector;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A font's lines and every character's advance and ink box, per pixel of font size, y upwards from the
        /// baseline. The ink box is TextMesh Pro's own character box, which is what words and enemies are fitted to.
        /// </summary>
        static FontVector Measure(string name, HashSet<char> characters)
        {
            TMP_FontAsset font = Font(name);
            FaceInfo face = font.faceInfo;
            float perPx = face.scale / face.pointSize;
            var vector = new FontVector
            {
                name = name,
                ascentLine = Round4(face.ascentLine * perPx),
                descentLine = Round4(face.descentLine * perPx),
                meanLine = Round4(face.meanLine * perPx),
                capLine = Round4(face.capLine * perPx),
                lineHeight = Round4(face.lineHeight * perPx),
                characters = new List<CharacterVector>()
            };

            var go = new GameObject("Measure");
            try
            {
                var text = go.AddComponent<TextMeshPro>();
                text.font = font;
                text.fontSize = MeasureFontPx / PixelsPerUnit * 10f;
                text.alignment = TextAlignmentOptions.BaselineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                float scale = PixelsPerUnit / MeasureFontPx; // world units to pixels, per pixel of font size
                foreach (char c in characters.OrderBy(c => c))
                {
                    if (!font.HasCharacter(c, false, true)) continue;
                    text.text = c.ToString();
                    text.ForceMeshUpdate();
                    if (text.textInfo.characterCount == 0) continue;
                    TMP_CharacterInfo info = text.textInfo.characterInfo[0];
                    vector.characters.Add(new CharacterVector
                    {
                        character = c.ToString(),
                        advance = Round4((info.xAdvance - info.origin) * scale),
                        preferred = Round4(text.GetPreferredValues(text.text).x * scale),
                        visible = info.isVisible,
                        inkLeft = Round4((info.bottomLeft.x - info.origin) * scale),
                        inkRight = Round4((info.topRight.x - info.origin) * scale),
                        inkBottom = Round4((info.bottomLeft.y - info.baseLine) * scale),
                        inkTop = Round4((info.topRight.y - info.baseLine) * scale)
                    });
                }

                // Kerning between the measured characters: what the pair adds to the first one's advance.
                var glyphCharacters = new Dictionary<uint, string>();
                foreach (TMP_Character known in font.characterTable)
                    if (known.unicode <= char.MaxValue && characters.Contains((char)known.unicode))
                        glyphCharacters[known.glyphIndex] = ((char)known.unicode).ToString();
                vector.kerning = new List<KerningVector>();
                foreach (GlyphPairAdjustmentRecord pair in font.fontFeatureTable.glyphPairAdjustmentRecords)
                {
                    if (!glyphCharacters.TryGetValue(pair.firstAdjustmentRecord.glyphIndex, out string first) ||
                        !glyphCharacters.TryGetValue(pair.secondAdjustmentRecord.glyphIndex, out string second)) continue;
                    float advance = pair.firstAdjustmentRecord.glyphValueRecord.xAdvance +
                                    pair.secondAdjustmentRecord.glyphValueRecord.xAdvance;
                    if (advance == 0f) continue;
                    vector.kerning.Add(new KerningVector { pair = first + second, advance = Round4(advance * perPx) });
                }
                vector.kerning.Sort((a, b) => string.CompareOrdinal(a.pair, b.pair));

                // Whole words, to check how the page adds characters up into a preferred width.
                vector.samples = new List<SampleVector>();
                foreach (string sample in new[] { "ct", "solid word", "NK", "a \"quoted\" back\\slash", "gr\u00fc\u00dfe" })
                    vector.samples.Add(new SampleVector
                        { text = sample, preferred = Round4(text.GetPreferredValues(sample).x * scale) });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            return vector;
        }

        static string StripTags(string text) => System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]*>", "");

        static double Round4(float value) => Math.Round(value, 4);

        static TMP_FontAsset Font(string name) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{name} SDF.asset");

        /// <summary>Everything that shows under one built object: text with its font, size and words, and plain shapes.</summary>
        static IEnumerable<PartVector> Parts(Transform built)
        {
            foreach (Renderer renderer in built.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Transform transform = renderer.transform;
                var part = new PartVector
                {
                    name = transform.name,
                    order = renderer.sortingOrder,
                    xPx = Round(PxX(transform.position.x)),
                    yPx = Round(PxY(transform.position.y)),
                    rotation = Round(transform.eulerAngles.z),
                    scaleX = Round(transform.lossyScale.x),
                    scaleY = Round(transform.lossyScale.y)
                };

                var text = renderer.GetComponent<TMP_Text>();
                if (text != null)
                {
                    // Text with nothing laid out leaves textBounds meaningless.
                    if (text.text.Length == 0 || text.textInfo.characterCount == 0 ||
                        text.textBounds.size.x <= 0f || text.textBounds.size.x > 100000f) continue;
                    part.shape = "text";
                    part.text = text.text;
                    part.font = text.font != null ? text.font.name.Replace(" SDF", "") : "";
                    part.sizePx = Round(text.fontSize * PixelsPerUnit / 10f);
                    part.italic = (text.fontStyle & FontStyles.Italic) != 0;
                    part.alignment = text.alignment.ToString();
                    part.characterSpacing = Round(text.characterSpacing);
                    part.color = Hex(text.color);
                    Bounds local = text.textBounds;
                    SetBox(part, transform.TransformPoint(local.min), transform.TransformPoint(local.max));
                }
                else
                {
                    var sprite = renderer as SpriteRenderer;
                    part.shape = sprite != null ? "sprite" : renderer.GetType().Name;
                    part.sprite = sprite != null && sprite.sprite != null ? sprite.sprite.name : "";
                    part.color = sprite != null ? Hex(sprite.color) : "";
                    SetBox(part, renderer.bounds.min, renderer.bounds.max);
                }
                yield return part;
            }
        }

        /// <summary>The box in page pixels, y downwards, whichever corners the rotation swapped.</summary>
        static void SetBox(PartVector part, Vector3 a, Vector3 b)
        {
            part.left = Round(PxX(Mathf.Min(a.x, b.x)));
            part.right = Round(PxX(Mathf.Max(a.x, b.x)));
            part.top = Round(PxY(Mathf.Max(a.y, b.y)));
            part.bottom = Round(PxY(Mathf.Min(a.y, b.y)));
        }

        static float PxX(float world) => world * PixelsPerUnit + 640f;
        static float PxY(float world) => 360f - world * PixelsPerUnit;
        /// <summary>Doubles, so JsonUtility writes 475.36 rather than the float nearest it in full.</summary>
        static double Round(float value) => Math.Round(value, 2);

        static string Hex(Color color) => "#" + (color.a < 1f
            ? ColorUtility.ToHtmlStringRGBA(color)
            : ColorUtility.ToHtmlStringRGB(color));

        // ------------------------------------------------ the file, as JsonUtility writes it

        [Serializable]
        class VectorFile
        {
            public Limits limits;
            public List<CodeVector> codes;
            public List<LayoutVector> layouts;
            public List<FontVector> fonts;
        }

        [Serializable]
        class FontVector
        {
            public string name;
            /// <summary>Lines per pixel of font size, y upwards from the baseline.</summary>
            public double ascentLine;
            public double descentLine;
            public double meanLine;
            public double capLine;
            public double lineHeight;
            public List<CharacterVector> characters;
            public List<SampleVector> samples;
            public List<KerningVector> kerning;
        }

        [Serializable]
        class KerningVector
        {
            public string pair;
            /// <summary>Added to the first character's advance, per pixel of font size.</summary>
            public double advance;
        }

        [Serializable]
        class SampleVector
        {
            public string text;
            /// <summary>TextMesh Pro's preferred width, per pixel of font size.</summary>
            public double preferred;
        }

        [Serializable]
        class CharacterVector
        {
            public string character;
            public double advance;
            /// <summary>TextMesh Pro's preferred width of the character alone.</summary>
            public double preferred;
            public bool visible;
            public double inkLeft;
            public double inkRight;
            public double inkBottom;
            public double inkTop;
        }

        [Serializable]
        class Limits
        {
            public int currentVersion;
            public int maxJsonBytes;
            public int maxNameLength;
            public int maxSentenceLength;
            public int maxWordLength;
            public float minSeconds;
            public float maxSeconds;
        }

        [Serializable]
        class CodeVector
        {
            public string label;
            public string code;
            /// <summary>Whether LevelShareCode.Decode reads it.</summary>
            public bool gameReads;
            public bool finished;
            /// <summary>CustomLevelData.ContentHash of the level as the game reads it.</summary>
            public string contentHash = "";
        }

        [Serializable]
        class LayoutVector
        {
            public string label;
            public string code;
            public double rightPx;
            public List<PieceVector> pieces;
            /// <summary>What no piece made: the ceiling.</summary>
            public List<PartVector> others;
        }

        [Serializable]
        class PieceVector
        {
            public int index;
            public string type;
            public string kind;
            public List<PartVector> parts;
        }

        [Serializable]
        class PartVector
        {
            public string name;
            /// <summary>"text", "sprite", or another renderer's type name.</summary>
            public string shape;
            public string text = "";
            public string font = "";
            public double sizePx;
            public bool italic;
            public string alignment = "";
            public double characterSpacing;
            public string sprite = "";
            public string color;
            public int order;
            /// <summary>The object's own position, rotation and scale.</summary>
            public double xPx;
            public double yPx;
            public double rotation;
            public double scaleX;
            public double scaleY;
            /// <summary>The box it covers, in page pixels: the laid-out text, or the sprite.</summary>
            public double left;
            public double top;
            public double right;
            public double bottom;
        }
    }
}

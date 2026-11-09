using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LowercaseUppercase.Levels;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Writes Web/test/vectors.json for the shared levels page: level codes with what this game makes of them, and
    /// what every piece of a few levels looks like once built. The page's JavaScript is tested against it, so the
    /// game's C# code stays the reference. Rerun it after changing level codes, CustomLevelData or how pieces are built.
    /// From the command line: Unity -batchmode -quit -projectPath . -executeMethod LowercaseUppercase.Editor.SharedLevelVectors.Write
    /// </summary>
    public static class SharedLevelVectors
    {
        const string OutputPath = "Web/test/vectors.json";
        const string SampleLevelPath = "Assets/Levels/SampleCustomLevel.json";
        const float PixelsPerUnit = 72f;

        [MenuItem("lowercase UPPERCASE/Shared Levels/Write Test Vectors")]
        public static void Write()
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

            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                foreach ((string label, CustomLevelData level) in new[]
                         { ("sample", sample), ("every piece", everyPiece), ("awkward text", awkward) })
                    file.layouts.Add(Layout(label, level, scratch));
            }
            finally
            {
                EditorSceneManager.CloseScene(scratch, true);
            }

            File.WriteAllText(OutputPath, JsonUtility.ToJson(file, true) + "\n", new UTF8Encoding(false));
            Debug.Log($"Wrote {file.codes.Count} codes and {file.layouts.Count} layouts to {OutputPath}.");
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
                         piece.type != CustomPieceType.Hill && piece.type != CustomPieceType.Lift)
                    piece.y = 360f;
                level.pieces.Add(piece);

                x += piece.type == CustomPieceType.CapsLock ? 1000f : 360f;
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

        static LayoutVector Layout(string label, CustomLevelData level, Scene scene)
        {
            var root = new GameObject("Vector Level");
            SceneManager.MoveGameObjectToScene(root, scene);
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

        static TMP_FontAsset Font(string name) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{name} SDF.asset");

        /// <summary>Everything that shows under one built object: text with its font, size and words, and plain shapes.</summary>
        static IEnumerable<PartVector> Parts(Transform built)
        {
            foreach (Renderer renderer in built.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled) continue;
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
                    // Nothing laid out (an arrow waiting in its launcher's pool) leaves textBounds meaningless.
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

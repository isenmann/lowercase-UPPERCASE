using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// Writes Web/src/run-limits.json for the online high scores, and Assets/Scripts/RunLimits.Generated.cs with the
    /// same hash and level order for the game. The levels are found by following each level's exit
    /// (<see cref="LevelTransition.nextScene"/>) from the first one until it leads back to the menu. Rerun it after
    /// changing a level's enemies, # or clock; RunLimitsTests fails until then.
    /// Works in Edit mode: each level is opened beside whatever is open and closed again, unless it was already open.
    /// </summary>
    public static class RunLimitsExport
    {
        public const string JsonPath = "Web/src/run-limits.json";
        public const string GeneratedPath = "Assets/Scripts/RunLimits.Generated.cs";
        const string FirstLevel = "Overworld";
        const string MenuScene = "Menu";
        const int MaxLevels = 30;

        [Serializable]
        public class LimitsFile
        {
            public int format;
            public string hash;
            public int pointsPerEnemy;
            public int pointsPerCollectible;
            public int pointsPerSecondLeft;
            public List<RunLimits.Level> levels = new List<RunLimits.Level>();
        }

        [MenuItem("lowercase UPPERCASE/High Scores/Write Run Limits")]
        public static void Write()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Write Run Limits works in Edit mode only: leave Play mode first.");
                return;
            }

            List<RunLimits.Level> levels = CountLevels();
            string hash = RunLimits.HashOf(levels);
            var file = new LimitsFile
            {
                format = RunLimits.FormatVersion,
                hash = hash,
                pointsPerEnemy = LevelRun.PointsPerEnemy,
                pointsPerCollectible = LevelRun.PointsPerCollectible,
                pointsPerSecondLeft = LevelRun.PointsPerSecondLeft,
                levels = levels
            };
            File.WriteAllText(JsonPath, JsonUtility.ToJson(file, true) + "\n");

            string generated = Generated(hash, levels);
            if (!File.Exists(GeneratedPath) || File.ReadAllText(GeneratedPath) != generated)
            {
                File.WriteAllText(GeneratedPath, generated);
                AssetDatabase.ImportAsset(GeneratedPath);
            }

            Debug.Log($"Wrote {JsonPath}: {levels.Count} levels, hash {hash}, at most {levels.Sum(l => l.maxPoints)} points.");
        }

        static List<RunLimits.Level> CountLevels()
        {
            var levels = new List<RunLimits.Level>();
            string scene = FirstLevel;
            while (scene != MenuScene)
            {
                if (levels.Count >= MaxLevels || levels.Any(l => l.scene == scene))
                    throw new InvalidOperationException($"The levels' exits don't lead back to {MenuScene} (stuck at {scene}).");

                string path = $"Assets/Scenes/{scene}.unity";
                Scene opened = SceneManager.GetSceneByPath(path);
                bool wasOpen = opened.isLoaded;
                if (!wasOpen) opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    levels.Add(RunLimits.Count(opened));
                    LevelTransition[] exits = opened.GetRootGameObjects()
                        .SelectMany(r => r.GetComponentsInChildren<LevelTransition>(true)).ToArray();
                    if (exits.Length != 1)
                        throw new InvalidOperationException($"{scene} has {exits.Length} LevelTransition components, not one.");
                    scene = exits[0].nextScene;
                }
                finally
                {
                    if (!wasOpen) EditorSceneManager.CloseScene(opened, true);
                }
            }
            return levels;
        }

        static string Generated(string hash, List<RunLimits.Level> levels)
        {
            var text = new StringBuilder();
            text.Append("// Written by \"lowercase UPPERCASE/High Scores/Write Run Limits\" (RunLimitsExport). Don't edit it by hand.\n");
            text.Append("namespace LowercaseUppercase\n{\n");
            text.Append("    public static partial class RunLimits\n    {\n");
            text.Append("        /// <summary>The hash of Web/src/run-limits.json: the server only takes runs from a build with the same one.</summary>\n");
            text.Append($"        public const string Hash = \"{hash}\";\n\n");
            text.Append("        /// <summary>The levels of a run, in the order they are played.</summary>\n");
            text.Append("        public static readonly string[] LevelScenes =\n        {\n");
            text.Append(string.Join(",\n", levels.Select(l => $"            \"{l.scene}\"")));
            text.Append("\n        };\n    }\n}\n");
            return text.ToString();
        }
    }
}

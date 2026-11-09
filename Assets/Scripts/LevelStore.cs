using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>A level file found in the store: the level, or why it couldn't be read.</summary>
    public sealed class StoredLevel
    {
        public readonly string Path;
        public readonly CustomLevelData Level;
        /// <summary>Why the file couldn't be read, or null.</summary>
        public readonly string Problem;
        public readonly DateTime Saved;

        public StoredLevel(string path, CustomLevelData level, string problem, DateTime saved)
        {
            Path = path;
            Level = level;
            Problem = problem;
            Saved = saved;
        }

        public string Name => Level != null ? Level.name : System.IO.Path.GetFileNameWithoutExtension(Path);
    }

    /// <summary>
    /// The player's own levels, one JSON file each in a "levels" folder under the game's data folder. Files are named
    /// by a random id, so renaming a level never moves its file.
    /// </summary>
    public static class LevelStore
    {
        public const string Extension = ".json";

        /// <summary>Set by tests to keep their levels apart from the player's.</summary>
        public static string FolderOverride { get; set; }

        public static string Folder => FolderOverride ?? Path.Combine(Application.persistentDataPath, "levels");

        /// <summary>Every level in the store, the most recently saved first.</summary>
        public static List<StoredLevel> List()
        {
            var levels = new List<StoredLevel>();
            if (!Directory.Exists(Folder)) return levels;
            foreach (string path in Directory.GetFiles(Folder, "*" + Extension))
            {
                DateTime saved = File.GetLastWriteTimeUtc(path);
                try
                {
                    levels.Add(new StoredLevel(path, Load(path), null, saved));
                }
                catch (Exception e) when (e is FormatException || e is IOException || e is UnauthorizedAccessException)
                {
                    levels.Add(new StoredLevel(path, null, e.Message, saved));
                }
            }
            levels.Sort((a, b) => b.Saved.CompareTo(a.Saved));
            return levels;
        }

        /// <summary>Reads a level file, or throws a FormatException saying what is wrong with it.</summary>
        public static CustomLevelData Load(string path) => CustomLevelData.FromJson(File.ReadAllText(path));

        /// <summary>
        /// Writes a level to its file, or to a new one when path is null, and returns the path. It is written beside
        /// the old file first and then swapped in, so a crash halfway can't leave half a level.
        /// </summary>
        public static string Save(CustomLevelData level, string path = null)
        {
            Directory.CreateDirectory(Folder);
            if (string.IsNullOrEmpty(path)) path = NewPath();
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, level.ToJson());
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
            return path;
        }

        public static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        static string NewPath()
        {
            string path;
            do path = Path.Combine(Folder, Guid.NewGuid().ToString("N").Substring(0, 12) + Extension);
            while (File.Exists(path));
            return path;
        }
    }
}

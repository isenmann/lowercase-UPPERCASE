using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Which levels the Chapters menu lets the player pick: the first always, and each later one once the level before it has
    /// been finished. Kept in PlayerPrefs as the names of the unlocked scenes, so reordering levels doesn't shift what is open.
    /// </summary>
    public static class LevelProgress
    {
        public const string DefaultStorageKey = "lowercaseUppercase.unlockedLevels";
        const char Separator = '|';

        /// <summary>Tests point this elsewhere, so they never touch the player's own progress.</summary>
        public static string StorageKey = DefaultStorageKey;

        static HashSet<string> Load()
        {
            string saved = PlayerPrefs.GetString(StorageKey, "");
            return new HashSet<string>(saved.Split(new[] { Separator }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        public static bool IsUnlocked(string scene) => Load().Contains(scene);

        /// <summary>Called when a level is finished, with the scene it leads to.</summary>
        public static void Unlock(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return;
            HashSet<string> unlocked = Load();
            if (!unlocked.Add(scene)) return;
            PlayerPrefs.SetString(StorageKey, string.Join(Separator.ToString(), unlocked));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
        }
    }
}

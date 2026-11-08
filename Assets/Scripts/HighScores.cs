using System;
using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The ten best runs with the names typed in for them, kept in PlayerPrefs. A new score goes in below equal ones, so
    /// whoever reached a score first keeps the higher place. It is saved the moment it qualifies, under a placeholder
    /// name, so quitting during the name entry doesn't lose it.
    /// </summary>
    public static class HighScores
    {
        public const int Size = 10;
        public const int MaxNameLength = 12;
        public const string Unnamed = "???";
        public const string DefaultStorageKey = "lowercaseUppercase.highScores";

        [Serializable]
        public class Entry
        {
            public string name;
            public int score;
        }

        [Serializable]
        class Table
        {
            public List<Entry> entries = new List<Entry>();
        }

        /// <summary>Tests point this elsewhere, so they never touch the player's own table.</summary>
        public static string StorageKey = DefaultStorageKey;

        public static List<Entry> Load()
        {
            string json = PlayerPrefs.GetString(StorageKey, "");
            if (string.IsNullOrEmpty(json)) return new List<Entry>();
            try
            {
                Table table = JsonUtility.FromJson<Table>(json);
                return table?.entries ?? new List<Entry>();
            }
            catch (ArgumentException)
            {
                return new List<Entry>();
            }
        }

        public static bool Qualifies(int score)
        {
            if (score <= 0) return false;
            List<Entry> entries = Load();
            return entries.Count < Size || score > entries[Size - 1].score;
        }

        /// <summary>Adds the score under the placeholder name and returns its place, or -1 if it isn't good enough.</summary>
        public static int Insert(int score)
        {
            if (!Qualifies(score)) return -1;
            List<Entry> entries = Load();
            int place = 0;
            while (place < entries.Count && entries[place].score >= score) place++;
            entries.Insert(place, new Entry { name = Unnamed, score = score });
            if (entries.Count > Size) entries.RemoveRange(Size, entries.Count - Size);
            Save(entries);
            return place;
        }

        public static void Rename(int place, string name)
        {
            List<Entry> entries = Load();
            if (place < 0 || place >= entries.Count) return;
            entries[place].name = string.IsNullOrWhiteSpace(name) ? Unnamed : name.Trim();
            Save(entries);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
        }

        /// <summary>Letters, digits, spaces and a little punctuation; nothing TextMeshPro would read as a tag.</summary>
        public static bool IsAllowedInName(char c) => char.IsLetterOrDigit(c) || c == ' ' || "-_.!?".IndexOf(c) >= 0;

        static void Save(List<Entry> entries)
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(new Table { entries = entries }));
            PlayerPrefs.Save();
        }
    }
}

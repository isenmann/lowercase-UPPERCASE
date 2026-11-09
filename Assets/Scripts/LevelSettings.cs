using System;
using System.Globalization;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// One of the level's own settings in the editor's panel. A text setting is typed into a copy and only
    /// <see cref="Apply"/>-ed when the typing is placed, because a half-typed sentence isn't a sentence yet.
    /// </summary>
    public sealed class LevelSetting
    {
        public readonly string Label;
        public readonly Func<CustomLevelData, string> Show;
        public readonly Action<CustomLevelData, int> Step;
        /// <summary>Sets a typed text, or returns what is wrong with it and leaves the level as it was.</summary>
        public readonly Func<CustomLevelData, string, string> Apply;
        public readonly int MaxLength;
        public bool IsText => Step == null;

        public LevelSetting(string label, Func<CustomLevelData, string> show, Action<CustomLevelData, int> step)
        {
            Label = label;
            Show = show;
            Step = step;
        }

        public LevelSetting(string label, Func<CustomLevelData, string> show, Func<CustomLevelData, string, string> apply,
            int maxLength)
        {
            Label = label;
            Show = show;
            Apply = apply;
            MaxLength = maxLength;
        }
    }

    /// <summary>The level's settings: its name, its sentence and missing letter, the time limit and the ceiling.</summary>
    public static class LevelSettings
    {
        public const char Blank = '_';
        public const float SecondsStep = 5f;
        public const float MinSeconds = 10f;
        public const float MaxSeconds = 600f;
        public const int MaxNameLength = 24;
        public const int MaxSentenceLength = 40;

        public static readonly LevelSetting[] All =
        {
            new LevelSetting("name", l => l.name, SetName, MaxNameLength),
            new LevelSetting("sentence", Sentence, SetSentence, MaxSentenceLength),
            new LevelSetting("letter", l => l.missingLetter == "A" ? "A" : "a",
                (l, direction) => l.missingLetter = l.missingLetter == "A" ? "a" : "A"),
            new LevelSetting("time", l => l.seconds.ToString("0", CultureInfo.InvariantCulture) + " s",
                (l, direction) => l.seconds = Mathf.Clamp(l.seconds + direction * SecondsStep, MinSeconds, MaxSeconds)),
            new LevelSetting("ceiling", l => l.ceiling ? "on" : "off", (l, direction) => l.ceiling = !l.ceiling),
        };

        /// <summary>The sentence as it is typed: the words around the letter, with _ where the letter goes.</summary>
        public static string Sentence(CustomLevelData level) => level.sentenceBefore + Blank + level.sentenceAfter;

        static string SetName(CustomLevelData level, string text)
        {
            string name = text.Trim();
            if (name.Length == 0) return "a level needs a name";
            level.name = name;
            return null;
        }

        static string SetSentence(CustomLevelData level, string text)
        {
            int blank = text.IndexOf(Blank);
            if (blank < 0 || text.IndexOf(Blank, blank + 1) >= 0) return "the sentence needs one _ for the letter";
            level.sentenceBefore = text.Substring(0, blank);
            level.sentenceAfter = text.Substring(blank + 1);
            return null;
        }
    }
}

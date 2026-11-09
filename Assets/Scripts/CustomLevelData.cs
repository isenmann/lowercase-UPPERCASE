using System;
using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A level made in the level editor, as it is saved and shared: the sentence, the rules, where the letter starts,
    /// and a flat list of pieces. Positions are in the builder's pixels: a 1280 x 720 page, y growing downwards, the
    /// ground surface at 520. <see cref="Levels.PrototypeBuilder.BuildCustomLevel"/> turns it into a level.
    /// </summary>
    [Serializable]
    public class CustomLevelData
    {
        public const int CurrentVersion = 1;
        public const float DefaultSeconds = 60f;
        public const float GroundPx = 520f;

        public int version = CurrentVersion;
        public string name = "untitled";

        [Header("Sentence")]
        public string sentenceBefore = "the c";
        public string missingLetter = "a";
        public string sentenceAfter = "t";

        [Header("Rules")]
        public float seconds = DefaultSeconds;
        [Tooltip("A ceiling word along the whole level, as in the cellar levels.")]
        public bool ceiling;

        [Header("Start: the letter's feet, in pixels")]
        public float startX = 110f;
        public float startY = GroundPx;

        public List<CustomPiece> pieces = new List<CustomPiece>();

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        /// <summary>Reads a level, or throws a FormatException saying what is wrong with it.</summary>
        public static CustomLevelData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("The level is empty.");

            CustomLevelData data;
            try
            {
                data = JsonUtility.FromJson<CustomLevelData>(json);
            }
            catch (ArgumentException e)
            {
                throw new FormatException($"The level isn't valid JSON: {e.Message}");
            }

            if (data == null) throw new FormatException("The level is empty.");
            if (data.version < 1 || data.version > CurrentVersion)
                throw new FormatException($"The level has format version {data.version}; this game reads up to {CurrentVersion}.");
            if (data.pieces == null) data.pieces = new List<CustomPiece>();
            if (data.seconds <= 0f) data.seconds = DefaultSeconds;
            return data;
        }
    }

    /// <summary>
    /// One piece of a custom level. A flat record, so it stays readable as JSON; which fields count depends on
    /// <see cref="type"/>, listed in <see cref="CustomPieceType"/>.
    /// </summary>
    [Serializable]
    public class CustomPiece
    {
        public string type;
        public float x;
        public float y;
        public string text = "";
        public string kind = "";

        public CustomPiece() { }

        public CustomPiece(string type, float x, float y, string text = "", string kind = "")
        {
            this.type = type;
            this.x = x;
            this.y = y;
            this.text = text;
            this.kind = kind;
        }
    }

    /// <summary>The piece types and what their fields mean. x is always the left edge unless said otherwise.</summary>
    public static class CustomPieceType
    {
        /// <summary>A word to stand on. y = its surface. text = the word. kind = one of the word kinds below.</summary>
        public const string Word = "word";
        /// <summary>A column of brick letters that A smashes. y = its bottom. text = the letters, top first.</summary>
        public const string Brick = "brick";
        /// <summary>A climbable sticky column. y = its bottom. text = the letters, top first.</summary>
        public const string StickyWall = "stickyWall";
        /// <summary>A semicolon checkpoint. y = the surface it stands on.</summary>
        public const string Checkpoint = "checkpoint";
        /// <summary>A power block that arms the letter with asterisks. y = its top.</summary>
        public const string Power = "power";
        /// <summary>A # to collect. x, y = its centre.</summary>
        public const string Hash = "hash";
        /// <summary>
        /// The word with the blank the letter slots into. y = its baseline, which is the surface of the word under it.
        /// text = the word with an underscore where the blank is ("c_t"). kind = "a" or "A", the form it wants.
        /// </summary>
        public const string Exit = "exit";

        public const string Solid = "solid";
        public const string Ice = "ice";
        public const string Hot = "hot";
        public const string Sticky = "sticky";
        /// <summary>A hyphen bridge: a crosses it, A snaps it.</summary>
        public const string Plank = "plank";
    }
}

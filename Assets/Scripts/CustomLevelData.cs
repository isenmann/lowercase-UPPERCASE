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

        [Tooltip("The ContentHash of the level when its maker last reached the exit in a test play from the start.")]
        public string finishedHash = "";

        public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

        /// <summary>
        /// Whether its maker reached the exit in a test play from the start, and nothing that matters for play has
        /// changed since. Renaming keeps it; moving a piece doesn't.
        /// </summary>
        public bool FinishedByMaker => !string.IsNullOrEmpty(finishedHash) && finishedHash == ContentHash();

        public void MarkFinished() => finishedHash = ContentHash();

        /// <summary>A short fingerprint of everything that matters for play: all but the name and the flag itself.</summary>
        public string ContentHash()
        {
            string keptName = name, keptHash = finishedHash;
            name = "";
            finishedHash = "";
            string json = ToJson(false);
            name = keptName;
            finishedHash = keptHash;

            // FNV-1a, 32 bits: plenty to tell an edited level from the one that was finished.
            uint hash = 2166136261;
            foreach (char c in json)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }

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
            if (data.finishedHash == null) data.finishedHash = "";
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
        [Tooltip("A second position: a lift's top, a Caps Lock zone's or cursor's right end. 0 = the piece's default.")]
        public float x2;
        public float y2;
        [Tooltip("How many: hooks in a chain, rows of a tube, hill or launcher, dots on a fire bar. 0 = the default.")]
        public int count;
        [Tooltip("The piece's one tuning number: spring pad width, flier travel, hopper side speed, launcher interval, fire bar turn speed, cursor speed. 0 = the default.")]
        public float value;
        [Tooltip("Seconds before a hopper or spark starts.")]
        public float delay;
        [Tooltip("Facing the other way: walkers and fliers start left, launchers fire left.")]
        public bool flip;

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

    /// <summary>
    /// The piece types and what their fields mean. x is the left edge unless said otherwise. Fields left at 0 take the
    /// piece's default.
    /// </summary>
    public static class CustomPieceType
    {
        // ------------------------------------------------ terrain

        /// <summary>A word to stand on. y = its surface. text = the word. kind = one of the word kinds below.</summary>
        public const string Word = "word";
        /// <summary>A column of brick letters that A smashes. y = its bottom. text = the letters, top first.</summary>
        public const string Brick = "brick";
        /// <summary>A climbable sticky column. y = its bottom. text = the letters, top first.</summary>
        public const string StickyWall = "stickyWall";
        /// <summary>A wall of a word stacked on itself, like the Overworld tubes. y = its bottom. text = the word ("tube"), count = rows (3).</summary>
        public const string Tube = "tube";
        /// <summary>
        /// Letters spaced so a drops through the holes and A walks across. y = their surface. text = the letters;
        /// a ¡ between two letters hides a riser enemy in that hole ("b¡etwe¡en").
        /// </summary>
        public const string Kerned = "kerned";
        /// <summary>A spring "!". x = its centre, y = its surface, value = pad width in pixels (80).</summary>
        public const string Spring = "spring";
        /// <summary>A door column that a plate opens. y = its bottom.</summary>
        public const string Door = "door";
        /// <summary>A period plate that A holds down; it opens the nearest door. x = its centre, y = its surface.</summary>
        public const string Plate = "plate";
        /// <summary>A rising "up" column. x = the left of the gap, y = the surface it is entered from, y2 = how high it lifts to (y - 300).</summary>
        public const string Lift = "lift";
        /// <summary>Comma hooks to swing from. x, y = the first hook's centre, count = hooks (1), 240 px apart.</summary>
        public const string Hooks = "hooks";
        /// <summary>A Caps Lock stretch from x to x2. Its key is a separate piece.</summary>
        public const string CapsLock = "capsLock";
        /// <summary>The key that switches off the nearest Caps Lock stretch. y = its baseline.</summary>
        public const string CapsKey = "capsKey";
        /// <summary>Scenery: a hill of count rows (3) standing on the ground.</summary>
        public const string Hill = "hill";
        /// <summary>Scenery: a cloud. y = its baseline.</summary>
        public const string Cloud = "cloud";

        // ------------------------------------------------ things

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
        /// <summary>
        /// An enemy; kind = one of the enemy kinds below. x = where it stands, y = its feet. Enemies that walk, hop or
        /// leap drop onto the word below that point.
        /// </summary>
        public const string Enemy = "enemy";

        // ------------------------------------------------ word kinds

        public const string Solid = "solid";
        public const string Ice = "ice";
        public const string Hot = "hot";
        public const string Sticky = "sticky";
        /// <summary>A hyphen bridge: a crosses it, A snaps it.</summary>
        public const string Plank = "plank";

        // ------------------------------------------------ enemy kinds

        /// <summary>&amp; walks; stomp or shoot it.</summary>
        public const string Walker = "walker";
        /// <summary>^ walks and burns a stomping A; shoot it.</summary>
        public const string Spike = "spike";
        /// <summary>§ walks; asterisks bounce off, stomp it.</summary>
        public const string Armour = "armour";
        /// <summary>! hops; value = sideways speed (0 hops in place), delay = start delay.</summary>
        public const string Hopper = "hopper";
        /// <summary>~ flies; value = travel either side in pixels (0 bobs up and down in place).</summary>
        public const string Flier = "flier";
        /// <summary>» a launcher column standing at y that fires arrows; count = rows (2), value = seconds between arrows (2), flip = fires left.</summary>
        public const string Launcher = "launcher";
        /// <summary>' hangs under the ceiling or word above it and falls as the letter passes below.</summary>
        public const string Drip = "drip";
        /// <summary>° leaps out of the hot word below it; delay = start delay.</summary>
        public const string Spark = "spark";
        /// <summary>A hot o standing at y with count dots (4) turning around it at value degrees a second (-90).</summary>
        public const string FireBar = "fireBar";
        /// <summary>ö drifts towards the letter once it comes near.</summary>
        public const string Eye = "eye";
        /// <summary>@ walks, curls under a stomp, rolls when kicked.</summary>
        public const string Curl = "curl";
        /// <summary>↕ walks and swaps Shift around when touched.</summary>
        public const string Swapper = "swapper";
        /// <summary>b walks and copies the letter's case.</summary>
        public const string Mimic = "mimic";
        /// <summary>| a cursor standing at y that chases from x to x2 erasing words; value = speed in units a second (3.5).</summary>
        public const string Cursor = "cursor";
    }
}

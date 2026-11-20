using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LowercaseUppercase
{
    /// <summary>
    /// What a run through the 15 levels can earn, level by level: the online high scores check every level report
    /// against it. The editor writes it to Web/src/run-limits.json ("lowercase UPPERCASE/High Scores/Write Run
    /// Limits"), together with <see cref="Hash"/> and <see cref="LevelScenes"/> in RunLimits.Generated.cs, so the game
    /// and the server agree on which levels they are talking about. Change a level's enemies, # or clock and the hash
    /// changes; a build with an old hash gets no online runs.
    /// </summary>
    public static partial class RunLimits
    {
        /// <summary>Bumped when the meaning of the numbers changes, so an old server never accepts a new kind of run.</summary>
        public const int FormatVersion = 1;

        [Serializable]
        public class Level
        {
            public string scene;
            public int seconds;
            public int enemies;
            public int collectibles;
            public int maxPoints;
        }

        /// <summary>
        /// Counts a loaded level the way the game pays for it: enemies that pay points (not launcher arrows) and every
        /// #, inactive ones included as <see cref="PrototypeFlow"/> counts them, and the level's clock.
        /// </summary>
        public static Level Count(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            PrototypeFlow[] flows = roots.SelectMany(r => r.GetComponentsInChildren<PrototypeFlow>(true)).ToArray();
            if (flows.Length != 1)
                throw new InvalidOperationException($"{scene.name} has {flows.Length} PrototypeFlow components, not one.");

            float clock = flows[0].levelSeconds;
            int seconds = Mathf.RoundToInt(clock);
            if (!Mathf.Approximately(clock, seconds))
                throw new InvalidOperationException($"{scene.name}'s clock of {clock} s isn't whole seconds.");

            var level = new Level
            {
                scene = scene.name,
                seconds = seconds,
                enemies = roots.SelectMany(r => r.GetComponentsInChildren<Enemy>(true)).Count(e => e.paysPoints),
                collectibles = roots.Sum(r => r.GetComponentsInChildren<Collectible>(true).Length)
            };
            level.maxPoints = MaxPoints(level);
            return level;
        }

        /// <summary>Every enemy, every # and the whole clock left over: more than anyone can score, never less.</summary>
        public static int MaxPoints(Level level) =>
            level.enemies * LevelRun.PointsPerEnemy
            + level.collectibles * LevelRun.PointsPerCollectible
            + level.seconds * LevelRun.PointsPerSecondLeft;

        /// <summary>
        /// The text the hash is taken of. Web/src/runlimits.js builds the same text, so keep the two in step:
        /// "v1", the points per enemy / # / second, then one line per level: scene seconds enemies collectibles.
        /// </summary>
        public static string Canonical(IEnumerable<Level> levels)
        {
            var lines = new List<string>
            {
                "v" + FormatVersion,
                string.Join(" ", LevelRun.PointsPerEnemy, LevelRun.PointsPerCollectible, LevelRun.PointsPerSecondLeft)
            };
            lines.AddRange(levels.Select(l => string.Join(" ",
                l.scene,
                l.seconds.ToString(CultureInfo.InvariantCulture),
                l.enemies.ToString(CultureInfo.InvariantCulture),
                l.collectibles.ToString(CultureInfo.InvariantCulture))));
            return string.Join("\n", lines);
        }

        /// <summary>The first 16 hex digits of the SHA-256 of <see cref="Canonical"/>.</summary>
        public static string HashOf(IEnumerable<Level> levels)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical(levels)));
                var hex = new StringBuilder();
                for (int i = 0; i < 8; i++) hex.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }
    }
}

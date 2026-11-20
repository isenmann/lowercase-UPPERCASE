using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// The online high scores check every level against Web/src/run-limits.json. These fail when a level's enemies, #
    /// or clock changed since the file was written: run "lowercase UPPERCASE/High Scores/Write Run Limits" again.
    /// </summary>
    public class RunLimitsTests
    {
        static readonly string[] PlayOrder =
        {
            "Overworld", "Crack", "Wall", "Plank", "Gate", "Cellar", "Space", "Skate",
            "Trail", "Chain", "Ladder", "Skyline", "Flames", "Tar", "Final"
        };

        [Serializable]
        class LimitsFile
        {
            public int format;
            public string hash;
            public int pointsPerEnemy;
            public int pointsPerCollectible;
            public int pointsPerSecondLeft;
            public List<RunLimits.Level> levels = new List<RunLimits.Level>();
        }

        static LimitsFile ReadFile()
        {
            string path = Path.Combine(Application.dataPath, "..", "Web", "src", "run-limits.json");
            Assert.IsTrue(File.Exists(path), "Web/src/run-limits.json is missing: run Write Run Limits.");
            return JsonUtility.FromJson<LimitsFile>(File.ReadAllText(path));
        }

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        [Test]
        public void TheRunIsTheFifteenLevelsInPlayOrder()
        {
            CollectionAssert.AreEqual(PlayOrder, RunLimits.LevelScenes);
            CollectionAssert.AreEqual(PlayOrder, ReadFile().levels.Select(l => l.scene).ToArray());
            CollectionAssert.AreEqual(ReadFile().levels.Select(l => l.seconds).ToArray(), RunLimits.LevelSeconds);
        }

        [Test]
        public void TheFileAndTheGameShareTheHashAndThePoints()
        {
            LimitsFile file = ReadFile();
            Assert.AreEqual(RunLimits.FormatVersion, file.format);
            Assert.AreEqual(LevelRun.PointsPerEnemy, file.pointsPerEnemy);
            Assert.AreEqual(LevelRun.PointsPerCollectible, file.pointsPerCollectible);
            Assert.AreEqual(LevelRun.PointsPerSecondLeft, file.pointsPerSecondLeft);
            Assert.AreEqual(RunLimits.HashOf(file.levels), file.hash, "The file's hash isn't the hash of its levels.");
            Assert.AreEqual(file.hash, RunLimits.Hash, "RunLimits.Generated.cs and run-limits.json disagree.");
            foreach (RunLimits.Level level in file.levels)
                Assert.AreEqual(RunLimits.MaxPoints(level), level.maxPoints, level.scene);
        }

        [Test]
        public void TheHashChangesWithAnyCount()
        {
            var levels = new List<RunLimits.Level>
            {
                new RunLimits.Level { scene = "Overworld", seconds = 60, enemies = 3, collectibles = 10 }
            };
            string before = RunLimits.HashOf(levels);
            levels[0].enemies = 4;
            Assert.AreNotEqual(before, RunLimits.HashOf(levels));
            Assert.AreEqual(16, before.Length);
        }

        /// <summary>Counted as the game loads them, so whatever wakes up or is built on load is counted too.</summary>
        [UnityTest]
        public IEnumerator TheLevelsStillHaveTheLimitsInTheFile()
        {
            LimitsFile file = ReadFile();
            var counted = new List<RunLimits.Level>();
            foreach (string scene in PlayOrder)
            {
                LevelTransition.SkipNextIntro = true;
                yield return SceneManager.LoadSceneAsync(scene);
                counted.Add(RunLimits.Count(SceneManager.GetActiveScene()));
            }

            for (int i = 0; i < PlayOrder.Length; i++)
            {
                RunLimits.Level want = file.levels[i], got = counted[i];
                string what = $"{got.scene} changed since run-limits.json was written: run Write Run Limits";
                Assert.AreEqual(want.seconds, got.seconds, $"{what} (seconds)");
                Assert.AreEqual(want.enemies, got.enemies, $"{what} (enemies)");
                Assert.AreEqual(want.collectibles, got.collectibles, $"{what} (#)");
            }
            Assert.AreEqual(RunLimits.Hash, RunLimits.HashOf(counted));
        }
    }
}

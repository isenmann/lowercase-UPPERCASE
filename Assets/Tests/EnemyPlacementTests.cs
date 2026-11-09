using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// Each enemy is introduced in one level with a short note that starts with its glyph,
    /// and it comes back in at least one later level.
    /// </summary>
    public class EnemyPlacementTests
    {
        static readonly string[] PlayOrder =
        {
            "Overworld", "Crack", "Wall", "Plank", "Gate", "Cellar", "Space", "Skate", "Trail", "Chain", "Ladder",
            "Skyline", "Flames", "Tar", "Final"
        };

        // The level that introduces each glyph. The cursor is the finale, so it has no later level.
        static readonly (string glyph, string level)[] Introductions =
        {
            ("§", "Crack"), ("^", "Wall"), ("~", "Plank"), ("<", "Gate"), ("'", "Cellar"), ("¡", "Space"),
            ("@", "Skate"), ("!", "Trail"), ("ö", "Ladder"), ("↕", "Skyline"), ("°", "Flames"),
            ("·", "Flames"), ("b", "Tar"), ("|", "Final"),
        };

        static IEnumerable<string> IntroducedGlyphs() => Introductions.Select(i => i.glyph);

        static IEnumerator Load(string level)
        {
            LevelTransition.SkipNextIntro = true;
            yield return SceneManager.LoadSceneAsync(level);
            yield return null;
        }

        /// <summary>Every enemy glyph in the loaded level, arrow templates included.</summary>
        static HashSet<string> GlyphsInLevel()
        {
            var glyphs = new HashSet<string>();
            foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include))
                if (enemy.glyph != null) glyphs.Add(enemy.glyph.text);
            return glyphs;
        }

        [UnityTest]
        public IEnumerator EachEnemyIsIntroducedWithANote([ValueSource(nameof(IntroducedGlyphs))] string glyph)
        {
            string level = Introductions.First(i => i.glyph == glyph).level;
            yield return Load(level);

            Assert.IsTrue(GlyphsInLevel().Contains(glyph), $"{level} should have the {glyph} enemy.");
            bool hasNote = Object.FindObjectsByType<TMP_Text>()
                .Any(t => t.name.StartsWith("Note ") && t.text.StartsWith(glyph + " "));
            Assert.IsTrue(hasNote, $"{level} should have a note starting with {glyph}.");

            // And no earlier level has it yet.
            foreach (string earlier in PlayOrder.TakeWhile(l => l != level))
            {
                yield return Load(earlier);
                Assert.IsFalse(GlyphsInLevel().Contains(glyph), $"{glyph} should first appear in {level}, not {earlier}.");
            }
        }

        [UnityTest]
        public IEnumerator EachEnemyComesBackInALaterLevel([ValueSource(nameof(IntroducedGlyphs))] string glyph)
        {
            string level = Introductions.First(i => i.glyph == glyph).level;
            if (glyph == "|") Assert.Ignore("The backspace cursor is the finale of the last level.");

            foreach (string later in PlayOrder.SkipWhile(l => l != level).Skip(1))
            {
                yield return Load(later);
                if (GlyphsInLevel().Contains(glyph)) yield break;
            }
            Assert.Fail($"{glyph} should come back after {level}.");
        }
    }
}

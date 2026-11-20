using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LowercaseUppercase.Tests
{
    /// <summary>
    /// Meets the newer enemies in the Enemy Room. The room is not in the build settings,
    /// so it is loaded by path, which only works in the editor.
    /// </summary>
    public class EnemyRoomTests
    {
        const string EnemyRoom = "Assets/Scenes/EnemyRoom.unity";
        const float Timeout = 8f;

        LetterController letter;

        [SetUp]
        public void StartANewGame() => LevelRun.NewGame();

        IEnumerator Load()
        {
#if UNITY_EDITOR
            LevelTransition.SkipNextIntro = true;
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                EnemyRoom, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            letter = FindLetter();
            yield return WaitUntil(() => letter.IsGrounded);
#else
            Assert.Ignore("The Enemy Room can only be loaded in the editor.");
            yield break;
#endif
        }

        static LetterController FindLetter()
        {
            var letter = GameObject.Find("Letter").GetComponent<LetterController>();
            letter.GetComponent<LetterInput>().enabled = false;
            letter.SetInput(0f, false, false);
            return letter;
        }

        static IEnumerator WaitUntil(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - start > Timeout) Assert.Fail("Timed out waiting for a condition.");
                yield return new WaitForFixedUpdate();
            }
        }

        static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>();

        static EnemyLauncher Launcher() => UnityEngine.Object.FindAnyObjectByType<EnemyLauncher>();

        void StandBeforeTheLauncher(float distance)
        {
            Bounds launcher = Launcher().GetComponent<BoxCollider2D>().bounds;
            var feet = new Vector2(launcher.min.x - distance, launcher.min.y + 0.05f);
            letter.Respawn(feet);
            letter.transform.position = feet;
        }

        [UnityTest]
        public IEnumerator AHopperLeavesItsWordAndLandsBackOnIt()
        {
            yield return Load();
            EnemyHop hopper = All<EnemyHop>().First(h => h.sideSpeed == 0f);
            float groundY = hopper.transform.position.y;

            yield return WaitUntil(() => hopper.transform.position.y > groundY + 0.5f);
            yield return WaitUntil(() => !hopper.IsAirborne);
            Assert.AreEqual(groundY, hopper.transform.position.y, 0.01f, "It should land back on its word.");
        }

        [UnityTest]
        public IEnumerator AMovingHopperDriftsAlongItsWord()
        {
            yield return Load();
            EnemyHop hopper = All<EnemyHop>().First(h => h.sideSpeed > 0f);
            float startX = hopper.transform.position.x;
            yield return WaitUntil(() => Mathf.Abs(hopper.transform.position.x - startX) > 0.5f);
        }

        [UnityTest]
        public IEnumerator AFlierTravelsAndBobs()
        {
            yield return Load();
            EnemyWave flier = All<EnemyWave>().First(w => w.travel > 0f);
            Vector2 start = flier.transform.position;
            float lowest = start.y, highest = start.y, furthest = 0f;
            yield return WaitUntil(() =>
            {
                Vector2 now = flier.transform.position;
                lowest = Mathf.Min(lowest, now.y);
                highest = Mathf.Max(highest, now.y);
                furthest = Mathf.Max(furthest, Mathf.Abs(now.x - start.x));
                return furthest > 1f && highest - lowest > flier.amplitude;
            });
            Assert.That(furthest, Is.LessThanOrEqualTo(flier.travel + 0.1f), "It should turn at the end of its travel.");
        }

        [UnityTest]
        public IEnumerator TheLauncherIsQuietWhileOffScreen()
        {
            yield return Load();
            float start = Time.time;
            yield return WaitUntil(() => Time.time - start > 3f);
            Assert.IsEmpty(All<EnemyArrow>(), "No arrows should be fired while the launcher is out of sight.");
        }

        [UnityTest]
        public IEnumerator ArrowsFlyOverLowercase()
        {
            yield return Load();
            StandBeforeTheLauncher(3f);
            LetterController standing = letter;
            float letterX = standing.transform.position.x;

            yield return WaitUntil(() => All<EnemyArrow>().Any(a => a.transform.position.x < letterX - 1f));
            Assert.IsTrue(standing != null, "An arrow should pass over a lowercase letter.");
        }

        [UnityTest]
        public IEnumerator ArrowsHitUppercase()
        {
            yield return Load();
            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            StandBeforeTheLauncher(3f);

            LetterController standing = letter;
            yield return WaitUntil(() => standing == null);
        }

        static T Leftmost<T>() where T : Component =>
            All<T>().OrderBy(c => c.transform.position.x).First();

        void PlaceLetterAt(Vector2 feet)
        {
            letter.Respawn(feet);
            letter.transform.position = feet;
        }

        [UnityTest]
        public IEnumerator ARiserComesUpAndGoesBackDown()
        {
            yield return Load();
            Enemy riser = Leftmost<EnemyRise>().GetComponent<Enemy>();
            Assert.IsFalse(riser.Box.enabled, "It should start hidden.");
            yield return WaitUntil(() => riser.Box.enabled);
            yield return WaitUntil(() => !riser.Box.enabled);
        }

        [UnityTest]
        public IEnumerator ARiserStaysDownWhileTheLetterStandsBesideItsHole()
        {
            yield return Load();
            EnemyRise riser = Leftmost<EnemyRise>();
            float holeX = riser.transform.position.x;
            WordBlock beside = UnityEngine.Object.FindObjectsByType<WordBlock>()
                .Where(w => w.transform.parent != null && w.transform.parent.name.StartsWith("Kerning"))
                .OrderBy(w => Mathf.Abs(w.GetComponent<BoxCollider2D>().bounds.center.x - holeX))
                .First();
            Bounds top = beside.GetComponent<BoxCollider2D>().bounds;
            PlaceLetterAt(new Vector2(top.center.x, top.max.y + 0.01f));

            float start = Time.time;
            yield return WaitUntil(() => Time.time - start > riser.downSeconds + riser.startDelay + 1f);
            Assert.IsFalse(riser.GetComponent<Enemy>().Box.enabled, "It shouldn't rise next to the letter.");
        }

        [UnityTest]
        public IEnumerator ADripFallsWhenTheLetterPassesUnderAndGrowsBack()
        {
            yield return Load();
            EnemyDrip drip = Leftmost<EnemyDrip>();
            Enemy body = drip.GetComponent<Enemy>();
            float hangY = drip.transform.position.y;
            // Beside it rather than under it, so the drip lands on the floor instead of the letter.
            PlaceLetterAt(new Vector2(drip.transform.position.x + drip.triggerHalfWidth * 0.8f, letter.transform.position.y));

            yield return WaitUntil(() => drip.transform.position.y < hangY - 0.5f);
            yield return WaitUntil(() => !body.Box.enabled);
            Assert.IsTrue(letter != null, "A drip that misses should splash on the floor.");
            yield return WaitUntil(() => body.Box.enabled);
            Assert.AreEqual(hangY, drip.transform.position.y, 0.01f, "It should grow back on its ceiling.");
        }

        [UnityTest]
        public IEnumerator SparksLeapAndCannotBeShot()
        {
            yield return Load();
            EnemySpark spark = Leftmost<EnemySpark>();
            Enemy body = spark.GetComponent<Enemy>();
            Assert.IsFalse(body.stompable || body.shootable, "Fire can't be stomped or shot.");
            float restY = spark.transform.position.y;
            yield return WaitUntil(() => spark.IsLeaping && spark.transform.position.y > restY + 1f);
            yield return WaitUntil(() => !spark.IsLeaping);
            Assert.IsFalse(body.Box.enabled, "It should hide in its hot word between leaps.");
        }

        [UnityTest]
        public IEnumerator TheFireBarTurns()
        {
            yield return Load();
            EnemyFireBar bar = UnityEngine.Object.FindAnyObjectByType<EnemyFireBar>();
            Vector2 tip = bar.dots[bar.dots.Length - 1].transform.position;
            yield return WaitUntil(() => Vector2.Distance(bar.dots[bar.dots.Length - 1].transform.position, tip) > 1f);
        }

        // ---------------------------------------------------------------- slam

        static Bounds SlamFloor() => GameObject.Find("Word slam down on whatever walks below").GetComponent<BoxCollider2D>().bounds;

        static WordBlock SlamLid()
        {
            Bounds floor = SlamFloor();
            return All<WordBlock>().Single(b => b.kind == WordKind.Cracked
                && b.transform.position.x > floor.min.x && b.transform.position.x < floor.max.x);
        }

        static Enemy SlamPenEnemy(string glyph)
        {
            Bounds floor = SlamFloor();
            Enemy found = All<Enemy>().Single(e => e.glyph.text == glyph
                && e.transform.position.x > floor.min.x && e.transform.position.x < floor.max.x);
            found.GetComponent<EnemyWalk>().speed = 0f;
            return found;
        }

        [UnityTest]
        public IEnumerator TurningUppercaseInTheAirSlamsThroughTheLid()
        {
            yield return Load();
            Bounds lid = SlamLid().GetComponent<BoxCollider2D>().bounds;
            Collider2D lidCollider = SlamLid().GetComponent<Collider2D>();
            foreach (Enemy e in new[] { SlamPenEnemy("^"), SlamPenEnemy("\u00a7") })
                e.Body.position = new Vector2(SlamFloor().max.x - 1f, e.Body.position.y);

            bool started = false, landed = false;
            letter.SlamStarted += () => started = true;
            letter.Slammed += _ => landed = true;
            PlaceLetterAt(new Vector2(lid.center.x, lid.max.y + 2f));
            letter.SetInput(0f, false, true);

            yield return WaitUntil(() => landed);
            Assert.IsTrue(started, "Turning A in the air should start a slam.");
            Assert.IsFalse(lidCollider.enabled, "The slam should break the lid on its way down.");
            Assert.That(letter.transform.position.y, Is.LessThan(lid.min.y), "The letter should land below the lid.");
            Assert.IsFalse(letter.IsSlamming);
        }

        [UnityTest]
        public IEnumerator TheSlamKnocksOutASpikeBesideTheLandingButNotOneFarAway()
        {
            yield return Load();
            Enemy spike = SlamPenEnemy("^");
            Enemy armour = SlamPenEnemy("\u00a7");
            Vector2 spikeFeet = spike.transform.position;
            float landingX = SlamFloor().max.x - 1.5f;
            spike.Body.position = new Vector2(landingX - letter.shockRadius * 0.7f, spikeFeet.y);
            armour.Body.position = new Vector2(landingX - letter.shockRadius - 2f, spikeFeet.y);
            yield return new WaitForFixedUpdate();

            int knockedOut = -1;
            letter.Slammed += n => knockedOut = n;
            PlaceLetterAt(new Vector2(landingX, spikeFeet.y + 1.2f));
            letter.SetInput(0f, false, true);

            yield return WaitUntil(() => knockedOut >= 0);
            Assert.AreEqual(1, knockedOut);
            Assert.IsTrue(spike == null || !spike.Box.enabled, "The spike beside the landing should be knocked out.");
            Assert.IsTrue(armour != null && armour.Box.enabled, "The armour out of reach should be left alone.");
            Assert.IsTrue(letter != null && !letter.IsFallingOff, "The slam shouldn't hurt the letter.");
        }

        [UnityTest]
        public IEnumerator UppercaseFromACapsLockChangeDoesNotSlam()
        {
            yield return Load();
            Bounds floor = SlamFloor();
            Bounds power = All<WordBlock>().Single(b => b.kind == WordKind.Power
                && b.transform.position.x > floor.min.x && b.transform.position.x < floor.max.x).GetComponent<BoxCollider2D>().bounds;

            bool started = false, denied = false, grewInTheAir = false;
            letter.SlamStarted += () => started = true;
            letter.GrowDenied += () => denied = true;
            letter.FormChanged += form => grewInTheAir = form == LetterForm.Uppercase && !letter.IsGrounded;

            // Half under the power block: no room to grow until the letter has moved out, still falling.
            PlaceLetterAt(new Vector2(power.max.x - 0.05f, power.min.y - 0.45f));
            letter.SetInput(1f, false, false);
            letter.SwapCase();
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);

            Assert.IsTrue(denied, "The power block should hold the growing back at first.");
            Assert.IsTrue(grewInTheAir, "The letter should grow in the air once it is clear.");
            Assert.IsFalse(started, "Only the player's own Shift should slam, even when growing has to wait.");
        }

        // ---------------------------------------------------------------- floating words

        /// <summary>The pen's two floats: the one resting low that rises, and the one resting high that sinks.</summary>
        static void PenFloats(out FloatingWord rising, out FloatingWord sinking)
        {
            FloatingWord[] floats = All<FloatingWord>().OrderBy(f => f.transform.position.x).ToArray();
            Assert.That(floats.Length, Is.EqualTo(2), "The Enemy Room should have the float pen's two floats.");
            rising = floats[0];
            sinking = floats[1];
            Assert.That(rising.topY, Is.GreaterThan(rising.RestY + 3f));
            Assert.That(sinking.bottomY, Is.LessThan(sinking.RestY - 3f));
        }

        void StandOn(FloatingWord floating)
        {
            Bounds word = floating.GetComponent<BoxCollider2D>().bounds;
            PlaceLetterAt(new Vector2(word.center.x, word.max.y + 0.02f));
        }

        [UnityTest]
        public IEnumerator LowercaseRidesAFloatUpToItsTop()
        {
            yield return Load();
            PenFloats(out FloatingWord rising, out _);
            StandOn(rising);
            yield return WaitUntil(() => letter.IsGrounded);

            int airborneSteps = 0;
            yield return WaitUntil(() =>
            {
                if (!letter.IsGrounded) airborneSteps++;
                return rising.transform.position.y >= rising.topY - 0.01f;
            });
            Assert.That(airborneSteps, Is.LessThan(3), "The letter should ride the float, not bounce on it.");
            Assert.AreEqual(rising.gameObject, letter.Surface.gameObject);
            Assert.AreEqual(rising.GetComponent<BoxCollider2D>().bounds.max.y, letter.transform.position.y, 0.08f,
                "The letter should stand on top of the float.");
        }

        [UnityTest]
        public IEnumerator UppercaseSinksAFloatAndItDriftsBackWhenLeft()
        {
            yield return Load();
            PenFloats(out _, out FloatingWord sinking);
            StandOn(sinking);
            yield return WaitUntil(() => letter.IsGrounded);
            yield return Steps(10);
            Assert.AreEqual(sinking.RestY, sinking.transform.position.y, 0.1f, "a shouldn't sink it.");

            letter.SetInput(0f, false, true);
            int airborneSteps = 0;
            yield return WaitUntil(() =>
            {
                if (!letter.IsGrounded) airborneSteps++;
                return sinking.transform.position.y <= sinking.bottomY + 0.01f;
            });
            Assert.That(airborneSteps, Is.LessThan(3), "The letter should ride the float down, not fall behind it.");

            // Step off onto the floor beyond, and the float goes back up.
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.Surface != null && letter.Surface.gameObject != sinking.gameObject);
            yield return WaitUntil(() => sinking.transform.position.y >= sinking.RestY - 0.1f);
        }

        [UnityTest]
        public IEnumerator ASlamJoltsAFloatDown()
        {
            yield return Load();
            PenFloats(out _, out FloatingWord sinking);
            Bounds word = sinking.GetComponent<BoxCollider2D>().bounds;

            float speedAtLanding = 0f;
            letter.Slammed += _ => speedAtLanding = sinking.Velocity;
            PlaceLetterAt(new Vector2(word.center.x, word.max.y + 1.5f));
            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => speedAtLanding < 0f);
            Assert.That(speedAtLanding, Is.LessThan(-sinking.sinkSpeed * 2f), "A slam should push it down much faster than standing.");
        }

        static Bounds WordBounds(string text) => GameObject.Find("Word " + text).GetComponent<BoxCollider2D>().bounds;

        /// <summary>Walks (and holds Shift if asked) for a while in the headwind and returns how far the letter got.</summary>
        IEnumerator WalkIntoTheHeadwind(bool shift, float[] moved)
        {
            Bounds head = WordBounds("headwind: walk into it as A");
            PlaceLetterAt(new Vector2(head.center.x, head.max.y + 0.02f));
            yield return WaitUntil(() => letter.IsGrounded);
            letter.SetInput(1f, false, shift);
            yield return Steps(25);
            float start = letter.transform.position.x;
            yield return Steps(50);
            moved[0] = letter.transform.position.x - start;
        }

        [UnityTest]
        public IEnumerator TheHeadwindPushesLowercaseBack()
        {
            yield return Load();
            var moved = new float[1];
            yield return WalkIntoTheHeadwind(false, moved);
            Assert.That(moved[0], Is.LessThan(-0.5f), "a walking into the wind should be pushed back.");
        }

        [UnityTest]
        public IEnumerator UppercaseWalksIntoTheHeadwind()
        {
            yield return Load();
            var moved = new float[1];
            yield return WalkIntoTheHeadwind(true, moved);
            Assert.AreEqual(LetterForm.Uppercase, letter.Form);
            Assert.That(moved[0], Is.GreaterThan(1.5f), "A should walk through the wind, only a little slower.");
        }

        [UnityTest]
        public IEnumerator TheWindDoesNotMoveAStandingUppercase()
        {
            yield return Load();
            Bounds take = WordBounds("tailwind");
            PlaceLetterAt(new Vector2(take.center.x, take.max.y + 0.02f));
            yield return GrowInPlace();
            float start = letter.transform.position.x;
            yield return Steps(50);
            Assert.AreEqual(start, letter.transform.position.x, 0.02f);
        }

        /// <summary>Runs at the tailwind pit as a and jumps from its edge; true if the letter lands beyond it.</summary>
        IEnumerator JumpThePit(bool[] landed)
        {
            Bounds take = WordBounds("tailwind");
            Bounds landing = WordBounds("and land");
            PlaceLetterAt(new Vector2(take.min.x + 0.3f, take.max.y + 0.02f));
            yield return WaitUntil(() => letter.IsGrounded);
            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.transform.position.x > take.max.x - 0.15f);
            letter.PressJump();
            letter.SetInput(1f, true, false);
            yield return Steps(5);
            yield return WaitUntil(() => letter.IsGrounded || letter.transform.position.y < take.max.y - 2f);
            landed[0] = letter.IsGrounded && letter.transform.position.x > landing.min.x;
        }

        [UnityTest]
        public IEnumerator TheTailwindCarriesLowercaseOverThePit()
        {
            yield return Load();
            var landed = new bool[1];
            yield return JumpThePit(landed);
            Assert.IsTrue(landed[0], "With the wind behind it, a should clear the pit.");
        }

        [UnityTest]
        public IEnumerator WithoutTheWindThePitIsTooWide()
        {
            yield return Load();
            foreach (Wind wind in All<Wind>()) wind.strength = 0f;
            var landed = new bool[1];
            yield return JumpThePit(landed);
            Assert.IsFalse(landed[0], "The pit should be too wide for a plain jump, or the wind isn't needed.");
        }

        /// <summary>The pen's wind box along the floor: the only wind lower than the page.</summary>
        static Bounds LowWind() =>
            All<Wind>().Select(w => w.GetComponent<BoxCollider2D>().bounds).Single(b => b.size.y < 3f);

        /// <summary>Runs at the low wind from 3 units before it, jumping or growing if asked; true if it got past.</summary>
        IEnumerator CrossTheLowWind(bool jump, bool shift, bool[] passed)
        {
            Bounds wind = LowWind();
            Bounds floor = WordBounds("low wind: jump it as a");
            PlaceLetterAt(new Vector2(wind.min.x - 3f, floor.max.y + 0.02f));
            yield return WaitUntil(() => letter.IsGrounded);
            letter.SetInput(1f, false, shift);
            if (jump)
            {
                yield return WaitUntil(() => letter.transform.position.x > wind.min.x - 0.6f);
                letter.PressJump();
                letter.SetInput(1f, true, shift);
            }
            for (int i = 0; i < 300 && letter.transform.position.x < wind.max.x + 0.6f; i++)
                yield return new WaitForFixedUpdate();
            passed[0] = letter.transform.position.x >= wind.max.x + 0.6f;
        }

        [UnityTest]
        public IEnumerator TheLowWindPushesAWalkingLowercaseBack()
        {
            yield return Load();
            var passed = new bool[1];
            yield return CrossTheLowWind(false, false, passed);
            Assert.IsFalse(passed[0], "a walking into the low wind should be pushed back.");
        }

        [UnityTest]
        public IEnumerator LowercaseJumpsOverTheLowWind()
        {
            yield return Load();
            var passed = new bool[1];
            yield return CrossTheLowWind(true, false, passed);
            Assert.IsTrue(passed[0], "a should clear a wind that only blows along the floor.");
        }

        [UnityTest]
        public IEnumerator UppercaseWalksThroughTheLowWind()
        {
            yield return Load();
            var passed = new bool[1];
            yield return CrossTheLowWind(false, true, passed);
            Assert.IsTrue(passed[0], "A should walk through the low wind.");
        }

        [UnityTest]
        public IEnumerator GustsComeAndGo()
        {
            yield return Load();
            Wind gusty = All<Wind>().Single(w => w.gustSeconds > 0f);
            yield return WaitUntil(() => gusty.Blowing <= 0f);
            Assert.AreEqual(0f, gusty.Speed);
            yield return WaitUntil(() => gusty.Blowing >= 1f);
            Assert.AreEqual(gusty.strength * gusty.direction, gusty.Speed, 0.001f);
        }

        // ---------------------------------------------------------------- heavy words

        static HeavyWord PenHeavy() => All<HeavyWord>().Single();
        static Bounds PushWall() => WordBounds("wall");

        /// <summary>Walks right into the heavy word from 1.5 units before it, as a or A, for the given steps.</summary>
        IEnumerator WalkIntoTheHeavyWord(bool shift, int steps)
        {
            Bounds heavy = PenHeavy().GetComponent<BoxCollider2D>().bounds;
            Bounds floor = WordBounds("heavy words: push it as A, climb it as a");
            PlaceLetterAt(new Vector2(heavy.min.x - 1.5f, floor.max.y + 0.02f));
            yield return WaitUntil(() => letter.IsGrounded);
            letter.SetInput(1f, false, shift);
            yield return Steps(steps);
        }

        [UnityTest]
        public IEnumerator LowercaseCannotPushAHeavyWord()
        {
            yield return Load();
            HeavyWord heavy = PenHeavy();
            float start = heavy.transform.position.x;
            yield return WalkIntoTheHeavyWord(false, 120);
            Assert.AreEqual(start, heavy.transform.position.x, 0.001f, "a is too light to move it.");
            Assert.IsFalse(letter.IsPushing);
            Assert.That(letter.transform.position.x, Is.LessThan(heavy.GetComponent<BoxCollider2D>().bounds.min.x),
                "a should stay on its side of it.");
        }

        [UnityTest]
        public IEnumerator UppercasePushesAHeavyWordUpToTheWall()
        {
            yield return Load();
            HeavyWord heavy = PenHeavy();
            Bounds wall = PushWall();
            float bottom = heavy.GetComponent<BoxCollider2D>().bounds.min.y;
            yield return WalkIntoTheHeavyWord(true, 60);
            Assert.IsTrue(letter.IsPushing);
            yield return WaitUntil(() => heavy.GetComponent<BoxCollider2D>().bounds.max.x >= wall.min.x - 0.03f);
            yield return Steps(30);
            Bounds pushed = heavy.GetComponent<BoxCollider2D>().bounds;
            Assert.That(pushed.max.x, Is.LessThanOrEqualTo(wall.min.x + 0.001f), "The wall should stop it.");
            Assert.AreEqual(bottom, pushed.min.y, 0.02f, "It should slide along the floor, not sink or rise.");
        }

        [UnityTest]
        public IEnumerator LowercaseClimbsTheWallFromTheHeavyWordButNotFromTheFloor()
        {
            yield return Load();
            Bounds wall = PushWall();
            Bounds floor = WordBounds("heavy words: push it as A, climb it as a");

            PlaceLetterAt(new Vector2(wall.min.x - 0.6f, floor.max.y + 0.02f));
            yield return WaitUntil(() => letter.IsGrounded);
            letter.PressJump();
            letter.SetInput(1f, true, false);
            yield return Steps(90);
            Assert.That(letter.transform.position.y, Is.LessThan(wall.max.y - 0.5f), "The wall is too high to jump from the floor.");

            // Move the word to the wall, as A would, and climb from it.
            HeavyWord heavy = PenHeavy();
            Bounds word = heavy.GetComponent<BoxCollider2D>().bounds;
            var body = heavy.GetComponent<Rigidbody2D>();
            body.position += new Vector2(wall.min.x - 0.01f - word.max.x, 0f);
            yield return Steps(2);
            word = heavy.GetComponent<BoxCollider2D>().bounds;
            PlaceLetterAt(new Vector2(word.max.x - 0.6f, word.max.y + 0.02f));
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.IsGrounded);
            letter.PressJump();
            letter.SetInput(1f, true, false);
            bool onTop = false;
            for (int i = 0; i < 120 && !onTop; i++)
            {
                yield return new WaitForFixedUpdate();
                onTop = letter.IsGrounded && letter.transform.position.y > wall.max.y - 0.05f;
            }
            Assert.IsTrue(onTop, "From the heavy word, a should get up onto the wall.");
        }

        [UnityTest]
        public IEnumerator AHeavyWordThatFallsOutOfTheLevelComesBack()
        {
            yield return Load();
            HeavyWord heavy = PenHeavy();
            var body = heavy.GetComponent<Rigidbody2D>();
            body.position = heavy.StartPosition + Vector2.down * (heavy.lostBelow + 1f);
            yield return Steps(3);
            Assert.AreEqual(heavy.StartPosition.x, body.position.x, 0.01f);
            Assert.AreEqual(heavy.StartPosition.y, body.position.y, 0.01f);
        }

        // ---------------------------------------------------------------- ink

        /// <summary>The pen's two pools, left to right: the shallow one behind the low wall and the deep one.</summary>
        static void PenPools(out InkPool shallow, out InkPool deep)
        {
            // The ink pen comes first; the pool where enemies sink is after it.
            InkPool[] pools = All<InkPool>().OrderBy(p => p.transform.position.x).Take(2).ToArray();
            Assert.That(pools.Length, Is.EqualTo(2), "The Enemy Room should have the ink pen's two pools.");
            shallow = pools[0];
            deep = pools[1];
            Assert.That(deep.SurfaceY, Is.GreaterThan(shallow.SurfaceY + 1f));
        }

        static Bounds PoolBounds(InkPool pool) => pool.GetComponent<BoxCollider2D>().bounds;

        /// <summary>How far the letter's middle is under the pool's surface.</summary>
        float DepthIn(InkPool pool) => pool.SurfaceY - (letter.transform.position.y + 0.2f);

        [UnityTest]
        public IEnumerator LowercaseFloatsUpToTheSurfaceOfInk()
        {
            yield return Load();
            PenPools(out InkPool shallow, out _);
            Bounds pool = PoolBounds(shallow);
            PlaceLetterAt(new Vector2(pool.min.x + 1f, pool.min.y + 0.15f));
            yield return WaitUntil(() => letter.IsFloating);
            yield return Steps(60);
            Assert.IsTrue(letter.IsFloating);
            Assert.IsFalse(letter.IsGrounded, "a shouldn't rest on the bottom of ink.");
            Assert.AreEqual(0f, DepthIn(shallow), 0.05f, "a should float with its middle at the surface.");
        }

        [UnityTest]
        public IEnumerator FloatingLowercaseCannotPassUnderTheDivider()
        {
            yield return Load();
            PenPools(out InkPool shallow, out InkPool deep);
            Bounds pool = PoolBounds(shallow);
            PlaceLetterAt(new Vector2(pool.min.x + 1f, pool.min.y + 0.15f));
            yield return WaitUntil(() => letter.IsFloating);
            letter.SetInput(1f, false, false);
            yield return Steps(200);
            Assert.That(letter.transform.position.x, Is.LessThan(PoolBounds(deep).min.x - 1f),
                "a floats too high to get under the dividing wall.");
        }

        [UnityTest]
        public IEnumerator UppercaseSinksAndWalksUnderTheDividerIntoTheDeepPool()
        {
            yield return Load();
            PenPools(out InkPool shallow, out InkPool deep);
            Bounds pool = PoolBounds(shallow);
            PlaceLetterAt(new Vector2(pool.min.x + 1f, pool.min.y + 0.15f));
            yield return WaitUntil(() => letter.IsFloating);
            yield return GrowInPlace();
            yield return WaitUntil(() => letter.IsGrounded);
            Assert.AreEqual(pool.min.y + Size(8f), letter.transform.position.y, 0.05f, "A should sink to the bottom.");
            letter.SetInput(1f, false, true);
            yield return WaitUntil(() => letter.transform.position.x > PoolBounds(deep).min.x + 1f);
        }

        [UnityTest]
        public IEnumerator UppercaseCannotHopOutOfTheDeepPoolButLowercaseJumpsOut()
        {
            yield return Load();
            PenPools(out _, out InkPool deep);
            Bounds pool = PoolBounds(deep);
            Bounds outWord = WordBounds("and out");
            PlaceLetterAt(new Vector2(pool.max.x - 1.6f, pool.min.y + 0.15f));
            yield return GrowInPlace();
            yield return WaitUntil(() => letter.IsGrounded);
            float highest = float.MinValue;
            letter.PressJump();
            letter.SetInput(0f, true, true);
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
                highest = Mathf.Max(highest, letter.transform.position.y);
            }
            Assert.That(highest, Is.LessThan(deep.SurfaceY - 1f), "A's hop shouldn't get it anywhere near the surface.");

            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.IsFloating);
            letter.PressJump();
            letter.SetInput(1f, true, false);
            yield return WaitUntil(() => letter.IsGrounded && letter.transform.position.y > outWord.max.y - 0.05f);
        }

        [UnityTest]
        public IEnumerator TurningUppercaseInInkSinksInsteadOfSlamming()
        {
            yield return Load();
            PenPools(out InkPool shallow, out _);
            Bounds pool = PoolBounds(shallow);
            PlaceLetterAt(new Vector2(pool.min.x + 1f, pool.min.y + 0.15f));
            yield return WaitUntil(() => letter.IsFloating);
            bool slammed = false;
            letter.SlamStarted += () => slammed = true;
            yield return GrowInPlace();
            yield return Steps(30);
            Assert.IsFalse(slammed);
            Assert.IsFalse(letter.IsSlamming);
        }

        [UnityTest]
        public IEnumerator EnemiesWalkIntoInkSinkAndStayOnTheBottom()
        {
            yield return Load();
            Bounds floor = WordBounds("the floor of the pool");
            InkPool pool = All<InkPool>().OrderBy(p => p.transform.position.x).Last();
            Bounds ink = PoolBounds(pool);
            Enemy[] sinkers = All<Enemy>()
                .Where(e => Mathf.Abs(e.Body.position.x - ink.center.x) < ink.extents.x + 2f).ToArray();
            Assert.That(sinkers.Length, Is.EqualTo(2), "Armour and a spike start on the pool's walls.");
            Assert.That(sinkers.All(e => e.Body.position.y > pool.SurfaceY), "They start out of the ink.");

            Func<Enemy, bool> onTheBottom = e => Mathf.Abs(e.Body.position.y - floor.max.y) < 0.02f
                && e.Body.position.x > ink.min.x && e.Body.position.x < ink.max.x;
            float start = Time.time;
            while (!sinkers.All(e => onTheBottom(e)) && Time.time - start < 8f) yield return new WaitForFixedUpdate();
            Assert.That(sinkers.All(e => onTheBottom(e)), "Both should walk off their wall and sink to the bottom.");

            // Slower in the ink, and the pool's walls turn them, so they stay down there.
            float before = sinkers[0].Body.position.x;
            yield return Steps(Mathf.RoundToInt(1f / Time.fixedDeltaTime));
            float speed = Mathf.Abs(sinkers[0].Body.position.x - before);
            Assert.That(speed, Is.EqualTo(sinkers[0].GetComponent<EnemyWalk>().speed * EnemyInk.MoveFactor).Within(0.1f));
            start = Time.time;
            while (Time.time - start < 12f)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(sinkers.All(e => onTheBottom(e)), "They should keep to the bottom of the pool.");
            }
        }

        static float Size(float pixels) => pixels / 72f;

        IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        IEnumerator GrowInPlace()
        {
            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
        }

        [UnityTest]
        public IEnumerator StompingACurlTurnsItIntoAStillO()
        {
            yield return Load();
            EnemyCurl curl = UnityEngine.Object.FindAnyObjectByType<EnemyCurl>();
            curl.walkSpeed = 0f;
            yield return GrowInPlace();

            PlaceLetterAt((Vector2)curl.transform.position + Vector2.up * 1.2f);
            yield return WaitUntil(() => curl.IsCurled);
            Assert.AreEqual("o", curl.GetComponent<Enemy>().glyph.text);
            Assert.IsTrue(letter != null, "The stomp shouldn't hurt the letter.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoAStillOKicksIt()
        {
            yield return Load();
            EnemyCurl curl = UnityEngine.Object.FindAnyObjectByType<EnemyCurl>();
            curl.walkSpeed = 0f;
            yield return GrowInPlace();
            PlaceLetterAt((Vector2)curl.transform.position + Vector2.up * 1.2f);
            yield return WaitUntil(() => curl.IsCurled);

            // Off to the side at once, before the bounce comes down on the o and kicks it.
            PlaceLetterAt((Vector2)curl.transform.position + Vector2.left * 2f);
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.Form == LetterForm.Lowercase && letter.IsGrounded);
            Assert.IsFalse(curl.IsRolling, "The o should lie still until it is kicked.");

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => curl.IsRolling);
            Assert.IsTrue(letter != null, "Kicking a still o shouldn't hurt the letter.");
        }

        [UnityTest]
        public IEnumerator TouchingACaseSwapperTurnsShiftAround()
        {
            yield return Load();
            EnemyCaseSwap swapper = UnityEngine.Object.FindAnyObjectByType<EnemyCaseSwap>();
            swapper.GetComponent<EnemyWalk>().speed = 0f;
            PlaceLetterAt((Vector2)swapper.transform.position + Vector2.left * 1.5f);
            yield return WaitUntil(() => letter.IsGrounded);

            letter.SetInput(1f, false, false);
            yield return WaitUntil(() => letter.IsCapsLocked);
            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            Assert.IsTrue(letter != null, "A case swapper shouldn't hurt.");
        }

        [UnityTest]
        public IEnumerator TheEyeWakesAndDriftsTowardTheLetter()
        {
            yield return Load();
            EnemyFollow eye = UnityEngine.Object.FindAnyObjectByType<EnemyFollow>();
            float startDistance = 6f;
            PlaceLetterAt(new Vector2(eye.transform.position.x - startDistance, letter.transform.position.y));
            yield return WaitUntil(() => eye.IsAwake);
            Vector2 woke = eye.transform.position;
            yield return WaitUntil(() => Vector2.Distance(eye.transform.position, woke) > 1f);
            Assert.That(eye.transform.position.x, Is.LessThan(woke.x), "It should drift toward the letter.");
        }

        [UnityTest]
        public IEnumerator TheMimicCopiesTheLettersCaseAMomentLater()
        {
            yield return Load();
            EnemyMimic mimic = UnityEngine.Object.FindAnyObjectByType<EnemyMimic>();
            Assert.IsFalse(mimic.IsUpper);

            letter.SetInput(0f, false, true);
            yield return WaitUntil(() => letter.Form == LetterForm.Uppercase);
            Assert.IsFalse(mimic.IsUpper, "It should take a moment to copy.");
            yield return WaitUntil(() => mimic.IsUpper);
            Assert.AreEqual("B", mimic.GetComponent<Enemy>().glyph.text);

            letter.SetInput(0f, false, false);
            yield return WaitUntil(() => !mimic.IsUpper);
        }

        [UnityTest]
        public IEnumerator AnUppercaseMimicCantBeStomped()
        {
            yield return Load();
            EnemyMimic mimic = UnityEngine.Object.FindAnyObjectByType<EnemyMimic>();
            mimic.GetComponent<EnemyWalk>().speed = 0f;
            yield return GrowInPlace();
            yield return WaitUntil(() => mimic.IsUpper);

            PlaceLetterAt((Vector2)mimic.transform.position + Vector2.up * 1.2f);
            LetterController dropping = letter;
            yield return WaitUntil(() => dropping == null);
        }

        [UnityTest]
        public IEnumerator TheCursorErasesTheWordsBehindTheLetterAndStops()
        {
            yield return Load();
            EnemyCursor cursor = UnityEngine.Object.FindAnyObjectByType<EnemyCursor>();
            cursor.speed = 12f; // The stretch takes about 9 s at its own speed, longer than the timeout.
            Bounds safe = GameObject.Find("Word safe again").GetComponent<BoxCollider2D>().bounds;
            PlaceLetterAt(new Vector2(safe.center.x, safe.max.y + 0.01f));

            yield return WaitUntil(() => cursor.IsDone);
            WordBlock first = GameObject.Find("Word backspace").GetComponent<WordBlock>();
            Assert.IsTrue(first.IsErased, "Words in the stretch should be erased.");
            Assert.IsFalse(GameObject.Find("Word safe again").GetComponent<WordBlock>().IsErased,
                "Words past the stretch should stay.");
            Assert.IsTrue(letter != null, "A letter past the stretch should be safe.");
        }

        [UnityTest]
        public IEnumerator StandingStillInFrontOfTheCursorCostsALife()
        {
            yield return Load();
            EnemyCursor cursor = UnityEngine.Object.FindAnyObjectByType<EnemyCursor>();
            PlaceLetterAt(new Vector2(cursor.transform.position.x + cursor.startLead + 0.5f, letter.transform.position.y));
            LetterController standing = letter;
            yield return WaitUntil(() => standing == null);
        }

        [UnityTest]
        public IEnumerator ShootingAnArrowPaysNothing()
        {
            yield return Load();
            StandBeforeTheLauncher(6f);
            yield return WaitUntil(() => All<EnemyArrow>().Length > 0);

            int score = LevelRun.Score;
            Enemy arrow = All<EnemyArrow>()[0].GetComponent<Enemy>();
            arrow.Kill();
            yield return null;
            Assert.AreEqual(score, LevelRun.Score);
        }
    }
}

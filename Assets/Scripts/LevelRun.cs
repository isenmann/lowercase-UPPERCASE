using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The clock and the score, which outlive the scene reload that follows a death. Time running out ends the run, so
    /// nothing refills the clock during a level. The score carries from level to level until the menu is reached.
    /// Enemies return when the level reloads, so each one pays only once per level, otherwise dying could earn points.
    /// A # that was picked up stays gone through the reload. A run has three lives; losing the last one ends it.
    /// Every 1500 points give back one lost life; they never add a life beyond the three.
    /// The asterisks from a power block carry into later levels, but a death takes them away.
    /// </summary>
    public static class LevelRun
    {
        public const float LevelSeconds = 60f;
        public const int PointsPerEnemy = 100;
        public const int PointsPerSecondLeft = 10;
        public const int PointsPerCollectible = 50;
        public const int StartingLives = 3;
        public const int PointsPerLifeRefill = 1500;

        static readonly HashSet<Vector2> paidEnemies = new HashSet<Vector2>();
        static readonly HashSet<Vector2> collected = new HashSet<Vector2>();

        // Only set in the moment before a reload, so any other way into a level starts it afresh.
        static bool continuingAfterDeath;
        static int scoreAtLevelStart;
        static bool armedAtLevelStart;
        // The score at which the next lost life comes back, and how many came back in this level, so a restart can
        // take them back with the points that earned them.
        static int nextRefillScore;
        static int refillAtLevelStart;
        static int refillsThisLevel;

        public static float TimeLeft { get; set; }
        public static int Score { get; private set; }
        public static int Lives { get; private set; }
        /// <summary>Whether the letter has its asterisks. Set by the shooter when a power block arms it.</summary>
        public static bool HasAsterisks { get; set; }

        public static bool TimeIsUp => TimeLeft <= 0f;

        // Domain reload is off in this project's Play mode settings, so statics survive from one session to the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void NewGame()
        {
            continuingAfterDeath = false;
            scoreAtLevelStart = 0;
            Score = 0;
            Lives = StartingLives;
            HasAsterisks = false;
            armedAtLevelStart = false;
            nextRefillScore = PointsPerLifeRefill;
            refillAtLevelStart = nextRefillScore;
            refillsThisLevel = 0;
            TimeLeft = LevelSeconds;
            paidEnemies.Clear();
            collected.Clear();
        }

        /// <summary>What this level has paid so far: enemies and #, and once banked, the time left.</summary>
        public static int PointsThisLevel => Score - scoreAtLevelStart;

        /// <summary>How many # the letter has picked up in this level so far.</summary>
        public static int CollectedThisLevel => collected.Count;

        /// <summary>Called by the flow as a level's scene loads, with that level's clock. True if the letter is coming back after a death.</summary>
        public static bool EnterLevel(float seconds = LevelSeconds)
        {
            if (continuingAfterDeath)
            {
                continuingAfterDeath = false;
                return true;
            }

            TimeLeft = seconds;
            scoreAtLevelStart = Score;
            armedAtLevelStart = HasAsterisks;
            refillAtLevelStart = nextRefillScore;
            refillsThisLevel = 0;
            paidEnemies.Clear();
            collected.Clear();
            return false;
        }

        /// <summary>Takes one life, and the asterisks with it. True if any lives are left to carry on with.</summary>
        public static bool LoseLife()
        {
            HasAsterisks = false;
            Lives = Mathf.Max(0, Lives - 1);
            return Lives > 0;
        }

        public static void Tick(float deltaTime) => TimeLeft = Mathf.Max(0f, TimeLeft - deltaTime);

        /// <summary>Keeps the clock and score through the reload that follows a death.</summary>
        public static void ContinueAfterDeath() => continuingAfterDeath = true;

        /// <summary>A deliberate restart is a fresh attempt, so it gives back what this level earned.</summary>
        public static void AbandonLevel()
        {
            Score = scoreAtLevelStart;
            HasAsterisks = armedAtLevelStart;
            Lives = Mathf.Max(1, Lives - refillsThisLevel);
            nextRefillScore = refillAtLevelStart;
            refillsThisLevel = 0;
        }

        /// <summary>
        /// Adds points, and gives back a lost life for each 1500 passed. Passing one with every life left gives
        /// nothing: lives are refilled, never added.
        /// </summary>
        static void AddPoints(int points)
        {
            Score += points;
            while (Score >= nextRefillScore)
            {
                nextRefillScore += PointsPerLifeRefill;
                if (Lives > 0 && Lives < StartingLives)
                {
                    Lives++;
                    refillsThisLevel++;
                    LifeRefilled?.Invoke();
                }
            }
        }

        /// <summary>Raised when points give back a lost life.</summary>
        public static event System.Action LifeRefilled;

        public static void EnemyKilled(Vector2 spawnPosition)
        {
            if (paidEnemies.Add(spawnPosition)) AddPoints(PointsPerEnemy);
        }

        /// <summary>Keyed by where the # was placed, like enemies, so it is only ever paid once per level.</summary>
        public static void Collect(Vector2 position)
        {
            if (collected.Add(position)) AddPoints(PointsPerCollectible);
        }

        public static bool IsCollected(Vector2 position) => collected.Contains(position);

        public static void BankTimeLeft()
        {
            AddPoints(Mathf.CeilToInt(TimeLeft) * PointsPerSecondLeft);
            TimeLeft = 0f;
        }
    }
}

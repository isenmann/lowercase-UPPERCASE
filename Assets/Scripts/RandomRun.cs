using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The endless run of random levels started from the menu. The seed only changes when a level is finished, so a
    /// death or a restart brings back the same level. The score and lives carry on through <see cref="LevelRun"/>.
    /// </summary>
    public static class RandomRun
    {
        static int seed;

        /// <summary>Which random level of this run is being played, counting from 1.</summary>
        public static int LevelNumber { get; private set; }

        /// <summary>The current level's seed. Opening the scene directly, as in the editor, starts a run.</summary>
        public static int Seed
        {
            get
            {
                if (seed == 0) Begin();
                return seed;
            }
        }

        // Domain reload is off in this project's Play mode settings, so statics survive from one session to the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            seed = 0;
            LevelNumber = 0;
        }

        public static void Begin()
        {
            seed = NewSeed();
            LevelNumber = 1;
        }

        /// <summary>Called when the letter slots into the exit, before the scene loads again.</summary>
        public static void Advance()
        {
            seed = NewSeed();
            LevelNumber++;
        }

        static int NewSeed() => Random.Range(1, 100000);
    }
}

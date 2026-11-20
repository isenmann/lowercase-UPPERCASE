using UnityEngine;

namespace LowercaseUppercase.Levels
{
    /// <summary>
    /// Sections for the things that give A more to do: the slam, heavy words, ink (with enemies sinking in it),
    /// floating words and wind. Each is taught once with a note in a level, and comes back without one later; the
    /// random levels use them all without notes. Kept out of the editor-only parts, because random levels are built
    /// while the game runs.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        // ---------------------------------------------------------------- slam

        /// <summary>Crack: a shelf with a brick lid over a word where armour and a spike walk. Slam through and knock them out.</summary>
        static float CrackSlam(Transform level, float x) => CrackSlam(level, x, true);

        static float CrackSlam(Transform level, float x, bool notes) => SlamShelf(level, x, notes,
            "guards walk below the brick", "jump up", "and slam", spikes: false);

        /// <summary>Gate: the same shelf over two spikes, which only a slam beside them or an asterisk gets rid of.</summary>
        static float GateSlam(Transform level, float x) => SlamShelf(level, x, false,
            "two spikes walk under the brick", "climb up", "then down", spikes: true);

        static float SlamShelf(Transform level, float x, bool notes, string floor, string before, string after, bool spikes)
        {
            float left = x;
            WordBlock ground = Word(level, floor, x, Ground, 72f);
            x = RightEdge(ground) + WordGap;
            PlaceCheckpoint(level, left + 20f, Ground);
            if (notes) Note(level, left + 40f, 300f, "grow in the air to slam: it breaks brick and knocks out what walks beside you");

            float shelf = left + 260f;
            shelf = RightEdge(Word(level, before, shelf, LowPlatform, 72f));
            WordBlock lid = BrickWord(level, "brick", shelf, LowPlatform);
            float lidLeft = shelf;
            shelf = RightEdge(lid);
            Word(level, after, shelf, LowPlatform, 72f);
            // Under the lid, for whoever comes down through it.
            HashRow(level, lidLeft + 40f, OnGround, 3);

            // One walks before the shelf's lid and one under it, so a slam through the lid lands among them.
            if (spikes) BuildSpike(level, ground, left + 160f);
            else BuildArmour(level, ground, left + 160f);
            BuildSpike(level, ground, lidLeft + 60f);
            return x;
        }

        // ---------------------------------------------------------------- heavy words

        const float HeavyPushPx = 432f;     // How far A pushes the heavy word to the wall: 6 units.
        const float HeavyWallPx = 216f;     // 3 units: out of a's jump from the floor, in reach from the heavy word.

        /// <summary>Wall: a heavy word on the floor before a wall too high for a. Push it there as A, climb it as a.</summary>
        static float WallHeavy(Transform level, float x) => WallHeavy(level, x, true);

        static float WallHeavy(Transform level, float x, bool notes) =>
            HeavyClimb(level, x, notes, "push the heavy word as A", "and up", "and down", guard: false);

        /// <summary>Tar: the same climb, with an ampersand walking the floor in front of the heavy word.</summary>
        static float TarHeavy(Transform level, float x) =>
            HeavyClimb(level, x, false, "something heavy blocks the way up", "on top", "over", guard: true);

        static float HeavyClimb(Transform level, float x, bool notes, string floorText, string top, string down, bool guard)
        {
            float left = x;
            WordBlock floor = Word(level, floorText, x, Ground, 72f);
            PlaceCheckpoint(level, left + 20f, Ground);
            if (notes) Note(level, left + 40f, 200f, "only A pushes heavy words; push it to the wall and climb it as a");
            x = RightEdge(floor);

            // Measured first, so it stands a whole push away from the wall.
            WordBlock heavy = HeavyWord(level, "heavy", x, Ground);
            float heavyWidthPx = RightEdge(heavy) - x;
            Object.DestroyImmediate(heavy.gameObject);
            float heavyLeft = x - HeavyPushPx - heavyWidthPx;
            HeavyWord(level, "heavy", heavyLeft, Ground);
            if (guard) BuildEnemy(level, floor, left + 100f);

            WordBlock wall = WallUpTo(level, x, HeavyWallPx, Ground);
            float ledge = TopPx(wall);
            WordBlock up = Word(level, top, RightEdge(wall), ledge, 72f);
            HashesOn(level, up, 2);
            x = RightEdge(up) + WordGap;
            Floor(level, down, ref x);
            return x;
        }

        /// <summary>A stacked "wall" standing with its bottom at bottomPx, with rows added until it is at least heightPx tall.</summary>
        static WordBlock WallUpTo(Transform level, float leftPx, float heightPx, float bottomPx)
        {
            int rows = 1;
            WordBlock wall = Stacked(level, "wall", leftPx, rows, bottomPx);
            while (wall.GetComponent<BoxCollider2D>().size.y < Size(heightPx) && rows < 12)
            {
                Object.DestroyImmediate(wall.gameObject);
                wall = Stacked(level, "wall", leftPx, ++rows, bottomPx);
            }
            return wall;
        }

        // ---------------------------------------------------------------- ink

        const float PoolLowWallPx = 86f;      // 1.2 units: a jumps it, A can't hop it.
        const float PoolOnePx = 300f;
        const float PoolGapPx = 80f;          // Under the dividing wall: 1.1 units, room for A (0.92) to walk through.
        const float PoolDividerTopPx = 160f;  // In the open, out of a's jump from the first pool's surface.
        const float PoolCeilingTopPx = 228f;  // Under a ceiling, up to it, so a can't get over it.
        const float PoolDeepPx = 150f;        // The second pool's surface over the ground: A can't get out, a can.

        /// <summary>
        /// Cellar: a low wall a jumps into a pool, a dividing wall only a sunk A gets under, and a deeper pool behind
        /// it that a floats up in and jumps out of.
        /// </summary>
        static float CellarInk(Transform level, float x) => InkPools(level, x, true, true, "ink in the cellar", enemies: false);

        static float CellarInk(Transform level, float x, bool notes, bool underCeiling) =>
            InkPools(level, x, notes, underCeiling, "ink in the cellar", enemies: false);

        /// <summary>Final: the same pools, with armour walking off the low wall into the ink and more on the bottom.</summary>
        static float FinalInk(Transform level, float x) => FinalInk(level, x, true);

        static float FinalInk(Transform level, float x, bool underCeiling) =>
            InkPools(level, x, false, underCeiling, "deep and dark", enemies: true);

        static float InkPools(Transform level, float x, bool notes, bool underCeiling, string floorText, bool enemies)
        {
            float left = x;
            Floor(level, floorText, ref x);
            PlaceCheckpoint(level, left + 20f, Ground);
            if (notes) Note(level, left + 40f, underCeiling ? 300f : 200f, "a floats on ink, A sinks to the bottom and walks under walls");

            WordBlock low = WallUpTo(level, x, PoolLowWallPx, Ground);
            float poolLeft = RightEdge(low);
            WordBlock bottom = Word(level, "the bottom of the ink", poolLeft, Ground, 72f);
            float bottomRight = RightEdge(bottom);

            float dividerLeft = poolLeft + PoolOnePx;
            float dividerTop = underCeiling ? PoolCeilingTopPx : PoolDividerTopPx;
            WordBlock divider = WallUpTo(level, dividerLeft, Ground - PoolGapPx - dividerTop, Ground - PoolGapPx);
            float dividerRight = RightEdge(divider);

            WordBlock far = WallUpTo(level, bottomRight, PoolDeepPx + 10f, Ground);
            float farRight = RightEdge(far);

            // The first pool reaches under the divider, so A walks from one pool into the other in ink.
            InkPool(level, poolLeft, dividerRight, TopPx(low) + 10f, Ground, dividerLeft);
            InkPool(level, dividerRight, bottomRight, TopPx(far) + 10f, Ground);
            // Along the bottom under the divider, where only A goes.
            HashRow(level, dividerLeft - 30f, OnGround, 3);

            if (enemies)
            {
                BuildArmour(level, low, x);
                BuildArmour(level, bottom, dividerRight + 120f);
            }

            x = farRight;
            WordBlock outWord = Word(level, "and out", x, TopPx(far), 72f);
            x = RightEdge(outWord) + WordGap;
            Floor(level, "dripping", ref x);
            return x;
        }

        // ---------------------------------------------------------------- floating words

        const float FloatLedgePx = 250f; // 270 px above the ground: higher than a can jump.

        /// <summary>
        /// Space: a float over a pit carries a up to a ledge too high to jump to; a second float at the ledge sinks
        /// under A back down to the floor.
        /// </summary>
        static float SpaceFloat(Transform level, float x) => SpaceFloat(level, x, true);

        static float SpaceFloat(Transform level, float x, bool notes) =>
            FloatRide(level, x, notes, "the words float here", "up here", "and down again", hopper: false);

        /// <summary>Ladder: the same ride, with a hopper on the ledge.</summary>
        static float LadderFloat(Transform level, float x) =>
            FloatRide(level, x, false, "ride the words", "high above", "back to the ground", hopper: true);

        static float FloatRide(Transform level, float x, bool notes, string floorText, string ledgeText, string downText,
            bool hopper)
        {
            float left = x;
            Floor(level, floorText, ref x);
            PlaceCheckpoint(level, left + 20f, Ground);
            if (notes) Note(level, left + 40f, 160f, "a rides a float up, A rides it down");

            WordBlock up = FloatWord(level, "float", x, Ground, FloatLedgePx);
            HashRow(level, x + 60f, FloatLedgePx - 120f, 3);
            x = RightEdge(up) + WordGap;
            WordBlock ledge = Word(level, ledgeText, x, FloatLedgePx, 72f);
            if (hopper) BuildHopper(level, ledge, x + 120f);
            x = RightEdge(ledge) + WordGap;
            WordBlock down = FloatWord(level, "float", x, FloatLedgePx, Ground);
            x = RightEdge(down) + WordGap;
            Floor(level, downText, ref x);
            return x;
        }

        // ---------------------------------------------------------------- wind

        const float WindGapPx = 500f;     // Wider than a can jump, narrower than a jumps with the wind behind it.
        const float LowWindWidthPx = 240f;
        const float LowWindTallPx = 96f;

        /// <summary>
        /// Trail: a gap only a crosses, carried by a tailwind, then a headwind over a long word that pushes a back,
        /// so it is crossed as A.
        /// </summary>
        static float TrailWind(Transform level, float x) => TrailWind(level, x, true);

        static float TrailWind(Transform level, float x, bool notes) =>
            WindCrossing(level, x, notes, "the wind picks up", "and land", "walk into the wind as A", 0f, lowWind: false);

        /// <summary>Skyline: the gap in gusts, a low wind box to jump as a, and the headwind.</summary>
        static float SkylineGusts(Transform level, float x) =>
            WindCrossing(level, x, false, "wait for a gust", "caught it", "lean into the wind", 1.5f, lowWind: true);

        const float TailwindRunUpPx = 160f; // The wind starts this far before the gap, so an idle a isn't blown in.

        static float WindCrossing(Transform level, float x, bool notes, string takeOff, string landing, string headwind,
            float gustSeconds, bool lowWind)
        {
            float left = x;
            WordBlock before = Word(level, takeOff, x, Ground, 72f);
            PlaceCheckpoint(level, left + 20f, Ground);
            if (notes) Note(level, left + 40f, 200f, "wind carries a over wide gaps, and A walks into it");
            float pitLeft = RightEdge(before);
            float pitRight = pitLeft + WindGapPx;
            WindStretch(level, pitLeft - TailwindRunUpPx, pitRight + 80f, gustSeconds: gustSeconds);
            HashArc(level, pitLeft, pitRight, 5, OnGround, 120f);

            WordBlock after = Word(level, landing, pitRight, Ground, 72f);
            x = RightEdge(after) + WordGap;

            if (lowWind)
            {
                // A box along the floor only, in the middle of a long word: a jumps it, A walks through.
                float lowLeft = x;
                WordBlock low = Word(level, "jump the low wind", x, Ground, 72f);
                float boxLeft = lowLeft + 240f;
                WindStretch(level, boxLeft, boxLeft + LowWindWidthPx, blowsLeft: true,
                    topPx: Ground - LowWindTallPx, bottomPx: Ground);
                x = RightEdge(low) + WordGap;
            }

            float headLeft = x;
            WordBlock head = Word(level, headwind, x, Ground, 72f);
            x = RightEdge(head);
            WindStretch(level, headLeft, x, blowsLeft: true);
            return x + WordGap;
        }
    }
}

using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// How ink treats enemies that walk: they are heavy like A, so they walk off a ledge into ink instead of turning,
    /// sink to the bottom and walk along it, slower than outside. The pool's walls turn them, so they stay in it.
    /// Flying enemies ignore ink.
    /// </summary>
    public static class EnemyInk
    {
        /// <summary>Units per second, as fast as A sinks.</summary>
        public const float SinkSpeed = 1.8f;
        /// <summary>Share of the walking speed left in ink, as for the letter.</summary>
        public const float MoveFactor = 0.6f;

        // Over the feet: an enemy whose feet are this far under the surface is in the ink.
        const float FeetDepth = 0.05f;
        // A ledge is walked off only if there is ink this far under the feet beyond it, so a pool's bottom stays a floor.
        const float DropProbe = 0.3f;
        // The cast down starts this far over the feet, and is narrower than the enemy so walls beside it don't count.
        const float CastLift = 0.05f;
        const float CastWidthShare = 0.8f;
        const float CastHeight = 0.04f;

        static readonly RaycastHit2D[] hits = new RaycastHit2D[4];

        /// <summary>Whether an enemy with its feet here is in ink.</summary>
        public static bool IsIn(Vector2 feet) => InkPool.At(feet + Vector2.up * FeetDepth) != null;

        /// <summary>Whether there is ink to walk into beyond a ledge, judged from a point at the height of the feet.</summary>
        public static bool BeyondLedge(Vector2 pointAtFeetHeight) =>
            InkPool.At(pointAtFeetHeight + Vector2.down * DropProbe) != null;

        /// <summary>
        /// How far an enemy in ink sinks this step: up to <see cref="SinkSpeed"/>, and no further than whatever is
        /// under it. 0 when it stands on something or isn't in ink.
        /// </summary>
        public static float SinkStep(Enemy enemy, Vector2 feet, float dt, ContactFilter2D solid)
        {
            // Just over the surface counts too: an enemy that walked off a ledge onto ink drops into it.
            if (!IsIn(feet) && !BeyondLedge(feet)) return 0f;
            float wanted = SinkSpeed * dt;
            var size = new Vector2(enemy.Box.size.x * CastWidthShare, CastHeight);
            var origin = new Vector2(feet.x + enemy.Box.offset.x, feet.y + CastLift + CastHeight * 0.5f);
            int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, solid, hits, wanted + CastLift);
            float free = wanted;
            for (int i = 0; i < count; i++)
                free = Mathf.Min(free, Mathf.Max(0f, hits[i].distance - CastLift));
            return free;
        }
    }
}

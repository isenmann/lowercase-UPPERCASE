using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A pool of ink. The light a floats up to its surface and can jump out from there; the heavy A sinks to the
    /// bottom and walks along it. Both move a little slower in it. The letter reads it while it is inside the trigger,
    /// whose top is the surface. Enemies that walk sink in it too, and walk along the bottom (see <see cref="EnemyInk"/>).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class InkPool : MonoBehaviour
    {
        static readonly List<InkPool> pools = new List<InkPool>();

        BoxCollider2D box;

        /// <summary>The world y of the ink's surface.</summary>
        public float SurfaceY
        {
            get
            {
                if (box == null) box = GetComponent<BoxCollider2D>();
                return transform.position.y + box.offset.y + box.size.y * 0.5f;
            }
        }

        /// <summary>
        /// The ink at a world point, if any. Enemies are kinematic and don't get trigger calls from the pool,
        /// so they ask here instead.
        /// </summary>
        public static InkPool At(Vector2 point)
        {
            foreach (InkPool pool in pools)
            {
                if (pool.box == null) pool.box = pool.GetComponent<BoxCollider2D>();
                if (pool.box.enabled && pool.box.OverlapPoint(point)) return pool;
            }
            return null;
        }

        void OnEnable() => pools.Add(this);
        void OnDisable() => pools.Remove(this);
    }
}

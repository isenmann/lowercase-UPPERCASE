using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A pool of ink. The light a floats up to its surface and can jump out from there; the heavy A sinks to the
    /// bottom and walks along it. Both move a little slower in it. The letter reads it while it is inside the trigger,
    /// whose top is the surface.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class InkPool : MonoBehaviour
    {
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
    }
}

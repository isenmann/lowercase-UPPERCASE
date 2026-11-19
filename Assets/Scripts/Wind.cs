using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// A stretch where the wind blows sideways. It carries the light a along and pushes it back when it walks against
    /// it; the heavy A only slows down a little walking into it. The letter reads <see cref="Speed"/> while it is inside
    /// the trigger. It can blow steadily or in gusts, and its marks drift with it so the player sees which.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Wind : MonoBehaviour
    {
        [Tooltip("1 blows right, -1 blows left.")]
        public float direction = 1f;
        [Tooltip("How fast the wind carries a standing a, units per second.")]
        public float strength = 3f;
        [Tooltip("Seconds it blows, then as many seconds calm. 0 blows steadily.")]
        public float gustSeconds;
        [Tooltip("Seconds a gust takes to rise or die down.")]
        public float gustRamp = 0.4f;

        [Header("Marks")]
        public TextMeshPro[] marks;
        [Tooltip("How fast the marks drift, as a share of the wind's speed.")]
        public float markSpeedShare = 1.2f;
        [Tooltip("Units at either end of the zone where the marks fade in and out.")]
        public float markFade = 0.6f;

        /// <summary>The wind's speed this physics step, signed: positive blows right. 0 while a gust is calm.</summary>
        public float Speed => direction * strength * Blowing;
        /// <summary>0 when calm, 1 when blowing fully.</summary>
        public float Blowing { get; private set; } = 1f;

        BoxCollider2D box;
        float clock;
        float[] markAlpha;

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
        }

        // Read on first use, not in Awake: a level built while playing (the editor, a custom level) adds this
        // component, which wakes at once, before the builder has handed it its marks.
        void ReadMarkAlphas()
        {
            if (marks == null) marks = new TextMeshPro[0];
            markAlpha = new float[marks.Length];
            for (int i = 0; i < marks.Length; i++) markAlpha[i] = marks[i] != null ? marks[i].color.a : 0f;
        }

        void FixedUpdate()
        {
            clock += Time.fixedDeltaTime;
            float target = gustSeconds <= 0f || Mathf.Repeat(clock, gustSeconds * 2f) < gustSeconds ? 1f : 0f;
            Blowing = gustRamp <= 0f ? target : Mathf.MoveTowards(Blowing, target, Time.fixedDeltaTime / gustRamp);
        }

        void Update()
        {
            float left = transform.position.x + box.offset.x - box.size.x * 0.5f;
            float width = box.size.x;
            float step = Speed * markSpeedShare * Time.deltaTime;
            if (markAlpha == null || markAlpha.Length != (marks?.Length ?? 0)) ReadMarkAlphas();
            for (int i = 0; i < marks.Length; i++)
            {
                TextMeshPro mark = marks[i];
                if (mark == null) continue;
                Vector3 at = mark.transform.position;
                float along = Mathf.Repeat(at.x + step - left, width);
                mark.transform.position = new Vector3(left + along, at.y, at.z);

                float edge = Mathf.Min(along, width - along);
                float fade = markFade > 0f ? Mathf.Clamp01(edge / markFade) : 1f;
                Color color = mark.color;
                color.a = markAlpha[i] * fade * Mathf.Lerp(0.35f, 1f, Blowing);
                mark.color = color;
            }
        }
    }
}

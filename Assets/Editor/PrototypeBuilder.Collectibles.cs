using UnityEngine;

namespace LowercaseUppercase.Editor
{
    /// <summary>
    /// The gold # to collect. Positions are the centre of each # in mockup pixels. They mark the lines worth taking:
    /// arcs over pits, rows on platforms off the main path, and runs through the gaps only a fits.
    /// </summary>
    public static partial class PrototypeBuilder
    {
        const float HashFontPx = 58f;
        const float HashSpacingPx = 52f;
        const float HashTriggerPx = 44f;
        const float OnGround = Ground - 26f;       // A centre that a running letter passes through.
        const float PitArcRisePx = 70f;           // Well inside a's jump, so the arc follows a natural leap.

        static void Hash(Transform level, float centreXPx, float centreYPx)
        {
            var go = new GameObject("Collectible #");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(X(centreXPx), Y(centreYPx), 0f);

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = Vector2.one * Size(HashTriggerPx);

            var glyph = Glyph(go.transform, "#", HashFontPx, monoBold);
            glyph.sortingOrder = 5;
            var collectible = go.AddComponent<Collectible>();
            collectible.glyph = glyph;
            collectible.CentreOnInk();
        }

        /// <summary>A straight row starting at the first centre.</summary>
        static void HashRow(Transform level, float firstCentreXPx, float centreYPx, int count)
        {
            for (int i = 0; i < count; i++)
                Hash(level, firstCentreXPx + i * HashSpacingPx, centreYPx);
        }

        /// <summary>An arch over the span between two edges, highest in the middle, as a jump across it would go.</summary>
        static void HashArc(Transform level, float leftPx, float rightPx, int count, float baseYPx = OnGround,
            float risePx = PitArcRisePx)
        {
            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / (count + 1f);
                Hash(level, Mathf.Lerp(leftPx, rightPx, t), baseYPx - risePx * 4f * t * (1f - t));
            }
        }

        /// <summary>A row centred on a platform word's top.</summary>
        static void HashesOn(Transform level, WordBlock platform, int count)
        {
            var box = platform.GetComponent<BoxCollider2D>();
            float centreX = platform.transform.TransformPoint(box.offset).x * PixelsPerUnit + 640f;
            float topPx = 360f - (platform.transform.TransformPoint(box.offset).y + box.size.y * 0.5f) * PixelsPerUnit;
            HashRow(level, centreX - (count - 1) * HashSpacingPx * 0.5f, topPx - 26f, count);
        }
    }
}

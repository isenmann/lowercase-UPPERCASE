using System.Collections.Generic;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// Fills any screen with the level. The levels are built for a 16:9 frame of 1280x720 px; the camera always shows
    /// all of that frame and, when the screen has another shape, more of the world on the longer side: above and below
    /// on 4:3, left and right on 21:9. The HUD moves out to the real screen edges and its bars, the fade and the
    /// popup backdrops stretch to cover. Added at runtime by <see cref="PrototypeFlow"/>, so the scenes need no
    /// rebuild. It follows window resizes and resolution changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenFit : MonoBehaviour
    {
        public const float DesignAspect = 16f / 9f;
        public const float DesignHalfHeight = 5f;
        public const float DesignHalfWidth = DesignHalfHeight * DesignAspect;
        const float RuledLineSpacing = 1.25f; // 90 px at 72 px per unit.

        // Which screen edge each HUD element sticks to: -1 left/bottom, 0 stays centred, 1 right/top. Elements not
        // listed stay where they are (the fade and the popups, which are centred).
        static readonly Dictionary<string, Vector2Int> HudEdges = new Dictionary<string, Vector2Int>
        {
            { "Top Bar", new Vector2Int(0, 1) },
            { "Top Line", new Vector2Int(0, 1) },
            { "Title", new Vector2Int(-1, 1) },
            { "Lives", new Vector2Int(0, 1) },
            { "Sentence", new Vector2Int(0, 1) },
            { "Collectibles", new Vector2Int(0, 1) },
            { "Score", new Vector2Int(0, 1) },
            { "Timer", new Vector2Int(1, 1) },
            { "Restart", new Vector2Int(1, 1) },
            { "Bottom Bar", new Vector2Int(0, -1) },
            { "Bottom Line", new Vector2Int(0, -1) },
            { "Form", new Vector2Int(-1, -1) },
            { "Chip a", new Vector2Int(-1, -1) },
            { "Chip A", new Vector2Int(-1, -1) },
            { "Chip a Label", new Vector2Int(-1, -1) },
            { "Chip A Label", new Vector2Int(-1, -1) },
            { "Readout", new Vector2Int(-1, -1) },
            { "Controls", new Vector2Int(1, -1) },
            { "Settings Button", new Vector2Int(1, 1) },
            { "Warnings", new Vector2Int(-1, 1) },
            { "Editor Readout", new Vector2Int(-1, 1) },
            { "Palette", new Vector2Int(-1, -1) },
            { "Panel", new Vector2Int(1, 1) },
            { "Play Button", new Vector2Int(1, 1) },
        };

        /// <summary>How far the view reaches past the 16:9 frame on each side, in world units.</summary>
        public static Vector2 Extra { get; private set; }

        public Transform hud;

        Camera view;
        Vector2Int appliedFor;
        readonly List<(Transform part, Vector3 position, Vector3 scale)> hudRest = new List<(Transform, Vector3, Vector3)>();
        readonly List<GameObject> extraLines = new List<GameObject>();

        // The camera is sized here, before anything reads it in Start (the level intro does).
        void Awake()
        {
            view = GetComponent<Camera>();
            FitCamera();
        }

        void Start()
        {
            // The HUD is remembered as built, so each resize starts again from the 16:9 layout.
            if (hud != null)
                foreach (Transform part in hud.GetComponentsInChildren<Transform>(true))
                    if (part != hud) hudRest.Add((part, part.localPosition, part.localScale));
            FitHud();
            ExtendRuledLines();
        }

        void OnDestroy() => Extra = Vector2.zero;

        void LateUpdate()
        {
            if (appliedFor.x == Screen.width && appliedFor.y == Screen.height) return;
            FitCamera();
            FitHud();
            ExtendRuledLines();
        }

        void FitCamera()
        {
            appliedFor = new Vector2Int(Screen.width, Screen.height);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : DesignAspect;

            view.orthographicSize = aspect < DesignAspect ? DesignHalfHeight * DesignAspect / aspect : DesignHalfHeight;
            Extra = new Vector2(Mathf.Max(0f, view.orthographicSize * aspect - DesignHalfWidth),
                view.orthographicSize - DesignHalfHeight);
        }

        void FitHud()
        {
            float stretchX = 1f + 2f * Extra.x / (2f * DesignHalfWidth);
            float stretchY = 1f + 2f * Extra.y / (2f * DesignHalfHeight);

            foreach ((Transform part, Vector3 position, Vector3 scale) in hudRest)
            {
                if (part == null) continue;
                Vector3 newPosition = position;
                if (part.parent == hud && HudEdges.TryGetValue(part.name, out Vector2Int edge))
                    newPosition += new Vector3(edge.x * Extra.x, edge.y * Extra.y, 0f);
                part.localPosition = newPosition;

                // Bars and backdrops as wide or tall as the frame grow with the screen. Their sprites are 1 unit,
                // so the scale is their size.
                Vector3 newScale = scale;
                if (part.GetComponent<SpriteRenderer>() != null)
                {
                    if (scale.x >= 2f * DesignHalfWidth - 0.01f) newScale.x *= stretchX;
                    if (scale.y >= 2f * DesignHalfHeight - 0.01f) newScale.y *= stretchY;
                }
                part.localScale = newScale;
            }
        }

        /// <summary>The ruled lines only fill the 16:9 frame's height, so taller screens get more above and below.</summary>
        void ExtendRuledLines()
        {
            foreach (GameObject line in extraLines) if (line != null) Destroy(line);
            extraLines.Clear();
            if (Extra.y <= 0f) return;

            GameObject root = GameObject.Find("Ruled Lines");
            if (root == null || root.transform.childCount == 0) return;

            Transform lowest = null, highest = null;
            foreach (Transform line in root.transform)
            {
                if (lowest == null || line.position.y < lowest.position.y) lowest = line;
                if (highest == null || line.position.y > highest.position.y) highest = line;
            }

            float top = view.transform.position.y + view.orthographicSize;
            float bottom = view.transform.position.y - view.orthographicSize;
            for (float y = highest.position.y + RuledLineSpacing; y < top; y += RuledLineSpacing)
                extraLines.Add(CopyLine(highest, y));
            for (float y = lowest.position.y - RuledLineSpacing; y > bottom; y -= RuledLineSpacing)
                extraLines.Add(CopyLine(lowest, y));
        }

        static GameObject CopyLine(Transform line, float y)
        {
            Transform copy = Instantiate(line, line.parent);
            copy.position = new Vector3(line.position.x, y, line.position.z);
            return copy.gameObject;
        }
    }
}

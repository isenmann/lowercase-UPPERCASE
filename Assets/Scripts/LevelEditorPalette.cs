using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The level editor's palette in the bottom bar: three tabs, and a row of chips for the tab that is open. The chip
    /// picked is the tool that clicking the empty page places. Chips follow the order of <see cref="EditorPalette.All"/>.
    /// </summary>
    public class LevelEditorPalette : MonoBehaviour
    {
        const float HitPaddingUnits = 0.05f;

        public TMP_Text[] tabs;
        public GameObject[] pages;
        public SpriteRenderer[] chips;
        public Color tabColor = Color.grey;
        public Color activeTabColor = Color.white;
        public Color chipColor = Color.black;
        public Color activeChipColor = Color.yellow;

        public int Page { get; private set; }
        public PaletteItem Current { get; private set; }

        void Awake()
        {
            Select(EditorPalette.Pages[0][0]);
        }

        public void ShowPage(int page)
        {
            Page = Mathf.Clamp(page, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == Page);
                tabs[i].color = i == Page ? activeTabColor : tabColor;
            }
        }

        public void Select(PaletteItem item)
        {
            Current = item;
            int index = 0;
            for (int page = 0; page < EditorPalette.Pages.Length; page++)
                foreach (PaletteItem candidate in EditorPalette.Pages[page])
                {
                    if (candidate == item) ShowPage(page);
                    chips[index].color = candidate == item ? activeChipColor : chipColor;
                    index++;
                }
        }

        /// <summary>The tab under a world point, or -1.</summary>
        public int TabAt(Vector2 world)
        {
            for (int i = 0; i < tabs.Length; i++)
                if (Contains(tabs[i].GetComponent<Renderer>(), world)) return i;
            return -1;
        }

        /// <summary>The chip under a world point on the open page, or null.</summary>
        public PaletteItem ItemAt(Vector2 world)
        {
            int index = 0;
            for (int page = 0; page < EditorPalette.Pages.Length; page++)
                foreach (PaletteItem item in EditorPalette.Pages[page])
                {
                    if (page == Page && Contains(chips[index], world)) return item;
                    index++;
                }
            return null;
        }

        static bool Contains(Renderer shape, Vector2 world)
        {
            if (shape == null || !shape.gameObject.activeInHierarchy) return false;
            Bounds bounds = shape.bounds;
            bounds.Expand(HitPaddingUnits * 2f);
            return bounds.Contains(new Vector3(world.x, world.y, bounds.center.z));
        }
    }
}

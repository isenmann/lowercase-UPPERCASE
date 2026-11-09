using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The level editor's panel: a title, up to <see cref="Rows"/> settings with arrows to step them, and a delete
    /// button. It shows the selected piece or the level's own settings. Text settings are edited by clicking them,
    /// which starts typing.
    /// </summary>
    public class LevelEditorPanel : MonoBehaviour
    {
        public const int Rows = 5;
        const float HitPaddingUnits = 0.08f;

        public enum Hit { None, Less, More, Value, Delete }

        public SpriteRenderer background;
        public TMP_Text title;
        public TMP_Text[] labels;
        public TMP_Text[] values;
        public TMP_Text[] lessButtons;
        public TMP_Text[] moreButtons;
        public TMP_Text deleteButton;

        public IReadOnlyList<PanelRow> Shown => shown;

        readonly List<PanelRow> shown = new List<PanelRow>();

        /// <summary>Shows a title and its rows; only the first <see cref="Rows"/> fit.</summary>
        public void Show(string name, IEnumerable<PanelRow> rows, bool deletable)
        {
            gameObject.SetActive(true);
            shown.Clear();
            shown.AddRange(rows);
            if (shown.Count > Rows) shown.RemoveRange(Rows, shown.Count - Rows);
            title.text = name;
            deleteButton.gameObject.SetActive(deletable);
            Refresh();
        }

        public void Hide()
        {
            shown.Clear();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            for (int i = 0; i < Rows; i++)
            {
                bool used = i < shown.Count;
                labels[i].text = used ? shown[i].Label : "";
                values[i].text = used ? Shorten(shown[i].Show()) : "";
                bool steps = used && !shown[i].IsText;
                lessButtons[i].text = steps ? "<" : "";
                moreButtons[i].text = steps ? ">" : "";
            }
            // Meshes are made lazily, and clicks find the buttons by their bounds, possibly in this same frame.
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        }

        const int MaxValueLength = 12;

        /// <summary>
        /// Long values are cut with "...". A value being typed ends with the caret "|", and keeps its end instead,
        /// where the typing is.
        /// </summary>
        static string Shorten(string value)
        {
            if (value.Length <= MaxValueLength) return value;
            if (value.EndsWith("|")) return "..." + value.Substring(value.Length - (MaxValueLength - 3));
            return value.Substring(0, MaxValueLength - 3) + "...";
        }

        public bool Contains(Vector2 world) => gameObject.activeInHierarchy && Over(background, world);

        /// <summary>Which button or value is under a world point, and on which row.</summary>
        public Hit HitAt(Vector2 world, out int row)
        {
            row = -1;
            if (!gameObject.activeInHierarchy) return Hit.None;
            if (deleteButton.gameObject.activeSelf && Over(deleteButton.GetComponent<Renderer>(), world)) return Hit.Delete;
            for (int i = 0; i < shown.Count; i++)
            {
                row = i;
                if (!shown[i].IsText)
                {
                    if (Over(lessButtons[i].GetComponent<Renderer>(), world)) return Hit.Less;
                    if (Over(moreButtons[i].GetComponent<Renderer>(), world)) return Hit.More;
                }
                if (Over(values[i].GetComponent<Renderer>(), world)) return Hit.Value;
            }
            row = -1;
            return Hit.None;
        }

        static bool Over(Renderer shape, Vector2 world)
        {
            if (shape == null || !shape.enabled) return false;
            Bounds bounds = shape.bounds;
            if (bounds.size == Vector3.zero) return false;
            bounds.Expand(HitPaddingUnits * 2f);
            return bounds.Contains(new Vector3(world.x, world.y, bounds.center.z));
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LowercaseUppercase
{
    /// <summary>
    /// The level editor's panel for the selected piece: its name, up to <see cref="Rows"/> settings with arrows to step
    /// them, and a delete button. Text settings are edited by clicking them, which starts typing.
    /// </summary>
    public class LevelEditorPanel : MonoBehaviour
    {
        public const int Rows = 4;
        const float HitPaddingUnits = 0.08f;

        public enum Hit { None, Less, More, Value, Delete }

        public SpriteRenderer background;
        public TMP_Text title;
        public TMP_Text[] labels;
        public TMP_Text[] values;
        public TMP_Text[] lessButtons;
        public TMP_Text[] moreButtons;
        public TMP_Text deleteButton;

        public CustomPiece Piece { get; private set; }
        public IReadOnlyList<PieceProperty> Properties => properties;

        readonly List<PieceProperty> properties = new List<PieceProperty>();

        /// <summary>Shows a piece's settings. A null piece shows only the title, for the letter's start.</summary>
        public void Show(CustomPiece piece, string name)
        {
            gameObject.SetActive(true);
            Piece = piece;
            properties.Clear();
            if (piece != null) properties.AddRange(PieceProperties.For(piece));
            if (properties.Count > Rows) properties.RemoveRange(Rows, properties.Count - Rows);
            title.text = name;
            deleteButton.gameObject.SetActive(piece != null);
            Refresh();
        }

        public void Hide()
        {
            Piece = null;
            properties.Clear();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            for (int i = 0; i < Rows; i++)
            {
                bool used = i < properties.Count;
                labels[i].text = used ? properties[i].Label : "";
                values[i].text = used ? Shorten(properties[i].Show(Piece)) : "";
                bool steps = used && !properties[i].IsText;
                lessButtons[i].text = steps ? "<" : "";
                moreButtons[i].text = steps ? ">" : "";
            }
            // Meshes are made lazily, and clicks find the buttons by their bounds, possibly in this same frame.
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        }

        const int MaxValueLength = 12;

        static string Shorten(string value) =>
            value.Length <= MaxValueLength ? value : value.Substring(0, MaxValueLength - 3) + "...";

        public bool Contains(Vector2 world) => gameObject.activeInHierarchy && Over(background, world);

        /// <summary>Which button or value is under a world point, and on which row.</summary>
        public Hit HitAt(Vector2 world, out int row)
        {
            row = -1;
            if (!gameObject.activeInHierarchy) return Hit.None;
            if (deleteButton.gameObject.activeSelf && Over(deleteButton.GetComponent<Renderer>(), world)) return Hit.Delete;
            for (int i = 0; i < properties.Count; i++)
            {
                row = i;
                if (!properties[i].IsText)
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

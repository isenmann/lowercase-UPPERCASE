using System.Collections.Generic;

namespace LowercaseUppercase
{
    /// <summary>
    /// The level editor's undo and redo: the level as JSON after each finished change. A drag or a typed word is one
    /// change, because the editor records only once it is done.
    /// </summary>
    public sealed class EditorHistory
    {
        public const int Limit = 200;

        readonly List<string> undo = new List<string>();
        readonly List<string> redo = new List<string>();

        public string Current { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        public EditorHistory(string json) => Current = json;

        /// <summary>Remembers the level as it is now, if it changed. A new change forgets what could be redone.</summary>
        public bool Record(string json)
        {
            if (json == Current) return false;
            undo.Add(Current);
            if (undo.Count > Limit) undo.RemoveAt(0);
            redo.Clear();
            Current = json;
            return true;
        }

        /// <summary>The level before the last change, or null when there is nothing to undo.</summary>
        public string Undo() => Move(undo, redo);

        /// <summary>The level after the last undone change, or null when there is nothing to redo.</summary>
        public string Redo() => Move(redo, undo);

        string Move(List<string> from, List<string> to)
        {
            if (from.Count == 0) return null;
            to.Add(Current);
            Current = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            return Current;
        }
    }
}

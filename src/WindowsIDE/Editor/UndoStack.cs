using System.Collections.Generic;

namespace WindowsIDE.Editor
{
    internal enum EditKind
    {
        Insert,
        Delete
    }

    internal sealed class EditAction
    {
        public EditKind Kind;
        public int Line;
        public int Column;
        public string Text;

        public EditAction(EditKind kind, int line, int column, string text)
        {
            this.Kind = kind;
            this.Line = line;
            this.Column = column;
            this.Text = (text == null) ? "" : text;
        }
    }

    internal sealed class UndoUnit
    {
        public readonly List<EditAction> Actions;

        public UndoUnit()
        {
            this.Actions = new List<EditAction>();
        }
    }

    /// <summary>
    /// TextBuffer に対する Undo / Redo スタック。
    /// </summary>
    public sealed class UndoStack
    {
        private readonly List<UndoUnit> units;
        private int index;
        private UndoUnit openUnit;

        /// <summary>
        /// 空の Undo スタックを作る。
        /// </summary>
        public UndoStack()
        {
            this.units = new List<UndoUnit>();
            this.index = 0;
            this.openUnit = null;
        }

        /// <summary>Undo できるなら true。</summary>
        public bool CanUndo
        {
            get { return this.index > 0; }
        }

        /// <summary>Redo できるなら true。</summary>
        public bool CanRedo
        {
            get { return this.index < this.units.Count; }
        }

        /// <summary>
        /// 履歴を捨てる。
        /// </summary>
        public void Clear()
        {
            this.units.Clear();
            this.index = 0;
            this.openUnit = null;
        }

        /// <summary>
        /// 複数操作を 1 つの Undo 単位にまとめる開始。
        /// </summary>
        public void BeginCompound()
        {
            this.CommitOpen();
            this.openUnit = new UndoUnit();
        }

        /// <summary>
        /// 複合単位を確定する。
        /// </summary>
        public void EndCompound()
        {
            if (this.openUnit == null)
            {
                return;
            }

            UndoUnit unit = this.openUnit;
            this.openUnit = null;
            if (unit.Actions.Count == 0)
            {
                return;
            }

            this.PushUnit(unit);
        }

        /// <summary>
        /// 挿入を記録する。text はバッファに入れた文字列。
        /// </summary>
        public void RecordInsert(int line, int column, string text)
        {
            if (text == null || text.Length == 0)
            {
                return;
            }

            this.DropRedo();
            if (this.openUnit != null)
            {
                this.openUnit.Actions.Add(new EditAction(EditKind.Insert, line, column, text));
                return;
            }

            if (this.TryCoalesceInsert(line, column, text))
            {
                return;
            }

            UndoUnit unit = new UndoUnit();
            unit.Actions.Add(new EditAction(EditKind.Insert, line, column, text));
            this.PushUnit(unit);
        }

        /// <summary>
        /// 削除を記録する。text は削除された文字列。
        /// </summary>
        public void RecordDelete(int line, int column, string text)
        {
            if (text == null || text.Length == 0)
            {
                return;
            }

            this.DropRedo();
            if (this.openUnit != null)
            {
                this.openUnit.Actions.Add(new EditAction(EditKind.Delete, line, column, text));
                return;
            }

            UndoUnit unit = new UndoUnit();
            unit.Actions.Add(new EditAction(EditKind.Delete, line, column, text));
            this.PushUnit(unit);
        }

        /// <summary>
        /// 直前の変更を取り消す。
        /// </summary>
        public void Undo(TextBuffer buffer)
        {
            this.CommitOpen();
            if (!this.CanUndo || buffer == null)
            {
                return;
            }

            this.index--;
            UndoUnit unit = this.units[this.index];
            for (int i = unit.Actions.Count - 1; i >= 0; i--)
            {
                ApplyInverse(buffer, unit.Actions[i]);
            }
        }

        /// <summary>
        /// 取り消した変更をやり直す。
        /// </summary>
        public void Redo(TextBuffer buffer)
        {
            this.CommitOpen();
            if (!this.CanRedo || buffer == null)
            {
                return;
            }

            UndoUnit unit = this.units[this.index];
            for (int i = 0; i < unit.Actions.Count; i++)
            {
                ApplyForward(buffer, unit.Actions[i]);
            }

            this.index++;
        }

        private void CommitOpen()
        {
            if (this.openUnit == null)
            {
                return;
            }

            UndoUnit unit = this.openUnit;
            this.openUnit = null;
            if (unit.Actions.Count > 0)
            {
                this.PushUnit(unit);
            }
        }

        private void PushUnit(UndoUnit unit)
        {
            this.units.Add(unit);
            this.index = this.units.Count;
        }

        private void DropRedo()
        {
            if (this.index < this.units.Count)
            {
                this.units.RemoveRange(this.index, this.units.Count - this.index);
            }
        }

        private bool TryCoalesceInsert(int line, int column, string text)
        {
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
            {
                return false;
            }

            if (this.index <= 0 || this.index != this.units.Count)
            {
                return false;
            }

            UndoUnit last = this.units[this.units.Count - 1];
            if (last.Actions.Count != 1)
            {
                return false;
            }

            EditAction action = last.Actions[0];
            if (action.Kind != EditKind.Insert)
            {
                return false;
            }

            if (action.Text.IndexOf('\n') >= 0 || action.Text.IndexOf('\r') >= 0)
            {
                return false;
            }

            int endCol = action.Column + action.Text.Length;
            if (action.Line != line || endCol != column)
            {
                return false;
            }

            action.Text = action.Text + text;
            return true;
        }

        private static void ApplyForward(TextBuffer buffer, EditAction action)
        {
            if (action.Kind == EditKind.Insert)
            {
                buffer.Insert(action.Line, action.Column, action.Text);
            }
            else
            {
                BufferPoint start = new BufferPoint(action.Line, action.Column);
                BufferPoint end = EndPoint(buffer, start, action.Text);
                buffer.Delete(start, end);
            }
        }

        private static void ApplyInverse(TextBuffer buffer, EditAction action)
        {
            if (action.Kind == EditKind.Insert)
            {
                BufferPoint start = new BufferPoint(action.Line, action.Column);
                BufferPoint end = EndPoint(buffer, start, action.Text);
                buffer.Delete(start, end);
            }
            else
            {
                buffer.Insert(action.Line, action.Column, action.Text);
            }
        }

        private static BufferPoint EndPoint(TextBuffer buffer, BufferPoint start, string text)
        {
            string normalized = (text == null) ? "" : text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] parts = normalized.Split('\n');
            if (parts.Length == 1)
            {
                return new BufferPoint(start.Line, start.Column + parts[0].Length);
            }

            int line = start.Line + parts.Length - 1;
            int col = parts[parts.Length - 1].Length;
            return buffer.Clamp(new BufferPoint(line, col));
        }
    }
}

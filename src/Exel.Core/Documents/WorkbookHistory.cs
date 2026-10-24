namespace Exel.Core;

public sealed record DocumentState(Workbook Book, string Selection, long Revision);

// Sparse dictionaries are copied; immutable formula strings are shared between snapshots.
public sealed class WorkbookHistory
{
    private readonly List<DocumentState> undo = [];
    private readonly Stack<DocumentState> redo = new();
    private long revision, sequence, savedRevision;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public bool IsDirty => revision != savedRevision;
    public void Record(Workbook book, string selection)
    {
        undo.Add(new(book.Snapshot(), selection, revision));
        if (undo.Count > 40) undo.RemoveAt(0);
        redo.Clear(); revision = ++sequence;
    }
    public DocumentState? Undo(Workbook book, string selection)
    {
        if (!CanUndo) return null;
        redo.Push(new(book.Snapshot(), selection, revision));
        var state = undo[^1]; undo.RemoveAt(undo.Count - 1); revision = state.Revision;
        return state;
    }
    public DocumentState? Redo(Workbook book, string selection)
    {
        if (!CanRedo) return null;
        undo.Add(new(book.Snapshot(), selection, revision));
        var state = redo.Pop(); revision = state.Revision; return state;
    }
    public void MarkSaved() => savedRevision = revision;
    public void Reset() { undo.Clear(); redo.Clear(); revision = sequence = savedRevision = 0; }
}


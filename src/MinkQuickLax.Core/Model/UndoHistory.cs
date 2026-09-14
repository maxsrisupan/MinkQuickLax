namespace MinkQuickLax.Core.Model;

/// <summary>Undo/redo over immutable snapshots (edit mode keeps whole <see cref="AppConfig"/> values).</summary>
public sealed class UndoHistory<T>(int capacity = 200)
    where T : class
{
    private readonly LinkedList<T> _undo = new();
    private readonly Stack<T> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records the state before a change. A new change clears the redo history.</summary>
    public void Push(T before)
    {
        _undo.AddLast(before);
        if (_undo.Count > capacity)
        {
            _undo.RemoveFirst();
        }
        _redo.Clear();
    }

    public bool TryUndo(T current, out T previous)
    {
        if (_undo.Last is not { } last)
        {
            previous = current;
            return false;
        }
        _undo.RemoveLast();
        _redo.Push(current);
        previous = last.Value;
        return true;
    }

    public bool TryRedo(T current, out T next)
    {
        if (!_redo.TryPop(out var popped))
        {
            next = current;
            return false;
        }
        _undo.AddLast(current);
        next = popped;
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

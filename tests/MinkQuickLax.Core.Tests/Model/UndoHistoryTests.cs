using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Model;

public sealed class UndoHistoryTests
{
    [Fact]
    public void UndoThenRedo_WalksTheHistory()
    {
        var stack = new UndoHistory<string>();
        stack.Push("a");   // a -> b
        stack.Push("b");   // b -> c

        Assert.True(stack.TryUndo("c", out var back1));
        Assert.Equal("b", back1);
        Assert.True(stack.TryUndo(back1, out var back2));
        Assert.Equal("a", back2);
        Assert.False(stack.TryUndo(back2, out var same));
        Assert.Equal("a", same);

        Assert.True(stack.TryRedo(back2, out var forward1));
        Assert.Equal("b", forward1);
        Assert.True(stack.TryRedo(forward1, out var forward2));
        Assert.Equal("c", forward2);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void NewChange_ClearsRedo()
    {
        var stack = new UndoHistory<string>();
        stack.Push("a");
        stack.TryUndo("b", out _);
        Assert.True(stack.CanRedo);

        stack.Push("a2");

        Assert.False(stack.CanRedo);
        Assert.True(stack.CanUndo);
    }

    [Fact]
    public void Capacity_DropsTheOldest()
    {
        var stack = new UndoHistory<string>(capacity: 2);
        stack.Push("1");
        stack.Push("2");
        stack.Push("3");

        Assert.True(stack.TryUndo("4", out var a));
        Assert.True(stack.TryUndo(a, out var b));
        Assert.False(stack.TryUndo(b, out _));
        Assert.Equal(("3", "2"), (a, b));
    }

    [Fact]
    public void Clear_EmptiesBoth()
    {
        var stack = new UndoHistory<string>();
        stack.Push("a");
        stack.TryUndo("b", out _);

        stack.Clear();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }
}

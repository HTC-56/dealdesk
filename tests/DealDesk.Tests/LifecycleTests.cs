using System;
using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// Lifecycle transition laws. Each law is one [Fact], plain Assert calls, no
/// loops needed — the state machine is a fixed five-state chain.
public sealed class LifecycleTests
{
    [Fact]
    public void Forward_chain_moves_are_allowed()
    {
        Assert.True(Lifecycle.CanMove("draft", "appraised"));
        Assert.True(Lifecycle.CanMove("appraised", "presented"));
        Assert.True(Lifecycle.CanMove("presented", "won"));
    }

    [Fact]
    public void Skipping_steps_is_rejected()
    {
        Assert.False(Lifecycle.CanMove("draft", "won"));
        Assert.False(Lifecycle.CanMove("draft", "presented"));
    }

    [Fact]
    public void Lost_is_reachable_from_any_open_state()
    {
        Assert.True(Lifecycle.CanMove("draft", "lost"));
        Assert.True(Lifecycle.CanMove("appraised", "lost"));
        Assert.True(Lifecycle.CanMove("presented", "lost"));
    }

    [Fact]
    public void Won_and_lost_are_terminal_with_no_next_states()
    {
        Assert.True(Lifecycle.IsTerminal("won"));
        Assert.True(Lifecycle.IsTerminal("lost"));
        Assert.False(Lifecycle.IsTerminal("draft"));
        Assert.Empty(Lifecycle.NextFrom("won"));
    }

    [Fact]
    public void Status_matching_is_trimmed_and_case_insensitive()
    {
        Assert.True(Lifecycle.CanMove("DRAFT", " Appraised "));
        Assert.Equal("appraised", Lifecycle.Canonical(" Appraised "));
    }

    [Fact]
    public void Refuse_returns_null_for_valid_moves_and_non_null_for_invalid_ones()
    {
        Assert.Null(Lifecycle.Refuse("draft", "appraised"));
        Assert.NotNull(Lifecycle.Refuse("draft", "draft"));
        Assert.NotNull(Lifecycle.Refuse("won", "lost"));
        Assert.NotNull(Lifecycle.Refuse("draft", "sold"));
    }
}

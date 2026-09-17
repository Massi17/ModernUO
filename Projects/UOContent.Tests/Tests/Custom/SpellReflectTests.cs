using System;
using Server;
using Server.Custom;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class SpellReflectTests
{
    // 8ms lockstep keeps the wheel and Core.TickCount in sync - same helper already used by
    // MagicShieldTests.cs for its own expiry tests.
    private static void RunFor(long ms)
    {
        var deadline = Core._tickCount + ms;

        while (Core._tickCount - deadline < 0)
        {
            Core._tickCount += 8;
            Timer.Slice(Core._tickCount);
        }
    }

    [Fact]
    public void ApplySetsTheFlag()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        SpellReflect.Apply(m, TimeSpan.FromMinutes(5));

        Assert.True(m.SpellReflectActive);
        Assert.True(SpellReflect.IsActive(m));

        m.Delete();
    }

    [Fact]
    public void ApplyReplacesAnExistingShield_CancellingTheOldExpiryTimer()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        SpellReflect.Apply(m, TimeSpan.FromSeconds(5));
        SpellReflect.Apply(m, TimeSpan.FromMinutes(5)); // re-cast: must not stack or throw

        Assert.True(m.SpellReflectActive);

        // If the first 5-second timer weren't cancelled, it would wrongly clear the
        // second (5-minute) shield here.
        RunFor(6000);
        Assert.True(m.SpellReflectActive);

        m.Delete();
    }

    [Fact]
    public void ClearCancelsTheExpiryTimer()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        SpellReflect.Apply(m, TimeSpan.FromSeconds(5));

        SpellReflect.Clear(m);
        Assert.False(m.SpellReflectActive);

        // Advancing past the original duration must not do anything odd with an
        // already-cancelled timer.
        RunFor(6000);
        Assert.False(m.SpellReflectActive);

        m.Delete();
    }

    [Fact]
    public void ShieldExpiresOnItsOwn_WithoutBeingConsumed()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        SpellReflect.Apply(m, TimeSpan.FromSeconds(5));

        Assert.True(m.SpellReflectActive);

        RunFor(6000);

        Assert.False(m.SpellReflectActive);
        Assert.False(SpellReflect.IsActive(m));

        m.Delete();
    }

    [Fact]
    public void ClearIsANoOp_WhenThereIsNoShield()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        SpellReflect.Clear(m); // must not throw

        Assert.False(m.SpellReflectActive);

        m.Delete();
    }
}

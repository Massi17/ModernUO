using System;
using Server;
using Server.Custom;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MagicShieldTests
{
    // 8ms lockstep keeps the wheel and Core.TickCount in sync — same helper as
    // Tests/Mobiles/AI/FamiliarAITests.cs, reused here for the expiry tests below.
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
    public void ApplySetsThePool()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        MagicShield.Apply(m, 50);

        Assert.Equal(50, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void ApplyReplacesAnExistingShield()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        MagicShield.Apply(m, 50);
        MagicShield.Apply(m, 20);

        Assert.Equal(20, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void AbsorbReducesThePoolAndReturnsZero_WhenDamageIsLessThanThePool()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50);

        var remaining = MagicShield.Absorb(m, 30);

        Assert.Equal(0, remaining);
        Assert.Equal(20, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void AbsorbBleedsThrough_WhenDamageExceedsThePool()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50);

        var remaining = MagicShield.Absorb(m, 70);

        Assert.Equal(20, remaining);
        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void AbsorbIsANoOp_WhenThereIsNoShield()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        var remaining = MagicShield.Absorb(m, 40);

        Assert.Equal(40, remaining);
        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void ClearCancelsTheExpiryTimer()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50, TimeSpan.FromSeconds(5));

        MagicShield.Clear(m);
        Assert.Equal(0, m.MagicShieldAbsorb);

        // Advancing past the original duration must not resurrect/re-clear a stale pool.
        RunFor(6000);
        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void ShieldExpiresOnItsOwn_WithoutBeingHit()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50, TimeSpan.FromSeconds(5));

        Assert.Equal(50, m.MagicShieldAbsorb);

        RunFor(6000);

        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }
}

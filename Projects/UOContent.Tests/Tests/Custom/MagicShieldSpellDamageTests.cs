using System;
using Server;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MagicShieldSpellDamageTests
{
    [Fact]
    public void SimpleImmediateSpellDamage_IsAbsorbed()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.RawStr = 100; // HitsMax = 50 + Str / 2 == 100, so the Hits assignment below isn't clamped
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        SpellHelper.Damage(TimeSpan.Zero, target, 30);

        Assert.Equal(20, target.MagicShieldAbsorb);
        Assert.Equal(100, target.Hits); // fully absorbed, real HP untouched

        target.Delete();
    }

    [Fact]
    public void SimpleImmediateSpellDamage_BleedsThroughWhenItExceedsThePool()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.RawStr = 100; // HitsMax = 50 + Str / 2 == 100, so the Hits assignment below isn't clamped
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        SpellHelper.Damage(TimeSpan.Zero, target, 70);

        Assert.Equal(0, target.MagicShieldAbsorb);
        Assert.Equal(80, target.Hits); // 20 points overflow past the shield

        target.Delete();
    }

    [Fact]
    public void ElementalSpellDamage_IsAbsorbed()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.RawStr = 100; // HitsMax = 50 + Str / 2 == 100, so the Hits assignment below isn't clamped
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        // Real caster, not null: SpellHelper.Damage's elemental overload runs Feint.GetDamageReduction(from, ...)
        // before the shield hook, and that method has a pre-existing (unrelated) null-key crash when `from` is
        // null — see task-3-report.md. Every live call site already supplies a real caster here, so this mirrors
        // actual usage rather than working around the bug.
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();

        // 100% fire, no resistance on a bare test Mobile — full 30 lands as fire damage pre-shield.
        SpellHelper.Damage(TimeSpan.Zero, target, caster, 30, 0, 100, 0, 0, 0);

        Assert.Equal(20, target.MagicShieldAbsorb);
        Assert.Equal(100, target.Hits);

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void MeleeDamage_IsNeverAbsorbed()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.RawStr = 100; // HitsMax = 50 + Str / 2 == 100, so the Hits assignment below isn't clamped
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        target.Damage(30); // direct Mobile.Damage call, the melee/generic path

        Assert.Equal(50, target.MagicShieldAbsorb); // untouched
        Assert.Equal(70, target.Hits);

        target.Delete();
    }
}

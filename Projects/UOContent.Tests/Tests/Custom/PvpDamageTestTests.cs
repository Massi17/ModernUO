using System;
using Server;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PvpDamageTestTests
{
    [Fact]
    public void ToggleEnablesThenDisables()
    {
        var attacker = new Mobile(World.NewMobile);
        attacker.DefaultMobileInit();

        Assert.False(PvpDamageTest.IsEnabled(attacker));

        var enabled = PvpDamageTest.Toggle(attacker);
        Assert.True(enabled);
        Assert.True(PvpDamageTest.IsEnabled(attacker));

        var disabled = PvpDamageTest.Toggle(attacker);
        Assert.False(disabled);
        Assert.False(PvpDamageTest.IsEnabled(attacker));

        attacker.Delete();
    }

    [Fact]
    public void IsEnabledFalseForUntouchedMobile()
    {
        var mobile = new Mobile(World.NewMobile);
        mobile.DefaultMobileInit();

        Assert.False(PvpDamageTest.IsEnabled(mobile));

        mobile.Delete();
    }

    [Fact]
    public void IsEnabledNullIsFalse()
    {
        Assert.False(PvpDamageTest.IsEnabled(null));
    }

    [Fact]
    public void ReportDoesNotThrow_WhenDisabled()
    {
        var attacker = new Mobile(World.NewMobile);
        attacker.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        PvpDamageTest.Report(attacker, target, 20, 30);

        attacker.Delete();
        target.Delete();
    }

    [Fact]
    public void ReportDoesNotThrow_WhenEnabled()
    {
        var attacker = new Mobile(World.NewMobile);
        attacker.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        PvpDamageTest.Toggle(attacker);
        PvpDamageTest.Report(attacker, target, 20, 30);

        attacker.Delete();
        target.Delete();
    }

    [Fact]
    public void ReportDoesNotThrow_WhenAttackerIsNull()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        PvpDamageTest.Report(null, target, 20, 30);

        target.Delete();
    }

    [Fact]
    public void ReportDoesNotThrow_WhenTargetIsNull()
    {
        var attacker = new Mobile(World.NewMobile);
        attacker.DefaultMobileInit();

        PvpDamageTest.Toggle(attacker);
        PvpDamageTest.Report(attacker, null, 20, 30);

        attacker.Delete();
    }

    [Fact]
    public void EnabledDuringSpellDamage_DoesNotAlterShieldAbsorption()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.RawStr = 100; // HitsMax = 50 + Str / 2 == 100, so the Hits assignment below isn't clamped
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        PvpDamageTest.Toggle(caster);
        SpellHelper.Damage(TimeSpan.Zero, target, caster, 30, 0, 100, 0, 0, 0);

        // Same outcome as MagicShieldSpellDamageTests.ElementalSpellDamage_IsAbsorbed - proves the new
        // PvpDamageTest.Report() call doesn't change the existing shield-absorption arithmetic.
        Assert.Equal(20, target.MagicShieldAbsorb);
        Assert.Equal(100, target.Hits);

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void EnabledDuringMeleeDamage_DoesNotAlterHits()
    {
        var attacker = new Mobile(World.NewMobile);
        attacker.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.RawStr = 100;
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        PvpDamageTest.Toggle(attacker);
        target.Damage(30, attacker); // direct Mobile.Damage call, the melee/generic path - never absorbed

        Assert.Equal(50, target.MagicShieldAbsorb); // untouched, matches MeleeDamage_IsNeverAbsorbed
        Assert.Equal(70, target.Hits);

        attacker.Delete();
        target.Delete();
    }
}

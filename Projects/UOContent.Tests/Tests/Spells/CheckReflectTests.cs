using System;
using Server;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class CheckReflectTests
{
    [Fact]
    public void NoShields_ReturnsNone_NoSwap()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.None, result);
        Assert.Same(originalTarget, target);

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void TargetHasShield_ReturnsReflected_SwapsAndConsumesTheShield()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.Reflected, result);
        Assert.Same(caster, target); // swapped: the effect now lands on the original caster
        Assert.False(originalTarget.SpellReflectActive); // shield consumed

        caster.Delete();
        originalTarget.Delete();
    }

    [Fact]
    public void BothHaveShields_ReturnsVanished_ClearsBothShields_NoSwap()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(caster, TimeSpan.FromMinutes(5));
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.Vanished, result);
        Assert.False(caster.SpellReflectActive);
        Assert.False(originalTarget.SpellReflectActive);
        Assert.Same(originalTarget, target); // no swap on the vanish path

        caster.Delete();
        originalTarget.Delete();
    }

    [Fact]
    public void CasterPierces_ReturnsNone_StillConsumesTargetShield_NoSwap()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.PiercesSpellReflect = true;
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.None, result);
        Assert.Same(originalTarget, target); // lands on the original target, not swapped
        Assert.False(originalTarget.SpellReflectActive); // shield still breaks

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void PiercingWithNoShieldOnTarget_ReturnsNone_IsANoOp()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.PiercesSpellReflect = true;
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.None, result);
        Assert.Same(originalTarget, target);

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void LegacyMagicDamageAbsorb_StillReflects_WhenNoNewShieldIsInvolved()
    {
        // Regression guard: MeerCaptain (Mobiles/Monsters/LBR/Meers/MeerCaptain.cs) sets
        // MagicDamageAbsorb directly as its own creature ability - this path must keep working.
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.MagicDamageAbsorb = 100;

        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.Reflected, result);
        Assert.Same(caster, target);

        caster.Delete();
        target.Delete();
    }
}

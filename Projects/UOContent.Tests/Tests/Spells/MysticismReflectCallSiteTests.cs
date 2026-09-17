using System;
using Server;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MysticismReflectCallSiteTests
{
    [Theory]
    [InlineData(6)] // BombardSpell's hardcoded circle
    [InlineData(2)] // EagleStrikeSpell's hardcoded circle
    public void DoubleShield_ReturnsVanished_ForTheseSpellsCircles(int circle)
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(caster, TimeSpan.FromMinutes(5));
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var result = SpellHelper.CheckReflect(circle, caster, ref target);

        Assert.Equal(ReflectResult.Vanished, result);

        caster.Delete();
        target.Delete();
    }
}

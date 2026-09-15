using Server.Spells;
using Server.Spells.First;
using Server.Spells.Seventh;
using Xunit;

namespace Server.Tests.Spells;

[Collection("Sequential UOContent Tests")]
public class CastInterruptRecastTests
{
    [Fact]
    public void UsesDeferredCast_TrueForTargetFirst_FalseOtherwise()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();

        var flameStrike = new FlameStrikeSpell(caster);
        var magicArrow = new MagicArrowSpell(caster);

        Assert.True(flameStrike.UsesDeferredCast);
        Assert.False(magicArrow.UsesDeferredCast);

        caster.Delete();
    }

    [Fact]
    public void Cast_WhileAnotherSpellIsCasting_DoesNotBlockAndShowsCursorImmediately()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;

        var b = new MagicArrowSpell(caster);
        var result = b.Cast();

        Assert.True(result);
        Assert.Same(b, caster.Spell);
        Assert.Equal(SpellState.Casting, b.State);
        Assert.IsType<SpellTarget<Mobile>>(caster.Target);

        // A hasn't been touched yet - it's only fizzled once B's own target click commits
        // (Task 4), not just from B being pressed.
        Assert.Equal(SpellState.Casting, a.State);
        Assert.Equal(manaBeforeCast, caster.Mana);

        caster.Delete();
    }
}

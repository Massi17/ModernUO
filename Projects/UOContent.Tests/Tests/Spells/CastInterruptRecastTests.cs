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

    [Fact]
    public void ClickingInterruptingSpellsTarget_DefersResolutionInsteadOfResolvingImmediately()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var b = new MagicArrowSpell(caster);
        b.Cast();

        var spellTarget = (SpellTarget<Mobile>)caster.Target;
        spellTarget.CheckLOS = false; // LOS needs real map tile data, unrelated to this invariant
        spellTarget.Invoke(caster, target);

        // B (not a TargetFirst spell) still deferred its resolution to phase 2, because it's
        // interrupting A.
        Assert.True(b.TargetFirstCommitted);

        // A's fizzle-charge fires right here, once B's deferred resolution begins (Task 4).
        Assert.Equal(SpellState.None, a.State);

        b.Disturb(DisturbType.Kill); // stop the pending cast timer
        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void InterruptedSpell_FizzlesAndChargesOnlyWhenNewSpellsTargetCommits()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;
        var aManaCost = a.ScaleMana(a.GetMana());

        var b = new MagicArrowSpell(caster);
        b.Cast();

        // Still nothing charged just from pressing B - matches Task 2's gate change.
        Assert.Equal(SpellState.Casting, a.State);
        Assert.Equal(manaBeforeCast, caster.Mana);

        var spellTarget = (SpellTarget<Mobile>)caster.Target;
        spellTarget.CheckLOS = false;
        spellTarget.Invoke(caster, target);

        // Clicking B's target commits it - A fizzles and pays right here.
        Assert.Equal(SpellState.None, a.State);
        Assert.Equal(manaBeforeCast - aManaCost, caster.Mana);

        // Defensive fix: disturbing A must not wipe out Caster.Spell now that it points at B.
        Assert.Same(b, caster.Spell);
        Assert.True(b.TargetFirstCommitted);

        b.Disturb(DisturbType.Kill); // stop the pending cast timer
        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void ChainedInterrupt_SettlesPendingObligationImmediately()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;
        var aManaCost = a.ScaleMana(a.GetMana());

        var b = new MagicArrowSpell(caster);
        b.Cast(); // B interrupts A - A remembered, not yet charged

        var c = new MagicArrowSpell(caster);
        c.Cast(); // C interrupts B before B's own target was ever clicked

        // B's own click will never come now - its pending obligation to fizzle A must be
        // settled immediately once C actually commits, not left to evaporate.
        Assert.Equal(SpellState.None, a.State);
        Assert.Equal(manaBeforeCast - aManaCost, caster.Mana);
        Assert.Same(c, caster.Spell);

        c.Disturb(DisturbType.Kill); // stop the pending cast timer
        caster.Delete();
    }
}

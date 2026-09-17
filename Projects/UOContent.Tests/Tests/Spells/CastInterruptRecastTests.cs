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
        var clumsy = new ClumsySpell(caster);

        Assert.True(flameStrike.UsesDeferredCast);
        Assert.False(clumsy.UsesDeferredCast);

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

        var a = new ClumsySpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;

        var b = new ClumsySpell(caster);
        var result = b.Cast();

        Assert.True(result);
        Assert.Same(b, caster.Spell);
        Assert.Equal(SpellState.Casting, b.State);
        Assert.IsType<SpellTarget<Mobile>>(caster.Target);

        // A hasn't been touched yet - it's only fizzled once B's own target click commits,
        // not just from B being pressed.
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

        var a = new ClumsySpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var b = new ClumsySpell(caster);
        b.Cast();

        var spellTarget = (SpellTarget<Mobile>)caster.Target;
        spellTarget.CheckLOS = false; // LOS needs real map tile data, unrelated to this invariant
        spellTarget.Invoke(caster, target);

        // B (not a TargetFirst spell) still deferred its resolution to phase 2, because it's
        // interrupting A.
        Assert.True(b.TargetFirstCommitted);

        // A's fizzle-charge fires right here, once B's deferred resolution begins.
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

        var a = new ClumsySpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;
        var aManaCost = a.ScaleMana(a.GetMana());

        var b = new ClumsySpell(caster);
        b.Cast();

        // Still nothing charged just from pressing B.
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

        var a = new ClumsySpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;
        var aManaCost = a.ScaleMana(a.GetMana());

        var b = new ClumsySpell(caster);
        b.Cast(); // B interrupts A - A remembered, not yet charged
        var bManaCost = b.ScaleMana(b.GetMana());

        var c = new ClumsySpell(caster);
        c.Cast(); // C interrupts B before B's own target was ever clicked

        // B's own click will never come now - its pending obligation to fizzle A, AND B's own
        // charge for interrupting A in the first place, must both be settled immediately once
        // C actually commits, not left to evaporate. Every spell that gets superseded pays -
        // A directly here, B because it was itself about to be torn down the same way.
        Assert.Equal(SpellState.None, a.State);
        Assert.Equal(SpellState.None, b.State);
        Assert.Equal(manaBeforeCast - aManaCost - bManaCost, caster.Mana);
        Assert.Same(c, caster.Spell);

        c.Disturb(DisturbType.Kill); // stop the pending cast timer
        caster.Delete();
    }

    [Fact]
    public void InterruptingSpellHurtBeforeItsOwnClick_DoesNothing()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var a = new ClumsySpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;

        var b = new ClumsySpell(caster);
        b.Cast(); // B interrupts A - A remembered, not yet charged

        // B itself takes a plain hit before ever clicking its own target. Phase 1 isn't really
        // "casting" yet - no mantra, no mana/reagents at stake for B - so a hit here must be a
        // pure no-op: B keeps its cursor, and A's pending fizzle-and-charge obligation stays
        // pending right along with it, exactly as if nothing had happened. It only gets settled
        // when B itself actually resolves one way or another (its own click, or a disturb type
        // other than a plain phase-1 Hurt).
        b.Disturb(DisturbType.Hurt, false, true);

        Assert.Equal(SpellState.Casting, b.State);
        Assert.Equal(SpellState.Casting, a.State);
        Assert.Equal(manaBeforeCast, caster.Mana);
        Assert.Same(b, caster.Spell);

        b.Disturb(DisturbType.Kill); // now actually settle both
        caster.Delete();
    }

    [Fact]
    public void InstantResolveSpell_StillChargesTheInterruptedSpellEvenThoughItFizzlesItself()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var a = new ClumsySpell(caster) { State = SpellState.Casting };
        caster.Spell = a;

        var manaBeforeCast = caster.Mana;
        var aManaCost = a.ScaleMana(a.GetMana());

        var b = new ReactiveArmorSpell(caster);
        var result = b.Cast(); // B interrupts A, but B itself never shows a cursor

        Assert.True(result);
        Assert.Equal(SpellState.None, a.State);
        Assert.Equal(manaBeforeCast - aManaCost, caster.Mana);

        caster.Delete();
    }

    [Fact]
    public void TargetFirstSpell_StaysFreeWhenSupersededByAnotherCast()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.RawInt = 100;
        caster.Mana = 100;
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var a = new FlameStrikeSpell(caster);
        a.Cast(); // A shows its own free, uncommitted cursor - not interrupting anything

        var manaBeforeCast = caster.Mana;

        var b = new ClumsySpell(caster);
        b.Cast(); // B supersedes A's cursor

        // A was never committed to anything of its own - stays free, exactly like the
        // original target-first-casting spec guarantees, regardless of what superseded it.
        Assert.Equal(SpellState.None, a.State);
        Assert.Equal(manaBeforeCast, caster.Mana);

        b.Disturb(DisturbType.Kill); // stop the pending cast timer
        caster.Delete();
    }
}

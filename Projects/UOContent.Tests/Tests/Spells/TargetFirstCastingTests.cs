using Server.Spells;
using Server.Spells.Seventh;
using Server.Targeting;
using Xunit;

namespace Server.Tests.Spells;

[Collection("Sequential UOContent Tests")]
public class TargetFirstCastingTests
{
    // Target.Invoke() calls OnTarget() and then unconditionally calls OnTargetFinish() on the
    // very next line, in the same synchronous call stack. For a TargetFirst spell, OnTarget()
    // only *schedules* the phase-2 cast-delay timer, so a SpellTarget that ran FinishSequence()
    // from OnTargetFinish() tore the spell down (State -> None, Caster.Spell -> null) before the
    // timer could ever tick - the timer's own guard then failed silently and the cast did
    // nothing at all. The spell must still be live and casting the instant Invoke() returns.
    [Fact]
    public void TargetFirstSpell_StaysCastingAfterTargetInvoke()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        // Phase 1 state, as Cast() leaves it right before OnCast() shows the cursor.
        var spell = new FlameStrikeSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        // LOS needs real map tile data, which is not guaranteed in the test host; the invariant
        // under test is unrelated to it.
        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Harmful) { CheckLOS = false };
        caster.Target = spellTarget;

        spellTarget.Invoke(caster, target); // the player clicks a valid target

        Assert.True(spell.TargetFirstCommitted); // phase 2 actually started
        Assert.Same(spell, caster.Spell);        // not torn down by OnTargetFinish
        Assert.Equal(SpellState.Casting, spell.State);

        spell.Disturb(DisturbType.Kill); // stops the pending cast timer
        caster.Delete();
        target.Delete();
    }
}

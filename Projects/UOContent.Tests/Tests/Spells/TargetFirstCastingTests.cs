using Server.Items;
using Server.Spells;
using Server.Spells.First;
using Server.Spells.Fourth;
using Server.Spells.Seventh;
using Server.Spells.Third;
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

    // Phase 1 (cursor up, target not yet picked) is State.Casting for a deferred-cast spell by
    // construction, but nothing has actually been committed yet - no mantra, no mana/reagents at
    // stake. A plain damage hit at this point must not disturb anything: not the state, not the
    // cursor. This mirrors what already happens for a normal (non-deferred) spell at the
    // equivalent moment, where the state is State.Sequencing and OnCasterHurt's IsCasting check
    // never even fires.
    [Fact]
    public void TargetFirstSpell_HurtDuringPhase1_DoesNothing()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.Player = true;

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var spell = new FlameStrikeSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Harmful) { CheckLOS = false };
        caster.Target = spellTarget;

        spell.OnCasterHurt();

        Assert.Same(spell, caster.Spell);
        Assert.Equal(SpellState.Casting, spell.State);
        Assert.Same(spellTarget, caster.Target);

        spell.Disturb(DisturbType.Kill); // stops the pending cursor/timeout cleanly
        caster.Delete();
    }

    // Ordinary spells never decoupled BlocksWeaponSwing from BlocksMovement - the default must
    // keep mirroring it exactly, or every non-TargetFirst spell's weapon-swing gating changes.
    [Fact]
    public void BlocksWeaponSwing_DefaultsToMirrorBlocksMovement()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();

        var spell = new ClumsySpell(caster);

        Assert.Equal(spell.BlocksMovement, spell.BlocksWeaponSwing); // both false: not casting

        spell.State = SpellState.Casting;
        Assert.Equal(spell.BlocksMovement, spell.BlocksWeaponSwing); // both true: casting

        caster.Delete();
    }

    // Flame Strike deliberately decouples the two: movable throughout (BlocksMovement is always
    // false), but weapon swings must stay allowed during phase 1 (still just aiming) and only
    // lock out once phase 2 actually commits.
    [Fact]
    public void FlameStrike_BlocksWeaponSwing_OnlyOncePhase2Commits()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new FlameStrikeSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.False(spell.BlocksMovement);    // movable throughout, by design
        Assert.False(spell.BlocksWeaponSwing); // phase 1: not committed yet, swings still allowed

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Harmful) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target); // click commits phase 2

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing); // phase 2: locked out until fizzle or hit

        spell.Disturb(DisturbType.Kill); // stops the pending cast timer
        caster.Delete();
        target.Delete();
    }

    // NextCombatTime reset on phase-2 commit (BeginTargetFirstDelay) isn't covered by an
    // automated test: it needs a real equipped BaseWeapon, whose Layer is resolved from
    // ItemData.Quality (client tile data) - not reliably available in this test host, the same
    // limitation already noted above for LOS. Covered by manual in-game verification instead.

    // Confirms the target-first pattern generalizes to an ordinary damage spell, not just
    // Flame Strike (which uses the pattern's own opt-in deferred-cost variant).
    [Fact]
    public void MagicArrow_BlocksWeaponSwing_OnlyOncePhase2Commits()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new MagicArrowSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing); // phase 1: not committed yet

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Harmful) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target); // click commits phase 2

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing); // phase 2: locked out until fizzle or hit

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        target.Delete();
    }

    // Confirms target-first works for a beneficial-Mobile spell, not just harmful ones -
    // ValidateTargetFirst uses CanBeBeneficial here instead of CanBeHarmful.
    [Fact]
    public void Heal_TargetFirst_AcceptsABeneficialTarget()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new HealSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing);

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Beneficial) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target);

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing);

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        target.Delete();
    }

    // Proves the generic target-first engine (SpellTarget<T>'s phase-1/phase-2 machinery)
    // actually works for T = Item, not just T = Mobile - this is the first spell in this
    // repo to exercise TargetFirst against an Item target.
    [Fact]
    public void MagicLock_TargetFirst_AcceptsAnItemTarget()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var chest = new WoodenChest();
        chest.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new MagicLockSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing);

        var spellTarget = new SpellTarget<Item>(spell) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, chest);

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing);

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        chest.Delete();
    }

    // Proves the generic target-first engine works for T = IPoint3D (a bare ground click),
    // not just T = Mobile or T = Item.
    [Fact]
    public void FireField_TargetFirst_AcceptsAGroundTarget()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var spell = new FireFieldSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing);

        var spellTarget = new SpellTarget<IPoint3D>(spell, allowGround: true) { CheckLOS = false };
        caster.Target = spellTarget;
        // A ground click reaches Target.Invoke() as a LandTarget, never a bare Point3D - that's
        // the only object type the engine's own switch in Target.Invoke() routes to the
        // AllowGround branch of CanTarget().
        spellTarget.Invoke(caster, new LandTarget(new Point3D(1001, 1000, 0), Map.Felucca));

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing);

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
    }
}

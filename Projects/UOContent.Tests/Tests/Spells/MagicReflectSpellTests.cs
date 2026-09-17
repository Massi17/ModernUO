using Server.Spells;
using Server.Spells.Fifth;
using Server.Targeting;
using Xunit;

namespace Server.Tests.Spells;

[Collection("Sequential UOContent Tests")]
public class MagicReflectSpellTests
{
    // Mirrors FlameStrike_BlocksWeaponSwing_OnlyOncePhase2Commits in TargetFirstCastingTests.cs -
    // same wiring, different spell, confirming the existing target-first mechanism generalizes.
    [Fact]
    public void IsTargetFirst_MovableThroughout_WeaponSwingBlockedOnlyAfterCommit()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.Player = true;

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new MagicReflectSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing); // phase 1: not committed yet

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.None) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target); // click commits phase 2

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing); // phase 2: locked out until fizzle or hit

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void ValidateTargetFirst_AcceptsOnlyPlayerMobiles()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var player = new Mobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        var creature = new Mobile(World.NewMobile);
        creature.DefaultMobileInit();
        creature.Player = false;

        var spell = new MagicReflectSpell(caster);

        Assert.True(spell.ValidateTargetFirst(player));
        Assert.False(spell.ValidateTargetFirst(creature));
        Assert.False(spell.ValidateTargetFirst("not a mobile"));

        caster.Delete();
        player.Delete();
        creature.Delete();
    }
}

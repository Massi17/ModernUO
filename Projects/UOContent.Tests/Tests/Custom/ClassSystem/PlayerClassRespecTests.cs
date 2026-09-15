using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassRespecTests
{
    private static PlayerMobile CreateEvolvedPlayer(int exp, int honor, string evolutionId = "TestAlpha")
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        PlayerClassAssignment.AssignClass(pm, "Test", out _);
        PlayerClassCurrency.AwardExp(pm, exp);
        PlayerClassCurrency.AwardHonor(pm, honor);
        PlayerClassEvolution.TryChooseEvolution(pm, evolutionId, out _);

        return pm;
    }

    [Fact]
    public void RespecEvolutionRefundsHalfOfSpentCurrencyAndClearsUnlockedAbilities()
    {
        var pm = CreateEvolvedPlayer(5000, 0);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", 0, out _); // -100 Exp
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_2", 0, out _); // -200 Exp

        var context = PlayerClassSystem.GetContext(pm);
        Assert.Equal(4700, context.ExpBalance);

        Assert.True(PlayerClassRespec.RespecEvolution(pm, "TestBeta", out var failureReason));
        Assert.Null(failureReason);

        Assert.Equal("TestBeta", context.EvolutionId);
        Assert.Empty(context.UnlockedAbilityIds);
        // 50% of the 300 Exp spent (100 + 200) is refunded
        Assert.Equal(4850, context.ExpBalance);
        Assert.Equal(5000, context.ExpLifetime);

        pm.Delete();
    }

    [Fact]
    public void RespecEvolutionFailsWithoutAnExistingEvolution()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        Assert.False(PlayerClassRespec.RespecEvolution(pm, "TestAlpha", out var reason));
        Assert.Contains("no evolution", reason);

        pm.Delete();
    }

    [Fact]
    public void RespecEvolutionFailsForUnknownEvolutionId()
    {
        var pm = CreateEvolvedPlayer(5000, 0);

        Assert.False(PlayerClassRespec.RespecEvolution(pm, "NoSuchEvolution", out var reason));
        Assert.Contains("No evolution", reason);

        pm.Delete();
    }

    [Fact]
    public void RespecClassClearsEvolutionAndReappliesSkillCapsForTheNewClass()
    {
        var pm = CreateEvolvedPlayer(5000, 0);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", 0, out _);

        Assert.True(PlayerClassRespec.RespecClass(pm, "Test", out var failureReason));
        Assert.Null(failureReason);

        var context = PlayerClassSystem.GetContext(pm);
        Assert.Equal("Test", context.ClassId);
        Assert.Null(context.EvolutionId);
        Assert.Empty(context.UnlockedAbilityIds);
        Assert.Equal(100.0, pm.Skills[SkillName.Swords].Cap);
        Assert.Equal(0.0, pm.Skills[SkillName.Magery].Cap);

        pm.Delete();
    }

    [Fact]
    public void RespecClassFailsWithoutAnExistingClass()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        Assert.False(PlayerClassRespec.RespecClass(pm, "Test", out var reason));
        Assert.Contains("no class", reason);

        pm.Delete();
    }
}

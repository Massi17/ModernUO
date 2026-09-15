using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassAbilitiesTests
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
    public void PvpAbilityIsLockedUntilAllPassivesAreUnlocked()
    {
        var pm = CreateEvolvedPlayer(5000, 5000);

        Assert.False(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", null, out var reason));
        Assert.Contains("locked", reason);

        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out _));
        Assert.False(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", null, out _));

        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_2", null, out _));
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", null, out _));

        pm.Delete();
    }

    [Fact]
    public void PvpAndPveCanBeUnlockedInAnyMixedOrderOncePassivesAreDone()
    {
        var pm = CreateEvolvedPlayer(5000, 5000);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out _);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_2", null, out _);

        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pve_1", null, out _));
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", null, out _));
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pve_2", null, out _));
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_2", null, out _));

        pm.Delete();
    }

    [Fact]
    public void UltimateIsLockedUntilAllPassivePvpPveAreUnlocked()
    {
        var pm = CreateEvolvedPlayer(5000, 5000);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out _);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_2", null, out _);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", null, out _);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pve_1", null, out _);

        Assert.False(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_ultimate_1", null, out _));

        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_2", null, out _);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pve_2", null, out _);

        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_ultimate_1", null, out _));

        pm.Delete();
    }

    [Fact]
    public void UnlockingSpendsTheFirstAffordablePaymentOptionAndRejectsInsufficientFunds()
    {
        var pm = CreateEvolvedPlayer(1000, 0);

        // passive_1: option0 Exp=100 affordable -> balance 900
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out _));
        Assert.Equal(900, PlayerClassSystem.GetContext(pm).ExpBalance);

        // passive_2: option0 Exp=200 affordable -> balance 700
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_2", null, out _));
        Assert.Equal(700, PlayerClassSystem.GetContext(pm).ExpBalance);

        // pvp_1: option0 Honor=200 unaffordable (honor=0); option1 Exp=400 affordable -> balance 300
        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", null, out _));
        Assert.Equal(300, PlayerClassSystem.GetContext(pm).ExpBalance);

        // pvp_2: option0 Honor=400 unaffordable (honor=0); option1 Exp=800 unaffordable (balance=300)
        Assert.False(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_2", null, out var reason));
        Assert.Contains("afford", reason);
        Assert.Equal(300, PlayerClassSystem.GetContext(pm).ExpBalance);

        pm.Delete();
    }

    [Fact]
    public void ExplicitPaymentOptionIndexIsHonoredAndValidated()
    {
        var pm = CreateEvolvedPlayer(0, 5000);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out _); // -200 Honor (Exp option unaffordable)
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_2", null, out _); // -400 Honor

        Assert.False(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", 5, out var badIndexReason));
        Assert.Contains("no payment option", badIndexReason);

        Assert.True(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_pvp_1", 0, out _)); // -200 Honor
        Assert.Equal(4200, PlayerClassSystem.GetContext(pm).HonorBalance);

        pm.Delete();
    }

    [Fact]
    public void CannotUnlockTheSameAbilityTwice()
    {
        var pm = CreateEvolvedPlayer(5000, 5000);
        PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out _);

        Assert.False(PlayerClassAbilities.TryUnlockAbility(pm, "TestAlpha_passive_1", null, out var reason));
        Assert.Contains("locked", reason);

        pm.Delete();
    }
}

using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassEvolutionTests
{
    [Fact]
    public void TryChooseEvolution_FailsWithoutAClass()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        Assert.False(PlayerClassEvolution.TryChooseEvolution(pm, "TestAlpha", out var reason));
        Assert.Contains("no class", reason);

        pm.Delete();
    }

    [Fact]
    public void TryChooseEvolution_FailsBelowThreshold()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);
        PlayerClassCurrency.AwardExp(pm, PlayerClassSystem.EvolutionThreshold - 1);

        Assert.False(PlayerClassEvolution.TryChooseEvolution(pm, "TestAlpha", out var reason));
        Assert.Contains("threshold", reason);

        pm.Delete();
    }

    [Fact]
    public void TryChooseEvolution_FailsForUnknownEvolutionId()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);
        PlayerClassCurrency.AwardExp(pm, PlayerClassSystem.EvolutionThreshold);

        Assert.False(PlayerClassEvolution.TryChooseEvolution(pm, "NoSuchEvolution", out var reason));
        Assert.Contains("No evolution", reason);

        pm.Delete();
    }

    [Fact]
    public void TryChooseEvolution_SucceedsOnceEligibleThenCannotBeChosenAgain()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);
        PlayerClassCurrency.AwardExp(pm, PlayerClassSystem.EvolutionThreshold);

        Assert.True(PlayerClassEvolution.TryChooseEvolution(pm, "TestAlpha", out var reason));
        Assert.Null(reason);
        Assert.Equal("TestAlpha", PlayerClassSystem.GetContext(pm).EvolutionId);

        Assert.False(PlayerClassEvolution.TryChooseEvolution(pm, "TestBeta", out var secondReason));
        Assert.Contains("already chose", secondReason);

        pm.Delete();
    }
}

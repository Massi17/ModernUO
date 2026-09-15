using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassAssignmentTests
{
    [Fact]
    public void AssignClass_SetsAllowedSkillsToClassCapAndEverythingElseToZero()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        Assert.True(PlayerClassAssignment.AssignClass(pm, "Test", out var failureReason));
        Assert.Null(failureReason);
        Assert.Equal("Test", PlayerClassSystem.GetContext(pm).ClassId);

        Assert.Equal(100.0, pm.Skills[SkillName.Swords].Cap);
        Assert.Equal(100.0, pm.Skills[SkillName.Tactics].Cap);
        Assert.Equal(0.0, pm.Skills[SkillName.Magery].Cap);
        Assert.Equal(0.0, pm.Skills[SkillName.Lockpicking].Cap);

        pm.Delete();
    }

    [Fact]
    public void AssignClass_FailsWhenPlayerAlreadyHasAClass()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        Assert.False(PlayerClassAssignment.AssignClass(pm, "Test", out var failureReason));
        Assert.Contains("already belongs", failureReason);

        pm.Delete();
    }

    [Fact]
    public void AssignClass_FailsForUnknownClassId()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        Assert.False(PlayerClassAssignment.AssignClass(pm, "NoSuchClass", out var failureReason));
        Assert.Contains("No class", failureReason);

        pm.Delete();
    }

    [Fact]
    public void AssignClass_ClampsExistingSkillBaseDownToTheNewCap()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        pm.Skills[SkillName.Magery].Base = 100.0;

        Assert.True(PlayerClassAssignment.AssignClass(pm, "Test", out var failureReason));
        Assert.Null(failureReason);

        Assert.Equal(0.0, pm.Skills[SkillName.Magery].Base);

        pm.Delete();
    }
}

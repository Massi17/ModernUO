using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassSystemTests
{
    [Fact]
    public void GetOrCreateContext_ReturnsSameInstanceOnSecondCall()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        var first = PlayerClassSystem.GetOrCreateContext(pm);
        var second = PlayerClassSystem.GetOrCreateContext(pm);

        Assert.Same(first, second);
        Assert.Same(pm, first.Player);

        pm.Delete();
    }

    [Fact]
    public void GetContext_ReturnsNullWhenNeverCreated()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        Assert.Null(PlayerClassSystem.GetContext(pm));

        pm.Delete();
    }

    [Fact]
    public void OnPlayerDeleted_RemovesContext()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        PlayerClassSystem.GetOrCreateContext(pm);
        PlayerClassSystem.OnPlayerDeleted(pm);

        Assert.Null(PlayerClassSystem.GetContext(pm));
    }

    [Fact]
    public void GetClass_AndGetEvolution_ResolveThePlaceholderTestData()
    {
        var testClass = PlayerClassSystem.GetClass("Test");
        Assert.NotNull(testClass);

        var evolution = PlayerClassSystem.GetEvolution("Test", "TestAlpha");
        Assert.NotNull(evolution);
        Assert.Equal(7, evolution.Abilities.Count);

        Assert.Null(PlayerClassSystem.GetClass("DoesNotExist"));
        Assert.Null(PlayerClassSystem.GetEvolution("Test", "DoesNotExist"));
    }
}

using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassCurrencyTests
{
    [Fact]
    public void AwardExp_IncreasesBalanceAndLifetimeTotal()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        PlayerClassCurrency.AwardExp(pm, 100);
        PlayerClassCurrency.AwardExp(pm, 50);

        var context = PlayerClassSystem.GetContext(pm);
        Assert.Equal(150, context.ExpBalance);
        Assert.Equal(150, context.ExpLifetime);

        pm.Delete();
    }

    [Fact]
    public void AwardHonor_IncreasesBalanceAndLifetimeTotal()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        PlayerClassCurrency.AwardHonor(pm, 300);

        var context = PlayerClassSystem.GetContext(pm);
        Assert.Equal(300, context.HonorBalance);
        Assert.Equal(300, context.HonorLifetime);

        pm.Delete();
    }

    [Fact]
    public void AwardExp_WithZeroOrNegativeAmount_IsANoOp()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        PlayerClassCurrency.AwardExp(pm, 0);
        PlayerClassCurrency.AwardExp(pm, -50);

        Assert.Null(PlayerClassSystem.GetContext(pm));

        pm.Delete();
    }
}

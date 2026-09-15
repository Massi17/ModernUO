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
        var magicArrow = new MagicArrowSpell(caster);

        Assert.True(flameStrike.UsesDeferredCast);
        Assert.False(magicArrow.UsesDeferredCast);

        caster.Delete();
    }
}

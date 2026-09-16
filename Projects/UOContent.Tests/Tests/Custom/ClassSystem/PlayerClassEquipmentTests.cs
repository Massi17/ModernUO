using Server;
using Server.Custom.ClassSystem;
using Server.Items;
using Server.Mobiles;
using Server.Tests;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassEquipmentTests
{
    [SkippableFact]
    public void ForbiddenWeaponCannotBeEquippedAfterClassAssignment()
    {
        TileDataRequirement.SkipIfMissing();

        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        var katana = new Katana();
        Assert.False(pm.EquipItem(katana));
        Assert.Null(katana.Parent);

        katana.Delete();
        pm.Delete();
    }

    [SkippableFact]
    public void AllowedWeaponCanStillBeEquipped()
    {
        TileDataRequirement.SkipIfMissing();

        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        var dagger = new Dagger();
        Assert.True(pm.EquipItem(dagger));

        pm.Delete();
    }

    [SkippableFact]
    public void PlayerWithoutAClassCanEquipAnything()
    {
        TileDataRequirement.SkipIfMissing();

        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        var katana = new Katana();
        Assert.True(pm.EquipItem(katana));

        pm.Delete();
    }

    [SkippableFact]
    public void AlreadyEquippedForbiddenItemIsMovedToBackpackWhenClassIsAssigned()
    {
        TileDataRequirement.SkipIfMissing();

        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        pm.AddItem(new Backpack());

        var katana = new Katana();
        Assert.True(pm.EquipItem(katana));

        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        Assert.Equal(pm.Backpack, katana.Parent);
        Assert.Null(pm.FindItemOnLayer(katana.Layer));

        pm.Delete();
    }
}

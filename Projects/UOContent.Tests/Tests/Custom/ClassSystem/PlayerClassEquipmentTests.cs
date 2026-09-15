using Server;
using Server.Custom.ClassSystem;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassEquipmentTests
{
    [Fact]
    public void ForbiddenWeaponCannotBeEquippedAfterClassAssignment()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        var katana = new Katana();
        Assert.False(pm.EquipItem(katana));
        Assert.Null(katana.Parent);

        katana.Delete();
        pm.Delete();
    }

    [Fact]
    public void AllowedWeaponCanStillBeEquipped()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();
        PlayerClassAssignment.AssignClass(pm, "Test", out _);

        var dagger = new Dagger();
        Assert.True(pm.EquipItem(dagger));

        pm.Delete();
    }

    [Fact]
    public void PlayerWithoutAClassCanEquipAnything()
    {
        var pm = new PlayerMobile(World.NewMobile);
        pm.DefaultMobileInit();

        var katana = new Katana();
        Assert.True(pm.EquipItem(katana));

        pm.Delete();
    }

    [Fact]
    public void AlreadyEquippedForbiddenItemIsMovedToBackpackWhenClassIsAssigned()
    {
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

using Server.Custom.ClassSystem;

namespace Server.Mobiles;

public partial class PlayerMobile
{
    public override bool EquipItem(Item item)
    {
        if (item?.Deleted == false && !PlayerClassEquipment.CanEquip(this, item, out var failureReason))
        {
            SendMessage(failureReason);
            return false;
        }

        return base.EquipItem(item);
    }
}

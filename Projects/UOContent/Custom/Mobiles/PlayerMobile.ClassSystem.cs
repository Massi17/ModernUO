using Server.Custom.ClassSystem;
using Server.Items;

namespace Server.Mobiles;

public partial class PlayerMobile
{
    public override bool OnEquip(Item item)
    {
        if (!PlayerClassEquipment.CanEquip(this, item, out var failureReason))
        {
            SendMessage(failureReason);
            return false;
        }

        return base.OnEquip(item);
    }
}

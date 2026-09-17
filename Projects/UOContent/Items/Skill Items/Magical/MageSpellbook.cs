using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0)]
public partial class MageSpellbook : Spellbook
{
    [Constructible]
    public MageSpellbook() : base(0xCC208284E0018)
    {
        Name = "Mage's Spellbook";
    }

    public override bool RequiresEquipToCast => true;
}

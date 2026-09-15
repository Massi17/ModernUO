using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Custom.ClassSystem;

[PropertyObject]
[SerializationGenerator(0)]
public partial class PlayerClassContext
{
    [DirtyTrackingEntity]
    private PlayerMobile _player;

    public PlayerMobile Player => _player;

    public PlayerClassContext(PlayerMobile player) => _player = player;

    [SerializableField(0)]
    private string _classId;

    [SerializableField(1)]
    private string _evolutionId;

    [SerializableField(2)]
    private int _expBalance;

    [SerializableField(3)]
    private int _honorBalance;

    [SerializableField(4)]
    private int _expLifetime;

    [SerializableField(5)]
    private int _honorLifetime;

    [SerializableField(6)]
    private List<string> _unlockedAbilityIds = new();
}

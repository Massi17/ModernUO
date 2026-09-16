using System;

namespace Server.Network;

public sealed class MagicShieldPacket : Packet
{
    public MagicShieldPacket(Serial mobile, int points) : base(0xBF)
    {
        EnsureCapacity(11);

        Stream.Write((short)0x4D53);
        Stream.Write(mobile);
        Stream.Write((ushort)Math.Clamp(points, 0, 0xFFFF));
    }
}

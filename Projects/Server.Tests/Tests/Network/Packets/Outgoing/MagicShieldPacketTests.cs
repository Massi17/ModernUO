using Server.Network;
using Xunit;

namespace Server.Tests.Network;

[Collection("Sequential Server Tests")]
public class MagicShieldPacketTests
{
    [Theory, InlineData(0), InlineData(50), InlineData(65535), InlineData(-5)]
    public void TestMagicShield(int points)
    {
        var serial = (Serial)0x1024;

        var expected = new MagicShieldPacket(serial, points).Compile();

        using var ns = PacketTestUtilities.CreateTestNetState();
        ns.SendMagicShield(serial, points);

        var result = ns.SendBuffer.GetReadSpan();
        AssertThat.Equal(result, expected);
    }
}

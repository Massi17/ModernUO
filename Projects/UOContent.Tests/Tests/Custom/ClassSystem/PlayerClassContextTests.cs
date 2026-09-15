using System;
using Server;
using Server.Custom.ClassSystem;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class PlayerClassContextTests
{
    [Fact]
    public void DefaultsAreEmptyAndZero()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();

        var context = new PlayerClassContext(player);

        Assert.Same(player, context.Player);
        Assert.Null(context.ClassId);
        Assert.Null(context.EvolutionId);
        Assert.Equal(0, context.ExpBalance);
        Assert.Equal(0, context.HonorBalance);
        Assert.Equal(0, context.ExpLifetime);
        Assert.Equal(0, context.HonorLifetime);
        Assert.Empty(context.UnlockedAbilityIds);

        player.Delete();
    }

    [Fact]
    public void SerializeThenDeserializeRoundTripsFields()
    {
        var player = new PlayerMobile(World.NewMobile);
        player.DefaultMobileInit();

        var context = new PlayerClassContext(player)
        {
            ClassId = "Test",
            EvolutionId = "TestAlpha",
            ExpBalance = 250,
            HonorBalance = 75,
            ExpLifetime = 900,
            HonorLifetime = 300
        };
        context.UnlockedAbilityIds.Add("TestAlpha_passive_1");

        var writer = new BufferWriter(true);
        context.Serialize(writer);

        var buffer = new byte[writer.Position];
        writer.Buffer.AsSpan(0, (int)writer.Position).CopyTo(buffer);
        writer.Close();

        var restored = new PlayerClassContext(player);
        restored.Deserialize(new BufferReader(buffer));

        Assert.Equal("Test", restored.ClassId);
        Assert.Equal("TestAlpha", restored.EvolutionId);
        Assert.Equal(250, restored.ExpBalance);
        Assert.Equal(75, restored.HonorBalance);
        Assert.Equal(900, restored.ExpLifetime);
        Assert.Equal(300, restored.HonorLifetime);
        Assert.Single(restored.UnlockedAbilityIds);
        Assert.Equal("TestAlpha_passive_1", restored.UnlockedAbilityIds[0]);

        player.Delete();
    }
}

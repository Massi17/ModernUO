using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ModernUO.CodeGeneratedEvents;
using Server.Json;
using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public class PlayerClassSystem : GenericPersistence
{
    private static readonly Dictionary<PlayerMobile, PlayerClassContext> _contexts = new();

    private static ClassSystemConfig _config;

    private static ClassSystemConfig Config => _config ??= LoadOrCreateConfig();

    public static void Configure()
    {
        _ = Config;
        _ = new PlayerClassSystem();
    }

    public PlayerClassSystem() : base("PlayerClasses", 10)
    {
    }

    private static ClassSystemConfig LoadOrCreateConfig()
    {
        var path = Path.Combine(Core.BaseDirectory, "Configuration/ClassSystem/classes.json");
        var config = JsonConfig.Deserialize<ClassSystemConfig>(path);

        if (config == null)
        {
            config = ClassSystemConfig.CreateDefault();
            JsonConfig.Serialize(path, config);
        }

        return config;
    }

    public static int EvolutionThreshold => Config.EvolutionThreshold;
    public static int EvolutionRespecRefundPercent => Config.EvolutionRespecRefundPercent;
    public static int ClassRespecRefundPercent => Config.ClassRespecRefundPercent;

    public static PlayerClassDefinitionData GetClass(string classId) =>
        string.IsNullOrEmpty(classId) ? null : Config.Classes.Find(c => c.Id == classId);

    public static EvolutionDefinitionData GetEvolution(string classId, string evolutionId)
    {
        if (string.IsNullOrEmpty(evolutionId))
        {
            return null;
        }

        return GetClass(classId)?.Evolutions.Find(e => e.Id == evolutionId);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(PlayerMobile pm) => _contexts.Remove(pm);

    public static PlayerClassContext GetContext(PlayerMobile pm) =>
        pm != null && _contexts.TryGetValue(pm, out var context) ? context : null;

    public static PlayerClassContext GetOrCreateContext(PlayerMobile pm)
    {
        if (pm == null)
        {
            return null;
        }

        ref var context = ref CollectionsMarshal.GetValueRefOrAddDefault(_contexts, pm, out var exists);
        if (!exists)
        {
            context = new PlayerClassContext(pm);
        }

        return context;
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0);
        writer.WriteEncodedInt(_contexts.Count);

        foreach (var (pm, context) in _contexts)
        {
            writer.Write(pm);
            context.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt();
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var context = new PlayerClassContext(player);
            context.Deserialize(reader);

            if (player != null)
            {
                _contexts.Add(player, context);
            }
        }
    }
}

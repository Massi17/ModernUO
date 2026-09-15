using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassEquipment
{
    private static readonly Dictionary<string, List<Type>> _forbiddenTypesByClass = new();

    public static bool CanEquip(PlayerMobile pm, Item item, out string failureReason)
    {
        var classId = PlayerClassSystem.GetContext(pm)?.ClassId;
        if (string.IsNullOrEmpty(classId))
        {
            failureReason = null;
            return true;
        }

        foreach (var type in GetForbiddenTypes(classId))
        {
            if (type.IsInstanceOfType(item))
            {
                failureReason = "Your class cannot use that.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private static List<Type> GetForbiddenTypes(string classId)
    {
        if (_forbiddenTypesByClass.TryGetValue(classId, out var cached))
        {
            return cached;
        }

        var types = new List<Type>();
        var classDef = PlayerClassSystem.GetClass(classId);
        if (classDef != null)
        {
            foreach (var typeName in classDef.ForbiddenItemTypeNames)
            {
                var type = AssemblyHandler.FindTypeByName(typeName);
                if (type != null)
                {
                    types.Add(type);
                }
            }
        }

        _forbiddenTypesByClass[classId] = types;
        return types;
    }
}

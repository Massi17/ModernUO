using System;
using System.Collections.Generic;
using Server.Logging;
using Server.Mobiles;

namespace Server.Custom.ClassSystem;

public static class PlayerClassEquipment
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(PlayerClassEquipment));

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

    public static void UnequipForbiddenItems(PlayerMobile pm)
    {
        var classId = PlayerClassSystem.GetContext(pm)?.ClassId;
        if (string.IsNullOrEmpty(classId))
        {
            return;
        }

        var forbidden = GetForbiddenTypes(classId);
        if (forbidden.Count == 0)
        {
            return;
        }

        foreach (var item in pm.Items.ToArray())
        {
            foreach (var type in forbidden)
            {
                if (type.IsInstanceOfType(item))
                {
                    pm.AddToBackpack(item);
                    break;
                }
            }
        }
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
                else
                {
                    _logger.Warning(
                        "Class '{ClassId}' has an unresolvable forbidden item type name '{TypeName}' — check for a typo in the class-system config",
                        classId,
                        typeName
                    );
                }
            }
        }

        _forbiddenTypesByClass[classId] = types;
        return types;
    }
}

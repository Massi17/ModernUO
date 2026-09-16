using System.Collections.Generic;

namespace Server.Custom;

public static class PvpDamageTest
{
    private static readonly HashSet<Mobile> _enabled = new();

    public static bool IsEnabled(Mobile attacker) => attacker != null && _enabled.Contains(attacker);

    public static bool Toggle(Mobile attacker)
    {
        if (_enabled.Remove(attacker))
        {
            return false;
        }

        _enabled.Add(attacker);
        return true;
    }

    public static void Report(Mobile attacker, Mobile target, int shieldAbsorbed, int hpDamage)
    {
        if (!IsEnabled(attacker))
        {
            return;
        }

        var total = shieldAbsorbed + hpDamage;
        attacker.SendMessage(
            $"[PvpTest] {target?.Name ?? "unknown"}: {total} total damage ({hpDamage} HP, {shieldAbsorbed} shield)."
        );
    }
}

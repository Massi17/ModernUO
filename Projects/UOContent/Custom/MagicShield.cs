using System;
using System.Collections.Generic;
using Server.Network;

namespace Server.Custom;

public static class MagicShield
{
    private static readonly Dictionary<Mobile, TimerExecutionToken> _expireTokens = new();

    public static void Apply(Mobile m, int points, TimeSpan? duration = null)
    {
        Clear(m);

        m.MagicShieldAbsorb = points;
        NotifyClient(m);

        if (duration is { } d)
        {
            Timer.StartTimer(d, () => Clear(m), out var token);
            _expireTokens[m] = token;
        }
    }

    public static void Clear(Mobile m)
    {
        if (_expireTokens.Remove(m, out var token))
        {
            token.Cancel();
        }

        if (m.MagicShieldAbsorb != 0)
        {
            m.MagicShieldAbsorb = 0;
            NotifyClient(m);
        }
    }

    public static int Absorb(Mobile target, int damage)
    {
        if (target.MagicShieldAbsorb <= 0 || damage <= 0)
        {
            return damage;
        }

        var absorbed = Math.Min(target.MagicShieldAbsorb, damage);
        target.MagicShieldAbsorb -= absorbed;
        NotifyClient(target);

        if (target.MagicShieldAbsorb == 0)
        {
            Clear(target);
        }

        return damage - absorbed;
    }

    private static void NotifyClient(Mobile m)
    {
        var map = m.Map;
        if (map == null)
        {
            return;
        }

        foreach (var ns in map.GetClientsInRange(m.Location))
        {
            if (ns.Mobile.CanSee(m))
            {
                ns.SendMagicShield(m.Serial, m.MagicShieldAbsorb);
            }
        }
    }
}

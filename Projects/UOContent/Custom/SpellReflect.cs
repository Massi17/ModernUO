using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.BuffIcons;
using Server.Mobiles;

namespace Server.Custom;

public static class SpellReflect
{
    private static readonly Dictionary<Mobile, TimerExecutionToken> _expireTokens = new();

    public static void Apply(Mobile m, TimeSpan duration)
    {
        Clear(m);

        m.SpellReflectActive = true;
        (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.MagicReflection, 1075817, 1075817, duration, retainThroughDeath: true));

        Timer.StartTimer(duration, () => Clear(m), out var token);
        _expireTokens[m] = token;
    }

    public static void Clear(Mobile m)
    {
        if (_expireTokens.Remove(m, out var token))
        {
            token.Cancel();
        }

        if (m.SpellReflectActive)
        {
            m.SpellReflectActive = false;
            (m as PlayerMobile)?.RemoveBuff(BuffIcon.MagicReflection);
        }
    }

    public static bool IsActive(Mobile m) => m.SpellReflectActive;

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(Mobile m) => Clear(m);
}

/*************************************************************************
 * ModernUO                                                              *
 * Copyright 2019-2026 - ModernUO Development Team                       *
 * Email: hi@modernuo.com                                                *
 * File: OutgoingMagicShieldPackets.cs                                   *
 *                                                                       *
 * This program is free software: you can redistribute it and/or modify  *
 * it under the terms of the GNU General Public License as published by  *
 * the Free Software Foundation, either version 3 of the License, or     *
 * (at your option) any later version.                                   *
 *                                                                       *
 * You should have received a copy of the GNU General Public License     *
 * along with this program.  If not, see <http://www.gnu.org/licenses/>. *
 *************************************************************************/

using System;
using System.Buffers;

namespace Server.Network;

public static class OutgoingMagicShieldPackets
{
    public static void SendMagicShield(this NetState ns, Serial serial, int points)
    {
        if (ns.CannotSendPackets())
        {
            return;
        }

        var writer = new SpanWriter(stackalloc byte[11]);

        writer.Write((byte)0xBF); // Packet ID
        writer.Write((ushort)11); // Length
        writer.Write((ushort)0x4D53); // Sub-command: Magic Shield
        writer.Write(serial);
        writer.Write((ushort)Math.Clamp(points, 0, 0xFFFF));

        ns.Send(writer.Span);
    }
}

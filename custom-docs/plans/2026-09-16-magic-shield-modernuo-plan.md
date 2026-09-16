# Magic Shield (ModernUO server side) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the server-side "magic shield" primitive — a depletable, spell-damage-only absorb pool on `Mobile`, with a client-notification packet — as a reusable engine piece, not yet wired to any spell/ability.

**Architecture:** A new `Mobile.MagicShieldAbsorb` int property (mirrors the existing `MeleeDamageAbsorb` pattern) holds the pool. A static `MagicShield` class in UOContent owns apply/deplete/expire and client notification. Three insertion points in `SpellHelper.cs` route spell damage through `MagicShield.Absorb` before it reaches `Mobile.Damage()`/`AOS.Damage()`. Client notification is a new `0xBF` sub-command packet, broadcast directly to in-range watchers (not through the `MobileDelta`/`ProcessDelta` queue).

**Tech Stack:** C# / .NET 10, xUnit (`[Fact]`/`[Theory]`), ModernUO's `SpanWriter`/`Packet` packet-encoding conventions.

**Spec:** `custom-docs/specs/2026-09-16-magic-shield-design.md` (ModernUO side — mechanic, hook points, network, testing). Companion client spec: `../../ClassicUO/custom-docs/specs/2026-09-16-magic-shield-design.md` (out of scope for this plan).

## Global Constraints

- `Projects/Server/` changes are pre-authorized for this feature (the user explicitly approved touching it for `MeleeDamageAbsorb`-style additions and the packet file) — but stay minimal: only the one `Mobile` property and one new packet file, nothing in `ProcessDelta`/`MobileDelta`.
- Shield absorbs **spell damage only** — never melee/ranged/poison/trap damage. `AOS.Damage()` (`Projects/UOContent/Misc/AOS.cs`) must never be modified — it's shared with non-magic damage sources.
- `Apply()` always **replaces** any existing shield (no stacking, no stacking refusal) — confirmed design decision.
- `Absorb()` **bleeds through**: damage exceeding the remaining pool zeroes the pool and the excess still reaches real HP in the same hit — mirrors `MeleeDamageAbsorb`'s existing behavior.
- New sub-command ID under `0xBF`: **`0x4D53`** (ASCII "MS") — confirmed unused anywhere in `Projects/` by grep at plan-writing time.
- All new tests use `Collection("Sequential Server Tests")` (Server.Tests) or `Collection("Sequential UOContent Tests")` (UOContent.Tests) matching the file each test lives in — never mix.

---

## Task 1: Data model and network packet

**Files:**
- Modify: `Projects/Server/Mobiles/Mobile.cs:478` (next to `MeleeDamageAbsorb`)
- Create: `Projects/Server/Network/Packets/OutgoingMagicShieldPackets.cs`
- Create (test-only comparator): `Projects/Server.Tests/Tests/Network/Packets/Outgoing/MagicShieldPackets.cs`
- Test: `Projects/Server.Tests/Tests/Network/Packets/Outgoing/MagicShieldPacketTests.cs`

**Interfaces:**
- Produces: `Mobile.MagicShieldAbsorb` (`int`, get/set, default 0). `NetState.SendMagicShield(Serial serial, int points)` extension method — Task 2 calls this from `MagicShield.NotifyClient`.

- [ ] **Step 1: Add the `Mobile.MagicShieldAbsorb` property**

In `Projects/Server/Mobiles/Mobile.cs`, immediately after the existing `MeleeDamageAbsorb` property (search for `MeleeDamageAbsorb` — it's a plain auto-property with no attributes):

```csharp
public int MagicShieldAbsorb { get; set; }
```

No test for this step alone — it's a bare field, exercised by Task 2's tests once `MagicShield` reads/writes it.

- [ ] **Step 2: Write the failing packet-encoding test**

Create `Projects/Server.Tests/Tests/Network/Packets/Outgoing/MagicShieldPackets.cs` (test-only reference encoder — this project's convention for every outgoing packet test is to compare the fast `SpanWriter` path against an independently-written legacy `Packet`-subclass; see `DamagePacketOld` in the sibling `DamagePackets.cs` for the exact precedent this mirrors):

```csharp
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
```

Create `Projects/Server.Tests/Tests/Network/Packets/Outgoing/MagicShieldPacketTests.cs`:

```csharp
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
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName=Server.Tests.Network.MagicShieldPacketTests.TestMagicShield"`
Expected: FAIL — `SendMagicShield` doesn't exist yet (compile error surfaces as a test-run failure).

- [ ] **Step 4: Implement the production packet writer**

Create `Projects/Server/Network/Packets/OutgoingMagicShieldPackets.cs` (mirrors `OutgoingDamagePackets.cs` exactly — same header/footer/writer shape as every file in this directory):

```csharp
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
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName=Server.Tests.Network.MagicShieldPacketTests.TestMagicShield"`
Expected: PASS (all 4 `InlineData` cases, including the negative-clamp and overflow-clamp cases).

- [ ] **Step 6: Full Server.Tests run and commit**

Run: `dotnet test Projects/Server.Tests` — confirm no regressions elsewhere.

```bash
git add Projects/Server/Mobiles/Mobile.cs Projects/Server/Network/Packets/OutgoingMagicShieldPackets.cs Projects/Server.Tests/Tests/Network/Packets/Outgoing/MagicShieldPackets.cs Projects/Server.Tests/Tests/Network/Packets/Outgoing/MagicShieldPacketTests.cs
git commit -m "feat: add MagicShieldAbsorb field and magic-shield notification packet"
```

---

## Task 2: `MagicShield` static API (apply, absorb, clear, expiry)

**Files:**
- Create: `Projects/UOContent/Custom/MagicShield.cs`
- Test: `Projects/UOContent.Tests/Tests/Custom/MagicShieldTests.cs`

**Interfaces:**
- Consumes: `Mobile.MagicShieldAbsorb` (Task 1), `NetState.SendMagicShield(Serial, int)` (Task 1), `Map.GetClientsInRange(Point3D)` (engine), `Timer.StartTimer(TimeSpan, Action, out TimerExecutionToken)` (engine), `TimerExecutionToken.Cancel()` (engine).
- Produces: `MagicShield.Apply(Mobile m, int points, TimeSpan? duration = null)`, `MagicShield.Clear(Mobile m)`, `MagicShield.Absorb(Mobile target, int damage) -> int` (returns damage still owed to real HP). Task 3 calls `Absorb`.

- [ ] **Step 1: Write the failing tests**

Create `Projects/UOContent.Tests/Tests/Custom/MagicShieldTests.cs`:

```csharp
using Server;
using Server.Custom;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MagicShieldTests
{
    // 8ms lockstep keeps the wheel and Core.TickCount in sync — same helper as
    // Tests/Mobiles/AI/FamiliarAITests.cs, reused here for the expiry tests below.
    private static void RunFor(long ms)
    {
        var deadline = Core._tickCount + ms;

        while (Core._tickCount - deadline < 0)
        {
            Core._tickCount += 8;
            Timer.Slice(Core._tickCount);
        }
    }

    [Fact]
    public void ApplySetsThePool()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        MagicShield.Apply(m, 50);

        Assert.Equal(50, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void ApplyReplacesAnExistingShield()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        MagicShield.Apply(m, 50);
        MagicShield.Apply(m, 20);

        Assert.Equal(20, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void AbsorbReducesThePoolAndReturnsZero_WhenDamageIsLessThanThePool()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50);

        var remaining = MagicShield.Absorb(m, 30);

        Assert.Equal(0, remaining);
        Assert.Equal(20, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void AbsorbBleedsThrough_WhenDamageExceedsThePool()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50);

        var remaining = MagicShield.Absorb(m, 70);

        Assert.Equal(20, remaining);
        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void AbsorbIsANoOp_WhenThereIsNoShield()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        var remaining = MagicShield.Absorb(m, 40);

        Assert.Equal(40, remaining);
        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void ClearCancelsTheExpiryTimer()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50, TimeSpan.FromSeconds(5));

        MagicShield.Clear(m);
        Assert.Equal(0, m.MagicShieldAbsorb);

        // Advancing past the original duration must not resurrect/re-clear a stale pool.
        RunFor(6000);
        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }

    [Fact]
    public void ShieldExpiresOnItsOwn_WithoutBeingHit()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        MagicShield.Apply(m, 50, TimeSpan.FromSeconds(5));

        Assert.Equal(50, m.MagicShieldAbsorb);

        RunFor(6000);

        Assert.Equal(0, m.MagicShieldAbsorb);

        m.Delete();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~MagicShieldTests"`
Expected: FAIL — `Server.Custom.MagicShield` doesn't exist yet.

- [ ] **Step 3: Implement `MagicShield`**

Create `Projects/UOContent/Custom/MagicShield.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~MagicShieldTests"`
Expected: PASS (all 7 tests).

- [ ] **Step 5: Full UOContent.Tests run and commit**

Run: `dotnet test Projects/UOContent.Tests`

```bash
git add Projects/UOContent/Custom/MagicShield.cs Projects/UOContent.Tests/Tests/Custom/MagicShieldTests.cs
git commit -m "feat: add MagicShield apply/absorb/clear API"
```

---

## Task 3: Hook into spell damage

**Files:**
- Modify: `Projects/UOContent/Spells/Base/SpellHelper.cs` (three sites — see below)
- Test: `Projects/UOContent.Tests/Tests/Custom/MagicShieldSpellDamageTests.cs`

**Interfaces:**
- Consumes: `MagicShield.Absorb(Mobile, int) -> int` (Task 2).
- Produces: nothing new — this task only wires an existing consumer into existing engine call sites.

- [ ] **Step 1: Write the failing tests**

Create `Projects/UOContent.Tests/Tests/Custom/MagicShieldSpellDamageTests.cs`. This drives `SpellHelper.Damage` directly (no live spell/caster needed — every overload accepts `Mobile from = null` and a `Spell spell = null`):

```csharp
using Server;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MagicShieldSpellDamageTests
{
    [Fact]
    public void SimpleImmediateSpellDamage_IsAbsorbed()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        SpellHelper.Damage(TimeSpan.Zero, target, 30);

        Assert.Equal(20, target.MagicShieldAbsorb);
        Assert.Equal(100, target.Hits); // fully absorbed, real HP untouched

        target.Delete();
    }

    [Fact]
    public void SimpleImmediateSpellDamage_BleedsThroughWhenItExceedsThePool()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        SpellHelper.Damage(TimeSpan.Zero, target, 70);

        Assert.Equal(0, target.MagicShieldAbsorb);
        Assert.Equal(80, target.Hits); // 20 points overflow past the shield

        target.Delete();
    }

    [Fact]
    public void ElementalSpellDamage_IsAbsorbed()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        // 100% fire, no resistance on a bare test Mobile — full 30 lands as fire damage pre-shield.
        SpellHelper.Damage(TimeSpan.Zero, target, 30, 0, 100, 0, 0, 0);

        Assert.Equal(20, target.MagicShieldAbsorb);
        Assert.Equal(100, target.Hits);

        target.Delete();
    }

    [Fact]
    public void MeleeDamage_IsNeverAbsorbed()
    {
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.Hits = 100;
        MagicShield.Apply(target, 50);

        target.Damage(30); // direct Mobile.Damage call, the melee/generic path

        Assert.Equal(50, target.MagicShieldAbsorb); // untouched
        Assert.Equal(70, target.Hits);

        target.Delete();
    }
}
```

This test's numbers hold regardless of era/resistance config: the shield fully absorbs the 30 damage (pool is 50), so `dmg` reaches `AOS.Damage(...)` as `0` — and `AOS.Damage` early-returns `0` for `damage <= 0` (`Projects/UOContent/Misc/AOS.cs:52`) before any resistance math runs. `target.Hits` is asserted unchanged for that reason, not because resistances happen to be zero.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~MagicShieldSpellDamageTests"`
Expected: FAIL on the three spell-damage tests (absorption never happens yet); `MeleeDamage_IsNeverAbsorbed` already passes (nothing hooks melee) — that's expected and fine, it's here as a regression guard for the next steps, not a red/green driver.

- [ ] **Step 3: Hook site 1 — simple immediate damage**

In `Projects/UOContent/Spells/Base/SpellHelper.cs`, inside `Damage(Spell spell, TimeSpan delay, Mobile target, Mobile from, double damage)` (the `delay == TimeSpan.Zero` branch, today ~line 953-970):

```csharp
if (delay == TimeSpan.Zero)
{
    var bcFrom = from as BaseCreature;
    var bcTarget = target as BaseCreature;

    bcFrom?.AlterSpellDamageTo(target, ref damageGiven);
    bcTarget?.AlterSpellDamageFrom(from, ref damageGiven);
    damageGiven = MagicShield.Absorb(target, damageGiven);

    target.Damage(damageGiven, from);

    bcFrom?.OnDamageSpell(target, damageGiven);

    if (from != null)
    {
        bcTarget?.OnHarmfulSpell(from);
        bcTarget?.OnDamagedBySpell(from, damageGiven);
    }
}
```

Only the one new line (`damageGiven = MagicShield.Absorb(target, damageGiven);`) is new — the rest of the branch is shown unchanged, for anchoring. Add `using Server.Custom;` to the top of the file if not already present.

- [ ] **Step 4: Hook site 2 — simple delayed damage (`SpellDamageTimer.OnTick`)**

Same file, private nested `SpellDamageTimer` class, `OnTick()` (today ~line 1108-1115):

```csharp
protected override void OnTick()
{
    (m_From as BaseCreature)?.AlterSpellDamageTo(m_Target, ref m_Damage);
    (m_Target as BaseCreature)?.AlterSpellDamageFrom(m_From, ref m_Damage);
    m_Damage = MagicShield.Absorb(m_Target, m_Damage);

    m_Target.Damage(m_Damage);
    m_Spell?.RemoveDelayedDamageContext(m_Target);
}
```

- [ ] **Step 5: Hook site 3 — elemental damage**

Same file, `Damage(Spell spell, TimeSpan delay, Mobile target, Mobile from, double damage, int phys, int fire, int cold, int pois, int nrgy, int chaos, DFAlgorithm dfa)` (the `delay == TimeSpan.Zero` branch, today ~line 1021-1037), right before the `AOS.Damage(...)` call:

```csharp
if (Feint.GetDamageReduction(from, target, out var feintReduction))
{
    dmg -= dmg * feintReduction / 100;
}

dmg = MagicShield.Absorb(target, dmg);

StaminaSystem.DFA = dfa;

var damageGiven = AOS.Damage(target, from, dmg, phys, fire, cold, pois, nrgy, chaos);
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~MagicShieldSpellDamageTests"`
Expected: PASS (all 4 tests).

- [ ] **Step 7: Full UOContent.Tests run and commit**

Run: `dotnet test Projects/UOContent.Tests` — pay attention to any spell-damage test that starts failing (a sign the insertion moved damage math it shouldn't have, e.g. inserted in the wrong branch or before a check that also needs the original `damage`/`dmg` value); every existing test has `MagicShieldAbsorb == 0` on its targets, so `MagicShield.Absorb` must be a true no-op for all of them (see `AbsorbIsANoOp_WhenThereIsNoShield` in Task 2) — a regression here means the hook changed behavior for the *unshielded* case, which the design never intended.

```bash
git add Projects/UOContent/Spells/Base/SpellHelper.cs Projects/UOContent.Tests/Tests/Custom/MagicShieldSpellDamageTests.cs
git commit -m "feat: absorb spell damage through MagicShield before it lands"
```

---

## Out of scope for this plan (tracked elsewhere)

- ClassicUO client rendering — separate plan against `../../ClassicUO/custom-docs/specs/2026-09-16-magic-shield-design.md`.
- Resending the shield packet to a watcher who newly walks into view of an already-shielded Mobile (noted as a known limitation in the spec's Task 1 — `NotifyClient` only broadcasts on state *change*, not on visibility change). Not implemented here; revisit if it turns out to matter in practice.
- Wiring `MagicShield.Apply` to an actual spell/ability/item — this plan builds the primitive only, per the "generic reusable mechanic" scope decision from brainstorming.

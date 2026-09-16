# Magic shield: a depletable, magic-only damage-absorb pool (design)

Client-visible counterpart: `../../ClassicUO/custom-docs/specs/2026-09-16-magic-shield-design.md` (HP-bar rendering). This doc owns the mechanic; the ClassicUO doc owns only how it's drawn.

## Goal

A reusable server primitive: grant a Mobile N points of "magic shield" that absorb damage from spell sources only (melee/ranged damage passes through untouched), for an optional duration, depleting point-for-point as spell damage lands. Not tied to any specific spell yet — this is the engine piece other systems (a future spell, a class-system passive, an item proc) will call into.

Explicitly out of scope for this pass: the spell/ability that grants the shield, a buff-bar icon, and anything beyond the HP-bar visual the client side covers.

## Data model

`Projects/Server/Mobiles/Mobile.cs`, next to the existing `MeleeDamageAbsorb` (line 478 today):

```csharp
public int MagicShieldAbsorb { get; set; }
```

A plain field, no behavior attached — same shape as the existing precedent. Everything else (apply/deplete/expire/notify) lives in UOContent.

## Applying and depleting

`Projects/UOContent/Custom/MagicShield.cs` (static API; adjust namespace/folder to match wherever we land other Custom/ engine primitives):

```csharp
public static class MagicShield
{
    private static readonly Dictionary<Mobile, TimerExecutionToken> _expireTokens = new();

    public static void Apply(Mobile m, int points, TimeSpan? duration = null)
    {
        Clear(m); // replace semantics: any prior shield is dropped, not stacked

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

    // Called from SpellHelper.Damage(). Returns the damage that should still
    // land on real HP after the shield has taken its share.
    public static int Absorb(Mobile target, int damage)
    {
        if (target.MagicShieldAbsorb <= 0)
        {
            return damage;
        }

        var absorbed = Math.Min(target.MagicShieldAbsorb, damage);
        target.MagicShieldAbsorb -= absorbed;
        NotifyClient(target);

        if (target.MagicShieldAbsorb == 0)
        {
            Clear(target); // cancels the now-irrelevant expiry timer
        }

        return damage - absorbed;
    }

    private static void NotifyClient(Mobile m) { /* Section: network */ }
}
```

`Apply` always replaces (confirmed): a fresh cast on an already-shielded target zeroes the old pool and timer before applying the new one, rather than stacking or refusing.

`Absorb` bleeds through: if incoming damage exceeds the remaining pool, the shield drops to 0 and the excess still reaches real HP in the same hit — mirrors `MeleeDamageAbsorb`'s existing behavior in Reactive Armor, so both absorb mechanics read the same way to anyone touching this code later.

## Hook points

`SpellHelper.Damage()` is not one chokepoint — spell damage reaches `Mobile.Damage()` (or the shared `AOS.Damage()` resistance calculator) through three call sites in `Projects/UOContent/Spells/Base/SpellHelper.cs`, all needing the same one-line insertion:

1. **Simple (non-elemental) immediate damage** — `Damage(Spell, TimeSpan, Mobile, Mobile, double)`, ~line 961, before `target.Damage(damageGiven, from)`.
2. **Simple (non-elemental) delayed damage** — the private nested `SpellDamageTimer.OnTick()`, ~line 1113, before `m_Target.Damage(m_Damage)`. This does *not* re-enter the method above — it calls `Mobile.Damage()` directly — so it needs its own insertion, not just the one in (1).
3. **Elemental damage** (phys/fire/cold/pois/nrgy/chaos split — what most AOS-era spells use) — `Damage(Spell, TimeSpan, Mobile, Mobile, double, int, int, int, int, int, int, DFAlgorithm)`, ~line 1037, before `AOS.Damage(target, from, dmg, phys, fire, cold, pois, nrgy, chaos)`, reducing `dmg` first. The delayed variant (`SpellDamageTimerAOS.OnTick()`) re-enters *this same* immediate branch (calls `Damage(spell, TimeSpan.Zero, ...)`), so it's already covered — no separate insertion needed for it.

**`AOS.Damage()` itself must not be touched** — `Projects/UOContent/Misc/AOS.cs` is a shared resistance calculator also called directly by `BaseWeapon`, weapon abilities, poison, traps, and monster melee specials (confirmed by grep across `Projects/UOContent`). Absorbing there would swallow non-magic damage too. The shield reduction has to happen in the spell-only caller (site 3 above) before `AOS.Damage()` is ever invoked.

Each site gets the same shape:

```csharp
if (target is Mobile targetMobile)
{
    damageGiven = MagicShield.Absorb(targetMobile, damageGiven); // or dmg, at site 3
}
```

Monster special abilities (`AlterMeleeDamageFrom`/their own direct damage calls, not `SpellHelper.Damage()`) are unaffected, matching the "spell damage only" scope decided during design. If a later feature needs shield absorption on a non-`SpellHelper` magic source, that's a new call to `MagicShield.Absorb` at that source's own damage point — the primitive doesn't need to change.

## Network: notifying the client

No changes to `Projects/Server/Network/`. `0xA1`/`0xA2`/`0xA3` (Hits/Mana/Stam) are fixed 2-value packets with no spare field, so the shield value travels as an `0xBF` (Extended Command) sub-command instead of a new top-level opcode — safer against upstream `git merge` collisions than claiming an opcode, since ModernUO is far more likely to add its own new sub-commands under `0xBF` occasionally than to reintroduce a byte we've claimed at the top level (and even that is a one-entry check at merge time, not a renumbering).

New packet, defined entirely in `Projects/UOContent/` as a `Packet` subclass (no `Projects/Server/` changes needed beyond the `Mobile` field above — `Packet` and `NetState.SendPacket` are already public):

- Sub-command ID: pick an unused value under `0xBF` (verify against ModernUO's current sub-command table at implementation time — not enumerated here to avoid the doc going stale).
- Payload: `Serial` (4 bytes) + `MagicShieldAbsorb` (2 bytes, same compact width as the existing Hits attribute encoding).

`NotifyClient(m)` sends this to every `NetState` that currently has `m` in view (same "who can see this mobile" lookup the engine already uses for broadcasting other Mobile updates) — called from `Apply`, `Absorb` (on every partial deplete, not just on full clear), and `Clear`. Additionally: when a mobile with `MagicShieldAbsorb > 0` newly enters another player's view, resend once so latecomers see the shield already in place (hook alongside wherever the standard HP update already gets (re)sent on enter-view).

## Testing

`Projects/UOContent.Tests/Tests/Custom/MagicShieldTests.cs` (xUnit, `[Fact]`, `Collection("Sequential UOContent Tests")` — no tiledata dependency, this is pure state/logic):

- `Apply` on a clean Mobile sets `MagicShieldAbsorb` to the requested points.
- `Apply` while a shield is already active replaces it (old points and timer gone, new points/duration in effect).
- `Absorb` with damage less than the pool: pool decreases by the damage amount, returned value is 0 (nothing reaches real HP).
- `Absorb` with damage exceeding the pool: pool goes to 0, returned value is the overflow (`damage - originalPool`).
- `Absorb` on a Mobile with no shield: returns the damage unchanged, no-op otherwise.
- Duration expiry: `Apply` with a duration, advance the timer wheel past it without dealing damage, `MagicShieldAbsorb` is back to 0.
- Melee/weapon damage path is untouched by any of this — no assertion needed here since `MagicShield.Absorb` is only ever called from `SpellHelper.Damage()`; a regression would show up as a `BaseWeapon`/melee test never calling it, not as a `MagicShieldTests` failure.

Packet encoding: a `MagicShieldPacketTests.cs` alongside the existing packet-encoding tests (mirrors `EquipmentPacketTests.cs`/`MobilePacketTests.cs` in `Server.Tests`) verifying the Serial+points byte layout.

## Open questions carried into implementation

- Exact free sub-command ID under `0xBF` — pick and record it in `CUSTOM_CHANGES.md` at implementation time.
- Purple hue/color values for the three ClassicUO draw surfaces are a client-side visual detail, tuned by eye — see the ClassicUO doc.

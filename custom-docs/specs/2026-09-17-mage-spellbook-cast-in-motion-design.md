# Mage's Spellbook — cast-in-motion for the remaining spells (design)

Extends the target-first casting flow (`custom-docs/specs/2026-09-14-target-first-casting-design.md`), already implemented and verified in game for `FlameStrikeSpell`, to the rest of the spells in `MageSpellbook` (`Items/Skill Items/Magical/MageSpellbook.cs`). Same effect for the player: the cast bar no longer roots them in place — the target cursor appears immediately, and the cast delay/mana/reagent cost only happens after they've clicked a target.

## Goal

Today, 12 of the 13 spells in the mage's book (everything except `FlameStrikeSpell`) use the standard flow: cast bar plays with the caster frozen in place, *then* the target cursor appears, click resolves instantly. Wanted: same target-first flow Flame Strike already has — cursor first (free, cancellable), then the delay/animation/cost, caster free to move throughout.

## Key finding: no engine changes needed

`SpellTarget<T>` (`Spells/Targeting/SpellTarget.cs`) is generic over `T` — the phase-1/phase-2 machinery (`OnTarget`, `ResolveTargetFirst`, range/LOS re-check, `ValidateTargetFirst`) already runs identically regardless of whether `T` is `Mobile`, `Item`, or `IPoint3D`. This was built once for `FlameStrikeSpell`'s `SpellTarget<Mobile>` but nothing in it is Mobile-specific. **Zero changes to `Spell.cs` or `SpellTarget.cs`** — every spell in scope gets the same four per-spell overrides `FlameStrikeSpell` already has, nothing more.

```csharp
public override bool TargetFirst => true;
public override bool BlocksMovement => false;
public override bool BlocksWeaponSwing => TargetFirstCommitted;
public override bool ValidateTargetFirst(object target) => /* per spell, see table */;
```

Plus `notifyOnLos: true` added to the `SpellTarget<T>` constructor call in each spell's `OnCast()` (today only `FlameStrikeSpell` passes this — extending it everywhere in scope for a consistent "Target can not be seen." message instead of the silent-ignore default; see decision log below).

## Scope

| Spell | Target type | In scope |
|---|---|---|
| Magic Arrow, Fireball, Lightning, Energy Bolt, Poison | `Mobile` (harmful) | Yes |
| Heal | `Mobile` (beneficial) | Yes |
| Magic Lock | `Item` | Yes |
| Unlock, Fire Field, Paralyze Field, Reveal | `IPoint3D` | Yes |
| Flame Strike | `Mobile` (harmful) | Already done — reference only, no change |
| Gate Travel | N/A — uses `RecallSpellTarget`, not `SpellTarget<T>` | **Out of scope.** Would need the phase-1/phase-2 logic duplicated into a second, unrelated `Target` subclass (rune/runebook/boat-key/house-deed handling) — real new engineering, not a copy of the existing pattern. Stays classic pre-cast for now. |
| Magic Reflection | Being redesigned (target-on-others reflect shield) | **Out of scope.** Gets its own design doc; will pick up `TargetFirst` as part of that redesign, not this change. |

11 spell files change. `Spellbook.cs`, `MageSpellbook.cs`, `Spell.cs`, `SpellTarget.cs` are untouched — no risk to any other spellbook/school that doesn't use this flow.

## `ValidateTargetFirst` per spell group

Mirrors the same principle as Flame Strike: a cheap, free rejection at the phase-1 click (wrong target type / obviously illegal target) using the engine's own existing gate methods and messaging. The deeper, spell-specific validity checks (chest already locked, town restrictions, dead/animated-dead for Heal, etc.) are **not** duplicated here — they stay exactly where they are today, inside each spell's `Target()` method, which still runs in full at the end of phase 2. A rejection there still charges mana/reagents, same as it does for Flame Strike today.

| Group | Spells | `ValidateTargetFirst` |
|---|---|---|
| Harmful, Mobile | Magic Arrow, Fireball, Lightning, Energy Bolt, Poison | `target is Mobile m && Caster.CanBeHarmful(m, true)` |
| Beneficial, Mobile | Heal | `target is Mobile m && Caster.CanBeBeneficial(m, true)` |
| Item | Magic Lock | `target is LockableContainer` |
| Ground, IPoint3D | Unlock, Fire Field, Paralyze Field, Reveal | `true` — any point is a legal aim; the real checks (`CheckTown`, lock state, etc.) stay in `Target()` |

## Decisions locked with the user

- **`BlocksWeaponSwing`**: same rule for all 11 spells, no exceptions — `TargetFirstCommitted`. Caster can swing a weapon freely during phase 1 (cursor open, nothing committed) but not during phase 2 (from the click until the spell resolves or fizzles), identical to Flame Strike today. Considered and rejected: leaving the non-offensive spells (Heal, Magic Lock, Unlock, Fire Field, Paralyze Field, Reveal) free to swing throughout, like Chivalry spells — explicitly declined in favor of one uniform rule across the whole book.
- **`notifyOnLos`**: extended to all 11 spells (was Flame-Strike-only, deliberately scoped tight when that feature shipped). Every spell in this book now sends "Target can not be seen." on a LOS-blocked click instead of silently ignoring it.
- **Poison's distance-dependent level**: distance is measured at final resolution (end of phase-2 delay), not at the click — consistent with how `DelayedDamage` already works for the other offensive spells in this book (Magic Arrow, Fireball, Energy Bolt, Flame Strike all resolve against the target's state at the end of the delay, not at cast time).

## Components touched

| File | Change |
|---|---|
| `Spells/First/Heal.cs` | `TargetFirst`, `BlocksMovement`, `BlocksWeaponSwing`, `ValidateTargetFirst`, `notifyOnLos: true` |
| `Spells/First/MagicArrow.cs` | same four members |
| `Spells/Third/Fireball.cs` | same four members |
| `Spells/Third/MagicLock.cs` | same four members |
| `Spells/Third/Poison.cs` | same four members |
| `Spells/Third/Unlock.cs` | same four members |
| `Spells/Fourth/FireField.cs` | same four members |
| `Spells/Fourth/Lightning.cs` | same four members |
| `Spells/Sixth/EnergyBolt.cs` | same four members |
| `Spells/Sixth/ParalyzeField.cs` | same four members |
| `Spells/Sixth/Reveal.cs` | same four members |

All 11 are pre-existing upstream files — log each in `CUSTOM_CHANGES.md` per the usual convention.

## Testing plan

Automated: extend the existing `TargetFirstCastingTests.cs` pattern with one case per target-type category not yet exercised by the Flame Strike tests — a harmful-Mobile spell (already covered by Flame Strike's own suite, but add one more to confirm the pattern generalizes), a beneficial-Mobile spell (Heal), an Item-target spell (Magic Lock), and an IPoint3D ground-target spell (one of Fire Field/Paralyze Field/Reveal/Unlock). This is about proving the *generic* engine behaves correctly for `T` types it has never been exercised with in `TargetFirst` mode before — not re-testing logic already covered for Mobile.

Manual in-game verification: per this repo's discipline (`LAVORI_IN_CORSO.md`), each of the 11 spells gets its own checklist entry — happy path, phase-1 free cancel/rejection, phase-2 committed failure (out of range/LOS/invalid target), phase-2 disturb (hit while delay is running) — before any of them is considered done.

## Out of scope (for now)

- **Gate Travel** — stays classic pre-cast. Revisit only as an explicit future step if wanted.
- **Magic Reflection** — being redesigned into a targetable reflect-shield (separate design doc); will get `TargetFirst` as part of that work, not here.
- Any change to `Disturb()`'s global behavior, or to the target-first mechanism itself — this change only consumes the existing mechanism, per spell, exactly as already built and verified.

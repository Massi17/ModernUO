# Magic Reflection — targetable reflect shield (design)

Redesigns `MagicReflectSpell` (`Spells/Fifth/MagicReflect.cs`) from a self-only passive resistance buff into a targetable, single-use reflect shield: cast it on yourself or anyone else (friend or foe), the next harmful spell that lands on them bounces back onto whoever cast it instead, recalculated against the reflecting party's own resistances. Replaces the AOS passive bonus (-25 physical / +10 all elemental resist, indefinite duration) entirely — the shield is now the spell's only effect.

## Player-facing behavior

1. Cast Magic Reflection, click any Mobile (self, ally, or hostile — no `CanBeBeneficial`/`CanBeHarmful` gate on the target relationship). They receive a reflect shield.
2. The shield is **single-use**: the next harmful spell that hits the shielded Mobile doesn't damage/affect them — it bounces back onto its original caster instead, with damage/effects recalculated against *that caster's* own resistances (not the original target's). The shield breaks the instant this happens.
3. **Double-shield case**: if the original caster (the one about to have the spell bounced back onto them) *also* has an active reflect shield at that moment, both shields break and the spell resolves against **neither** party — it simply vanishes.
4. If never triggered, the shield expires on its own after **5 minutes**.
5. **Future extension point (not built yet):** a caster can be flagged to pierce any reflect shield outright — their harmful spells always land on the original target even if shielded, and the target's shield still breaks as if it had reflected. No ability grants this flag today; it exists so a future PvP class evolution can flip it without touching this spell's code again. See "Piercing hook" below.

## Scope

Reflects the **21 existing spells** across Magery/Necromancy/Mysticism that already call `SpellHelper.CheckReflect` (not just the 6 in the mage's spellbook — `grep -rl CheckReflect Projects/UOContent/Spells` lists all 21). Explicitly out of scope for this change:

- **Fire Field / Paralyze Field** — apply damage/paralysis directly in `FireFieldItem.OnMoveOver`/`InternalTimer` and `ParalyzeField.OnMoveOver`, bypassing `CheckReflect` entirely. Not touched.
- **Creatures** — `BaseCreature.CheckReflect(Mobile caster, ref bool reflect)` is separate, existing, per-creature reflect logic (e.g. liches). The new shield only applies to player-controlled Mobiles for now; creature reflect behavior is unmodified.
- **The piercing ability itself** — only the hook is built now (see below); the PvP evolution that uses it is a separate, future design.

## State

`Mobile.SpellReflectActive` — new `bool` property added directly to `Projects/Server/Mobiles/Mobile.cs`, same pattern as the existing `Mobile.MagicShieldAbsorb` (explicit go-ahead given to touch `Projects/Server/` for this, mirroring that precedent). Runtime-only, not serialized (the shield is short-lived — a world save mid-shield simply loses it, same as the current passive buff already behaves on logout per its own comment about surviving relog, which this redesign does not preserve).

`Server.Custom.SpellReflect` — new static class in `Projects/UOContent/Custom/`, same shape as `Server.Custom.MagicShield`:

```csharp
public static class SpellReflect
{
    public static void Apply(Mobile m, TimeSpan duration); // sets SpellReflectActive, buff icon, starts expiry timer
    public static void Clear(Mobile m);                    // breaks the shield, removes buff icon, cancels expiry timer
    public static bool IsActive(Mobile m);
}
```

Reuses the existing `BuffIcon.MagicReflection` enum member for the buff bar — no new icon needed.

## The reflect hook

`SpellHelper.CheckReflect` gains a third outcome alongside "not reflected" / "reflected":

```csharp
public enum ReflectResult { None, Reflected, Vanished }

public static ReflectResult CheckReflect(int circle, ref Mobile caster, ref Mobile target)
```

- **`None`** — no shield on target (or target's a creature — creature `CheckReflect` path unchanged, still runs first/separately). Caller proceeds exactly as today.
- **`Reflected`** — target had a shield; it's consumed; `(caster, target)` are swapped exactly as the existing pre-AOS reflect logic already does — this is what makes "recalculated on the reflecting party's own resistances" free: every caller already computes damage/effects *after* this swap, using whichever Mobile ends up in the `target`/`m` variable.
- **`Vanished`** — both caster and target had active shields; both are consumed; caller must skip applying the spell's effect entirely.

**Piercing hook** — checked first, before either shield: `Mobile.PiercesSpellReflect` (new `bool` property, same file/same pattern as `SpellReflectActive`, default `false`, nothing sets it today). If the caster has it set and the target has an active shield: target's shield still breaks (`Clear`), but no swap happens — `CheckReflect` returns `None` and the spell lands on the original target normally. Zero behavior change until something outside this spec's work sets the flag.

### The 21 call sites

Each gets one extra guard line right after its existing `CheckReflect(...)` call:

```csharp
if (SpellHelper.CheckReflect((int)Circle, ref source, ref m) == ReflectResult.Vanished)
{
    return; // both shields broke, spell resolves against nobody
}
```

Placed before any damage/effect computation — every one of the 21 already calls `CheckReflect` before computing its effect, so this is a mechanical, one-line addition per file, not a restructure. Files: `MindBlast.cs`, `Paralyze.cs`, `Clumsy.cs`, `Feeblemind.cs`, `MagicArrow.cs`, `Weaken.cs`, `Curse.cs`, `Lightning.cs`, `ManaDrain.cs`, `BombardSpell.cs`, `EagleStrikeSpell.cs`, `SpellPlagueSpell.cs`, `PainSpike.cs`, `Strangle.cs`, `Harm.cs`, `FlameStrike.cs`, `ManaVampire.cs`, `EnergyBolt.cs`, `Explosion.cs`, `Fireball.cs`, `Poison.cs`.

## `MagicReflectSpell` itself

Becomes another target-first spell, consistent with the rest of the book (`custom-docs/specs/2026-09-17-mage-spellbook-cast-in-motion-design.md`):

```csharp
public override bool TargetFirst => true;
public override bool BlocksMovement => false;
public override bool BlocksWeaponSwing => TargetFirstCommitted;
public override bool ValidateTargetFirst(object target) => target is Mobile;
```

No `CanBeBeneficial`/`CanBeHarmful` check — any Mobile is a legal target, per the earlier decision. `OnCast()` opens `SpellTarget<Mobile>(this, TargetFlags.None, notifyOnLos: true)` — neutral cursor (not harmful-red or beneficial-blue), since the target relationship is deliberately unrestricted. On a successful cast: `SpellReflect.Apply(target, TimeSpan.FromMinutes(5))`.

The old AOS branch in `OnCast()` (resistance mods, `_table` dictionary, `EndReflect` player-deleted cleanup) is deleted entirely, along with the pre-AOS/UOR branches (`MagicDamageAbsorb`, `DefensiveSpell`). Confirmed with the user: no era gating — the new shield is the spell's only behavior regardless of `Core.Expansion`, replacing all three of today's era branches, not just the AOS one.

## Components touched

| File | Change |
|---|---|
| `Projects/Server/Mobiles/Mobile.cs` | `SpellReflectActive`, `PiercesSpellReflect` bool properties |
| `Projects/UOContent/Custom/SpellReflect.cs` | new — `Apply`/`Clear`/`IsActive`, expiry timer, buff icon notify |
| `Projects/UOContent/Spells/Base/SpellHelper.cs` | `CheckReflect` return type becomes `ReflectResult`; piercing check added before shield check |
| `Projects/UOContent/Spells/Fifth/MagicReflect.cs` | full rewrite — target-first, calls `SpellReflect.Apply`, old era-branching effect code removed |
| 21 spell files listed above | one-line `Vanished` guard added after their existing `CheckReflect` call |

## Testing plan

Automated: shield applied + consumed on a normal reflect (damage lands on original caster, recalculated against their resist — assert the number differs from what the original target would've taken if their resist differs); double-shield case (assert neither Mobile takes damage, both shields clear); 5-minute expiry (shield gone, `IsActive` false, no reflect on a subsequent hit); piercing flag (shield still breaks, but damage lands on the original target, not swapped) — this one exercises the hook even though nothing sets the flag in production yet, so it doesn't silently rot.

Manual in-game verification per `LAVORI_IN_CORSO.md`: cast on self, cast on another player, get hit and confirm the caster takes the damage instead, double-shield scenario between two players, expiry after 5 minutes of no trigger, buff icon appears/disappears correctly, target-first cursor behaves like the rest of the book (move freely in phase 1, blocked weapon swing in phase 2).

## Out of scope

- Fire Field / Paralyze Field reflect (would need the same treatment inside `FireFieldItem`/`ParalyzeField`, not part of this change).
- Creature-targetable shields.
- The PvP class evolution ability that will eventually set `PiercesSpellReflect` — only the flag and the engine's respect for it are built here.
- Visual/message feedback beyond reusing the existing `FixedEffect`/buff icon — exact wording and any new effects are an implementation detail to settle during the build, not a design decision.

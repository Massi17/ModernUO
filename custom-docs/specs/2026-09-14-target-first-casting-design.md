# Target-first casting (design)

Reusable alternative spell-casting flow — target selection happens immediately, the cast delay happens after, and once the caster has committed to a target the cost is paid no matter the outcome. Tested first on `FlameStrikeSpell`; built so any future spell can opt in.

## Goal

Today (and in every other spell): cast → animation + cast delay → target cursor appears → click → instant resolution.

Wanted: cast → target cursor appears immediately → click → **then** animation + cast delay → resolution.

## Baseline: today's flow (`Spell.cs`, `Spells/Targeting/SpellTarget.cs`)

1. `Spell.Cast()` — gates on mana/state, plays mantra + hand animation, starts `_castTimer` for `GetCastDelay()`.
2. `_castTimer` fires → `State = Sequencing`, `Caster.NextSpellTime` set (cast recovery), `OnCast()` runs → for `FlameStrikeSpell` this sets `Caster.Target = new SpellTarget<Mobile>(...)`.
3. Player clicks → `SpellTarget<T>.OnTarget` calls `_spell.Target(o as T)` directly and synchronously → `CheckHSequence`/`CheckSequence` deducts mana + reagents, rolls the fizzle check, applies the effect — all in the same instant.
4. `OnTargetFinish` → `FinishSequence()`.

Interruption (`Spell.Disturb()`) during step 1–2 stops `_castTimer`, resets `State`, sets `NextSpellTime` via `GetDisturbRecovery()`. **No mana/reagent cost on disturb** — nothing has been deducted yet at that point.

`SpellTarget<T>`/`Target` already validate **range** and **line of sight** before `OnTarget` even fires (`Target.cs`: `OnTargetOutOfRange`, `CheckLOS`) — this is existing, free behavior for every spell, harmful or not.

## New flow: `TargetFirst = true`

A new virtual flag on `Spell`:

```csharp
public virtual bool TargetFirst => false; // opt-in, every other spell unaffected
```

### Phase 1 — cast to target click (free, cancellable)

1. `Cast()`: same initial mana/state gating as today (`Caster.Mana >= requiredMana`), **plus a new reagent-availability check** — the cursor must not appear at all if the caster can't actually pay for the spell. Today's `Cast()` only gates on mana; reagents aren't checked until `CheckSequence()`, which for a `TargetFirst` spell wouldn't run until phase 2. `ConsumeReagents()` checks and consumes in the same call (`Container.ConsumeTotal`), so a new non-consuming sibling is needed — a `HasReagents()` check built on `Container.GetAmount()` (which just reads a quantity, no side effect), mirroring `ConsumeReagents()`'s own bypass conditions (scroll/wand in use, non-player caster, Lower Reagent Cost roll, free-consume duel rule) so the two stay in agreement. `Cast()`'s gate becomes `Caster.Mana >= requiredMana && HasReagents()` for `TargetFirst` spells. Skips mantra, hand animation, and `_castTimer`. Calls `OnCast()` immediately/synchronously, showing the target cursor. `State` stays `Casting`.
2. Cancelling or being disturbed here is free — nothing has been committed.
3. On click, before anything else: range + LOS are already validated for free by the base `Target` class (unchanged). A new per-spell hook is also checked:

   ```csharp
   public virtual bool ValidateTargetFirst(object target) => true; // default: no extra check
   ```

   `FlameStrikeSpell` overrides it as `target is Mobile m && Caster.CanBeHarmful(m, true)` — reuses the engine's own "can't harm that" checks and messaging, no new message text needed.

   If this fails: message already sent by `CanBeHarmful`, spell ends here, **no cost** — phase 1 stays free.

### Phase 2 — target click to resolution (committed, costs on any outcome)

4. Mantra + hand animation play now (moved from `Cast()` to here). A timer for `GetCastDelay()` starts — reuses the existing `_castTimer` field, so `Disturb()`'s existing `_castTimer?.Stop()` logic cancels it with no changes there.
5. **Any interruption from here on charges the cost.** `Disturb()` gains a check: if `TargetFirst` and phase 2 has started (new field, e.g. `_targetFirstCommitted`), consume mana + reagents (see shared helper below) before doing the normal reset. No effect is applied — just the cost.
6. When the timer fires: re-run the same three checks (range, LOS, `ValidateTargetFirst`) — the target may have moved, died, or broken LOS during the delay.
   - Any of the three fails → **cost is still charged**, spell fails, message matches the specific reason:
     - Out of range → `500446` ("That is too far away.")
     - Out of LOS → `500237` ("Target can not be seen.")
     - No longer valid (dead/etc.) → whatever `ValidateTargetFirst`'s own check sends (for FlameStrike, `CanBeHarmful`'s message)
   - All pass → cost is charged, fizzle-check + effect resolve exactly as `CheckSequence`/`Target()` do today.

### Shared resource-consumption helper

`CheckSequence()`'s mana-deduction + reagent/scroll/wand-consumption block gets extracted into its own method (e.g. `protected void ConsumeCastingResources()`), left as-is otherwise. Called from:
- `CheckSequence()`'s existing success path (unchanged behavior for every spell).
- The three new phase-2 outcomes above for `TargetFirst` spells: success, resolution-time validity failure, and mid-phase-2 disturb.

Scope note: this helper only covers mana + reagents/scroll/wand charge — it deliberately does **not** pull in `CheckSequence()`'s other success-only side effects (karma award, vampiric-embrace garlic burn, hand-clearing). Those stay success-only, since only mana/reagents were asked to be charged unconditionally.

**Do not call the new helper on the success path.** When phase 2's checks all pass, resolution still goes through the spell's own `Target(o)` exactly as it does today (for `FlameStrikeSpell`: `CheckHSequence` → `CheckSequence`, which already deducts mana/reagents internally). Calling `ConsumeCastingResources()` there too would double-charge. The new helper exists purely for the two outcomes where the spell's own `Target()` never gets called: resolution-time validity failure, and a mid-phase-2 disturb.

Edge case: if the caster no longer has enough mana/reagents by the time phase 2 resolves (e.g. drained mid-delay), charge what's being asked is ambiguous by construction — treat it the same as today's existing "insufficient mana/reagents" messages inside `CheckSequence`, without a new message. Flagged here so it isn't silently glossed over during implementation.

## Components touched

| File | Change |
|---|---|
| `Projects/UOContent/Spells/Base/Spell.cs` | `TargetFirst` flag; new `HasReagents()` non-consuming check, folded into `Cast()`'s pre-cursor gate; `Cast()` branches on `TargetFirst`; new `ValidateTargetFirst` virtual hook; extracted `ConsumeCastingResources()` helper; `Disturb()` charges cost when a `TargetFirst` spell is mid-phase-2 |
| `Projects/UOContent/Spells/Targeting/SpellTarget.cs` | `OnTarget` branches on `TargetFirst`: defers to phase-2 timer instead of resolving immediately; re-validates range/LOS/`ValidateTargetFirst` when that timer fires |
| `Projects/UOContent/Spells/Seventh/FlameStrike.cs` | `override bool TargetFirst => true;`, `override bool ValidateTargetFirst(...)` |

No other spell's behavior changes — `TargetFirst` defaults to `false` everywhere else.

## State/interrupt reuse

Deliberately reuses existing machinery instead of building parallel state tracking:
- `State` stays `SpellState.Casting` through both phases, so `IsCasting`/`Disturb()`'s existing gating keeps working unmodified.
- Phase 2's timer reuses the `_castTimer` field, so `Disturb()`'s existing `_castTimer?.Stop()` cancels it for free.
- Only genuinely new pieces: the `TargetFirst` flag, the `ValidateTargetFirst` hook, the phase-tracking field for "has phase 2 started" (needed so `Disturb()` knows whether to charge cost), and the resolution-time range/LOS/validity re-check.

## Testing plan

Per this repo's own discipline (`custom-docs/MANUALE.md`, `LAVORI_IN_CORSO.md`): `dotnet build` clean is necessary but not sufficient. This needs actual in-game verification (server running, a character with Magery, a valid target) before it's considered done — happy path (cast → click → wait → hit), the three click-time rejections (out of range/LOS/dead — no cost), the three resolution-time failures (out of range/LOS/dead after the delay — cost charged), and a mid-phase-2 interrupt (cost charged, no effect). Log the file changes in `CUSTOM_CHANGES.md` per the usual convention (this modifies pre-existing files: `Spell.cs`, `SpellTarget.cs`, `FlameStrike.cs`).

## Out of scope (for now)

- Applying `TargetFirst` to any spell besides `FlameStrikeSpell`. The flag is built to be reusable, but no other spell is being switched over yet — that's an explicit future step, not part of this change.
- Any change to the global `Disturb()` behavior for non-`TargetFirst` spells — confirmed explicitly out of scope.

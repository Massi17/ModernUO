# Interrupting a cast to start a new one (design)

Generalizes the target-first mechanism (`custom-docs/specs/2026-09-14-target-first-casting-design.md`) so it applies to *any* spell whenever it's used to interrupt an already-in-progress cast — not just spells with `TargetFirst = true`.

## Goal

Today: casting a spell while another is already mid-cast (animation/delay playing, before its own target cursor appears) is fully blocked — message 502642 ("You are already casting a spell.").

Wanted: it isn't blocked. The new spell's cursor appears immediately. Clicking a valid target with it fizzles the old spell (charging its mana/reagents, since it hadn't taken effect yet) and the new spell then proceeds normally from that click — its own mantra, animation, and cast delay, then resolution.

## Mechanism

The target-first flow already built for `FlameStrikeSpell` is exactly this shape (cursor first, real casting deferred to after a click). This feature reuses it rather than building a second one: a cast now uses the deferred flow if `TargetFirst` is true **or** it's interrupting an in-progress cast. A new field remembers which spell is being interrupted, and the interrupted spell is fizzled at the moment the new spell's target click commits — not when the new spell is merely pressed.

```csharp
private Spell _interruptedSpell;

public bool UsesDeferredCast => TargetFirst || _interruptedSpell != null;
```

Every place the phase machinery currently checks `TargetFirst` (`Cast()`'s branch, `SpellTarget<T>.OnTarget`'s branch) checks `UsesDeferredCast` instead. `TargetFirstCommitted`/`_targetFirstCommitted` need no change — they already mean "phase 2 of the deferred flow is active" regardless of why the flow started.

## `Cast()`: stop blocking, remember instead

The `isCasting` block (non-wand case only — a wand-triggered attempt keeps today's "frozen" message unchanged) no longer blocks. It captures the in-progress spell and lets the rest of `Cast()`'s gating (mana, reagents, paralysis, etc.) run exactly as it would for a fresh cast:

```csharp
var isCasting = Caster.Spell?.IsCasting == true;
var isWand = Scroll is BaseWand;

Spell interruptedSpell = null;
if (isCasting && !isWand)
{
    interruptedSpell = Caster.Spell as Spell;
}
else if (isCasting) // isWand
{
    Caster.SendLocalizedMessage(502643); // You can not cast a spell while frozen.
    return false;
}
```

This is the current (post target-first-fix) three-way gate in `Cast()`, and the two spots that need to change:

```csharp
if (Caster.Mana < requiredMana)
{
    // unchanged
}
else if (TargetFirst && !HasReagents())              // → (TargetFirst || interruptedSpell != null) && !HasReagents()
{
    Caster.LocalOverheadMessage(MessageType.Regular, 0x22, 502630);
}
else
{
    if (Caster.Spell == null && Caster.CheckSpellCast(this) && CheckCast() &&   // → (Caster.Spell == null || interruptedSpell != null) && ...
        Caster.Region.OnBeginSpellCast(Caster, this))
    {
        State = SpellState.Casting;
        Caster.Spell = this;

        if (TargetFirst)   // → if (TargetFirst || interruptedSpell != null)
        {
            ...
        }
    }
}
```

The reagent-gate check and the success-block guard both still read the *local* `interruptedSpell` variable, not `_interruptedSpell` — that field isn't set until inside the success block, alongside the existing `State = SpellState.Casting; Caster.Spell = this;`:

```csharp
State = SpellState.Casting;
Caster.Spell = this;
_interruptedSpell = interruptedSpell;
```

`UsesDeferredCast` (the `TargetFirst || _interruptedSpell != null` property) becomes readable from this point on — used by the phase-1 branch's condition (`if (TargetFirst)` → `if (UsesDeferredCast)`) and by `SpellTarget<T>.OnTarget`'s branch condition later.

## Fizzling the interrupted spell at commit

`BeginTargetFirstDelay` (called from `SpellTarget<T>.OnTarget` once the new spell's target validates) is where phase 2 begins for any deferred cast — this is the commit point, so it's where the interrupted spell gets disturbed:

```csharp
public void BeginTargetFirstDelay(Action onResolve)
{
    if (_interruptedSpell != null)
    {
        var interrupted = _interruptedSpell;
        _interruptedSpell = null;
        interrupted.Disturb(DisturbType.NewCast);
    }

    SayMantra();
    // ... unchanged from here ...
}
```

`Disturb()` needs one addition so this actually charges the interrupted spell. Today it only charges when the disturbed spell is a target-first spell that had already committed (`_targetFirstCommitted`). Add: when the reason is specifically `DisturbType.NewCast` and the spell hadn't committed to anything of its own, it still pays — but only if it isn't itself a target-first spell just sitting on its own free, uncommitted cursor (that case stays free, unchanged, per the target-first spec):

```csharp
if (wasCasting)
{
    _castTimer?.Stop();
    _animTimer?.Stop();
    Caster.NextSpellTime = Core.TickCount + (int)GetDisturbRecovery().TotalMilliseconds;

    if (_targetFirstCommitted)
    {
        _targetFirstCommitted = false;
        ConsumeCastingResources();
    }
    else if (TargetFirst)
    {
        Target.Cancel(Caster);
    }
    else if (type == DisturbType.NewCast)
    {
        ConsumeCastingResources();
    }
}
```

This produces exactly the three behaviors already confirmed:
- A normal spell (not `TargetFirst`, not committed) bumped by a new cast → pays (new `else if (type == DisturbType.NewCast)` branch).
- A `TargetFirst` spell sitting on its own uncommitted cursor, bumped by anything (a new cast or otherwise) → free, cursor cancelled (existing `else if (TargetFirst)` branch, unchanged).
- A `TargetFirst` spell already committed to its own target, bumped by anything → pays (existing `if (_targetFirstCommitted)` branch, unchanged).

## Required defensive fix

`Disturb()` currently sets `Caster.Spell = null;` unconditionally. That's harmless today because nothing else could be occupying `Caster.Spell` when a spell gets disturbed. It's no longer harmless: by the time `_interruptedSpell.Disturb(...)` runs (inside the new spell's `BeginTargetFirstDelay`), `Caster.Spell` already points at the *new* spell. An unconditional null-out would wipe that out too. Guard it the same way `FinishSequence()` already guards its own `Caster.Spell = null`:

```csharp
if (Caster.Spell == this)
{
    Caster.Spell = null;
}
```

This is a correctness requirement for this feature, not optional polish.

## Chained interrupts

Fireball casting → Lightning interrupts it (Fireball remembered, Lightning's cursor up) → before clicking Lightning's target, Magic Arrow interrupts *Lightning* too. Lightning's own click will now never come (its cursor was just replaced by Magic Arrow's), so if nothing else happens, Fireball's pending punishment is never delivered — it would sit forever uncharged, and the player would learn that stacking cast-attempts fast enough dodges cost entirely except for the very last one.

Resolution: when a spell holding its own pending `_interruptedSpell` is itself interrupted, settle that pending obligation immediately, right there, rather than letting it evaporate.

Placement matters: this must happen only once the *new* cast (Magic Arrow) has actually passed all of its own gates and committed — not the instant `interruptedSpell` is captured. If Magic Arrow's own attempt fails (insufficient mana, paralyzed, etc.), nothing about Lightning-interrupting-Fireball should change; Fireball must stay exactly as pending as it was. So this goes in the success block, alongside `_interruptedSpell = interruptedSpell;`, not earlier in `Cast()`:

```csharp
_interruptedSpell = interruptedSpell;

if (interruptedSpell?._interruptedSpell != null)
{
    var chained = interruptedSpell._interruptedSpell;
    interruptedSpell._interruptedSpell = null;
    chained.Disturb(DisturbType.NewCast);
}
```

Every spell that gets superseded is punished — either directly (when its own supersessor's target is clicked) or immediately (if its own supersessor gets superseded first, and only once that third cast genuinely commits).

## Testing

`dotnet build` + `dotnet test` (regression check). One targeted test mirroring the one written for the target-first critical bug: start casting spell A, cast spell B before A resolves, confirm A is *not yet* disturbed (still `IsCasting`, no mana/reagents deducted) until B's target is actually clicked — then confirm A is disturbed and charged at that point. Final sign-off is manual in-game verification, tracked in `custom-docs/LAVORI_IN_CORSO.md` as usual.

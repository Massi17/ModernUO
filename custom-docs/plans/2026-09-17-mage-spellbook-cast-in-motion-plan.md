# Mage's Spellbook — Cast-in-Motion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give 11 of the 13 spells in `MageSpellbook` (all except the already-converted `FlameStrikeSpell` and the out-of-scope `MagicReflectSpell`/`GateTravelSpell`) the same target-first casting flow — target cursor appears immediately, caster free to move, cast delay/cost only commits once a target is clicked.

**Architecture:** Each of the 11 spells gets the same four-member override shape already proven on `FlameStrikeSpell` and `MagicReflectSpell` (`TargetFirst`, `BlocksMovement`, `BlocksWeaponSwing`, `ValidateTargetFirst`) plus `notifyOnLos: true` on its `SpellTarget<T>` constructor call in `OnCast()`. `SpellTarget<T>`'s phase-1/phase-2 machinery is already generic over `T` (Mobile/Item/IPoint3D) — zero engine changes. A test-fixture hazard is fixed first: two existing test files use `MagicArrowSpell` as their stand-in for "an ordinary, non-target-first spell," which this plan is about to make false.

**Tech Stack:** C# / .NET 10, xUnit (`[Fact]`), ModernUO's existing `SpellTarget<T>`/`TargetFirstCommitted` test patterns.

**Spec:** `custom-docs/specs/2026-09-17-mage-spellbook-cast-in-motion-design.md`. Also relevant (already implemented, not touched by this plan): `custom-docs/specs/2026-09-14-target-first-casting-design.md` (the mechanism being reused) and `Projects/UOContent/Spells/Fifth/MagicReflect.cs` (a second real, merged example of the same override shape, including how `ValidateTargetFirst` should send its own rejection message when the engine doesn't already do it for you).

## Global Constraints

- **No changes to `Spell.cs` or `SpellTarget.cs`** — the spec's key finding is that the existing generic engine needs nothing new. If any task's implementer finds itself wanting to touch either file, that's a signal to stop and escalate, not proceed.
- **Exact four-member shape, same order, on every one of the 11 spells** (matches the order already used by `FlameStrikeSpell`/`MagicReflectSpell`):
  ```csharp
  public override bool BlocksMovement => false;

  public override bool TargetFirst => true;

  public override bool BlocksWeaponSwing => TargetFirstCommitted;

  public override bool ValidateTargetFirst(object target) => /* per spell, see each task */;
  ```
  Inserted immediately after the spell's last existing property (`Circle`, `DelayedDamage`, `TargetRange`, whichever comes last) and before its `Target(...)` method.
- **`BlocksWeaponSwing` is always `TargetFirstCommitted`, no exceptions** — this was explicitly decided and locked with the user (rejected: leaving non-offensive spells free to swing throughout).
- **`notifyOnLos: true` on every `SpellTarget<T>` constructor call in every one of the 11 spells' `OnCast()`** — extends what was previously Flame-Strike-only.
- **`ValidateTargetFirst` per spell, exactly as the spec's table specifies** — do not invent stricter or looser checks:
  - Harmful Mobile (Magic Arrow, Fireball, Lightning, Energy Bolt, Poison): `target is Mobile m && Caster.CanBeHarmful(m, true)`
  - Beneficial Mobile (Heal): `target is Mobile m && Caster.CanBeBeneficial(m, true)`
  - Item (Magic Lock): `target is LockableContainer`
  - Ground/IPoint3D (Unlock, Fire Field, Paralyze Field, Reveal): `true`
- **Gate Travel and Magic Reflection are untouched** — explicitly out of scope per the spec.
- **No new `using` statements needed anywhere.** Every member touched (`TargetFirst`, `BlocksMovement`, `BlocksWeaponSwing`, `TargetFirstCommitted`, `ValidateTargetFirst`) is inherited from the base `Spell`/`MagerySpell` class, already in scope in every one of these files. `LockableContainer` (needed for Magic Lock's `ValidateTargetFirst`) is already imported there via `using Server.Items;`.
- **Test-fixture hazard (Task 1 exists specifically for this):** `MagicArrowSpell` is used in `Projects/UOContent.Tests/Tests/Spells/CastInterruptRecastTests.cs` (14 occurrences across 8 test methods) and `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs` (1 occurrence) purely as a convenient stand-in for "a plain, non-target-first spell" — contrasted against `FlameStrikeSpell`. One of those usages (`UsesDeferredCast_TrueForTargetFirst_FalseOtherwise`) asserts `Assert.False(magicArrow.UsesDeferredCast)` directly — this becomes a **hard test failure** the moment `MagicArrowSpell.TargetFirst => true` lands, unless fixed first. Task 1 swaps every such usage to `ClumsySpell` (First-circle, harmful-Mobile, plain Magery spell, not in this plan's scope, already imported via the same `using Server.Spells.First;` both files already have) before Task 2 touches `MagicArrowSpell`'s own source file.
- **Baseline test failure, not this plan's concern:** `UOContent.Tests.Mobiles.AI.FamiliarAITests.HiddenCaster_FamiliarRefusesRetaliation` fails on current `main` before any commit in this plan — pre-existing, unrelated to spells/casting. Every task's "no regressions" check means no regressions **beyond** this one known failure.
- All test additions decorate their containing class with `[Collection("Sequential UOContent Tests")]` — already present on both files this plan touches or extends.

---

## Task 1: Fix the `MagicArrowSpell` test-fixture hazard

**Files:**
- Modify: `Projects/UOContent.Tests/Tests/Spells/CastInterruptRecastTests.cs`
- Modify: `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`

**Interfaces:** none — this is pure test-fixture maintenance, no production code touched. Task 2 depends on this being done first (it's about to make `MagicArrowSpell.TargetFirst => true`, which is exactly what these fixtures currently assume is false).

This task does **not** change what any test verifies — every assertion, comment, and test name stays as-is. It only swaps which spell class stands in for "an ordinary, non-target-first spell," since `MagicArrowSpell` is about to stop being one.

- [ ] **Step 1: Replace every `MagicArrowSpell` occurrence in `CastInterruptRecastTests.cs` with `ClumsySpell`**

In `Projects/UOContent.Tests/Tests/Spells/CastInterruptRecastTests.cs`, replace every occurrence of `MagicArrowSpell` with `ClumsySpell` (14 occurrences, across these lines — search for `MagicArrowSpell` to find them all):

```
Line 18:  var magicArrow = new MagicArrowSpell(caster);
Line 21:  Assert.False(magicArrow.UsesDeferredCast);
Line 35:  var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
Line 40:  var b = new MagicArrowSpell(caster);
Line 69:  var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
Line 72:  var b = new MagicArrowSpell(caster);
Line 104: var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
Line 110: var b = new MagicArrowSpell(caster);
Line 143: var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
Line 149: var b = new MagicArrowSpell(caster);
Line 153: var c = new MagicArrowSpell(caster);
Line 178: var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
Line 183: var b = new MagicArrowSpell(caster);
Line 212: var a = new MagicArrowSpell(caster) { State = SpellState.Casting };
Line 242: var b = new MagicArrowSpell(caster);
```

That's 15 lines (the count above said 14 occurrences of the class name but line 18/21 together are 2 references from one declaration — 15 total edit sites). For the one at lines 18/21 specifically, also rename the variable itself for clarity (it's the only one whose name references the spell):

```csharp
// Before:
var magicArrow = new MagicArrowSpell(caster);

Assert.True(flameStrike.UsesDeferredCast);
Assert.False(magicArrow.UsesDeferredCast);

// After:
var clumsy = new ClumsySpell(caster);

Assert.True(flameStrike.UsesDeferredCast);
Assert.False(clumsy.UsesDeferredCast);
```

Every other occurrence (the `a`/`b`/`c`-named ones) only needs its constructor's type changed — e.g. `var a = new MagicArrowSpell(caster) { State = SpellState.Casting };` becomes `var a = new ClumsySpell(caster) { State = SpellState.Casting };`. The variable names (`a`, `b`, `c`) already don't reference the spell name, so leave them as-is. No `using` change needed — `ClumsySpell` lives in `Server.Spells.First`, the same namespace this file already imports for `MagicArrowSpell`.

- [ ] **Step 2: Replace the one `MagicArrowSpell` occurrence in `TargetFirstCastingTests.cs` with `ClumsySpell`**

In `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`, inside `BlocksWeaponSwing_DefaultsToMirrorBlocksMovement` (around line 88), replace:

```csharp
var spell = new MagicArrowSpell(caster);
```

with:

```csharp
var spell = new ClumsySpell(caster);
```

Nothing else in this method changes — the comment above it ("Ordinary spells never decoupled BlocksWeaponSwing from BlocksMovement...") already describes the test's actual intent accurately once the subject spell is genuinely ordinary again. No `using` change needed, same reasoning as Step 1.

- [ ] **Step 3: Run the full test suite to confirm both files are still green**

Run: `dotnet test Projects/UOContent.Tests`
Expected: same pass/fail/skip counts as the pre-existing baseline (one known, unrelated `FamiliarAITests` failure, nothing else). This confirms the swap is behavior-preserving before Task 2 starts changing `MagicArrowSpell`'s actual source file.

- [ ] **Step 4: Commit**

```bash
git add Projects/UOContent.Tests/Tests/Spells/CastInterruptRecastTests.cs Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs
git commit -m "test: stop using MagicArrowSpell as the 'ordinary spell' fixture, it's about to become target-first"
```

---

## Task 2: Harmful-Mobile group (Magic Arrow, Fireball, Lightning, Energy Bolt, Poison)

**Files:**
- Modify: `Projects/UOContent/Spells/First/MagicArrow.cs`
- Modify: `Projects/UOContent/Spells/Third/Fireball.cs`
- Modify: `Projects/UOContent/Spells/Fourth/Lightning.cs`
- Modify: `Projects/UOContent/Spells/Sixth/EnergyBolt.cs`
- Modify: `Projects/UOContent/Spells/Third/Poison.cs`
- Modify (test): `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`

**Interfaces:**
- Consumes: `Spell.TargetFirst`/`BlocksMovement`/`BlocksWeaponSwing`/`TargetFirstCommitted`/`ValidateTargetFirst` (engine, already built — no changes needed), `Mobile.CanBeHarmful(Mobile, bool) -> bool` (engine).
- Produces: nothing new for later tasks — each of these 5 files is independent of the others in this plan.

All five get the identical four-member block. Only the insertion point (which existing property comes last) differs per file.

- [ ] **Step 1: `MagicArrow.cs`**

In `Projects/UOContent/Spells/First/MagicArrow.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.First;

        public override Type[] DelayedDamageSpellFamilyStacking => AOSNoDelayedDamageStackingSelf;

        public override bool DelayedDamage => true;

        public void Target(Mobile m)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.First;

        public override Type[] DelayedDamageSpellFamilyStacking => AOSNoDelayedDamageStackingSelf;

        public override bool DelayedDamage => true;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile m && Caster.CanBeHarmful(m, true);

        public void Target(Mobile m)
```

Then, in the same file's `OnCast()`, replace:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Mobile>(this, TargetFlags.Harmful);
        }
```

with:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Mobile>(this, TargetFlags.Harmful, notifyOnLos: true);
        }
```

- [ ] **Step 2: `Fireball.cs`**

In `Projects/UOContent/Spells/Third/Fireball.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public override bool DelayedDamage => true;

        public void Target(Mobile m)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public override bool DelayedDamage => true;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile m && Caster.CanBeHarmful(m, true);

        public void Target(Mobile m)
```

Then apply the same `OnCast()` change as Step 1 (add `, notifyOnLos: true` to the existing `new SpellTarget<Mobile>(this, TargetFlags.Harmful)` call).

- [ ] **Step 3: `Lightning.cs`**

In `Projects/UOContent/Spells/Fourth/Lightning.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Fourth;

        public override bool DelayedDamage => false;

        public void Target(Mobile m)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Fourth;

        public override bool DelayedDamage => false;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile m && Caster.CanBeHarmful(m, true);

        public void Target(Mobile m)
```

Then apply the same `OnCast()` change as Step 1.

- [ ] **Step 4: `EnergyBolt.cs`**

In `Projects/UOContent/Spells/Sixth/EnergyBolt.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Sixth;

        public override bool DelayedDamage => true;

        public void Target(Mobile m)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Sixth;

        public override bool DelayedDamage => true;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile m && Caster.CanBeHarmful(m, true);

        public void Target(Mobile m)
```

Then apply the same `OnCast()` change as Step 1.

- [ ] **Step 5: `Poison.cs`**

In `Projects/UOContent/Spells/Third/Poison.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public void Target(Mobile m)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile m && Caster.CanBeHarmful(m, true);

        public void Target(Mobile m)
```

Then apply the same `OnCast()` change as Step 1.

- [ ] **Step 6: Add the harmful-Mobile generalization test**

Per the spec's testing plan: confirm the target-first pattern generalizes to a plain damage spell beyond `FlameStrikeSpell`. Add this test to `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`, as the last method in the class (immediately before the closing `}` on the line after the final existing comment):

```csharp
    // Confirms the target-first pattern generalizes to an ordinary damage spell, not just
    // Flame Strike (which uses the pattern's own opt-in deferred-cost variant).
    [Fact]
    public void MagicArrow_BlocksWeaponSwing_OnlyOncePhase2Commits()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new MagicArrowSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing); // phase 1: not committed yet

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Harmful) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target); // click commits phase 2

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing); // phase 2: locked out until fizzle or hit

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        target.Delete();
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TargetFirstCastingTests"`
Expected: PASS (all tests in the file, including the new one and the existing ones from Task 1's fixture swap).

- [ ] **Step 8: Full build and full test suite run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions beyond the one known pre-existing `FamiliarAITests` failure. Pay particular attention to `CastInterruptRecastTests.cs` — Task 1 already decoupled it from `MagicArrowSpell`, but this is the first point where `MagicArrowSpell` itself actually becomes target-first, so a genuine, unexpected interaction would surface here.

```bash
git add Projects/UOContent/Spells/First/MagicArrow.cs Projects/UOContent/Spells/Third/Fireball.cs Projects/UOContent/Spells/Fourth/Lightning.cs Projects/UOContent/Spells/Sixth/EnergyBolt.cs Projects/UOContent/Spells/Third/Poison.cs Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs
git commit -m "feat: Magic Arrow, Fireball, Lightning, Energy Bolt, Poison become target-first (cast in motion)"
```

---

## Task 3: Heal (beneficial Mobile)

**Files:**
- Modify: `Projects/UOContent/Spells/First/Heal.cs`
- Modify (test): `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`

**Interfaces:**
- Consumes: `Mobile.CanBeBeneficial(Mobile, bool) -> bool` (engine).
- Produces: nothing new for later tasks.

Heal is its own task because its `ValidateTargetFirst` rule is `CanBeBeneficial`, not `CanBeHarmful`, and its `OnCast()` uses `TargetFlags.Beneficial`.

- [ ] **Step 1: Add the four-member override block**

In `Projects/UOContent/Spells/First/Heal.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.First;

        public void Target(Mobile m)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.First;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile m && Caster.CanBeBeneficial(m, true);

        public void Target(Mobile m)
```

- [ ] **Step 2: Update `OnCast()`**

In the same file, replace:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Mobile>(this, TargetFlags.Beneficial);
        }
```

with:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Mobile>(this, TargetFlags.Beneficial, notifyOnLos: true);
        }
```

- [ ] **Step 3: Add the beneficial-Mobile test**

Per the spec's testing plan: confirm target-first works with a beneficial (not harmful) target relationship. Add this test to `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`, as the last method in the class:

```csharp
    // Confirms target-first works for a beneficial-Mobile spell, not just harmful ones -
    // ValidateTargetFirst uses CanBeBeneficial here instead of CanBeHarmful.
    [Fact]
    public void Heal_TargetFirst_AcceptsABeneficialTarget()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new HealSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing);

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.Beneficial) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target);

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing);

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        target.Delete();
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TargetFirstCastingTests"`
Expected: PASS (all tests in the file).

- [ ] **Step 5: Full build and full test suite run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions beyond the known pre-existing `FamiliarAITests` failure.

```bash
git add Projects/UOContent/Spells/First/Heal.cs Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs
git commit -m "feat: Heal becomes target-first (cast in motion)"
```

---

## Task 4: Magic Lock (Item target)

**Files:**
- Modify: `Projects/UOContent/Spells/Third/MagicLock.cs`
- Modify (test): `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`

**Interfaces:**
- Consumes: `SpellTarget<Item>` (engine, already generic — this task is the first to actually exercise it in `TargetFirst` mode).
- Produces: nothing new for later tasks.

Magic Lock is its own task because it's the only spell in scope targeting an `Item` rather than a `Mobile`, and its test is the one that proves the generic engine's phase-1/phase-2 machinery genuinely works for `T = Item`, not just `T = Mobile`.

- [ ] **Step 1: Add the four-member override block**

In `Projects/UOContent/Spells/Third/MagicLock.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public void Target(Item item)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is LockableContainer;

        public void Target(Item item)
```

- [ ] **Step 2: Update `OnCast()`**

In the same file, replace:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Item>(this);
        }
```

with:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Item>(this, notifyOnLos: true);
        }
```

- [ ] **Step 3: Add the Item-target generic-engine test**

Add this test to `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`, as the last method in the class. This needs a concrete `LockableContainer` — `WoodenChest` (`Server.Items`) has a parameterless `[Constructible]` constructor and is exactly that:

```csharp
    // Proves the generic target-first engine (SpellTarget<T>'s phase-1/phase-2 machinery)
    // actually works for T = Item, not just T = Mobile - this is the first spell in this
    // repo to exercise TargetFirst against an Item target.
    [Fact]
    public void MagicLock_TargetFirst_AcceptsAnItemTarget()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var chest = new WoodenChest();
        chest.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new MagicLockSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing);

        var spellTarget = new SpellTarget<Item>(spell) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, chest);

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing);

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        chest.Delete();
    }
```

This test needs `Server.Items` (for `WoodenChest`) and `Server.Spells.Third` (for `MagicLockSpell`) — check the file's current `using` block first; add whichever of these two isn't already present (today it has `using Server.Spells; using Server.Spells.First; using Server.Spells.Seventh; using Server.Targeting; using Xunit;` — neither `Server.Items` nor `Server.Spells.Third` is there yet, so both need adding).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TargetFirstCastingTests"`
Expected: PASS (all tests in the file).

- [ ] **Step 5: Full build and full test suite run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions beyond the known pre-existing `FamiliarAITests` failure.

```bash
git add Projects/UOContent/Spells/Third/MagicLock.cs Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs
git commit -m "feat: Magic Lock becomes target-first (cast in motion)"
```

---

## Task 5: Ground/IPoint3D group (Unlock, Fire Field, Paralyze Field, Reveal)

**Files:**
- Modify: `Projects/UOContent/Spells/Third/Unlock.cs`
- Modify: `Projects/UOContent/Spells/Fourth/FireField.cs`
- Modify: `Projects/UOContent/Spells/Sixth/ParalyzeField.cs`
- Modify: `Projects/UOContent/Spells/Sixth/Reveal.cs`
- Modify (test): `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`

**Interfaces:**
- Consumes: `SpellTarget<IPoint3D>` (engine, already generic).
- Produces: nothing new for later tasks.

All four share `ValidateTargetFirst => true` (any point is a legal aim per the spec — the real checks like `CheckTown`/lock state stay inside each `Target()`, unchanged). They differ in whether `OnCast()` passes `allowGround: true` — **`Unlock.cs` does NOT** (its `Target(IPoint3D p)` explicitly rejects a `Mobile` p and requires a `LockableContainer`, so bare ground was never a valid click for it and stays that way); the other three already pass `allowGround: true` today and keep doing so.

- [ ] **Step 1: `Unlock.cs`**

In `Projects/UOContent/Spells/Third/Unlock.cs`, replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public void Target(IPoint3D p)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Third;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => true;

        public void Target(IPoint3D p)
```

Then, in the same file, replace:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<IPoint3D>(this);
        }
```

with:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<IPoint3D>(this, notifyOnLos: true);
        }
```

Note: no `allowGround: true` here — matches the file's existing behavior (bare ground was never a valid target for Unlock, and this change doesn't alter that).

- [ ] **Step 2: `FireField.cs`**

In `Projects/UOContent/Spells/Fourth/FireField.cs`, replace:

```csharp
    public override SpellCircle Circle => SpellCircle.Fourth;

    public int TargetRange => Core.T2A ? 15 : 18;

    public void Target(IPoint3D p)
```

with:

```csharp
    public override SpellCircle Circle => SpellCircle.Fourth;

    public int TargetRange => Core.T2A ? 15 : 18;

    public override bool BlocksMovement => false;

    public override bool TargetFirst => true;

    public override bool BlocksWeaponSwing => TargetFirstCommitted;

    public override bool ValidateTargetFirst(object target) => true;

    public void Target(IPoint3D p)
```

(Note this file uses file-scoped `namespace Server.Spells.Fourth;` — one level less indentation than the brace-wrapped files. Match the file's actual indentation, not the other steps' literal whitespace.)

Then, in the same file, replace:

```csharp
    public override void OnCast()
    {
        Caster.Target = new SpellTarget<IPoint3D>(this, allowGround: true);
    }
```

with:

```csharp
    public override void OnCast()
    {
        Caster.Target = new SpellTarget<IPoint3D>(this, allowGround: true, notifyOnLos: true);
    }
```

- [ ] **Step 3: `ParalyzeField.cs`**

Same shape as Step 2 (also file-scoped `namespace Server.Spells.Sixth;`, also has its own `TargetRange` property). In `Projects/UOContent/Spells/Sixth/ParalyzeField.cs`, replace:

```csharp
    public override SpellCircle Circle => SpellCircle.Sixth;

    public int TargetRange => Core.T2A ? 15 : 18;

    public void Target(IPoint3D p)
```

with:

```csharp
    public override SpellCircle Circle => SpellCircle.Sixth;

    public int TargetRange => Core.T2A ? 15 : 18;

    public override bool BlocksMovement => false;

    public override bool TargetFirst => true;

    public override bool BlocksWeaponSwing => TargetFirstCommitted;

    public override bool ValidateTargetFirst(object target) => true;

    public void Target(IPoint3D p)
```

Then, in the same file, replace:

```csharp
    public override void OnCast()
    {
        Caster.Target = new SpellTarget<IPoint3D>(this, allowGround: true);
    }
```

with:

```csharp
    public override void OnCast()
    {
        Caster.Target = new SpellTarget<IPoint3D>(this, allowGround: true, notifyOnLos: true);
    }
```

- [ ] **Step 4: `Reveal.cs`**

In `Projects/UOContent/Spells/Sixth/Reveal.cs` (brace-wrapped namespace, standard 8-space indentation), replace:

```csharp
        public override SpellCircle Circle => SpellCircle.Sixth;

        public void Target(IPoint3D p)
```

with:

```csharp
        public override SpellCircle Circle => SpellCircle.Sixth;

        public override bool BlocksMovement => false;

        public override bool TargetFirst => true;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => true;

        public void Target(IPoint3D p)
```

Then, in the same file, replace:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<IPoint3D>(this, allowGround: true);
        }
```

with:

```csharp
        public override void OnCast()
        {
            Caster.Target = new SpellTarget<IPoint3D>(this, allowGround: true, notifyOnLos: true);
        }
```

- [ ] **Step 5: Add the ground-target generic-engine test**

Add this test to `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs`, as the last method in the class. Uses `FireFieldSpell` as the representative (explicitly ground-targeted via `allowGround: true`):

```csharp
    // Proves the generic target-first engine works for T = IPoint3D (a bare ground click),
    // not just T = Mobile or T = Item.
    [Fact]
    public void FireField_TargetFirst_AcceptsAGroundTarget()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);

        var spell = new FireFieldSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing);

        var spellTarget = new SpellTarget<IPoint3D>(spell, allowGround: true) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, new Point3D(1001, 1000, 0));

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing);

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
    }
```

This test needs `Server.Spells.Fourth` (for `FireFieldSpell`) — add it to the file's `using` block if Task 4 hasn't already left the block in a state that covers it (it won't have — Task 4 added `Server.Spells.Third`, not `Fourth`).

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TargetFirstCastingTests"`
Expected: PASS (all tests in the file — by now it has 4 new tests added across Tasks 2, 3, 4, and this one, plus the 4 pre-existing ones).

- [ ] **Step 7: Full build and full test suite run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions beyond the known pre-existing `FamiliarAITests` failure.

```bash
git add Projects/UOContent/Spells/Third/Unlock.cs Projects/UOContent/Spells/Fourth/FireField.cs Projects/UOContent/Spells/Sixth/ParalyzeField.cs Projects/UOContent/Spells/Sixth/Reveal.cs Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs
git commit -m "feat: Unlock, Fire Field, Paralyze Field, Reveal become target-first (cast in motion)"
```

---

## Task 6: Repo docs and final full regression

**Files:**
- Modify: `custom-docs/CUSTOM_CHANGES.md`
- Modify: `custom-docs/LAVORI_IN_CORSO.md`

**Interfaces:** none — documentation only.

- [ ] **Step 1: Add rows to `CUSTOM_CHANGES.md`**

Append one row per pre-existing file this plan touched (the two test files are also pre-existing, per this doc's own stated scope of tracking every modified pre-existing file — not just production code). Use today's actual date (the date this step runs) for every row. Add these rows to the existing `| File | Motivo | Data |` table:

```markdown
| `Projects/UOContent/Spells/First/MagicArrow.cs` | Add target-first casting (cursor appears immediately, cast delay/cost commits on target click) — part of extending the mechanism across the mage's spellbook | <today> |
| `Projects/UOContent/Spells/Third/Fireball.cs` | Add target-first casting | <today> |
| `Projects/UOContent/Spells/Fourth/Lightning.cs` | Add target-first casting | <today> |
| `Projects/UOContent/Spells/Sixth/EnergyBolt.cs` | Add target-first casting | <today> |
| `Projects/UOContent/Spells/Third/Poison.cs` | Add target-first casting | <today> |
| `Projects/UOContent/Spells/First/Heal.cs` | Add target-first casting (beneficial-Mobile variant, ValidateTargetFirst uses CanBeBeneficial) | <today> |
| `Projects/UOContent/Spells/Third/MagicLock.cs` | Add target-first casting (Item-target variant, first spell to exercise the generic engine against T = Item) | <today> |
| `Projects/UOContent/Spells/Third/Unlock.cs` | Add target-first casting (ground/IPoint3D variant, no allowGround since bare ground was never a valid target for this spell) | <today> |
| `Projects/UOContent/Spells/Fourth/FireField.cs` | Add target-first casting (ground/IPoint3D variant, allowGround: true) | <today> |
| `Projects/UOContent/Spells/Sixth/ParalyzeField.cs` | Add target-first casting (ground/IPoint3D variant, allowGround: true) | <today> |
| `Projects/UOContent/Spells/Sixth/Reveal.cs` | Add target-first casting (ground/IPoint3D variant, allowGround: true) | <today> |
| `Projects/UOContent.Tests/Tests/Spells/CastInterruptRecastTests.cs` | Swap MagicArrowSpell for ClumsySpell as the "ordinary spell" test fixture — MagicArrowSpell became target-first as part of this feature | <today> |
| `Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs` | Same fixture swap, plus 4 new tests covering the harmful/beneficial-Mobile, Item, and ground/IPoint3D target-first categories | <today> |
```

- [ ] **Step 2: Add a `LAVORI_IN_CORSO.md` entry**

Add a new section, following the exact structure already used by the "Target-first casting su Flame Strike" and "Magic Reflection — scudo riflettente targetabile" entries in the same file (Stato/Cosa copre/Fatto/Manca bullets):

```markdown
## Cast-in-corsa per il resto del libro del mago

- **Stato:** Implementato, buildato, testato (4 nuovi test automatici che coprono le 4 categorie di bersaglio — Mobile ostile, Mobile amico, Item, terreno/IPoint3D — più il fixture fix su `CastInterruptRecastTests.cs`) — **non ancora verificato in gioco**.
- **Cosa copre:** 11 spell del libro del mago (Magic Arrow, Fireball, Lightning, Energy Bolt, Poison, Heal, Magic Lock, Unlock, Fire Field, Paralyze Field, Reveal) ottengono lo stesso meccanismo target-first già verificato su Flame Strike e Magic Reflection — mirino immediato, cast libero in movimento, costo/delay solo dal click in poi. Zero modifiche al motore condiviso (`Spell.cs`/`SpellTarget.cs`).
- **Fatto:**
  - 11 file spell (elencati in `CUSTOM_CHANGES.md`): stessi quattro membri (`TargetFirst`, `BlocksMovement`, `BlocksWeaponSwing`, `ValidateTargetFirst`) + `notifyOnLos: true`
  - Fix preventivo: `CastInterruptRecastTests.cs`/`TargetFirstCastingTests.cs` non usano più `MagicArrowSpell` come "spell ordinaria" di riferimento (ora diventata anche lei target-first) — sostituita con `ClumsySpell`
  - 4 nuovi test in `TargetFirstCastingTests.cs`, uno per categoria di bersaglio mai esercitata prima in modalità target-first (Mobile ostile via Magic Arrow, Mobile amico via Heal, Item via Magic Lock, terreno via Fire Field)
  - Build e `dotnet test` puliti
- **Manca (da verificare in gioco, una spell per volta — happy path, rifiuto gratis in fase 1, fallimento addebitato in fase 2, disturbo durante il delay):**
  - [ ] Magic Arrow
  - [ ] Fireball
  - [ ] Lightning
  - [ ] Energy Bolt
  - [ ] Poison (in particolare: il livello di veleno dipende dalla distanza misurata a fine delay, non al click — verificare che allontanarsi durante il delay riduca/annulli l'effetto)
  - [ ] Heal (in particolare: il mirino ora accetta anche bersagli diversi da se stessi durante il movimento)
  - [ ] Magic Lock (in particolare: mirare un oggetto non-baule non deve costare nulla in fase 1)
  - [ ] Unlock (in particolare: NON ha `allowGround` — verificare che il terreno vuoto resti un bersaglio non valido, invariato)
  - [ ] Fire Field
  - [ ] Paralyze Field
  - [ ] Reveal
  - Decisione finale: tenere così, aggiustare, o rivedere qualcosa
- **Fuori scope, noto e accettato:** Gate Travel resta col pre-cast classico (richiede un secondo `Target` da estendere, non è il pattern già pronto); Magic Reflection è stata gestita a parte (già completata e mergiata).
```

- [ ] **Step 3: Full solution build and full test suite, then commit**

Run: `dotnet build` from the repo root — confirm clean across every project.
Run: `dotnet test` from the repo root — confirm no regressions beyond the one known pre-existing `FamiliarAITests` failure.

```bash
git add custom-docs/CUSTOM_CHANGES.md custom-docs/LAVORI_IN_CORSO.md
git commit -m "docs: log mage spellbook cast-in-motion changes and track manual verification"
```

---

## Out of scope for this plan (tracked elsewhere or deliberately not built)

- **Gate Travel** — stays classic pre-cast, per the spec's explicit decision (would need new engineering on `RecallSpellTarget`, a different `Target` subclass entirely).
- **Magic Reflection** — already handled in a separate, already-merged plan (`custom-docs/plans/2026-09-17-magic-reflection-shield-plan.md`).
- Any change to the target-first mechanism itself (`Spell.cs`, `SpellTarget.cs`) — this plan only consumes the existing, already-verified mechanism, per spell.

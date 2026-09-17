# Magic Reflection — Targetable Reflect Shield Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign `MagicReflectSpell` from a self-only passive resistance buff into a targetable, single-use reflect shield — cast on any player Mobile, the next harmful spell that hits them bounces back onto its original caster (recalculated on the reflecting party's own resistances via the existing caster/target swap), with a double-shield "vanish" case and a piercing hook reserved for a future PvP ability.

**Architecture:** Two new `bool` properties on `Mobile` (`SpellReflectActive`, `PiercesSpellReflect`). A new static `Server.Custom.SpellReflect` class owns apply/clear/expiry, mirroring the existing `Server.Custom.MagicShield` pattern. `SpellHelper.CheckReflect` changes its return type from `bool` to a new `ReflectResult` enum (`None`/`Reflected`/`Vanished`) and gains the new shield + piercing checks ahead of its existing legacy `MagicDamageAbsorb`/`BaseCreature` reflect path (which must keep working unchanged — it's still live for `MeerCaptain`). Every one of the 21 existing call sites of `CheckReflect` (19 files — 2 have it commented out already and are left alone) gets a one-line guard added right after the call to skip effect application on `Vanished`. `MagicReflectSpell` itself is rewritten to use the existing target-first casting flow (`TargetFirst`/`BlocksMovement`/`BlocksWeaponSwing`/`ValidateTargetFirst` — the same pattern already built and verified for `FlameStrikeSpell`, reused as-is, no engine changes needed for that part).

**Tech Stack:** C# / .NET 10, xUnit (`[Fact]`), ModernUO's `Timer`/`TimerExecutionToken` and `BuffInfo`/`PlayerMobile.AddBuff` conventions.

**Spec:** `custom-docs/specs/2026-09-17-magic-reflection-shield-design.md`. Also relevant (already implemented, not touched by this plan): `custom-docs/specs/2026-09-14-target-first-casting-design.md` (the target-first mechanism `MagicReflectSpell` reuses in Task 4).

## Global Constraints

- `Projects/Server/Mobiles/Mobile.cs` changes are pre-authorized for this feature (explicit user go-ahead, same precedent as `MagicShieldAbsorb`) — stay minimal: exactly the two new properties, nothing else in that file.
- The legacy `target.MagicDamageAbsorb > 0` branch inside `CheckReflect` **must not be removed or altered** — `Mobiles/Monsters/LBR/Meers/MeerCaptain.cs:110-112` sets `MagicDamageAbsorb` directly as its own creature ability, independent of `MagicReflectSpell`. It stays exactly as-is; the new shield check is additive, checked first, and falls through to the untouched legacy path when the new shield isn't involved.
- No era gating anywhere in this feature (confirmed decision) — the new shield is `MagicReflectSpell`'s only behavior regardless of `Core.Expansion`.
- Shield duration is a fixed **5 minutes** (`TimeSpan.FromMinutes(5)`) if never consumed by a reflect.
- `SpellReflect.Apply` always **replaces** any existing shield on that Mobile (cancels the old expiry timer, no stacking) — same pattern as `MagicShield.Apply`.
- The new shield is reachable only by casting on a **player** Mobile (`Mobile.Player == true`) — both `ValidateTargetFirst` (phase-1 gate) and `Target()` (phase-2/authoritative gate) enforce this. Creatures keep their existing, separate `BaseCreature.CheckReflect` behavior untouched.
- Piercing (`Mobile.PiercesSpellReflect`) always consumes the target's shield when it applies, even though damage still lands on the original target (not swapped) — confirmed decision. Nothing sets this flag in production code anywhere in this plan; it exists purely as a hook for a future, separate PvP class-evolution ability.
- `DuelContext.cs`'s pre-AOS duel-cleanup block (`mob.MagicDamageAbsorb = 0` etc., only runs under `!Core.AOS`) is **not** extended to reset the new shield — same deliberately-left-as-a-known-gap treatment the sibling `MagicShield` feature already got for the identical concern (see `custom-docs/plans/2026-09-16-magic-shield-modernuo-plan.md`, "Out of scope"). Not part of this plan.
- Automated tests for the 19 call-site edits (Tasks 5-8) do **not** attempt to drive a full successful spell cast through `Cast()` → `CheckSequence()` to observe the applied effect. Two existing test files in this repo (`TargetFirstCastingTests.cs`, `CastInterruptRecastTests.cs`) already establish this boundary deliberately: every test that reaches a target-click stops at `TargetFirstCommitted`/mana-charged and explicitly disturbs the spell rather than letting `CheckSequence()`'s skill-based fizzle roll run to a real resolution, precisely because that roll isn't reliably controllable in this test host. The real coverage of the *logic* under test is `SpellHelper.CheckReflect` itself, fully and directly unit-tested in Task 3 with bare `Mobile`s (no spell casting involved at all). The 19 call-site tasks are mechanical, identically-shaped edits verified by `dotnet build`, a full `dotnet test` regression pass, and a manual in-game checklist per spell (per this repo's own discipline — see Task 9).
- All new tests use `[Collection("Sequential UOContent Tests")]`, matching every file they sit next to.

---

## Task 1: `Mobile` properties

**Files:**
- Modify: `Projects/Server/Mobiles/Mobile.cs:484` (immediately after the existing `MagicDamageAbsorb` property)

**Interfaces:**
- Produces: `Mobile.SpellReflectActive` (`bool`, get/set, default `false`), `Mobile.PiercesSpellReflect` (`bool`, get/set, default `false`). Task 2 reads/writes `SpellReflectActive`. Task 3 reads/writes both.

- [ ] **Step 1: Add the two properties**

In `Projects/Server/Mobiles/Mobile.cs`, right after the existing `MagicDamageAbsorb` property (search for `MagicDamageAbsorb` — it's the third of three adjacent `[CommandProperty(AccessLevel.GameMaster)] public int ... { get; set; }` one-liners, immediately after `MeleeDamageAbsorb` and `MagicShieldAbsorb`):

```csharp
    [CommandProperty(AccessLevel.GameMaster)]
    public int MagicDamageAbsorb { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool SpellReflectActive { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool PiercesSpellReflect { get; set; }
```

(Only the two new properties are new — `MagicDamageAbsorb` is shown unchanged, for anchoring.)

No test for this step alone — bare properties, exercised by Task 2's and Task 3's tests.

- [ ] **Step 2: Build and commit**

Run: `dotnet build` — confirm clean.

```bash
git add Projects/Server/Mobiles/Mobile.cs
git commit -m "feat: add SpellReflectActive and PiercesSpellReflect to Mobile"
```

---

## Task 2: `Server.Custom.SpellReflect` static class

**Files:**
- Create: `Projects/UOContent/Custom/SpellReflect.cs`
- Test: `Projects/UOContent.Tests/Tests/Custom/SpellReflectTests.cs`

**Interfaces:**
- Consumes: `Mobile.SpellReflectActive` (Task 1), `Timer.StartTimer(TimeSpan, Action, out TimerExecutionToken)` (engine), `TimerExecutionToken.Cancel()` (engine), `BuffInfo`/`BuffIcon.MagicReflection`/`PlayerMobile.AddBuff`/`PlayerMobile.RemoveBuff` (engine, already used by the pre-existing `MagicReflect.cs`).
- Produces: `SpellReflect.Apply(Mobile m, TimeSpan duration)`, `SpellReflect.Clear(Mobile m)`, `SpellReflect.IsActive(Mobile m) -> bool`. Task 3 calls `Apply`/`Clear` from inside `SpellHelper.CheckReflect`. Task 4 calls `Apply` from `MagicReflectSpell.Target()`.

- [ ] **Step 1: Write the failing tests**

Create `Projects/UOContent.Tests/Tests/Custom/SpellReflectTests.cs`:

```csharp
using System;
using Server.Custom;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class SpellReflectTests
{
    // 8ms lockstep keeps the wheel and Core.TickCount in sync - same helper already used by
    // MagicShieldTests.cs for its own expiry tests.
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
    public void ApplySetsTheFlag()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        SpellReflect.Apply(m, TimeSpan.FromMinutes(5));

        Assert.True(m.SpellReflectActive);
        Assert.True(SpellReflect.IsActive(m));

        m.Delete();
    }

    [Fact]
    public void ApplyReplacesAnExistingShield_CancellingTheOldExpiryTimer()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        SpellReflect.Apply(m, TimeSpan.FromSeconds(5));
        SpellReflect.Apply(m, TimeSpan.FromMinutes(5)); // re-cast: must not stack or throw

        Assert.True(m.SpellReflectActive);

        // If the first 5-second timer weren't cancelled, it would wrongly clear the
        // second (5-minute) shield here.
        RunFor(6000);
        Assert.True(m.SpellReflectActive);

        m.Delete();
    }

    [Fact]
    public void ClearCancelsTheExpiryTimer()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        SpellReflect.Apply(m, TimeSpan.FromSeconds(5));

        SpellReflect.Clear(m);
        Assert.False(m.SpellReflectActive);

        // Advancing past the original duration must not do anything odd with an
        // already-cancelled timer.
        RunFor(6000);
        Assert.False(m.SpellReflectActive);

        m.Delete();
    }

    [Fact]
    public void ShieldExpiresOnItsOwn_WithoutBeingConsumed()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();
        SpellReflect.Apply(m, TimeSpan.FromSeconds(5));

        Assert.True(m.SpellReflectActive);

        RunFor(6000);

        Assert.False(m.SpellReflectActive);
        Assert.False(SpellReflect.IsActive(m));

        m.Delete();
    }

    [Fact]
    public void ClearIsANoOp_WhenThereIsNoShield()
    {
        var m = new Mobile(World.NewMobile);
        m.DefaultMobileInit();

        SpellReflect.Clear(m); // must not throw

        Assert.False(m.SpellReflectActive);

        m.Delete();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~SpellReflectTests"`
Expected: FAIL — `Server.Custom.SpellReflect` doesn't exist yet.

- [ ] **Step 3: Implement `SpellReflect`**

Create `Projects/UOContent/Custom/SpellReflect.cs`:

```csharp
using System;
using System.Collections.Generic;
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
        (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.MagicReflection, 1075817, retainThroughDeath: true));

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
}
```

`(m as PlayerMobile)?.AddBuff(...)` is a no-op for a bare `Mobile` (every test above uses one) — matches the null-conditional pattern the pre-existing `MagicReflect.cs` already used for the same buff icon.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~SpellReflectTests"`
Expected: PASS (all 5 tests).

- [ ] **Step 5: Full UOContent.Tests run and commit**

Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions.

```bash
git add Projects/UOContent/Custom/SpellReflect.cs Projects/UOContent.Tests/Tests/Custom/SpellReflectTests.cs
git commit -m "feat: add SpellReflect apply/clear/expiry API"
```

---

## Task 3: `ReflectResult` enum and `SpellHelper.CheckReflect` rewrite

**Files:**
- Create: `Projects/UOContent/Spells/Base/ReflectResult.cs`
- Modify: `Projects/UOContent/Spells/Base/SpellHelper.cs:888-931` (both `CheckReflect` overloads)
- Test: `Projects/UOContent.Tests/Tests/Spells/CheckReflectTests.cs`

**Interfaces:**
- Consumes: `Mobile.SpellReflectActive`/`PiercesSpellReflect` (Task 1), `SpellReflect.Clear(Mobile)` (Task 2).
- Produces: `ReflectResult` enum (`None`, `Reflected`, `Vanished`). `SpellHelper.CheckReflect(int, Mobile, ref Mobile) -> ReflectResult` and `SpellHelper.CheckReflect(int, ref Mobile, ref Mobile) -> ReflectResult` — same two overloads that already exist today, return type changed from `bool`. Tasks 5-8 call these and branch on `ReflectResult.Vanished`.

- [ ] **Step 1: Write the failing tests**

Create `Projects/UOContent.Tests/Tests/Spells/CheckReflectTests.cs`:

```csharp
using System;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class CheckReflectTests
{
    [Fact]
    public void NoShields_ReturnsNone_NoSwap()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.None, result);
        Assert.Same(originalTarget, target);

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void TargetHasShield_ReturnsReflected_SwapsAndConsumesTheShield()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.Reflected, result);
        Assert.Same(caster, target); // swapped: the effect now lands on the original caster
        Assert.False(originalTarget.SpellReflectActive); // shield consumed

        caster.Delete();
        originalTarget.Delete();
    }

    [Fact]
    public void BothHaveShields_ReturnsVanished_ClearsBothShields_NoSwap()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(caster, TimeSpan.FromMinutes(5));
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.Vanished, result);
        Assert.False(caster.SpellReflectActive);
        Assert.False(originalTarget.SpellReflectActive);
        Assert.Same(originalTarget, target); // no swap on the vanish path

        caster.Delete();
        originalTarget.Delete();
    }

    [Fact]
    public void CasterPierces_ReturnsNone_StillConsumesTargetShield_NoSwap()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.PiercesSpellReflect = true;
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.None, result);
        Assert.Same(originalTarget, target); // lands on the original target, not swapped
        Assert.False(originalTarget.SpellReflectActive); // shield still breaks

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void PiercingWithNoShieldOnTarget_ReturnsNone_IsANoOp()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        caster.PiercesSpellReflect = true;
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();

        var originalTarget = target;
        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.None, result);
        Assert.Same(originalTarget, target);

        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void LegacyMagicDamageAbsorb_StillReflects_WhenNoNewShieldIsInvolved()
    {
        // Regression guard: MeerCaptain (Mobiles/Monsters/LBR/Meers/MeerCaptain.cs) sets
        // MagicDamageAbsorb directly as its own creature ability - this path must keep working.
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.MagicDamageAbsorb = 100;

        var result = SpellHelper.CheckReflect(1, caster, ref target);

        Assert.Equal(ReflectResult.Reflected, result);
        Assert.Same(caster, target);

        caster.Delete();
        target.Delete();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CheckReflectTests"`
Expected: FAIL to compile — `SpellHelper.CheckReflect` still returns `bool`, `ReflectResult` doesn't exist yet.

- [ ] **Step 3: Create the `ReflectResult` enum**

Create `Projects/UOContent/Spells/Base/ReflectResult.cs`:

```csharp
namespace Server.Spells;

public enum ReflectResult
{
    None,
    Reflected,
    Vanished
}
```

- [ ] **Step 4: Rewrite `CheckReflect`**

In `Projects/UOContent/Spells/Base/SpellHelper.cs`, replace the two existing `CheckReflect` overloads (currently lines 888-931 — search for `public static bool CheckReflect`) with:

```csharp
        public static ReflectResult CheckReflect(int circle, Mobile caster, ref Mobile target) =>
            CheckReflect(circle, ref caster, ref target);

        public static ReflectResult CheckReflect(int circle, ref Mobile caster, ref Mobile target)
        {
            if (caster.PiercesSpellReflect && target.SpellReflectActive)
            {
                SpellReflect.Clear(target);
                return ReflectResult.None;
            }

            if (target.SpellReflectActive)
            {
                SpellReflect.Clear(target);

                if (caster.SpellReflectActive)
                {
                    SpellReflect.Clear(caster);
                    return ReflectResult.Vanished;
                }

                target.FixedEffect(0x37B9, 10, 5);
                (caster, target) = (target, caster);
                return ReflectResult.Reflected;
            }

            var reflect = false;

            if (target.MagicDamageAbsorb > 0)
            {
                if (!Core.UOR)
                {
                    // T2A: single-use reflection, consumed immediately
                    target.MagicDamageAbsorb = 0;
                    reflect = true;
                }
                else
                {
                    ++circle;

                    target.MagicDamageAbsorb -= circle;

                    // This order isn't very intuitive, but you have to nullify reflect before target gets switched
                    reflect = target.MagicDamageAbsorb >= 0;
                    if (target.MagicDamageAbsorb <= 0)
                    {
                        target.MagicDamageAbsorb = 0;
                        DefensiveSpell.Nullify(target);
                    }
                }
            }

            if (target is BaseCreature creature)
            {
                creature.CheckReflect(caster, ref reflect);
            }

            if (reflect)
            {
                target.FixedEffect(0x37B9, 10, 5);
                (caster, target) = (target, caster);
            }

            return reflect ? ReflectResult.Reflected : ReflectResult.None;
        }
```

Everything from `var reflect = false;` onward is the pre-existing legacy body, unchanged — only the new shield/piercing block above it, and the final `return` line's type, are new. `using Server.Custom;` is already present at the top of this file (verify — it's there for the existing `MagicShield` usage).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CheckReflectTests"`
Expected: PASS (all 6 tests). This will also surface every one of the 19 not-yet-updated call sites as a **build error** (they still treat the return value as unused, which is fine and still compiles — a `bool`-returning-turned-`ReflectResult`-returning method with a discarded return value compiles either way; the ones that explicitly branch on the old `bool`, `BombardSpell.cs` and `EagleStrikeSpell.cs`, **will** fail to compile at this point, since `if (SpellHelper.CheckReflect(...))` can no longer implicitly convert `ReflectResult` to `bool`). This is expected and resolved in Task 7.

- [ ] **Step 6: Fix the two call sites that no longer compile**

`BombardSpell.cs:46` and `EagleStrikeSpell.cs:39` use `if (SpellHelper.CheckReflect(...))`, which breaks now that the return type isn't `bool`. Task 7 rewrites these two files properly; for this task only, make the minimum change needed to get a clean build without duplicating Task 7's work — change both conditions to explicit equality checks:

In `Projects/UOContent/Spells/Mysticism/BombardSpell.cs:46`:
```csharp
            if (SpellHelper.CheckReflect(6, ref source, ref m) == ReflectResult.Reflected)
```

In `Projects/UOContent/Spells/Mysticism/EagleStrikeSpell.cs:39`:
```csharp
            if (SpellHelper.CheckReflect(2, ref source, ref m) == ReflectResult.Reflected)
```

(Both files need `using Server.Spells;` for `ReflectResult` — check first; if the file's namespace is already `Server.Spells.Mysticism`, `ReflectResult` in `Server.Spells` resolves without a new `using` since it's a parent namespace... it does **not** — C# doesn't do parent-namespace fallback. Add `using Server.Spells;` to both files' `using` block if not already present.)

This is a minimal compile fix, not the full Vanished-aware rewrite — Task 7 replaces this `if` entirely.

- [ ] **Step 7: Full build and full test run, then commit**

Run: `dotnet build` — expect clean (only the two files above needed the Step 6 fix; every other call site still compiles as-is with its return value discarded).
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions beyond the two files touched here.

```bash
git add Projects/UOContent/Spells/Base/ReflectResult.cs Projects/UOContent/Spells/Base/SpellHelper.cs Projects/UOContent/Spells/Mysticism/BombardSpell.cs Projects/UOContent/Spells/Mysticism/EagleStrikeSpell.cs Projects/UOContent.Tests/Tests/Spells/CheckReflectTests.cs
git commit -m "feat: CheckReflect returns ReflectResult (None/Reflected/Vanished)"
```

---

## Task 4: Rewrite `MagicReflectSpell`

**Files:**
- Modify: `Projects/UOContent/Spells/Fifth/MagicReflect.cs` (full rewrite)
- Test: `Projects/UOContent.Tests/Tests/Spells/MagicReflectSpellTests.cs`

**Interfaces:**
- Consumes: `SpellReflect.Apply(Mobile, TimeSpan)` (Task 2), `Spell.TargetFirst`/`BlocksMovement`/`BlocksWeaponSwing`/`TargetFirstCommitted`/`ValidateTargetFirst` (engine, already built and verified for `FlameStrikeSpell` — no changes needed here).
- Produces: nothing new for later tasks — this is the last piece that makes the feature playable.

- [ ] **Step 1: Write the failing tests**

Create `Projects/UOContent.Tests/Tests/Spells/MagicReflectSpellTests.cs`:

```csharp
using Server.Spells;
using Server.Spells.Fifth;
using Server.Targeting;
using Xunit;

namespace Server.Tests.Spells;

[Collection("Sequential UOContent Tests")]
public class MagicReflectSpellTests
{
    // Mirrors FlameStrike_BlocksWeaponSwing_OnlyOncePhase2Commits in TargetFirstCastingTests.cs -
    // same wiring, different spell, confirming the existing target-first mechanism generalizes.
    [Fact]
    public void IsTargetFirst_MovableThroughout_WeaponSwingBlockedOnlyAfterCommit()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        target.Player = true;

        caster.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        target.MoveToWorld(new Point3D(1001, 1000, 0), Map.Felucca);

        var spell = new MagicReflectSpell(caster) { State = SpellState.Casting };
        caster.Spell = spell;

        Assert.True(spell.TargetFirst);
        Assert.False(spell.BlocksMovement);
        Assert.False(spell.BlocksWeaponSwing); // phase 1: not committed yet

        var spellTarget = new SpellTarget<Mobile>(spell, TargetFlags.None) { CheckLOS = false };
        caster.Target = spellTarget;
        spellTarget.Invoke(caster, target); // click commits phase 2

        Assert.True(spell.TargetFirstCommitted);
        Assert.True(spell.BlocksWeaponSwing); // phase 2: locked out until fizzle or hit

        spell.Disturb(DisturbType.Kill);
        caster.Delete();
        target.Delete();
    }

    [Fact]
    public void ValidateTargetFirst_AcceptsOnlyPlayerMobiles()
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var player = new Mobile(World.NewMobile);
        player.DefaultMobileInit();
        player.Player = true;
        var creature = new Mobile(World.NewMobile);
        creature.DefaultMobileInit();
        creature.Player = false;

        var spell = new MagicReflectSpell(caster);

        Assert.True(spell.ValidateTargetFirst(player));
        Assert.False(spell.ValidateTargetFirst(creature));
        Assert.False(spell.ValidateTargetFirst("not a mobile"));

        caster.Delete();
        player.Delete();
        creature.Delete();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~MagicReflectSpellTests"`
Expected: FAIL — `MagicReflectSpell` doesn't have `TargetFirst`/`ValidateTargetFirst` overrides yet (compiles today, but `spell.TargetFirst` is `false` and `ValidateTargetFirst` always returns `true`, so the assertions fail).

- [ ] **Step 3: Rewrite `MagicReflect.cs`**

Replace the entire contents of `Projects/UOContent/Spells/Fifth/MagicReflect.cs`:

```csharp
using System;
using Server.Custom;
using Server.Targeting;

namespace Server.Spells.Fifth
{
    public class MagicReflectSpell : MagerySpell, ITargetingSpell<Mobile>
    {
        private static readonly SpellInfo _info = new(
            "Magic Reflection",
            "In Jux Sanct",
            242,
            9012,
            Reagent.Garlic,
            Reagent.MandrakeRoot,
            Reagent.SpidersSilk
        );

        public MagicReflectSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
        {
        }

        public override SpellCircle Circle => SpellCircle.Fifth;

        public override bool TargetFirst => true;

        public override bool BlocksMovement => false;

        public override bool BlocksWeaponSwing => TargetFirstCommitted;

        public override bool ValidateTargetFirst(object target) => target is Mobile { Player: true };

        public void Target(Mobile m)
        {
            if (!m.Player)
            {
                Caster.SendMessage("You can only cast this on a player.");
                return;
            }

            if (CheckSequence())
            {
                SpellReflect.Apply(m, TimeSpan.FromMinutes(5));

                m.FixedParticles(0x375A, 10, 15, 5037, EffectLayer.Waist);
                m.PlaySound(0x1E9);
            }
        }

        public override void OnCast()
        {
            Caster.Target = new SpellTarget<Mobile>(this, TargetFlags.None, notifyOnLos: true);
        }
    }
}
```

This deletes the old `_table` dictionary, the `CheckCast()` override, all three era branches (AOS resistance mods, UOR `MagicDamageAbsorb`/`DefensiveSpell`, pre-UOR variant), and the `[OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))] EndReflect` cleanup — none of that applies to the new shield mechanic. `TargetFlags.None` (not `.Harmful` or `.Beneficial`) reflects that any Mobile relationship is allowed, per the design decision.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~MagicReflectSpellTests"`
Expected: PASS (both tests).

- [ ] **Step 5: Full build and test run, then commit**

Run: `dotnet build` — confirm clean (no remaining references to the deleted `_table`/`EndReflect`/era-branch members anywhere else in the codebase; there shouldn't be any, since they were all private/spell-local, but the build is the authoritative check).
Run: `dotnet test Projects/UOContent.Tests`

```bash
git add Projects/UOContent/Spells/Fifth/MagicReflect.cs Projects/UOContent.Tests/Tests/Spells/MagicReflectSpellTests.cs
git commit -m "feat: MagicReflectSpell becomes a targetable single-use reflect shield"
```

---

## Task 5: Update the 13 bare 3-argument call sites

**Files:**
- Modify: `Projects/UOContent/Spells/First/Weaken.cs:30`
- Modify: `Projects/UOContent/Spells/First/Clumsy.cs:30`
- Modify: `Projects/UOContent/Spells/First/Feeblemind.cs:30`
- Modify: `Projects/UOContent/Spells/Second/Harm.cs:30`
- Modify: `Projects/UOContent/Spells/Third/Poison.cs:29`
- Modify: `Projects/UOContent/Spells/Fourth/Curse.cs:95`
- Modify: `Projects/UOContent/Spells/Fourth/ManaDrain.cs:33`
- Modify: `Projects/UOContent/Spells/Fifth/Paralyze.cs:36`
- Modify: `Projects/UOContent/Spells/Sixth/Explosion.cs:40`
- Modify: `Projects/UOContent/Spells/Seventh/FlameStrike.cs:39`
- Modify: `Projects/UOContent/Spells/Seventh/ManaVampire.cs:31`
- Modify: `Projects/UOContent/Spells/Mysticism/SpellPlagueSpell.cs:42`
- Modify: `Projects/UOContent/Spells/Fourth/Lightning.cs:30`

**Interfaces:**
- Consumes: `SpellHelper.CheckReflect(int, Mobile, ref Mobile) -> ReflectResult` (Task 3).
- Produces: nothing new — every one of these files needs `using Server.Spells;` for `ReflectResult` if its namespace isn't already `Server.Spells.<X>` with `ReflectResult` visible (check each file's existing `namespace` line — files under `Server.Spells.First`/`Second`/`Fourth`/`Fifth`/`Sixth`/`Seventh` are sub-namespaces of `Server.Spells` and **do** see `ReflectResult` without a new `using`; only `SpellPlagueSpell.cs`, under `Server.Spells.Mysticism`, is in the same situation — also a sub-namespace of `Server.Spells`, so it also needs no new `using`). No file in this task needs a new `using` line.

All 13 edits share the exact same shape: the existing bare-statement call

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);
```

becomes

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }
```

`SpellPlagueSpell.cs` uses the literal circle `6` instead of `(int)Circle` — same shape otherwise. Every occurrence is inside a method returning `void` (`Target(Mobile m)`), so a bare `return;` is always correct — verified per-file below, since indentation/exact surrounding code differs slightly.

- [ ] **Step 1: `Weaken.cs`**

In `Projects/UOContent/Spells/First/Weaken.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                var length = SpellHelper.GetDuration(Caster, m);
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                var length = SpellHelper.GetDuration(Caster, m);
```

- [ ] **Step 2: `Clumsy.cs`**

In `Projects/UOContent/Spells/First/Clumsy.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                var length = SpellHelper.GetDuration(Caster, m);
```

with the same guard shape as Step 1 (identical surrounding code in this file).

- [ ] **Step 3: `Feeblemind.cs`**

In `Projects/UOContent/Spells/First/Feeblemind.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                // TODO: StoneForm immunity
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                // TODO: StoneForm immunity
```

- [ ] **Step 4: `Harm.cs`**

In `Projects/UOContent/Spells/Second/Harm.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                double damage;
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                double damage;
```

- [ ] **Step 5: `Poison.cs`**

In `Projects/UOContent/Spells/Third/Poison.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                m.Spell?.OnCasterHurt();
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                m.Spell?.OnCasterHurt();
```

- [ ] **Step 6: `Curse.cs`**

In `Projects/UOContent/Spells/Fourth/Curse.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                if (DoCurse(Caster, m))
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                if (DoCurse(Caster, m))
```

- [ ] **Step 7: `ManaDrain.cs`**

In `Projects/UOContent/Spells/Fourth/ManaDrain.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                m.Spell?.OnCasterHurt();
```

with the same guard shape as Step 5.

- [ ] **Step 8: `Paralyze.cs`**

In `Projects/UOContent/Spells/Fifth/Paralyze.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                double duration;
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                double duration;
```

- [ ] **Step 9: `Explosion.cs`**

In `Projects/UOContent/Spells/Sixth/Explosion.cs`, replace:

```csharp
                SpellHelper.Turn(Caster, m);
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                new InternalTimer(this, Caster, defender, m).Start();
```

with:

```csharp
                SpellHelper.Turn(Caster, m);

                if (SpellHelper.CheckReflect((int)Circle, Caster, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                new InternalTimer(this, Caster, defender, m).Start();
```

(`defender` was already captured as a copy of `m` *before* this call, on the line above `SpellHelper.Turn` — unaffected either way, since the guard only skips creating the `InternalTimer`, and `defender` is never read if that never happens.)

- [ ] **Step 10: `FlameStrike.cs`**

In `Projects/UOContent/Spells/Seventh/FlameStrike.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                double damage;
```

with the same guard shape as Step 4.

- [ ] **Step 11: `ManaVampire.cs`**

In `Projects/UOContent/Spells/Seventh/ManaVampire.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                m.Spell?.OnCasterHurt();
```

with the same guard shape as Step 5.

- [ ] **Step 12: `SpellPlagueSpell.cs`**

In `Projects/UOContent/Spells/Mysticism/SpellPlagueSpell.cs`, replace:

```csharp
            SpellHelper.CheckReflect(6, Caster, ref m);

            /* The target is hit with an explosion of chaos damage and then inflicted
```

with:

```csharp
            if (SpellHelper.CheckReflect(6, Caster, ref m) == ReflectResult.Vanished)
            {
                return;
            }

            /* The target is hit with an explosion of chaos damage and then inflicted
```

(Note the 4-space, not 8-space, base indentation in this file — it uses file-scoped `namespace Server.Spells.Mysticism;` rather than a brace-wrapped namespace, one level less nesting than the others in this task. Match the file's actual indentation, not the other steps' literal whitespace.)

- [ ] **Step 13: `Lightning.cs`**

In `Projects/UOContent/Spells/Fourth/Lightning.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, Caster, ref m);

                double damage;
```

with the same guard shape as Step 4.

- [ ] **Step 14: Full build and test run, then commit**

Run: `dotnet build` — confirm clean across all 13 files.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions (every existing test's Mobiles have `SpellReflectActive == false` by default, so `CheckReflect` returns `None` for all of them exactly as it returned `false` before — the guard's `if` body never fires for any pre-existing test).

```bash
git add Projects/UOContent/Spells/First/Weaken.cs Projects/UOContent/Spells/First/Clumsy.cs Projects/UOContent/Spells/First/Feeblemind.cs Projects/UOContent/Spells/Second/Harm.cs Projects/UOContent/Spells/Third/Poison.cs Projects/UOContent/Spells/Fourth/Curse.cs Projects/UOContent/Spells/Fourth/ManaDrain.cs Projects/UOContent/Spells/Fifth/Paralyze.cs Projects/UOContent/Spells/Sixth/Explosion.cs Projects/UOContent/Spells/Seventh/FlameStrike.cs Projects/UOContent/Spells/Seventh/ManaVampire.cs Projects/UOContent/Spells/Mysticism/SpellPlagueSpell.cs Projects/UOContent/Spells/Fourth/Lightning.cs
git commit -m "feat: skip spell effect on a double-shield Vanished reflect (13 spells)"
```

---

## Task 6: Update the 3 `ref`-caster call sites

**Files:**
- Modify: `Projects/UOContent/Spells/First/MagicArrow.cs:40`
- Modify: `Projects/UOContent/Spells/Third/Fireball.cs:31`
- Modify: `Projects/UOContent/Spells/Sixth/EnergyBolt.cs:32`

**Interfaces:**
- Consumes: `SpellHelper.CheckReflect(int, ref Mobile, ref Mobile) -> ReflectResult` (Task 3).
- Produces: nothing new.

Same guard shape as Task 5, applied to the `ref source, ref m` overload instead of the `Caster, ref m` one. All three files are `Server.Spells.<X>` sub-namespaces, so `ReflectResult` is visible without a new `using`.

- [ ] **Step 1: `MagicArrow.cs`**

In `Projects/UOContent/Spells/First/MagicArrow.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, ref source, ref m);

                double damage;
```

with:

```csharp
                if (SpellHelper.CheckReflect((int)Circle, ref source, ref m) == ReflectResult.Vanished)
                {
                    return;
                }

                double damage;
```

- [ ] **Step 2: `Fireball.cs`**

In `Projects/UOContent/Spells/Third/Fireball.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, ref source, ref m);

                double damage;
```

with the same guard shape as Step 1.

- [ ] **Step 3: `EnergyBolt.cs`**

In `Projects/UOContent/Spells/Sixth/EnergyBolt.cs`, replace:

```csharp
                SpellHelper.CheckReflect((int)Circle, ref source, ref m);

                double damage;
```

with the same guard shape as Step 1.

- [ ] **Step 4: Full build and test run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions.

```bash
git add Projects/UOContent/Spells/First/MagicArrow.cs Projects/UOContent/Spells/Third/Fireball.cs Projects/UOContent/Spells/Sixth/EnergyBolt.cs
git commit -m "feat: skip spell effect on a double-shield Vanished reflect (3 more spells)"
```

---

## Task 7: Update the 2 Mysticism `if`-branch call sites

**Files:**
- Modify: `Projects/UOContent/Spells/Mysticism/BombardSpell.cs:44-53`
- Modify: `Projects/UOContent/Spells/Mysticism/EagleStrikeSpell.cs:37-47`
- Test: `Projects/UOContent.Tests/Tests/Spells/MysticismReflectCallSiteTests.cs` (see note below on why this task alone gets a dedicated test)

**Interfaces:**
- Consumes: `SpellHelper.CheckReflect(int, ref Mobile, ref Mobile) -> ReflectResult` (Task 3).
- Produces: nothing new.

These two already branch on the return value (unlike every other call site) — Task 3, Step 6 already made them compile again with an `== ReflectResult.Reflected` equality check, but that was a stopgap that doesn't yet skip the rest of `Target()` on `Vanished`. This task finishes the job properly: the code that already runs *inside* the `if` (a reflect visual-effect timer) must run only on `Reflected`, and everything *after* the `if` block (the real damage/knockback logic) must be skipped entirely on `Vanished`.

Because these two files' structure is different enough from the mechanical Task 5/6 pattern (existing `if`-branch, `Timer.StartTimer` closures) to be worth a concrete test, this task gets one — restructuring an `if` incorrectly (e.g. leaving `Vanished` falling into the "no reflect" path instead of returning early) is exactly the kind of mistake a reviewer could plausibly make here but not in the mechanical batches.

- [ ] **Step 1: Write the failing test**

Create `Projects/UOContent.Tests/Tests/Spells/MysticismReflectCallSiteTests.cs`. This drives `SpellHelper.CheckReflect` directly with the exact circle numbers these two spells hardcode (`6` for Bombard, `2` for Eagle Strike) to confirm the return-value contract they now depend on, without needing a full spell cast:

```csharp
using System;
using Server.Custom;
using Server.Spells;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class MysticismReflectCallSiteTests
{
    [Theory]
    [InlineData(6)] // BombardSpell's hardcoded circle
    [InlineData(2)] // EagleStrikeSpell's hardcoded circle
    public void DoubleShield_ReturnsVanished_ForTheseSpellsCircles(int circle)
    {
        var caster = new Mobile(World.NewMobile);
        caster.DefaultMobileInit();
        var target = new Mobile(World.NewMobile);
        target.DefaultMobileInit();
        SpellReflect.Apply(caster, TimeSpan.FromMinutes(5));
        SpellReflect.Apply(target, TimeSpan.FromMinutes(5));

        var result = SpellHelper.CheckReflect(circle, caster, ref target);

        Assert.Equal(ReflectResult.Vanished, result);

        caster.Delete();
        target.Delete();
    }
}
```

This is a thin, deliberately redundant confirmation on top of Task 3's already-thorough `CheckReflectTests.cs` — it exists to pin the exact circle constants these two files hardcode, so a future refactor of either spell's circle number would be caught here too.

- [ ] **Step 2: Run the test to verify it passes already**

Run: `dotnet test --filter "FullyQualifiedName~MysticismReflectCallSiteTests"`
Expected: PASS immediately — this test only exercises `SpellHelper.CheckReflect`, already correct since Task 3. It's establishing a baseline before the source edits below, which are about the *callers'* control flow, not `CheckReflect` itself.

- [ ] **Step 3: Rewrite `BombardSpell.cs`**

In `Projects/UOContent/Spells/Mysticism/BombardSpell.cs`, replace:

```csharp
            var source = Caster;

            if (SpellHelper.CheckReflect(6, ref source, ref m) == ReflectResult.Reflected)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(0.5), () =>
                {
                    source.MovingEffect(m, 0x1363, 12, 1, false, true, 0, 0);
                    source.PlaySound(0x64B);
                });
            }

            Caster.MovingEffect(m, 0x1363, 12, 1, false, true, 0, 0);
```

with:

```csharp
            var source = Caster;
            var reflectResult = SpellHelper.CheckReflect(6, ref source, ref m);

            if (reflectResult == ReflectResult.Vanished)
            {
                return;
            }

            if (reflectResult == ReflectResult.Reflected)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(0.5), () =>
                {
                    source.MovingEffect(m, 0x1363, 12, 1, false, true, 0, 0);
                    source.PlaySound(0x64B);
                });
            }

            Caster.MovingEffect(m, 0x1363, 12, 1, false, true, 0, 0);
```

(This starts from the Task 3, Step 6 stopgap state — `== ReflectResult.Reflected` is already there; this step adds the `Vanished` early-return above it. If executing this plan starting fresh from before Task 3, the "before" block instead has `if (SpellHelper.CheckReflect(6, ref source, ref m))` with a `bool` return — either starting shape ends at the same "after".)

- [ ] **Step 4: Rewrite `EagleStrikeSpell.cs`**

In `Projects/UOContent/Spells/Mysticism/EagleStrikeSpell.cs`, replace:

```csharp
            var source = Caster;

            if (SpellHelper.CheckReflect(2, ref source, ref m) == ReflectResult.Reflected)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(0.5), () =>
                {
                    /* Conjures a magical eagle that assaults the Target with its talons, dealing energy damage. */
                    source.MovingEffect(m, 0x407A, 8, 1, false, true, 0, 0);
                    source.PlaySound(0x2EE);
                });
            }

            Caster.MovingParticles(m, 0x407A, 7, 0, false, true, 0, 0, 0xBBE, 0xFA6, 0xFFFF, 0);
```

with:

```csharp
            var source = Caster;
            var reflectResult = SpellHelper.CheckReflect(2, ref source, ref m);

            if (reflectResult == ReflectResult.Vanished)
            {
                return;
            }

            if (reflectResult == ReflectResult.Reflected)
            {
                Timer.StartTimer(TimeSpan.FromSeconds(0.5), () =>
                {
                    /* Conjures a magical eagle that assaults the Target with its talons, dealing energy damage. */
                    source.MovingEffect(m, 0x407A, 8, 1, false, true, 0, 0);
                    source.PlaySound(0x2EE);
                });
            }

            Caster.MovingParticles(m, 0x407A, 7, 0, false, true, 0, 0, 0xBBE, 0xFA6, 0xFFFF, 0);
```

- [ ] **Step 5: Full build and test run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions.

```bash
git add Projects/UOContent/Spells/Mysticism/BombardSpell.cs Projects/UOContent/Spells/Mysticism/EagleStrikeSpell.cs Projects/UOContent.Tests/Tests/Spells/MysticismReflectCallSiteTests.cs
git commit -m "feat: BombardSpell/EagleStrikeSpell skip their effect on a Vanished reflect"
```

---

## Task 8: Update `MindBlast.cs` (2 call sites in one file)

**Files:**
- Modify: `Projects/UOContent/Spells/Fifth/MindBlast.cs:31-108` (both the `Core.AOS` and non-AOS branches of `Target()`)

**Interfaces:**
- Consumes: `SpellHelper.CheckReflect(int, ref Mobile, ref Mobile) -> ReflectResult` (Task 3).
- Produces: nothing new.

`MindBlastSpell.Target()` has two entirely separate code paths (`if (Core.AOS) { ... } else if (CheckHSequence(m)) { ... }`), each with its own `CheckReflect` call using local `from`/`target` variables instead of `source`/`m`. Both need the same guard, placed in each branch.

- [ ] **Step 1: AOS branch**

In `Projects/UOContent/Spells/Fifth/MindBlast.cs`, replace:

```csharp
                    Mobile from = Caster, target = m;

                    SpellHelper.Turn(from, target);

                    SpellHelper.CheckReflect((int)Circle, ref from, ref target);

                    var damage = Math.Min((int)((Caster.Skills.Magery.Value + Caster.Int) / 5), 60);
```

with:

```csharp
                    Mobile from = Caster, target = m;

                    SpellHelper.Turn(from, target);

                    if (SpellHelper.CheckReflect((int)Circle, ref from, ref target) == ReflectResult.Vanished)
                    {
                        return;
                    }

                    var damage = Math.Min((int)((Caster.Skills.Magery.Value + Caster.Int) / 5), 60);
```

- [ ] **Step 2: non-AOS branch**

Same file, replace:

```csharp
                Mobile from = Caster, target = m;

                SpellHelper.Turn(from, target);

                SpellHelper.CheckReflect((int)Circle, ref from, ref target);

                // Algorithm: (highestStat - lowestStat) / 2 [- 50% if resisted]
```

with:

```csharp
                Mobile from = Caster, target = m;

                SpellHelper.Turn(from, target);

                if (SpellHelper.CheckReflect((int)Circle, ref from, ref target) == ReflectResult.Vanished)
                {
                    return;
                }

                // Algorithm: (highestStat - lowestStat) / 2 [- 50% if resisted]
```

(Note this branch is nested one level shallower — 4 spaces less indentation than Step 1's AOS branch, since it's directly inside `else if (CheckHSequence(m))` rather than a further-nested `if (Caster.CanBeHarmful(m) && CheckSequence())`. Match the file's actual indentation.)

- [ ] **Step 3: Full build and test run, then commit**

Run: `dotnet build` — confirm clean.
Run: `dotnet test Projects/UOContent.Tests` — confirm no regressions.

```bash
git add Projects/UOContent/Spells/Fifth/MindBlast.cs
git commit -m "feat: MindBlast skips its effect on a Vanished reflect (both era branches)"
```

---

## Task 9: Repo docs and final full regression

**Files:**
- Modify: `custom-docs/CUSTOM_CHANGES.md`
- Modify: `custom-docs/LAVORI_IN_CORSO.md`

**Interfaces:** none — documentation only.

- [ ] **Step 1: Add rows to `CUSTOM_CHANGES.md`**

Append one row per pre-existing file this plan touched (new files — `SpellReflect.cs`, `ReflectResult.cs`, and the test files — are not logged here, per this doc's own stated scope: only pre-existing files). Use today's date (the date this step actually runs) for every row. Add these rows to the existing `| File | Motivo | Data |` table:

```markdown
| `Projects/Server/Mobiles/Mobile.cs` | Add `SpellReflectActive`/`PiercesSpellReflect` bool properties for the Magic Reflection redesign (targetable reflect shield) | <today> |
| `Projects/UOContent/Spells/Base/SpellHelper.cs` | `CheckReflect` return type changes from `bool` to `ReflectResult` (None/Reflected/Vanished); adds the new shield + piercing checks ahead of the untouched legacy `MagicDamageAbsorb`/`BaseCreature` reflect path | <today> |
| `Projects/UOContent/Spells/Fifth/MagicReflect.cs` | Full rewrite: targetable single-use reflect shield (target-first casting) replaces the old self-only passive resistance buff, no era gating | <today> |
| `Projects/UOContent/Spells/First/Weaken.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/First/Clumsy.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/First/Feeblemind.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/First/MagicArrow.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Second/Harm.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Third/Poison.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Third/Fireball.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Fourth/Curse.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Fourth/ManaDrain.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Fourth/Lightning.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Fifth/Paralyze.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Fifth/MindBlast.cs` | Skip spell effect on a double-shield Vanished reflect (both era branches) | <today> |
| `Projects/UOContent/Spells/Sixth/Explosion.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Sixth/EnergyBolt.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Seventh/FlameStrike.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Seventh/ManaVampire.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Mysticism/SpellPlagueSpell.cs` | Skip spell effect on a double-shield Vanished reflect | <today> |
| `Projects/UOContent/Spells/Mysticism/BombardSpell.cs` | Restructure existing reflect-effect `if` to also skip the whole spell effect on Vanished | <today> |
| `Projects/UOContent/Spells/Mysticism/EagleStrikeSpell.cs` | Restructure existing reflect-effect `if` to also skip the whole spell effect on Vanished | <today> |
```

- [ ] **Step 2: Add a `LAVORI_IN_CORSO.md` entry**

Add a new section, following the exact structure of the existing "Target-first casting su Flame Strike" entry in the same file (Stato/Fatto/Manca bullets):

```markdown
## Magic Reflection — scudo riflettente targetabile

- **Stato:** Implementato, buildato, testato (unit test su `SpellReflect`, `SpellHelper.CheckReflect`, wiring target-first di `MagicReflectSpell` — copertura diretta della logica, non attraverso un cast completo in-game, per la stessa ragione già nota per Flame Strike: il fizzle roll di `CheckSequence()` non è controllabile in modo affidabile in questo ambiente di test) — **non ancora verificato in gioco**.
- **Cosa copre:** redesign completo di `MagicReflectSpell` (`Spells/Fifth/MagicReflect.cs`) da buff passivo di resistenza a scudo riflettente targetabile a singolo utilizzo, riflette tutte le 19 spell (Magery/Necromancy commentate escluse/Mysticism) che chiamano `SpellHelper.CheckReflect`, caso "doppio scudo" (nessuno prende danno), scadenza a 5 minuti, hook di perforazione (`Mobile.PiercesSpellReflect`) per una futura abilità PvP non ancora costruita.
- **Fatto:**
  - `Projects/Server/Mobiles/Mobile.cs`: `SpellReflectActive`, `PiercesSpellReflect`
  - `Projects/UOContent/Custom/SpellReflect.cs`: Apply/Clear/IsActive, scadenza a timer, buff icon
  - `Projects/UOContent/Spells/Base/ReflectResult.cs`: nuovo enum None/Reflected/Vanished
  - `Projects/UOContent/Spells/Base/SpellHelper.cs`: `CheckReflect` riscritto, path legacy (`MagicDamageAbsorb`/`MeerCaptain`) intatto
  - `Projects/UOContent/Spells/Fifth/MagicReflect.cs`: riscrittura completa, target-first, nessun gating per era
  - 19 file spell (elencati in `CUSTOM_CHANGES.md`): guardia "Vanished" dopo la chiamata a `CheckReflect` esistente
  - Build e `dotnet test` puliti
- **Manca (da verificare in gioco, uno per uno):**
  - [ ] Cast su se stessi → scudo applicato, buff icon visibile
  - [ ] Cast su un altro giocatore → scudo applicato a lui, non a te
  - [ ] Bersaglio scudato colpito da una spell offensiva → nessun danno a lui, il danno arriva invece a chi ha lanciato la spell, ricalcolato sulle sue resistenze
  - [ ] Doppio scudo (entrambi attivi) → nessuno dei due prende danno, entrambi gli scudi si rompono
  - [ ] Scudo mai consumato → sparisce da solo dopo 5 minuti
  - [ ] Mirino target-first: ti muovi liberamente in fase 1, non puoi menare fendenti in fase 2 fino a risoluzione/flizzo
  - [ ] Cast su una creatura/mostro → rifiutato con messaggio, nessuno scudo applicato
  - [ ] Verificare a schermo che il testo del buff icon (cliloc 1075817/1075818, riusato dal vecchio Magic Reflection) abbia senso per il nuovo scudo — se descrive ancora i vecchi numeri di resistenza, va sostituito con un cliloc più generico
  - Decisione finale: tenere, aggiustare, o revert
- **Fuori scope, noto e accettato:** Fire Field/Paralyze Field non riflettono (non passano da `CheckReflect`); le creature non possono ricevere lo scudo; `DuelContext.cs` non resetta il nuovo scudo all'inizio di un duello (stesso gap già accettato per `MagicShieldAbsorb`); l'abilità PvP che userà `PiercesSpellReflect` non è ancora stata costruita.
```

- [ ] **Step 3: Full solution build and full test suite, then commit**

Run: `dotnet build` from the repo root — confirm clean across every project.
Run: `dotnet test` from the repo root — confirm no regressions anywhere in the solution, not just `UOContent.Tests`.

```bash
git add custom-docs/CUSTOM_CHANGES.md custom-docs/LAVORI_IN_CORSO.md
git commit -m "docs: log Magic Reflection shield changes and track manual verification"
```

---

## Out of scope for this plan (tracked elsewhere or deliberately not built)

- Fire Field / Paralyze Field reflect — they apply damage directly (`FireFieldItem.OnMoveOver`/`InternalTimer`, `ParalyzeField.OnMoveOver`), bypassing `CheckReflect` entirely; not touched.
- Creature-targetable shields — `ValidateTargetFirst`/`Target()` both reject non-player Mobiles; `BaseCreature.CheckReflect`'s own separate logic is untouched.
- The PvP class-evolution ability that will eventually set `Mobile.PiercesSpellReflect` — only the flag and the engine's respect for it are built here (Task 3).
- `DuelContext.cs` duel-start cleanup for the new shield — known, deliberately-accepted gap, same treatment as the sibling `MagicShieldAbsorb` feature.
- Any new/changed cliloc text for the buff icon tooltip — reuses the existing `1075817`/`1075818` pair already tied to `BuffIcon.MagicReflection`; flagged in the manual verification checklist (Task 9) rather than resolved here, since the exact client-side wording couldn't be verified from this repo (cliloc strings are client data, not server source).
- The sibling "cast-in-motion for the rest of the mage's spellbook" work (`custom-docs/specs/2026-09-17-mage-spellbook-cast-in-motion-design.md`) — separate plan, not part of this one.

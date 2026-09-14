# Custom Changes Log

Traccia le modifiche fatte a file **esistenti** di ModernUO (stock content in `Projects/UOContent` o motore in `Projects/Server`) — non il contenuto nuovo, che vive in [`Projects/UOContent/Custom/`](../Projects/UOContent/Custom/) e non richiede tracciamento perché non collide mai con gli aggiornamenti upstream.

Ogni riga qui sotto aiuta, ad ogni sync con l'upstream, a capire quali modifiche in arrivo rischiano di entrare in conflitto con le nostre.

| File | Motivo | Data |
|---|---|---|
| `Projects/UOContent/Spells/Base/Spell.cs` | Add TargetFirst opt-in flag and its supporting members (ValidateTargetFirst, HasReagents, ConsumeCastingResources) - purely additive, no behavior change yet | 2026-09-14 |
| Projects/UOContent/Spells/Base/Spell.cs | Cast() branches on TargetFirst: shows the target cursor immediately instead of after the cast delay, when a spell opts in | 2026-09-14 |
| Projects/UOContent/Spells/Base/Spell.cs | Add BeginTargetFirstDelay() (phase-2 kickoff), give CastTimer an optional resolve delegate, charge mana/reagents on Disturb() once a TargetFirst cast is committed | 2026-09-14 |
| `Projects/UOContent/Spells/Seventh/FlameStrike.cs` | Prova: `BlocksMovement => false` per rendere lo spell castabile in movimento (stesso pattern già usato da tutti gli spell di Chivalry) | 2026-09-13 |
| Projects/UOContent/Spells/Seventh/FlameStrike.cs | Opt into TargetFirst casting (target cursor appears immediately, cast delay happens after target is picked) | 2026-09-14 |
| Projects/UOContent/Spells/Targeting/SpellTarget.cs | OnTarget defers resolution for TargetFirst spells through Spell.BeginTargetFirstDelay instead of resolving instantly; re-validates range/LOS/validity when the delay finishes | 2026-09-14 |
| Projects/UOContent/Spells/Base/Spell.cs | Fix: cancel a lingering TargetFirst phase-1 cursor on disturb; move OnSpellCast/NextSpellTime from click-time to resolution-time to match normal cast-recovery timing | 2026-09-15 |
| Projects/UOContent/Spells/Targeting/SpellTarget.cs | Fix: guard against acting on a stale/disturbed TargetFirst spell instance on a delayed click | 2026-09-15 |
| Projects/UOContent/Spells/Base/Spell.cs | Fix (final review): defer FinishSequence() past TargetFirst resolution via new TargetFirstCommitted accessor + EndTargetFirstCommitment(); restructure Cast()'s failure messaging so a TargetFirst reagent-gate failure sends 502630 instead of "insufficient mana"; remove dead CS0169 pragma; doc/Delta(MobileDelta.Flags) parity fixes | 2026-09-15 |
| Projects/UOContent/Spells/Targeting/SpellTarget.cs | Fix (final review): OnTargetFinish defers to ResolveTargetFirst instead of tearing the spell down before the phase-2 timer can fire; ResolveTargetFirst now owns FinishSequence() on every exit path | 2026-09-15 |
| Projects/UOContent.Tests/Tests/Spells/TargetFirstCastingTests.cs | New: regression test pinning that a TargetFirst spell is still live (Caster.Spell set, State == Casting) immediately after Target.Invoke() returns | 2026-09-15 |

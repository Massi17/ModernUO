# Skill — design

## Come funziona in ModernUO (riferimento per la fattibilità)

**File chiave:**
- `Projects/Server/Skills.cs` — `enum SkillName` (57 valori fissi, 0-57: Alchemy...Throwing), classe `Skill` (valore per singola skill: `Base`, `Cap`, `Lock`, formula `Value`/`NonRacialValue`), classe `SkillInfo` (metadata per skill: scaling su Str/Dex/Int, gain rate, stat primaria/secondaria), classe `Skills` (collezione per-mobile).
- `Projects/UOContent/Skills/SkillsInfo.cs` — carica `SkillInfo.Table` da `Distribution/Data/skills.json` (dati puri, non hardcoded in C#); raggruppamenti per categoria (CombatSkills, CraftSkills, MagicSkills, ecc.) filtrati per espansione (`Core.Expansion`).
- `Projects/UOContent/Skills/SkillCheck.cs` — motore dei check: `CheckSkill()` calcola successo (`chance >= random`) e chiama `Gain()` per l'aumento; formula di gain combina cap totale, cap della skill, difficoltà del check, `GainFactor` da skills.json.
- `Projects/UOContent/Skills/SkillEvents.cs` — evento statico `SkillUsed(Mobile, Skill, bool success)`, pensato apposta per subscriber cross-assembly (utile per hook custom senza toccare il core).

**Come funziona ad alto livello:**
1. Un'azione di gioco chiama `Mobile_SkillCheckLocation/Target` (via `Mobile.SkillCheck*Handler`) passando min/max skill richiesta.
2. Si calcola `chance` in base al valore attuale (`Skill.Value`, che include bonus da Str/Dex/Int scalati) e si tira un random.
3. Se il personaggio è vivo e la region lo permette (`AllowGain`, anti-macro incluso), c'è una probabilità di **gain** — formula in `SkillCheck.Gain()`, che rispetta `Skill.Cap` e `Skills.Cap` (limite totale, default 7000 = 700.0 skill points).
4. `SkillEvents.SkillUsed` viene invocato *dopo ogni tentativo*, successo o meno — è il punto di aggancio più pulito per logica custom (achievement, XP alternativo, ecc.) senza modificare file esistenti.

**Punti di estensione — fattibilità:**

| Modifica | Fattibilità | Dove |
|---|---|---|
| Cambiare formule/rate di gain, scaling su stat, cap di una skill esistente | **Facile** — solo dati/config, zero codice engine | `Distribution/Data/skills.json` (+ eventualmente `stats.*` in ServerConfiguration) |
| Aggiungere logica custom quando una skill viene usata (hook, reward, tracking) | **Facile** — nessuna modifica a file esistenti | Sottoscrivi `SkillEvents.SkillUsed` da un file nuovo in `Custom/` |
| Cambiare la formula di check/gain stessa (non solo i parametri) | **Media** — richiede modificare `SkillCheck.cs` (file esistente, va loggato in `CUSTOM_CHANGES.md`) | `Projects/UOContent/Skills/SkillCheck.cs` |
| Aggiungere una skill **completamente nuova** (mai vista dal client UO ufficiale) | **Fattibile** (questo progetto usa un client custom) — il server è data-driven (basta un nuovo valore enum + riga in skills.json + il packet skill-list è già dinamico, legge `skills.Length`); serve però anche aggiungere la skill nel client custom (nome, posizione nella skill gump, stringhe localizzate) | Serve `enum SkillName` + `skills.json` + verificare `SkillsInfo.CombatSkills`/`CraftSkills`/ecc. se rilevante alla categoria, **più** la modifica lato client (già in scope, vedi [`README.md`](README.md)) |

**Insidie note:**
- `Skills.Cap` è il totale (7000 = 700 skill points complessivi, il classico cap "GM in 7 skill"); cambiarlo è una riga di config ma impatta bilanciamento globale.
- `SkillCheck.Gain()` ha casi speciali (creature, pet, `Focus` esclusa per `BaseCreature`, skill "accelerate" per eventi) — attenzione a non romperli se si tocca la formula generale.
- La serializzazione di `Skill`/`Skills` è versionata a mano (non col code-gen `[SerializationGenerator]` usato altrove) — se si aggiunge un campo persistito, va gestito manualmente il bump di versione.

## Decisioni custom

Nessuna decisione ancora presa.

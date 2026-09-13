# Spell — design

## Come funziona in ModernUO (riferimento per la fattibilità)

**File chiave** (tutti in `Projects/UOContent/Spells/`):
- `Base/Spell.cs` (990 righe) — classe astratta `Spell`, il cuore del sistema: gestisce il flusso di cast (`Cast()`, `CheckSequence()`, timer di cast/animazione, fizzle, disturb, mana, cast delay/recovery scalati da FC/FCR).
- `Base/MagerySpell.cs` — sottoclasse intermedia per gli incantesimi di Magery (circoli 1-8).
- `Base/SpellInfo.cs` — metadata statici per spell (nome, mantra, reagenti, effetti visivi mano sinistra/destra, `AllowTown`).
- `Base/SpellRegistry.cs` — array fisso `Type[700]` che mappa **spellID numerico → classe C#** (usato da spellbook/scroll per istanziare lo spell da castare).
- `Base/SpellHelper.cs` (1414 righe) — utility condivise: danno, resist, reflect, teleport, field placement, ecc.
- `Initializer.cs` — registra ogni spell con `Register(spellId, type)`, con gating per espansione (`if (Core.AOS)`, `if (Core.SE)`, `if (Core.ML)`, `if (Core.SA)`).
- Cartelle `First/`…`Eighth/` (Magery per circolo), `Necromancy/`, `Chivalry/`, `Bushido/`, `Ninjitsu/`, `Spellweaving/`, `Mysticism/`, `Gargoyle/SpellDefinitions/` — uno spell leaf per file.

**Flusso ad alto livello:**
1. Il giocatore avvia il cast (spellbook, scroll, wand) → si crea un'istanza `Spell` via `SpellRegistry.NewSpell(spellID, caster, scroll)`.
2. `Cast()` verifica stato (non già in cast, non paralizzato, mana sufficiente, spell non bloccato da feature flag, ecc.), fa partire `CastTimer` per la durata del cast (scalata da Faster Casting).
3. Al termine del timer, `CheckSequence()` riverifica le condizioni (reagenti consumati con `ConsumeReagents()`, mana, skill check via `CheckFizzle()`) e chiama `OnCast()` sullo spell concreto.
4. Lo spell concreto (es. `MagicArrowSpell.OnCast()`) tipicamente apre un target (`SpellTarget<Mobile>`) e nel callback (`Target(m)`) applica l'effetto vero: danno (`SpellHelper.Damage(...)`, `GetNewAosDamage(...)`), buff/debuff (vedi il design `effects.md` per il meccanismo `BuffInfo`), evocazioni, teleport, ecc. — più effetti visivi/sonori (`MovingParticles`, `PlaySound`).
5. Ogni spell leaf è minimale: eredita da `MagerySpell` (o dalla base della propria scuola), definisce `_info` statico, override di `Circle`, `OnCast()`, e la logica dell'effetto — vedi `Spells/First/MagicArrow.cs` come esempio canonico (≈70 righe totali).

**Vincoli/insidie note — determinano cosa è facile vs difficile:**

| Modifica | Fattibilità | Note |
|---|---|---|
| Nuovo spell in una scuola esistente (Magery/Necro/Chivalry/ecc.), riusando icone/effetti visivi client già esistenti | **Facile** — file nuovo in `Custom/`, zero conflitti | Serve uno spellID libero nel range della scuola (vedi sotto) |
| Modificare danno/formula/mana/reagenti di uno spell esistente | **Facile** — un file esistente, ma va loggato in `CUSTOM_CHANGES.md` | Ogni spell leaf è isolato, rischio conflitto upstream basso salvo che modifichino proprio quel file |
| Nuova scuola di magia interamente nuova (nuovo spellbook type, nuova skill associata) | **Media-difficile** — architettura estendibile ma tocca più punti | `SpellbookType` enum, range di spellID dedicato, nuova skill (vedi design `skills.md`), UI gump del libro |
| Spell con icona/effetto grafico **mai esistito nel client UO ufficiale** | **Fattibile** (questo progetto usa un client custom) — serve aggiungere l'asset nel client | Gli `int` in `SpellInfo`/animazioni sono ID di asset; vanno aggiunti/mappati anche lato client custom, non solo lato server |

**Il vincolo numerico degli spellID (il punto più importante per la fattibilità):**
- `SpellRegistry` è un array `Type[700]` indicizzato per spellID — non c'è collisione tecnica ad aggiungere ID liberi, ma **il contenuto dello spellbook (`Spellbook.Content`, campo `ulong`) è una bitmask a 64 bit**: ogni scuola ha un range di ID riservato che deve stare dentro 64 valori consecutivi (vedi `Spellbook.GetTypeForSpell`, `Items/Skill Items/Magical/Spellbook.cs:345`): Magery 0-63, Necromancy 100-116, Paladin 200-209, Samurai 400-405, Ninja 500-507, Spellweaving/Arcanist 600-616, Mysticism 677-692.
- Per aggiungere uno spell a una scuola esistente serve un ID libero **dentro il range di quella scuola** (es. Mysticism ha slot liberi commentati nell'`Initializer.cs`: 677-681, 685-686, 692 — segno che ModernUO stesso lascia margine).
- Per una scuola interamente nuova servirebbe un range di 64 ID mai usato e un nuovo valore in `SpellbookType`.

## Decisioni custom

Nessuna decisione ancora presa.

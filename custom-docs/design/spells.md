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

**La macchina a stati del cast (`SpellState`: `None` → `Casting` → `Sequencing` → `None`):**
- **`Casting`**: dalla chiamata a `Cast()` fino allo scadere del `CastTimer` (durata = `GetCastDelay()`, scalata da Faster Casting — cap 2 per Magery/Necromancy, 4 per altre skill, -2 col Protection spell attivo, -1 fisso in Stygian Abyss). Interrompibile (vedi Disturb sotto). In questo stato parte anche `AnimTimer` (rianima le mani ogni 1.5s) e le mani vengono liberate (disarma temporaneamente le armi).
- **`Sequencing`**: dal termine del `CastTimer` (che imposta subito `NextSpellTime = ora + GetCastRecovery()`, il "Faster Cast Recovery") fino al completamento di `CheckSequence()` — tipicamente il tempo in cui il client aspetta la selezione del bersaglio. Non più interrompibile da un colpo subito.
- **`None`**: nessuno spell attivo.

**Il personaggio si blocca durante il cast? Sì, di default — con un'eccezione nota.** `Spell.BlocksMovement` (default: `IsCasting`, cioè vero solo nello stato `Casting`) viene controllato da `Mobile.CanMove()` (`Projects/Server/Mobiles/Mobile.cs:4135`): se vero, il **server rifiuta il movimento**, non è solo un vincolo visivo lato client. Lo stesso flag blocca anche i colpi con arma mentre si casta (controllato in `BaseWeapon.cs:844` e `BaseRanged.cs:57`). Il blocco si toglie automaticamente appena si passa a `Sequencing` — si può quindi camminare mentre si aspetta di selezionare il bersaglio, dopo che la barra di cast è finita. **Eccezione già implementata:** tutti gli spell di Chivalry (`Spells/Chivalry/*.cs`) sovrascrivono `BlocksMovement => false` — i Paladini possono muoversi durante il cast, fedele alle regole classiche di UO. È un pattern riusabile: per rendere una skill/scuola custom "castabile in movimento" basta override di quella property, nessuna modifica a `Spell.cs`.

**Interruzione (`Disturb`):** un colpo subito durante `Casting` (`OnCasterHurt`) ha una probabilità di interrompere il cast, resistibile con lo spell Protection attivo (più alto il livello, meno probabile l'interruzione). Se scatta: i timer si fermano, si applica una penalità di recovery (`GetDisturbRecovery()`, solo pre-AoS — in AoS è zero), messaggio "concentrazione disturbata". Una volta in `Sequencing`, il colpo non interrompe più.

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

### Cast differito ("target-first casting")

Flusso alternativo di cast, opt-in per singolo spell, pensato per gli spell dove ha senso scegliere prima il bersaglio e pagare il costo dopo (oggi solo **Flame Strike**). Dettagli di design originali in `custom-docs/specs/2026-09-14-target-first-casting-design.md` e `custom-docs/specs/2026-09-15-cast-interrupt-recast-design.md` — questa sezione è il riferimento aggiornato al comportamento attuale (le regole sotto hanno sostituito alcune decisioni originali di quei documenti, vedi `LAVORI_IN_CORSO.md` per la cronologia dei test).

**Le due fasi** (entrambe restano `SpellState.Casting` — a differenza del cast normale, dove il momento "mirino aperto in attesa del click" è già `Sequencing`):

| Fase | Da... a... | Cosa è già "impegnato" |
|---|---|---|
| **Fase 1** (mirino aperto) | Dal cast al click sul bersaglio | Niente — nessun mantra, nessuna mana/reagenti spesi. È solo mira, non un vero cast. |
| **Fase 2** (delay in corso) | Dal click alla risoluzione | Tutto — da qui in poi qualunque esito (successo o fallimento) addebita mana/reagenti. |

**Chi usa questo flusso:** `Spell.UsesDeferredCast` è vero se lo spell ha `TargetFirst => true` (opt-in diretto, oggi solo `FlameStrikeSpell`) **oppure** se sta interrompendo un altro cast già in corso (`_interruptedSpell != null` — vedi "Interrompere un cast per lanciarne un altro" in `LAVORI_IN_CORSO.md`). In quest'ultimo caso il meccanismo di fase 1/fase 2 si applica anche a spell che non hanno `TargetFirst`, solo perché stanno interrompendo qualcos'altro.

**Cosa succede quando il caster viene disturbato, per fase e per causa:**

| Causa del disturbo | Fase 1 (mirino aperto, non committato) | Fase 2 (dopo il click, delay in corso) |
|---|---|---|
| **Colpo subito** (`DisturbType.Hurt`) | **Nulla.** Nessun costo, mirino resta aperto, nessuno stato cambia — un colpo semplice non può interrompere qualcosa che non è ancora davvero iniziato. | Addebita mana/reagenti e fa fallire lo spell (nessun danno). Oggi **qualunque** colpo lo fa scattare — nessuna distinzione per tipo di colpo o probabilità (vedi nota "da decidere" sotto). |
| **Nuovo cast** (`DisturbType.NewCast`, un altro spell interrompe questo) | Mirino cancellato, **nessun costo** — ma se questo spell stava a sua volta interrompendone un altro, quell'obbligo pendente viene comunque saldato subito. | Addebita mana/reagenti (era comunque già "impegnato"). |
| **Morte** (`DisturbType.Kill`) | Mirino cancellato, nessun costo. | Addebita mana/reagenti. |
| **Richiesta di equip/uso oggetto** | Mirino cancellato, nessun costo. | Addebita mana/reagenti. |

**Messaggio "Target can not be seen." al click (fase 1):** per qualunque spell (non solo cast differito), se clicchi un bersaglio senza linea di vista, di norma **non succede nulla** — nessun messaggio, il motore ignora il click in silenzio (bug preesistente e condiviso da tutti gli spell offensivi di Magery, non introdotto da questo lavoro). `SpellTarget<T>` (in `Spells/Targeting/SpellTarget.cs`) ha due flag opzionali per cambiare questo, passati al costruttore:

| Flag | Effetto | Chi lo usa oggi |
|---|---|---|
| *(nessuno, default)* | Click senza LOS → nessun messaggio, nessuna riapertura. | Tutti gli spell tranne Flame Strike |
| `notifyOnLos: true` | Click senza LOS → messaggio "Target can not be seen.", mirino resta chiuso (bisogna rilanciare lo spell per riprovare). | **Solo Flame Strike** (scelta deliberata: fix tenuto scoped, non esteso agli altri spell) |
| `retryOnLos: true` | Click senza LOS → messaggio "Target cannot be seen. Try again.", mirino si riapre **automaticamente** subito, pronto per un nuovo click. | Nessuno spell oggi |

Nota: durante il delay di fase 2, se il bersaglio esce dalla linea di vista PRIMA che il delay finisca, il messaggio "Target can not be seen." parte sempre (non serve nessuno dei due flag) — è un ricontrollo esplicito fatto da `SpellTarget<T>.ResolveTargetFirst`, non passa per `OnTargetOutOfLOS`.

**Decisione futura, non ancora presa (discusso 2026-09-15):** in fase 2, oggi qualunque colpo fa flizzare lo spell incondizionatamente. Da rivedere: quali tipi di colpo devono poter interrompere e con quale probabilità/percentuale, invece di "sempre e comunque". Tracciato in `LAVORI_IN_CORSO.md`.

**Attacco fisico del caster durante il cast (`BlocksMovement` vs `BlocksWeaponSwing`):** di norma un unico flag, `Spell.BlocksMovement` (default `IsCasting`), governa sia "posso muovermi mentre casto" sia "posso colpire con l'arma mentre casto" — `BaseWeapon.OnSwing` blocca il colpo se `Caster.Spell.IsCasting && Caster.Spell.BlocksWeaponSwing`. I due comportamenti sono ora **disaccoppiabili**: `BlocksWeaponSwing` (virtual, default `=> BlocksMovement`) può essere sovrascritto indipendentemente.

| Spell | `BlocksMovement` | `BlocksWeaponSwing` | Effetto |
|---|---|---|---|
| Spell "normale" (non sovrascrive niente) | `IsCasting` (vero per tutto il cast) | uguale a `BlocksMovement` (comportamento invariato, mai cambiato) | Non ti muovi né colpisci per tutta la durata del cast |
| Spell di Chivalry, Flame Strike (fase 1) | `false` | segue comunque `BlocksMovement` se non sovrascritto → `false` | Ti muovi E colpisci liberamente durante il cast |
| **Flame Strike (fase 2)** | `false` (sempre — resta castabile in movimento) | `TargetFirstCommitted` (vero solo dopo il click, fino a flizzo/risoluzione) | Ti muovi liberamente, ma **non puoi colpire con l'arma** dal click fino a quando lo spell flizza o va a segno |

Quando `BlocksWeaponSwing` diventa vero (cioè al click che avvia la fase 2), `Spell.BeginTargetFirstDelay` resetta anche `Caster.NextCombatTime` al delay dell'arma equipaggiata — come se il personaggio avesse appena colpito — così un colpo già "pronto" nello stesso istante del click non scivola attraverso, e non c'è un colpo gratuito istantaneo appena il blocco si toglie.

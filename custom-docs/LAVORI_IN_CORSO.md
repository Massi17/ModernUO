# Lavori in corso

Traccia i lavori **non ancora ufficialmente terminati** — un lavoro esce da questa lista solo quando è stato verificato e tu lo confermi concluso, non quando il codice è scritto/compilato.

Formato per ogni voce: cosa è stato fatto, cosa manca per chiuderlo, stato attuale.

---

## Motore classi/evoluzioni/abilità (segnaposto)

- **Stato:** Implementato, buildato, testato (34 test automatici) — in attesa di verifica in gioco.
- **Cosa copre:** il motore generico descritto in `custom-docs/design/classes.md` (vincolo skill/equip per classe, soglia EXP+HONOR, scelta evoluzione esclusiva tra 3, sblocco passive→pvp/pve→ultimate con pricing multi-valuta per-abilità, respec con rimborso parziale) — validato con **una sola classe segnaposto ("Test") e le sue 3 evoluzioni segnaposto** ("TestAlpha"/"TestBeta"/"TestGamma"), non ancora con contenuto reale. La sottoclasse lavorativa (economia) resta esplicitamente fuori da questo lavoro — un piano separato futuro.
- **Fatto:**
  - `Projects/UOContent/Custom/ClassSystem/`: `ClassSystemConfig.cs` (modello dati + seed di default), `PlayerClassContext.cs` (stato per personaggio, serializzato), `PlayerClassSystem.cs` (persistenza + registry, config caricata pigramente da `Configuration/ClassSystem/classes.json`), `PlayerClassCurrency.cs` (EXP/HONOR), `PlayerClassAssignment.cs` (assegna classe + cap skill), `PlayerClassEquipment.cs` (veto equip per tipo), `PlayerClassEvolution.cs` (soglia + scelta evoluzione), `PlayerClassAbilities.cs` (ordine di sblocco + pricing), `PlayerClassRespec.cs` (respec con rimborso parziale)
  - `Projects/UOContent/Custom/Mobiles/PlayerMobile.ClassSystem.cs`: override `OnEquip` (nuovo file, `PlayerMobile.cs` non toccato)
  - `Projects/UOContent/Custom/Commands/ClassSystemCommands.cs`: comandi `SetClass`/`AwardExp`/`AwardHonor`/`RespecEvolution`/`RespecClass` (GM) e `ChooseEvolution`/`UnlockAbility`/`ClassStatus` (player)
  - Build e `dotnet test` puliti (34/34 sui nuovi test, nessun fallimento preesistente toccato)
  - Nessuna riga in `CUSTOM_CHANGES.md`: nessun file preesistente è stato modificato, solo file nuovi
- **Manca (da verificare in gioco, uno per uno):**
  - [ ] `[SetClass Test` su un personaggio target → messaggio di conferma, `[ClassStatus` mostra `Class: Test | Evolution: (none yet)`
  - [ ] Tentare `[SetClass Test` una seconda volta sullo stesso personaggio → messaggio di rifiuto ("already belongs"), nessun cambiamento
  - [ ] Dopo `SetClass`, provare ad allenare una skill NON nella whitelist (es. Magery) → cap 0, non allenabile; una skill nella whitelist (es. Swords) → allenabile fino a 100
  - [ ] Provare a equipaggiare una Katana dopo `SetClass Test` → rifiutata con messaggio "Your class cannot use that.", l'item resta nel backpack
  - [ ] Equipaggiare un'arma NON in lista (es. Dagger) dopo `SetClass Test` → funziona normalmente
  - [ ] `[ChooseEvolution TestAlpha` prima di raggiungere la soglia (EXP+HONOR lifetime) → messaggio di rifiuto ("threshold")
  - [ ] `[AwardExp 1000` (o oltre la soglia configurata) poi `[ChooseEvolution TestAlpha` → successo, `[ClassStatus` mostra l'evoluzione
  - [ ] `[ChooseEvolution TestBeta` dopo aver già scelto `TestAlpha` → messaggio di rifiuto ("already chose")
  - [ ] `[UnlockAbility TestAlpha_pvp_1` prima di aver sbloccato le passive → messaggio di rifiuto ("locked")
  - [ ] Sbloccare entrambe le passive, poi pvp e pve in ordine misto a piacere → tutte si sbloccano, `[ClassStatus` le elenca
  - [ ] `[UnlockAbility TestAlpha_ultimate_1` prima di aver sbloccato TUTTE le pvp/pve → rifiutata; dopo averle sbloccate tutte → riuscita
  - [ ] `[UnlockAbility <id> 0` con un indice di opzione di pagamento esplicito → spende esattamente quella valuta/importo; con un indice fuori range → rifiutata
  - [ ] `[RespecEvolution TestBeta` dopo aver sbloccato alcune abilità → evoluzione cambiata, abilità sbloccate azzerate, parte della valuta spesa rimborsata (~50% di default)
  - [ ] `[RespecClass Test` → stessa dinamica, e i cap delle skill vengono ricalcolati per la nuova classe (in questo test, la stessa "Test", visto che è l'unica classe segnaposto)
  - Decisione finale: tenere il motore così com'è per procedere al contenuto reale delle 4 classi, o aggiustare qualcosa prima

---

## Target-first casting su Flame Strike

- **Stato:** Implementato, buildato, testato, revisionato, **verificato in gioco (11/11)** — due bug trovati e corretti durante la verifica (colpo in fase 1 che flizzava a torto; LOS al click silenziosa senza messaggio). In attesa solo della decisione finale (tenere/generalizzare, aggiustare, o revert)
- **Fatto:**
  - `Spell.cs`: flag `TargetFirst`, `ValidateTargetFirst`, `HasReagents`, `ConsumeCastingResources`, `BeginTargetFirstDelay`, `Disturb()` addebita il costo se interrotto dopo il click
  - `SpellTarget.cs`: `OnTarget` rimanda la risoluzione per gli spell `TargetFirst`, doppio controllo range/LOS/validità
  - `FlameStrike.cs`: `TargetFirst => true`
  - Build e `dotnet test` puliti (nessun fallimento nuovo, solo quelli preesistenti non collegati)
  - Un giro di correzioni dopo la revisione end-to-end: cursore "fantasma" che sopravviveva a un'interruzione prima di scegliere il bersaglio (ora annullato correttamente), e il timer di recupero (`NextSpellTime`) che partiva al click invece che alla fine del delay (ora coerente con tutti gli altri spell)
  - Loggato in `custom-docs/CUSTOM_CHANGES.md`
- **Manca (da verificare in gioco, uno per uno):**
  - [x] Percorso felice: lancia → mirino appare subito → clicca un bersaglio valido → aspetta il delay → il danno arriva — **confermato 2026-09-15**
  - [x] Click su bersaglio troppo lontano → messaggio "That is too far away.", **nessun costo**, mirino sparisce — **confermato 2026-09-15**
  - [x] Click su bersaglio senza linea di vista → messaggio "Target can not be seen.", **nessun costo** — bug preesistente del motore trovato in test (condiviso da tutti gli spell offensivi di Magery, non specifico del pilot: `SpellTarget<T>.OnTargetOutOfLOS` non mandava nulla senza `retryOnLos`). **Corretto solo per Flame Strike** (su richiesta esplicita, per tenere lo scope limitato): nuovo flag opt-in `notifyOnLos` in `SpellTarget.cs`, attivato in `FlameStrike.cs`. **Confermato in gioco 2026-09-15**
  - [x] Click su bersaglio morto/non attaccabile → messaggio di `CanBeHarmful`, **nessun costo** — **confermato 2026-09-15**
  - [x] Bersaglio esce di range durante il nuovo delay (es. si allontana dopo il click) → messaggio "That is too far away." alla fine del timer, **mana e reagenti consumati** — **confermato 2026-09-15**
  - [x] Bersaglio esce dalla linea di vista durante il delay → messaggio "Target can not be seen.", **mana e reagenti consumati** — **confermato da player normale 2026-09-15**
  - [x] Bersaglio muore durante il delay → spell fallito, **mana e reagenti consumati** — **confermato 2026-09-15**
  - [x] Vieni colpito durante il nuovo delay (dopo il click) → spell interrotto, **mana e reagenti consumati**, nessun danno — **confermato 2026-09-15** (comportamento attuale: QUALSIASI colpo fa flizzare in questa fase; deciso di tenerlo così per ora — vedi nota sotto sulla decisione futura su cosa/quanto deve far flizzare)
  - [x] Vieni colpito PRIMA di cliccare un bersaglio (mirino ancora a schermo) → **decisione cambiata rispetto alla spec originale**: non deve succedere assolutamente nulla (niente costo, niente sparizione del mirino) perché la fase 1 non è ancora un vero cast (nessun mantra, nessuna mana/reagenti in gioco) — bug trovato in test 2026-09-15 (qualsiasi colpo flizzava anche qui), **corretto** in `Spell.cs` (`Disturb()` ora ignora `DisturbType.Hurt` durante la fase 1 non ancora committata), coperto da test di regressione, e **riverificato in gioco: confermato corretto 2026-09-15**
  - [x] Annulli il mirino PRIMA di cliccare un bersaglio → **nessun costo** (fase 1 resta gratis) — **confermato 2026-09-15**
  - [x] Mana o reagenti insufficienti: il mirino non deve nemmeno apparire — **confermato 2026-09-15**
  - Decisione finale: tenere la modifica, aggiustare qualcosa, o revert
  - **Decisione futura (non bloccante, non ancora da implementare):** in fase 2 (dopo il click, durante il delay) oggi QUALSIASI colpo fa flizzare lo spell. Da rivedere in futuro: quali tipi di colpo devono poter interrompere e con quale probabilità/percentuale (invece che "sempre e comunque") — discusso 2026-09-15, nessuna decisione presa ancora
- **Note tecniche minori emerse in revisione, non bloccanti, da valutare in futuro:**
  - Un commento XML su `BeginTargetFirstDelay` è rimasto leggermente disallineato dopo la correzione (dice ancora che avvia lui il recovery clock, ora lo fa `CastTimer`)
  - Nel ramo di risoluzione di `CastTimer` manca l'aggiornamento del flag di paralisi (`Caster.Delta(MobileDelta.Flags)`) che il ramo originale invece fa — preesistente, non introdotto da questa feature
  - Nessun test automatico copre lo scenario "click fantasma dopo interruzione" o il timing di `NextSpellTime` — solo verifica manuale per ora

---

## Blocco attacchi fisici durante la fase 2 di Flame Strike

- **Stato:** Implementato, buildato, testato — in attesa di verifica in gioco
- **Perché:** `BlocksMovement => false` su Flame Strike (per poterlo castare in movimento) accoppiava per errore anche "posso colpire con l'arma mentre casto" — `BaseWeapon.cs` usava lo stesso flag per entrambe le cose. Richiesto: poter muoversi ma non poter colpire fisicamente durante la fase 2 (dal click sul bersaglio fino a quando lo spell flizza o va a segno), e resettare il timer del colpo fisico nel momento esatto in cui si clicca il bersaglio.
- **Fatto:**
  - `Spell.cs`: nuovo `BlocksWeaponSwing` (virtual, default `=> BlocksMovement` — nessun cambiamento per nessuno spell che non lo sovrascrive)
  - `BaseWeapon.cs`: `OnSwing` ora legge `BlocksWeaponSwing` invece di `BlocksMovement` direttamente
  - `FlameStrike.cs`: `BlocksWeaponSwing => TargetFirstCommitted` — libero di colpire in fase 1 (sta solo mirando), bloccato in fase 2 fino a flizzo/risoluzione
  - `Spell.cs`: `BeginTargetFirstDelay` (il momento esatto del click che avvia la fase 2) resetta `Caster.NextCombatTime` al delay dell'arma equipaggiata, come se avesse appena colpito
  - Build e `dotnet test` puliti (30/30)
  - Loggato in `custom-docs/CUSTOM_CHANGES.md`
- **Manca (da verificare in gioco):**
  - Fase 1 (mirino aperto, prima del click): puoi ancora colpire normalmente con l'arma
  - Clicchi il bersaglio → da quel momento, tentare di colpire con l'arma non fa nulla (nessun danno, nessuna animazione di colpo)
  - Il blocco dura fino a quando lo spell flizza (colpito, bersaglio invalido, ecc.) o va a segno — dopo, puoi tornare a colpire normalmente
  - Il timer del prossimo colpo fisico si resetta esattamente al click sul bersaglio (non un colpo "gratis" appena finisce il blocco)
  - Un normale spell (non-TargetFirst) continua a comportarsi come prima — bloccato dal colpire per tutta la durata di `Casting`, nessun cambiamento
  - **Non coperto da test automatico:** il reset di `NextCombatTime` — serve un'arma equipaggiata reale, il cui `Layer` si risolve dai dati client (tiledata) non disponibili in modo affidabile nell'ambiente di test (stessa limitazione già nota per la LOS)

---

## Interrompere un cast per lanciarne un altro

- **Stato:** Implementato, buildato, testato — in attesa di revisione finale e di verifica in gioco
- **Fatto:**
  - `Spell.cs`: `_interruptedSpell` + `UsesDeferredCast` (generalizzazione del meccanismo target-first)
  - `Spell.cs`: `Cast()` non blocca più con "You are already casting a spell." — cattura lo spell interrotto e lascia proseguire i controlli normali (mana, reagenti, paralisi, ecc.) per il nuovo spell
  - `SpellTarget.cs`: `OnTarget` rimanda la risoluzione per qualsiasi spell che interrompe un cast in corso, non solo quelli con `TargetFirst`
  - `Spell.cs`: `BeginTargetFirstDelay` flizza lo spell interrotto (con addebito mana/reagenti) nel momento in cui il click sul bersaglio del nuovo spell si conferma, non quando il nuovo spell viene semplicemente premuto
  - `Spell.cs`: catena di interruzioni (A interrotto da B, B interrotto da C prima di cliccare) — l'obbligo di A viene risolto subito quando C si conferma, non perso
  - Fix: `Disturb()` non azzera più `Caster.Spell` incondizionatamente (poteva cancellare il riferimento al nuovo spell mentre disturbava quello vecchio)
  - Build e `dotnet test` puliti (nessun fallimento nuovo)
  - Loggato in `custom-docs/CUSTOM_CHANGES.md`
  - Fix (revisione finale): l'obbligo di flizzare e far pagare lo spell interrotto ora viene rispettato su OGNI percorso di interruzione (annullamento del mirino, timeout, click su bersaglio non valido, essere colpiti mentre il mirino è aperto, spell a risoluzione istantanea, interruzioni concatenate) - prima veniva rispettato solo se il click sul bersaglio dello spell interruttore arrivava normalmente. Nelle interruzioni concatenate, ora paga anche lo spell "di mezzo", non solo il primo interrotto.
- **Manca (da verificare in gioco, uno per uno):**
  - Spell A in cast (animazione/delay in corso, mirino non ancora apparso) → premi Spell B → nessun blocco, nessun messaggio "already casting", il mirino di B appare subito
  - Clicchi un bersaglio valido col mirino di B → A flizza e paga mana/reagenti, B parte normalmente da lì (mantra, animazione, il suo delay, poi risolve)
  - Annulli il mirino di B prima di cliccare (o scade il timeout) → A viene comunque flizzato e addebitato (fix della revisione finale: prima evaporava, ora no); se invece B fallisce i suoi stessi controlli su mana/reagenti PRIMA di mostrare il mirino, A resta come se nulla fosse, nessun addebito (B non ha mai davvero interrotto nulla)
  - Interrompi B con uno spell C prima di cliccare il bersaglio di B → A viene comunque flizzato e addebitato subito al momento in cui C si conferma
  - Uno spell `TargetFirst` (Flame Strike) con il proprio mirino ancora aperto, interrotto da qualsiasi cosa (compreso un nuovo cast) → resta gratis, comportamento invariato rispetto alla feature target-first originale
  - Uno spell `TargetFirst` già confermato (mirino cliccato, delay in corso) interrotto → paga, comportamento invariato
  - Tentativo di cast con una bacchetta (wand) mentre già in cast → messaggio "You can not cast a spell while frozen." invariato, nessun cambiamento di comportamento
  - **Comportamento noto e accettato (non un problema):** se usi per interrompere uno spell che si risolve senza mai mostrare un mirino (es. Reactive Armor sotto AOS), lo spell interrotto (A) viene comunque flizzato e paga correttamente, ma lo spell interruttore stesso (B) fallisce silenziosamente su se stesso (l'effetto di B - es. l'attivazione dell'armatura reattiva - non si applica, pur avendo comunque il suo delay/animazione se presenti). Non è più possibile annullare il costo di uno spell in questo modo; resta solo il fatto che alcuni spell "istantanei" non funzionano bene come interruttori.
  - Decisione finale: tenere la modifica, aggiustare qualcosa, o revert

---

## Scudo magico (magic shield) — meccanica base

- **Stato:** Implementato, buildato, testato (3 task via subagent-driven development, ognuno con revisione singola approvata, più revisione finale whole-branch con un solo finding bloccante — questo stesso aggiornamento alla documentazione) — non ancora agganciato a nulla di giocabile, mai verificato in game.
- **Fatto:** `Mobile.MagicShieldAbsorb` (pool assorbimento danno, nuova property int get/set), `Server.Custom.MagicShield` (API statica Apply/Clear/Absorb, sostituzione invece di stack, bleed-through sull'eccedenza, scadenza opzionale a timer), tre hook in `SpellHelper.cs` che assorbono solo danno da incantesimo (mai mischia/ranged diretto), pacchetto di rete `0xBF`/`0x4D53` per notificare il client. Riferimenti: spec `custom-docs/specs/2026-09-16-magic-shield-design.md`, piano `custom-docs/plans/2026-09-16-magic-shield-modernuo-plan.md`.
- **Manca (da fare prima o durante l'aggancio a uno spell/abilità reale):**
  - Nessuno spell/abilità/oggetto concede ancora lo scudo — è solo il motore, per design (vedi spec).
  - `MagicShield.Apply(points <= 0)` non fa clamp a 0 — un valore negativo resterebbe scritto sul Mobile indefinitamente (nessun chiamante lo fa oggi, ma va sistemato prima che qualcuno lo faccia).
  - Nessun test copre il sito di aggancio 2 (`SpellDamageTimer.OnTick`, il percorso del danno da incantesimo ritardato — dove vivono spell reali come Magic Arrow/Fireball) con un delay non-zero; verificato solo per ispezione.
  - `Server.Custom.MagicShield`'s `_expireTokens` dictionary non ha un hook di pulizia alla cancellazione del giocatore (il timer si autopulisce comunque quando scatta, quindi la perdita è limitata, non un leak illimitato).
  - `DuelContext.cs` azzera `MagicDamageAbsorb`/`MeleeDamageAbsorb` all'inizio di un duello ma non `MagicShieldAbsorb` — da allineare quando lo scudo sarà davvero raggiungibile in game.
  - Comportamento da confermare come voluto prima del rilascio: con assorbimento totale (danno che arriva a 0 dopo lo scudo), il bersaglio non viene interrotto nel cast, non viene smascherato dal nascondimento, non viene liberato dalla paralisi, e non genera damage entry per quel colpo (stesso comportamento già esistente di Attune Weapon sul melee, quindi coerente, ma è una proprietà PvP forte da confermare esplicitamente).
  - Bug preesistente e non correlato trovato durante l'implementazione (non toccato, per la regola workflow #1 del CLAUDE.md): `Feint.GetDamageReduction` (`Projects/UOContent/Items/Weapons/Abilities/Feint.cs:52`) va in `ArgumentNullException` se chiamato con `from == null` — dormiente oggi, nessun chiamante di produzione lo raggiunge così.

---

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

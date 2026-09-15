# Lavori in corso

Traccia i lavori **non ancora ufficialmente terminati** — un lavoro esce da questa lista solo quando è stato verificato e tu lo confermi concluso, non quando il codice è scritto/compilato.

Formato per ogni voce: cosa è stato fatto, cosa manca per chiuderlo, stato attuale.

---

## Target-first casting su Flame Strike

- **Stato:** Implementato, buildato, testato, revisionato (con un giro di correzioni) — in attesa di verifica in gioco
- **Fatto:**
  - `Spell.cs`: flag `TargetFirst`, `ValidateTargetFirst`, `HasReagents`, `ConsumeCastingResources`, `BeginTargetFirstDelay`, `Disturb()` addebita il costo se interrotto dopo il click
  - `SpellTarget.cs`: `OnTarget` rimanda la risoluzione per gli spell `TargetFirst`, doppio controllo range/LOS/validità
  - `FlameStrike.cs`: `TargetFirst => true`
  - Build e `dotnet test` puliti (nessun fallimento nuovo, solo quelli preesistenti non collegati)
  - Un giro di correzioni dopo la revisione end-to-end: cursore "fantasma" che sopravviveva a un'interruzione prima di scegliere il bersaglio (ora annullato correttamente), e il timer di recupero (`NextSpellTime`) che partiva al click invece che alla fine del delay (ora coerente con tutti gli altri spell)
  - Loggato in `custom-docs/CUSTOM_CHANGES.md`
- **Manca (da verificare in gioco, uno per uno):**
  - Percorso felice: lancia → mirino appare subito → clicca un bersaglio valido → aspetta il delay → il danno arriva
  - Click su bersaglio troppo lontano → messaggio "That is too far away.", **nessun costo**, mirino sparisce
  - Click su bersaglio senza linea di vista → messaggio "Target can not be seen.", **nessun costo**
  - Click su bersaglio morto/non attaccabile → messaggio di `CanBeHarmful`, **nessun costo**
  - Bersaglio esce di range durante il nuovo delay (es. si allontana dopo il click) → messaggio "That is too far away." alla fine del timer, **mana e reagenti consumati**
  - Bersaglio esce dalla linea di vista durante il delay → messaggio "Target can not be seen.", **mana e reagenti consumati**
  - Bersaglio muore durante il delay → spell fallito, **mana e reagenti consumati**
  - Vieni colpito durante il nuovo delay (dopo il click) → spell interrotto, **mana e reagenti consumati**, nessun danno
  - Vieni colpito PRIMA di cliccare un bersaglio (mirino ancora a schermo) → **nessun costo**, il mirino deve sparire (era il bug corretto nel giro di revisione)
  - Annulli il mirino PRIMA di cliccare un bersaglio → **nessun costo** (fase 1 resta gratis)
  - Mana o reagenti insufficienti: il mirino non deve nemmeno apparire
  - Decisione finale: tenere la modifica, aggiustare qualcosa, o revert
- **Note tecniche minori emerse in revisione, non bloccanti, da valutare in futuro:**
  - Un commento XML su `BeginTargetFirstDelay` è rimasto leggermente disallineato dopo la correzione (dice ancora che avvia lui il recovery clock, ora lo fa `CastTimer`)
  - Nel ramo di risoluzione di `CastTimer` manca l'aggiornamento del flag di paralisi (`Caster.Delta(MobileDelta.Flags)`) che il ramo originale invece fa — preesistente, non introdotto da questa feature
  - Nessun test automatico copre lo scenario "click fantasma dopo interruzione" o il timing di `NextSpellTime` — solo verifica manuale per ora

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
- **Manca (da verificare in gioco, uno per uno):**
  - Spell A in cast (animazione/delay in corso, mirino non ancora apparso) → premi Spell B → nessun blocco, nessun messaggio "already casting", il mirino di B appare subito
  - Clicchi un bersaglio valido col mirino di B → A flizza e paga mana/reagenti, B parte normalmente da lì (mantra, animazione, il suo delay, poi risolve)
  - Annulli il mirino di B prima di cliccare (o B fallisce i suoi stessi controlli su mana/reagenti) → A resta come se nulla fosse, nessun addebito
  - Interrompi B con uno spell C prima di cliccare il bersaglio di B → A viene comunque flizzato e addebitato subito al momento in cui C si conferma
  - Uno spell `TargetFirst` (Flame Strike) con il proprio mirino ancora aperto, interrotto da qualsiasi cosa (compreso un nuovo cast) → resta gratis, comportamento invariato rispetto alla feature target-first originale
  - Uno spell `TargetFirst` già confermato (mirino cliccato, delay in corso) interrotto → paga, comportamento invariato
  - Tentativo di cast con una bacchetta (wand) mentre già in cast → messaggio "You can not cast a spell while frozen." invariato, nessun cambiamento di comportamento
  - **Problema noto, non chiuso (trovato in revisione, richiede una decisione):** se annulli il mirino dello spell B (Esc, o interrompi B a sua volta senza cliccare un bersaglio valido, o il click su B fallisce per range/LOS/validità) PRIMA che B si confermi, l'obbligo di far pagare A evapora — A resta bloccato in "casting" per sempre (il suo CastTimer non fa più nulla) e non paga mai, e il giocatore può ripetere per annullare completamente il costo di uno spell
  - **Problema noto, non chiuso (trovato in revisione, richiede una decisione):** uno spell che si risolve senza mai mostrare un mirino (es. Reactive Armor e altri spell AOS a risoluzione istantanea) se usato per interrompere un altro spell fallisce silenziosamente su se stesso E fa evaporare l'obbligo di pagamento dello spell interrotto, perché il nuovo flusso differito parte comunque anche se lo spell non ha davvero un bersaglio da cliccare
  - Decisione finale: tenere la modifica, aggiustare qualcosa, o revert

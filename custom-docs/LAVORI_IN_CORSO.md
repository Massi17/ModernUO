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

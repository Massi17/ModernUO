# ModernUO Fork — Design di sincronizzazione e sviluppo custom

**Data:** 2026-09-13
**Stato:** Implementato (setup iniziale completato)

## Obiettivo

Mantenere un fork personale di [ModernUO](https://github.com/modernuo/ModernUO) (.NET 10 Ultima Online server emulator) aggiornato con l'upstream nel tempo, capendo ad ogni sync cosa è cambiato upstream e quali modifiche interessano il progetto personale, mentre in parallelo si sviluppa contenuto/modifiche custom (sia nuovo contenuto di gioco sia modifiche a comportamenti esistenti/engine).

## Contesto del progetto upstream

- Solution monolitica: `Projects/Server` (motore engine), `Projects/UOContent` (contenuto di gioco — target primario per modifiche), `Projects/Application`, `Projects/Logger`, più `Server.Tests`/`UOContent.Tests`.
- Nessun meccanismo di plugin/assembly esterno: chi personalizza modifica file direttamente dentro l'albero del progetto.
- Convenzione ufficiale del progetto (da `CLAUDE.md` upstream): non toccare `Projects/Server` senza motivo esplicito; `Projects/UOContent` è il target primario di modifica.

## Decisioni prese

1. **Hosting: fork pubblico su GitHub** — [`Massi17/ModernUO`](https://github.com/Massi17/ModernUO), creato tramite il pulsante "Fork" (storia condivisa con l'upstream verificata: `origin/main` risultava antenato/allineato a `main` al momento del setup).
2. **Percorso locale del progetto:** `C:\Users\massi\Documents\GitHub\ModernUO`.
3. **Tipo di modifiche previste:** sia contenuto nuovo, sia modifiche a file esistenti / al motore (`Projects/Server`) — non solo aggiunte isolate.
4. **Strategia di sync scelta: merge periodico** (non rebase). Motivazione: per un fork personale modificato pesantemente e mantenuto a lungo termine, il rebase ripetuto di centinaia di commit custom ad ogni release upstream diventa insostenibile; il merge con `git rerere` attivo è più sostenibile (i conflitti già risolti vengono riapplicati automaticamente).
5. **Convenzione per il contenuto nuovo:** file nuovi sotto `Projects/UOContent/Custom/` (cartella che non esiste upstream) — azzera i conflitti per le aggiunte pure.
6. **Convenzione per le modifiche a file esistenti:** modifica diretta del file, ma ogni modifica viene annotata in `docs/CUSTOM_CHANGES.md` (file, motivo, data) per tenere traccia di cosa e perché è stato toccato, e per permettere di incrociare gli aggiornamenti upstream con le aree a rischio conflitto.

## Struttura repository

- `origin` → `https://github.com/Massi17/ModernUO.git` (il fork dell'utente).
- `upstream` → `https://github.com/modernuo/ModernUO.git`.
- Branch di lavoro: `main`, con tracking impostato su `origin/main`.
- `rerere.enabled` attivo a livello di repository.

## Flusso di sincronizzazione upstream

Eseguito su richiesta dell'utente (nessuna cadenza fissa imposta):

1. `git fetch upstream`.
2. Riepilogo in italiano di cosa è cambiato: changelog/release notes ufficiali ModernUO + log dei nuovi commit + `git diff --stat` per i file toccati.
3. Incrocio della lista file toccati con `docs/CUSTOM_CHANGES.md`: segnalazione esplicita di quali modifiche upstream toccano file già modificati dall'utente (rischio conflitto) rispetto a quelle "sicure".
4. Decisione condivisa: merge completo o rimando di parti specifiche.
5. `git merge upstream/main`; risoluzione conflitti insieme, con `git rerere` abilitato per riutilizzare risoluzioni passate.
6. Build (`dotnet build`) + test (`dotnet test`, suite `Server.Tests`/`UOContent.Tests`) prima di considerare il merge concluso.
7. Commit e push su `origin`, sempre con conferma esplicita dell'utente prima del push.

## Testing

- Dopo ogni merge upstream: build completa + suite di test esistenti prima del push, per intercettare regressioni subito.
- Per nuove feature custom: test in `UOContent.Tests` scritti seguendo TDD quando si inizia a sviluppare codice specifico (skill `test-driven-development` da invocare al momento).

## Tooling

- Nessuna automazione complessa nella fase iniziale: il flusso di sync sopra viene eseguito manualmente (con assistenza) ad ogni richiesta dell'utente.
- Possibile estensione futura: controllo periodico automatico di nuovi tag/release su GitHub upstream con notifica proattiva, da implementare in seguito con la skill di scheduling se richiesto.

## Setup iniziale completato (2026-09-13)

- Remote `origin`/`upstream` configurati correttamente sul clone esistente in `C:\Users\massi\Documents\GitHub\ModernUO` (inizialmente `origin` puntava per errore all'upstream; corretto).
- `main` impostato per tracciare `origin/main`.
- `rerere.enabled = true` nel repo.
- Creata `Projects/UOContent/Custom/` con README che spiega la convenzione.
- Creato `custom-docs/CUSTOM_CHANGES.md` con template tabellare, vuoto.
- Questa spec spostata da scratchpad a `custom-docs/specs/` (non `docs/`, perché `/docs/` è nel `.gitignore` ufficiale di ModernUO — riservato al sito mkdocs).

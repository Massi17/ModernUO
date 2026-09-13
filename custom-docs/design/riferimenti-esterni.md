# Riferimenti esterni — community UO shard scripting

## ServUO

Sito comunitario: [servuo.dev](https://www.servuo.dev/) (stesso contenuto anche su servuo.com). Struttura: forum per categoria (Community, Emulation, VitaNex, XmlSpawner), sezione "Resources"/archivio con tutorial, directory di shard attivi, download dell'emulatore da GitHub.

**Repository:** [github.com/ServUO/ServUO](https://github.com/ServUO/ServUO) — struttura a cartelle:
- `/Scripts` — contenuto custom (item, mobili, spell, sistemi) — l'equivalente diretto di `Projects/UOContent` in ModernUO, ma qui è un'unica cartella flat per tutto (non un progetto .csproj separato)
- `/Server` — motore core (equivalente di `Projects/Server`)
- `/Config` — configurazione (`Server.cfg` da leggere per primo)
- `/Data`, `/Spawns`, `/SpawnsOld`, `/RevampedSpawns`, `/Ultima` — dati e sistemi di spawn

**Pattern base per un item custom** (da tutorial + forum):
- Si eredita da `Item` o da una sottoclasse specializzata (`BaseWeapon`, `BaseArmor`, `BasePotion`, ecc.) — stesso principio di ModernUO
- Comportamento via override di metodi virtuali (`OnDoubleClick`, `OnHit`, ecc.) — stesso principio di ModernUO
- **`[Constructable]`** (attenzione: **una sola "a"**, diverso da `[Constructible]` — verificare l'esatto attributo usato in ModernUO prima di copiare) sul costruttore: senza, l'item non è invocabile col comando `[add ItemName` in game — un dettaglio da tutorial confermato in un thread di supporto
- Serializzazione **manuale**: metodi `Serialize`/`Deserialize` scritti a mano con versioning esplicito (nessun source-generator) — vedi sezione differenze sotto

**Tutorial trovato:** ["Beginner's Guide to Creating a Basic Item Script"](https://www.servuo.dev/threads/beginners-guide-to-creating-a-basic-item-script.16832/) (thread) / [versione archivio](https://www.servuo.dev/archive/beginners-guide-to-creating-a-basic-item-script.2422/) — copre dove mettere lo script dentro `/Scripts`, struttura base della classe, esempi di codice. **Nota:** WebFetch ha recuperato solo un estratto (la pagina completa non si è caricata per intero) — se serve il tutorial integrale, andrebbe riaperto direttamente nel browser.

**Categoria tutorial generale:** [servuo.dev/archive/categories/tutorials.21](https://www.servuo.dev/archive/categories/tutorials.21/) — raccolta più ampia (animazioni, gestione Git, world building con Region Editor, sistema di spawn UO-Respawn).

**Vita-Nex: Core** — libreria di funzionalità avanzate per server ServUO ("push the limits of Ultima Online"), sezione dedicata su [servuo.dev/archive/vita-nex.281](https://www.servuo.dev/archive/vita-nex.281/). Potenzialmente utile come fonte di idee/pattern per sistemi complessi (non ancora esplorata in profondità in questa sessione).

## Altre risorse utili trovate

- **[RunUO Wiki](https://www.runuo.net/wiki/index.php/RunUO_Constructor_Guide)** (es. "RunUO Constructor Guide", "RunUO FAQ") — storica documentazione della famiglia RunUO/ServUO/ModernUO. **Non raggiungibile in questa sessione (HTTP 403)** — riprovare in futuro, magari da browser diretto invece che via fetch automatico.
- **[zerodowned/Custom-Scripts-for-ServUO](https://github.com/zerodowned/Custom-Scripts-for-ServUO)** — repository GitHub di script di esempio "per RunUO/ServUO/PlayUO" — utile come libreria di idee/pattern di contenuto custom già scritti (da tradurre per ModernUO, vedi sotto).
- **Grokipedia — "ServUO Scripting"** ([grokipedia.com/page/ServUO_Scripting](https://grokipedia.com/page/ServUO_Scripting)) — pagina trovata via ricerca, sembra una sintesi strutturata sull'argomento, ma **non raggiungibile in questa sessione (HTTP 403)**. Da riprovare.
- Forum ServUO in generale (thread come ["ServUO Scripting Noob Questions"](https://www.servuo.dev/threads/servuo-scripting-noob-questions.11466/)) — buona fonte per problemi pratici comuni quando si scrivono script.

## Differenze da tenere a mente rispetto a ModernUO

Prima di copiare un pattern trovato online (ServUO/RunUO) dentro questo fork di ModernUO, va "tradotto" — differenze principali osservate:

1. **Serializzazione:** ServUO/RunUO scrivono `Serialize`/`Deserialize` a mano con versioning manuale. ModernUO usa un **source generator** (`[SerializationGenerator(version)]` + `[SerializableField]`) che genera questo codice — vedi `custom-docs/MANUALE.md` punto 3 e `dev-docs/serialization.md` nel repo. Non copiare mai Serialize/Deserialize manuali da un tutorial ServUO direttamente in ModernUO.
2. **Struttura progetto:** ServUO ha un'unica cartella `/Scripts` flat; ModernUO separa `Projects/Server` (motore) e `Projects/UOContent` (contenuto) come progetti .csproj distinti, con la nostra convenzione aggiuntiva di isolare il contenuto nuovo in `Projects/UOContent/Custom/` (vedi `custom-docs/MANUALE.md` punto 1).
3. **Threading/performance:** ModernUO è progettato attorno a un event loop single-thread con regole rigide anti-concorrenza (niente `lock`, niente thread manuali nel game loop — vedi `CLAUDE.md` nel repo); pattern ServUO più datati potrebbero non seguire queste regole e vanno adattati.
4. **Attributo costruttore:** **confermato** — ServUO/RunUO usano `[Constructable]` (una "a"), ModernUO usa `[Constructible]` (`Projects/Server/Attributes.cs:105`, "i" come nell'inglese corretto). Un copia-incolla diretto da un tutorial ServUO non compila finché non si corregge questo nome.
5. Il repo ModernUO ha già una guida di migrazione dettagliata pronta all'uso per questi casi: `dev-docs/runuo-migration-docs/` (skill Claude dedicate: `migrate-from-runuo/*`, vedi `CLAUDE.md`) — consultarla per la traduzione precisa di un pattern specifico, questo file serve solo da mappa/indice dei riferimenti esterni.

## Note

Ricerca svolta il 2026-09-13, mentre si attendeva la verifica in gioco di una modifica di prova (Flame Strike castabile in movimento). Alcune fonti (RunUO Wiki, Grokipedia) hanno risposto HTTP 403 al fetch automatico e andrebbero riconsultate da browser se servono davvero.

## Verifica esterna aggiuntiva (2026-09-13)

**ServUO ha aggiornato il proprio stack .NET — aggiornamento rilevante rispetto a quanto scritto sopra.** Il repo ufficiale ([github.com/ServUO/ServUO](https://github.com/ServUO/ServUO)) risulta ora su **.NET 10** (istruzioni d'installazione con `dotnet-sdk-10.0`/`dotnet-runtime-10.0`), con ultimo aggiornamento registrato il 1° giugno 2026 — non è più fermo al vecchio .NET Framework storicamente associato a RunUO/ServUO. **Questo restringe il divario tecnico con ModernUO** (entrambi ora su .NET 10), ma le differenze architetturali documentate sopra restano presumibilmente valide e non riverificate in questa sessione: ServUO mantiene comunque `Serialize`/`Deserialize` manuali (nessuna evidenza di adozione di source-generator) e la struttura a cartella `/Scripts` flat (nessun cambiamento trovato). L'aggiornamento .NET da solo non implica che le regole di threading/concorrenza di ModernUO si applichino anche a ServUO — verificarlo con codice reale se mai servisse un confronto più preciso.

**Vita-Nex: Core — confermato ancora attivo/mantenuto nel 2026**, non solo un riferimento storico: thread recenti sul forum ServUO (es. discussioni su "Build 57.4", luglio 2026) mostrano attività continua della community attorno alla libreria.

**Non raggiungibili anche in questo secondo tentativo:** RunUO Wiki (`runuo.net/wiki`) e Grokipedia (`grokipedia.com/page/ServUO_Scripting`) — entrambi ancora HTTP 403. Se servono davvero, vanno aperti da un browser reale, non via fetch automatico.

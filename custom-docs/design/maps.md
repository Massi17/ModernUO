# Mappe/facet custom — design

## Info aggiuntive dell'utente, verificate nel codice (2026-09-13)

L'utente ha fornito una ricerca dettagliata esterna sul livello "file del client UO" (indipendente dall'emulatore — stessi file/tool per RunUO, ServUO, ModernUO, Sphere). Punti tecnici **verificati direttamente nel codice** di questo repo:

- **Struttura a due strati confermata:** `map{N}.mul` = terreno (tile id + quota Z per casella); `staidx{N}.mul` + `statics{N}.mul` = statics congelati (alberi, muri, edifici). Attorno: `tiledata.mul` (proprietà tile), `art.mul`/`artidx.mul` (grafica item), `texmaps.mul` (texture terreno), `hues.mul`, `radarcol.mul`, `multi.mul`/`multi.idx` (case/navi), `light.mul`.
- **Config server confermata:** `Distribution/Configuration/modernuo.json`, chiave **`dataDirectories`** (`Projects/Server/Configuration/ServerSettings.cs:28-29`) — esattamente come descritto.
- **Fallimento se i file mancano: confermato ed è un hard-fail.** `Core.FindDataFile()` (`Projects/Server/Main.cs:346`) lancia `FileNotFoundException` se un file richiesto non è in nessuna `dataDirectories` (comportamento di default, `throwNotFound: true`). Bonus trovato nel codice: su Linux c'è un fallback automatico case-insensitive per il nome file (righe 354-364) — **assente su Windows**, dove però il filesystem NTFS è già case-insensitive di suo, quindi il classico errore "maiuscole/minuscole nel nome file" citato nei thread ServUO è tipicamente un problema Linux/macOS, non Windows.
- **Precisazione importante sul supporto `.uop` — più stretta di quanto sembrasse dal riassunto:** verificato in `Projects/Server/TileMatrix/TileMatrix.cs:105-152`, **solo il file mappa/terreno** ha un fallback `.uop` (`map{N}.mul` → se assente, `map{N}LegacyMUL.uop`, riga 113). **`staidx{N}.mul` e `statics{N}.mul` lato server ModernUO devono essere in formato `.mul` classico — nessun fallback `.uop` esiste per questi due**, né per `tiledata.mul`. Questo conferma ancora di più il ciclo "spacchetta (da UOP) → modifica → reimpacchetta (in MUL)" per gli statics, ma per il terreno il server può leggere direttamente l'UOP senza riconversione.
- **Sistema di registrazione mappe di ModernUO, confermato diverso da ServUO:** `Distribution/Data/map-definitions.json` + `Projects/Server/Maps/MapLoader.cs` (data-driven), non il `MapDefinitions.cs` hardcoded di ServUO — già verificato in una ricerca precedente in questo stesso file (vedi sotto). Quindi la "riga 7" del workflow proposto (registrare la mappa lato server) per ModernUO è: una voce in `map-definitions.json`, non toccare codice C#.
- **`maps_layouts` lato ClassicUO, confermato esatto:** chiave JSON `"maps_layouts"` in `settings.json` (`src/ClassicUO.Client/Configuration/Settings.cs:87`), proprietà C# `MapsLayouts`, usata da `MapLoader.cs` per calcolare `MAPS_COUNT` dal numero di voci — esattamente come descritto dall'utente.

**Non verificato da noi (solo ricerca web/community, preso per buono):** l'elenco di tool (UOFiddler, CentrED+/CentrED#, UO Landscaper, Dragon, TerrainLab, Pandora's Box, Region Editor, UO Architect), il range di versioni client consigliato (7.0.20–7.0.50), i dettagli storici di Ultima Live, e gli aneddoti sui thread di troubleshooting (dimensioni Felucca 7168×4096/896×512 in blocchi, il caso Z=-84, ecc.) — informazioni di dominio della community UO, non verificabili nel nostro codice.

## Cosa abbiamo trovato

**Community (ServUO/RunUO, ricerca web):** il forum ServUO conferma che "il numero di mappe fisiche non può essere cambiato" nel loro sistema base — le opzioni standard sono (1) clonare un landmass esistente come nuova regione/facet via `MapDefinitions.cs`, (2) sostituire una mappa esistente con una custom (richiede rifare tutti gli spawn, perché elevazioni/punti di riferimento cambiano), o (3) "Ultima Live" (streaming di mappe extra al client, fino a 200 mondi, ma legato a RunUO 2.3-2.7 e client vecchi — non direttamente applicabile qui). Fonti: [servuo.dev/threads/adding-a-facet](https://www.servuo.dev/threads/adding-a-facet.10716/), [servuo.dev/threads/add-a-custom-map](https://www.servuo.dev/threads/add-a-custom-map.14702/).

**Strumento della community per costruire mappe: CentrED#** ([github.com/kaczy93/centredsharp](https://github.com/kaczy93/centredsharp), [sito](https://kaczy93.github.io/centredsharp/)) — riscrittura moderna del classico CentrED, editor di mappe UO client/server (permette a più persone di editare terreno/statics insieme), scritto in C#/.NET 10, esplicitamente costruito in relazione a ServUO, ModernUO, ClassicUO e UOFiddler. È lo strumento da usare per disegnare terreno e piazzare statics su una mappa, sia per modificarne una esistente sia per costruirne una da zero.

**Verificato nel codice — molto più favorevole di quanto suggerisca ServUO:**
- **Server (ModernUO):** le mappe sono **completamente data-driven**, non hardcoded come in ServUO. `Projects/Server/Maps/MapLoader.cs` legge `Distribution/Data/map-definitions.json` ad ogni avvio e registra un `Map` per ogni voce (`index`, `id`, `fileIndex`, `width`/`height`, `season`, `name`, `rules`). Il commento nel file è esplicito: i primi 32 index sono riservati al core, 127 e 255 pure, ma gli altri sono liberi. File attuale: le 6 mappe stock (Felucca...TerMur, index/id/fileIndex 0-5) + Internal (127) — nessun'altra voce.
- **Vincolo reale:** il commento nel codice dice che `id` e `fileIndex` **devono restare 0-5 per qualunque mappa "visibile"** — cioè il file-set grafico (tile art/statics) riusato deve essere uno dei 6 esistenti, a meno di intervenire anche lato client (vedi sotto). L'`index` invece è libero (fuori dai range riservati): si può quindi registrare un facet con nome/regole/dimensioni proprie **con una semplice riga in un file JSON, zero codice C#** — esattamente la tecnica "clona un landmass esistente" di cui parla il forum ServUO, ma qui è nativa e senza toccare `.cs`.
- **Client (ClassicUO):** `src/ClassicUO.Assets/MapLoader.cs:23` — `MAPS_COUNT = 6` di default, ma è **sovrascrivibile**: se il client legge un'impostazione `MapsLayouts` (stringa `width,height` per mappa, separate da `;`) nel proprio config, ricalcola `MAPS_COUNT` dal numero di voci (riga 106-113). Questo conferma che il client supporta già più di 6 file-set di mappa, a patto che i file corrispondenti (map/statics/staidx per quell'indice) esistano nella cartella dati del client.

## Fattibilità

| Modifica | Fattibilità | Note |
|---|---|---|
| Modificare terreno/statics di una mappa **esistente** (aggiungere una città, un dungeon, ridisegnare un'area) | **Facile** — CentrED# è fatto apposta per questo, nessun codice da toccare, si edita direttamente il file mappa esistente | Va comunque aggiornato `custom-docs/CUSTOM_CHANGES.md` se si sovrascrivono file di dati distribuiti col repo |
| Registrare un **facet/nome nuovo** che riusa la grafica di una mappa esistente (regole diverse, dimensioni diverse, stesso terreno) | **Facile** — una voce in più in `map-definitions.json`, zero C# | `id`/`fileIndex` devono restare uno dei 6 esistenti (0-5) |
| Costruire una mappa **fisicamente nuova** (terreno mai visto, non un riuso) | **Media** — servono: (1) disegnarla con CentrED# da zero, (2) generare i file mappa/statics per un nuovo `fileIndex`, (3) registrarla in `map-definitions.json` lato server, (4) impostare `MapsLayouts` lato client (ClassicUO) per far riconoscere il nuovo file-set | Non richiede toccare `.cs` né lato server né (probabilmente) lato client — solo dati e configurazione, ma è comunque il lavoro più corposo delle tre righe |
| "Ultima Live" (streaming di mappe extra) | **Non applicabile direttamente** — legato a RunUO 2.3-2.7, non a ModernUO | Da considerare solo come riferimento storico/concettuale, non come soluzione pronta |

## Strumenti della community (riferiti dall'utente, non verificati da noi)

| Tool | Uso |
|---|---|
| **UOFiddler** | Coltellino svizzero per gli asset: item/art/texture/gump/suoni/font, copia porzioni di mappa tra file MUL |
| **CentrED+** | Editor mappa/statics client-server multi-utente, fork mantenuto di CentrED |
| **CentrED#** | Riscrittura C#/.NET moderna e cross-platform di CentrED — permette di costruire una mappa da zero (già trovato/confermato in ricerca precedente) |
| **UO Landscaper / Dragon** | Generano il terreno da un'immagine BMP dipinta a mano, poi si rifinisce in CentrED |
| **TerrainLab** | Genera un set MUL strutturalmente valido ma vuoto (nessun asset EA), da riempire con arte propria |
| **Pandora's Box** | Assistente da staff in-game |
| **Region Editor / UO Architect** | Editor di regioni / costruzione in-game |

## Workflow tipico (community, da adattare per il passo 7 su ModernUO)

1. Scegli e resta su una versione client compatibile (community consiglia 7.0.20–7.0.50 — evita problemi su tutti i file tranne `multicollection.uop`, mai decrittato)
2. Converti UOP → MUL dove serve
3. Genera/clona il terreno (BMP con Landscaper/Dragon, o copia di una mappa esistente)
4. Rifinisci in CentrED+/CentrED#
5. Rigenera i file derivati (radarcol, facet)
6. Riconverti MUL → UOP dove serve (solo il file mappa/terreno lo supporta lato ModernUO, vedi sopra)
7. **Registra la mappa lato server — su ModernUO: una voce in `Distribution/Data/map-definitions.json`, non il `MapDefinitions.cs` dei tutorial ServUO** (confermato nel codice, non serve verificarlo su Discord)
8. Distribuisci i file ai giocatori insieme al client custom

## Trappole comuni (community)

- **Dimensioni incoerenti tra i file** — es. Felucca è 7168×4096 (896×512 in blocchi); un mismatch tipico citato è 896×512 vs 768×512
- **Serve sia UOP che MUL nella stessa cartella** in certi casi (UOP per il client, MUL per il server) — coerente con quanto verificato sopra per ModernUO lato server
- **Statics che referenziano art assente dal client** → disconnessioni al login, difficili da diagnosticare
- **Limite fisico di mappe con client ufficiale** (5-6) — ClassicUO lo rimuove via `maps_layouts` (confermato sopra), rendendo obsoleta la vecchia soluzione "Ultima Live"
- Errori banali ricorrenti: maiuscole/minuscole nei nomi file (soprattutto Linux/macOS, vedi sopra), dimensione mappa sbagliata, versione client incompatibile, login sul facet sbagliato

## Nota legale (riferita dall'utente)

Distribuire ai giocatori un client con file modificati è prassi comune negli shard, ma sono asset di proprietà EA — zona grigia. La pratica più prudente: far scaricare il client ufficiale e distribuire solo i file patchati separatamente.

## Fonti

- [servuo.dev/threads/adding-a-facet.10716](https://www.servuo.dev/threads/adding-a-facet.10716/) — raggiungibile (dopo redirect da servuo.com), risposta comunitaria con le 3 opzioni base
- [servuo.dev/threads/add-a-custom-map.14702](https://www.servuo.dev/threads/add-a-custom-map.14702/) — trovato via ricerca, non aperto direttamente in questa sessione (contenuto equivalente già coperto dal thread sopra)
- [github.com/kaczy93/centredsharp](https://github.com/kaczy93/centredsharp) e [kaczy93.github.io/centredsharp](https://kaczy93.github.io/centredsharp/) — raggiungibili, pagina del sito povera di dettagli tecnici (rimanda alla wiki GitHub per i formati file, non consultata in questa sessione)
- Verifica diretta nel codice: `Projects/Server/Maps/MapLoader.cs`, `Distribution/Data/map-definitions.json`, `Projects/Server/Configuration/ServerSettings.cs`, `Projects/Server/Main.cs` (`FindDataFile`), `Projects/Server/TileMatrix/TileMatrix.cs` (repo ModernUO), `src/ClassicUO.Assets/MapLoader.cs`, `src/ClassicUO.Client/Configuration/Settings.cs` (repo ClassicUO) — non solo ricerca web, confermato leggendo l'implementazione reale
- Ricerca esterna fornita direttamente dall'utente (2026-09-13): riepilogo dettagliato su struttura file client UO, tool della community, workflow e trappole comuni — verificata dov'era verificabile nel nostro codice (vedi sezione dedicata sopra)

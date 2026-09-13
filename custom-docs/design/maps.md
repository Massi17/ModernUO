# Mappe/facet custom — design

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

## Fonti

- [servuo.dev/threads/adding-a-facet.10716](https://www.servuo.dev/threads/adding-a-facet.10716/) — raggiungibile (dopo redirect da servuo.com), risposta comunitaria con le 3 opzioni base
- [servuo.dev/threads/add-a-custom-map.14702](https://www.servuo.dev/threads/add-a-custom-map.14702/) — trovato via ricerca, non aperto direttamente in questa sessione (contenuto equivalente già coperto dal thread sopra)
- [github.com/kaczy93/centredsharp](https://github.com/kaczy93/centredsharp) e [kaczy93.github.io/centredsharp](https://kaczy93.github.io/centredsharp/) — raggiungibili, pagina del sito povera di dettagli tecnici (rimanda alla wiki GitHub per i formati file, non consultata in questa sessione)
- Verifica diretta nel codice: `Projects/Server/Maps/MapLoader.cs`, `Distribution/Data/map-definitions.json` (repo ModernUO), `src/ClassicUO.Assets/MapLoader.cs` (repo ClassicUO) — non solo ricerca web, confermato leggendo l'implementazione reale

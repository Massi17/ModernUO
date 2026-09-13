# Effetti — design

## Come funziona in ModernUO (riferimento per la fattibilità)

"Effetti" copre due sistemi completamente separati e indipendenti in ModernUO: quelli **visivi/sonori** (nessuno stato lato server) e quelli **di stato** (buff/debuff meccanici sul personaggio). Non c'è un'unica architettura unificata — vanno trattati come due problemi distinti.

### 1. Effetti visivi/sonori — `Server.Effects` (`Projects/Server/Effects.cs`)

Classe statica, stateless: ogni chiamata costruisce un pacchetto binario e lo spedisce subito ai client nel raggio visivo, senza persistere nulla lato server. Metodi principali:

- `PlaySound(IEntity, soundID)` — solo audio.
- `SendLocationEffect` / `SendTargetEffect` / `SendMovingEffect` — effetto "hued" (immagine + hue + durata) su un punto, un target o in movimento tra due entità.
- `SendLocationParticles` / `SendTargetParticles` / `SendMovingParticles` — varianti particellari (per client che le supportano, vedi `ParticleSupportType`/`SendParticlesTo`, riga 58-62), con fallback automatico all'effetto "hued" semplice se il client non le supporta.
- `SendBoltEffect` — effetto fulmine pre-costruito (usato per danno magico diretto).

Ogni chiamata richiede: un `IEntity` (origine/target), un `itemID` (grafica UO, da tabella client), `speed`/`duration`, opzionalmente `hue` e `EffectLayer` (per ancorare l'effetto a una parte del corpo, es. `EffectLayer.Head`).

**Punto di estensione per un effetto visivo custom:** quasi sempre non serve toccare `Effects.cs` — basta chiamare i metodi esistenti con gli ID giusti (spesso via `Mobile.FixedParticles(...)`/`Mobile.PlaySound(...)`, wrapper di comodo su `Mobile` che richiamano `Effects` — vedi uso in `Clumsy.cs:39-40`). Serve un nuovo metodo in `Effects.cs` solo per un *tipo di pacchetto* di rete genuinamente nuovo (raro, e sarebbe una modifica a `Projects/Server`, da valutare con attenzione).

### 2. Effetti di stato (buff/debuff) — due livelli separati che vanno usati INSIEME

**a) Livello puramente presentazionale — icona buff bar:** `Projects/UOContent/Engines/BuffIcons/` (`BuffInfo.cs`, `BuffIcon.cs` enum con ~190 valori già definiti, `BuffIconPackets.cs`). `BuffInfo` è solo dati (icona, testo, durata) + un proprio timer interno (`StartTimer`/`StopTimer`, `TimerExecutionToken`) che a scadenza chiama `mobile.RemoveBuff(id)`. `Mobile.AddBuff(BuffInfo)` / `RemoveBuff(BuffIcon)` (in `Mobile.cs:4236-4268`) mantengono un `Dictionary<BuffIcon, BuffInfo>` (`m_BuffTable`, creato lazy) e spediscono il pacchetto icona al client. **Questo livello non altera nessuna meccanica di gioco da solo** — è solo ciò che il giocatore vede nella buff bar.

**b) Livello meccanico — lo stato vero e proprio:** non esiste una classe base astratta "StatusEffect"/"Buff" che gestisca automaticamente applicazione/durata/rimozione. Ogni effetto meccanico è implementato **ad hoc**, tipicamente in uno di questi modi (visti in `Projects/Server/Mobiles/Mobile.cs`):

- **Property + timer dedicati** per effetti binari con logica propria, es. `Paralyzed` (`Mobile.cs:585-598, 3681-3691`): property bool con setter che notifica il client, un `TimerExecutionToken _paraTimerToken`, e `ExpireParalyzed()` come callback di scadenza. Stesso pattern per `Poisoned`/`m_Poison` (oggetto `Poison`, con hook virtuale `OnPoisoned` per estendere la reazione, `Mobile.cs:8641-8733`).
- **`StatMod`** (`Mobile.cs`, `AddStatMod`/`RemoveStatMod`/`GetStatMod`, riga ~8429-8500, lista `_statMods`) — modificatore temporaneo e nominato su Str/Dex/Int, con la propria scadenza interna; è il building block generico per qualunque "cala/aumenta una statistica per X tempo".
- **`ResistanceMod`** (`AddResistanceMod`, riga 3155) — stesso concetto per le resistenze elementali.

**Il pattern standard per un nuovo debuff/buff custom** (visto identico in tutti gli spell, es. `Clumsy.cs`): nello stesso punto del codice si (1) applica l'effetto meccanico vero — qui `SpellHelper.AddStatCurse(...)`, un helper che internamente usa `AddStatMod` — e (2) si chiama `(mobile as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.X, cliloc, durata, ...))` per mostrare l'icona coerente. I due livelli **non sono collegati automaticamente**: è responsabilità di chi scrive lo spell/effetto tenerli sincronizzati (stessa durata, rimozione coordinata).

### Timer ed effetti a tempo

Non c'è un "effect scheduler" dedicato: ogni effetto a tempo usa direttamente il sistema timer generico di ModernUO (`Timer.StartTimer`/`Timer.DelayCall` con `TimerExecutionToken`, vedi `dev-docs/timers.md`). `BuffInfo.StartTimer` ne è un esempio minimo; gli effetti meccanici (paralisi, veleno, stat mod) fanno lo stesso indipendentemente.

### Vincoli/insidie note

- **`BuffIcon` è un enum chiuso** con ID numerici che riflettono valori noti al client — aggiungere una voce custom è fattibile (questo progetto usa un client custom) ma va coordinata: l'ID va aggiunto/mappato anche lato client, altrimenti l'icona non si vede correttamente.
- **Nessuna cancellazione automatica coordinata**: se si aggiunge un effetto meccanico custom, va gestita esplicitamente la rimozione sia dello stato (timer/property) sia del buff icon (altrimenti l'icona resta visibile dopo che l'effetto meccanico è scaduto, o viceversa).
- Il codice audit di ModernUO (regola timer, vedi `custom-docs/MANUALE.md`) richiede di cancellare sempre i timer in `OnDelete()`/`OnAfterDelete()` per item/mobile coinvolti in un effetto a tempo.
- Un effetto visivo (`Effects.*`) è **fire-and-forget**: se il server si riavvia a metà di un effetto lungo, non c'è nulla da "riprendere" (non persiste). Un effetto meccanico invece (StatMod, Paralyzed, ecc.) va verificato per la persistenza attraverso il salvataggio del mondo se deve sopravvivere a un riavvio — di default molti non sono pensati per farlo (es. `RetainThroughDeath` su `BuffInfo` esiste proprio perché il default è "non sopravvive").

## Decisioni custom

Nessuna decisione ancora presa.

# Classi — design

## Come funziona in ModernUO (riferimento per la fattibilità)

**Non esiste un sistema di classi nativo.** Ultima Online (e quindi ModernUO) è un gioco a skill libere: qualunque personaggio può allenare qualunque skill fino al proprio cap individuale, senza restrizioni strutturali legate a una "classe". Un vero sistema di classi (nel senso stile WoW/D&D, con restrizioni permanenti su cosa un personaggio può fare) **non esiste e andrebbe progettato da zero** come architettura custom.

### Ciò che più si avvicina: le "Professions" alla creazione personaggio

`Projects/UOContent/Misc/ProfessionInfo.cs` carica un file dati (`Data/Professions/.../prof.txt`) con un elenco di "professioni" predefinite (es. Warrior, Mage, Necromancer, Paladin, Samurai, Ninja...). Ogni professione è solo un **preset**: statistiche iniziali + fino a 4 skill iniziali + un set di equipaggiamento di partenza. Non impone nessuna restrizione dopo la creazione — è puro flavor per lo start, non un vincolo persistente.

Il flusso è in `Projects/UOContent/Engines/Character Creation/CharacterCreation.cs`:
- `OnCharacterCreated` (riga 199) legge la professione scelta (`ProfessionInfo.GetProfession`), applica stat/skill iniziali (`SetStats`/`SetSkills`, righe 399 e 476) e assegna l'equipaggiamento specifico (`GiveProfessionItems`, riga 501, con uno `switch` per nome professione — vedi i case `"necromancer"`, `"paladin"`, `"samurai"`, `"ninja"`, righe 508-703).
- `ValidateSkills` (riga 429) limita le skill iniziali scegliibili a un elenco fisso (`_allowedStartingSkills`, righe 20-77) e impone che la somma sia 100 o 120 punti totali — ma questo vale solo in fase di creazione, non dopo.
- Dopo la creazione, **nulla impedisce** al personaggio di allenare qualsiasi altra skill.

### Il concetto più vicino a "restrizione strutturale": le razze

`Race` (in `Projects/Server`, usata ovunque in `CharacterCreation.cs` tramite `m.Race`, `raceFlag`, ecc.) impone vincoli reali e persistenti: alcune skill sono negate per razza (es. `SkillName.Archery` negata ai Gargoyle, `SkillName.Throwing` riservata ai Gargoyle — righe 451-452), ed equipaggiamento/aspetto differiscono per razza in tutto `GiveProfessionItems`/`AddSkillItems`. Le razze sono l'unico meccanismo nativo che **sopravvive oltre la creazione personaggio**.

### Punto di aggancio reale per un sistema di classi custom

Il vero hook riusabile è il **cap per singola skill**: ogni `Skill` (istanza per mobile, `Projects/Server/Skills.cs`, proprietà `Cap`/`CapFixedPoint`, righe 205-225) ha un tetto massimo individuale, indipendente dal cap totale del personaggio. Un sistema di classi custom realistico si costruirebbe così:

1. Assegnare una "classe" al personaggio (proprietà custom su una sottoclasse/estensione di `PlayerMobile`, in `Projects/UOContent/Custom/`).
2. Alla creazione (agganciandosi a `CharacterCreatedEvent`, già usato da `CharacterCreation.cs`) o in un momento successivo scelto dal giocatore, impostare `m.Skills[nomeSkill].Cap = 0` per le skill vietate dalla classe e un cap normale/elevato per quelle permesse.
3. Eventualmente bloccare l'equipaggiamento di armi/armature non coerenti con la classe (hook lato `Item.OnEquip`/`CheckItemUse`, pattern standard ModernUO per restrizioni di equip).

Questo è interamente realizzabile in `UOContent` (nessuna modifica al motore `Server` necessaria), riusando meccanismi esistenti (cap-per-skill, eventi di creazione personaggio, razze) invece di inventare un motore di classi da zero. La sfida principale non è tecnica ma di design: decidere se le classi sono scelte una tantum e fisse, cambiabili, o solo "soft" (bonus/malus invece di veti duri).

## Decisioni custom

Framework generico deciso il 2026-09-15 (brainstorming). Copre le **regole del sistema**, valide per qualunque classe futura. Non ancora decisi: nomi/temi delle 4 classi iniziali, il contenuto reale delle 3 evoluzioni per classe (cosa fanno le singole passive/pvp/pve/ultimate), la UI lato client — lavoro di una sessione successiva, una per classe.

### Struttura: Classe → fase base → Evoluzione

- **Classe = vincolo reale**, non solo flavor: sfrutta il cap-per-skill custom descritto sopra (skill vietate → `Cap = 0`) più un blocco equip lato `Item.OnEquip`/`CheckItemUse` per armi/armature incoerenti con la classe. 4 classi iniziali, scelta alla creazione del personaggio (come le Professions, ma persistente).
- **Fase "classe base"**: subito dopo la scelta, il personaggio ha solo i vincoli skill/equip della classe — **nessuna abilità speciale ancora**. È pura gavetta.
- **Soglia di evoluzione**: quando il totale *lifetime* di EXP+HONOR guadagnati (somma di tutto ciò che il personaggio ha mai guadagnato, non il saldo spendibile — non scende mai, nemmeno con un respec) supera una soglia configurabile, si sblocca la scelta dell'Evoluzione.
- **Evoluzione = sottoclasse esclusiva**: ogni classe ha 3 evoluzioni, sono percorsi alternativi (tipo specializzazione), se ne sceglie una sola. Da qui in poi il personaggio ha accesso al pool di abilità di quell'evoluzione.

### Abilità: pool e ordine di sblocco

Ogni evoluzione ha un pool di N passive, N pvp, N pve, N ultimate (N per-evoluzione, deciso quando si progetta il contenuto reale). Ordine di sblocco, univoco e non aggirabile:

1. **Tutte** le passive (ordine libero tra loro) — obbligatorie prima di qualunque pvp/pve.
2. Pvp e pve **liberamente intrecciate**: si può sbloccare in qualsiasi ordine e mescolare le due categorie a piacere, finché non sono sbloccate **tutte** (100% di entrambe le categorie, nessuna soglia parziale).
3. Solo a quel punto si aprono le **ultimate** (ordine libero tra loro).

Modello "albero permanente": ogni abilità sbloccata resta acquisita per sempre ed è sempre utilizzabile insieme a tutte le altre già sbloccate — nessun sistema di loadout/slot attivi da gestire.

### Valute: EXP e HONOR

- **EXP**: guadagnato tramite PvM. **HONOR**: guadagnato tramite PvP. Sono due contatori custom nuovi sul personaggio (estensione `PlayerMobile` in `Projects/UOContent/Custom/`, non riusano gold/fame/altri sistemi esistenti).
- Per ogni personaggio si tracciano **due totali distinti**: il *lifetime* (mai guadagnato, usato solo per il gate della soglia di evoluzione, monotono crescente) e il *saldo spendibile* (quanto resta da spendere in abilità, diminuisce quando si compra, aumenta con eventuale rimborso da respec).

### Pricing: dati per abilità, non regole per categoria

Niente regola fissa tipo "pvp costa sempre HONOR" — ogni abilità definisce nei suoi dati una o più **opzioni di pagamento**, ciascuna un insieme di importi in EXP e/o HONOR. Il giocatore sblocca l'abilità soddisfacendo per intero una qualsiasi delle opzioni definite. Questo singolo meccanismo copre da solo tutti i casi:

- pagamento a valuta singola con alternativa penalizzata (es. pvp: opzione A = 300 HONOR, opzione B = 600 EXP);
- pagamento a valuta combinata obbligatoria, utile per le ultimate (es. un'unica opzione = 5000 EXP **e** 5000 HONOR insieme, nessuna alternativa a valuta singola).

Tutti gli importi (e quali opzioni esistono per quale abilità) vivono in un **file di configurazione** dedicato — nessun hardcode per categoria — così le classi specifiche potranno calibrare passive/pvp/pve/ultimate indipendentemente quando si progetta il contenuto reale, riusando il sistema di configurazione nativo di ModernUO (`dev-docs/configuration.md`).

### Respec

- Cambiare **solo evoluzione** (restando nella stessa classe) o **classe intera** è possibile, ma non gratuito: entrambi rimborsano solo una **percentuale** di EXP/HONOR spesi (mai il 100%), per scoraggiare il cambio frequente. Le due percentuali (evoluzione vs classe) sono valori distinti in un file di configurazione, non ancora calibrati — placeholder da tarare in seguito.
- Il respec **non tocca i totali lifetime**: non fa riperdere una soglia di evoluzione già raggiunta.
- Il respec di **classe** azzera anche l'evoluzione scelta (le abilità sbloccate dell'evoluzione abbandonata vengono perse, rimborso parziale come sopra); se il lifetime è già oltre soglia, il personaggio può ri-scegliere subito una nuova evoluzione senza rifare la gavetta.
- Accesso al respec **oggi**: solo comando riservato allo staff (GM). **In futuro** la stessa funzione sarà esposta anche tramite un item o dialogo con un NPC — l'implementazione iniziale deve tenere la logica di respec separata dal trigger (comando GM come primo/unico chiamante), per non dover riscrivere nulla quando si aggiungerà l'item/NPC.

### Interazione con meccanismi esistenti

Razze (`Race`) e Professions restano invariate e si sommano ai vincoli di classe (non li sostituiscono): un personaggio ha sempre i veti di razza + i veti di classe, e la Profession scelta alla creazione resta solo un preset di stat/skill/equip iniziale come oggi.

### Non ancora deciso (fuori scope di questo giro)

- Nomi/temi delle 4 classi e contenuto reale di ogni evoluzione (numero e effetto di passive/pvp/pve/ultimate).
- Valore della soglia EXP+HONOR per sbloccare l'evoluzione.
- Percentuali di rimborso del respec.
- UI lato client per navigare/acquistare l'albero di abilità (da progettare in `ClassicUO/custom-docs/design/` quando si passa al contenuto reale — vedi nota sul vincolo del client, non bloccante, in `README.md`).

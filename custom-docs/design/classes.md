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

Nessuna decisione ancora presa.

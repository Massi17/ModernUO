# Armature — design

## Come funziona in ModernUO (riferimento per la fattibilità)

**File chiave:**
- `Projects/UOContent/Items/Armor/BaseArmor.cs` (~1490 righe) — classe base astratta di ogni pezzo d'armatura. Ogni tipo concreto (Plate, Chain, Leather, Studded, Ring, Bone, Dragon, ecc., in `Projects/UOContent/Items/Armor/<Materiale>/`) eredita da qui e si limita a impostare poche `virtual`/`override` (es. `ArmorBase`, `MaterialType`, `AosStrReq`, `BasePhysicalResistance`).
- `Projects/Server/Mobiles/Mobile.cs` — aggregazione delle resistenze a livello di personaggio (`ComputeResistances()`, riga 3228; `GetResistance()`, riga 3135).
- `Projects/Server/Items/Item.cs` — `PhysicalResistance`/`FireResistance`/ecc. sono proprietà `virtual` generiche su **ogni** Item (riga 621), non solo sulle armature: anche vestiti, gioielli, talismani le sovrascrivono.

**Come funziona ad alto livello:**
1. Ogni pezzo indossato ha un `ArmorRating` (AR, `BaseArmor.cs:289`) — combina `BaseArmorRating` (valore base del tipo) + bonus da `ArmorProtectionLevel` (incantamento runico) + bonus da `CraftResource` (materiale: ferro, rame, oro, valorite...) + bonus da `Quality` (regular/exceptional), poi scala per durabilità (`ScaleArmorByDurability`).
2. Ogni pezzo ha anche resistenze elementali indipendenti (`PhysicalResistance`/`FireResistance`/`ColdResistance`/`PoisonResistance`/`EnergyResistance`, `BaseArmor.cs:484-497`), ciascuna = `Base*Resistance` (fisso per tipo) + `GetProtOffset()` (dal livello di protezione runico) + bonus del materiale (`GetResourceAttrs()`) + bonus custom (`_physicalBonus` ecc., impostabili via `[CommandProperty]`/imbuing).
3. `Mobile.ComputeResistances()` **somma** le resistenze di *tutti* gli item indossati (non solo armature) e le clampa tra `GetMinResistance`/`GetMaxResistance` — il cap per i giocatori è `Mobile.MaxPlayerResistance` (statico, default 70).
4. L'AR totale del personaggio (visualizzato come "Armor") non è sommato qui: ogni pezzo contribuisce il proprio `ArmorRatingScaled` sommato altrove (calcolo separato lato combattimento/formula difesa) — non ancora tracciato in dettaglio in questa sessione, ma il punto di aggancio per singolo pezzo è comunque `ArmorRating`.
5. Requisiti (`StrRequirement`) e bonus stat (`StrBonus`/`DexBonus`/`IntBonus`) sono `virtual` con doppio binario **AOS/pre-AOS** (`AosStrReq` vs `OldStrReq`, scelti automaticamente in base a `Core.AOS`) — pattern ricorrente in tutto il codice era-sensibile.
6. Restrizioni di equip (`CanEquip`, riga 1055): genere (`AllowMaleWearer`/`AllowFemaleWearer`), razza (`CheckRace`, generico su `Item` — `RequiredRaces`), etica (`Ethic.CheckEquip`).

**Sistema "magico"/runico:** non c'è un motore di "imbuing" separato individuabile nel codice attuale — i bonus (resistenze extra, `AosAttributes`, `AosArmorAttributes`, `AosSkillBonuses`) sono campi diretti su `BaseArmor`, popolati da crafting runico (`BaseRunicTool` in `Items/Skill Items/Tools/`) o dal loot generator. Il crafting base (exceptional quality, scelta materiale) è in `Engines/Craft/DefBlacksmithy.cs` (piastre/maglia) e `DefTailoring.cs` (cuoio/stoffa).

**Punti di estensione — fattibilità:**

| Modifica | Fattibilità | Dove |
|---|---|---|
| Nuovo pezzo d'armatura (nuovo materiale, nuove stat) | **Facile** — nuova classe che eredita `BaseArmor`, nessun file esistente toccato | Nuovo file in `Projects/UOContent/Custom/` |
| Cambiare formula AR/resistenze per *tutte* le armature (bilanciamento globale) | **Media** — richiede modificare `ArmorRating`/`PhysicalResistance` ecc. in `BaseArmor.cs` (file esistente, va loggato in `CUSTOM_CHANGES.md`) | `BaseArmor.cs` righe 289-330, 484-497 |
| Alzare/abbassare il cap di resistenza (70) | **Facile** — una riga, campo statico | `Mobile.MaxPlayerResistance` |
| Nuova restrizione di equip (es. per classe custom) | **Facile** — override di `CanEquip` in una sottoclasse nuova, o hook esterno su `Item.OnEquip`/evento | `Custom/` |
| Nuovo materiale craftabile con bonus propri | **Media** — tocca `CraftResource`/`CraftResources` (enum + tabella dati condivisa da armi/armature/altro) | `Projects/Server` + dati risorse |

**Insidie note:**
- Le resistenze sono generiche su `Item`, non specifiche di `BaseArmor`: una modifica alla formula armatura non tocca automaticamente vestiti/gioielli che le implementano a parte.
- Doppio binario AOS/pre-AOS ovunque — qualsiasi nuova proprietà stat-dipendente va valutata per entrambe le ere se il fork supporta più espansioni (vedi `dev-docs/era-expansion.md`).
- Serializzazione già alla versione 10 con molti `SaveFlag` condizionali (campi scritti solo se diversi dal default) — aggiungere campi nuovi va fatto seguendo il pattern esistente (`[SerializableField]` + versione incrementata), non a mano.

## Decisioni custom

Nessuna decisione ancora presa.

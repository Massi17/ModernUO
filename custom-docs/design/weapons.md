# Armi — design

## Come funziona in ModernUO (riferimento per la fattibilità)

**File chiave:**
- `Projects/UOContent/Items/Weapons/BaseWeapon.cs` (3628 righe) — classe astratta madre di ogni arma. Espone proprietà `virtual` per quasi tutto: `MinDamage`/`MaxDamage`, `Speed`, `StrRequirement`/`DexRequirement`, `DefSkill`/`AosSkill` (skill richiesta), `DefType`/`AosType` (tipo di danno: Slashing/Piercing/Bashing/...), `DefMaxRange`, `PrimaryAbility`/`SecondaryAbility`. Ogni arma concreta (es. `Axes/Axe.cs`, `Swords/Katana.cs`) è una sottoclasse che sovrascrive solo queste proprietà — non serve mai toccare `BaseWeapon.cs` per aggiungere un'arma nuova.
- Danno: `GetBaseDamageRange` → `GetBaseDamage` (riga 2456, roll min/max + eventuale offset da `WeaponDamageLevel` in era pre-AOS) → `ScaleDamageAOS`/`ScaleDamageOld` (righe 2524/2597, applicano bonus da Str/Tactics/Anatomy/Lumberjacking + `AosAttribute.WeaponDamage` + malus da spell come Divine Fury/Discordance) → `ComputeDamageAOS`/`ComputeDamage` (righe 2594/2684) → `OnHit` (riga 1733, applica il danno al difensore, gestisce armor absorb, effetti speciali).
- Colpo/mancato: `OnSwing` (riga 834) decide hit/miss e chiama `OnHit`/`OnMiss`.
- **Weapon Abilities** (colpi speciali: Bleed Attack, Crushing Blow, Whirlwind, ecc.): classi separate in `Projects/UOContent/Items/Weapons/Abilities/`, una per abilità, tutte derivate da `WeaponAbility` (`Abilities/WeaponAbility.cs`). Registrate in un **array statico a indice fisso** (`WeaponAbility.Abilities[]`, righe 15-50) — l'indice corrisponde al packet-id mandato al client, quindi non è un registro liberamente estensibile senza toccare quel file. Un'arma sceglie la propria abilità sovrascrivendo `PrimaryAbility`/`SecondaryAbility` con una delle abilità *esistenti* (es. `Axe.cs:16 → WeaponAbility.CrushingBlow`) — questo sì è libero, nessuna modifica a file esistenti.
- **Proprietà magiche** (Hit Lightning, Hit Leech Hits/Stam/Mana, Velocity, Self Repair, Resist bonus, ecc.): bitfield con accessor dedicati in `AosWeaponAttributes` (`Projects/UOContent/Misc/AOS.cs`, righe ~759-930) — stesso pattern usato per `AosAttributes` (armature/gioielli) e `AosElementAttributes` (danno elementale). Aggiungere una proprietà del tutto nuova richiede toccare questo file (nuovo bit-flag + property), ma **combinare/riusare quelle esistenti** su un'arma custom è gratis.
- **Crafting/runici**: armi runiche derivano da `BaseRunicTool` (es. `RunicHammer.cs`) — assegnano un `CraftResource` che poi pilota bonus/hue casuali in fase di crafting via `CraftSystem` (Blacksmithy per armi in metallo). Non trovato un sistema di "Imbuing" nel repo in questa versione (probabile assente o non ancora implementato/rinominato).
- **Armi a distanza**: `BaseRanged` (`Items/Weapons/Ranged/BaseRanged.cs`) aggiunge `AmmoType` (astratto, riga 28) e la logica di consumo munizioni da quiver/zaino (righe 145-183). Nuove armi a distanza custom bastano ereditare da `BaseRanged` e implementare `AmmoType` con una munizione esistente o nuova.

**Punti di estensione più realistici per modifiche custom (senza toccare lo stock):**
1. **Arma completamente nuova** — sottoclasse di `BaseWeapon`/`BaseRanged`/`BaseThrown` in `Custom/`, overridando solo le proprietà virtuali. Zero rischio di conflitto upstream.
2. **Nuova combinazione di abilità/proprietà magiche esistenti** su un'arma custom — gratis, si fa tutto nella sottoclasse.
3. **Bonus/malus al danno legati a condizioni custom** (nuovo status, nuova classe, ecc.) — il punto di aggancio pulito è `override GetDamageBonus()` o `override ScaleDamageAOS(...)` chiamando `base.ScaleDamageAOS(...)` e poi aggiungendo il proprio termine, **senza modificare `BaseWeapon.cs`**.
4. **Nuova weapon ability** (colpo speciale mai visto) — qui c'è una vera insidia: richiede aggiungere una voce all'array a indice fisso in `WeaponAbility.cs` (file esistente → va loggato in `CUSTOM_CHANGES.md`, rischio concreto di conflitto ad ogni merge upstream che tocca quel file, es. se ModernUO aggiunge una nuova abilità stock nello stesso punto dell'array).
5. **Nuova proprietà magica** (bit-flag mai vista) — stessa insidia del punto 4, tocca `AOS.cs` (file grosso, cambia spesso upstream).

**In sintesi:** aggiungere armi con proprietà/abilità *esistenti*, in qualsiasi combinazione, è a rischio-conflitto quasi zero. Introdurre abilità o proprietà magiche *nuove* dal punto di vista del protocollo è fattibile ma tocca file centrali e va pianificato con più attenzione (conflitti upstream più probabili, e per le weapon ability serve anche verificare cosa il client si aspetta per quell'indice/animazione).

## Decisioni custom

Nessuna decisione ancora presa.

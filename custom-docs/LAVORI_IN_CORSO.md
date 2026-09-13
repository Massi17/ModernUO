# Lavori in corso

Traccia i lavori **non ancora ufficialmente terminati** — un lavoro esce da questa lista solo quando è stato verificato e tu lo confermi concluso, non quando il codice è scritto/compilato.

Formato per ogni voce: cosa è stato fatto, cosa manca per chiuderlo, stato attuale.

---

## Flame Strike castabile in movimento (prova)

- **Stato:** In attesa di verifica in gioco
- **Fatto:**
  - `Projects/UOContent/Spells/Seventh/FlameStrike.cs`: aggiunta `override bool BlocksMovement => false` (stesso pattern degli spell di Chivalry)
  - Loggato in `custom-docs/CUSTOM_CHANGES.md`
  - Build (`dotnet build Projects/UOContent/UOContent.csproj`) verificata senza errori
- **Manca:**
  - Pubblicare il server (`./publish.cmd`) e avviarlo (primo avvio, mai fatto su questa macchina — richiede setup iniziale: file dati UO, account admin)
  - Test in gioco: castare Flame Strike e verificare di potersi muovere durante il cast
  - Decisione finale: tenere la modifica, oppure revert se non è il comportamento voluto
- **Non ancora committato** (modifiche in working tree — verificare `git status` prima di considerarlo completo)

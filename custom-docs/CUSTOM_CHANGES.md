# Custom Changes Log

Traccia le modifiche fatte a file **esistenti** di ModernUO (stock content in `Projects/UOContent` o motore in `Projects/Server`) — non il contenuto nuovo, che vive in [`Projects/UOContent/Custom/`](../Projects/UOContent/Custom/) e non richiede tracciamento perché non collide mai con gli aggiornamenti upstream.

Ogni riga qui sotto aiuta, ad ogni sync con l'upstream, a capire quali modifiche in arrivo rischiano di entrare in conflitto con le nostre.

| File | Motivo | Data |
|---|---|---|
| `Projects/UOContent/Spells/Seventh/FlameStrike.cs` | Prova: `BlocksMovement => false` per rendere lo spell castabile in movimento (stesso pattern già usato da tutti gli spell di Chivalry) | 2026-09-13 |

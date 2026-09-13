# Design notes

Appunti e ragionamenti sui sistemi di gioco custom, elaborati *prima* di scrivere codice. Un file per sistema:

- [`armors.md`](armors.md)
- [`weapons.md`](weapons.md)
- [`classes.md`](classes.md)
- [`skills.md`](skills.md)
- [`spells.md`](spells.md)
- [`effects.md`](effects.md)

Aggiungine altri quando serve — un nuovo sistema, un nuovo file.

## Vincolo del client: NON è un blocco

Questo progetto userà come base **[ClassicUO](https://github.com/ClassicUO/ClassicUO)** (client open source, non il client UO ufficiale non modificato), eventualmente modificato. Diversi limiti descritti nei file di questa cartella come "richiede client custom" o "bloccato senza modifiche al client" (es. elenco skill hardcoded, ID di spellbook/buff icon, asset grafici mai esistiti) **sono quindi fattibili**, non show-stopper — comportano solo lavoro aggiuntivo lato client da pianificare esplicitamente, non un limite architetturale del server da aggirare.

## Come si usa

Quando vuoi ragionare a fondo su un sistema nuovo o complesso, dimmelo (es. "voglio ragionare sul sistema delle armature"). Uso la skill di brainstorming per esplorare il problema con te — domande, alternative, trade-off — e il risultato finale viene scritto qui, nel file del sistema corrispondente.

Questi file restano la "teoria": una volta che una decisione è stabile e pronta per l'implementazione, si passa alla scrittura del codice vero e proprio (in `Projects/UOContent/Custom/` per contenuto nuovo, vedi [`../MANUALE.md`](../MANUALE.md)).

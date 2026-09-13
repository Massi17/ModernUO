# Manuale — come lavorare su questo fork

Guida pratica per te: dove mettere il codice, come tracciare le modifiche, come comportarti con git. Il "perché" delle scelte è in [`specs/2026-09-13-modernuo-fork-sync-design.md`](specs/2026-09-13-modernuo-fork-sync-design.md); qui trovi solo il "cosa fare".

## 0. Prima di scrivere codice: ragiona sulla "teoria"

Per sistemi nuovi o complessi (armature, armi, classi, skill, spell, effetti...), non si parte dal codice. Si ragiona prima in [`custom-docs/design/`](design/README.md): un file per sistema, dove elaboriamo insieme decisioni, tabelle, alternative e trade-off. Solo quando la teoria è stabile si passa all'implementazione. Dimmi semplicemente "voglio ragionare su [sistema]" per iniziare.

## 1. Dove va il codice

**Regola d'oro: chiediti sempre "sto aggiungendo qualcosa di nuovo, o sto modificando qualcosa che esiste già?"**

| Cosa stai facendo | Dove | Serve loggare? |
|---|---|---|
| Nuovo item, mobile, quest, sistema, comando | File nuovo dentro `Projects/UOContent/Custom/` | No — i file nuovi in questa cartella non collidono mai con gli aggiornamenti upstream |
| Modifichi un file **esistente** in `Projects/UOContent` (fuori da `Custom/`) o in `Projects/Server` | Modifica diretta sul file | **Sì**, sempre — vedi punto 2 |

Se non sei sicuro se un file è "esistente" o "nuovo": `git log --follow -- <percorso file>` — se ha commit precedenti al tuo, è esistente.

**Non toccare `Projects/Server` se non è strettamente necessario.** È il motore del gioco; ModernUO stesso lo sconsiglia senza motivo esplicito. Se pensi di doverlo modificare, fermati e parliamone prima — spesso lo stesso risultato si ottiene da `UOContent`.

## 2. Ogni volta che modifichi un file esistente

Prima di committare, aggiungi una riga in [`custom-docs/CUSTOM_CHANGES.md`](CUSTOM_CHANGES.md):

```
| Projects/UOContent/Percorso/File.cs | Motivo breve della modifica | 2026-09-13 |
```

Non è burocrazia fine a se stessa: è quello che uso io per capire, ad ogni sync con l'upstream, quali aggiornamenti rischiano di scontrarsi con le tue modifiche. Se salti questo passaggio, un futuro conflitto ti arriva come sorpresa invece che come avviso.

## 3. Prima di ogni commit

1. **Build:** `dotnet build` dalla root del repo — deve passare pulito.
2. **Test:** `dotnet test` — se hai toccato logica non banale, aggiungi/aggiorna test in `UOContent.Tests`.
3. **Rispetta le convenzioni di codice di ModernUO** (già documentate in `CLAUDE.md` e `dev-docs/code-standards.md` nel repo — es. niente `Console.WriteLine`, niente `lock`/thread manuali nel game loop, naming `_camelCase`/`PascalCase`, ecc). Se lavori con me, le applico automaticamente; se scrivi codice da solo, dai un'occhiata a `CLAUDE.md` prima.
4. Se hai modificato un file esistente, controlla di aver aggiornato `CUSTOM_CHANGES.md` (punto 2).

## 4. Git — regole semplici

- **Non fare mai `rebase`, `push --force`, o riscrivere la storia** su `main`. Il workflow di questo fork si basa su merge, non su rebase (vedi spec).
- **Commit piccoli e descrittivi** — meglio tanti commit chiari che uno enorme "modifiche varie".
- **Il push su `origin` (il tuo fork) lo confermo sempre con te prima di farlo** — è già la regola concordata, non serve che tu me lo ricordi ogni volta.
- Non toccare direttamente il remote `upstream` (è in sola lettura per noi, serve solo per i `fetch`).

## 5. Quando vuoi sincronizzare con l'upstream ModernUO

Dimmi semplicemente "controlliamo gli aggiornamenti upstream" (o simile). Io:

1. Faccio `fetch` dall'upstream.
2. Ti riassumo in italiano cosa è cambiato.
3. Ti segnalo se qualche modifica upstream rischia di scontrarsi con qualcosa che hai in `CUSTOM_CHANGES.md`.
4. Decidiamo insieme se e come fare il merge.

Non c'è una cadenza fissa: lo facciamo quando vuoi tu.

## 6. Lavori non ancora conclusi

Un lavoro non è "finito" solo perché il codice è scritto e compila — lo è quando l'hai verificato e me lo confermi. Finché non succede, resta tracciato in [`custom-docs/LAVORI_IN_CORSO.md`](LAVORI_IN_CORSO.md): cosa è stato fatto, cosa manca, stato attuale. Consultalo a inizio sessione per sapere cosa è rimasto in sospeso.

## 7. Riprendere il lavoro in una nuova sessione

Non devi ripetermi nulla del contesto: ho le decisioni salvate in memoria persistente. Ti basta:

- Aprire Claude Code dentro `C:\Users\massi\Documents\GitHub\ModernUO`.
- Dirmi in breve cosa vuoi fare (nuova feature, sync upstream, fix, ecc).

## 8. Skill Claude Code attive su questo PC

Ho attivato queste skill di ModernUO (da `dev-docs/claude-skills/`) per lavorare meglio sul contenuto custom:

- `modernuo-code-audit` — controllo automatico convenzioni ad ogni modifica `.cs`
- `modernuo-content-patterns` — pattern per Item/Mobile/Creature
- `modernuo-serialization` — persistenza dello stato
- `modernuo-timers` — azioni ritardate, spawn, effetti a tempo

**Attenzione:** `.claude/` è nel `.gitignore` di ModernUO, quindi questa attivazione **non si sincronizza** col push/pull del repo — vale solo su questo PC. Se un giorno lavori da un'altra macchina, chiedimi di rifare l'attivazione (comando rapido):

```sh
for name in modernuo-code-audit modernuo-content-patterns modernuo-serialization modernuo-timers; do
  mkdir -p ".claude/skills/$name" && cp "dev-docs/claude-skills/$name.md" ".claude/skills/$name/SKILL.md"
done
```

Le altre skill disponibili in `dev-docs/claude-skills/` (gump, spell/era, quest/eventi, regioni, networking, ecc.) le attivo al volo quando il task specifico le richiede, senza bisogno che tu me lo chieda.

## Checklist rapida prima di ogni commit

- [ ] Il codice nuovo è in `Projects/UOContent/Custom/`? (se è davvero nuovo)
- [ ] Se ho modificato un file esistente, ho aggiunto la riga in `CUSTOM_CHANGES.md`?
- [ ] `dotnet build` passa?
- [ ] `dotnet test` passa (e ho aggiunto test se serviva)?
- [ ] Il commit message spiega il *perché*, non solo il *cosa*?

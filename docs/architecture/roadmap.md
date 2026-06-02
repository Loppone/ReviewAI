# ReviewAI Roadmap

> Documento permanente di progetto. Preserva lo stato di ReviewAI, le decisioni
> architetturali adottate e la roadmap futura, così che il contesto non venga perso
> tra una sessione e l'altra.
>
> Ultimo aggiornamento: 2026-06-02

---

## Visione del progetto

ReviewAI è una **Web API .NET 10 stateless** che analizza una **GitHub Pull Request**
tramite **Claude AI** e restituisce una **code review strutturata** (summary, punteggio
di severità e liste di issue per sicurezza, performance, naming e pattern).

Flusso principale:

```
GitHub Pull Request
  → recupero diff        (Octokit / GitHubDiffService)
  → analisi tramite Claude (Anthropic SDK / ClaudeReviewService)
  → restituzione review strutturata (ReviewPullRequestResult → HTTP)
```

Il servizio è **senza database**, stateless by design: ogni richiesta è autosufficiente.

---

## Architettura attuale

Decisioni architetturali già adottate e relativa motivazione:

| Scelta | Motivazione |
|--------|-------------|
| **Vertical Slice Architecture** | Ogni feature è isolata sotto `Features/<Feature>/`. Riduce l'accoppiamento orizzontale e mantiene coese le modifiche legate a una funzionalità. |
| **MediatR** | Disaccoppia il boundary HTTP dalla logica applicativa: i controller dispatchano comandi/query, gli handler contengono la logica. Punto di estensione naturale per pipeline behaviors futuri. |
| **CQRS** | Comandi e query espliciti come messaggi (`ReviewPullRequestCommand`). Chiarezza del flusso e testabilità per handler. |
| **FluentResults** | I fallimenti attesi sono valori espliciti (`Result`/`Result<T>`), non eccezioni. Flusso di errore prevedibile e componibile. |
| **Typed Errors** | Errori come tipi (`ValidationError`, `NotFoundError`, ...) invece di stringhe/eccezioni generiche. Permette un mapping HTTP deterministico e senza string-parsing. |
| **ASP.NET Core Web API** | Host HTTP standard, integrazione nativa con DI, Options pattern e middleware. Documentazione API via **Scalar** (non Swagger). |
| **Anthropic SDK** | Client ufficiale per Claude. Model ID gestiti tramite la classe `AnthropicModels`. |
| **GitHub (Octokit)** | Client .NET per l'API GitHub. Astratto dietro `IGitHubClient` per testabilità (seam mockabile). |

### Struttura della soluzione

```
src/
  ReviewAI.Api/                      → Web API (composition root)
    Controllers/ReviewController.cs   (dispatch + nulla di più)
    Http/ResultActionResultExtensions.cs (mapping Result → HTTP)
    Program.cs                        (DI, Options, MediatR)
  ReviewAI.Core/                     → logica applicativa + integrazioni
    Features/ReviewPullRequest/       (Command, Handler, Result)
    Services/                         (GitHubDiffService, ClaudeReviewService)
    Common/Errors/                    (4 typed errors)
    Configuration/                    (AnthropicOptions)
tests/
  ReviewAI.Tests/                    → unit test (handler + servizi + options)
```

### Flusso di una richiesta

```
POST /api/review/pr  { pullRequestUrl }
  → ReviewController            (crea Command, _mediator.Send)
  → ReviewPullRequestHandler    (orchestrazione + short-circuit)
       → GitHubDiffService.GetPullRequestDiff()  → Result<string>
            (se IsFailed → return immediato, Claude NON viene chiamato)
       → ClaudeReviewService.ReviewDiffAsync(diff) → Result<ReviewPullRequestResult>
  → result.ToActionResult()     → 200 / 400 / 404 / 502 / 500
```

---

## Decisioni architetturali confermate

Decisioni già prese, da considerare **standard di progetto**.

### Result Pattern

- Utilizzare **FluentResults** per tutti i fallimenti attesi.
- **Non usare eccezioni** per errori attesi (validazione, not found, dipendenze esterne, risposta AI invalida).
- Utilizzare **errori tipizzati** (vedi sotto), mai string-parsing per distinguere i fallimenti.
- Handler e servizi restituiscono `Result` / `Result<T>`; l'handler **short-circuita** sul primo fallimento.
- **Mapping HTTP solo al boundary API** (`ToActionResult`): nessun concetto HTTP dentro Core.
- Le eccezioni restano riservate ai fallimenti **realmente inattesi/irrecuperabili**.

### Error Types

Definiti in `ReviewAI.Core/Common/Errors/`:

| Tipo | Causa | HTTP |
|------|-------|------|
| `ValidationError` | Input non valido (URL PR malformato, numero PR non valido) | **400** Bad Request |
| `NotFoundError` | Repository o pull request inesistenti | **404** Not Found |
| `ExternalServiceError` | GitHub API / Anthropic SDK / rete / timeout / diff URL mancante | **502** Bad Gateway |
| `InvalidAiResponseError` | Claude risponde ma viola il contratto JSON atteso | **502** Bad Gateway |
| (Result fallito sconosciuto) | Errore non previsto | **500** Internal Server Error |
| successo | — | **200** OK con `result.Value` |

### Configurazione

- Utilizzare il **pattern `IOptions<T>`** per la configurazione tipizzata.
- **`ValidateOnStart()`** + `ValidateDataAnnotations()`: configurazione mancante/invalida fa **fallire l'avvio** con messaggio chiaro (no errori scoperti solo a runtime).
- Configurazione **fortemente tipizzata** (es. `AnthropicOptions`), bound da sezione `appsettings.json`.
- **Nessun valore hard-coded** nei servizi (model, token, temperature provengono dalla configurazione).
- I **segreti** (`ANTHROPIC_API_KEY`, `GITHUB_TOKEN`) restano in **variabili d'ambiente**, non in `appsettings.json`.

### Coding Style

- **Primary Constructors** (C# 12+) per l'iniezione delle dipendenze; il parametro usato dal comportamento va copiato subito in un campo `private readonly`.
- **Una classe per file**.
- **SOLID** applicato rigorosamente.
- **Clean Code**: handler piccoli e focalizzati, controller sottili, nessuna logica di business nei controller.
- **Vertical Slice Architecture**: niente dipendenze cross-feature.
- Niente Swagger (solo Scalar), niente repository pattern, niente AutoMapper, niente classi statiche (eccetto extension). Non aggiungere pacchetti non concordati.

---

## Stato attuale del progetto

### Completato

- ✅ **Endpoint review PR** (`POST /api/review/pr`) end-to-end a livello di pipeline.
- ✅ **CQRS** con MediatR (command + handler), Core registrato correttamente.
- ✅ **Result Pattern** su tutta la slice (servizi → handler → boundary).
- ✅ **Typed Errors** (4 tipi) con mapping HTTP deterministico.
- ✅ **Configurazione Anthropic tipizzata** (`AnthropicOptions`: Model, MaxTokens, Temperature).
- ✅ **Validazione startup** (`ValidateOnStart` + DataAnnotations) — fail-fast.
- ✅ **Model ID corretto** (`claude-sonnet-4-5`, verificato contro le costanti dell'SDK; eliminato `claude-3.5` non valido).
- ✅ **Validazione URL PR** robusta (`Uri.TryCreate`, formato, numero).
- ✅ **Test esistenti**: 21 test verdi (handler, `GitHubDiffService`, `ClaudeReviewService`, `AnthropicOptions`).
- ✅ **Seam testabili**: `IGitHubClient` (Octokit) per il mocking del path GitHub.
- ✅ **Documentazione API** via Scalar (solo Development).

### In corso

- 🔄 **P1 — Affidabilità Claude**: prossima attività pianificata (structured output / tool use + parsing robusto). Non ancora avviata.
- 🔄 **MVP**: bloccato dal solo completamento del P1 (il P0 — model ID + config — è chiuso).

### Non ancora implementato

- ❌ Structured output / tool use Anthropic; parsing tollerante (fence markdown, preamboli, troncamenti).
- ❌ Logging / observability (`ILogger`, tracing, metrics).
- ❌ Exception handler globale al boundary.
- ❌ Autenticazione/autorizzazione sull'endpoint.
- ❌ Resilienza (timeout/retry/circuit breaker, `IHttpClientFactory`, Polly) e gestione rate-limit.
- ❌ Health checks, CI/CD, test di integrazione (mapping HTTP end-to-end), monitoring.
- ❌ Limiti/troncamento dimensione diff per PR molto grandi.

---

## Debito tecnico noto

Problemi già identificati e tracciati (riferiti ai report di analisi precedenti):

- **Parsing Claude fragile** — funziona solo su JSON puro; nessuna tolleranza a fence markdown, preamboli testuali o JSON troncato → `InvalidAiResponseError` frequenti su input reali.
- **Nessuno structured output** — il formato JSON non è forzato (no tool use / prefill); ci si affida all'istruzione "Return only JSON".
- **Logging assente** — zero `ILogger`; gli errori esterni e di parsing vengono convertiti in `Result.Fail` **senza lasciare traccia** (stack trace e raw text persi).
- **Exception handling globale assente** — un'eccezione fuori dai punti gestiti (es. `OverflowException` nel parser) produce un 500 grezzo non strutturato.
- **Nessuna autenticazione** — l'endpoint è aperto: superficie diretta di abuso e di costo su Claude.
- **Nessuna resilienza** — `AnthropicClient` creato con `new HttpClient()` (no `IHttpClientFactory`); nessun retry/timeout; rate-limit non gestiti.
- **Nessun rate limiting** lato API.
- **Nessuna CI/CD** — build e test non automatizzati.
- **Nessun health check**.

Note di dettaglio aggiuntive (basse priorità):

- `catch (Exception)` troppo generico in `ClaudeReviewService` → maschera bug di programmazione come `ExternalServiceError` (502).
- **Cancellazione incoerente**: `GitHubDiffService` riceve il `CancellationToken` ma **non lo passa** alle chiamate Octokit; `ClaudeReviewService` lo passa ma classifica l'annullamento come `ExternalServiceError` (502).
- `MaxTokens` default portato a 2048 (era 500): mitiga ma non elimina i troncamenti su PR estese.

---

## Roadmap prioritaria

### P1 — Affidabilità Claude

- Structured Output / Tool Use (formato a schema garantito).
- Parsing robusto della risposta AI.
- Gestione fence markdown e preamboli testuali.
- Riduzione drastica degli `InvalidAiResponseError`.

### P2 — Osservabilità

- Introduzione di `ILogger` nei servizi.
- Logging degli errori esterni (con eccezione originale) prima del `Result.Fail`.
- Logging dei parse failures (incluso un estratto del raw text del modello).
- Global exception handler al boundary API.

### P3 — Sicurezza e affidabilità

- Authentication sull'endpoint.
- Rate limiting.
- Config validation avanzata (regole oltre le DataAnnotations di base).
- `IHttpClientFactory` per i client HTTP.
- Polly (retry/timeout/circuit breaker, gestione rate-limit).

### P4 — Production Readiness

- Health checks.
- CI/CD (build + test automatici).
- Test di integrazione (mapping `Result` → HTTP end-to-end via `WebApplicationFactory`).
- Monitoring.

### P5 — Evoluzione architetturale

- Rivalutare la collocazione dei servizi (`GitHubDiffService`, `ClaudeReviewService`) rispetto alla Vertical Slice Architecture: oggi in `Core/Services/` con un unico consumatore; valutare lo spostamento dentro `Features/ReviewPullRequest/` applicando la *rule of three* prima di estrarre componenti condivisi.
- Gestione di PR molto grandi (limiti/troncamento/chunking del diff per la context window).
- Eventuali nuove feature.

---

## Stato di maturità

- **Stato attuale: Prototipo avanzato.** Scaffolding architetturale solido (CQRS, Result pattern, typed errors, config validata, DI pulita, test sui percorsi di errore). Una sola feature; integrazione Claude **non ancora validata end-to-end** contro l'API reale.
- **Obiettivo successivo: MVP.** Sbloccato dal completamento del **P1** (affidabilità Claude): con structured output e parsing robusto la feature centrale produce risultati affidabili.
- **Obiettivo finale: Production-ready.** Richiede P2–P4: observability, sicurezza, resilienza, CI/CD, health checks, test di integrazione.

---

## Come riprendere il progetto in futuro

Sezione di onboarding rapido per un nuovo sviluppatore (o per Claude Code in una nuova sessione).

### Dove siamo arrivati

- La pipeline `PR → diff → Claude → review strutturata` è implementata end-to-end a livello di codice.
- Il **P0 è chiuso**: model ID valido (`claude-sonnet-4-5`) e configurazione Anthropic tipizzata + validata all'avvio.
- Build pulita (0 warning/0 errori) e **21 test verdi**.
- L'integrazione Claude **non è ancora stata provata contro l'API reale** (servono `ANTHROPIC_API_KEY` e `GITHUB_TOKEN` veri); la correttezza è coperta dai unit test.

### Cosa è stato deciso (standard non negoziabili)

- **Result Pattern + Typed Errors** ovunque; mapping HTTP solo al boundary (`ToActionResult`).
- **Configurazione via `IOptions<T>` con `ValidateOnStart`**; nessun valore hard-coded; segreti in env var.
- **Vertical Slice + CQRS (MediatR)**; controller sottili; SOLID; primary constructors; una classe per file.
- Stack fisso: Octokit, Anthropic SDK, FluentResults, Scalar. Niente Swagger/repository/AutoMapper/classi statiche. Nessun pacchetto nuovo senza accordo.
- Riferimenti: questo file, `CLAUDE.md` (standard di progetto), e la cronologia delle PR.

### Cosa fare dopo

1. **Iniziare dal P1 — Affidabilità Claude** (priorità assoluta verso l'MVP):
   - introdurre structured output / tool use Anthropic per garantire JSON a schema;
   - rendere il parsing tollerante (fence markdown, preamboli, troncamenti);
   - obiettivo: azzerare gli `InvalidAiResponseError` da formato.
2. A seguire **P2 — Osservabilità** (`ILogger` + global exception handler): prerequisito per qualunque diagnosi in esercizio.
3. Poi **P3–P4** (sicurezza, resilienza, CI/CD, health checks, test di integrazione) verso la production-readiness.
4. Infine **P5** (evoluzione architetturale e nuove feature).

### Verifica rapida dell'ambiente

```bash
dotnet build      # atteso: 0 warning, 0 errori
dotnet test       # atteso: tutti i test verdi (21 alla data di questo documento)
```

Per una prova end-to-end reale servono `ANTHROPIC_API_KEY` e `GITHUB_TOKEN` validi; l'app espone Scalar in ambiente Development per esercitare l'endpoint `POST /api/review/pr`.

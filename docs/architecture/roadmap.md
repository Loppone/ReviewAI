# ReviewAI Roadmap

> Documento permanente di progetto. Preserva lo stato di ReviewAI, le decisioni
> architetturali adottate e la roadmap futura, così che il contesto non venga perso
> tra una sessione e l'altra.
>
> Ultimo aggiornamento: 2026-06-04

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
    Authentication/                   (API key auth: handler + defaults)
    Middleware/GlobalExceptionHandler.cs (ProblemDetails RFC 7807)
    Http/ResultActionResultExtensions.cs (mapping Result → HTTP)
    Configuration/                    (ApiKeyAuth, RateLimit, ResilientHttpClient extensions)
    Program.cs                        (DI, Options, MediatR, auth, rate limiter, named HTTP client)
  ReviewAI.Core/                     → logica applicativa + integrazioni
    Features/ReviewPullRequest/       (Command, Handler, Result)
    Services/                         (GitHubDiffService, ClaudeReviewService)
    Common/Errors/                    (4 typed errors)
    Configuration/                    (AnthropicOptions, Resilience, KnownAnthropicModels + validators)
tests/
  ReviewAI.Tests/                    → unit test (handler + servizi + options + auth + resilience)
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
- ✅ **Affidabilità Claude (P1)**: contratto di risposta garantito via **Forced Tool Use**
  con **schema Strict** e **`ToolChoice` forzato** (`submit_code_review`); **deserializzazione
  tipizzata** nel record (campi mancanti/invalidi → `InvalidAiResponseError`); **gestione
  esplicita del troncamento** (`stop_reason == max_tokens` → `InvalidAiResponseError`);
  **fallback tollerante** per risposte testuali (strip fence markdown/preamboli);
  ri-sollevamento corretto della cancellazione. Nessuna nuova dipendenza.
- ✅ **Osservabilità (P2)**: `ILogger<T>` nei servizi Core con **structured logging**; log degli
  **errori esterni con eccezione originale** prima del `Result.Fail`; log dei **parse failure
  con estratto raw troncato** (500 char); **`GlobalExceptionHandler`** (`IExceptionHandler`) →
  **`ProblemDetails` (RFC 7807)** per le eccezioni inattese; **gestione corretta di
  `OperationCanceledException`** (rethrow → 499 nativo del framework). Nuova dipendenza:
  `Microsoft.Extensions.Logging.Abstractions`.
- ✅ **Sicurezza e affidabilità (P3) — completata** (Slice A–E):
  - **Resilienza per-client (Slice A + E)**: client Claude e GitHub consumano `HttpClient`
    gestiti da **`IHttpClientFactory`** (named client `"Anthropic"` e `"GitHub"`,
    `SocketsHttpHandler` con `PooledConnectionLifetime`); pipeline di resilienza
    (`Microsoft.Extensions.Http.Resilience`): total timeout → **retry esponenziale con jitter**
    su transient (5xx/408/429/network) → **circuit breaker** → attempt timeout. Parametri
    **per-client** da `ResilienceOptions`/`ClientResilienceOptions` tipizzate e validate all'avvio
    (`ResilienceOptionsValidator` + `ValidateOnStart`). Octokit instradato attraverso il named client
    `"GitHub"` via `HttpClientAdapter` + `IHttpMessageHandlerFactory`: resilienza confinata alla
    composition root (nessun Polly nei servizi). `BrokenCircuitException` mappata a
    `ExternalServiceError` (502). Eliminato `new HttpClient()` (socket exhaustion).
  - **API Key Authentication (Slice B)**: schema di autenticazione custom
    (`ApiKeyAuthenticationHandler`) sull'header configurabile; confronto **constant-time**
    (`CryptographicOperations.FixedTimeEquals`, senza short-circuit) contro le chiavi valide;
    nessun key material nei log; chiavi da env var `REVIEWAI_API_KEYS`; `ApiKeyAuthOptions`
    validata all'avvio (`ApiKeyAuthOptionsValidator`).
  - **Rate Limiting (Slice C)**: fixed-window **per-API-key** sull'endpoint review, partizionato
    sul **fingerprint SHA-256** della chiave (claim `NameIdentifier`, non la chiave grezza);
    risposta **429** con body **RFC 7807** e header `Retry-After`; `RateLimitOptions` validata all'avvio.
  - **Allowed Models Validation (Slice D)**: whitelist `AllowedModels` + `KnownAnthropicModels`;
    `Model` deve appartenere alla whitelist e ogni voce dev'essere un modello supportato —
    regole cross-property in `AnthropicOptionsValidator`, fail-fast all'avvio.
- ✅ **Test esistenti**: **97 test verdi** (handler, `GitHubDiffService`, `ClaudeReviewService`,
  `AnthropicOptions`, e i test P3: auth handler + validator, rate limit options + partition key,
  resilience options + validator + pipeline, allowed models; integration test HTTP).
- ✅ **Continuous Integration (P4 Slice A)**: workflow GitHub Actions su pull request e push verso
  `master`; restore, build Release e test Release automatici della solution; SDK .NET fissato tramite
  `global.json` con roll-forward limitato alle patch della feature band.
- 🔄 **Integration Tests (P4-B Slice 1)**: test infrastructure con `WebApplicationFactory`, pipeline
  HTTP reale fino all'handler e dipendenze esterne sostituite; coperti successo `200` e
  `ValidationError` → `400` con short-circuit verso Claude.
- ✅ **Seam testabili**: `IGitHubClient` (Octokit) per il mocking del path GitHub.
- ✅ **Documentazione API** via Scalar (solo Development).

### In corso

- 🔄 **MVP**: funzionalmente completo a livello di implementazione e test, in attesa di
  validazione end-to-end contro servizi reali (P0 + P1 + P2 + P3 chiusi).

### Non ancora implementato

- ❌ Tracing distribuito e metrics (OpenTelemetry) — il **logging strutturato** è coperto da P2; tracing/metrics restano fuori scope.
- ❌ Health checks, test di integrazione (mapping HTTP end-to-end), monitoring.
- ❌ Limiti/troncamento dimensione diff per PR molto grandi.

---

## Debito tecnico noto

Problemi già identificati e tracciati (riferiti ai report di analisi precedenti).
**Risolti** dalle slice P3 (mantenuti qui per tracciabilità):

- ~~**Nessuna autenticazione**~~ → **risolto (Slice B)**: API key authentication constant-time sull'endpoint.
- ~~**Nessun rate limiting**~~ → **risolto (Slice C)**: fixed-window per-API-key, 429 + RFC 7807.
- ~~**Nessuna resilienza**~~ → **risolto (Slice A + E)**: `IHttpClientFactory` + retry/timeout +
  **circuit breaker** per i client Claude **e** GitHub/Octokit, parametri per-client validati all'avvio.

Ancora aperti:

- **Nessun health check**.

Note di dettaglio aggiuntive (basse priorità):

- `catch (Exception)` ancora generico in `ClaudeReviewService` (e in `GitHubDiffService`) per i
  fallimenti dell'SDK/pipeline classificati come `ExternalServiceError` 502 (incl. `BrokenCircuitException`):
  può mascherare bug di programmazione, ma ora **logga l'eccezione** (P2) e la **cancellazione** è
  ri-sollevata correttamente (esclusa dal catch generico), non più mascherata come 502.
- `MaxTokens` default 2048: il **troncamento è ora rilevato esplicitamente**
  (`stop_reason == max_tokens`); resta da gestire il chunking di PR molto grandi.

---

## Roadmap prioritaria

> **P1 — Affidabilità Claude, P2 — Osservabilità e P3 — Sicurezza e affidabilità: ✅ completati**
> (vedi sezione "Completato").
> **In corso: P4 — Production Readiness.** Continuous Integration completata (Slice A);
> Integration Tests avviati (P4-B Slice 1).

### P2 — Osservabilità — ✅ COMPLETATO

- ✅ Introduzione di `ILogger` nei servizi (structured logging).
- ✅ Logging degli errori esterni (con eccezione originale) prima del `Result.Fail`.
- ✅ Logging dei parse failures (incluso un estratto **troncato** del raw text del modello).
- ✅ Global exception handler al boundary API (`ProblemDetails`, RFC 7807).
- ✅ Gestione corretta di `OperationCanceledException` (rethrow → 499 nativo del framework).

### P3 — Sicurezza e affidabilità — ✅ COMPLETATO

- ✅ **`IHttpClientFactory`** + retry/timeout via `Microsoft.Extensions.Http.Resilience` (Polly,
  confinato alla composition root) — Slice A.
- ✅ **API Key Authentication** sull'endpoint (constant-time, env var) — Slice B.
- ✅ **Rate limiting** per-API-key (fixed-window, 429 + RFC 7807) — Slice C.
- ✅ **Config validation avanzata**: whitelist model ID (`AllowedModels`) — Slice D.
- ✅ **Circuit breaker** + **resilienza client Octokit** (`HttpClientAdapter`) + parametri
  per-client validati all'avvio — Slice E.

### P4 — Production Readiness

- Health checks.
- ✅ Continuous Integration (build + test automatici).
- 🔄 Test di integrazione (mapping `Result` → HTTP end-to-end via `WebApplicationFactory`).
- Monitoring.

### P5 — Evoluzione architetturale

- Rivalutare la collocazione dei servizi (`GitHubDiffService`, `ClaudeReviewService`) rispetto alla Vertical Slice Architecture: oggi in `Core/Services/` con un unico consumatore; valutare lo spostamento dentro `Features/ReviewPullRequest/` applicando la *rule of three* prima di estrarre componenti condivisi.
- Gestione di PR molto grandi (limiti/troncamento/chunking del diff per la context window).
- Eventuali nuove feature.

---

## Stato di maturità

- **Stato attuale: MVP funzionalmente completo a livello di implementazione e test, in attesa
  di validazione end-to-end contro servizi reali.** Scaffolding architetturale solido (CQRS,
  Result pattern, typed errors, config validata, DI pulita) e **P0 + P1 + P2 + P3 chiusi**: il contratto
  di risposta Claude è garantito (forced tool use + schema strict + parsing robusto), l'osservabilità
  è in essere (logging strutturato + global exception handler) e la sicurezza/affidabilità è coperta
  (API key auth, rate limiting per-key, resilienza per-client con circuit breaker su Claude e GitHub,
  whitelist model ID).
- **Obiettivo successivo: Production-ready.** La Continuous Integration è coperta; restano
  il completamento dei test di integrazione HTTP, health checks e monitoring (tracing/metrics).

---

## Come riprendere il progetto in futuro

Sezione di onboarding rapido per un nuovo sviluppatore (o per Claude Code in una nuova sessione).

### Dove siamo arrivati

- La pipeline `PR → diff → Claude → review strutturata` è implementata end-to-end a livello di codice.
- I **P0, P1, P2 e P3 sono chiusi**: model ID valido (`claude-sonnet-4-5`), configurazione Anthropic
  tipizzata + validata all'avvio, contratto di risposta Claude garantito via forced tool use
  + schema strict (con fallback tollerante e gestione del troncamento), osservabilità
  (logging strutturato + `GlobalExceptionHandler` → `ProblemDetails`), e sicurezza/affidabilità:
  API key auth (constant-time, env var `REVIEWAI_API_KEYS`), rate limiting per-API-key (429 + RFC 7807),
  resilienza per-client con retry/timeout/circuit breaker su Claude **e** GitHub/Octokit (named client
  via `IHttpClientFactory`, `HttpClientAdapter`), whitelist model ID (`AllowedModels`).
- Build pulita (0 warning/0 errori) e **97 test verdi**.
- L'integrazione Claude **non è ancora stata provata contro l'API reale** (servono `ANTHROPIC_API_KEY` e `GITHUB_TOKEN` veri); la correttezza è coperta dai test automatizzati.

### Cosa è stato deciso (standard non negoziabili)

- **Result Pattern + Typed Errors** ovunque; mapping HTTP solo al boundary (`ToActionResult`).
- **Configurazione via `IOptions<T>` con `ValidateOnStart`**; nessun valore hard-coded; segreti in env var.
- **Vertical Slice + CQRS (MediatR)**; controller sottili; SOLID; primary constructors; una classe per file.
- Stack fisso: Octokit, Anthropic SDK, FluentResults, Scalar, `Microsoft.Extensions.Http.Resilience`. Niente Swagger/repository/AutoMapper/classi statiche. Nessun pacchetto nuovo senza accordo.
- Riferimenti: questo file, `CLAUDE.md` (standard di progetto), e la cronologia delle PR.

### Cosa fare dopo

1. **P4 — Production Readiness**: health checks, test di integrazione end-to-end
   (mapping `Result` → HTTP via `WebApplicationFactory`), monitoring/tracing-metrics.
2. Validare l'integrazione Claude **end-to-end** contro l'API reale (`ANTHROPIC_API_KEY` + `GITHUB_TOKEN`).
3. Infine **P5** (evoluzione architetturale e nuove feature).

### Verifica rapida dell'ambiente

```bash
dotnet build      # atteso: 0 warning, 0 errori
dotnet test       # atteso: tutti i test verdi (95 alla data di questo documento)
```

Per una prova end-to-end reale servono `ANTHROPIC_API_KEY` e `GITHUB_TOKEN` validi; l'app espone Scalar in ambiente Development per esercitare l'endpoint `POST /api/review/pr`.

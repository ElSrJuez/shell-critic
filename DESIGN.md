# Console Critic – Design Snapshot (Oct 2025)

An unobtrusive, values-aware “console nanny” for PowerShell.
Guiding principles: keep latency low, reuse built-ins, favour plain files over infrastructure.

---

## 1 · High-level Flow

User types command →  
A. Capture & Summarise →  
B. Index/Store →  
C. Retrieval →  
D. Critic/Nanny (suggestions) → PSHost inline or PSReadLine predictor.

---

## 2 · Architecture Bins

### A. Capture & Summarise

| Step | Component | Notes |
|------|-----------|-------|
| Hook | `ConsoleCriticProvider` (implements `IFeedbackProvider`) | Prototype exists |
| Event DTO | `InvocationEvent` | timestamp, commandLine, outcome, `ErrorRecord?` |
| Redact | `Redactor` util | Masks paths & secrets |
| Summarise | `LlmWorker.Summarise(err)` | ≤ 300 ms, cached as `summary` |
| Embed | `LlmWorker.Embed(cmd/err)` | 384-d float[], stored inline |
| Log | `FileMemoryStore.Append(line)` | JSONL in `%LocalAppData%/ConsoleCritic/logs/` |

### B. Index / Store / App Memory
#### Tiered Memory Implementation & Limitations (v0.2)

Console Critic uses a two-tier memory architecture for storing invocation records:

- **RAM Tier:**
  - In-memory ring buffer (`Queue<InvocationRecord>`) holds the most recent N records (configurable, default ~200).
  - Enables ultra-fast lookups and vector search for recent events.
  - Eviction policy: oldest records are removed when buffer is full.

- **Disk Tier:**
  - Persistent storage via Akavache (`LocalMachine` cache) for durable retention.
  - All records are written to disk; no fallback or silent error handling—errors are surfaced immediately.
  - No expiry or retention logic is implemented yet; records persist until manually purged or Akavache is configured for expiry.
  - No archival/zipping of old records; planned for future versions.

**Limitations (as of v0.2):**
- No multi-provider or failback logic; only RAM and disk tiers are supported.
- No automatic expiry, archival, or cleanup for disk tier.
- No diagnostics for memory pressure or disk usage.
- No unit/integration tests for tiered memory flows (pending).
- Configuration for expiry/retention is not yet exposed.

Future versions will address retention, diagnostics, and extensibility for additional storage backends.

Unified, schema-driven tiered memory using a single record type for both RAM and disk:

* Record schema: `InvocationRecord(Timestamp, Trigger, CommandName, CommandLine, ErrorType?, ErrorMessage?, Embedding?, Summary?)`
* RAM tier: `InMemoryRingBuffer<InvocationRecord>` (or MemoryCache) holding last ~200 normalized-embedding records for ultra-fast look-ups.
* Disk tier: append-only per-day files (TSV for feedback; JSONL for system/diagnostics) using the same schema fields.
* Write-through: `TieredMemoryStore.Add(record)` writes to RAM and disk; eviction removes oldest from RAM only.
* Retention: zips/archives files older than `config.RetentionDays`.
* Abstractions: `IMemoryStore`, `IRingBufferStore`, `IFileAppendStore` to enable future SQLite/cloud back-ends without changing the schema.

### C. Retrieval

RAG-friendly retrieval operating over the tiered memory:

* Predicate queries: `Memory.Query(predicate, take)` – LINQ over RAM first, then streamed disk.
* Vector KNN: `VectorSearch.Knn(embedding, k)` – cosine similarity on unit-normalized 384-d vectors; RAM tier first, bounded disk window fallback.
* Hybrid scoring: optional boosts for same trigger, same command, recency decay.
* RAG pipeline:
  1. Embed current invocation via `LlmWorker.Embed` (store normalized vector for reuse).
  2. Retrieve top-K similar `InvocationRecord`s from RAM; fill remainder from bounded disk scan.
  3. Use stored summaries where available; else fall back to compact redacted snippets.
  4. Deduplicate near-duplicates (use nightly clusters) and cap total context (3–7 items).
  5. Build prompt: system constraints + current error + retrieved context; call summariser/LLM.
* `Export-ConsoleCriticMemory` cmdlet for manual analysis.

### D. Critic / Nanny

* `TriggerEvaluator` maps `InvocationEvent + Memory` → `Critique[]`.  
* `ActionRecommender` converts critique → `FeedbackItem` (inline) or `PredictionResult`.  
* Policy gates: verbosity, Do-Not-Disturb, per-trigger toggles.

### E. LLM / AI Enrichment

| Interface | Implementation (v0.2) | Purpose |
|-----------|-----------------------|---------|
| `ILlmWorker` | `LocalOnnxWorker` (MiniLM) | Embed & Summarise |
| Queue | `BackgroundJobQueue` | Caps CPU & time |
| Cluster | nightly job → `clusters-YYYYMMDD.json` | Deduplicate error hints |

### F. Config & Policy

`$HOME/.console-critic/critic.json`
```json
{
  "UseAI": false,
  "Verbosity": "Normal",
  "RetentionDays": 30,
  "Triggers": {
    "Error": true,
    "CommandNotFound": true
  }
}
```

### G. Privacy & Compliance

* Logs are local by default.
* Redaction before disk write.
* Opt-in telemetry stub (disabled by default).
* `Export-ConsoleCriticMemory -Redact` for safe sharing.

### H. Extension Surface

```csharp
public interface ITrigger
{
    bool Match(InvocationEvent e, IMemoryStore mem);
    Critique[] Produce(InvocationEvent e, IMemoryStore mem);
}
```
Third-party DLLs can extend triggers, storage, or outputs.

### I. Observability & Tests

* **Pester** – end-to-end PowerShell interaction tests.  
* **xUnit** – unit tests for redaction, evaluator logic, vector search.  
* **EventCounters** – measure time spent in AI queue and overall latency.

---

## 3 · Version Roadmap

| Version | Focus |
|---------|-------|
| v0.2 | Inline error feedback · JSONL logger · Config scaffold |
| v0.3 | PSReadLine predictor suggestions |
| v0.4 | Retention, redaction, opt-in AI embeddings & summaries |
| v0.5 | Devil/Angel channels, TTS prompts, richer memory back-ends |

---

## 4 · Non-Goals

* No automatic command execution.
* No custom GUI windows.
* No mandatory cloud dependencies.

---

## Implementation Plan (Checklist)

1. [x] FeedbackProvider captures PowerShell events and writes to `critic-YYYYMMDD.log` (TSV), restoring clean feedback event logging
2. [x] Out-of-band/system events and diagnostics written to `events-YYYYMMDD.jsonl` and `diagnostics-YYYYMMDD.log` (separated from feedback)
3. [x] Embedding generation via ONNX MiniLM, path from config
4. [x] Summariser via Foundry Local/OpenAI (stub, config-driven)
5. [x] Config file at `%LOCALAPPDATA%/ConsoleCritic/config.json` (model paths, options)
6. [x] CriticFeedbackProvider logs only feedback events to TSV, not JSONL; system logs are kept separate
7. [ ] Implement real Summariser via OpenAI/Foundry and validate ≤300 ms latency
  7.1. [ ] Retention/archival logic for disk tier (expiry, cleanup, or archiving)
  7.2. [ ] Unit/integration tests for tiered memory (RAM + disk)
  7.3. [ ] Configurable expiry for Akavache objects
  7.4. [ ] Diagnostics/observability for memory and disk usage
  7.5. [ ] Documentation update for tiered memory implementation and limitations
8. [x] Implement tiered memory (RAM + disk) using Akavache: install `Akavache.Sqlite3` & `Akavache.SystemTextJson`, initialize builder (`WithAkavacheCacheDatabase<SystemJsonSerializer>` + `WithSqliteProvider`), and implement `TieredMemoryStore` (MemoryCache for RAM, LocalMachine for disk) to persist `InvocationRecord` schema with normalized embeddings and summaries
  8.1. [x] Akavache is initialized at startup using the builder pattern in `AkavacheInit.Initialize()`, with `WithAkavacheCacheDatabase<SystemJsonSerializer>`, `WithSqliteProvider`, and application name set to `ConsoleCritic`.
  8.2. [x] `InvocationRecord` schema is persisted to both RAM and disk tiers, including all required fields (timestamp, command, error, embedding, summary).
  8.3. [x] End-to-end validation: `TieredMemoryStore` writes and retrieves records from both RAM and disk; error handling is surfaced and tested.
  8.4. [ ] Embedding and summary generation is fully integrated and stored for each record.
  8.5. [ ] Add retention/expiry logic for disk tier (Akavache object expiry or manual cleanup).
  8.6. [ ] Add diagnostics/observability for tiered memory (usage, errors, timings).
  8.7. [ ] Add unit/integration tests for tiered memory flows.
  8.8. [ ] Update documentation and code comments for final implementation.
9. [ ] Implement vector search (cosine/Euclidean) for embeddings
10. [ ] Add nightly clustering job for error deduplication
11. [ ] Extend config for policy gates, verbosity, triggers
12. [ ] Add export cmdlet for memory analysis (with redaction)
13. [ ] Add extension surface for custom triggers/storage
14. [ ] Add Pester/xUnit tests for all major flows
18. [ ] Integrate retrieval to use Akavache tiers (RAM first, bounded disk window) for KNN and predicate queries

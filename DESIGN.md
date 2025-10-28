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
8. [ ] Implement tiered memory (RAM + disk) using Akavache: install `Akavache.Sqlite3` & `Akavache.SystemTextJson`, initialize builder (`WithAkavacheCacheDatabase<SystemJsonSerializer>` + `WithSqliteProvider`), and implement `TieredMemoryStore` (MemoryCache for RAM, LocalMachine for disk) to persist `InvocationRecord` schema with normalized embeddings and summaries
9. [ ] Implement vector search (cosine/Euclidean) for embeddings
10. [ ] Add nightly clustering job for error deduplication
11. [ ] Extend config for policy gates, verbosity, triggers
12. [ ] Add export cmdlet for memory analysis (with redaction)
13. [ ] Add extension surface for custom triggers/storage
14. [ ] Add Pester/xUnit tests for all major flows
18. [ ] Integrate retrieval to use Akavache tiers (RAM first, bounded disk window) for KNN and predicate queries

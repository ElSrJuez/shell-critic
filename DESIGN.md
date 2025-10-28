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

### B. Index / Store / Memory

* Append-only JSONL per day with inline `emb`.
* `InMemoryRingBuffer` holds last ~200 events for instant look-ups.
* Retention job zips files older than `config.RetentionDays`.
* `IMemoryStore` abstraction allows future Sqlite / cloud back-ends.

### C. Retrieval

* `Memory.Query(predicate, take)` – LINQ over streamed JSON.  
* `VectorSearch.Knn(embedding, k)` – brute-force for ≤2 k vectors; upgrade to FAISS later.  
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

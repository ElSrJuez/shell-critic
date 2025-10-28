# LLM Integration Plan

This document tracks local-first language-model features for Console Critic.

---

## 1 · Goals

* **Low-latency inference** for on-demand summarisation.
* **Offline semantic search** over command memory via embeddings.
* **Opt-in, replaceable model backend** (local ONNX / Foundry, future cloud).

---

## 2 · Components

| Layer | Artifact | Status |
|-------|----------|--------|
| API | `ILlmWorker` interface <br/>• `EmbedAsync(string)` → `float[]`<br/>• `SummariseAsync(string)` → `string` | Implemented ✔︎ |
| Local runtime | `LocalFoundryWorker` (FoundryLocalManager) | Implemented ✔︎ |
| Factory | `LlmWorkerProvider` static class returning singleton | ☐ |
| Background queue | simple Channel inside worker | Implemented ✔︎ |
| Embedding store | inline `emb` field in JSONL | Planned |
| Vector search | brute-force cosine/Euclidean over ≤2 k vecs | Planned |
| ANN upgrade | optional FAISS / VectorData store | Later |

---

## 3 · Usage Scenarios

### 3.1  Summarisation (Inference)

1. Capture pipeline detects an `ErrorRecord` > 5 lines.
2. Call `await LlmWorker.SummariseAsync(errorText, ct)`, with 300 ms timeout.
3. Persist summary in `summary` field; surface in inline `FeedbackItem`.

### 3.2  Embedding Generation

1. On each captured `InvocationEvent`, enqueue `EmbedAsync(cmdLine|errorText)`.
2. Store resulting `float[384]` in same JSONL record (`emb`).
3. Ring-buffer keeps last 200 embeddings in RAM for quick search.

### 3.3  Semantic Search

1. Predictor receives partial command from PSReadLine.
2. Build query embedding via `EmbedAsync(partialCommand)` (cached).
3. Run `VectorSearch.Knn(queryEmb, k, buffer)` to fetch nearest past events.
4. Combine distance + rule heuristics → craft suggestion string.

---

## 4 · Model Choices

| Alias | Model | Context | Params | Use |
|-------|-------|---------|--------|------|
| `phi3-mini-4k-instruct-cuda-gpu` | Phi-3-mini 4k | 4 k tokens | 3.8 B | summarisation |
| `qwen2.5-0.5b` | MiniLM-L12-v2 (emb) | 128 tokens | 0.5 B | embeddings |

Both served by Foundry Local runtime; downloaded on first use.

---

## 5 · Configuration Snippet

```json
{
  "Llm": {
    "UseAI": true,
    "ModelAlias": "phi3-mini-4k-instruct-cuda-gpu",
    "EmbeddingAlias": "qwen2.5-0.5b",
    "MaxCpuMsPerSession": 300,
    "EmbeddingDim": 384
  }
}
```

---

## 6 · Open Tasks

* Implement `LlmWorkerProvider` with lazy singleton and graceful fallback (NoOpWorker).
* Add embedding field to logger pipeline.
* Implement simple cosine-similarity search.
* Nightly clustering job (optional).

---

## Clarification of Pipelines (Updated)

• **Inference / Summaries** – Served by Foundry Local via its OpenAI-compatible endpoint.
  1. `FoundryLocalManager.StartModelAsync(alias)` to launch model.
  2. Create `OpenAIClient` with `ApiKeyCredential(manager.ApiKey)` and `Endpoint = manager.Endpoint`.
  3. Use `CompleteChatAsync` / streaming APIs to generate short summaries.

• **Embeddings / Semantic Search** – Generated locally with ONNX Runtime using MiniLM-L12-v2 (AI Dev Gallery sample). Foundry Local does **not** expose an embeddings route today, so `OnnxEmbeddingWorker` remains responsible for `EmbedAsync`.

These responsibilities are reflected in code:

| Responsibility | Worker | Backend |
|----------------|--------|---------|
| SummariseAsync | `FoundryGenerationWorker` (TBD) | Foundry Local + OpenAI SDK |
| EmbedAsync | `OnnxEmbeddingWorker` | ONNX Runtime + MiniLM |

`LlmWorkerProvider.Current` returns a composite that delegates each call to the appropriate worker.

---

## Implementation Plan (Checklist)

1. [x] Define `ILlmWorker` interface for embeddings and summaries
2. [x] Implement `OnnxEmbeddingWorker` for local MiniLM embeddings
3. [x] Implement `FoundryGenerationWorker` for summaries (stub, config-driven)
4. [x] Implement `LlmWorkerProvider` composite, config-driven
5. [x] Add config file for model paths and options
6. [x] Ensure dependencies are copied to output for PowerShell module loading
7. [x] Provide token_type_ids input for ONNX models
8. [x] Restore clean feedback event logging to TSV, separating feedback from system logs
9. [ ] Add embedding field to feedback event log (TSV)
10. [ ] Implement vector search (cosine/Euclidean) for embeddings
11. [ ] Implement real SummariseAsync via OpenAI/Foundry
12. [ ] Add in-memory ring buffer for last N embeddings/events
13. [ ] Add config flag to disable summariser if not needed
14. [ ] Add clustering job for error deduplication

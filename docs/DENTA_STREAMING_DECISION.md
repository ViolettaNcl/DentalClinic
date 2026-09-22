# ADR 0001: validated responses over browser SSE

[Documentation](README.md) · [Architecture](en/ARCHITECTURE.md) · [Русский](DENTA_STREAMING_DECISION.ru.md)

Status: Accepted

Original decision: 2026-09-06

Implementation reviewed: 2026-09-22

## Context

A Denta response includes prose, suggestions, local links, and a booking flag. Exposing fragments of provider JSON would require another partial parser at the boundary where safety rules and link checks apply.

## Decision

Keep `POST /api/chat/stream` as a browser-facing SSE endpoint. Resolve one complete typed response before emitting answer content.

The current implementation is:

1. `DentaAssistantService` asks `DentaClinicRouter` for a deterministic answer from clinic data.
2. If generation is needed, `DentaAiService` calls Gemini `generateContent`, tries supported request formats/models, and parses the result into `DentaResponse`.
3. `ChatController` normalizes the result, emits one `delta` event with the reply, and then a `done` event with suggestions, links, and `startBooking`.
4. Provider failures become an SSE error event. Ordinary `POST /api/chat` remains available for a JSON response.

`GeminiApiKeyHandler` handles API-key transport, cancellation, and duplicate trailing user messages. It no longer rewrites provider streaming calls or synthesizes provider SSE events. Earlier descriptions of that mechanism are superseded.

## Consequences

The browser receives the answer after validation, rather than token by token. This simplifies response handling and avoids displaying partial structured data, at the cost of waiting for the complete response.

Structured parsing and safety checks reduce risk; they do not guarantee that every generated statement is correct. Clinic facts and clinical decisions still require authoritative sources and human oversight.

## Verification

`DentalClinic.Tests/Unit/DentaStructuredSseContractTests.cs` checks the typed response contract, reply/metadata events, and absence of the old provider-SSE and marker parsers.

## When to revisit

Reconsider incremental generation only if measured latency justifies it and a tested incremental decoder can preserve schema validation, safe links, booking semantics, and the existing safety checks.

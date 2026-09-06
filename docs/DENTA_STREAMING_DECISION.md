# Denta structured streaming decision

Status: **Accepted**  
Date: **2026-09-06**

## Decision

Keep the current **reliability-first structured SSE contract** for Denta instead of reintroducing token-by-token provider streaming.

The public browser endpoint remains `/api/chat/stream` and keeps an SSE transport contract. Internally, however, `GeminiApiKeyHandler` intentionally converts Denta's provider request from `streamGenerateContent` to `generateContent` whenever the strict structured response schema is active. It validates the complete structured object first and only then exposes the converted result as a single synthetic provider SSE event.

`ChatController` still converts that provider event into the browser-facing reply event(s) plus the final metadata event containing suggestions, links and booking state. The visible answer may therefore arrive as one reply chunk rather than token-by-token.

## Why this is intentional

Denta is a healthcare-facing assistant. Its response schema carries more than prose: it also contains suggestions, safe local links and booking intent, while the safety layer forbids diagnosis, medication/dosage advice, pain/outcome guarantees and unsafe emergency handling.

Gemini's schema-constrained streaming response arrives as partial JSON fragments. Exposing or interpreting those fragments before the object is complete would add a second partial-JSON parser in the safety boundary and could let malformed/incomplete structured output leak into the UI. Denta's normal answers are deliberately short, so the UX gain from token animation is smaller than the reliability and safety cost.

The current contract also preserves the ordinary `/api/chat` fallback for browsers, proxies or networks that cannot consume SSE correctly.

## Regression contract

`DentaStructuredSseContractTests` locks the important behavior:

- the provider call is changed from `streamGenerateContent` to `generateContent`;
- `alt=sse` and the legacy query-string API key are removed before the provider call;
- the API key is sent through the protected header path;
- the schema-validated provider response is exposed as exactly one synthetic SSE event;
- the converted event preserves the safe reply, bounded/localized suggestions, safe local links and booking fallback.

## Revisit only when

True structured streaming should be reconsidered only if at least one of these becomes true:

1. the provider exposes independently validated structured fields incrementally rather than arbitrary partial JSON;
2. measured response latency becomes a material UX problem for Denta's short-answer format; or
3. we implement and test a dedicated incremental structured decoder that cannot expose unvalidated partial fields and preserves the existing medical-safety boundary.

Until then, deterministic structured output is the production default.

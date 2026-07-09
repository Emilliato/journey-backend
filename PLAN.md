# LearnBridge — Build Plan

Work through phases in order. Each item should land as one feature branch, incrementally committed, checked off here as part of the final commit for that item. Don't start a phase before the previous one is merged.

## Phase 1 — Backend skeleton
- [x] ASP.NET Core solution structure (`LearnBridge.Api`, `LearnBridge.Data`, `LearnBridge.Tests`)
- [x] EF Core entities + migration for: `learners`, `parents`, `parental_consent`, `learning_profile`, `goals`, `journey_memory`, `conversation_sessions`, `access_audit_log`
- [x] ASP.NET Core Identity wired up (parent accounts)
- [x] Authorization policies: learner reads/writes own rows, parent reads/writes their children's rows, default-deny otherwise
- [x] Audit logging middleware writing to `access_audit_log` on every learner-linked read/write

## Phase 2 — Auth + child profile (online-only)
- [ ] Angular signup / login flow against Identity API
- [ ] Child profile creation flow, blocked without connectivity (no offline fallback by design)
- [ ] Parental consent capture as part of profile creation, gating all later writes

## Phase 3 — Online JOURNEY interaction
- [ ] Claude Proxy endpoint (.NET) — key isolated server-side
- [ ] Angular chat UI wired to the proxy
- [ ] Goal panel, updated live from JOURNEY tool calls
- [ ] `journey_memory` writes from conversation, respecting the closed category enum

## Phase 4 — Offline capability
- [ ] `@angular/pwa` setup, service worker caching app shell
- [ ] Dexie.js schema mirroring the backend entities relevant to offline use
- [ ] WebLLM integration (`navigator.gpu` feature-detect, fallback to cached-content-only mode if unsupported)
- [ ] Separate, shorter offline system prompt for the local model (see `docs/ARCHITECTURE.md`)
- [ ] Real connectivity detection (health-check ping, not just `navigator.onLine`)

## Phase 5 — Sync
- [ ] Sync API: batch upsert endpoint, last-write-wins conflict resolution
- [ ] Angular Sync Manager: detects reconnect, pushes queued records, clears pending flags
- [ ] Visible sync-status UI (syncing / synced / offline)

## Phase 6 — Hardening
- [ ] Full offline → online → offline cycle tested end to end
- [ ] Confirm no Claude API key or secrets anywhere in the Angular bundle or service worker
- [ ] Confirm audit log completeness across all learner-linked endpoints
- [ ] Load the model on the actual demo/target device ahead of time where relevant — never over live wifi

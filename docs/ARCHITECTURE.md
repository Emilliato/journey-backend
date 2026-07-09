# LearnBridge — Architecture

## Component architecture

```mermaid
flowchart TB
    Parent[Parent / Learner]

    subgraph Client["Angular PWA — client device"]
        UI[Angular UI]
        SW[Service Worker]
        WebLLM[WebLLM<br/>offline model, e.g. Phi-4-mini]
        IDB[(IndexedDB<br/>profile, goals, memory,<br/>pending sync queue)]
        SyncMgr[Sync Manager]
    end

    subgraph Backend[".NET Backend — ASP.NET Core"]
        Auth[Identity / Auth API]
        CoreAPI[Core API]
        SyncAPI[Sync API]
        Proxy[Claude Proxy]
    end

    subgraph Cloud["Cloud"]
        DB[(SQL Server)]
        Claude[Claude API]
    end

    Parent --> UI
    UI --> SW
    UI --> WebLLM
    UI --> IDB
    WebLLM --> IDB
    SyncMgr --> IDB
    SyncMgr --> SyncAPI

    UI --> Auth
    UI --> CoreAPI
    UI --> Proxy

    Auth --> DB
    CoreAPI --> DB
    SyncAPI --> DB
    Proxy --> Claude
    Proxy --> CoreAPI
```

## Online setup → offline interaction → auto-sync

```mermaid
sequenceDiagram
    actor Parent
    participant App as Angular PWA
    participant API as .NET Backend
    participant DB as SQL Server
    participant Local as WebLLM
    participant Claude as Claude API

    Note over Parent,DB: Phase 1 — requires connectivity
    Parent->>App: Sign up / log in
    App->>API: Auth request
    API->>DB: Validate / create account
    API-->>App: JWT
    Parent->>App: Create child profile + consent
    App->>API: POST profile + consent
    API->>DB: Persist profile, consent record
    API-->>App: Confirmed

    Note over Parent,Claude: Phase 2 — day-to-day interaction
    alt Online
        App->>API: JOURNEY message
        API->>Claude: Forward with context
        Claude-->>API: Response
        API-->>App: Response
        API->>DB: Persist memory / goal updates
    else Offline
        App->>Local: JOURNEY message
        Local-->>App: Response from local model
        App->>App: Write to IndexedDB, flag pending_sync
    end

    Note over App,DB: Phase 3 — connectivity resumes
    App->>App: Sync Manager detects real reachability
    App->>API: Push queued pending_sync records
    API->>DB: Upsert, resolve conflicts
    API-->>App: Ack
    App->>App: Clear pending_sync flags
```

## Notes

- **Connectivity detection**: don't trust `navigator.onLine` alone — it only reports a network interface, not real reachability. Pair it with a lightweight health-check ping to the backend before deciding which backend (Claude Proxy vs. WebLLM) to route a JOURNEY message to.
- **Claude API key isolation**: the key lives only in the .NET Claude Proxy. Nothing in the Angular bundle, service worker, or IndexedDB should ever contain it.
- **Offline persona**: the local model does not reuse JOURNEY's full online system prompt as-is. Small quantized models follow nuanced multi-part instructions worse than Claude — give the offline path a separate, shorter, more directive system prompt scoped to encouragement, goal recall, and simple cached-content Q&A, not open-ended coaching.
- **Data model**: `learners`, `parental_consent`, `learning_profile`, `goals`, `journey_memory`, `conversation_sessions`, `access_audit_log` — same shape as the original data-layer spec, implemented as EF Core entities against SQL Server. Access control (who can read/write which rows) is enforced through ASP.NET Core authorization policies rather than database-native row-level security, since SQL Server RLS is heavier to maintain via EF Core migrations than policy-based authorization in the API layer — enforce it there, consistently, on every endpoint.
- **Sync conflicts**: last-write-wins is the default per `CLAUDE.md`. This is only safe because of the single-learner, single-primary-device assumption — revisit if a learner is ever expected to use JOURNEY from two devices without connectivity between them.

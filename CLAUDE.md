# LearnBridge — Project Context

AI-companion learning platform (JOURNEY) for school-age learners. Works fully online and supports offline interaction with JOURNEY, auto-syncing when connectivity resumes. Built for the LearnBridge / Winterborne Consulting engagement.

## Stack

- **Backend**: ASP.NET Core (.NET), EF Core, SQL Server
- **Frontend**: Angular, configured as a PWA (`@angular/pwa`, service worker)
- **Offline model**: WebLLM running client-side in-browser via WebGPU (e.g. Phi-4-mini)
- **Online model**: Claude API, called only from the backend
- **Local storage**: IndexedDB via Dexie.js
- **Auth**: ASP.NET Core Identity

## Non-negotiable constraints

These are business/compliance decisions, not style preferences. Do not optimise around them.

1. **Auth and child-profile creation require connectivity.** Never build an offline path for signup, login, or creating a learner profile.
2. **Consent gates everything.** No row may be written to `learning_profile`, `goals`, or `journey_memory` for a learner without an active (non-revoked) `parental_consent` record. Enforce server-side, not just in the UI.
3. **The Claude API key lives only in the backend Claude Proxy.** Never in Angular code, never in an environment file that ships to the client, never in the service worker. Use .NET User Secrets locally; never commit secrets to the repo.
4. **`journey_memory.category` is a closed enum**: `academic`, `preference`, `engagement`, `goal_related`. No freeform categories. No health, emotional-state, or family-relationship fields anywhere in the schema — this is a hard boundary given this is a minor's data, not a later refactor.
5. **Every read or write to a learner-linked table produces an `access_audit_log` row.**
6. **Sync conflict resolution is last-write-wins by default** (single learner, single primary device assumption). Flag explicitly in code comments if an implementation needs to depart from this.

## Architecture reference

Component diagram and the online/offline/sync sequence live in `docs/ARCHITECTURE.md`. Read it before touching sync logic, the offline model integration, or the schema.

## Build order

`PLAN.md` has the phased implementation checklist. Work through phases in order — each phase should land as a mergeable, working increment.

## Commands

- Backend: `dotnet build`, `dotnet test`, `dotnet ef database update`
- Frontend: `ng serve`, `ng build`, `ng test`

## Git workflow

- One feature branch per PLAN.md item: `feature/<phase>-<short-description>`.
- Small, incremental commits — not one commit per phase.
- Never commit directly to `main`.

## Permissions

Destructive commands (force-push, `rm -rf`, database drops) are hard-denied in `.claude/settings.json`. That's a second layer, not the primary safeguard — don't rely on it instead of good judgment.

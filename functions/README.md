# CoinDreams Cloud Functions

Server-authoritative backend for CoinDreams. Closes the architectural gap
flagged as **Critical #1** in the QA report: until these functions are
deployed and the Unity client is migrated to call them, the so-called
"server-authoritative" pillar is in fact client-authoritative — anyone with
a packet sniffer can rewrite their `players/{uid}` document.

This directory is the **scaffold**. Function bodies are stubbed and currently
throw `HttpsError("unimplemented")`. The next milestone is porting the C#
engines under `Assets/Scripts/Domain/{Cards,Village,Player}/` into the
matching files under `functions/src/`.

## Layout

```
functions/
  package.json           Node 20, firebase-functions v5, firebase-admin v12
  tsconfig.json          strict TypeScript -> lib/
  .eslintrc.js           minimal lint
  src/
    index.ts             Admin SDK init + callable exports
    types.ts             DTOs mirroring the C# domain types
    executeDraw.ts       Callable: draws a card (stub)
    executeUpgrade.ts    Callable: upgrades a village building (stub)
    executeSteal.ts      Callable: steals coins between two players (stub)
```

## Local development

```bash
cd functions
npm install
npm run build           # tsc -> lib/
npm run serve           # builds + starts firebase emulators (functions only)
```

Run a fuller emulator suite (Auth + Firestore + Functions) from the repo root:

```bash
firebase emulators:start
```

## Deploying

> **WARNING — read the security note below before deploying `firestore.rules`.**

Deploy just the rules:

```bash
firebase deploy --only firestore:rules
```

Deploy just the functions:

```bash
firebase deploy --only functions
```

Deploy everything Firestore-related:

```bash
firebase deploy --only firestore,functions
```

## What's stubbed

Each callable currently:

- Validates `request.auth` is present and matches the operation's owner.
- Validates the request payload shape.
- Logs the invocation via `firebase-functions/logger`.
- Throws `HttpsError("unimplemented", "Translate engine logic from <file>.cs")`.

The transaction body, RNG, energy regen, idempotency stamps and snapshot
mutation all need to be ported from the C# engines:

| Callable          | Source of truth                                              |
|-------------------|--------------------------------------------------------------|
| `executeDraw`     | `Assets/Scripts/Domain/Cards/AuthoritativeDrawEngine.cs`     |
| `executeUpgrade`  | `Assets/Scripts/Domain/Village/AuthoritativeVillageUpgradeEngine.cs` |
| `executeSteal`    | `Assets/Scripts/Domain/Player/AuthoritativeStealEngine.cs`   |

Each stub file contains an ordered implementation checklist in its header
comment — start there.

## Unity migration plan (Critical #1 closeout)

Once the function bodies are ported, the Unity client must stop running the
engines locally and call the callables instead. Touch points:

1. **Repository layer** — `Assets/Scripts/Runtime/Firebase/FirestorePlayerRepository.cs`
   (or wherever the Firestore reads/writes live). Replace direct
   `DocumentReference.SetAsync(...)` calls for mutating ops with:
   ```csharp
   var func = FirebaseFunctions.DefaultInstance.GetHttpsCallable("executeDraw");
   var response = await func.CallAsync(payloadDictionary);
   ```
2. **Service registration (VContainer)** — bind the existing
   `IAuthoritativeDrawService` / `IAuthoritativeVillageUpgradeService` /
   `AuthoritativeStealEngine` consumers to remote implementations that
   marshal the request DTO into a `Dictionary<string, object>` for
   `HttpsCallable`, and unmarshal the result.
3. **Keep the C# engines** — do NOT delete them. They become the
   authoritative reference implementation for the TypeScript port, and they
   are still useful as fallback / offline preview logic in `Editor` builds.
4. **Catalog payloads** — the draw catalog (`cards[]`) and the village
   upgrade catalog (`upgradeCostsByBuilding`) are currently passed in from
   the client. That's acceptable as a first cut, but the eventual goal is
   to load these from Firestore (`/config/cards`, `/config/village`) inside
   the function itself so a tampered client cannot inject custom weights or
   slash upgrade costs.
5. **Telemetry** — wire `firebase-functions/logger` output to Cloud Logging
   dashboards so suspicious draws/steals are visible.

### TODO list (track in your issue tracker)

- [ ] Port `AuthoritativeDrawEngine.cs` -> `executeDraw.ts`.
- [ ] Port `AuthoritativeVillageUpgradeEngine.cs` -> `executeUpgrade.ts`.
- [ ] Port `AuthoritativeStealEngine.cs` -> `executeSteal.ts`.
- [ ] Add the `updatedAtUtcTicks` and `schemaVersion` fields to the C#
      `PlayerProfileSnapshot` so client and server agree.
- [ ] Move card / village catalogs into Firestore `/config/*` and read
      them server-side instead of accepting them from the client.
- [ ] Migrate `FirestorePlayerRepository` mutating paths to `HttpsCallable`.
- [ ] Add integration tests in the Firebase emulator (Auth + Firestore +
      Functions) covering each callable's happy path and idempotency.
- [ ] Deploy functions, smoke test, THEN deploy `firestore.rules`.

## Security note (DO NOT skip)

The included `firestore.rules` makes `/players/{playerId}` **read-only**
for clients. Admin SDK (used inside these functions) bypasses the rules by
design, so the callables will still work — but anything in the Unity
runtime that writes `players/*` directly will start failing the instant
the rules are deployed.

**Recommended order:**

1. Deploy `functions` first (`firebase deploy --only functions`).
2. Migrate the Unity client to call the callables for every mutating op.
3. Smoke test against the deployed callables.
4. Only then deploy `firestore.rules` (`firebase deploy --only firestore:rules`).

Reversing this order will brick live players until the client is updated.

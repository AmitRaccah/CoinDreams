# CoinDreams Cloud Functions — Local Emulator Workflow

Server-authoritative backend for CoinDreams. The Unity client calls these
callables instead of writing to Firestore directly, so coins, energy, and
village levels can only change through validated server logic.

**This project runs the Firebase Emulator only.** It is a learning project on
the Spark (free) plan — Cloud Functions deployment requires Blaze and is
intentionally not used. All development, testing, and play-in-editor sessions
hit `http://localhost:5001` via the Functions emulator.

## Layout

```
functions/
  package.json           Node 20, firebase-functions v5, firebase-admin v12
  tsconfig.json          strict TypeScript -> lib/
  src/
    index.ts             Admin SDK init + callable exports
    types.ts             DTOs mirroring the C# domain types
    executeDraw.ts       Callable: draws a card
    executeUpgrade.ts    Callable: upgrades a village building
    executeSteal.ts      Callable: steals coins between two players
    beginVoodooSession.ts  Callable: opens a steal session and picks a victim
    executeVoodooStab.ts   Callable: applies one stab against the chosen victim
    internal/            Shared helpers (snapshot mutations, steal engine)
  scripts/
    seed.ts              Populates the emulator with test players (see below)
```

The five callables exported from `src/index.ts`:

| Callable               | Source file                       |
|------------------------|-----------------------------------|
| `executeDraw`          | `src/executeDraw.ts`              |
| `executeUpgrade`       | `src/executeUpgrade.ts`           |
| `executeSteal`         | `src/executeSteal.ts`             |
| `beginVoodooSession`   | `src/beginVoodooSession.ts`       |
| `executeVoodooStab`    | `src/executeVoodooStab.ts`        |

## Prerequisites

- **Node.js 20 LTS** — match the runtime declared in `firebase.json`.
- **Firebase CLI:**
  ```bash
  npm install -g firebase-tools
  ```
- **Login (one time):**
  ```bash
  firebase login
  ```
- **Project link (first time only):** the repo does not check in a
  `.firebaserc`. Pick or create your Firebase project from the console, then:
  ```bash
  firebase use --add
  ```
  Any project id works for emulator-only use, but the id you choose must
  match the `projectId` the seed script and the Unity client use. The
  default in `scripts/seed.ts` is `coindreams-dev` — change it there if you
  pick a different id.

## Install dependencies (one-time)

```bash
cd functions
npm install
```

This pulls `firebase-admin`, `firebase-functions`, `typescript`, and
`ts-node` (used by the seed script).

## Build TypeScript

```bash
npm run build
```

Auto-watch alternative for active development:

```bash
npm run build -- --watch
```

Outputs to `functions/lib/` which is what `firebase.json` points the
emulator at.

## Run the emulator suite

```bash
firebase emulators:start --only functions,firestore,auth
```

Expected output:

- Web UI: `http://localhost:4000`
- Functions: `localhost:5001`
- Firestore: `localhost:8080`
- Auth: `localhost:9099`

The Unity editor build is configured to call
`FirebaseFunctions.UseFunctionsEmulator("http://localhost:5001")`, so once
the emulator is up Play-in-editor will route every callable locally.

## Seed test player documents

`beginVoodooSession` picks a random victim from `/players/{playerId}`. If
the only document in there is the local player, there is no victim to find
and the call fails. `executeSteal` similarly needs a target document to read.

Seed a handful of fake victims into the emulator:

```bash
npm run seed
```

Behind the scenes this runs `ts-node scripts/seed.ts`, which writes four
players (`Alice`, `Bob`, `Carol`, `Dave`) shaped like
`PlayerProfileSnapshot` from `src/types.ts`.

Re-run any time you reset the emulator data — seed data is not
auto-restored.

## Common workflows

### Start a dev session

Three terminals (or three split panes):

1. **TypeScript watcher:**
   ```bash
   cd functions
   npm run build -- --watch
   ```
2. **Emulator suite:**
   ```bash
   firebase emulators:start --only functions,firestore,auth
   ```
3. **Unity editor** — open the project and press Play. The
   `FirebaseAuthService` will sign the local player in anonymously against
   the Auth emulator on `9099`, then callables flow through `5001`.

Shortcut: `npm run dev` chains `build` then `emulators:start` in one
command (no watcher).

### Reset Firestore data

The emulator wipes Firestore on shutdown by default. To persist data
between runs, point the CLI at a local export directory:

```bash
firebase emulators:start \
  --only functions,firestore,auth \
  --import=./local-data \
  --export-on-exit=./local-data
```

The first run creates `local-data/` (ignored via `functions/.gitignore`
under the repo root if you keep it inside `functions/`, otherwise add a
top-level ignore). Subsequent runs reload that snapshot.

To wipe state, just delete `local-data/` and reseed.

### Inspect calls

Emulator UI at `http://localhost:4000`:

- **Functions** tab — invocation log and console output from
  `functions.logger.info/...` calls inside each callable.
- **Firestore** tab — live view of `/players/{playerId}` and
  `/stealSessions/{sessionId}` documents as they mutate.
- **Auth** tab — anonymous UIDs created by the Unity client.

## When something breaks

**Functions emulator not reachable from Unity.**
- Confirm the emulator is up: visit `http://localhost:5001` in a browser —
  you should get a small JSON message from the Functions runtime, not a
  connection refused.
- Check Windows Defender / firewall — the first time the emulator binds to
  `5001` Windows usually prompts for network access. Allow it on private
  networks at minimum.
- Confirm the Unity client is in editor mode and the
  `UseFunctionsEmulator("http://localhost:5001")` branch ran. Device
  builds will not use the emulator.

**"Auth required" / `unauthenticated` errors inside a callable.**
- The Unity client must sign in before calling. The existing
  `FirebaseAuthService` handles anonymous sign-in on startup against the
  Auth emulator — verify the Auth emulator is included in
  `--only functions,firestore,auth` and that the Unity log shows an
  anonymous UID.

**"Victim not found" / empty `/players` collection.**
- Run `npm run seed` to populate the test player documents.
- After resetting the emulator, the seed must be run again unless you
  used `--import/--export-on-exit` to persist `local-data/`.

**TypeScript compilation errors block the emulator.**
- The Functions emulator loads from `functions/lib/`. If `tsc` fails, the
  emulator will keep serving the previous build. Run `npm run build`
  manually and read the compiler output before restarting.

## Important — emulator-only mode

This project never deploys to production.

- The Firebase project is on the **Spark (free) plan**.
- `firebase deploy --only functions` will FAIL with a billing error
  (Blaze is required for Functions deployment) — **do not run it**.
- `firebase deploy --only firestore:rules` is technically allowed (rules
  do not require Blaze), but it is discouraged for this learning project
  because the local emulator already enforces the rules in
  `firestore.rules`. Keep changes local until there is a real reason to
  push.
- The Admin SDK used inside the callables bypasses Firestore rules by
  design, so even with strict rules deployed the server-side logic
  continues to work — but rules updates only matter to the production
  client, which does not exist here.

All testing happens against the emulator. If you ever need to compare
behavior against a live project, do it in a throwaway sandbox project
under a separate `.firebaserc` alias rather than the main one.

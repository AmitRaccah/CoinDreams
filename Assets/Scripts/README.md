# CoinDreams — Assembly Architecture (`Assets/Scripts`)

> The project's C# under `Assets/Scripts` is split from the single predefined
> `Assembly-CSharp` into **7 layered assembly definitions** (`.asmdef`). This file
> documents that structure, the rules, and the traps. Read it before adding a new
> folder, moving a type, or wiring a new package reference.

---

## Why this exists

Before the split, every project script lived in `Assembly-CSharp`, so **any**
one-line edit recompiled all of it. The split into layered assemblies means an
edit to an upper layer (UI/presenters) recompiles only that layer, not the
domain core. It also turns the layering into a **compiler-enforced invariant**:
a lower layer physically cannot reference a higher one.

> Note: the split shrinks **compile** time, not the post-compile **domain
> reload** (Unity always reloads the full domain after a code change). Domain
> Reload on *entering Play* is disabled separately (see "Related decisions").

---

## The layers (dependencies point **inward only** — strict DAG)

```
        ┌─────────────────────────────────────────────┐
        │                Game.Composition              │  ← composition root (DI)
        └─────────────────────────────────────────────┘
                              │ references
                              ▼
        ┌─────────────────────────────────────────────┐
        │                  Game.Runtime                │  ← MonoBehaviours, presenters, UI
        └─────────────────────────────────────────────┘
              │              │             │
              ▼              ▼             ▼
   ┌────────────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐
   │ Game.Infra-    │  │  Game.   │  │  Game.   │  │   Game.      │
   │ structure      │  │  Config  │  │ Signals  │  │   Domain     │
   │ (Firebase, DI) │  │  (SOs)   │  │ (DTOs,   │  │ (pure C#     │
   │                │  │          │  │  pure C#)│  │  game rules) │
   └────────────────┘  └──────────┘  └──────────┘  └──────────────┘
              │
              ▼
        Game.Domain

   Game.Editor  ── Editor-only tooling, references nothing (platform: Editor)
```

- **Leaves** (reference nothing in the project): `Game.Domain`, `Game.Signals`, `Game.Config`.
- `Game.Infrastructure` → `Game.Domain`.
- `Game.Runtime` → Domain + Config + Infrastructure + Signals.
- `Game.Composition` (the only place concrete services are wired into VContainer) → everything above.

---

## Assembly reference table

| Assembly | Folder | Role | Project refs | External refs | Notes |
|---|---|---|---|---|---|
| **Game.Domain** | `Domain/` | Pure game rules / value objects | — | — | `noEngineReferences: true` — **pure C#, no `UnityEngine`** |
| **Game.Signals** | `Signals/` | MessagePipe signal DTOs (`readonly struct`) | — | — | `noEngineReferences: true` — pure C# |
| **Game.Config** | `Config/` | `ScriptableObject` config | — | engine only | Self-contained; Config→Domain mapping happens in Runtime |
| **Game.Infrastructure** | `Infrastructure/` | Firebase, persistence, cloud functions | Domain | VContainer, UniTask, Firebase.Functions | Firebase App/Auth/Firestore are auto-referenced plugin DLLs (no explicit ref) |
| **Game.Runtime** | `Runtime/` | MonoBehaviours, presenters, UI, camera | Domain, Config, Infrastructure, Signals | VContainer, MessagePipe, UniTask, MoreMountains.Tools, Unity.Addressables, Unity.ResourceManager, Unity.InputSystem, Unity.TextMeshPro | Largest assembly |
| **Game.Composition** | `Composition/` | DI composition root (`LifetimeScope`s) | Domain, Config, Infrastructure, Runtime, Signals | VContainer, MessagePipe, MessagePipe.VContainer | The two lifetime scopes |
| **Game.Editor** | `Editor/` | Editor tooling | — | UnityEditor (implicit) | `includePlatforms: [Editor]`, `autoReferenced: false`, namespace `Game.EditorTools` |

> `Assets/TutorialInfo/*` (Unity's sample `Readme`) is intentionally left in the
> predefined `Assembly-CSharp` — it's not project code.

---

## Rules

1. **Dependencies point inward only.** Never make a lower layer reference a
   higher one. If `Game.Domain` needs something from `Game.Runtime`, the design
   is inverted — introduce an interface in Domain and implement it upward.
2. **`Game.Domain` and `Game.Signals` are pure C#** (`noEngineReferences: true`).
   Do **not** add `using UnityEngine` there — it won't compile, and that's the
   point.
3. **New code goes in the layer that matches its dependencies**, not just its
   topic. A helper that touches `MonoBehaviour` is Runtime; a rule that's pure
   math is Domain.
4. **Concrete wiring lives only in `Game.Composition`** (the `LifetimeScope`s).

---

## Gotchas (read before you trip on them)

- **MMFeedbacks (Feel) is NOT its own assembly.** Feel folds `Assets/Feel/MMFeedbacks`
  into the **`MoreMountains.Tools`** assembly via `.asmref` files. So `MMF_Player`
  etc. come from `MoreMountains.Tools` — that's why `Game.Runtime` references it.
  **Never author a `MoreMountains.Feedbacks.asmdef`** — it would duplicate
  assembly membership and break the build.
- **Moving a type between assemblies can break *serialized* references.**
  `UnityEvent` persistent calls store an **assembly-qualified** type name
  (`..., Assembly-CSharp`); moving the type out silently breaks the button/event —
  re-point it in the Inspector, or fix the string to `..., Game.Runtime`.
  `m_EditorClassIdentifier: Assembly-CSharp::...` is cosmetic (the real binding is
  the `m_Script` GUID) and Unity re-stamps it on reimport.
- **Commit `.asmdef` together with its auto-generated `.meta`.**
- **References are by *name*.** A typo = a silently missing reference = a compile
  error at the first unresolved type.

---

## Related decisions

- **Domain Reload is disabled on Play** (`ProjectSettings/EditorSettings.asset` →
  `m_EnterPlayModeOptions: 1`; Scene Reload kept). New code must be
  **domain-reload-safe**: no mutable `static` state assuming a fresh start, and
  every event subscription / Firestore listener / `CancellationToken` must tear
  down deterministically (IDisposable via VContainer, or `OnDestroy`).
- **Removed unused packages:** `com.unity.visualscripting` and
  `com.unity.multiplayer.center` (0 usages).

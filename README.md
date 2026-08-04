# Hermes Game Engine — Unity C# Project Template

[![Unity Build Pipeline](https://github.com/USERNAME/hermes-game-engine/actions/workflows/unity-build.yml/badge.svg)](https://github.com/USERNAME/hermes-game-engine/actions/workflows/unity-build.yml)

> 🚀 **Quickstart:** See the [Builder Guide](BUILDER.md) for CI/CD setup,
> deployment instructions, and how to go from Unity project → build → live
> on the Hermes portal.

A production-quality Unity project template for rapid game development, featuring a modular architecture with core lifecycle management, monetization integrations, analytics tracking, and CI/CD pipeline scripts.

## Project Structure

```
hermes-game-engine/
├── Assets/
│   └── Scripts/
│       ├── Core/
│       │   ├── GameManager.cs      — Singleton game state machine (Init/Gameplay/Pause/GameOver)
│       │   ├── SceneLoader.cs      — Async scene loading with progress/complete callbacks
│       │   └── StateMachine.cs     — Generic finite state machine base class
│       ├── Monetization/
│       │   ├── AdManager.cs        — AppLovin MAX mediation wrapper (rewarded/interstitial/banner)
│       │   └── IAPManager.cs       — Unity IAP wrapper with product registration and purchase flow
│       └── Analytics/
│           └── AnalyticsManager.cs — Event tracking with REST API posting and batching
├── Builds/
│   ├── WebGL/
│   ├── Android/
│   └── Windows/
├── Pipelines/
│   ├── butler.sh                   — itch.io Butler push script
│   ├── steam.vdf                   — SteamPipe content builder script
│   └── Fastlane/                   — Fastlane (iOS/Android) automation directory
└── README.md                       — This file
```

## Getting Started

### 1. Import into Unity

1. Create a new Unity project (2021.3 LTS or later recommended).
2. Copy the `Assets/` folder into your project's `Assets/` directory.
3. *(Optional)* Install required packages:
   - **AppLovin MAX** — for `AdManager` functionality (download from [AppLovin Developer Portal](https://dash.applovin.com/))
   - **Unity In App Purchasing** — for `IAPManager` functionality (install via Package Manager)

### 2. Initialise the Core Systems

Create a persistent boot scene with an empty `GameObject` named `[App]` and attach the following scripts:

| Component | Purpose |
|---|---|
| `GameManager` | Core state machine (drives the game lifecycle) |
| `SceneLoader` | Async scene loading (required by GameManager) |
| `AdManager` | Ad mediation (optional, attach when needed) |
| `IAPManager` | In-app purchases (optional, attach when needed) |
| `AnalyticsManager` | Event tracking (optional, attach when needed) |

All managers are singletons — they will auto-create themselves on first access if not present in the scene.

### 3. Configure Ad Unit IDs

`AdManager` reads Ad Unit IDs from **environment variables** at runtime, with fallbacks for the Unity Editor:

| Environment Variable | EditorPrefs Key | Purpose |
|---|---|---|
| `MAX_REWARDED_ID` | `MaxRewardedId` | Rewarded video ad unit |
| `MAX_INTERSTITIAL_ID` | `MaxInterstitialId` | Interstitial ad unit |
| `MAX_BANNER_ID` | `MaxBannerId` | Banner ad unit |

Set these via your CI/CD pipeline, terminal, or OS settings. In the Editor, use:
```
UnityEditor.EditorPrefs.SetString("MaxRewardedId", "YOUR_AD_UNIT_ID");
```

### 4. Configure Analytics

Set the `ANALYTICS_ENDPOINT` environment variable, or edit the `_apiEndpoint` field in the `AnalyticsManager` inspector.

## Core Architecture

### Game State Machine (`GameManager`)

The `GameManager` drives the game lifecycle through four states:

```
Init ──► Gameplay ──► Pause
 │          │            │
 │          └──► GameOver◄┘
 │               │
 └──► (ReturnToMenu / scene reload)
```

**Key methods:**
- `CompleteInitialization()` — Transition from Init → Gameplay
- `PauseGame()` / `ResumeGame()` — Pause/resume with timescale management
- `EndGame()` — Transition to GameOver
- `RestartGame(reloadScene)` — Restart from GameOver
- `ReturnToMenu()` — Load menu scene and reset to Init

### Generic State Machine (`StateMachine<T>`)

A reusable, type-safe FSM base class. Use it for AI states, UI states, or any game subsystem:

```csharp
var fsm = new StateMachine<string>();
fsm.RegisterState("Idle", new IdleState());
fsm.RegisterState("Patrol", new PatrolState());
fsm.Initialize("Idle");
fsm.TransitionTo("Patrol");
```

### Scene Loading (`SceneLoader`)

Async scene loading with progress callbacks and activation control:

```csharp
SceneLoader.Instance.LoadScene("Gameplay", onProgress: (p) => {
    loadingSlider.value = p;
}, onComplete: () => {
    Debug.Log("Scene loaded!");
});
```

## Monetization

### AdManager

Full-featured AppLovin MAX wrapper supporting:
- **Rewarded Video** — Show with placement, reward callback
- **Interstitial** — Preload and show with lifecycle events
- **Banner** — Show/hide/destroy at any position

All events are exposed as C# `event` delegates for clean integration.

### IAPManager

Unity IAP wrapper with:
- Product registration (consumable, non-consumable, subscriptions)
- Purchase flow with success/failure results
- Purchase restoration (iOS)
- Simulated purchases in the Unity Editor

## Analytics

### AnalyticsManager

Event tracking with:
- `LogEvent(name, parameters)` — Custom events with typed parameters
- `LogRevenue(revenue, network)` — Revenue tracking
- Automatic batching and periodic flush
- REST API POST to configurable endpoint
- Session ID generation
- Verbose logging mode for debugging

## CI/CD Pipelines

### itch.io (butler.sh)

```bash
# Make executable and run
chmod +x Pipelines/butler.sh
./Pipelines/butler.sh windows
./Pipelines/butler.sh android beta
```

### Steam (steam.vdf)

```bash
steamcmd +login your_username +run_app_build "Pipelines/steam.vdf" +quit
```

### Fastlane

Place your `Fastfile`, `Appfile`, and `Matchfile` in `Pipelines/Fastlane/` for iOS/Android build automation.

## Best Practices

- **All managers are singletons** — access via `ClassName.Instance` from any script.
- **XML doc comments** on all public methods for IntelliSense support.
- **`[RequireComponent]`** and `[DefaultExecutionOrder]` attributes used where appropriate.
- **Null checks** and error handling throughout.
- **`OnDestroy` cleanup** prevents dangling references and singleton leaks.
- **Editor simulation** — all managers work in the Editor without dependencies.
- **Environment variables** for secrets (Ad Unit IDs, API keys, endpoints) — never hardcode.

## License

Internal use — Hermes Game Engine.
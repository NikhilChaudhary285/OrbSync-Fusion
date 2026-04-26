# OrbSync — Photon Fusion Orb Click Game



\---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Tech Stack](#tech-stack)
3. [Game Rules](#game-rules)
4. [Controls](#controls)
5. [Architecture Summary](#architecture-summary)
6. [Rejoin System — How It Works](#rejoin-system--how-it-works)
7. [Important Technical Notes](#important-technical-notes)
8. [Testing Guide](#testing-guide)
9. [Known Limitations \& Assignment Notes](#known-limitations--assignment-notes)
10. [Build \& Run Instructions](#build--run-instructions)
11. [Project Links](#project-links)

\---

## Project Overview

OrbSync is a minimal multiplayer game built in **Unity + Photon Fusion (Shared Mode)** designed specifically to validate a robust **player rejoin system**. Up to 4 players join a shared room, click on randomly spawning orbs to collect them and earn score, and can leave and rejoin seamlessly within a 30-second window — restoring their exact score and position.

The game has no levels, no abilities, and no complex phases. The entire focus is correctness: network consistency, anti-duplication, and seamless state restoration on rejoin.

\---

## Tech Stack

|Layer|Technology|
|-|-|
|Engine|Unity 6 (LTS)|
|Networking|Photon Fusion 2 — Shared Mode|
|Language|C#|
|Platform|Windows (PC) · Android|
|Auth Identity|`SystemInfo.deviceUniqueIdentifier` via Fusion `AuthenticationValues`|

\---

## Game Rules

* **Max 4 players** per room
* Orbs spawn randomly in the scene at regular intervals
* Players **click on orbs** with the mouse (or tap on Android) to collect them
* Each collected orb awards **+1 score** to the collecting player
* A collected orb **disappears for all players simultaneously** — no duplication
* If two players click the same orb at the same time, **only one gets the score** (host arbitration via RPC)

\---

## Controls

|Action|Input|
|-|-|
|Collect an orb|**Left-click** on the orb (PC) / **Tap** the orb (Android)|
|Camera|Fixed top-down camera, no manual control needed|
|Rejoin|Relaunch the build — auto-connects to the same room (`OrbRoom01`)|

> There is no manual player movement in this game. Player position is relevant only for the rejoin restore test — it is saved on disconnect and restored on rejoin within the 30-second window.

\---

## Architecture Summary

```
GameManager              — Central coordinator, holds all manager references
├── NetworkManager       — Fusion runner, OnPlayerJoined / OnPlayerLeft callbacks
├── PlayerStateManager   — Spawns players, tracks score, saves data on disconnect
├── RejoinManager        — Stores disconnected player data, manages 30s expiry timer
└── OrbManager           — Spawns orbs via RPC, tracks active orbs, syncs late joiners
    └── OrbRpcRelay      — NetworkBehaviour that sends/receives all orb RPCs
```

**Key design decisions:**

* Orbs are **not** spawned via Fusion's `runner.Spawn`. They are spawned locally on every client via **RPC broadcast** (`RPC\\\\\\\_SpawnOrb`), keeping them lightweight and free of state authority conflicts.
* Orb collection uses a **request → host arbitration → confirm** RPC chain to prevent double-collection when two players click simultaneously.
* Score is stored as a `\\\\\\\[Networked]` property on `NetworkedPlayer` and also mirrored into a plain C# field (`CachedScore`) so it remains readable during the Fusion despawn phase when networked properties become inaccessible.

\---

## Rejoin System — How It Works

### On Disconnect

1. `NetworkManager.OnPlayerLeft` fires
2. `GameManager.OnPlayerLeft` calls `PlayerStateManager.SavePlayerData`
3. Score is read from `CachedScore` (plain C# field — safe after network despawn)
4. Position is read from `transform.position`
5. Data is stored in `RejoinManager` keyed by **stable player UID** (device identifier — see note below)
6. A **30-second countdown coroutine** starts in `RejoinManager`

### On Rejoin — Within 30 Seconds

1. Player relaunches and reconnects — Fusion fires `OnPlayerJoined`
2. `GameManager.OnLocalPlayerJoined` retrieves the UID and calls `RejoinManager.TryGetSavedData(uid)`
3. Match found → `PlayerStateManager.SpawnAndRestorePlayer` is called
4. Player spawns at their **saved position** with their **saved score** intact
5. The expiry timer is cancelled

### On Rejoin — After 30 Seconds

1. Same flow, but `TryGetSavedData` returns `false` (data was already cleared by the expired timer)
2. Player is treated as a **fresh join** — spawns at a designated spawn point with score = 0

\---

## Important Technical Notes

### 🔑 Player Identity — Why We Use `deviceUniqueIdentifier`

In **Photon Fusion Shared Mode**, `PlayerRef` (the player slot number) is **not guaranteed to be stable across reconnects**. A player who was `\\\\\\\[Player:2]` before disconnecting may come back as `\\\\\\\[Player:3]`. Keying rejoin data on `PlayerRef` will always cause a lookup miss.

**The solution:** each device is assigned a stable, persistent UID at connection time:

```csharp
var authValues = new Fusion.Photon.Realtime.AuthenticationValues();
authValues.UserId = SystemInfo.deviceUniqueIdentifier;
```

This UID is passed into Fusion's `StartGameArgs.AuthValues`. On rejoin, we retrieve it via:

```csharp
var userId = Runner.AuthenticationValues?.UserId;
```

`RejoinManager` keys all saved data on this `userId` string rather than `PlayerRef`, so reconnects always find the correct save slot regardless of what slot number Fusion assigns.

### ⚠️ Testing Across Devices — Critical

Because identity is tied to `SystemInfo.deviceUniqueIdentifier`:

* **Two instances running on the same PC** share the same identifier — the rejoin system treats them as one player
* To properly test multi-player scenarios and the rejoin flow, use **different physical devices**

**Recommended setups:**

|Setup|Notes|
|-|-|
|Windows build on PC + APK on Android phone|Best option — different devices, same network|
|Two different PCs on same internet|Works fine|
|Unity Editor + a build on the same machine|Both share the same UID — rejoin will work but they appear as the same identity|

### 📦 Orb Spawning via RPC (Not Fusion Spawn)

Orbs are **not** Fusion `NetworkObject`s. Each client instantiates its own local orb `GameObject` when it receives `RPC\\\\\\\_SpawnOrb`. The host maintains the authoritative list of active orbs and replays this list to any player who joins or rejoins mid-session via `OrbManager.SyncStateToPlayer`. Already-collected orbs are not replayed — so rejoining players never see ghost orbs.

### 🏠 Host Responsibility

The first player to join the room acts as the host. The host runs the orb spawn loop and arbitrates all collection requests. Host migration is out of scope for this assignment — if the host leaves, orb spawning stops until a new session is started.

\---

## Testing Guide

### Test 1 — Basic Flow

1. Launch 2+ instances on different devices
2. Verify orbs appear and are visible on all clients
3. Click orbs — score increments on the collecting player
4. All clients see collected orbs disappear immediately

### Test 2 — Rejoin Within 30 Seconds ✅

1. Note Instance 2's current score and approximate position
2. Close Instance 2 completely
3. Wait \~10 seconds
4. Relaunch Instance 2
5. **Expected:** player spawns at saved position, score is restored

### Test 3 — Rejoin After 30 Seconds ✅

1. Close Instance 2
2. Wait 35+ seconds
3. Relaunch Instance 2
4. **Expected:** player spawns at a fresh spawn point, score = 0

### Test 4 — World State on Rejoin ✅

1. While Instance 2 is disconnected, collect orbs on Instance 1
2. Reconnect Instance 2
3. **Expected:** Instance 2 does NOT display already-collected orbs; it sees all currently active orbs correctly

### Test 5 — Two Players Click Same Orb ✅

1. Position two players near the same orb
2. Both click at the same time
3. **Expected:** only one player receives score, orb disappears once, both clients show consistent state

### Test 6 — Click and Disconnect Simultaneously

1. Click an orb and immediately force-close the client
2. **Expected:** no double score awarded, orb disappears cleanly for remaining players

### Test 7 — Stress Test

1. Let many orbs accumulate
2. Have a player leave and rejoin multiple times
3. **Expected:** no duplicate orbs, no missing orbs, consistent state on all clients

\---

## Known Limitations \& Assignment Notes

|Limitation|Reason / Notes|
|-|-|
|Host migration not supported|Out of scope per assignment specification|
|Two instances on the same PC share a UID|`SystemInfo.deviceUniqueIdentifier` is per device, not per process — use different physical devices for full multi-player testing|
|`PlayerRef` changes on reconnect|Fusion Shared Mode limitation — fully mitigated by the UID-based rejoin key|
|Score is awarded locally|In Shared Mode, each client owns its own networked score; host confirms which player won the orb before score is applied|
|30s rejoin timer uses `Time.time`|Timer is tied to the host's Unity process — if the host application is suspended, the timer pauses|

\---

## Build \& Run Instructions

### Windows

1. Download `OrbClickGameFusion\\\\\\\_Builds\\\\\\\_04272026.zip`
2. Extract the Windows folder
3. Run `OrbClickGame.exe`
4. Game auto-connects to room `OrbRoom01`

### Android

1. Download and sideload `OrbClickGame.apk`
2. Enable "Install from unknown sources" in device settings if prompted
3. Launch the app — auto-connects to the same room

### Running from Unity Editor

1. Open the project in Unity 6
2. Ensure Photon Fusion 2 SDK is imported
3. Add your Photon App ID to `Assets/Fusion/Resources/NetworkProjectConfig`
4. Open `Assets/Scenes/GameScene`
5. Press Play

\---

## Project Links

|Resource|Link|
|-|-|
|📁 GitHub Repository|[OrbSync-Fusion](https://github.com/NikhilChaudhary285/OrbSync-Fusion)|
|🎥 Demo Video|[Orb Click Game (Fusion) — Demo Video](https://drive.google.com/file/d/1WVju_96nDcXoj0tC3D6uOxFx1CUuEHP0/view?usp=sharing)|
|📦 Builds (Windows + Android)|[Orb Click Game Fusion — Builds](https://drive.google.com/file/d/1EX3JLSkFCiLC57Sn9ydDXqD9rIbTbz9-/view?usp=sharing)|
|🗂️ Full Unity Project|[OrbSync-Fusion](https://drive.google.com/file/d/1_gBaj2W97-_RZIwjvIBiXTnL2j1p9Q-4/view?usp=sharing)|
|🌐 Portfolio|[nikhilchaudhary285.github.io](https://nikhilchaudhary285.github.io/)|

\---

## Contact

**Nikhil Chaudhary**
📧 nikhilchaudhary285@gmail.com
💼 [LinkedIn](https://www.linkedin.com/in/nikhilchaudhary285)

\---


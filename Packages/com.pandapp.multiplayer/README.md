# Pandapp Multiplayer Framework

Reusable multiplayer framework package (Core + Transport + App + optional Gameplay/UI).

## Install (UPM)

Unity Package Manager -> **Add package from git URL**:

`https://<repo>.git?path=/Packages/com.pandapp.multiplayer#v0.1.0`

## Structure

- `Runtime/Core` -> game-agnostic contracts + data types (`Pandapp.Multiplayer.Core`)
- `Runtime/App` -> Unity entry/config layer (`Pandapp.Multiplayer.App`)
- `Runtime/Transport/PhotonPun2` -> Photon PUN2 adapter (`Pandapp.Multiplayer.Transport.Pun2`)
- `Runtime/Gameplay` -> reusable gameplay primitives (`Pandapp.Multiplayer.Gameplay`)
- `Runtime/UI` -> optional UI layer (`Pandapp.Multiplayer.UI`)

## Bootstrap (minimal)

1) Add `MultiplayerApp` to your Bootstrap scene.
2) Assign `transportBehaviour` (ex: `Pun2NetworkTransport`).
3) (Optional) Assign `sceneLoaderBehaviour` (must implement `ISceneLoader`) or leave empty to use the default loader.
4) (Optional) Assign `gameModuleBehaviour` (must implement `IGameModule`) to handle player/message/update callbacks (helper base: `GameModuleBehaviourBase`).
5) (Optional) Assign `MultiplayerConfig` + `SceneCatalog`.
6) Message IDs: Core uses `1-99`. Gameplay primitives use `180-199`. Use `100-179` for your game (Photon PUN2 supports only `0-199`).

## Matchmaking

- `ConnectOptions.GameVersion` isolates matchmaking pools (both clients must match).
- `QuickMatchOptions` supports `MaxPlayers`, `QueueId`, `ModeId`, `MapId`, and `CustomProperties`.

## Gameplay primitives (v0.1)

- `NetworkIdentity` gives an object a `NetworkId` (scene: set in editor, spawned: assigned at runtime).
- `NetworkTransformSync` replicates transform state (host/owner authority + interpolation).
- `NetworkPrefabCatalog` maps `PrefabId -> prefab` for network spawning.
- `NetworkSpawner` lets the host spawn/despawn objects + replicate ownership (syncs existing spawns to late joiners).
- `NetworkCommandRouter` routes client -> host commands (reserved message id: `184`) via `INetworkCommandHandler`.

## Photon PUN2 (CS0234 fix)

`Pun2NetworkTransport` is compiled in a separate asmdef. To avoid `Photon.Pun` reference errors:

1) Import Photon PUN2 (define: `PHOTON_UNITY_NETWORKING`).
2) Unity menu: `Pandapp/Multiplayer/Photon PUN2/Setup (Fix CS0234)` (adds `PANDAPP_PHOTON_PUN2` + asmdefs if needed).

## Samples

Unity Package Manager -> this package -> **Samples** -> **Minimal Demo** -> **Import**

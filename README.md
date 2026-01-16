# Pand Multiplayer Framework

This repo contains a Unity UPM package:

- Package path: `Packages/com.pandapp.multiplayer`
- Install (Git URL): `https://<repo>.git?path=/Packages/com.pandapp.multiplayer#v0.1.0`

## Structure

- `Packages/com.pandapp.multiplayer/Runtime/Core`
- `Packages/com.pandapp.multiplayer/Runtime/App`
- `Packages/com.pandapp.multiplayer/Runtime/Transport/PhotonPun2`
- `Packages/com.pandapp.multiplayer/Runtime/Gameplay`
- `Packages/com.pandapp.multiplayer/Runtime/UI`

## Gameplay

- `NetworkCommandRouter` provides client -> host commands (authoritative actions like kick/shoot) without hardcoding gameplay into transport.

## Photon PUN2

Photon adapter setup (one-time):

1) Import Photon PUN2 (define: `PHOTON_UNITY_NETWORKING`).
2) Unity menu: `Pandapp/Multiplayer/Photon PUN2/Setup (Fix CS0234)`

## Sample

Unity Package Manager -> `com.pandapp.multiplayer` -> **Samples** -> **Minimal Demo**

# Pand Multiplayer Framework

Bu repo, Unity UPM (Package Manager) formatında bir paket içerir.

- Paket yolu: `Packages/com.pandapp.multiplayer`
- Kurulum (Git URL): `https://<repo>.git?path=/Packages/com.pandapp.multiplayer#v0.1.0`

## Structure

- `Packages/com.pandapp.multiplayer/Runtime/App`
- `Packages/com.pandapp.multiplayer/Runtime/Core`
- `Packages/com.pandapp.multiplayer/Runtime/Transport/PhotonPun2`
- `Packages/com.pandapp.multiplayer/Runtime/UI`

## Photon PUN2

Photon PUN2 adapter’ı asmdef ile izole edildiği için ek bir setup adımı vardır:

1) Photon PUN2 import et (define: `PHOTON_UNITY_NETWORKING`).
2) Unity menüsü: `Pandapp/Multiplayer/Photon PUN2/Setup (Fix CS0234)`

## Sample

Unity Package Manager → `com.pandapp.multiplayer` → **Samples** → **Minimal Demo**

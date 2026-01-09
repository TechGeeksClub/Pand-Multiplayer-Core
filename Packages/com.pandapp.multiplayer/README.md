# Pandapp Multiplayer Framework

Reusable multiplayer framework paketi (Core + Transport + UI katmanları).

## Install (UPM)

Unity Package Manager → **Add package from git URL**:

`https://<repo>.git?path=/Packages/com.pandapp.multiplayer#v0.1.0`

## Structure

- `Runtime/Core` → oyun-agnostic kontratlar ve data tipleri (`Pandapp.Multiplayer.Core`)
- `Runtime/App` → Unity entry/config katmanı (`Pandapp.Multiplayer.App`)
- `Runtime/Transport/PhotonPun2` → Photon PUN2 adapter katmanı (`Pandapp.Multiplayer.Transport.Pun2`)
- `Runtime/UI` → opsiyonel UI katmanı (`Pandapp.Multiplayer.UI`)

## Bootstrap (Minimal)

1) Sahneye `MultiplayerApp` ekle ve `transportBehaviour` alanına bir transport component bağla.
2) (Opsiyonel) `SceneCatalog` + `MultiplayerConfig` oluşturup `MultiplayerApp.config` alanına ver.
3) Mesaj ID aralığı: Core `1-99` rezerve; oyun mesajları için `100+` kullan.
4) Quick match için `MultiplayerApp.QuickMatch(...)` çağır (join random, yoksa create).

## Photon PUN2 (CS0234 fix)

`Pun2NetworkTransport` ayrı bir asmdef içinde derlenir; bu yüzden Photon assembly’leri referanslanabilir olmalı.

1) Photon PUN2’yi import et (define: `PHOTON_UNITY_NETWORKING`).
2) Unity menüsü: `Pandapp/Multiplayer/Photon PUN2/Setup (Fix CS0234)`
   - Gerekirse Photon için asmdef oluşturur
   - `PANDAPP_PHOTON_PUN2` define’ını ekler
3) Derleme bitince `Pun2NetworkTransport` kullan.

## Samples

Unity Package Manager → bu paket → **Samples** → **Minimal Demo** → **Import**

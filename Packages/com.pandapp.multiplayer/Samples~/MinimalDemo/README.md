# Minimal Demo (Sample)

Bu sample, `Pandapp.Multiplayer` paketinin Core + App akışını hızlıca doğrulamak için hazırlanmıştır.

## Setup

1) Unity: Package Manager → `Pandapp Multiplayer Framework` → **Samples** → **Minimal Demo** → **Import**
2) `Assets/Samples/.../Minimal Demo/Scenes` içindeki sahneleri Build Settings'e ekle:
   - `PandappMinimalDemo_Bootstrap`
   - `PandappMinimalDemo_Game`
3) Play → UI üzerinden Connect/Create/Join/Ready/Start akışını test et.
4) İstersen `Quick Match` ile (oda arayıp yoksa oluşturup) hızlı giriş test et.
5) Gameplay testi: Host instance'da `Spawn Players + Ball` → WASD ile kendi oyuncunu, ok tuşlarıyla (host) topu hareket ettir.

## Photon PUN2 ile test

1) Photon PUN2'yi import et (define: `PHOTON_UNITY_NETWORKING`).
2) Unity menüsü: `Pandapp/Multiplayer/Photon PUN2/Setup (Fix CS0234)` (bir kere).
3) `PandappMinimalDemo_Bootstrap` sahnesinde:
   - `LoopbackNetworkTransport` komponentini kaldır
   - Yerine `Pun2NetworkTransport` ekle
   - `MultiplayerApp.transportBehaviour` alanına `Pun2NetworkTransport` bağla

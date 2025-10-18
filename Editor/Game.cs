using UnityEngine;

namespace PandNet.Core
{
    public class MultiplayerExample : MonoBehaviour
    {
        // Bu script sahneye eklenirse oyun başında bir mesaj gösterir
        void Start()
        {
            Debug.Log("PandNet Multiplayer Core paketi çalışıyor!");
        }

        // Basit bir örnek fonksiyon
        public void ConnectToServer()
        {
            Debug.Log("Sunucuya bağlanılıyor... (örnek fonksiyon)");
        }
    }
}

using System.Collections.Generic;
using System.Text;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.Samples.MinimalDemo
{
    public sealed class MinimalDemoGameModule : MonoBehaviour, IGameModule
    {
        private readonly List<string> logs = new List<string>();

        public IReadOnlyList<string> Logs => logs;

        public void OnPlayerJoined(PlayerInfo player)
        {
            if (player == null)
            {
                return;
            }

            logs.Add($"Player joined: {player.PlayerName} ({player.PlayerId}) Host={player.IsHost}");
        }

        public void OnPlayerLeft(PlayerInfo player)
        {
            if (player == null)
            {
                return;
            }

            logs.Add($"Player left: {player.PlayerName} ({player.PlayerId})");
        }

        public void OnMessageReceived(NetworkMessage msg)
        {
            if (msg == null)
            {
                return;
            }

            var text = msg.Payload == null ? string.Empty : Encoding.UTF8.GetString(msg.Payload);
            logs.Add($"Message {msg.MessageId} from {msg.SenderId}: {text}");
        }

        public void OnUpdate(float deltaTime) {}
    }
}


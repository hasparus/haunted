using System;
using Unity.Netcode;
using UnityEngine;

namespace Demons
{
    public class OfflineTransport : NetworkTransport
    {
        public override ulong ServerClientId => 0;
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) { }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient() => false;
        public override bool StartServer() => true;
        public override void DisconnectRemoteClient(ulong clientId) { }
        public override void DisconnectLocalClient() { }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
        public override void Shutdown() { }
        public override void Initialize(NetworkManager networkManager = null) { }
    }
}

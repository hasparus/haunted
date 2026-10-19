using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace Demons
{
    // The couch game is a host with nobody to talk to. Opening a room starts a relay under that same host, so going
    // online never restarts the game: demons, pebbles and the day keep running and friends just walk in.
    public class CouchTransport : NetworkTransport
    {
        UnityTransport room;

        public event Action Lost;

        public bool OpenRoom(RelayServerData relay)
        {
            room = GetComponent<UnityTransport>();
            room.Initialize(NetworkManager.Singleton);
            room.SetRelayServerData(relay);
            room.OnTransportEvent += Forward;
            if (room.StartServer()) return true;
            CloseRoom();
            return false;
        }

        public void CloseRoom()
        {
            if (!room) return;
            var net = NetworkManager.Singleton;
            if (net && net.IsServer && !net.ShutdownInProgress)
                for (int i = net.ConnectedClientsIds.Count - 1; i >= 0; i--)
                    if (net.ConnectedClientsIds[i] != NetworkManager.ServerClientId) net.DisconnectClient(net.ConnectedClientsIds[i]);
            room.OnTransportEvent -= Forward;
            // ponytail: detached first, so its Shutdown doesn't warn that Netcode itself isn't shutting down
            room.Initialize();
            room.Shutdown();
            room = null;
        }

        // A failed relay takes down only the room, not the couch game it runs under.
        void Forward(NetworkEvent kind, ulong clientId, ArraySegment<byte> payload, float receiveTime)
        {
            if (kind != NetworkEvent.TransportFailure)
            {
                InvokeOnTransportEvent(kind, clientId, payload, receiveTime);
                return;
            }
            CloseRoom();
            Lost?.Invoke();
        }

        protected override void OnEarlyUpdate()
        {
            if (room) room.EarlyUpdate();
        }

        protected override void OnPostLateUpdate()
        {
            if (room) room.PostLateUpdate();
        }

        public override ulong ServerClientId => room ? room.ServerClientId : 0;

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            if (room) room.Send(clientId, payload, networkDelivery);
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            clientId = 0;
            payload = default;
            receiveTime = Time.realtimeSinceStartup;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient() => false;
        public override bool StartServer() => true;

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (room) room.DisconnectRemoteClient(clientId);
        }

        public override void DisconnectLocalClient() { }
        public override ulong GetCurrentRtt(ulong clientId) => room ? room.GetCurrentRtt(clientId) : 0;
        public override void Shutdown() => CloseRoom();
        public override void Initialize(NetworkManager networkManager = null) { }
    }
}

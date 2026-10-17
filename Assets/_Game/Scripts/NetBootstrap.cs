using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Demons
{
    [RequireComponent(typeof(NetworkManager))]
    public class NetBootstrap : MonoBehaviour
    {
        public static bool TypingCode { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => TypingCode = false;

        NetworkManager net;
        ISession session;
        string status = "", joinCode = "";
        bool busy;

        void Start()
        {
            net = GetComponent<NetworkManager>();
            if (NetworkManager.Singleton != net)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            net.OnClientStopped += wasHost => { if (session != null && !busy) _ = BackToCouch("Disconnected"); };

            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-join");
            if (i >= 0 && i + 1 < args.Length)
            {
                joinCode = args[i + 1];
                _ = GoOnline(host: false);
            }
            else StartCouch();
        }

        void StartCouch()
        {
            net.NetworkConfig.NetworkTransport = GetComponent<OfflineTransport>();
            net.StartHost();
        }

        async Task BackToCouch(string why)
        {
            status = why;
            var leaving = session;
            session = null;
            if (leaving != null)
            {
                try { await leaving.LeaveAsync(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (net.IsListening && net.NetworkConfig.NetworkTransport is OfflineTransport) return;
            if (net.IsListening) net.Shutdown();
            while (net.ShutdownInProgress) await Task.Yield();
            StartCouch();
        }

        public async Task GoOnline(bool host)
        {
            busy = true;
            try
            {
                status = "Signing in...";
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if (net.IsListening) net.Shutdown();
                while (net.ShutdownInProgress) await Task.Yield();
                net.NetworkConfig.NetworkTransport = GetComponent<UnityTransport>();

                var network = new NetworkOptions { RelayProtocol = RelayProtocol.WSS };
                if (host)
                {
                    status = "Creating session...";
                    session = await MultiplayerService.Instance.CreateSessionAsync(
                        new SessionOptions { MaxPlayers = Scoring.MaxDemons }.WithRelayNetwork().WithNetworkOptions(network));
                    status = $"Online. Join code: {session.Code}";
                }
                else
                {
                    status = "Joining...";
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                        joinCode.Trim().ToUpperInvariant(), new JoinSessionOptions().WithNetworkOptions(network));
                    status = $"Online. Joined {session.Code}";
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await BackToCouch($"Online failed: {e.Message}");
            }
            busy = false;
        }

        public static NetBootstrap Instance { get; private set; }

        public string Status => status;

        public bool CanGoOnline => session == null && !busy;

        public bool IsServer => net && net.IsServer;

        public void Host() => _ = GoOnline(host: true);

        public void BeginJoin()
        {
            joinCode = "";
            TypingCode = true;
        }

        public void Join(string code)
        {
            joinCode = code;
            TypingCode = false;
            _ = GoOnline(host: false);
        }

        public void CancelJoin() => TypingCode = false;
    }
}

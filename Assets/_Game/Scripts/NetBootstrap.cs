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

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = GameState.FontSize(0.035f) };
            var label = new GUIStyle(GUI.skin.label) { fontSize = style.fontSize, alignment = TextAnchor.MiddleRight };
            float h = style.fontSize * 1.8f, w = style.fontSize * 9;
            var right = new Rect(Screen.width - w - 12, 8, w, h);
            var left = new Rect(right.x - w - 8, 8, w, h);

            if (TypingCode)
            {
                var e = Event.current;
                bool enter = e.type == EventType.KeyDown && e.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
                GUI.SetNextControlName("join-code");
                joinCode = GUI.TextField(left, joinCode, 12, new GUIStyle(GUI.skin.textField) { fontSize = style.fontSize });
                GUI.FocusControl("join-code");
                if (enter || GUI.Button(right, "Join", style))
                {
                    TypingCode = false;
                    _ = GoOnline(host: false);
                }
                if (GUI.Button(new Rect(right.x, right.yMax + 4, w, h), "Cancel", style)) TypingCode = false;
                return;
            }

            if (session == null && !busy && !Progression.Instance)
            {
                if (GUI.Button(left, "Host online", style)) _ = GoOnline(host: true);
                if (GUI.Button(right, "Join online", style))
                {
                    joinCode = "";
                    TypingCode = true;
                }
            }
            if (!string.IsNullOrEmpty(status))
                GUI.Label(new Rect(Screen.width - 3 * w, right.yMax + 4, 3 * w - 12, h), status, label);
        }
    }
}

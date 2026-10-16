using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demons
{
    [RequireComponent(typeof(NetworkManager))]
    public class NetBootstrap : MonoBehaviour
    {
        public static bool TypingCode { get; private set; }

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
            net.OnClientStopped += _ => { if (session != null && !busy) BackToCouch("Disconnected"); };

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

        void BackToCouch(string why)
        {
            session = null;
            status = why;
            if (net.IsListening) net.Shutdown();
            StartCouch();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (busy || session != null || TypingCode || kb == null) return;
            if (kb.hKey.wasPressedThisFrame) _ = GoOnline(host: true);
            if (kb.jKey.wasPressedThisFrame)
            {
                joinCode = "";
                TypingCode = true;
            }
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
                BackToCouch($"Online failed: {e.Message}");
            }
            busy = false;
        }

        void OnGUI()
        {
            var rect = new Rect(Screen.width - 412, 8, 400, 24);
            if (TypingCode)
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    TypingCode = false;
                    _ = GoOnline(host: false);
                    e.Use();
                    return;
                }
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    TypingCode = false;
                    e.Use();
                    return;
                }
                GUI.SetNextControlName("join-code");
                joinCode = GUI.TextField(rect, joinCode, 12);
                GUI.FocusControl("join-code");
                return;
            }
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 13 };
            var hint = session == null && !busy ? "H: host online · J: join by code" : "";
            GUI.Label(rect, string.IsNullOrEmpty(status) ? hint : $"{status}   {hint}", style);
        }
    }
}

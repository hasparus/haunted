using System;
using System.Text;
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
            net.NetworkConfig.ConnectionApproval = true;
            net.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Application.version);
            net.ConnectionApprovalCallback = Approve;
            net.OnClientStopped += wasHost =>
            {
                if (session == null || busy) return;
                string reason = net.DisconnectReason;
                string title = reason == Closed ? "Room closed" : reason == Full || reason.StartsWith("Different builds") ? "Couldn't join" : "Disconnected";
                _ = BackToCouch("", string.IsNullOrEmpty(reason) ? "Lost connection to the room." : reason, title);
            };

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

        const string Closed = "The host closed the room.";
        const string Full = "The room is full: four demons already work there. CPU demons step aside for people, so ask the host to remove one.";

        static void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string theirs = Encoding.UTF8.GetString(request.Payload);
            response.Approved = theirs == Application.version;
            if (!response.Approved)
                response.Reason = $"Different builds: the host runs v{Application.version}, you run {(theirs.Length > 0 ? "v" + theirs : "an older one")}. Everyone needs the latest version from itch.io.";
            else if (request.ClientNetworkId != NetworkManager.ServerClientId && Demon.All.FindAll(d => !Bots.IsBot(d)).Count >= Scoring.MaxDemons)
            {
                response.Approved = false;
                response.Reason = Full;
            }
        }

        async Task BackToCouch(string why, string error = "", string title = "Couldn't connect")
        {
            status = why;
            Error = error;
            ErrorTitle = title;
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
                await BackToCouch("", host
                    ? "Couldn't book a room. Check your connection and try again."
                    : $"Room {joinCode.Trim().ToUpperInvariant()} isn't on the calendar. Double-check the code with your host.");
            }
            busy = false;
        }

        public static NetBootstrap Instance { get; private set; }

        public string Status => status;

        // Shown until dismissed: failed joins, version mismatches, dropped rooms.
        public string Error { get; private set; } = "";

        public string ErrorTitle { get; private set; } = "";

        public void DismissError() => Error = "";

        public bool Online => session != null;

        public string Code => session?.Code;

        public async void Leave()
        {
            if (session == null || busy) return;
            busy = true;
            if (net.IsServer)
            {
                foreach (var id in net.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId) net.DisconnectClient(id, Closed);
                for (int i = 0; i < 10; i++) await Awaitable.NextFrameAsync();
            }
            busy = false;
            await BackToCouch("");
        }

        public bool CanGoOnline => session == null && !busy;

        public bool IsServer => net && net.IsServer;

        public void Host()
        {
            Error = "";
            _ = GoOnline(host: true);
        }

        public void BeginJoin()
        {
            joinCode = "";
            TypingCode = true;
        }

        public void Join(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            joinCode = code;
            TypingCode = false;
            Error = "";
            _ = GoOnline(host: false);
        }

        public void CancelJoin() => TypingCode = false;
    }
}

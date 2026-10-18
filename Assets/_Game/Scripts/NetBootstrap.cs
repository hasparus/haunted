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
        string status = "", joinCode = "", refusal = "";
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
            net.OnClientStopped += _ =>
            {
                if (session != null && !busy) Dropped(net.DisconnectReason);
            };
            GetComponent<CouchTransport>().Lost += RoomLost;

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
            net.NetworkConfig.NetworkTransport = GetComponent<CouchTransport>();
            net.StartHost();
        }

        void Dropped(string reason)
        {
            string title = reason == Closed ? "Room closed" : reason == Full || reason.StartsWith("Different builds") ? "Couldn't join" : "Disconnected";
            _ = BackToCouch("", string.IsNullOrEmpty(reason) ? "Lost connection to the room." : reason, title);
        }

        // The host's room went away under the couch game (relay failure, or the services dropped it); the game plays on.
        void RoomLost()
        {
            if (session != null && !busy) Dropped("");
        }

        const string Closed = "The host closed the room.";
        const string Full = "The room is full: four demons already work there. Bots step aside for people, so ask the host to remove one.";

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
            if (net.IsListening && net.NetworkConfig.NetworkTransport is CouchTransport couch)
            {
                couch.CloseRoom();
                return;
            }
            if (net.IsListening) net.Shutdown();
            while (net.ShutdownInProgress) await Task.Yield();
            StartCouch();
        }

        readonly RoomHandoff handoff = new();

        // A host opens its booked room under the running couch game. A guest has to swap the couch game for the host's,
        // but only once the room exists, so everyone keeps playing through signing in and booking.
        class RoomHandoff : INetworkHandler
        {
            public async Task StartAsync(NetworkConfiguration configuration)
            {
                var net = NetworkManager.Singleton;
                if (configuration.Role != NetworkRole.Client)
                {
                    if (!net.GetComponent<CouchTransport>().OpenRoom(configuration.RelayServerData)) throw new InvalidOperationException("The room's relay didn't start.");
                    return;
                }
                if (net.IsListening) net.Shutdown();
                while (net.ShutdownInProgress) await Task.Yield();
                var transport = net.GetComponent<UnityTransport>();
                net.NetworkConfig.NetworkTransport = transport;
                transport.SetRelayServerData(configuration.RelayServerData);
                if (net.StartClient())
                    while (net.IsListening && !net.IsConnectedClient) await Task.Yield();
                if (net.IsConnectedClient) return;
                Instance.refusal = string.IsNullOrEmpty(net.DisconnectReason) ? "Couldn't reach the room. Check your connection and try again." : net.DisconnectReason;
                throw new InvalidOperationException(Instance.refusal);
            }

            public Task StopAsync()
            {
                var net = NetworkManager.Singleton;
                if (net && net.NetworkConfig.NetworkTransport is CouchTransport couch)
                {
                    couch.CloseRoom();
                    Instance.RoomLost();
                }
                else if (net && net.IsListening) net.Shutdown();
                return Task.CompletedTask;
            }
        }

        public async Task GoOnline(bool host)
        {
            busy = true;
            refusal = "";
            try
            {
                status = "Signing in...";
                // A fresh anonymous player per launch: two tabs or windows on one machine would otherwise be one player,
                // and the services refuse a player joining a room they're already in.
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile($"p{Guid.NewGuid():N}"[..30]));
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();

                var network = new NetworkOptions { RelayProtocol = RelayProtocol.WSS };
                if (host)
                {
                    status = "Creating session...";
                    session = await MultiplayerService.Instance.CreateSessionAsync(
                        new SessionOptions { MaxPlayers = Scoring.MaxDemons }.WithRelayNetwork().WithNetworkOptions(network).WithNetworkHandler(handoff));
                    status = $"Online. Join code: {session.Code}";
                }
                else
                {
                    status = "Joining...";
                    session = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                        joinCode.Trim().ToUpperInvariant(), new JoinSessionOptions().WithNetworkOptions(network).WithNetworkHandler(handoff));
                    status = $"Online. Joined {session.Code}";
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (refusal != "") await BackToCouch("", refusal, "Couldn't join");
                else await BackToCouch("", host ? "Couldn't book a room. Check your connection and try again." : JoinFailure(e, joinCode.Trim().ToUpperInvariant()));
            }
            busy = false;
            // The room can close between connecting and the booking going through.
            if (session != null && !host && !net.IsConnectedClient) Dropped(net.DisconnectReason);
        }

        static string JoinFailure(Exception e, string code) => e switch
        {
            SessionException { Error: SessionError.SessionNotFound or SessionError.InvalidParameter } =>
                $"Room {code} isn't on the calendar. Double-check the code with your host.",
            SessionException { Error: SessionError.RateLimitExceeded } => "Too many attempts. Give it a minute, then try again.",
            // ponytail: a mistyped code and a full room both come back as unknown errors, told apart only by the message
            SessionException typo when typo.Message.Contains("invalid character") =>
                $"Room {code} isn't on the calendar. Double-check the code with your host.",
            SessionException full when full.Message.Contains("full") => Full,
            SessionException => $"Couldn't get into room {code}. Ask your host to check it's still open, then try again.",
            _ => "Couldn't reach the room. Check your connection and try again.",
        };

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

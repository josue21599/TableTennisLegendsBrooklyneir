namespace Brooklyneir.Net
{
    public enum LinkState { Idle, Hosting, Connecting, Handshaking, Lobby, InMatch, Reconnecting, Ended }

    public enum EndReason { None, Finished, Void, OpponentLeft, LocalLeft, Rejected, ConnectFailed }

    /// <summary>What this phone is: protocol, app version and balance.json hash. Both phones must match.</summary>
    public struct NetIdentity
    {
        public ushort Protocol;
        public string AppVersion;
        public uint BalanceHash;
    }

    /// <summary>
    /// The connection's life above the match: hosting or connecting, the handshake (versions and
    /// session code), the lobby, the match, reconnection with the session code, and how it ended.
    /// The match itself (freezes, voided rallies) is NetMatchSession's job. Pure, unit-tested.
    /// </summary>
    public sealed class SessionStateMachine
    {
        public readonly NetRole Role;
        readonly NetIdentity _me;
        readonly NetConfig _cfg;
        double _reconnectStart;

        public LinkState State { get; private set; }
        public EndReason Reason { get; private set; }
        public RejectReason RejectedBecause { get; private set; }
        /// <summary>Identifies this match; the guest needs it to come back after a disconnection.</summary>
        public uint SessionCode { get; private set; }
        /// <summary>The last Hello the host accepted was a reconnection (send the full state again).</summary>
        public bool LastAcceptWasReconnect { get; private set; }
        bool _matchStarted;

        public SessionStateMachine(NetRole role, NetIdentity me, NetConfig cfg)
        {
            Role = role;
            _me = me;
            _cfg = cfg;
            State = LinkState.Idle;
        }

        void End(EndReason r) { State = LinkState.Ended; Reason = r; }

        // ------------------------------------------------------------ host

        /// <summary>Host: listening for a guest. sessionCode is random and never 0.</summary>
        public void StartHosting(uint sessionCode)
        {
            SessionCode = sessionCode == 0 ? 1u : sessionCode;
            State = LinkState.Hosting;
            Reason = EndReason.None;
        }

        /// <summary>
        /// Host: a guest says Hello. Returns None to accept (answer Welcome) or why it is rejected.
        /// New guests come with session code 0; a returning guest brings the code it was given.
        /// </summary>
        public RejectReason HostOnHello(HelloMsg m)
        {
            LastAcceptWasReconnect = false;
            if (m.Protocol != _me.Protocol) return RejectReason.ProtocolMismatch;
            if (m.AppVersion != _me.AppVersion) return RejectReason.AppVersionMismatch;
            if (m.BalanceHash != _me.BalanceHash) return RejectReason.BalanceMismatch;
            switch (State)
            {
                case LinkState.Hosting:
                    if (m.SessionCode != 0) return RejectReason.BadSession;
                    State = LinkState.Lobby;
                    return RejectReason.None;
                case LinkState.Reconnecting:
                    if (m.SessionCode != SessionCode) return RejectReason.BadSession;
                    State = LinkState.InMatch;
                    LastAcceptWasReconnect = true;
                    return RejectReason.None;
                default:
                    return RejectReason.Full;
            }
        }

        // ------------------------------------------------------------ guest

        public void StartConnecting()
        {
            State = LinkState.Connecting;
            Reason = EndReason.None;
        }

        /// <summary>Guest: the transport connected; send Hello (session code 0, or the code when coming back).</summary>
        public void GuestOnConnected()
        {
            if (State == LinkState.Connecting) State = LinkState.Handshaking;
        }

        public uint HelloCode { get { return _matchStarted ? SessionCode : 0u; } }

        public void GuestOnWelcome(WelcomeMsg m)
        {
            if (State != LinkState.Handshaking && State != LinkState.Reconnecting) return;
            bool back = _matchStarted && m.SessionCode == SessionCode;
            SessionCode = m.SessionCode;
            State = back ? LinkState.InMatch : LinkState.Lobby;
        }

        public void GuestOnReject(RejectMsg m)
        {
            RejectedBecause = m.Reason;
            End(EndReason.Rejected);
        }

        /// <summary>Guest: could not reach the host at all.</summary>
        public void ConnectFailed()
        {
            if (State == LinkState.Connecting || State == LinkState.Handshaking) End(EndReason.ConnectFailed);
        }

        // ------------------------------------------------------------ both

        public void MatchStarted()
        {
            if (State != LinkState.Lobby) return;
            State = LinkState.InMatch;
            _matchStarted = true;
        }

        /// <summary>
        /// The transport lost the other phone. During a match: wait for a reconnection; before it,
        /// there is nothing to resume.
        /// </summary>
        public void TransportDisconnected(double now)
        {
            if (State == LinkState.InMatch)
            {
                State = LinkState.Reconnecting;
                _reconnectStart = now;
            }
            else if (State == LinkState.Lobby || State == LinkState.Handshaking) End(EndReason.OpponentLeft);
            else if (State == LinkState.Connecting) End(EndReason.ConnectFailed);
        }

        public void Update(double now)
        {
            if (State == LinkState.Reconnecting && now - _reconnectStart > _cfg.reconnectWindowSeconds) End(EndReason.Void);
        }

        public void OnBye() { if (State != LinkState.Ended) End(EndReason.OpponentLeft); }
        public void LocalLeave() { if (State != LinkState.Ended) End(EndReason.LocalLeft); }
        public void MatchFinished() { if (State == LinkState.InMatch) End(EndReason.Finished); }
        /// <summary>The match itself declared the match void (NetMatchSession: no data within the window).</summary>
        public void MatchVoided() { if (State != LinkState.Ended) End(EndReason.Void); }

        /// <summary>How the match counts for rewards, from this phone's point of view.</summary>
        public LanOutcome Outcome
        {
            get
            {
                switch (Reason)
                {
                    case EndReason.Finished: return LanOutcome.Finished;
                    case EndReason.OpponentLeft: return _matchStarted ? LanOutcome.OpponentLeft : LanOutcome.Void;
                    case EndReason.LocalLeft: return _matchStarted ? LanOutcome.LocalLeft : LanOutcome.Void;
                    default: return LanOutcome.Void;
                }
            }
        }
    }
}

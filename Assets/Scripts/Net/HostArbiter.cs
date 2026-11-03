using Brooklyneir.Gameplay;

namespace Brooklyneir.Net
{
    public enum GuestHitDecision { Accepted, RejectedNotAllowed, RejectedPointConfirmed }

    /// <summary>
    /// The host's referee for a LAN match. The host alone decides points, but a valid hit always
    /// beats a point that is not confirmed yet: while the ball is heading to the guest, a point
    /// (second bounce, ball dead...) stays pending for GraceTicks, because the guest's HitBegin may
    /// still be on its way. A HitBegin whose contact tick is not later than the events that ended
    /// the rally rolls the referee back to the moment of contact and the hit counts. Points on the
    /// host's own side are confirmed at once (the host's hits are detected locally, without delay).
    ///
    /// Rule of thumb for ties: an event at the same tick as the contact happened before it (the
    /// ball bounces, then rises to the racket). Pure, allocation-free after construction, tested.
    /// </summary>
    public sealed class HostArbiter
    {
        struct Snap { public uint Tick; public RallyReferee State; }

        public readonly RallyReferee Referee = new RallyReferee();
        readonly RallyReferee _shotStart = new RallyReferee();
        readonly Snap[] _history;
        int _historyCount;
        readonly RallyReferee _scratch = new RallyReferee();

        public readonly int GuestPlayer;
        /// <summary>How long a point on the guest's side waits for a late hit (ticks).</summary>
        public int GraceTicks;

        public bool PointPending { get; private set; }
        public bool PointConfirmed { get; private set; }
        public uint PendingDeadline { get; private set; }
        public int Winner { get { return Referee.Winner; } }
        public PointReason Reason { get { return Referee.Reason; } }

        public HostArbiter(int guestPlayer, int graceTicks, int historySize = 8)
        {
            GuestPlayer = guestPlayer;
            GraceTicks = graceTicks < 0 ? 0 : graceTicks;
            _history = new Snap[historySize < 2 ? 2 : historySize];
            for (int i = 0; i < _history.Length; i++) _history[i].State = new RallyReferee();
        }

        public void StartRally(int server)
        {
            Referee.StartRally(server);
            PointPending = false;
            PointConfirmed = false;
            BeginShot();
        }

        void BeginShot()
        {
            _shotStart.CopyFrom(Referee);
            _historyCount = 0;
        }

        /// <summary>The host's own player hit (detected locally): applied at once.</summary>
        public bool HostHit(int player)
        {
            if (PointConfirmed || PointPending || !Referee.CanHit(player)) return false;
            Referee.RegisterHit(player);
            BeginShot();
            return true;
        }

        /// <summary>A shield saved the ball for this player (counts as its hit; decided by the host's ball).</summary>
        public bool ShieldHit(int player)
        {
            if (PointConfirmed || PointPending || !Referee.CanHit(player)) return false;
            Referee.RegisterHit(player);
            BeginShot();
            return true;
        }

        /// <summary>A pause froze the match for this many ticks: a pending point keeps its remaining grace.</summary>
        public void ShiftPending(uint ticks)
        {
            if (PointPending) PendingDeadline += ticks;
        }

        /// <summary>The rally in play does not count (reconnection): nothing pending, nothing confirmed.</summary>
        public void Abort()
        {
            Referee.Abort();
            PointPending = false;
            PointConfirmed = false;
            _historyCount = 0;
        }

        public void OnTableBounce(int side, uint tick) { Apply(0, side, false, tick); }
        public void OnNetContact(uint tick) { Apply(1, 0, false, tick); }
        public void OnBallDead(bool wideOfTable, uint tick) { Apply(2, 0, wideOfTable, tick); }

        void Apply(int kind, int side, bool wide, uint tick)
        {
            if (Referee.RallyOver) return;
            bool guestReceiving = Referee.Receiver == GuestPlayer;
            if (kind == 0) Referee.OnTableBounce(side);
            else if (kind == 1) Referee.OnNetContact();
            else Referee.OnBallDead(wide);
            Record(tick);
            if (!Referee.RallyOver) return;
            if (guestReceiving && GraceTicks > 0)
            {
                PointPending = true;
                PendingDeadline = tick + (uint)GraceTicks;
            }
            else PointConfirmed = true;
        }

        void Record(uint tick)
        {
            if (_historyCount == _history.Length)
            {
                // Keep the newest; the oldest events of a shot are folded into the shot start.
                _shotStart.CopyFrom(_history[0].State);
                for (int i = 1; i < _history.Length; i++)
                {
                    RallyReferee keep = _history[i - 1].State;
                    _history[i - 1] = _history[i];
                    _history[i].State = keep;
                }
                _historyCount--;
            }
            _history[_historyCount].Tick = tick;
            _history[_historyCount].State.CopyFrom(Referee);
            _historyCount++;
        }

        /// <summary>Rally state right after every event with tick ≤ contactTick (into _scratch).</summary>
        void StateAt(uint contactTick)
        {
            _scratch.CopyFrom(_shotStart);
            for (int i = 0; i < _historyCount; i++)
            {
                if (_history[i].Tick > contactTick) break;
                _scratch.CopyFrom(_history[i].State);
            }
        }

        /// <summary>
        /// The guest's HitBegin arrived (contact at contactTick). Accepted if the guest could hit at
        /// that moment and the point has not been confirmed; then any pending point is undone.
        /// </summary>
        public GuestHitDecision GuestHit(uint contactTick)
        {
            if (PointConfirmed) return GuestHitDecision.RejectedPointConfirmed;
            StateAt(contactTick);
            if (!_scratch.CanHit(GuestPlayer)) return GuestHitDecision.RejectedNotAllowed;
            Referee.CopyFrom(_scratch);
            Referee.RegisterHit(GuestPlayer);
            PointPending = false;
            BeginShot();
            return GuestHitDecision.Accepted;
        }

        /// <summary>Call every tick/frame: confirms a pending point when its grace has run out. True on confirmation.</summary>
        public bool Update(uint nowTick)
        {
            if (!PointPending || nowTick < PendingDeadline) return false;
            PointPending = false;
            PointConfirmed = true;
            return true;
        }
    }

    /// <summary>
    /// The guest's copy of the score: changed only by the host's PointResult, never by the guest's
    /// own physics, so the two phones can never show different scores. Pure, tested.
    /// </summary>
    public sealed class ScoreMirror
    {
        public readonly int[] Points = new int[2];
        public readonly int[] Sets = new int[2];
        public int Server { get; private set; }
        public bool MatchOver { get; private set; }
        public int MatchWinner { get; private set; }
        public bool TimeUp { get; private set; }
        public readonly bool[] MatchPoint = new bool[2];
        public float Clock { get; private set; }
        public int LastWinner { get; private set; }
        public PointReason LastReason { get; private set; }

        public ScoreMirror(int firstServer)
        {
            Server = firstServer;
            MatchWinner = -1;
            LastWinner = -1;
        }

        public void Apply(PointResultMsg m)
        {
            Points[0] = m.Points0; Points[1] = m.Points1;
            Sets[0] = m.Sets0; Sets[1] = m.Sets1;
            Server = m.NextServer;
            MatchPoint[0] = m.MatchPoint0; MatchPoint[1] = m.MatchPoint1;
            TimeUp = m.TimeUp;
            Clock = m.Clock;
            LastWinner = m.Winner;
            LastReason = m.Reason;
            if (m.MatchWon) { MatchOver = true; MatchWinner = m.Winner; }
        }

        /// <summary>Host side: the message for a point just awarded on the host's MatchScore.</summary>
        public static PointResultMsg Build(MatchScore score, ScoreEvent ev, PointReason reason, float clock)
        {
            PointResultMsg m = new PointResultMsg();
            m.Winner = (byte)ev.PointWinner;
            m.Reason = reason;
            m.Points0 = (ushort)score.Points[0]; m.Points1 = (ushort)score.Points[1];
            m.Sets0 = (byte)score.Sets[0]; m.Sets1 = (byte)score.Sets[1];
            m.NextServer = (byte)score.Server;
            m.MatchPoint0 = !score.MatchOver && score.IsMatchPoint(0);
            m.MatchPoint1 = !score.MatchOver && score.IsMatchPoint(1);
            m.SetWon = ev.SetWon;
            m.MatchWon = ev.MatchWon;
            m.TimeUp = score.TimeUp;
            m.Clock = clock;
            return m;
        }
    }
}

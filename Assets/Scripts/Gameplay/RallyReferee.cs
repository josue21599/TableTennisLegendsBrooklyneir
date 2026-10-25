namespace Brooklyneir.Gameplay
{
    public enum PointReason
    {
        None,
        DoubleBounce,   // receiver let it bounce twice
        MissedReturn,   // receiver did not reach a good ball
        OutLong,        // hitter's ball missed the table
        OutSide,
        IntoNet,        // hitter's ball hit the net and came back / fell
        OwnSideBounce   // hitter's ball bounced on its own half
    }

    /// <summary>
    /// Arcade rules for a single rally (pure logic, no Unity objects).
    /// Simplification: the serve flies directly to the receiver's half (no bounce on the server's
    /// side). After that, each shot must bounce exactly once on the receiver's half, then the
    /// receiver must return it. Net touches that still land correctly are legal (no lets).
    /// </summary>
    public class RallyReferee
    {
        public int LastHitter { get; private set; }
        public int Receiver { get { return 1 - LastHitter; } }
        public int BouncesOnReceiverSide { get; private set; }
        public bool RallyOver { get; private set; }
        public int Winner { get; private set; }
        public PointReason Reason { get; private set; }
        public int HitCount { get; private set; }
        public bool NetTouchedThisShot { get; private set; }
        public bool IsServeInFlight { get { return HitCount == 0 && !RallyOver; } }

        public RallyReferee()
        {
            RallyOver = true;
            Winner = -1;
        }

        public void StartRally(int server)
        {
            LastHitter = server;
            BouncesOnReceiverSide = 0;
            RallyOver = false;
            Winner = -1;
            Reason = PointReason.None;
            HitCount = 0;
            NetTouchedThisShot = false;
        }

        /// <summary>True when <paramref name="player"/> may legally hit the ball right now.</summary>
        public bool CanHit(int player)
        {
            return !RallyOver && player == Receiver && BouncesOnReceiverSide == 1;
        }

        public void RegisterHit(int player)
        {
            if (RallyOver) return;
            LastHitter = player;
            BouncesOnReceiverSide = 0;
            NetTouchedThisShot = false;
            HitCount++;
        }

        public void OnNetContact()
        {
            if (!RallyOver) NetTouchedThisShot = true;
        }

        public void OnTableBounce(int side)
        {
            if (RallyOver) return;
            if (side == LastHitter)
            {
                Award(Receiver, NetTouchedThisShot ? PointReason.IntoNet : PointReason.OwnSideBounce);
                return;
            }
            BouncesOnReceiverSide++;
            if (BouncesOnReceiverSide >= 2) Award(LastHitter, PointReason.DoubleBounce);
        }

        /// <summary>The ball left play (floor, out of bounds, dropped below the table edge).</summary>
        public void OnBallDead(bool wideOfTable)
        {
            if (RallyOver) return;
            if (BouncesOnReceiverSide >= 1)
                Award(LastHitter, PointReason.MissedReturn);
            else if (NetTouchedThisShot)
                Award(Receiver, PointReason.IntoNet);
            else
                Award(Receiver, wideOfTable ? PointReason.OutSide : PointReason.OutLong);
        }

        /// <summary>The receiver has bounced once: the next bounce on its side would lose the point.</summary>
        public bool NextBounceLoses(int side)
        {
            return !RallyOver && side == Receiver && BouncesOnReceiverSide >= 1;
        }

        /// <summary>Ends the rally from outside (e.g. training reset). No winner.</summary>
        public void Abort()
        {
            RallyOver = true;
            Winner = -1;
            Reason = PointReason.None;
        }

        void Award(int winner, PointReason reason)
        {
            RallyOver = true;
            Winner = winner;
            Reason = reason;
        }

        public static string Describe(PointReason r)
        {
            switch (r)
            {
                case PointReason.DoubleBounce: return "DOBLE BOTE";
                case PointReason.MissedReturn: return "SIN DEVOLUCIÓN";
                case PointReason.OutLong: return "FUERA";
                case PointReason.OutSide: return "FUERA POR EL LADO";
                case PointReason.IntoNet: return "RED";
                case PointReason.OwnSideBounce: return "BOTE EN CAMPO PROPIO";
                default: return "";
            }
        }
    }
}

using Brooklyneir.Physics;

namespace Brooklyneir.Gameplay
{
    public enum PracticeState { WaitingServe, Outgoing, Incoming, Lost }

    public enum PracticeLoss { None, OwnSide, Net, Out, NoBounce, DoubleBounce, NotReturned }

    /// <summary>What a single event meant for the practice session.</summary>
    public struct PracticeOutcome
    {
        /// <summary>A return of the player reached the wall (counts towards the streak).</summary>
        public bool Return;
        public bool Lost;
        public PracticeLoss Reason;

        public static PracticeOutcome Nothing { get { return new PracticeOutcome(); } }
        public static PracticeOutcome Loss(PracticeLoss reason) { return new PracticeOutcome { Lost = true, Reason = reason }; }
    }

    /// <summary>
    /// Rules of the solo practice wall (pure logic, unit-tested). The player is always player 1
    /// (table half 0); the wall stands beyond the other half.
    ///
    /// General rule: every ball that comes back from the wall must bounce exactly once on the
    /// player's half before the player's next stroke. Any ball that reaches the player's half
    /// without that bounce is a loss: a stroke that lands on the player's own half, a ball that
    /// comes back and passes the end line without bouncing, or one that bounces twice.
    /// Also lost: a stroke that hits the net or falls off the table before reaching the wall.
    /// The session itself never ends; there is no score and no opponent.
    /// </summary>
    public class PracticeWallReferee
    {
        public const int PlayerSide = 0;

        public PracticeState State { get; private set; }
        public PracticeLoss LastLoss { get; private set; }
        /// <summary>Bounces on the player's half since the ball came back from the wall.</summary>
        public int BouncesOnPlayerSide { get; private set; }
        bool _strokeIsReturn;

        public PracticeWallReferee()
        {
            Reset();
        }

        public void Reset()
        {
            State = PracticeState.WaitingServe;
            LastLoss = PracticeLoss.None;
            BouncesOnPlayerSide = 0;
            _strokeIsReturn = false;
        }

        /// <summary>The ball is coming back to the player.</summary>
        public bool Incoming { get { return State == PracticeState.Incoming; } }

        /// <summary>The player may hit only a ball that came back and bounced once on their half.</summary>
        public bool CanHit { get { return State == PracticeState.Incoming && BouncesOnPlayerSide == 1; } }

        public void OnServe()
        {
            State = PracticeState.Outgoing;
            LastLoss = PracticeLoss.None;
            BouncesOnPlayerSide = 0;
            _strokeIsReturn = false;
        }

        /// <summary>Returns false (and changes nothing) if the stroke is not allowed.</summary>
        public bool OnRacketHit()
        {
            if (!CanHit) return false;
            State = PracticeState.Outgoing;
            BouncesOnPlayerSide = 0;
            _strokeIsReturn = true;
            return true;
        }

        public PracticeOutcome OnContact(BallContactType type, int side)
        {
            if (State == PracticeState.Outgoing)
            {
                switch (type)
                {
                    case BallContactType.Table:
                        // A stroke must not land on the player's own half.
                        return side == PlayerSide ? Lose(PracticeLoss.OwnSide) : PracticeOutcome.Nothing;
                    case BallContactType.Net:
                        return Lose(PracticeLoss.Net);
                    case BallContactType.Floor:
                        return Lose(PracticeLoss.Out);
                    case BallContactType.Wall:
                        State = PracticeState.Incoming;
                        BouncesOnPlayerSide = 0;
                        PracticeOutcome o = new PracticeOutcome();
                        o.Return = _strokeIsReturn;
                        return o;
                    default:
                        return PracticeOutcome.Nothing; // net cord: keeps going
                }
            }
            if (State == PracticeState.Incoming)
            {
                switch (type)
                {
                    case BallContactType.Table:
                        if (side != PlayerSide) return PracticeOutcome.Nothing; // bounce on the wall side on its way back
                        BouncesOnPlayerSide++;
                        return BouncesOnPlayerSide >= 2 ? Lose(PracticeLoss.DoubleBounce) : PracticeOutcome.Nothing;
                    case BallContactType.Net:
                        return Lose(PracticeLoss.Net);
                    case BallContactType.Floor:
                        return Lose(BouncesOnPlayerSide == 0 ? PracticeLoss.NoBounce : PracticeLoss.NotReturned);
                    default:
                        return PracticeOutcome.Nothing;
                }
            }
            return PracticeOutcome.Nothing;
        }

        /// <summary>
        /// Checked every substep with the ball position. Applies the general rule to balls that
        /// pass the player's end line, and catches balls that leave the play area.
        /// </summary>
        public PracticeOutcome OnBallPosition(float x, float y, float z, float outOfBoundsX, float outOfBoundsZ)
        {
            if (State != PracticeState.Outgoing && State != PracticeState.Incoming) return PracticeOutcome.Nothing;
            if (State == PracticeState.Incoming && BouncesOnPlayerSide == 0 && z < -TableGeometry.HalfLength)
                return Lose(PracticeLoss.NoBounce);
            if (System.Math.Abs(x) > outOfBoundsX || System.Math.Abs(z) > outOfBoundsZ)
                return Lose(State == PracticeState.Incoming && BouncesOnPlayerSide == 1 ? PracticeLoss.NotReturned : PracticeLoss.Out);
            return PracticeOutcome.Nothing;
        }

        PracticeOutcome Lose(PracticeLoss reason)
        {
            State = PracticeState.Lost;
            LastLoss = reason;
            return PracticeOutcome.Loss(reason);
        }

        public static string Describe(PracticeLoss reason)
        {
            switch (reason)
            {
                case PracticeLoss.OwnSide: return "BOTÓ EN TU CAMPO";
                case PracticeLoss.Net: return "RED";
                case PracticeLoss.Out: return "FUERA";
                case PracticeLoss.NoBounce: return "SIN BOTE EN TU CAMPO";
                case PracticeLoss.DoubleBounce: return "DOBLE BOTE";
                case PracticeLoss.NotReturned: return "NO LA DEVOLVISTE";
                default: return "";
            }
        }
    }

    /// <summary>
    /// Session statistics of the practice wall (pure, no UI). There is no end condition and no
    /// rating: only the current streak, the best streak, total returns and session time.
    /// </summary>
    public class PracticeWallStats
    {
        public int CurrentStreak { get; private set; }
        public int BestStreak { get; private set; }
        public int TotalReturns { get; private set; }
        public float SessionSeconds { get; private set; }

        public void RegisterReturn()
        {
            CurrentStreak++;
            TotalReturns++;
            if (CurrentStreak > BestStreak) BestStreak = CurrentStreak;
        }

        public void RegisterLoss()
        {
            CurrentStreak = 0;
        }

        public void Tick(float dt)
        {
            if (dt > 0f) SessionSeconds += dt;
        }
    }
}

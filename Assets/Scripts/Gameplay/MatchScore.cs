namespace Brooklyneir.Gameplay
{
    public struct ScoreEvent
    {
        public int PointWinner;
        public bool SetWon;
        public bool MatchWon;
        public int SetWinner;
        public int MatchWinner;
        public bool GoldenPoint;
    }

    /// <summary>
    /// The single source of truth for the score. Serve rotates every 2 points and every point at
    /// deuce (both players at pointsToWin - 1 when win-by-two is on). The first server of each set
    /// alternates. Optional time limit: when time is up the leader wins at the end of the current
    /// point; if tied, the next point is a golden point.
    /// </summary>
    public class MatchScore
    {
        readonly int _pointsToWin;
        readonly int _setsToWin;
        readonly bool _winByTwo;
        readonly int _firstServer;

        public int[] Points { get; private set; }
        public int[] Sets { get; private set; }
        public int SetNumber { get; private set; }
        public int Server { get; private set; }
        public bool MatchOver { get; private set; }
        public int MatchWinner { get; private set; }
        public bool TimeUp { get; private set; }
        public int PointsToWin { get { return _pointsToWin; } }
        public int SetsToWin { get { return _setsToWin; } }
        int _setFirstServer;

        public MatchScore(int pointsToWin, int setsToWin, bool winByTwo, int firstServer)
        {
            _pointsToWin = pointsToWin < 1 ? 1 : pointsToWin;
            _setsToWin = setsToWin < 1 ? 1 : setsToWin;
            _winByTwo = winByTwo;
            _firstServer = firstServer == 1 ? 1 : 0;
            Reset();
        }

        public void Reset()
        {
            Points = new int[2];
            Sets = new int[2];
            SetNumber = 1;
            MatchOver = false;
            MatchWinner = -1;
            TimeUp = false;
            _setFirstServer = _firstServer;
            Server = _setFirstServer;
        }

        public bool IsDeuce
        {
            get { return _winByTwo && Points[0] >= _pointsToWin - 1 && Points[1] >= _pointsToWin - 1; }
        }

        public bool IsGamePoint(int player)
        {
            if (MatchOver) return false;
            int mine = Points[player], other = Points[1 - player];
            if (mine + 1 < _pointsToWin) return false;
            return !_winByTwo || mine + 1 - other >= 2;
        }

        public bool IsMatchPoint(int player)
        {
            return IsGamePoint(player) && Sets[player] + 1 >= _setsToWin;
        }

        /// <summary>Called by the match timer. Ends the match at the next point if someone leads.</summary>
        public void SignalTimeUp()
        {
            TimeUp = true;
        }

        public ScoreEvent AwardPoint(int winner)
        {
            ScoreEvent ev = new ScoreEvent();
            ev.PointWinner = winner;
            ev.SetWinner = -1;
            ev.MatchWinner = -1;
            if (MatchOver || winner < 0 || winner > 1) return ev;

            Points[winner]++;
            int a = Points[winner], b = Points[1 - winner];
            bool setWon = a >= _pointsToWin && (!_winByTwo || a - b >= 2);
            if (TimeUp && a != b) setWon = true; // leader wins when time is up
            ev.GoldenPoint = TimeUp && a == b;

            if (setWon)
            {
                ev.SetWon = true;
                ev.SetWinner = winner;
                Sets[winner]++;
                if (Sets[winner] >= _setsToWin || TimeUp)
                {
                    MatchOver = true;
                    MatchWinner = winner;
                    ev.MatchWon = true;
                    ev.MatchWinner = winner;
                    return ev;
                }
                SetNumber++;
                Points[0] = 0;
                Points[1] = 0;
                _setFirstServer = 1 - _setFirstServer;
                Server = _setFirstServer;
                return ev;
            }

            UpdateServer();
            return ev;
        }

        void UpdateServer()
        {
            int total = Points[0] + Points[1];
            int deuceStart = 2 * (_pointsToWin - 1);
            int switches;
            if (IsDeuce)
                switches = deuceStart / 2 + (total - deuceStart);
            else
                switches = total / 2;
            Server = (_setFirstServer + switches) % 2;
        }
    }
}

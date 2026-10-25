using System.Collections.Generic;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.SaveSystem;

namespace Brooklyneir.Progression
{
    public class RivalDefinition
    {
        public string Name;
        public string Nickname;
        public int Rating;            // 1..100, decides simulated matches
        public string RacketId;
        public string Color;
    }

    /// <summary>
    /// 8-player single-elimination bracket. Entrant 0 is the player, 1..7 are rivals. The player's
    /// matches are played for real against the AI; the other matches of each round are simulated
    /// with the saved seed, weighted by rating, so a resumed tournament gives the same results.
    /// </summary>
    public class Tournament
    {
        public const int Player = 0;
        public const int Entrants = 8;
        public static readonly string[] RoundNames = { "CUARTOS DE FINAL", "SEMIFINAL", "FINAL" };

        static List<RivalDefinition> _rivals;
        public static IList<RivalDefinition> Rivals
        {
            get
            {
                if (_rivals == null)
                {
                    _rivals = new List<RivalDefinition>();
                    _rivals.Add(null); // slot 0 = player
                    _rivals.Add(new RivalDefinition { Name = "Dex Marlowe", Nickname = "Bodega", Rating = 35, RacketId = "brooklyn_street", Color = "#F2C230" });
                    _rivals.Add(new RivalDefinition { Name = "Nia Volt", Nickname = "Chispa", Rating = 48, RacketId = "neon_pulse", Color = "#22E6FF" });
                    _rivals.Add(new RivalDefinition { Name = "Kaito Brr", Nickname = "Hielo", Rating = 55, RacketId = "iron_wall", Color = "#9AD0FF" });
                    _rivals.Add(new RivalDefinition { Name = "Rosa Vega", Nickname = "La Muralla", Rating = 62, RacketId = "iron_wall", Color = "#FF5C7A" });
                    _rivals.Add(new RivalDefinition { Name = "Jaylen Spinz", Nickname = "Remolino", Rating = 70, RacketId = "tropic_wave", Color = "#16B58A" });
                    _rivals.Add(new RivalDefinition { Name = "Mika Overdrive", Nickname = "Turbo", Rating = 80, RacketId = "cyber_blade", Color = "#9B5CFF" });
                    _rivals.Add(new RivalDefinition { Name = "Big Sal", Nickname = "El Tejado", Rating = 90, RacketId = "rooftop_viper", Color = "#7CFF3A" });
                }
                return _rivals;
            }
        }

        readonly TournamentState _s;

        public Tournament(TournamentState state)
        {
            _s = state;
        }

        public TournamentState State { get { return _s; } }
        public bool Active { get { return _s.active && !_s.finished; } }
        public int Round { get { return _s.round; } }
        public bool PlayerEliminated { get { return _s.playerEliminated; } }
        public bool PlayerIsChampion { get { return _s.finished && !_s.playerEliminated && _s.bracket.Length == 15 && _s.bracket[14] == Player; } }

        public static TournamentState CreateNew(int seed)
        {
            TournamentState s = new TournamentState();
            s.active = true;
            s.seed = seed;
            s.round = 0;
            s.playerEliminated = false;
            s.finished = false;
            s.bracket = new int[15];
            for (int i = 0; i < 15; i++) s.bracket[i] = -1;
            // Seeded shuffle of the 7 rivals; the player always starts in slot 0.
            List<int> rivals = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            GameRandom rng = new GameRandom(seed);
            for (int i = rivals.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int t = rivals[i]; rivals[i] = rivals[j]; rivals[j] = t;
            }
            s.bracket[0] = Player;
            for (int i = 0; i < 7; i++) s.bracket[i + 1] = rivals[i];
            return s;
        }

        /// <summary>First index of each round's slots in the bracket array.</summary>
        static int RoundStart(int round)
        {
            switch (round)
            {
                case 0: return 0;
                case 1: return 8;
                case 2: return 12;
                default: return 14;
            }
        }

        static int RoundSize(int round) { return 8 >> round; }

        /// <summary>The entrant the player faces this round, or -1.</summary>
        public int CurrentOpponent()
        {
            if (!Active || _s.playerEliminated) return -1;
            int start = RoundStart(_s.round);
            int size = RoundSize(_s.round);
            for (int i = 0; i < size; i++)
            {
                if (_s.bracket[start + i] == Player)
                {
                    int pair = i % 2 == 0 ? i + 1 : i - 1;
                    return _s.bracket[start + pair];
                }
            }
            return -1;
        }

        public AIDifficulty DifficultyForCurrentRound()
        {
            int opp = CurrentOpponent();
            int rating = opp > 0 ? Rivals[opp].Rating : 50;
            if (_s.round == 2 || rating >= 75) return AIDifficulty.Hard;
            if (_s.round == 1 || rating >= 50) return AIDifficulty.Normal;
            return AIDifficulty.Easy;
        }

        /// <summary>Records the player's match and simulates the rest of the round.</summary>
        public void RecordPlayerResult(bool playerWon)
        {
            if (!Active || _s.playerEliminated) return;
            int start = RoundStart(_s.round);
            int size = RoundSize(_s.round);
            int next = RoundStart(_s.round + 1);
            GameRandom rng = new GameRandom(_s.seed * 31 + _s.round * 7919 + 17);
            for (int m = 0; m < size / 2; m++)
            {
                int a = _s.bracket[start + m * 2];
                int b = _s.bracket[start + m * 2 + 1];
                int winner;
                if (a == Player || b == Player)
                    winner = playerWon ? Player : (a == Player ? b : a);
                else
                    winner = Simulate(a, b, rng);
                _s.bracket[next + m] = winner;
            }
            if (!playerWon) _s.playerEliminated = true;
            if (_s.round == 2 || _s.playerEliminated)
            {
                // Finish the remaining rounds so the bracket can be shown complete.
                int round = _s.round + 1;
                while (round <= 2)
                {
                    int rs = RoundStart(round);
                    int rn = RoundStart(round + 1);
                    for (int m = 0; m < RoundSize(round) / 2; m++)
                        _s.bracket[rn + m] = Simulate(_s.bracket[rs + m * 2], _s.bracket[rs + m * 2 + 1], rng);
                    round++;
                }
                _s.finished = true;
                _s.active = false;
                return;
            }
            _s.round++;
        }

        static int Simulate(int a, int b, GameRandom rng)
        {
            int ra = a == Player ? 60 : Rivals[a].Rating;
            int rb = b == Player ? 60 : Rivals[b].Rating;
            float pA = ra / (float)(ra + rb);
            return rng.Value() < pA ? a : b;
        }

        public static string EntrantName(int entrant)
        {
            if (entrant == Player) return "TÚ";
            if (entrant < 0 || entrant >= Rivals.Count) return "?";
            RivalDefinition r = Rivals[entrant];
            return r.Name + " \"" + r.Nickname + "\"";
        }
    }
}

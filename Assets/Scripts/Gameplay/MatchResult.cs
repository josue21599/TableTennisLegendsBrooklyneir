using System.Collections.Generic;

namespace Brooklyneir.Gameplay
{
    /// <summary>Summary of a finished match, used by the results screen and the progression.</summary>
    public class MatchResult
    {
        public MatchConfig Config;
        public int Winner;
        public int[] Sets = new int[2];
        public List<int[]> SetScores = new List<int[]>();
        public int[] Points = new int[2];
        public int[] Smashes = new int[2];
        public int[] PowersUsed = new int[2];
        public int[] Hits = new int[2];
        public int LongestRally;
        public float DurationSeconds;
        public bool Abandoned;
    }

    public class TrainingResult
    {
        public DrillDefinition Drill;
        public int Successes;
        public int Balls;
        public int Stars;
    }
}

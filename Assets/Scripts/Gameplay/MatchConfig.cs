using System;
using System.Collections.Generic;

namespace Brooklyneir.Gameplay
{
    public enum GameMode { QuickMatch, VersusCpu, Tournament, Training, Custom, Demo }
    public enum AIDifficulty { Easy = 0, Normal = 1, Hard = 2 }
    public enum PowerFrequency { Off = 0, Low = 1, Normal = 2, High = 3 }

    /// <summary>Everything needed to start a match. Validated before use.</summary>
    [Serializable]
    public class MatchConfig
    {
        public GameMode mode = GameMode.QuickMatch;
        public bool player2IsAI = false;
        public AIDifficulty aiDifficulty = AIDifficulty.Normal;
        public int pointsToWin = 7;
        public int bestOfSets = 1;
        public bool winByTwo = true;
        public float timeLimitMinutes = 0f;
        public bool powerUpsEnabled = true;
        public PowerFrequency powerFrequency = PowerFrequency.Normal;
        public string tableId = "classic";
        public string racketP1 = "classic_oak";
        public string racketP2 = "brooklyn_street";
        /// <summary>Only for tournament matches: index of the rival, for display.</summary>
        public int tournamentRival = -1;
        /// <summary>When true player 1 is also an AI (menu background demo).</summary>
        public bool player1IsAI = false;

        public static readonly int[] AllowedSets = { 1, 3, 5 };

        public MatchConfig Clone()
        {
            return (MatchConfig)MemberwiseClone();
        }

        public int SetsToWin { get { return (bestOfSets + 1) / 2; } }

        /// <summary>
        /// Fixes contradictory or out-of-range settings and returns human-readable notes about
        /// what was changed (shown in the custom match screen).
        /// </summary>
        public List<string> Validate()
        {
            List<string> notes = new List<string>();
            if (pointsToWin < 3) { pointsToWin = 3; notes.Add("Mínimo 3 puntos por set."); }
            if (pointsToWin > 21) { pointsToWin = 21; notes.Add("Máximo 21 puntos por set."); }
            if (Array.IndexOf(AllowedSets, bestOfSets) < 0)
            {
                bestOfSets = bestOfSets <= 1 ? 1 : (bestOfSets <= 3 ? 3 : 5);
                notes.Add("Los sets deben ser 1, 3 o 5.");
            }
            if (timeLimitMinutes < 0f) timeLimitMinutes = 0f;
            if (timeLimitMinutes > 15f) { timeLimitMinutes = 15f; notes.Add("Límite de tiempo máximo: 15 min."); }
            if (timeLimitMinutes > 0f && bestOfSets > 1)
            {
                bestOfSets = 1;
                notes.Add("Con límite de tiempo se juega a un solo set.");
            }
            if (!powerUpsEnabled) powerFrequency = PowerFrequency.Off;
            if (powerUpsEnabled && powerFrequency == PowerFrequency.Off) powerFrequency = PowerFrequency.Normal;
            if (mode == GameMode.VersusCpu || mode == GameMode.Tournament) player2IsAI = true;
            if (mode == GameMode.QuickMatch && player1IsAI) player1IsAI = false;
            if (string.IsNullOrEmpty(tableId)) tableId = "classic";
            if (string.IsNullOrEmpty(racketP1)) racketP1 = "classic_oak";
            if (string.IsNullOrEmpty(racketP2)) racketP2 = "brooklyn_street";
            return notes;
        }
    }
}

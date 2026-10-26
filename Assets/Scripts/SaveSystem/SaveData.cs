using System;
using System.Collections.Generic;
using Brooklyneir.Rackets;
using Brooklyneir.Tables;

namespace Brooklyneir.SaveSystem
{
    public enum QualityLevel { Low = 0, Medium = 1, High = 2 }

    [Serializable]
    public class SettingsData
    {
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.9f;
        public bool vibration = true;
        public float touchSensitivity = 1f;   // 0.5 .. 2
        public int quality = (int)QualityLevel.Medium;
        public bool reducedCameraEffects = false;
        public bool assistP1 = true;
        public bool assistP2 = true;
        public bool showLandingMarker = true;

        public void Sanitize()
        {
            musicVolume = Clamp01(musicVolume);
            sfxVolume = Clamp01(sfxVolume);
            if (float.IsNaN(touchSensitivity) || touchSensitivity < 0.5f) touchSensitivity = 0.5f;
            if (touchSensitivity > 2f) touchSensitivity = 2f;
            if (quality < 0 || quality > 2) quality = (int)QualityLevel.Medium;
        }

        static float Clamp01(float v)
        {
            if (float.IsNaN(v)) return 0.7f;
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }

    [Serializable]
    public class StatsData
    {
        public int matchesPlayed;
        public int matchesWonVsCpu;
        public int matchesLostVsCpu;
        public int localMatches;
        public int pointsWon;
        public int totalHits;
        public int longestRally;
        public int smashes;
        public int powersUsed;
        public int tournamentsWon;
        public int tournamentsPlayed;
        public int coinsEarned;
        public int trainingSessions;
        public int hardWins;
        /// <summary>Practice wall: best streak ever and returns over all sessions.</summary>
        public int practiceBestStreak;
        public int practiceTotalReturns;

        public void Sanitize()
        {
            matchesPlayed = NonNeg(matchesPlayed);
            practiceBestStreak = NonNeg(practiceBestStreak);
            practiceTotalReturns = NonNeg(practiceTotalReturns);
            matchesWonVsCpu = NonNeg(matchesWonVsCpu);
            matchesLostVsCpu = NonNeg(matchesLostVsCpu);
            localMatches = NonNeg(localMatches);
            pointsWon = NonNeg(pointsWon);
            totalHits = NonNeg(totalHits);
            longestRally = NonNeg(longestRally);
            smashes = NonNeg(smashes);
            powersUsed = NonNeg(powersUsed);
            tournamentsWon = NonNeg(tournamentsWon);
            tournamentsPlayed = NonNeg(tournamentsPlayed);
            coinsEarned = NonNeg(coinsEarned);
            trainingSessions = NonNeg(trainingSessions);
            hardWins = NonNeg(hardWins);
        }

        static int NonNeg(int v) { return v < 0 ? 0 : v; }
    }

    [Serializable]
    public class TournamentState
    {
        public bool active;
        public int seed;
        public int round;           // 0 = quarter-finals, 1 = semis, 2 = final
        /// <summary>15 slots: [0..7] first round entrants, [8..11] QF winners, [12..13] SF winners, [14] champion. -1 = unknown.</summary>
        public int[] bracket = new int[0];
        public bool playerEliminated;
        public bool finished;
    }

    [Serializable]
    public class TrainingRecord
    {
        public string drillId;
        public int bestStars;
        public int bestScore;
    }

    /// <summary>Everything that is persisted. JsonUtility-friendly (fields, lists, nested classes).</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int coins;
        public List<string> ownedRackets = new List<string>();
        public List<string> ownedTables = new List<string>();
        public string racketP1 = "classic_oak";
        public string racketP2 = "brooklyn_street";
        public string selectedTable = "classic";
        public List<string> achievements = new List<string>();
        public List<TrainingRecord> training = new List<TrainingRecord>();
        public TournamentState tournament = new TournamentState();
        public StatsData stats = new StatsData();
        public SettingsData settings = new SettingsData();
        public bool tutorialSeen;
        public bool rulesSeen;

        public static SaveData CreateNew(int startingCoins)
        {
            SaveData d = new SaveData();
            d.coins = startingCoins;
            d.Sanitize(999999);
            return d;
        }

        /// <summary>
        /// Repairs missing, unknown or out-of-range data (older saves, hand-edited or partially
        /// corrupted files). Never throws. Returns true if something had to be fixed.
        /// </summary>
        public bool Sanitize(int maxCoins)
        {
            bool fixedSomething = false;
            if (ownedRackets == null) { ownedRackets = new List<string>(); fixedSomething = true; }
            if (ownedTables == null) { ownedTables = new List<string>(); fixedSomething = true; }
            if (achievements == null) { achievements = new List<string>(); fixedSomething = true; }
            if (training == null) { training = new List<TrainingRecord>(); fixedSomething = true; }
            if (tournament == null) { tournament = new TournamentState(); fixedSomething = true; }
            if (tournament.bracket == null) { tournament.bracket = new int[0]; fixedSomething = true; }
            if (stats == null) { stats = new StatsData(); fixedSomething = true; }
            if (settings == null) { settings = new SettingsData(); fixedSomething = true; }
            if (coins < 0) { coins = 0; fixedSomething = true; }
            if (coins > maxCoins) { coins = maxCoins; fixedSomething = true; }

            fixedSomething |= RemoveUnknownAndDuplicates(ownedRackets, true);
            fixedSomething |= RemoveUnknownAndDuplicates(ownedTables, false);

            // Free content is always owned.
            IList<RacketDefinition> rackets = RacketCatalog.All;
            for (int i = 0; i < rackets.Count; i++)
                if (rackets[i].Unlock == UnlockRule.Free && !ownedRackets.Contains(rackets[i].Id)) { ownedRackets.Add(rackets[i].Id); fixedSomething = true; }
            IList<TableDefinition> tables = TableCatalog.All;
            for (int i = 0; i < tables.Count; i++)
                if (tables[i].Unlock == UnlockRule.Free && !ownedTables.Contains(tables[i].Id)) { ownedTables.Add(tables[i].Id); fixedSomething = true; }

            if (!ownedRackets.Contains(racketP1)) { racketP1 = ownedRackets[0]; fixedSomething = true; }
            if (!ownedRackets.Contains(racketP2)) { racketP2 = ownedRackets.Count > 1 ? ownedRackets[1] : ownedRackets[0]; fixedSomething = true; }
            if (!ownedTables.Contains(selectedTable)) { selectedTable = ownedTables[0]; fixedSomething = true; }

            for (int i = training.Count - 1; i >= 0; i--)
            {
                TrainingRecord r = training[i];
                if (r == null || string.IsNullOrEmpty(r.drillId)) { training.RemoveAt(i); fixedSomething = true; continue; }
                if (r.bestStars < 0) r.bestStars = 0;
                if (r.bestStars > 3) r.bestStars = 3;
                if (r.bestScore < 0) r.bestScore = 0;
            }
            for (int i = achievements.Count - 1; i >= 0; i--)
                if (string.IsNullOrEmpty(achievements[i])) { achievements.RemoveAt(i); fixedSomething = true; }

            if (tournament.active && (tournament.bracket.Length != 15 || tournament.round < 0 || tournament.round > 2))
            {
                tournament = new TournamentState();
                fixedSomething = true;
            }

            stats.Sanitize();
            settings.Sanitize();
            if (version != CurrentVersion) { version = CurrentVersion; fixedSomething = true; }
            return fixedSomething;
        }

        static bool RemoveUnknownAndDuplicates(List<string> list, bool rackets)
        {
            bool changed = false;
            HashSet<string> seen = new HashSet<string>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                string id = list[i];
                bool known = !string.IsNullOrEmpty(id) && (rackets ? RacketCatalog.Exists(id) : TableCatalog.Exists(id));
                if (!known || seen.Contains(id))
                {
                    list.RemoveAt(i);
                    changed = true;
                    continue;
                }
                seen.Add(id);
            }
            return changed;
        }

        public TrainingRecord GetTraining(string drillId)
        {
            for (int i = 0; i < training.Count; i++) if (training[i].drillId == drillId) return training[i];
            return null;
        }
    }
}

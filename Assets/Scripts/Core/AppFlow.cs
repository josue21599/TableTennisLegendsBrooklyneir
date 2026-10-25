using System;
using Brooklyneir.Audio;
using Brooklyneir.CameraSystem;
using Brooklyneir.Controls;
using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;
using Brooklyneir.Tables;
using Brooklyneir.UI;
using UnityEngine;

namespace Brooklyneir.Core
{
    public enum AppState { Boot, Title, Menu, Match, Training, Results }

    /// <summary>
    /// Application state machine: title, menus, matches, training and results. Every transition
    /// goes through here, so the match, HUD, music, camera and menus are always consistent.
    /// Handles the Android back button and external pauses.
    /// </summary>
    public class AppFlow : MonoBehaviour
    {
        public AppState State { get; private set; }
        MatchHud _hud;
        TouchInputController _input;
        bool _tournamentMatch;

        public void Init(TouchInputController input)
        {
            _input = input;
            MatchController m = Game.Match;
            m.MatchFinished += OnMatchFinished;
            m.TrainingFinished += OnTrainingFinished;
            _hud = new MatchHud();
            _hud.Build(Game.UI, Game.Match);
            _hud.SetVisible(false);
            State = AppState.Boot;
            GoToTitle();
        }

        SaveData Data { get { return Game.Save.Data; } }

        // ================================================================ menus

        public void GoToTitle()
        {
            State = AppState.Title;
            StartDemo();
            Game.UI.Replace(new TitleScreen());
        }

        public void GoToMenu()
        {
            State = AppState.Menu;
            if (Game.Match.Phase == MatchPhase.Inactive || !Game.Match.IsDemo) StartDemo();
            Game.UI.Replace(new MainMenuScreen());
        }

        void StartDemo()
        {
            _hud.SetVisible(false);
            Game.Arena.Show(TableCatalog.Get(Data.selectedTable));
            MatchConfig demo = new MatchConfig();
            demo.mode = GameMode.Demo;
            demo.player1IsAI = true;
            demo.player2IsAI = true;
            demo.powerUpsEnabled = false;
            demo.racketP1 = Data.racketP1;
            demo.racketP2 = Data.racketP2;
            demo.tableId = Data.selectedTable;
            Game.Match.StartMatch(demo);
            Game.Camera.SetMode(CameraMode.Menu);
            Game.Audio.PlayMusic(MusicTrack.Menu);
        }

        /// <summary>Updates the menu background after a table/racket change.</summary>
        public void RefreshBackground()
        {
            if (State == AppState.Menu || State == AppState.Title) StartDemo();
        }

        // ================================================================ matches

        public void StartMatch(MatchConfig config)
        {
            config.Validate();
            if (!Data.tutorialSeen)
            {
                // First match ever: explain controls, rules and powers once.
                Data.tutorialSeen = true;
                Data.rulesSeen = true;
                Game.Save.MarkDirty();
                Game.UI.Push(new TutorialScreen(() => StartMatch(config)));
                return;
            }
            _tournamentMatch = config.mode == GameMode.Tournament;
            State = AppState.Match;
            Game.UI.Clear();
            Game.Arena.Show(TableCatalog.Get(config.tableId));
            Game.Audio.PlayMusic(MusicTrack.Match);
            Game.Match.StartMatch(config);
            _hud.Bind();
            _hud.SetVisible(true);
        }

        public void StartTournamentMatch()
        {
            Tournament t = new Tournament(Data.tournament);
            if (!t.Active) return;
            int opponent = t.CurrentOpponent();
            if (opponent <= 0) return;
            RivalDefinition rival = Tournament.Rivals[opponent];
            MatchConfig c = new MatchConfig();
            c.mode = GameMode.Tournament;
            c.player2IsAI = true;
            c.aiDifficulty = t.DifficultyForCurrentRound();
            c.pointsToWin = t.Round == 2 ? 11 : 7;
            c.bestOfSets = t.Round == 2 ? 3 : 1;
            c.winByTwo = true;
            c.powerUpsEnabled = true;
            c.powerFrequency = PowerFrequency.Normal;
            c.racketP1 = Data.racketP1;
            c.racketP2 = rival.RacketId;
            c.tableId = t.Round == 2 ? (Game.Economy.OwnsTable("legend") ? "legend" : "classic") : Data.selectedTable;
            c.tournamentRival = opponent;
            StartMatch(c);
        }

        public void StartTraining(DrillDefinition drill)
        {
            if (!Data.tutorialSeen)
            {
                Data.tutorialSeen = true;
                Data.rulesSeen = true;
                Game.Save.MarkDirty();
                Game.UI.Push(new TutorialScreen(() => StartTraining(drill)));
                return;
            }
            State = AppState.Training;
            Game.UI.Clear();
            Game.Arena.Show(TableCatalog.Get(Data.selectedTable));
            Game.Audio.PlayMusic(MusicTrack.Match);
            Game.Match.StartTraining(drill, Data.racketP1);
            _hud.Bind();
            _hud.SetVisible(true);
        }

        public void Pause()
        {
            if (State != AppState.Match && State != AppState.Training) return;
            if (Game.Match.Paused) return;
            Game.Match.SetPaused(true);
            Game.UI.Push(new PauseScreen());
        }

        public void Resume()
        {
            Game.UI.Clear();
            Game.Match.SetPaused(false);
        }

        /// <summary>Called when Android sends the app to the background mid-match.</summary>
        public void OnMatchPausedExternally()
        {
            if ((State == AppState.Match || State == AppState.Training) && !(Game.UI.Top is PauseScreen))
                Game.UI.Push(new PauseScreen());
            Game.Save.SaveIfDirty();
        }

        public void RestartMatch()
        {
            Game.UI.Clear();
            Game.Audio.PlayMusic(MusicTrack.Match);
            Game.Match.SetPaused(false);
            Game.Match.Restart();
            _hud.Bind();
            _hud.SetVisible(true);
            State = Game.Match.IsTraining ? AppState.Training : AppState.Match;
        }

        public void QuitToMenu()
        {
            if (_tournamentMatch && State == AppState.Match)
            {
                // Leaving a tournament match counts as a loss, as announced in the dialog.
                Tournament t = new Tournament(Data.tournament);
                if (t.Active) t.RecordPlayerResult(false);
                Game.Save.Save();
            }
            _tournamentMatch = false;
            Game.Match.Abort();
            Game.UI.Clear();
            GoToMenu();
        }

        void OnMatchFinished(MatchResult result)
        {
            if (Game.Match.IsDemo)
            {
                StartDemo();
                return;
            }
            State = AppState.Results;
            RewardSummary rewards = Rewards.ForMatch(result, Data, Game.Economy);
            Game.Save.Save();
            _hud.SetVisible(false);
            Game.Audio.PlayMusic(MusicTrack.Menu);
            Game.UI.Replace(new ResultsScreen(result, rewards, _tournamentMatch));
            if (rewards.Unlocks.Count > 0) Game.Audio.Play(Sfx.Unlock, 1f);
        }

        void OnTrainingFinished(TrainingResult result)
        {
            State = AppState.Results;
            RewardSummary rewards = Rewards.ForTraining(result, Data, Game.Economy);
            Game.Save.Save();
            _hud.SetVisible(false);
            Game.UI.Replace(new TrainingResultsScreen(result, rewards));
        }

        // ================================================================ back button

        void Update()
        {
            _hud.Tick(Time.unscaledDeltaTime);
            if (!BackButton.PressedThisFrame()) return;
            if (Game.UI.HandleBack()) return;
            switch (State)
            {
                case AppState.Match:
                case AppState.Training:
                    Pause();
                    break;
                case AppState.Menu:
                    Game.UI.Confirm("¿SALIR?", "¿Quieres cerrar Table Tennis Legends Brooklyneir?", "SALIR", () =>
                    {
                        Game.Save.Save();
                        Application.Quit();
                    });
                    break;
                case AppState.Title:
                    GoToMenu();
                    break;
                default:
                    GoToMenu();
                    break;
            }
        }
    }
}

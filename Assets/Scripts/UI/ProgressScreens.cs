using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Progression;
using Brooklyneir.SaveSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>Player profile: statistics and local achievements with progress.</summary>
    public class ProfileScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Header("PERFIL Y PROGRESO");
            SaveData d = Game.Save.Data;
            StatsData s = d.stats;

            Image statsBg = UIKit.Panel(Root, new Color(0.06f, 0.08f, 0.14f, 0.88f), 30, false, "Stats");
            UIKit.Region((RectTransform)statsBg.transform, 0.03f, 0.04f, 0.4f, 0.84f);
            RectTransform col = UIKit.Node(statsBg.transform, "Col");
            UIKit.Stretch(col, 30f, 30f, 30f, 30f);
            UIKit.Column(col, 6f);
            Stat(col, "Partidos jugados", s.matchesPlayed.ToString());
            Stat(col, "Victorias vs máquina", s.matchesWonVsCpu + " / " + (s.matchesWonVsCpu + s.matchesLostVsCpu));
            Stat(col, "Victorias en difícil", s.hardWins.ToString());
            Stat(col, "Partidos de 2 jugadores", s.localMatches.ToString());
            Stat(col, "Torneos ganados", s.tournamentsWon.ToString());
            Stat(col, "Peloteo más largo", s.longestRally + " golpes");
            Stat(col, "Remates", s.smashes.ToString());
            Stat(col, "Poderes usados", s.powersUsed.ToString());
            Stat(col, "Estrellas de entrenamiento", Achievements.TotalStars(d) + " / 15");
            Stat(col, "Raquetas", d.ownedRackets.Count + " / " + Brooklyneir.Rackets.RacketCatalog.All.Count);
            Stat(col, "Mesas", d.ownedTables.Count + " / " + Brooklyneir.Tables.TableCatalog.All.Count);
            Stat(col, "Monedas ganadas", s.coinsEarned.ToString("N0"));

            RectTransform area = UIKit.Node(Root, "Achievements");
            UIKit.Region(area, 0.42f, 0.04f, 0.97f, 0.84f);
            RectTransform content;
            ScrollRect sr = UIKit.Scroll(area, out content);
            UIKit.Stretch((RectTransform)sr.transform);
            foreach (AchievementDefinition a in Achievements.All)
            {
                bool done = d.achievements.Contains(a.Id);
                Image card = UIKit.Panel(content, done ? new Color(0.12f, 0.22f, 0.16f, 0.92f) : new Color(0.07f, 0.09f, 0.16f, 0.88f), 26, false, "Ach");
                RectTransform rt = UIKit.Height((RectTransform)card.transform, 118f);
                Image ic = UIKit.Icon(rt, done ? IconId.Trophy : IconId.Lock, done ? Theme.Gold : Theme.TextDim);
                UIKit.At((RectTransform)ic.transform, 0f, 0.5f, 24f, 0f, 70f, 70f);
                Text t = UIKit.Heading(rt, a.Title.ToUpperInvariant(), 34, done ? Theme.Gold : Theme.Text, TextAnchor.LowerLeft);
                UIKit.Region((RectTransform)t.transform, 0.14f, 0.5f, 0.75f, 0.94f);
                Text desc = UIKit.Label(rt, a.Description, 24, Theme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
                UIKit.Region((RectTransform)desc.transform, 0.14f, 0.1f, 0.6f, 0.5f);
                float prog = 0f;
                try { prog = a.Progress(d); } catch (System.Exception) { prog = 0f; }
                Image bar = UIKit.Bar(rt, done ? 1f : prog, done ? Theme.Success : Theme.Orange);
                UIKit.Region((RectTransform)bar.transform.parent, 0.62f, 0.22f, 0.8f, 0.36f);
                Text r = UIKit.Label(rt, "+" + a.Reward, 30, Theme.Gold, TextAnchor.MiddleRight);
                UIKit.Region((RectTransform)r.transform, 0.8f, 0f, 0.95f, 1f);
            }
        }

        static void Stat(RectTransform parent, string label, string value)
        {
            RectTransform row = UIKit.Height(UIKit.Node(parent, "Row"), 56f);
            Text l = UIKit.Label(row, label, 28, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Region((RectTransform)l.transform, 0f, 0f, 0.7f, 1f);
            Text v = UIKit.Label(row, value, 30, Theme.Text, TextAnchor.MiddleRight);
            UIKit.Region((RectTransform)v.transform, 0.6f, 0f, 1f, 1f);
        }
    }

    /// <summary>Audio, vibration, touch sensitivity, graphics quality, camera and assist options.</summary>
    public class SettingsScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Header("AJUSTES", true, false);
            SettingsData s = Game.Save.Data.settings;
            RectTransform left = UIKit.Node(Root, "Left");
            UIKit.Region(left, 0.04f, 0.06f, 0.48f, 0.84f);
            UIKit.Column(left, 16f);
            UIKit.Slider(UIKit.Height(UIKit.Node(left, "Music"), 86f), "Música", s.musicVolume, v => { s.musicVolume = v; Apply(); });
            UIKit.Slider(UIKit.Height(UIKit.Node(left, "Sfx"), 86f), "Efectos", s.sfxVolume, v => { s.sfxVolume = v; Apply(); });
            UIKit.Slider(UIKit.Height(UIKit.Node(left, "Sens"), 86f), "Sensibilidad", Mathf.InverseLerp(0.5f, 2f, s.touchSensitivity),
                v => { s.touchSensitivity = Mathf.Lerp(0.5f, 2f, v); Apply(); }, v => Mathf.Lerp(0.5f, 2f, v).ToString("0.0") + "x");
            UIKit.Toggle(UIKit.Height(UIKit.Node(left, "Vibration"), 80f), "Vibración", s.vibration, v =>
            {
                s.vibration = v;
                Apply();
                if (v && Game.Haptics != null) Game.Haptics.Pulse(0.8f);
            });
            UIKit.Segmented(UIKit.Height(UIKit.Node(left, "Quality"), 80f), "Gráficos", new[] { "BAJO", "MEDIO", "ALTO" }, s.quality, i => { s.quality = i; Apply(); });

            RectTransform right = UIKit.Node(Root, "Right");
            UIKit.Region(right, 0.52f, 0.06f, 0.96f, 0.84f);
            UIKit.Column(right, 16f);
            UIKit.Toggle(UIKit.Height(UIKit.Node(right, "Camera"), 80f), "Reducir efectos de cámara", s.reducedCameraEffects, v => { s.reducedCameraEffects = v; Apply(); });
            UIKit.Toggle(UIKit.Height(UIKit.Node(right, "Marker"), 80f), "Indicador de bote", s.showLandingMarker, v => { s.showLandingMarker = v; Apply(); });
            UIKit.Toggle(UIKit.Height(UIKit.Node(right, "AssistP1"), 80f), "Ayuda para principiantes (J1)", s.assistP1, v => { s.assistP1 = v; Apply(); });
            UIKit.Toggle(UIKit.Height(UIKit.Node(right, "AssistP2"), 80f), "Ayuda para principiantes (J2)", s.assistP2, v => { s.assistP2 = v; Apply(); });
            Button tut = UIKit.Button(right, "VER TUTORIAL", IconId.Info, ButtonStyle.Secondary, () => UI.Push(new TutorialScreen(null)), 32);
            UIKit.Height((RectTransform)tut.transform, 90f);
            Button reset = UIKit.Button(right, "BORRAR PROGRESO", IconId.Restart, ButtonStyle.Danger, () =>
                UI.Confirm("BORRAR PROGRESO", "Se perderán monedas, desbloqueos, logros y torneo. Los ajustes se mantienen.", "BORRAR", () =>
                {
                    Game.Save.ResetProgress();
                    Game.Economy = new Economy(Game.Save.Data, Game.Balance.economy);
                    UI.Toast("Progreso borrado", Theme.Danger);
                    Game.Flow.RefreshBackground();
                }), 32);
            UIKit.Height((RectTransform)reset.transform, 90f);
            Text fps = UIKit.Label(Root, "Objetivo: 60 FPS (según el móvil). El modo BAJO reduce resolución, sombras y público.", 24, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            UIKit.At((RectTransform)fps.transform, 0f, 0f, 70f, 10f, 1500f, 60f);
        }

        static void Apply()
        {
            Game.Save.Data.settings.Sanitize();
            Game.Save.MarkDirty();
            GameRoot.ApplySettings();
        }

        public override void OnHide()
        {
            Game.Save.SaveIfDirty();
        }
    }

    /// <summary>Tournament bracket: start, play the next round, see results.</summary>
    public class TournamentScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Header("TORNEO BROOKLYNEIR");
            SaveData d = Game.Save.Data;
            TournamentState st = d.tournament;
            bool hasBracket = st.bracket != null && st.bracket.Length == 15;
            Tournament t = new Tournament(st);

            if (!hasBracket || (!st.active && !st.finished))
            {
                Text intro = UIKit.Label(Root, "8 jugadores, eliminación directa: cuartos y semifinal a 7 puntos, final al mejor de 3 sets a 11.\n" +
                    "Cada ronda ganada da monedas. El campeón gana " + Game.Balance.economy.tournamentChampion + " monedas y desbloquea la Arena de Leyendas.",
                    32, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Normal);
                UIKit.Region((RectTransform)intro.transform, 0.1f, 0.45f, 0.9f, 0.75f);
                Image trophy = UIKit.Icon(Root, IconId.Trophy, Theme.Gold);
                UIKit.At((RectTransform)trophy.transform, 0.5f, 0.5f, 0f, -120f, 200f, 200f);
                Button start = UIKit.Button(Root, "EMPEZAR TORNEO", IconId.Play, ButtonStyle.Primary, StartNew, 44);
                UIKit.At((RectTransform)start.transform, 0.5f, 0f, 0f, 50f, 640f, 120f);
                return;
            }

            DrawBracket(st);

            if (t.Active)
            {
                int opp = t.CurrentOpponent();
                RivalDefinition rival = opp > 0 ? Tournament.Rivals[opp] : null;
                string label = "JUGAR " + Tournament.RoundNames[t.Round];
                Button play = UIKit.Button(Root, label, IconId.Play, ButtonStyle.Primary, () => Game.Flow.StartTournamentMatch(), 40);
                UIKit.At((RectTransform)play.transform, 1f, 0f, -50f, 30f, 680f, 120f);
                if (rival != null)
                {
                    Text vs = UIKit.Label(Root, "Rival: " + rival.Name + " \"" + rival.Nickname + "\"  ·  Dificultad " + DifficultyName(t.DifficultyForCurrentRound()),
                        30, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
                    UIKit.At((RectTransform)vs.transform, 0f, 0f, 60f, 60f, 1100f, 60f);
                }
                Button quit = UIKit.Button(Root, "ABANDONAR", IconId.Cross, ButtonStyle.Ghost, () =>
                    UI.Confirm("ABANDONAR TORNEO", "Perderás el progreso de este torneo.", "ABANDONAR", () =>
                    {
                        Game.Save.Data.tournament = new TournamentState();
                        Game.Save.Save();
                        UI.Pop();
                        UI.Push(new TournamentScreen());
                    }), 28);
                UIKit.At((RectTransform)quit.transform, 1f, 1f, -340f, -40f, 300f, 84f);
            }
            else
            {
                bool champ = t.PlayerIsChampion;
                Text res = UIKit.Heading(Root, champ ? "¡ERES EL CAMPEÓN!" : "ELIMINADO. CAMPEÓN: " + Tournament.EntrantName(st.bracket[14]).ToUpperInvariant(),
                    44, champ ? Theme.Gold : Theme.Text, TextAnchor.MiddleLeft);
                UIKit.At((RectTransform)res.transform, 0f, 0f, 60f, 50f, 1200f, 100f);
                Button again = UIKit.Button(Root, "NUEVO TORNEO", IconId.Restart, ButtonStyle.Primary, StartNew, 40);
                UIKit.At((RectTransform)again.transform, 1f, 0f, -50f, 30f, 560f, 120f);
            }
        }

        static string DifficultyName(AIDifficulty d)
        {
            return d == AIDifficulty.Easy ? "fácil" : (d == AIDifficulty.Hard ? "difícil" : "normal");
        }

        void StartNew()
        {
            Game.Save.Data.tournament = Tournament.CreateNew(System.Environment.TickCount & 0x7FFFFFFF);
            Game.Save.Data.stats.tournamentsPlayed++;
            Game.Save.Save();
            UI.Pop();
            UI.Push(new TournamentScreen());
        }

        void DrawBracket(TournamentState st)
        {
            RectTransform area = UIKit.Node(Root, "Bracket");
            UIKit.Region(area, 0.03f, 0.2f, 0.97f, 0.84f);
            int[] starts = { 0, 8, 12, 14 };
            int[] sizes = { 8, 4, 2, 1 };
            string[] titles = { "CUARTOS", "SEMIFINAL", "FINAL", "CAMPEÓN" };
            for (int r = 0; r < 4; r++)
            {
                float x0 = r * 0.25f, x1 = x0 + 0.23f;
                Text h = UIKit.Label(area, titles[r], 26, Theme.TextDim);
                UIKit.Region((RectTransform)h.transform, x0, 0.93f, x1, 1f);
                for (int i = 0; i < sizes[r]; i++)
                {
                    int entrant = st.bracket[starts[r] + i];
                    float slot = 0.9f / sizes[r];
                    float yc = 0.9f - slot * (i + 0.5f);
                    bool isPlayer = entrant == Tournament.Player;
                    bool eliminatedHere = r < 3 && entrant >= 0 && st.bracket[starts[r + 1] + i / 2] >= 0 && st.bracket[starts[r + 1] + i / 2] != entrant;
                    Color bg = isPlayer ? new Color(1f, 0.48f, 0.1f, 0.9f) : new Color(0.08f, 0.1f, 0.18f, 0.9f);
                    if (r == 3 && entrant >= 0) bg = isPlayer ? Theme.Gold : new Color(0.25f, 0.2f, 0.08f, 0.95f);
                    Image box = UIKit.Panel(area, bg, 18, false, "Slot");
                    UIKit.Region((RectTransform)box.transform, x0, yc - Mathf.Min(0.05f, slot * 0.4f), x1, yc + Mathf.Min(0.05f, slot * 0.4f));
                    string name = entrant < 0 ? "—" : (isPlayer ? "TÚ" : Tournament.Rivals[entrant].Name.ToUpperInvariant());
                    Text n = UIKit.Label(box.transform, name, 26, eliminatedHere ? new Color(1f, 1f, 1f, 0.35f) : Theme.Text);
                    UIKit.Stretch((RectTransform)n.transform, 10f, 10f, 0f, 0f);
                }
            }
        }
    }

    /// <summary>Training drills with best stars.</summary>
    public class TrainingSelectScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Header("ENTRENAMIENTO");
            RectTransform area = UIKit.Node(Root, "Drills");
            UIKit.Region(area, 0.03f, 0.04f, 0.97f, 0.84f);
            RectTransform content;
            ScrollRect sr = UIKit.Scroll(area, out content);
            UIKit.Stretch((RectTransform)sr.transform);
            {
                Button wall = UIKit.Button(content, "", null, ButtonStyle.Dark, () => Game.Flow.StartPracticeWall());
                RectTransform wr = UIKit.Height((RectTransform)wall.transform, 150f);
                Image wic = UIKit.Icon(wr, IconId.Ball, Theme.Cyan);
                UIKit.At((RectTransform)wic.transform, 0f, 0.5f, 30f, 0f, 90f, 90f);
                Text wt = UIKit.Heading(wr, "PARED DE PRÁCTICA", 40, Theme.Text, TextAnchor.LowerLeft);
                UIKit.Region((RectTransform)wt.transform, 0.1f, 0.5f, 0.7f, 0.92f);
                Text wd = UIKit.Label(wr, "Saca y devuelve la pelota contra la pared todas las veces que puedas. Sin rival, sin límite: sal cuando quieras.", 24, Theme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
                UIKit.Region((RectTransform)wd.transform, 0.1f, 0.05f, 0.75f, 0.5f);
                Text wb = UIKit.Label(wr, "Mejor racha: " + Game.Save.Data.stats.practiceBestStreak, 28, Theme.Gold, TextAnchor.MiddleRight);
                UIKit.Region((RectTransform)wb.transform, 0.72f, 0.2f, 0.97f, 0.8f);
            }
            foreach (DrillDefinition d in TrainingDrills.All)
            {
                DrillDefinition drill = d;
                TrainingRecord rec = Game.Save.Data.GetTraining(d.Id);
                int stars = rec != null ? rec.bestStars : 0;
                Button b = UIKit.Button(content, "", null, ButtonStyle.Dark, () => Game.Flow.StartTraining(drill));
                RectTransform rt = UIKit.Height((RectTransform)b.transform, 150f);
                Image ic = UIKit.Icon(rt, d.Kind == DrillKind.Precision ? IconId.Target : d.Kind == DrillKind.Spin ? IconId.Curve :
                    d.Kind == DrillKind.Smash ? IconId.Smash : d.Kind == DrillKind.Control ? IconId.ArrowRight : IconId.Ball, Theme.Success);
                UIKit.At((RectTransform)ic.transform, 0f, 0.5f, 30f, 0f, 90f, 90f);
                Text t = UIKit.Heading(rt, d.Title.ToUpperInvariant(), 40, Theme.Text, TextAnchor.LowerLeft);
                UIKit.Region((RectTransform)t.transform, 0.1f, 0.5f, 0.7f, 0.92f);
                Text desc = UIKit.Label(rt, d.Instructions, 24, Theme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
                UIKit.Region((RectTransform)desc.transform, 0.1f, 0.05f, 0.75f, 0.5f);
                for (int s = 0; s < 3; s++)
                {
                    Image star = UIKit.Icon(rt, s < stars ? IconId.Star : IconId.StarEmpty, s < stars ? Theme.Gold : Theme.TextDim);
                    UIKit.At((RectTransform)star.transform, 1f, 0.5f, -40f - (2 - s) * 70f, 0f, 64f, 64f);
                }
            }
        }
    }
}

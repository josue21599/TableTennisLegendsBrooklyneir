using System;
using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.PowerUps;
using Brooklyneir.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>Pause menu (also shown automatically when Android sends the app to the background).</summary>
    public class PauseScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Text t = UIKit.Heading(Root, "PAUSA", 110, Theme.Text);
            UIKit.At((RectTransform)t.transform, 0.5f, 1f, 0f, -80f, 900f, 160f);
            RectTransform col = UIKit.Node(Root, "Buttons");
            UIKit.At(col, 0.5f, 0.5f, 0f, -40f, 620f, 560f);
            UIKit.Column(col, 22f);
            UIKit.Height((RectTransform)UIKit.Button(col, "REANUDAR", IconId.Play, ButtonStyle.Primary, () => Game.Flow.Resume(), 42).transform, 120f);
            UIKit.Height((RectTransform)UIKit.Button(col, "REINICIAR", IconId.Restart, ButtonStyle.Secondary, () =>
                UI.Confirm("REINICIAR", "¿Empezar de nuevo con la misma configuración?", "REINICIAR", () => Game.Flow.RestartMatch()), 36).transform, 110f);
            UIKit.Height((RectTransform)UIKit.Button(col, "AJUSTES", IconId.Gear, ButtonStyle.Secondary, () => UI.Push(new SettingsScreen()), 36).transform, 110f);
            UIKit.Height((RectTransform)UIKit.Button(col, "SALIR AL MENÚ", IconId.Home, ButtonStyle.Danger, () =>
            {
                bool tournament = Game.Match.Config != null && Game.Match.Config.mode == GameMode.Tournament;
                UI.Confirm("SALIR", tournament ? "Si abandonas, este partido del torneo cuenta como derrota." : "Se perderá el partido en curso.", "SALIR", () => Game.Flow.QuitToMenu());
            }, 36).transform, 110f);
        }

        public override bool OnBack()
        {
            Game.Flow.Resume();
            return true;
        }
    }

    /// <summary>End of match: winner, set scores, stats, coins earned, achievements.</summary>
    public class ResultsScreen : UIScreen
    {
        readonly MatchResult _r;
        readonly RewardSummary _rewards;
        readonly bool _tournament;
        float _t;
        Text _coins;
        int _shownCoins;

        public ResultsScreen(MatchResult r, RewardSummary rewards, bool tournament)
        {
            _r = r;
            _rewards = rewards;
            _tournament = tournament;
        }

        protected override void OnBuild()
        {
            _shownCoins = 0;
            _t = 0f;
            bool vsCpu = _r.Config.player2IsAI;
            int w = _r.Winner;
            Color wc = w >= 0 ? MatchController.PlayerColors[w] : Theme.Text;
            string title;
            if (vsCpu) title = w == 0 ? "¡VICTORIA!" : "DERROTA";
            else title = "¡GANA EL JUGADOR " + (w + 1) + "!";
            Text h = UIKit.Heading(Root, title, 110, vsCpu && w != 0 ? Theme.TextDim : wc);
            UIKit.At((RectTransform)h.transform, 0.5f, 1f, 0f, -40f, 1600f, 160f);

            // Score.
            Image card = UIKit.Panel(Root, new Color(0.06f, 0.08f, 0.14f, 0.9f), 30, false, "Card");
            UIKit.Region((RectTransform)card.transform, 0.05f, 0.2f, 0.52f, 0.8f);
            Text big = UIKit.Heading(card.transform, _r.Config.bestOfSets > 1 ? _r.Sets[0] + " - " + _r.Sets[1] : PointsLine(), 120, Theme.Text);
            UIKit.Region((RectTransform)big.transform, 0f, 0.6f, 1f, 0.95f);
            string sets = "";
            for (int i = 0; i < _r.SetScores.Count; i++) sets += (i > 0 ? "    " : "") + _r.SetScores[i][0] + "-" + _r.SetScores[i][1];
            Text setLine = UIKit.Label(card.transform, sets, 32, Theme.TextDim);
            UIKit.Region((RectTransform)setLine.transform, 0f, 0.5f, 1f, 0.6f);
            string p2 = vsCpu ? "CPU" : "J2";
            string[] rows =
            {
                "Golpes: " + _r.Hits[0] + "  /  " + _r.Hits[1],
                "Remates: " + _r.Smashes[0] + "  /  " + _r.Smashes[1],
                "Poderes: " + _r.PowersUsed[0] + "  /  " + _r.PowersUsed[1],
                "Peloteo más largo: " + _r.LongestRally,
                "Duración: " + Mathf.FloorToInt(_r.DurationSeconds / 60f) + ":" + Mathf.FloorToInt(_r.DurationSeconds % 60f).ToString("00")
            };
            Text legend = UIKit.Label(card.transform, (vsCpu ? "TÚ" : "J1") + " / " + p2, 24, Theme.TextDim);
            UIKit.Region((RectTransform)legend.transform, 0f, 0.43f, 1f, 0.5f);
            for (int i = 0; i < rows.Length; i++)
            {
                Text row = UIKit.Label(card.transform, rows[i], 30, Theme.Text);
                UIKit.Region((RectTransform)row.transform, 0f, 0.35f - i * 0.075f, 1f, 0.42f - i * 0.075f);
            }

            // Rewards.
            Image rcard = UIKit.Panel(Root, new Color(0.06f, 0.08f, 0.14f, 0.9f), 30, false, "Rewards");
            UIKit.Region((RectTransform)rcard.transform, 0.55f, 0.2f, 0.95f, 0.8f);
            Image coin = UIKit.Icon(rcard.transform, IconId.Coin, Theme.Gold);
            UIKit.At((RectTransform)coin.transform, 0.5f, 1f, -150f, -50f, 100f, 100f);
            _coins = UIKit.Heading(rcard.transform, "+0", 90, Theme.Gold, TextAnchor.MiddleLeft);
            UIKit.At((RectTransform)_coins.transform, 0.5f, 1f, 60f, -40f, 360f, 120f);
            string lines = string.Join("\n", _rewards.Lines.ToArray());
            for (int i = 0; i < _rewards.Unlocks.Count; i++) lines += "\n¡DESBLOQUEADO! " + _rewards.Unlocks[i];
            Text lt = UIKit.Label(rcard.transform, lines, 30, Theme.Text, TextAnchor.UpperCenter, FontStyle.Normal);
            UIKit.Region((RectTransform)lt.transform, 0.05f, 0.05f, 0.95f, 0.72f);

            // Buttons.
            if (_tournament)
            {
                Button cont = UIKit.Button(Root, "CONTINUAR", IconId.Bracket, ButtonStyle.Primary, () =>
                {
                    Game.Flow.GoToMenu();
                    UI.Push(new TournamentScreen());
                }, 42);
                UIKit.At((RectTransform)cont.transform, 1f, 0f, -60f, 40f, 520f, 120f);
            }
            else
            {
                Button again = UIKit.Button(Root, "REVANCHA", IconId.Restart, ButtonStyle.Primary, () => Game.Flow.RestartMatch(), 42);
                UIKit.At((RectTransform)again.transform, 1f, 0f, -60f, 40f, 460f, 120f);
            }
            Button menu = UIKit.Button(Root, "MENÚ", IconId.Home, ButtonStyle.Secondary, () => Game.Flow.GoToMenu(), 40);
            UIKit.At((RectTransform)menu.transform, 1f, 0f, -560f, 40f, 340f, 120f);
        }

        string PointsLine()
        {
            if (_r.SetScores.Count > 0) return _r.SetScores[_r.SetScores.Count - 1][0] + " - " + _r.SetScores[_r.SetScores.Count - 1][1];
            return _r.Points[0] + " - " + _r.Points[1];
        }

        public override void Tick(float dt)
        {
            _t += dt;
            if (_shownCoins < _rewards.Coins && _t > 0.4f)
            {
                int step = Mathf.Max(1, _rewards.Coins / 40);
                _shownCoins = Mathf.Min(_rewards.Coins, _shownCoins + step);
                _coins.text = "+" + _shownCoins;
                if (_shownCoins % (step * 4) == 0 || _shownCoins == _rewards.Coins) Game.Audio.Play(Sfx.Coin, 0.4f);
            }
        }

        public override bool OnBack()
        {
            Game.Flow.GoToMenu();
            return true;
        }
    }

    public class TrainingResultsScreen : UIScreen
    {
        readonly TrainingResult _r;
        readonly RewardSummary _rewards;

        public TrainingResultsScreen(TrainingResult r, RewardSummary rewards)
        {
            _r = r;
            _rewards = rewards;
        }

        protected override void OnBuild()
        {
            Text h = UIKit.Heading(Root, _r.Drill.Title.ToUpperInvariant(), 72, Theme.Text);
            UIKit.At((RectTransform)h.transform, 0.5f, 1f, 0f, -50f, 1600f, 120f);
            for (int s = 0; s < 3; s++)
            {
                Image star = UIKit.Icon(Root, s < _r.Stars ? IconId.Star : IconId.StarEmpty, s < _r.Stars ? Theme.Gold : Theme.TextDim);
                UIKit.At((RectTransform)star.transform, 0.5f, 0.5f, (s - 1) * 220f, 140f, 190f, 190f);
            }
            Text grade = UIKit.Heading(Root, TrainingDrills.Grade(_r.Stars), 64, _r.Stars > 0 ? Theme.Gold : Theme.TextDim);
            UIKit.At((RectTransform)grade.transform, 0.5f, 0.5f, 0f, -20f, 1200f, 100f);
            Text score = UIKit.Label(Root, "Aciertos: " + _r.Successes + " de " + _r.Balls + "    ·    Para 3 estrellas: " + _r.Drill.ThreeStars, 36, Theme.Text);
            UIKit.At((RectTransform)score.transform, 0.5f, 0.5f, 0f, -110f, 1400f, 70f);
            Text coins = UIKit.Label(Root, "+" + _rewards.Coins + " monedas    " + string.Join("   ", _rewards.Lines.ToArray()), 32, Theme.Gold);
            UIKit.At((RectTransform)coins.transform, 0.5f, 0.5f, 0f, -180f, 1600f, 70f);
            DrillDefinition drill = _r.Drill;
            Button again = UIKit.Button(Root, "REPETIR", IconId.Restart, ButtonStyle.Primary, () => Game.Flow.StartTraining(drill), 40);
            UIKit.At((RectTransform)again.transform, 1f, 0f, -60f, 40f, 420f, 120f);
            Button other = UIKit.Button(Root, "OTRO EJERCICIO", IconId.Training, ButtonStyle.Secondary, () =>
            {
                Game.Flow.GoToMenu();
                UI.Push(new TrainingSelectScreen());
            }, 34);
            UIKit.At((RectTransform)other.transform, 1f, 0f, -500f, 40f, 460f, 120f);
            Button menu = UIKit.Button(Root, "MENÚ", IconId.Home, ButtonStyle.Secondary, () => Game.Flow.GoToMenu(), 34);
            UIKit.At((RectTransform)menu.transform, 1f, 0f, -980f, 40f, 300f, 120f);
        }

        public override bool OnBack()
        {
            Game.Flow.GoToMenu();
            return true;
        }
    }

    /// <summary>Four illustrated pages: controls, rules, powers, special shot.</summary>
    public class TutorialScreen : UIScreen
    {
        readonly Action _then;
        int _page;
        RectTransform _content;
        Text _pageLabel;
        Button _next;

        public TutorialScreen(Action then)
        {
            _then = then;
        }

        public override bool Transparent { get { return false; } }

        protected override void OnBuild()
        {
            _page = 0;
            Text h = UIKit.Heading(Root, "CÓMO SE JUEGA", 64, Theme.Orange, TextAnchor.MiddleLeft);
            UIKit.At((RectTransform)h.transform, 0f, 1f, 60f, -30f, 1200f, 110f);
            _content = UIKit.Node(Root, "Page");
            UIKit.Region(_content, 0.05f, 0.18f, 0.95f, 0.84f);
            _pageLabel = UIKit.Label(Root, "", 30, Theme.TextDim);
            UIKit.At((RectTransform)_pageLabel.transform, 0.5f, 0f, 0f, 60f, 300f, 60f);
            Button prev = UIKit.Button(Root, "ATRÁS", IconId.ArrowLeft, ButtonStyle.Secondary, () => Go(-1), 34, Sfx.UiBack);
            UIKit.At((RectTransform)prev.transform, 0f, 0f, 60f, 30f, 300f, 110f);
            _next = UIKit.Button(Root, "SIGUIENTE", IconId.ArrowRight, ButtonStyle.Primary, () => Go(1), 36);
            UIKit.At((RectTransform)_next.transform, 1f, 0f, -60f, 30f, 420f, 110f);
            ShowPage();
        }

        void Go(int d)
        {
            if (d > 0 && _page == 3)
            {
                Finish();
                return;
            }
            _page = Mathf.Clamp(_page + d, 0, 3);
            ShowPage();
        }

        void Finish()
        {
            UI.Pop();
            if (_then != null) _then();
        }

        public override bool OnBack()
        {
            Finish();
            return true;
        }

        void ShowPage()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _pageLabel.text = (_page + 1) + " / 4";
            UIKit.SetButtonLabel(_next, _page == 3 ? "¡A JUGAR!" : "SIGUIENTE");
            switch (_page)
            {
                case 0:
                    Row(0, IconId.TwoPlayers, "DOS JUGADORES, UN MÓVIL", "Con el móvil en horizontal, el jugador 1 juega con la mitad izquierda de la pantalla y el jugador 2 con la derecha. Cada uno usa un dedo a la vez.");
                    Row(1, IconId.ArrowRight, "MUEVE LA RAQUETA", "Arrastra el dedo por tu mitad. La raqueta te sigue y su altura se ajusta sola a la pelota.");
                    Row(2, IconId.Smash, "EL GOLPE ES TU MOVIMIENTO", "Atraviesa la pelota: recto hacia la red = recto (rápido y bola alta = REMATE). En diagonal = hacia ese lado. Hacia atrás = globo.");
                    Row(3, IconId.Curve, "EFECTO", "Golpea moviendo la pala hacia arriba o abajo de la pantalla: la pelota curva hacia ese lado. Sin moverla, golpe suave y seguro.");
                    break;
                case 1:
                    Row(0, IconId.Ball, "SAQUE", "Toca la pantalla para sacar cuando la pelota esté en tu raqueta. El saque va directo al campo rival. Se cambia de saque cada 2 puntos.");
                    Row(1, IconId.Table, "UN BOTE Y DEVOLVER", "La pelota debe botar una vez en tu campo y tú debes devolverla al campo rival.");
                    Row(2, IconId.Cross, "PIERDES EL PUNTO SI...", "La dejas botar dos veces, no llegas, la mandas fuera o a la red, o bota en tu propio campo.");
                    Row(3, IconId.Trophy, "GANAR", "Gana quien llega a los puntos del partido (con 2 de ventaja si está activado). El anillo en la mesa marca dónde va a botar la pelota.");
                    break;
                case 2:
                    Row(0, IconId.Random, "RECOGE PODERES", "Aparecen esferas sobre la mesa. Si tu golpe pasa por una, el poder es tuyo. Solo puedes guardar uno.");
                    Row(1, IconId.Fire, "FUEGO · SÚPER REMATE · CURVA", "Afectan a tu siguiente golpe: más velocidad, un remate brutal o una curva cerrada. Siempre se pueden devolver.");
                    Row(2, IconId.Slow, "RALENTIZACIÓN · ESCUDO", "La pelota llega más lenta hacia ti, o un muro te salva una vez de perder el punto.");
                    Row(3, IconId.Freeze, "CONGELACIÓN · ALEATORIO", "El rival se mueve más lento unos segundos (el escudo lo bloquea). El aleatorio te da uno de los anteriores.");
                    break;
                default:
                    Row(0, IconId.Special, "GOLPE ESPECIAL", "Cada golpe y cada punto llenan tu anillo dorado. Cuando esté lleno, toca el botón de la corona y tu siguiente golpe será el BROOKLYN BOMB.");
                    Row(1, IconId.Cpu, "CONTRA LA MÁQUINA", "La máquina reacciona con un pequeño retraso y comete errores según la dificultad. ¡No es invencible!");
                    Row(2, IconId.Coin, "MONEDAS Y DESBLOQUEOS", "Gana monedas en partidos, torneos, entrenamiento y logros. Desbloquea raquetas con estadísticas reales y nuevas mesas.");
                    Row(3, IconId.Pause, "PAUSA", "Botón de pausa a la derecha o el botón Atrás de Android. Si sales de la app, el partido se pausa solo.");
                    break;
            }
        }

        void Row(int index, IconId icon, string title, string text)
        {
            float y1 = 1f - index * 0.25f, y0 = y1 - 0.23f;
            Image card = UIKit.Panel(_content, new Color(0.08f, 0.1f, 0.18f, 0.95f), 26, false, "Row");
            UIKit.Region((RectTransform)card.transform, 0f, y0, 1f, y1);
            Image ic = UIKit.Icon(card.transform, icon, Theme.Orange);
            UIKit.At((RectTransform)ic.transform, 0f, 0.5f, 30f, 0f, 100f, 100f);
            Text t = UIKit.Heading(card.transform, title, 36, Theme.Text, TextAnchor.LowerLeft);
            UIKit.Region((RectTransform)t.transform, 0.09f, 0.52f, 0.98f, 0.95f);
            Text b = UIKit.Label(card.transform, text, 27, Theme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            UIKit.Region((RectTransform)b.transform, 0.09f, 0.05f, 0.98f, 0.52f);
        }
    }
}

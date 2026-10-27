using System.Collections.Generic;
using Brooklyneir.Art;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.Rackets;
using Brooklyneir.Tables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>
    /// Pre-match setup for quick match (2 players), versus CPU and custom matches: rackets for
    /// each player, table, difficulty, and for custom matches every rule option. The chosen
    /// options are remembered per mode while the app is open.
    /// </summary>
    public class MatchSetupScreen : UIScreen
    {
        static readonly Dictionary<GameMode, MatchConfig> Remembered = new Dictionary<GameMode, MatchConfig>();
        static readonly int[] PointOptions = { 3, 5, 7, 11, 15, 21 };
        static readonly float[] TimeOptions = { 0f, 2f, 3f, 5f, 8f };

        readonly GameMode _mode;
        MatchConfig _cfg;
        TMP_Text _notes;

        public MatchSetupScreen(GameMode mode)
        {
            _mode = mode;
        }

        MatchConfig Config
        {
            get
            {
                MatchConfig c;
                if (!Remembered.TryGetValue(_mode, out c))
                {
                    c = new MatchConfig();
                    c.mode = _mode;
                    c.player2IsAI = _mode == GameMode.VersusCpu;
                    c.pointsToWin = _mode == GameMode.Custom ? 11 : 7;
                    c.bestOfSets = 1;
                    Remembered[_mode] = c;
                }
                var d = Game.Save.Data;
                c.racketP1 = d.racketP1;
                c.racketP2 = d.racketP2;
                c.tableId = d.selectedTable;
                return c;
            }
        }

        protected override void OnBuild()
        {
            _cfg = Config;
            string title = _mode == GameMode.QuickMatch ? "PARTIDA RÁPIDA" : _mode == GameMode.VersusCpu ? "CONTRA LA MÁQUINA" : "PARTIDA PERSONALIZADA";
            Header(title);

            // Left: participants and table.
            RectTransform left = UIKit.Node(Root, "Left");
            UIKit.Region(left, 0.03f, 0.18f, 0.47f, 0.84f);
            UIKit.Column(left, 18f);
            bool p2Human = !_cfg.player2IsAI;
            RacketCard(left, 0, "JUGADOR 1", MatchController.PlayerColors[0]);
            if (_mode == GameMode.Custom)
            {
                RectTransform rival = UIKit.Height(UIKit.Node(left, "Rival"), 80f);
                UIKit.Segmented(rival, "Rival", new[] { "2 JUGADORES", "MÁQUINA" }, p2Human ? 0 : 1, i =>
                {
                    _cfg.player2IsAI = i == 1;
                    Rebuild();
                });
            }
            if (p2Human) RacketCard(left, 1, "JUGADOR 2", MatchController.PlayerColors[1]);
            else
            {
                RectTransform diff = UIKit.Height(UIKit.Node(left, "Difficulty"), 80f);
                UIKit.Segmented(diff, "Dificultad", new[] { "FÁCIL", "NORMAL", "DIFÍCIL" }, (int)_cfg.aiDifficulty, i => _cfg.aiDifficulty = (AIDifficulty)i);
            }
            TableCard(left);

            // Right: rules.
            RectTransform right = UIKit.Node(Root, "Right");
            UIKit.Region(right, 0.52f, 0.18f, 0.97f, 0.84f);
            Image bg = right.gameObject.AddComponent<Image>();
            bg.sprite = IconFactory.RoundedPanel(30);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.85f);
            RectTransform rules = UIKit.Node(right, "Rules");
            UIKit.Stretch(rules, 30f, 30f, 24f, 24f);
            UIKit.Column(rules, 12f);

            if (_mode == GameMode.Custom)
            {
                string[] pts = new string[PointOptions.Length];
                int pi = 2;
                for (int i = 0; i < PointOptions.Length; i++) { pts[i] = PointOptions[i].ToString(); if (PointOptions[i] == _cfg.pointsToWin) pi = i; }
                UIKit.Stepper(UIKit.Height(UIKit.Node(rules, "Points"), 78f), "Puntos por set", pts, pi, i => { _cfg.pointsToWin = PointOptions[i]; Validate(); });
                int si = _cfg.bestOfSets == 5 ? 2 : (_cfg.bestOfSets == 3 ? 1 : 0);
                UIKit.Segmented(UIKit.Height(UIKit.Node(rules, "Sets"), 74f), "Sets", new[] { "1", "AL MEJOR DE 3", "AL MEJOR DE 5" }, si, i => { _cfg.bestOfSets = i == 0 ? 1 : (i == 1 ? 3 : 5); Validate(); });
                UIKit.Toggle(UIKit.Height(UIKit.Node(rules, "WinBy2"), 74f), "Ganar por 2 puntos", _cfg.winByTwo, v => _cfg.winByTwo = v);
                string[] times = { "SIN LÍMITE", "2 MIN", "3 MIN", "5 MIN", "8 MIN" };
                int ti = 0;
                for (int i = 0; i < TimeOptions.Length; i++) if (Mathf.Approximately(TimeOptions[i], _cfg.timeLimitMinutes)) ti = i;
                UIKit.Stepper(UIKit.Height(UIKit.Node(rules, "Time"), 78f), "Duración", times, ti, i => { _cfg.timeLimitMinutes = TimeOptions[i]; Validate(); });
            }
            else
            {
                string[] pts = { "5 PUNTOS", "7 PUNTOS", "11 PUNTOS" };
                int pi = _cfg.pointsToWin == 5 ? 0 : (_cfg.pointsToWin == 11 ? 2 : 1);
                UIKit.Segmented(UIKit.Height(UIKit.Node(rules, "Points"), 74f), "Partido a", pts, pi, i => _cfg.pointsToWin = i == 0 ? 5 : (i == 1 ? 7 : 11));
            }
            UIKit.Toggle(UIKit.Height(UIKit.Node(rules, "Powers"), 74f), "Poderes", _cfg.powerUpsEnabled, v => { _cfg.powerUpsEnabled = v; Validate(); Rebuild(); });
            if (_cfg.powerUpsEnabled)
            {
                int fi = _cfg.powerFrequency == PowerFrequency.Low ? 0 : (_cfg.powerFrequency == PowerFrequency.High ? 2 : 1);
                UIKit.Segmented(UIKit.Height(UIKit.Node(rules, "Freq"), 74f), "Frecuencia", new[] { "BAJA", "NORMAL", "ALTA" }, fi,
                    i => _cfg.powerFrequency = i == 0 ? PowerFrequency.Low : (i == 1 ? PowerFrequency.Normal : PowerFrequency.High));
            }
            _notes = UIKit.Label(UIKit.Height(UIKit.Node(rules, "Notes"), 70f), "", 26, Theme.Gold, TextAnchor.MiddleLeft, FontStyle.Italic);
            UIKit.Stretch((RectTransform)_notes.transform);

            Button play = UIKit.Button(Root, "¡A JUGAR!", IconId.Play, ButtonStyle.Primary, StartMatch, 46);
            UIKit.At((RectTransform)play.transform, 1f, 0f, -60f, 30f, 520f, 120f);
            TMP_Text hint = UIKit.Label(Root, p2Human ? "Jugador 1 a la izquierda · Jugador 2 a la derecha. Cada uno usa su mitad de la pantalla." : "Golpea atravesando la pelota: recto = recto o remate, en diagonal = a ese lado, arriba/abajo = efecto, atrás = globo.",
                28, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            UIKit.At((RectTransform)hint.transform, 0f, 0f, 60f, 40f, 1200f, 100f);
            Validate();
        }

        void RacketCard(RectTransform parent, int player, string label, Color color)
        {
            RacketDefinition def = RacketCatalog.Get(player == 0 ? _cfg.racketP1 : _cfg.racketP2);
            Button card = UIKit.Button(parent, "", null, ButtonStyle.Dark, () => UI.Push(new RacketSelectScreen(player)));
            RectTransform rt = UIKit.Height((RectTransform)card.transform, 150f);
            card.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.16f, 0.88f);
            UIKit.Outline(rt, new Color(color.r, color.g, color.b, 0.6f), 26, 4);
            Image sw = UIKit.Panel(rt, ProceduralTextures.Hex(def.ForehandRubber), 40, false, "Swatch");
            UIKit.At((RectTransform)sw.transform, 0f, 0.5f, 30f, 0f, 100f, 100f);
            Image ic = UIKit.Icon(sw.transform, IconId.Racket, Color.white);
            UIKit.Stretch((RectTransform)ic.transform, 16f, 16f, 16f, 16f);
            TMP_Text l = UIKit.Label(rt, label, 26, color, TextAnchor.LowerLeft);
            UIKit.Region((RectTransform)l.transform, 0.2f, 0.52f, 0.9f, 0.9f);
            TMP_Text n = UIKit.Heading(rt, def.Name.ToUpperInvariant(), 40, Theme.Text, TextAnchor.UpperLeft);
            UIKit.Region((RectTransform)n.transform, 0.2f, 0.08f, 0.9f, 0.52f);
            Image arrow = UIKit.Icon(rt, IconId.ArrowRight, Theme.TextDim);
            UIKit.At((RectTransform)arrow.transform, 1f, 0.5f, -30f, 0f, 50f, 50f);
        }

        void TableCard(RectTransform parent)
        {
            TableDefinition def = TableCatalog.Get(_cfg.tableId);
            Button card = UIKit.Button(parent, "", null, ButtonStyle.Dark, () => UI.Push(new TableSelectScreen()));
            RectTransform rt = UIKit.Height((RectTransform)card.transform, 150f);
            card.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.16f, 0.88f);
            UIKit.Outline(rt, new Color(1f, 1f, 1f, 0.2f), 26, 4);
            Image sw = UIKit.Panel(rt, ProceduralTextures.Hex(def.TopColor), 24, false, "Swatch");
            UIKit.At((RectTransform)sw.transform, 0f, 0.5f, 30f, 0f, 100f, 100f);
            Image ic = UIKit.Icon(sw.transform, IconId.Table, ProceduralTextures.Hex(def.LineColor));
            UIKit.Stretch((RectTransform)ic.transform, 14f, 14f, 14f, 14f);
            TMP_Text l = UIKit.Label(rt, "MESA", 26, Theme.TextDim, TextAnchor.LowerLeft);
            UIKit.Region((RectTransform)l.transform, 0.2f, 0.52f, 0.9f, 0.9f);
            TMP_Text n = UIKit.Heading(rt, def.Name.ToUpperInvariant(), 40, Theme.Text, TextAnchor.UpperLeft);
            UIKit.Region((RectTransform)n.transform, 0.2f, 0.08f, 0.9f, 0.52f);
            Image arrow = UIKit.Icon(rt, IconId.ArrowRight, Theme.TextDim);
            UIKit.At((RectTransform)arrow.transform, 1f, 0.5f, -30f, 0f, 50f, 50f);
        }

        void Validate()
        {
            List<string> notes = _cfg.Validate();
            if (_notes != null) _notes.text = notes.Count > 0 ? string.Join("  ", notes.ToArray()) : "";
        }

        void Rebuild()
        {
            UI.Pop();
            UI.Push(new MatchSetupScreen(_mode));
        }

        void StartMatch()
        {
            MatchConfig c = _cfg.Clone();
            c.mode = _mode == GameMode.Custom ? GameMode.Custom : _mode;
            if (_mode == GameMode.QuickMatch) c.player2IsAI = false;
            c.Validate();
            Game.Flow.StartMatch(c);
        }
    }
}

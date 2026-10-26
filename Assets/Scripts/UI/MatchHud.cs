using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using Brooklyneir.PowerUps;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>
    /// In-match interface. The table runs along the screen's long axis, so each human player gets
    /// their half of the screen: player 1 the left, player 2 the right (mirrored). Each half has
    /// the power button (top outer corner) with cooldown, the special-shot button with an energy
    /// ring (bottom outer corner), the opponent's held power, messages, countdown and status
    /// tints (frozen / slow). A single scoreboard sits at the top centre (J1 left, J2 right).
    /// Only the buttons receive touches; everything else lets touches through to the racket.
    /// </summary>
    public class MatchHud
    {
        class Frame
        {
            public int Player;
            public RectTransform Root;
            public Text MyScore, OppScore, MySets, OppSets, MyName, OppName, Timer, Message, Count;
            public Image ServeMine, ServeOpp;
            public Button PowerButton;
            public Image PowerBg, PowerIcon, PowerCooldown;
            public Text PowerLabel;
            public Button SpecialButton;
            public Image SpecialFill, SpecialIcon, SpecialBg;
            public Image FrozenTint, SlowTint;
            public RectTransform OppStatus;
            public Image OppPowerIcon;
            public float MessageTimer;
            public RectTransform MessageRect;
            public RectTransform Board;
        }

        RectTransform _root;
        readonly Frame[] _frames = new Frame[2];
        MatchController _match;
        Button _pause;
        RectTransform _training, _trainingIntro;
        Text _trainingTitle, _trainingCounter, _trainingText;
        RectTransform _practice;
        Text _practiceStreak, _practiceBest, _practiceTotal, _practiceTime;
        Button _exit;
        bool _visible;

        public void Build(UIManager ui, MatchController match)
        {
            _match = match;
            _root = UIKit.Node(ui.HudLayer, "MatchHUD");
            UIKit.Stretch(_root);
            for (int p = 0; p < 2; p++) _frames[p] = BuildFrame(p);

            _pause = UIKit.Button(_root, "", IconId.Pause, ButtonStyle.Dark, () => Game.Flow.Pause(), 30);
            UIKit.At((RectTransform)_pause.transform, 0.5f, 0f, 0f, 24f, 100f, 100f);

            // Training panel.
            Image tp = UIKit.Panel(_root, new Color(0f, 0f, 0f, 0.45f), 28, false, "Training");
            _training = (RectTransform)tp.transform;
            UIKit.At(_training, 0f, 1f, 30f, -30f, 640f, 150f);
            _trainingTitle = UIKit.Heading(_training, "", 40, Theme.Gold, TextAnchor.UpperLeft);
            UIKit.Stretch((RectTransform)_trainingTitle.transform, 26f, 20f, 18f, 70f);
            _trainingCounter = UIKit.Label(_training, "", 34, Theme.Text, TextAnchor.LowerLeft);
            UIKit.Stretch((RectTransform)_trainingCounter.transform, 26f, 20f, 70f, 18f);

            Image intro = UIKit.Panel(_root, new Color(0.04f, 0.05f, 0.1f, 0.9f), 36, true, "TrainingIntro");
            _trainingIntro = (RectTransform)intro.transform;
            UIKit.At(_trainingIntro, 0.5f, 0.5f, 0f, 0f, 1100f, 560f);
            UIKit.Outline(_trainingIntro, new Color(1f, 1f, 1f, 0.15f), 36, 3);
            Text it = UIKit.Heading(_trainingIntro, "ENTRENAMIENTO", 56, Theme.Gold);
            UIKit.Region((RectTransform)it.transform, 0.05f, 0.75f, 0.95f, 0.95f);
            _trainingText = UIKit.Label(_trainingIntro, "", 36, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Region((RectTransform)_trainingText.transform, 0.07f, 0.3f, 0.93f, 0.75f);
            Button go = UIKit.Button(_trainingIntro, "EMPEZAR", IconId.Play, ButtonStyle.Primary, () =>
            {
                _trainingIntro.gameObject.SetActive(false);
                _match.BeginTrainingDrill();
            }, 40);
            UIKit.Region((RectTransform)go.transform, 0.3f, 0.06f, 0.7f, 0.24f);

            // Practice wall panel: session statistics only (no rating), plus an always-visible exit.
            Image pp = UIKit.Panel(_root, new Color(0.03f, 0.04f, 0.08f, 0.72f), 26, false, "Practice");
            _practice = (RectTransform)pp.transform;
            UIKit.At(_practice, 0.5f, 1f, 0f, -20f, 900f, 130f);
            UIKit.Outline(_practice, new Color(1f, 1f, 1f, 0.08f), 26, 3);
            _practiceStreak = PracticeStat(_practice, 0, "RACHA");
            _practiceBest = PracticeStat(_practice, 1, "MEJOR");
            _practiceTotal = PracticeStat(_practice, 2, "DEVOLUCIONES");
            _practiceTime = PracticeStat(_practice, 3, "TIEMPO");
            _exit = UIKit.Button(_root, "SALIR", IconId.Home, ButtonStyle.Secondary, () => Game.Flow.QuitToMenu(), 30);
            UIKit.At((RectTransform)_exit.transform, 1f, 1f, -24f, -24f, 230f, 96f);

            match.Message += OnMessage;
            match.Countdown += OnCountdown;
        }

        // Places an element in player p's half, mirrored horizontally for player 2 so each player's
        // controls sit on their own outer edge of the screen.
        static RectTransform Side(int p, RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            if (p == 1) { ax = 1f - ax; x = -x; }
            return UIKit.At(rt, ax, ay, x, y, w, h);
        }

        static Text PracticeStat(RectTransform parent, int index, string label)
        {
            float x0 = index * 0.25f, x1 = x0 + 0.25f;
            Text l = UIKit.Label(parent, label, 22, Theme.TextDim);
            UIKit.Region((RectTransform)l.transform, x0, 0.62f, x1, 0.95f);
            Text v = UIKit.Heading(parent, "0", 52, Theme.Text);
            UIKit.Region((RectTransform)v.transform, x0, 0.04f, x1, 0.66f);
            return v;
        }

        Frame BuildFrame(int p)
        {
            Frame f = new Frame();
            f.Player = p;
            f.Root = UIKit.Node(_root, "FrameP" + (p + 1));
            if (p == 0) UIKit.Region(f.Root, 0f, 0f, 0.5f, 1f);
            else UIKit.Region(f.Root, 0.5f, 0f, 1f, 1f);
            Color mine = MatchController.PlayerColors[p];
            Color opp = MatchController.PlayerColors[1 - p];

            // Status tints over this player's half.
            f.FrozenTint = UIKit.Panel(f.Root, new Color(0.6f, 0.85f, 1f, 0f), 2, false, "FrozenTint");
            UIKit.Stretch((RectTransform)f.FrozenTint.transform);
            f.FrozenTint.sprite = IconFactory.Gradient(new Color(1, 1, 1, 1), new Color(1, 1, 1, 1));
            f.SlowTint = UIKit.Panel(f.Root, new Color(0.3f, 0.6f, 1f, 0f), 2, false, "SlowTint");
            UIKit.Stretch((RectTransform)f.SlowTint.transform);

            // Scoreboard: one broadcast-style board at the top centre of the screen (player 1's
            // frame owns it; player 2's copy stays hidden). Left column = J1, right column = J2.
            Image board = UIKit.Panel(p == 0 ? _root : f.Root, new Color(0.03f, 0.04f, 0.08f, 0.72f), 26, false, "Score");
            RectTransform br = (RectTransform)board.transform;
            UIKit.At(br, 0.5f, 1f, 0f, -20f, 440f, 150f);
            UIKit.Outline(br, new Color(1f, 1f, 1f, 0.08f), 26, 3);
            f.Board = br;
            f.MyName = UIKit.Label(br, "J" + (p + 1), 26, mine, TextAnchor.MiddleCenter);
            UIKit.Region((RectTransform)f.MyName.transform, 0.03f, 0.66f, 0.47f, 0.96f);
            f.OppName = UIKit.Label(br, "J" + (2 - p), 26, opp, TextAnchor.MiddleCenter);
            UIKit.Region((RectTransform)f.OppName.transform, 0.53f, 0.66f, 0.97f, 0.96f);
            f.MyScore = UIKit.Heading(br, "0", 70, Theme.Text, TextAnchor.MiddleCenter);
            UIKit.Region((RectTransform)f.MyScore.transform, 0.12f, 0.02f, 0.4f, 0.7f);
            f.OppScore = UIKit.Heading(br, "0", 70, Theme.Text, TextAnchor.MiddleCenter);
            UIKit.Region((RectTransform)f.OppScore.transform, 0.6f, 0.02f, 0.88f, 0.7f);
            f.MySets = UIKit.Label(br, "", 28, Theme.Gold, TextAnchor.MiddleCenter);
            UIKit.Region((RectTransform)f.MySets.transform, 0.4f, 0.1f, 0.49f, 0.6f);
            f.OppSets = UIKit.Label(br, "", 28, Theme.Gold, TextAnchor.MiddleCenter);
            UIKit.Region((RectTransform)f.OppSets.transform, 0.51f, 0.1f, 0.6f, 0.6f);
            f.ServeMine = UIKit.Icon(br, IconId.Ball, Theme.Gold);
            UIKit.At((RectTransform)f.ServeMine.transform, 0f, 0.35f, 14f, 0f, 30f, 30f);
            f.ServeOpp = UIKit.Icon(br, IconId.Ball, Theme.Gold);
            UIKit.At((RectTransform)f.ServeOpp.transform, 1f, 0.35f, -14f, 0f, 30f, 30f);
            f.Timer = UIKit.Label(p == 0 ? _root : f.Root, "", 30, Theme.Text, TextAnchor.MiddleCenter);
            UIKit.At((RectTransform)f.Timer.transform, 0.5f, 1f, 0f, -175f, 240f, 50f);
            if (p == 1)
            {
                br.gameObject.SetActive(false);
                f.Timer.gameObject.SetActive(false);
            }

            // Power button (top outer corner of this player's half).
            f.PowerButton = UIKit.Button(f.Root, "", null, ButtonStyle.Dark, () => _match.RequestActivatePower(p), 30);
            RectTransform pr = (RectTransform)f.PowerButton.transform;
            Side(p, pr, 0f, 1f, 30f, -30f, 190f, 190f);
            f.PowerBg = f.PowerButton.GetComponent<Image>();
            f.PowerBg.sprite = IconFactory.CircleSprite();
            f.PowerBg.type = Image.Type.Simple;
            f.PowerIcon = UIKit.Icon(pr, IconId.Random, Color.white);
            UIKit.Stretch((RectTransform)f.PowerIcon.transform, 44f, 44f, 38f, 50f);
            f.PowerCooldown = UIKit.Icon(pr, IconId.Ball, new Color(0f, 0f, 0f, 0.55f));
            f.PowerCooldown.sprite = IconFactory.CircleSprite();
            f.PowerCooldown.type = Image.Type.Filled;
            f.PowerCooldown.fillMethod = Image.FillMethod.Radial360;
            f.PowerCooldown.fillOrigin = 2;
            UIKit.Stretch((RectTransform)f.PowerCooldown.transform);
            f.PowerLabel = UIKit.Label(pr, "PODER", 22, Theme.TextDim);
            UIKit.Region((RectTransform)f.PowerLabel.transform, 0f, 0.06f, 1f, 0.26f);

            // Opponent status (their held power), next to the power button.
            f.OppStatus = UIKit.Node(f.Root, "OppStatus");
            Side(p, f.OppStatus, 0f, 1f, 236f, -86f, 76f, 76f);
            Image osBg = f.OppStatus.gameObject.AddComponent<Image>();
            osBg.sprite = IconFactory.CircleSprite();
            osBg.color = new Color(0f, 0f, 0f, 0.5f);
            osBg.raycastTarget = false;
            f.OppPowerIcon = UIKit.Icon(f.OppStatus, IconId.Random, Color.white);
            UIKit.Stretch((RectTransform)f.OppPowerIcon.transform, 14f, 14f, 14f, 14f);

            // Special button (bottom outer corner) with energy ring.
            f.SpecialButton = UIKit.Button(f.Root, "", null, ButtonStyle.Dark, () => _match.RequestSpecial(p), 30);
            RectTransform sr = (RectTransform)f.SpecialButton.transform;
            Side(p, sr, 0f, 0f, 36f, 36f, 170f, 170f);
            f.SpecialBg = f.SpecialButton.GetComponent<Image>();
            f.SpecialBg.sprite = IconFactory.CircleSprite();
            f.SpecialBg.type = Image.Type.Simple;
            f.SpecialFill = UIKit.Icon(sr, IconId.Ball, Theme.Gold);
            f.SpecialFill.sprite = IconFactory.RingSprite();
            f.SpecialFill.type = Image.Type.Filled;
            f.SpecialFill.fillMethod = Image.FillMethod.Radial360;
            f.SpecialFill.fillOrigin = 2;
            UIKit.Stretch((RectTransform)f.SpecialFill.transform, -6f, -6f, -6f, -6f);
            f.SpecialIcon = UIKit.Icon(sr, IconId.Special, Theme.Gold);
            UIKit.Stretch((RectTransform)f.SpecialIcon.transform, 40f, 40f, 40f, 40f);

            // Messages and countdown, centred in this player's half.
            f.Message = UIKit.Heading(f.Root, "", 58, Color.white);
            f.MessageRect = (RectTransform)f.Message.transform;
            UIKit.At(f.MessageRect, 0.5f, 0.5f, 0f, 150f, 860f, 100f);
            f.Count = UIKit.Heading(f.Root, "", 150, Theme.Gold);
            UIKit.At((RectTransform)f.Count.transform, 0.5f, 0.5f, 0f, 0f, 420f, 220f);
            return f;
        }

        /// <summary>Configures the HUD for the match that just started.</summary>
        public void Bind()
        {
            bool training = _match.IsTraining;
            bool p2 = _match.Human[1];
            bool vsCpu = !p2;
            _frames[0].Root.gameObject.SetActive(true);
            _frames[1].Root.gameObject.SetActive(p2);
            // Against the CPU, player 1's frame takes the whole screen.
            UIKit.Region(_frames[0].Root, 0f, 0f, p2 ? 0.5f : 1f, 1f);
            for (int p = 0; p < 2; p++)
            {
                Frame f = _frames[p];
                bool powers = _match.Config.powerUpsEnabled && !training;
                f.PowerButton.gameObject.SetActive(powers);
                f.SpecialButton.gameObject.SetActive(!training);
                f.OppStatus.gameObject.SetActive(powers);
                f.Message.text = "";
                f.Count.text = "";
                f.MessageTimer = 0f;
                f.Board.gameObject.SetActive(!training && p == 0);
                f.Timer.gameObject.SetActive(p == 0);
                f.OppName.text = vsCpu && p == 0 ? "CPU" : "J" + (2 - p);
                if (_match.Config.mode == GameMode.Tournament && _match.Config.tournamentRival > 0 && p == 0)
                    f.OppName.text = Brooklyneir.Progression.Tournament.Rivals[_match.Config.tournamentRival].Nickname.ToUpperInvariant();
                f.MyName.text = vsCpu ? "TÚ" : "J" + (p + 1);
            }
            bool practice = _match.IsPracticeWall;
            _training.gameObject.SetActive(training && !practice);
            _trainingIntro.gameObject.SetActive(training && !practice);
            _practice.gameObject.SetActive(practice);
            _exit.gameObject.SetActive(practice);
            if (training && _match.Drill != null)
            {
                _trainingTitle.text = _match.Drill.Title.ToUpperInvariant();
                _trainingText.text = _match.Drill.Instructions;
            }
        }

        public void SetVisible(bool on)
        {
            _visible = on;
            _root.gameObject.SetActive(on);
        }

        void OnMessage(int player, string text, Color color)
        {
            for (int p = 0; p < 2; p++)
            {
                if (player >= 0 && player != p) continue;
                Frame f = _frames[p];
                f.Message.text = text;
                f.Message.color = color;
                f.MessageTimer = string.IsNullOrEmpty(text) ? 0f : (text == "TOCA PARA SACAR" ? 999f : 1.4f);
                f.MessageRect.localScale = Vector3.one * 1.25f;
            }
            // Single-player: messages for the CPU's side are shown smaller in player 1's frame.
            if (player == 1 && !_match.Human[1] && !string.IsNullOrEmpty(text) && text != "TOCA PARA SACAR")
            {
                Frame f = _frames[0];
                if (f.MessageTimer <= 0.2f)
                {
                    f.Message.text = "CPU: " + text;
                    f.Message.color = new Color(color.r, color.g, color.b, 0.85f);
                    f.MessageTimer = 1.1f;
                }
            }
        }

        void OnCountdown(string text)
        {
            for (int p = 0; p < 2; p++)
            {
                _frames[p].Count.text = text;
                ((RectTransform)_frames[p].Count.transform).localScale = Vector3.one * 1.4f;
            }
        }

        public void Tick(float dt)
        {
            if (!_visible || _match == null || _match.Phase == MatchPhase.Inactive) return;
            MatchScore score = _match.Score;
            PowerUpSystem powers = _match.Powers;
            for (int p = 0; p < 2; p++)
            {
                Frame f = _frames[p];
                if (!f.Root.gameObject.activeSelf) continue;
                int o = 1 - p;
                if (score != null)
                {
                    f.MyScore.text = score.Points[p].ToString();
                    f.OppScore.text = score.Points[o].ToString();
                    bool sets = score.SetsToWin > 1;
                    f.MySets.text = sets ? score.Sets[p].ToString() : "";
                    f.OppSets.text = sets ? score.Sets[o].ToString() : "";
                    f.ServeMine.enabled = _match.ServeOwner == p;
                    f.ServeOpp.enabled = _match.ServeOwner == o;
                }
                if (_match.Config.timeLimitMinutes > 0f && !_match.IsTraining)
                {
                    float left = Mathf.Max(0f, _match.Config.timeLimitMinutes * 60f - _match.MatchClock);
                    f.Timer.text = left <= 0f ? "¡TIEMPO!" : string.Format("{0}:{1:00}", (int)(left / 60f), (int)(left % 60f));
                }
                else f.Timer.text = "";

                PlayerPowerState s = powers.Players[p];
                if (f.PowerButton.gameObject.activeSelf)
                {
                    bool has = s.Held != PowerUpType.None;
                    f.PowerIcon.enabled = has;
                    if (has)
                    {
                        f.PowerIcon.sprite = IconFactory.Get(PowerVisuals.IconOf(s.Held));
                        f.PowerIcon.color = PowerVisuals.ColorOf(s.Held);
                    }
                    float cd = powers.Tuning.globalCooldown > 0f ? s.Cooldown / powers.Tuning.globalCooldown : 0f;
                    f.PowerCooldown.fillAmount = Mathf.Clamp01(cd);
                    Color active = ActiveColor(s);
                    f.PowerBg.color = active.a > 0f ? Color.Lerp(Theme.Panel, active, 0.55f + Mathf.Sin(Time.unscaledTime * 8f) * 0.15f)
                        : (has ? Color.Lerp(Theme.Panel, PowerVisuals.ColorOf(s.Held), 0.35f) : new Color(0.08f, 0.1f, 0.16f, 0.75f));
                    f.PowerLabel.text = has ? (s.Cooldown > 0f ? "ESPERA" : "¡USAR!") : (active.a > 0f ? "ACTIVO" : "PODER");
                    f.PowerLabel.color = has && s.Cooldown <= 0f ? Color.white : Theme.TextDim;
                    PlayerPowerState os = powers.Players[o];
                    f.OppPowerIcon.enabled = os.Held != PowerUpType.None;
                    if (os.Held != PowerUpType.None)
                    {
                        f.OppPowerIcon.sprite = IconFactory.Get(PowerVisuals.IconOf(os.Held));
                        f.OppPowerIcon.color = PowerVisuals.ColorOf(os.Held);
                    }
                }
                if (f.SpecialButton.gameObject.activeSelf)
                {
                    f.SpecialFill.fillAmount = s.SpecialEnergy / 100f;
                    bool ready = s.SpecialReady;
                    f.SpecialIcon.color = s.SpecialArmed ? Color.white : (ready ? Theme.Gold : new Color(1f, 1f, 1f, 0.3f));
                    f.SpecialBg.color = s.SpecialArmed ? Color.Lerp(Theme.Panel, Theme.Gold, 0.6f + Mathf.Sin(Time.unscaledTime * 10f) * 0.2f)
                        : (ready ? Color.Lerp(Theme.Panel, Theme.Gold, 0.25f) : new Color(0.08f, 0.1f, 0.16f, 0.75f));
                }

                float frozenA = s.Frozen ? 0.18f + Mathf.Sin(Time.unscaledTime * 6f) * 0.04f : 0f;
                f.FrozenTint.color = new Color(0.6f, 0.88f, 1f, Mathf.MoveTowards(f.FrozenTint.color.a, frozenA, dt * 2f));
                float slowA = s.SlowActive ? 0.1f : 0f;
                f.SlowTint.color = new Color(0.3f, 0.6f, 1f, Mathf.MoveTowards(f.SlowTint.color.a, slowA, dt * 2f));

                if (f.MessageTimer > 0f)
                {
                    f.MessageTimer -= dt;
                    f.MessageRect.localScale = Vector3.Lerp(f.MessageRect.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));
                    Color c = f.Message.color;
                    c.a = Mathf.Clamp01(f.MessageTimer * 3f);
                    if (f.MessageTimer > 900f) c.a = 0.6f + Mathf.Sin(Time.unscaledTime * 5f) * 0.3f;
                    f.Message.color = c;
                    if (f.MessageTimer <= 0f) f.Message.text = "";
                }
                RectTransform cr = (RectTransform)f.Count.transform;
                cr.localScale = Vector3.Lerp(cr.localScale, Vector3.one, 1f - Mathf.Exp(-10f * dt));
            }
            if (_match.IsPracticeWall && _match.PracticeStats != null)
            {
                PracticeWallStats ps = _match.PracticeStats;
                _practiceStreak.text = ps.CurrentStreak.ToString();
                _practiceBest.text = ps.BestStreak.ToString();
                _practiceTotal.text = ps.TotalReturns.ToString();
                int secs = (int)ps.SessionSeconds;
                _practiceTime.text = string.Format("{0}:{1:00}", secs / 60, secs % 60);
            }
            if (_match.IsTraining && _match.Drill != null)
                _trainingCounter.text = "Bola " + Mathf.Min(_match.TrainingFed, _match.Drill.Balls) + "/" + _match.Drill.Balls + "    Aciertos " + _match.TrainingSuccesses;
            _pause.gameObject.SetActive(_match.Phase != MatchPhase.MatchOver);
        }

        static Color ActiveColor(PlayerPowerState s)
        {
            if (s.FireActive) return PowerVisuals.ColorOf(PowerUpType.Fire);
            if (s.SuperSmashArmed) return PowerVisuals.ColorOf(PowerUpType.SuperSmash);
            if (s.CurveArmed) return PowerVisuals.ColorOf(PowerUpType.Curve);
            if (s.ShieldActive) return PowerVisuals.ColorOf(PowerUpType.Shield);
            if (s.SlowActive) return PowerVisuals.ColorOf(PowerUpType.Slow);
            return new Color(0f, 0f, 0f, 0f);
        }
    }
}

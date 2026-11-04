using Brooklyneir.Art;
using Brooklyneir.Core;
using Brooklyneir.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>LAN: two phones on the same Wi-Fi. "Crear partida" or "Unirse".</summary>
    public class LanMenuScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Header("LAN · MISMA WIFI", true, false);
            TMP_Text info = UIKit.Label(Root, "Dos móviles conectados a la misma wifi (o uno compartiendo su zona wifi con el otro). Sin cuentas y sin Internet.", 30, Theme.TextDim);
            UIKit.Region((RectTransform)info.transform, 0.1f, 0.66f, 0.9f, 0.8f);

            RectTransform row = UIKit.Node(Root, "Choices");
            UIKit.Region(row, 0.12f, 0.24f, 0.88f, 0.6f);
            HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 40f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            UIKit.Button(row, "CREAR PARTIDA", IconId.Plus, ButtonStyle.Primary, () => UI.Push(new LanHostScreen()), 44);
            UIKit.Button(row, "UNIRSE", IconId.TwoPlayers, ButtonStyle.Cyan, () => UI.Push(new LanJoinScreen()), 44);

            if (!LanTransport.Available)
            {
                TMP_Text warn = UIKit.Label(Root, LanTransport.NotInstalled, 30, Theme.Danger);
                UIKit.Region((RectTransform)warn.transform, 0.1f, 0.08f, 0.9f, 0.18f);
            }
        }
    }

    /// <summary>Shared by the host and join screens: status text that follows the lobby.</summary>
    public abstract class LanStatusScreen : UIScreen
    {
        protected TMP_Text Status;
        protected TMP_Text Detail;
        float _refresh;

        protected LanLobby Lan { get { return Game.Lan; } }

        protected void BuildStatus(float minY, float maxY)
        {
            Status = UIKit.Heading(Root, "", 46, Theme.Text);
            UIKit.Region((RectTransform)Status.transform, 0.05f, (minY + maxY) * 0.5f, 0.95f, maxY);
            Detail = UIKit.Label(Root, "", 30, Theme.TextDim);
            UIKit.Region((RectTransform)Detail.transform, 0.05f, minY, 0.95f, (minY + maxY) * 0.5f);
        }

        public override void OnShow() { if (Lan != null) Lan.Changed += Refresh; Refresh(); }
        public override void OnHide() { if (Lan != null) Lan.Changed -= Refresh; }

        public override void Tick(float dt)
        {
            _refresh -= dt;
            if (_refresh <= 0f) { _refresh = 0.5f; Refresh(); }
        }

        protected abstract void Refresh();

        protected string RivalLine()
        {
            string ping = Lan.RoundTripMs >= 0f ? "  ·  " + Mathf.RoundToInt(Lan.RoundTripMs) + " ms" : "";
            return "Conectado con " + (Lan.RivalName ?? "el otro móvil") + ping;
        }

        protected string EndedLine()
        {
            if (!string.IsNullOrEmpty(Lan.Error)) return Lan.Error;
            switch (Lan.Machine.Reason)
            {
                case EndReason.OpponentLeft: return "El otro móvil se ha ido.";
                case EndReason.ConnectFailed: return "No se pudo conectar.";
                case EndReason.Rejected: return LanLobby.RejectText(Lan.Machine.RejectedBecause);
                default: return "Conexión cerrada.";
            }
        }

        public override bool OnBack()
        {
            if (Lan != null) Lan.Close();
            UI.Pop();
            return true;
        }
    }

    /// <summary>Host: shows this phone's address, waits for the other phone, then chooses the match.</summary>
    public class LanHostScreen : LanStatusScreen
    {
        TMP_Text _address;
        Button _again;
        RectTransform _choices;

        protected override void OnBuild()
        {
            Header("CREAR PARTIDA", true, false);
            TMP_Text hint = UIKit.Label(Root, "En el otro móvil, toca UNIRSE y escribe esta dirección:", 32, Theme.TextDim);
            UIKit.Region((RectTransform)hint.transform, 0.05f, 0.7f, 0.95f, 0.8f);
            _address = UIKit.Heading(Root, "", 110, Theme.Gold);
            UIKit.Region((RectTransform)_address.transform, 0.05f, 0.5f, 0.95f, 0.7f);
            BuildStatus(0.26f, 0.48f);
            _again = UIKit.Button(Root, "ESPERAR A OTRO MÓVIL", IconId.Restart, ButtonStyle.Secondary, Start, 34);
            UIKit.At((RectTransform)_again.transform, 0.5f, 0f, 0f, 110f, 700f, 110f);
            _choices = UIKit.Node(Root, "Choices");
            UIKit.Region(_choices, 0.15f, 0.05f, 0.85f, 0.22f);
            HorizontalLayoutGroup h = _choices.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 40f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            UIKit.Button(_choices, "PARTIDA RÁPIDA", IconId.TwoPlayers, ButtonStyle.Primary, () => UI.Push(new MatchSetupScreen(Gameplay.GameMode.QuickMatch, true)), 38);
            UIKit.Button(_choices, "PERSONALIZADA", IconId.Gear, ButtonStyle.Cyan, () => UI.Push(new MatchSetupScreen(Gameplay.GameMode.Custom, true)), 38);
            _choices.gameObject.SetActive(false);
            if (Lan != null && LanTransport.Available) Lan.Host();
        }

        void Start()
        {
            if (Lan != null) Lan.Host();
            Refresh();
        }

        protected override void Refresh()
        {
            if (Status == null) return;
            if (Lan == null || !LanTransport.Available)
            {
                _address.text = "—";
                Status.text = "LAN NO DISPONIBLE";
                Detail.text = LanTransport.NotInstalled;
                _again.gameObject.SetActive(false);
                return;
            }
            _address.text = string.IsNullOrEmpty(Lan.LocalAddress) ? "sin wifi" : Lan.LocalAddress;
            bool ended = Lan.Machine == null || Lan.Machine.State == LinkState.Ended;
            _again.gameObject.SetActive(ended);
            _choices.gameObject.SetActive(!ended && Lan.Machine.State == LinkState.Lobby);
            if (ended) { Status.text = "SIN CONEXIÓN"; Detail.text = EndedLine(); return; }
            switch (Lan.Machine.State)
            {
                case LinkState.Hosting:
                    Status.text = "ESPERANDO AL OTRO MÓVIL...";
                    Detail.text = string.IsNullOrEmpty(Lan.LocalAddress) ? "Conéctate a una wifi o comparte tu zona wifi." : "Puerto " + Lan.Config.gamePort;
                    break;
                case LinkState.Lobby:
                    Status.text = RivalLine();
                    Detail.text = "Elige la partida: el otro móvil empezará a la vez que tú.";
                    break;
                default:
                    Status.text = Lan.Machine.State.ToString();
                    Detail.text = "";
                    break;
            }
        }
    }

    /// <summary>Guest: types the host's address with an on-screen keypad (no system keyboard) and connects.</summary>
    public class LanJoinScreen : LanStatusScreen
    {
        const string PrefKey = "ttlb_lan_last_host";
        const int MaxLength = 15;
        string _ip;
        TMP_Text _field;
        Button _connect;

        protected override void OnBuild()
        {
            Header("UNIRSE", true, false);
            _ip = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(_ip))
            {
                // Start from this phone's own network (e.g. "192.168.1."): only the last number changes.
                string mine = LanLobby.LocalIPv4();
                int dot = mine.LastIndexOf('.');
                _ip = dot > 0 ? mine.Substring(0, dot + 1) : "";
            }

            TMP_Text hint = UIKit.Label(Root, "Dirección que muestra el móvil que creó la partida:", 30, Theme.TextDim);
            UIKit.Region((RectTransform)hint.transform, 0.04f, 0.74f, 0.5f, 0.82f);
            Image box = UIKit.Panel(Root, Theme.PanelLight, 20, false, "Field");
            UIKit.Region((RectTransform)box.transform, 0.05f, 0.58f, 0.49f, 0.73f);
            _field = UIKit.Heading(box.transform, "", 72, Theme.Gold);
            UIKit.Stretch((RectTransform)_field.transform, 20f, 20f, 0f, 0f);
            BuildStatus(0.3f, 0.54f);
            UIKit.Region((RectTransform)Status.transform, 0.03f, 0.42f, 0.51f, 0.54f);
            UIKit.Region((RectTransform)Detail.transform, 0.03f, 0.26f, 0.51f, 0.42f);
            _connect = UIKit.Button(Root, "CONECTAR", IconId.Play, ButtonStyle.Primary, Connect, 44);
            UIKit.Region((RectTransform)_connect.transform, 0.08f, 0.07f, 0.46f, 0.21f);

            RectTransform pad = UIKit.Node(Root, "Keypad");
            UIKit.Region(pad, 0.55f, 0.06f, 0.96f, 0.82f);
            GridLayoutGroup g = pad.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(230f, 150f);
            g.spacing = new Vector2(18f, 18f);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "⌫" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                if (k == "⌫") UIKit.Button(pad, "", IconId.ArrowLeft, ButtonStyle.Secondary, Backspace, 54);
                else UIKit.Button(pad, k, null, ButtonStyle.Secondary, () => Type(k), 60);
            }
            ShowField();
        }

        void Type(string k)
        {
            if (_ip.Length >= MaxLength) return;
            _ip += k;
            ShowField();
        }

        void Backspace()
        {
            if (_ip.Length > 0) _ip = _ip.Substring(0, _ip.Length - 1);
            ShowField();
        }

        void ShowField() { if (_field != null) _field.text = _ip.Length > 0 ? _ip : "_"; }

        void Connect()
        {
            if (Lan == null) return;
            PlayerPrefs.SetString(PrefKey, _ip);
            PlayerPrefs.Save();
            Lan.Join(_ip);
            Refresh();
        }

        protected override void Refresh()
        {
            if (Status == null) return;
            if (Lan == null || !LanTransport.Available)
            {
                Status.text = "LAN NO DISPONIBLE";
                Detail.text = LanTransport.NotInstalled;
                _connect.interactable = false;
                return;
            }
            LinkState s = Lan.Machine == null ? LinkState.Idle : Lan.Machine.State;
            _connect.interactable = s == LinkState.Idle || s == LinkState.Ended;
            switch (s)
            {
                case LinkState.Idle: Status.text = ""; Detail.text = ""; break;
                case LinkState.Connecting:
                case LinkState.Handshaking: Status.text = "CONECTANDO..."; Detail.text = _ip; break;
                case LinkState.Lobby: Status.text = RivalLine(); Detail.text = "Esperando a que el anfitrión elija la partida..."; break;
                case LinkState.Ended: Status.text = "SIN CONEXIÓN"; Detail.text = EndedLine(); break;
                default: Status.text = s.ToString(); Detail.text = ""; break;
            }
        }
    }

    /// <summary>
    /// LAN pause: the same on both phones (either can pause or resume). Resuming starts a short
    /// countdown on both, to a common tick. Leaving gives the match to the other player.
    /// </summary>
    public class LanPauseScreen : UIScreen
    {
        TMP_Text _status;
        Button _resume;

        protected override void OnBuild()
        {
            TMP_Text t = UIKit.Heading(Root, "PAUSA", 110, Theme.Text);
            UIKit.At((RectTransform)t.transform, 0.5f, 1f, 0f, -80f, 900f, 160f);
            _status = UIKit.Label(Root, "El partido está parado en los dos móviles.", 32, Theme.TextDim);
            UIKit.At((RectTransform)_status.transform, 0.5f, 1f, 0f, -200f, 1400f, 70f);
            RectTransform col = UIKit.Node(Root, "Buttons");
            UIKit.At(col, 0.5f, 0.5f, 0f, -60f, 620f, 300f);
            UIKit.Column(col, 22f);
            _resume = UIKit.Button(col, "REANUDAR", IconId.Play, ButtonStyle.Primary, () => Game.Flow.Resume(), 42);
            UIKit.Height((RectTransform)_resume.transform, 120f);
            UIKit.Height((RectTransform)UIKit.Button(col, "SALIR DEL PARTIDO", IconId.Home, ButtonStyle.Danger, () =>
                UI.Confirm("SALIR", "Si sales, el partido cuenta como perdido y gana el otro jugador.", "SALIR", () => Game.Flow.QuitToMenu()), 36).transform, 110f);
        }

        public override void Tick(float dt)
        {
            NetMatchSession net = Game.Match != null ? Game.Match.Net : null;
            if (net == null) return;
            if (net.ResumeScheduled)
            {
                long left = (long)net.ResumeTick - net.Now;
                int secs = Mathf.Max(0, Mathf.CeilToInt(left / (float)net.Ball.TickRate));
                _status.text = "Reanudando en " + secs + "...";
                _resume.interactable = false;
            }
            else if (net.State == SessionState.WaitingReconnect)
            {
                _status.text = "Conexión perdida: esperando al otro móvil...";
                _resume.interactable = false;
            }
            else
            {
                _status.text = "El partido está parado en los dos móviles.";
                _resume.interactable = true;
            }
        }

        public override bool OnBack()
        {
            Game.Flow.Resume();
            return true;
        }
    }
}

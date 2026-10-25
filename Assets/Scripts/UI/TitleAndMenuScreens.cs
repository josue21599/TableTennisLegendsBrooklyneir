using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>Animated logo over the arena demo. Tap anywhere to continue.</summary>
    public class TitleScreen : UIScreen
    {
        Text _tap;
        RectTransform _logo;
        float _t;

        protected override void OnBuild()
        {
            _logo = UIKit.Node(Root, "Logo");
            UIKit.At(_logo, 0.5f, 0.5f, 0f, 120f, 1500f, 520f);
            BuildLogo(_logo, 1f);
            _tap = UIKit.Heading(Root, "TOCA PARA EMPEZAR", 48, Color.white);
            UIKit.At((RectTransform)_tap.transform, 0.5f, 0f, 0f, 150f, 1200f, 90f);
            Text v = UIKit.Label(Root, "v" + Application.version + "  ·  2 jugadores en un móvil  ·  sin conexión", 26, Theme.TextDim);
            UIKit.At((RectTransform)v.transform, 0.5f, 0f, 0f, 50f, 1400f, 50f);
            Button full = UIKit.Button(Root, "", null, ButtonStyle.Ghost, () => Game.Flow.GoToMenu());
            UIKit.Stretch((RectTransform)full.transform);
            full.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            Object.Destroy(full.GetComponent<ButtonFx>());
            Transform o = full.transform.Find("Outline");
            if (o != null) Object.Destroy(o.gameObject);
        }

        public static void BuildLogo(RectTransform parent, float scale)
        {
            Text top = UIKit.Heading(parent, "TABLE TENNIS", Mathf.RoundToInt(92 * scale), Theme.Text);
            UIKit.Region((RectTransform)top.transform, 0f, 0.62f, 1f, 0.98f);
            Text legends = UIKit.Heading(parent, "LEGENDS", Mathf.RoundToInt(170 * scale), Theme.Orange);
            UIKit.Region((RectTransform)legends.transform, 0f, 0.22f, 1f, 0.7f);
            Image strip = UIKit.Panel(parent, Theme.Cyan, 12, false, "Strip");
            UIKit.Region((RectTransform)strip.transform, 0.2f, 0.06f, 0.8f, 0.2f);
            Text brook = UIKit.Heading(strip.transform, "BROOKLYNEIR", Mathf.RoundToInt(56 * scale), new Color(0.05f, 0.07f, 0.12f));
            UIKit.Stretch((RectTransform)brook.transform);
            Image ball = UIKit.Icon(parent, IconId.Ball, Theme.Gold);
            UIKit.At((RectTransform)ball.transform, 0.5f, 0.5f, 560f * scale, 70f * scale, 110f * scale, 110f * scale);
        }

        public override void Tick(float dt)
        {
            _t += dt;
            Color c = _tap.color;
            c.a = 0.55f + Mathf.Sin(_t * 3.5f) * 0.45f;
            _tap.color = c;
            _logo.localScale = Vector3.one * (1f + Mathf.Sin(_t * 1.6f) * 0.012f);
        }

        public override bool OnBack() { return false; }
    }

    /// <summary>Main menu: the five game modes plus collection, shop, profile and settings.</summary>
    public class MainMenuScreen : UIScreen
    {
        protected override void OnBuild()
        {
            RectTransform logo = UIKit.Node(Root, "Logo");
            UIKit.At(logo, 0f, 1f, 50f, -20f, 760f, 270f);
            TitleScreen.BuildLogo(logo, 0.5f);

            Text c = UIKit.CoinChip(Root, Game.Economy.Coins);
            UIKit.At((RectTransform)c.transform.parent, 1f, 1f, -40f, -40f, 300f, 90f);

            RectTransform modes = UIKit.Node(Root, "Modes");
            UIKit.Region(modes, 0.03f, 0.2f, 0.97f, 0.7f);
            HorizontalLayoutGroup h = modes.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;

            ModeCard(modes, "PARTIDA RÁPIDA", "2 jugadores\nen este móvil", IconId.TwoPlayers, Theme.Orange, () => UI.Push(new MatchSetupScreen(GameMode.QuickMatch)));
            ModeCard(modes, "CONTRA LA MÁQUINA", "3 niveles\nde dificultad", IconId.Cpu, Theme.Cyan, () => UI.Push(new MatchSetupScreen(GameMode.VersusCpu)));
            ModeCard(modes, "TORNEO", "8 rivales\npremios y trofeo", IconId.Trophy, Theme.Gold, () => UI.Push(new TournamentScreen()));
            ModeCard(modes, "ENTRENAMIENTO", "5 ejercicios\ncon estrellas", IconId.Training, Theme.Success, () => UI.Push(new TrainingSelectScreen()));
            ModeCard(modes, "PERSONALIZADA", "puntos, sets,\npoderes, tiempo", IconId.Gear, new Color(0.75f, 0.5f, 1f), () => UI.Push(new MatchSetupScreen(GameMode.Custom)));

            RectTransform bottom = UIKit.Node(Root, "Bottom");
            UIKit.Region(bottom, 0.03f, 0.04f, 0.97f, 0.16f);
            HorizontalLayoutGroup b = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            b.spacing = 20f;
            b.childControlWidth = true;
            b.childControlHeight = true;
            b.childForceExpandWidth = true;
            b.childForceExpandHeight = true;
            UIKit.Button(bottom, "RAQUETAS", IconId.Racket, ButtonStyle.Secondary, () => UI.Push(new RacketSelectScreen(0)), 32);
            UIKit.Button(bottom, "MESAS", IconId.Table, ButtonStyle.Secondary, () => UI.Push(new TableSelectScreen()), 32);
            UIKit.Button(bottom, "TIENDA", IconId.Shop, ButtonStyle.Secondary, () => UI.Push(new ShopScreen()), 32);
            UIKit.Button(bottom, "PERFIL", IconId.Profile, ButtonStyle.Secondary, () => UI.Push(new ProfileScreen()), 32);
            UIKit.Button(bottom, "AJUSTES", IconId.Gear, ButtonStyle.Secondary, () => UI.Push(new SettingsScreen()), 32);
        }

        void ModeCard(RectTransform parent, string title, string subtitle, IconId icon, Color accent, System.Action onClick)
        {
            Button card = UIKit.Button(parent, "", null, ButtonStyle.Dark, onClick);
            RectTransform rt = (RectTransform)card.transform;
            Image bg = card.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.09f, 0.16f, 0.88f);
            UIKit.Outline(rt, new Color(accent.r, accent.g, accent.b, 0.55f), 26, 4);
            Image band = UIKit.Panel(rt, accent, 26, false, "Band");
            UIKit.Region((RectTransform)band.transform, 0f, 0.86f, 1f, 1f);
            Image ic = UIKit.Icon(rt, icon, accent);
            UIKit.At((RectTransform)ic.transform, 0.5f, 0.62f, 0f, 0f, 150f, 150f);
            Text t = UIKit.Heading(rt, title, 38, Theme.Text);
            UIKit.Region((RectTransform)t.transform, 0.04f, 0.2f, 0.96f, 0.4f);
            Text s = UIKit.Label(rt, subtitle, 26, Theme.TextDim, TextAnchor.UpperCenter, FontStyle.Normal);
            UIKit.Region((RectTransform)s.transform, 0.04f, 0.02f, 0.96f, 0.2f);
        }

        public override bool OnBack() { return false; }
    }
}

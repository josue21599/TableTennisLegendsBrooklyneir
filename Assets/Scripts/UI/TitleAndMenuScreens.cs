using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using Brooklyneir.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>Animated logo over the arena demo. Tap anywhere to continue.</summary>
    public class TitleScreen : UIScreen
    {
        TMP_Text _tap;
        RectTransform _logo;
        float _t;

        protected override void OnBuild()
        {
            _logo = UIKit.Node(Root, "Logo");
            UIKit.At(_logo, 0.5f, 0.5f, 0f, 120f, 1500f, 520f);
            BuildLogo(_logo, true);
            _tap = UIKit.Heading(Root, "TOCA PARA EMPEZAR", 48, Color.white);
            UIKit.At((RectTransform)_tap.transform, 0.5f, 0f, 0f, 150f, 1200f, 90f);
            TMP_Text v = UIKit.Label(Root, "v" + Application.version + "  ·  2 jugadores en un móvil  ·  sin conexión", 26, Theme.TextDim);
            UIKit.At((RectTransform)v.transform, 0.5f, 0f, 0f, 50f, 1400f, 50f);
            Button full = UIKit.Button(Root, "", null, ButtonStyle.Ghost, () => Game.Flow.GoToMenu());
            UIKit.Stretch((RectTransform)full.transform);
            full.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            Object.Destroy(full.GetComponent<ButtonFx>());
            Transform o = full.transform.Find("Outline");
            if (o != null) Object.Destroy(o.gameObject);
        }

        /// <summary>
        /// Three-line logo stacked from the font's real cap height (LogoLayout), not fixed
        /// fractions: the gap between lines and the plaque padding come from ui.json.
        /// Always the full sizes (96/128/64). centered = middle of the parent (title screen);
        /// otherwise the block hugs the parent's top-left corner (main menu).
        /// </summary>
        public static void BuildLogo(RectTransform parent, bool centered)
        {
            UiConfig cfg = UiFont.Config;
            const TextTier topTier = TextTier.Display, legendsTier = TextTier.Huge, plaqueTier = TextTier.Title;
            int topSize = cfg.SizeOf(topTier), legendsSize = cfg.SizeOf(legendsTier), plaqueSize = cfg.SizeOf(plaqueTier);

            float capRatio = LogoLayout.DefaultCapRatio;
            TMP_FontAsset font = UiFont.Font;
            if (font != null) capRatio = LogoLayout.CapRatio(font.faceInfo.capLine, font.faceInfo.pointSize);

            float parentW = parent.rect.width > 1f ? parent.rect.width : parent.sizeDelta.x;
            float parentH = parent.rect.height > 1f ? parent.rect.height : parent.sizeDelta.y;
            // parentH 0 = no vertical centring: the block starts at the top edge.
            LogoLayout.Result L = LogoLayout.Compute(capRatio, topSize, legendsSize, plaqueSize,
                cfg.logoLineGap, cfg.logoPlaquePadding, centered ? parentH : 0f);

            TMP_Text top = UIKit.Heading(parent, "TABLE TENNIS", topTier, Theme.Text);
            TMP_Text legends = UIKit.Heading(parent, "LEGENDS", legendsTier, Theme.Orange);
            Image strip = UIKit.Panel(parent, Theme.Cyan, 12, false, "Strip");
            TMP_Text brook = UIKit.Heading(strip.transform, "BROOKLYNEIR", plaqueTier, new Color(0.05f, 0.07f, 0.12f));
            UIKit.SingleLine(brook);
            brook.overflowMode = TextOverflowModes.Overflow;
            brook.alignment = TextAlignmentOptions.Midline;
            UIKit.Stretch((RectTransform)brook.transform);

            float padX = L.PlaqueCap * (0.6f + cfg.logoPlaquePadding * 2f);
            float brookW = brook.preferredWidth;
            float plaqueW = Mathf.Min(brookW > 1f ? brookW + 2f * padX : parentW * 0.6f, parentW);
            float topW = top.preferredWidth, legendsW = legends.preferredWidth;

            // Horizontal centre of the text column, measured from the parent's left edge.
            float columnW = Mathf.Max(plaqueW, Mathf.Max(topW, legendsW));
            float cx = centered || topW < 1f || legendsW < 1f ? parentW * 0.5f : columnW * 0.5f;

            PlaceAt((RectTransform)top.transform, cx, L.TopCenter, parentW, topSize * 1.3f);
            LineStyle(top);
            PlaceAt((RectTransform)legends.transform, cx, L.LegendsCenter, parentW, legendsSize * 1.3f);
            LineStyle(legends);
            PlaceAt((RectTransform)strip.transform, cx, L.PlaqueCenter, plaqueW, L.PlaqueHeight);

            float ballSize = L.LegendsCap * 0.85f;
            float ballDx = legendsW > 1f ? legendsW * 0.5f + ballSize * 0.75f : 560f;
            Image ball = UIKit.Icon(parent, IconId.Ball, Theme.Gold);
            PlaceAt((RectTransform)ball.transform, cx + ballDx, L.LegendsCenter - L.LegendsCap * 0.35f, ballSize, ballSize);
        }

        static void LineStyle(TMP_Text t)
        {
            UIKit.SingleLine(t);
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Midline;
        }

        /// <summary>Box of size w x h centred at (x from the left edge, y from the top edge) of its parent.</summary>
        static void PlaceAt(RectTransform rt, float x, float yFromTop, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -yFromTop);
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
            UIKit.At(logo, 0f, 1f, 40f, -30f, 900f, 290f); // top-left corner, same margin as the coin chip
            TitleScreen.BuildLogo(logo, false);

            TMP_Text c = UIKit.CoinChip(Root, Game.Economy.Coins);
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
            TMP_Text t = UIKit.Heading(rt, title, 38, Theme.Text);
            UIKit.Region((RectTransform)t.transform, 0.04f, 0.2f, 0.96f, 0.4f);
            TMP_Text s = UIKit.Label(rt, subtitle, 26, Theme.TextDim, TextAnchor.UpperCenter, FontStyle.Normal);
            UIKit.Region((RectTransform)s.transform, 0.04f, 0.02f, 0.96f, 0.2f);
        }

        public override bool OnBack() { return false; }
    }
}

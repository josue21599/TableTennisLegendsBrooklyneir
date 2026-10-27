using System.Collections.Generic;
using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using Brooklyneir.Progression;
using Brooklyneir.Rackets;
using Brooklyneir.Tables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>Racket collection: 3D preview, comparable stats, buy / equip for each player.</summary>
    public class RacketSelectScreen : UIScreen
    {
        int _player;
        string _selected;
        RectTransform _left;
        Button _action;
        readonly List<KeyValuePair<string, Image>> _cards = new List<KeyValuePair<string, Image>>();

        public RacketSelectScreen(int player, string focusId = null)
        {
            _player = player;
            _selected = focusId;
        }

        string Equipped { get { return _player == 0 ? Game.Save.Data.racketP1 : Game.Save.Data.racketP2; } }

        protected override void OnBuild()
        {
            _cards.Clear();
            if (string.IsNullOrEmpty(_selected)) _selected = Equipped;
            Header("RAQUETAS");
            RectTransform tabs = UIKit.Node(Root, "PlayerTabs");
            UIKit.At(tabs, 0.5f, 1f, 160f, -40f, 520f, 90f);
            UIKit.Segmented(tabs, "", new[] { "JUGADOR 1", "JUGADOR 2" }, _player, i =>
            {
                _player = i;
                _selected = Equipped;
                RefreshAll();
            });

            _left = UIKit.Node(Root, "Detail");
            UIKit.Region(_left, 0.03f, 0.04f, 0.44f, 0.85f);

            RectTransform listArea = UIKit.Node(Root, "List");
            UIKit.Region(listArea, 0.46f, 0.18f, 0.97f, 0.85f);
            RectTransform content;
            ScrollRect sr = UIKit.Scroll(listArea, out content);
            UIKit.Stretch((RectTransform)sr.transform);
            foreach (RacketDefinition def in RacketCatalog.All) Card(content, def);

            _action = UIKit.Button(Root, "EQUIPAR", IconId.Check, ButtonStyle.Primary, OnAction, 42);
            UIKit.At((RectTransform)_action.transform, 1f, 0f, -60f, 30f, 560f, 120f);
            RefreshAll();
        }

        void Card(RectTransform parent, RacketDefinition def)
        {
            Button b = UIKit.Button(parent, "", null, ButtonStyle.Dark, () =>
            {
                _selected = def.Id;
                RefreshAll();
            });
            RectTransform rt = UIKit.Height((RectTransform)b.transform, 120f);
            Image bg = b.GetComponent<Image>();
            _cards.Add(new KeyValuePair<string, Image>(def.Id, bg));
            Image sw = UIKit.Panel(rt, ProceduralTextures.Hex(def.ForehandRubber), 36, false, "Rubber");
            UIKit.At((RectTransform)sw.transform, 0f, 0.5f, 24f, 0f, 86f, 86f);
            Image half = UIKit.Panel(sw.transform, ProceduralTextures.Hex(def.BackhandRubber), 36, false, "Back");
            UIKit.Region((RectTransform)half.transform, 0.5f, 0f, 1f, 1f);
            TMP_Text n = UIKit.Heading(rt, def.Name.ToUpperInvariant(), 36, Theme.Text, TextAnchor.LowerLeft);
            UIKit.Region((RectTransform)n.transform, 0.2f, 0.45f, 0.72f, 0.92f);
            TMP_Text r = UIKit.Label(rt, RacketCatalog.RarityName(def.Rarity) + "  ·  " + def.Style.ToUpperInvariant(), 24, Theme.RarityColor(def.Rarity), TextAnchor.UpperLeft);
            UIKit.Region((RectTransform)r.transform, 0.2f, 0.08f, 0.72f, 0.45f);
            bool owned = Game.Economy.OwnsRacket(def.Id);
            TMP_Text status = UIKit.Label(rt, owned ? "" : def.Price.ToString("N0"), 32, Theme.Gold, TextAnchor.MiddleRight);
            UIKit.Region((RectTransform)status.transform, 0.72f, 0f, 0.9f, 1f);
            Image ic = UIKit.Icon(rt, owned ? IconId.Check : IconId.Coin, owned ? Theme.Success : Theme.Gold);
            UIKit.At((RectTransform)ic.transform, 1f, 0.5f, -24f, 0f, 50f, 50f);
        }

        void RefreshAll()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool sel = _cards[i].Key == _selected;
                bool eq = _cards[i].Key == Equipped;
                _cards[i].Value.color = sel ? new Color(0.2f, 0.27f, 0.45f, 0.95f) : (eq ? new Color(0.1f, 0.2f, 0.16f, 0.9f) : new Color(0.07f, 0.09f, 0.16f, 0.88f));
            }
            for (int i = _left.childCount - 1; i >= 0; i--) Object.Destroy(_left.GetChild(i).gameObject);
            RacketDefinition def = RacketCatalog.Get(_selected);
            RacketDefinition eqDef = RacketCatalog.Get(Equipped);

            RawImage preview = UIKit.Node(_left, "Preview").gameObject.AddComponent<RawImage>();
            UIKit.Region((RectTransform)preview.transform, 0.12f, 0.5f, 0.88f, 1f);
            ((RectTransform)preview.transform).GetComponent<RawImage>().texture = Game.Preview.Texture;
            preview.raycastTarget = false;
            Game.Preview.ShowRacket(def);
            AspectRatioFitter fit = preview.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;

            TMP_Text n = UIKit.Heading(_left, def.Name.ToUpperInvariant(), 50, Theme.Text, TextAnchor.MiddleLeft);
            UIKit.Region((RectTransform)n.transform, 0f, 0.42f, 1f, 0.5f);
            TMP_Text r = UIKit.Label(_left, RacketCatalog.RarityName(def.Rarity) + "  ·  " + def.Description, 26, Theme.RarityColor(def.Rarity), TextAnchor.UpperLeft, FontStyle.Normal);
            UIKit.Region((RectTransform)r.transform, 0f, 0.34f, 1f, 0.42f);

            string[] labels = { "POTENCIA", "CONTROL", "EFECTO", "RESPUESTA", "PRECISIÓN" };
            int[] mine = { def.Stats.power, def.Stats.control, def.Stats.spin, def.Stats.response, def.Stats.precision };
            int[] theirs = { eqDef.Stats.power, eqDef.Stats.control, eqDef.Stats.spin, eqDef.Stats.response, eqDef.Stats.precision };
            for (int i = 0; i < 5; i++)
            {
                float y0 = 0.27f - i * 0.065f;
                TMP_Text l = UIKit.Label(_left, labels[i], 24, Theme.TextDim, TextAnchor.MiddleLeft);
                UIKit.Region((RectTransform)l.transform, 0f, y0, 0.3f, y0 + 0.055f);
                Color c = mine[i] > theirs[i] ? Theme.Success : (mine[i] < theirs[i] ? Theme.Danger : Theme.Orange);
                Image bar = UIKit.Bar(_left, mine[i] / 10f, c, theirs[i] / 10f);
                UIKit.Region((RectTransform)bar.transform.parent, 0.31f, y0 + 0.012f, 0.88f, y0 + 0.043f);
                TMP_Text v = UIKit.Label(_left, mine[i].ToString(), 26, Theme.Text, TextAnchor.MiddleRight);
                UIKit.Region((RectTransform)v.transform, 0.88f, y0, 1f, y0 + 0.055f);
            }

            bool owned = Game.Economy.OwnsRacket(def.Id);
            bool equipped = def.Id == Equipped;
            Image abg = _action.GetComponent<Image>();
            if (equipped) { UIKit.SetButtonLabel(_action, "EQUIPADA"); abg.color = Theme.PanelLighter; }
            else if (owned) { UIKit.SetButtonLabel(_action, "EQUIPAR J" + (_player + 1)); abg.color = Theme.Orange; }
            else if (def.Unlock == UnlockRule.Coins) { UIKit.SetButtonLabel(_action, "COMPRAR " + def.Price.ToString("N0")); abg.color = Game.Economy.CanAfford(def.Price) ? Theme.Gold : Theme.PanelLighter; }
            else { UIKit.SetButtonLabel(_action, "GANA UN TORNEO"); abg.color = Theme.PanelLighter; }
            RefreshCoins();
        }

        void OnAction()
        {
            RacketDefinition def = RacketCatalog.Get(_selected);
            if (def.Id == Equipped) return;
            if (Game.Economy.OwnsRacket(def.Id))
            {
                if (_player == 0) Game.Save.Data.racketP1 = def.Id; else Game.Save.Data.racketP2 = def.Id;
                Game.Save.Save();
                UI.Toast(def.Name + " equipada para el jugador " + (_player + 1), Theme.Success);
                Game.Flow.RefreshBackground();
                RefreshAll();
                return;
            }
            if (def.Unlock != UnlockRule.Coins) { UI.Toast("Se desbloquea ganando un torneo", Theme.Gold); return; }
            if (!Game.Economy.CanAfford(def.Price))
            {
                Game.Audio.Play(Sfx.UiError, 0.8f);
                UI.Toast("Te faltan " + (def.Price - Game.Economy.Coins).ToString("N0") + " monedas", Theme.Danger);
                return;
            }
            UI.Confirm("COMPRAR RAQUETA", "¿Comprar " + def.Name + " por " + def.Price.ToString("N0") + " monedas?", "COMPRAR", () =>
            {
                PurchaseResult r = Game.Economy.BuyRacket(def.Id);
                if (r == PurchaseResult.Success)
                {
                    Game.Save.Save();
                    Game.Audio.Play(Sfx.Unlock, 1f);
                    UI.Toast("¡" + def.Name + " desbloqueada!", Theme.Gold);
                    Achievements.Evaluate(Game.Save.Data, Game.Economy);
                    Game.Save.Save();
                }
                else UI.Toast("No se pudo comprar (" + r + ")", Theme.Danger);
                RebuildList();
            });
        }

        void RebuildList()
        {
            UI.Pop();
            UI.Push(new RacketSelectScreen(_player, _selected));
        }

        public override void OnHide()
        {
            if (Game.Preview != null) Game.Preview.Clear();
        }
    }

    /// <summary>Tables and scenarios: preview, description, buy / select.</summary>
    public class TableSelectScreen : UIScreen
    {
        string _selected;
        RectTransform _left;
        Button _action;
        readonly List<KeyValuePair<string, Image>> _cards = new List<KeyValuePair<string, Image>>();

        public TableSelectScreen(string focusId = null)
        {
            _selected = focusId;
        }

        protected override void OnBuild()
        {
            _cards.Clear();
            if (string.IsNullOrEmpty(_selected)) _selected = Game.Save.Data.selectedTable;
            Header("MESAS Y ESCENARIOS");
            _left = UIKit.Node(Root, "Detail");
            UIKit.Region(_left, 0.03f, 0.04f, 0.44f, 0.85f);
            RectTransform listArea = UIKit.Node(Root, "List");
            UIKit.Region(listArea, 0.46f, 0.18f, 0.97f, 0.85f);
            RectTransform content;
            ScrollRect sr = UIKit.Scroll(listArea, out content);
            UIKit.Stretch((RectTransform)sr.transform);
            foreach (TableDefinition def in TableCatalog.All) Card(content, def);
            _action = UIKit.Button(Root, "ELEGIR", IconId.Check, ButtonStyle.Primary, OnAction, 42);
            UIKit.At((RectTransform)_action.transform, 1f, 0f, -60f, 30f, 560f, 120f);
            Refresh();
        }

        void Card(RectTransform parent, TableDefinition def)
        {
            Button b = UIKit.Button(parent, "", null, ButtonStyle.Dark, () =>
            {
                _selected = def.Id;
                Refresh();
            });
            RectTransform rt = UIKit.Height((RectTransform)b.transform, 120f);
            _cards.Add(new KeyValuePair<string, Image>(def.Id, b.GetComponent<Image>()));
            Image sw = UIKit.Panel(rt, ProceduralTextures.Hex(def.TopColor), 20, false, "Top");
            UIKit.At((RectTransform)sw.transform, 0f, 0.5f, 24f, 0f, 130f, 80f);
            Image line = UIKit.Panel(sw.transform, ProceduralTextures.Hex(def.LineColor), 2, false, "Line");
            UIKit.Region((RectTransform)line.transform, 0.48f, 0.08f, 0.52f, 0.92f);
            TMP_Text n = UIKit.Heading(rt, def.Name.ToUpperInvariant(), 36, Theme.Text, TextAnchor.LowerLeft);
            UIKit.Region((RectTransform)n.transform, 0.25f, 0.45f, 0.72f, 0.92f);
            TMP_Text d = UIKit.Label(rt, def.Description, 22, Theme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            UIKit.Region((RectTransform)d.transform, 0.25f, 0.06f, 0.75f, 0.45f);
            bool owned = Game.Economy.OwnsTable(def.Id);
            string status = owned ? "" : (def.Unlock == UnlockRule.Coins ? def.Price.ToString("N0") : "TORNEO");
            TMP_Text st = UIKit.Label(rt, status, 30, Theme.Gold, TextAnchor.MiddleRight);
            UIKit.Region((RectTransform)st.transform, 0.72f, 0f, 0.9f, 1f);
            Image ic = UIKit.Icon(rt, owned ? IconId.Check : (def.Unlock == UnlockRule.Coins ? IconId.Coin : IconId.Lock), owned ? Theme.Success : Theme.Gold);
            UIKit.At((RectTransform)ic.transform, 1f, 0.5f, -24f, 0f, 50f, 50f);
        }

        void Refresh()
        {
            string current = Game.Save.Data.selectedTable;
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].Value.color = _cards[i].Key == _selected ? new Color(0.2f, 0.27f, 0.45f, 0.95f)
                    : (_cards[i].Key == current ? new Color(0.1f, 0.2f, 0.16f, 0.9f) : new Color(0.07f, 0.09f, 0.16f, 0.88f));
            for (int i = _left.childCount - 1; i >= 0; i--) Object.Destroy(_left.GetChild(i).gameObject);
            TableDefinition def = TableCatalog.Get(_selected);
            RawImage preview = UIKit.Node(_left, "Preview").gameObject.AddComponent<RawImage>();
            UIKit.Region((RectTransform)preview.transform, 0.05f, 0.32f, 0.95f, 1f);
            preview.texture = Game.Preview.Texture;
            preview.raycastTarget = false;
            AspectRatioFitter fit = preview.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            Game.Preview.ShowTable(def);
            TMP_Text n = UIKit.Heading(_left, def.Name.ToUpperInvariant(), 50, Theme.Text, TextAnchor.MiddleLeft);
            UIKit.Region((RectTransform)n.transform, 0f, 0.22f, 1f, 0.32f);
            TMP_Text d = UIKit.Label(_left, def.Description + "\nMismas medidas y física en todas las mesas.", 28, Theme.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            UIKit.Region((RectTransform)d.transform, 0f, 0.05f, 1f, 0.22f);

            bool owned = Game.Economy.OwnsTable(def.Id);
            Image abg = _action.GetComponent<Image>();
            if (def.Id == current) { UIKit.SetButtonLabel(_action, "SELECCIONADA"); abg.color = Theme.PanelLighter; }
            else if (owned) { UIKit.SetButtonLabel(_action, "ELEGIR"); abg.color = Theme.Orange; }
            else if (def.Unlock == UnlockRule.Coins) { UIKit.SetButtonLabel(_action, "COMPRAR " + def.Price.ToString("N0")); abg.color = Game.Economy.CanAfford(def.Price) ? Theme.Gold : Theme.PanelLighter; }
            else { UIKit.SetButtonLabel(_action, "GANA UN TORNEO"); abg.color = Theme.PanelLighter; }
            RefreshCoins();
        }

        void OnAction()
        {
            TableDefinition def = TableCatalog.Get(_selected);
            if (def.Id == Game.Save.Data.selectedTable) return;
            if (Game.Economy.OwnsTable(def.Id))
            {
                Game.Save.Data.selectedTable = def.Id;
                Game.Save.Save();
                UI.Toast(def.Name + " seleccionada", Theme.Success);
                Game.Flow.RefreshBackground();
                Refresh();
                return;
            }
            if (def.Unlock != UnlockRule.Coins) { UI.Toast("Gana un torneo para desbloquearla", Theme.Gold); return; }
            if (!Game.Economy.CanAfford(def.Price))
            {
                Game.Audio.Play(Sfx.UiError, 0.8f);
                UI.Toast("Te faltan " + (def.Price - Game.Economy.Coins).ToString("N0") + " monedas", Theme.Danger);
                return;
            }
            UI.Confirm("COMPRAR MESA", "¿Comprar " + def.Name + " por " + def.Price.ToString("N0") + " monedas?", "COMPRAR", () =>
            {
                PurchaseResult r = Game.Economy.BuyTable(def.Id);
                if (r == PurchaseResult.Success)
                {
                    Game.Save.Save();
                    Game.Audio.Play(Sfx.Unlock, 1f);
                    UI.Toast("¡" + def.Name + " desbloqueada!", Theme.Gold);
                }
                else UI.Toast("No se pudo comprar (" + r + ")", Theme.Danger);
                string keep = _selected;
                UI.Pop();
                UI.Push(new TableSelectScreen(keep));
            });
        }

        public override void OnHide()
        {
            if (Game.Preview != null) Game.Preview.Clear();
        }
    }

    /// <summary>Shop: everything that can still be unlocked, with prices; opens the item's screen.</summary>
    public class ShopScreen : UIScreen
    {
        protected override void OnBuild()
        {
            Header("TIENDA");
            TMP_Text info = UIKit.Label(Root, "Gana monedas jugando partidos, torneos, retos de entrenamiento y logros. Sin compras reales.", 28, Theme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            UIKit.Region((RectTransform)info.transform, 0.04f, 0.77f, 0.96f, 0.85f);
            RectTransform area = UIKit.Node(Root, "Items");
            UIKit.Region(area, 0.03f, 0.04f, 0.97f, 0.76f);
            RectTransform content;
            ScrollRect sr = UIKit.Scroll(area, out content);
            UIKit.Stretch((RectTransform)sr.transform);
            int shown = 0;
            foreach (RacketDefinition r in RacketCatalog.All)
            {
                if (Game.Economy.OwnsRacket(r.Id)) continue;
                string id = r.Id;
                Item(content, "RAQUETA", r.Name, RacketCatalog.RarityName(r.Rarity), r.Price, ProceduralTextures.Hex(r.ForehandRubber), IconId.Racket, () => UI.Push(new RacketSelectScreen(0, id)));
                shown++;
            }
            foreach (TableDefinition t in TableCatalog.All)
            {
                if (Game.Economy.OwnsTable(t.Id)) continue;
                string id = t.Id;
                Item(content, "MESA", t.Name, t.Unlock == UnlockRule.Coins ? "ESCENARIO" : "PREMIO DE TORNEO", t.Unlock == UnlockRule.Coins ? t.Price : -1,
                    ProceduralTextures.Hex(t.TopColor), IconId.Table, () => UI.Push(new TableSelectScreen(id)));
                shown++;
            }
            if (shown == 0)
            {
                TMP_Text done = UIKit.Heading(content, "¡LO TIENES TODO!", 48, Theme.Gold);
                UIKit.Height((RectTransform)done.transform, 140f);
            }
        }

        void Item(RectTransform parent, string kind, string name, string tag, int price, Color color, IconId icon, System.Action open)
        {
            Button b = UIKit.Button(parent, "", null, ButtonStyle.Dark, open);
            RectTransform rt = UIKit.Height((RectTransform)b.transform, 120f);
            Image sw = UIKit.Panel(rt, color, 30, false, "Swatch");
            UIKit.At((RectTransform)sw.transform, 0f, 0.5f, 24f, 0f, 90f, 90f);
            Image ic = UIKit.Icon(sw.transform, icon, Color.white);
            UIKit.Stretch((RectTransform)ic.transform, 16f, 16f, 16f, 16f);
            TMP_Text k = UIKit.Label(rt, kind + "  ·  " + tag, 24, Theme.TextDim, TextAnchor.UpperLeft);
            UIKit.Region((RectTransform)k.transform, 0.12f, 0.1f, 0.7f, 0.45f);
            TMP_Text n = UIKit.Heading(rt, name.ToUpperInvariant(), 38, Theme.Text, TextAnchor.LowerLeft);
            UIKit.Region((RectTransform)n.transform, 0.12f, 0.45f, 0.7f, 0.92f);
            bool afford = price >= 0 && Game.Economy.CanAfford(price);
            TMP_Text p = UIKit.Label(rt, price >= 0 ? price.ToString("N0") : "TORNEO", 34, afford ? Theme.Gold : Theme.TextDim, TextAnchor.MiddleRight);
            UIKit.Region((RectTransform)p.transform, 0.7f, 0f, 0.92f, 1f);
            Image c = UIKit.Icon(rt, price >= 0 ? IconId.Coin : IconId.Lock, afford ? Theme.Gold : Theme.TextDim);
            UIKit.At((RectTransform)c.transform, 1f, 0.5f, -24f, 0f, 54f, 54f);
        }
    }
}

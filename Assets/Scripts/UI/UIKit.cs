using System;
using System.Collections.Generic;
using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    public enum ButtonStyle { Primary, Secondary, Ghost, Danger, Gold, Cyan, Dark }

    /// <summary>Visual identity: dark night-court blues with Brooklyn orange and neon cyan accents.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("#0B1020");
        public static readonly Color Panel = Hex("#151D33");
        public static readonly Color PanelLight = Hex("#1F2A47");
        public static readonly Color PanelLighter = Hex("#2A3860");
        public static readonly Color Orange = Hex("#FF7A1A");
        public static readonly Color OrangeDark = Hex("#C4520A");
        public static readonly Color Cyan = Hex("#22C3FF");
        public static readonly Color Gold = Hex("#F5C542");
        public static readonly Color Text = Hex("#F4F6FB");
        public static readonly Color TextDim = Hex("#9AA6C1");
        public static readonly Color Danger = Hex("#FF4D5E");
        public static readonly Color Success = Hex("#3DDC84");
        public static readonly Color Overlay = new Color(0.02f, 0.03f, 0.07f, 0.78f);

        public static Color Hex(string h)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(h, out c) ? c : Color.magenta;
        }

        public static Color RarityColor(Brooklyneir.Rackets.Rarity r)
        {
            switch (r)
            {
                case Brooklyneir.Rackets.Rarity.Rare: return Hex("#3FA9FF");
                case Brooklyneir.Rackets.Rarity.Epic: return Hex("#B36BFF");
                case Brooklyneir.Rackets.Rarity.Legendary: return Gold;
                default: return Hex("#B9C3D6");
            }
        }
    }

    /// <summary>Small UI construction kit (uGUI built from code; no prefabs needed).</summary>
    public static class UIKit
    {
        /// <summary>The single interface font instance (see UiFont).</summary>
        public static TMP_FontAsset Font { get { return UiFont.Font; } }

        /// <summary>TextAnchor (used by the screens) to TextMeshPro alignment.</summary>
        public static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        public static RectTransform Node(Transform parent, string name)
        {
            GameObject g = new GameObject(name, typeof(RectTransform));
            g.layer = 5; // UI
            RectTransform rt = (RectTransform)g.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors a fixed-size rect at a normalised point of its parent (pivot = anchor).</summary>
        public static RectTransform At(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Region(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(Transform parent, Color color, int radius = 28, bool raycast = false, string name = "Panel")
        {
            RectTransform rt = Node(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.RoundedPanel(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Outline(Transform parent, Color color, int radius = 28, int width = 4)
        {
            RectTransform rt = Node(parent, "Outline");
            Stretch(rt);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.RoundedPanel(radius, width);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// TMP_Text with the interface font at one of the four fixed sizes. <paramref name="size"/> is a
        /// hint mapped to a tier (see UiConfig), so no free sizes reach the raster font. The style
        /// argument is kept for the screens' calls but ignored: faux bold/italic would distort the
        /// pixel font.
        /// </summary>
        public static TMP_Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            return Label(parent, text, UiFont.Config.TierForHint(size), color, align);
        }

        public static TMP_Text Label(Transform parent, string text, TextTier tier, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            RectTransform rt = Node(parent, "Label");
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset f = Font;
            if (f != null) t.font = f;
            t.fontSize = UiFont.Size(tier);
            t.fontStyle = FontStyles.Normal;
            t.text = text;
            t.color = color;
            t.alignment = Align(align);
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.enableAutoSizing = false;
            t.richText = true;
            return t;
        }

        /// <summary>Heading: same font, larger tier, with a crisp pixel drop shadow (no italic).</summary>
        public static TMP_Text Heading(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            return Heading(parent, text, UiFont.Config.TierForHint(size), color, align);
        }

        public static TMP_Text Heading(Transform parent, string text, TextTier tier, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            TMP_Text t = Label(parent, text, tier, color, align);
            t.gameObject.name = "Heading";
            PixelShadow.Attach(t, UiFont.Config.shadowOffsetPx, UiFont.Config.shadowAlpha);
            return t;
        }

        /// <summary>Texts in fixed boxes (buttons): wrap, and end with an ellipsis instead of spilling out.</summary>
        public static TMP_Text FitBox(TMP_Text t)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// <summary>Single-line texts in fixed-height boxes: cut with an ellipsis instead of spilling.</summary>
        public static TMP_Text SingleLine(TMP_Text t)
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public static Image Icon(Transform parent, IconId id, Color color)
        {
            RectTransform rt = Node(parent, "Icon_" + id);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.Get(id);
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        static Color StyleColor(ButtonStyle s)
        {
            switch (s)
            {
                case ButtonStyle.Primary: return Theme.Orange;
                case ButtonStyle.Secondary: return Theme.PanelLighter;
                case ButtonStyle.Ghost: return new Color(1f, 1f, 1f, 0.06f);
                case ButtonStyle.Danger: return Theme.Danger;
                case ButtonStyle.Gold: return Theme.Gold;
                case ButtonStyle.Cyan: return Theme.Cyan;
                default: return Theme.Panel;
            }
        }

        static Color StyleText(ButtonStyle s)
        {
            return s == ButtonStyle.Gold || s == ButtonStyle.Cyan ? new Color(0.07f, 0.08f, 0.12f) : Theme.Text;
        }

        /// <summary>Rounded button with optional icon, press animation and click sound.</summary>
        public static Button Button(Transform parent, string label, IconId? icon, ButtonStyle style, Action onClick, int fontSize = 34, Sfx sound = Sfx.UiClick)
        {
            RectTransform rt = Node(parent, "Button_" + label);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = IconFactory.RoundedPanel(26);
            bg.type = Image.Type.Sliced;
            bg.color = StyleColor(style);
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.fadeDuration = 0.06f;
            b.colors = cb;
            if (style == ButtonStyle.Ghost) Outline(rt, new Color(1f, 1f, 1f, 0.25f), 26, 3);
            // Lower edge for depth (drawn beneath the button by the Shadow mesh effect).
            if (style != ButtonStyle.Ghost)
            {
                Shadow edge = rt.gameObject.AddComponent<Shadow>();
                edge.effectColor = style == ButtonStyle.Primary ? Theme.OrangeDark : new Color(0f, 0f, 0f, 0.45f);
                edge.effectDistance = new Vector2(0f, -6f);
            }
            Color tc = StyleText(style);
            if (icon.HasValue && !string.IsNullOrEmpty(label))
            {
                Image ic = Icon(rt, icon.Value, tc);
                At((RectTransform)ic.transform, 0f, 0.5f, 26f, 0f, fontSize * 1.4f, fontSize * 1.4f);
                TMP_Text t = FitBox(Label(rt, label, fontSize, tc));
                Stretch((RectTransform)t.transform, fontSize * 1.4f + 34f, 18f, 0f, 0f);
                t.alignment = TextAlignmentOptions.Left;
            }
            else if (icon.HasValue)
            {
                Image ic = Icon(rt, icon.Value, tc);
                Stretch((RectTransform)ic.transform, 16f, 16f, 16f, 16f);
            }
            else
            {
                TMP_Text t = FitBox(Label(rt, label, fontSize, tc));
                Stretch((RectTransform)t.transform, 12f, 12f, 0f, 0f);
            }
            rt.gameObject.AddComponent<ButtonFx>();
            if (onClick != null)
                b.onClick.AddListener(() =>
                {
                    if (Game.Audio != null) Game.Audio.Play(sound, 0.8f);
                    onClick();
                });
            return b;
        }

        public static void SetButtonLabel(Button b, string text)
        {
            TMP_Text t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = text;
        }

        /// <summary>Horizontal slider 0..1 with a label and value text.</summary>
        public static Slider Slider(Transform parent, string label, float value, Action<float> onChange, Func<float, string> format = null)
        {
            RectTransform row = Node(parent, "Slider_" + label);
            Stretch(row);
            TMP_Text l = Label(row, label, 32, Theme.Text, TextAnchor.MiddleLeft);
            Region((RectTransform)l.transform, 0f, 0f, 0.34f, 1f);
            TMP_Text v = Label(row, "", 30, Theme.TextDim, TextAnchor.MiddleRight);
            Region((RectTransform)v.transform, 0.86f, 0f, 1f, 1f);

            RectTransform area = Node(row, "Track");
            Region(area, 0.36f, 0.3f, 0.84f, 0.7f);
            Image track = area.gameObject.AddComponent<Image>();
            track.sprite = IconFactory.RoundedPanel(12);
            track.type = Image.Type.Sliced;
            track.color = Theme.PanelLighter;

            RectTransform fillArea = Node(area, "FillArea");
            Stretch(fillArea);
            RectTransform fill = Node(fillArea, "Fill");
            Stretch(fill);
            Image fi = fill.gameObject.AddComponent<Image>();
            fi.sprite = IconFactory.RoundedPanel(12);
            fi.type = Image.Type.Sliced;
            fi.color = Theme.Orange;

            RectTransform handleArea = Node(area, "HandleArea");
            Stretch(handleArea);
            RectTransform handle = Node(handleArea, "Handle");
            handle.sizeDelta = new Vector2(56f, 56f);
            Image hi = handle.gameObject.AddComponent<Image>();
            hi.sprite = IconFactory.CircleSprite();
            hi.color = Color.white;

            Slider s = row.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = hi;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = value;
            Func<float, string> fmt = format ?? (x => Mathf.RoundToInt(x * 100f) + "%");
            v.text = fmt(value);
            s.onValueChanged.AddListener(x =>
            {
                v.text = fmt(x);
                if (onChange != null) onChange(x);
            });
            return s;
        }

        /// <summary>On/off pill switch with a label.</summary>
        public static Button Toggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            RectTransform row = Node(parent, "Toggle_" + label);
            Stretch(row);
            TMP_Text l = Label(row, label, 32, Theme.Text, TextAnchor.MiddleLeft);
            Region((RectTransform)l.transform, 0f, 0f, 0.7f, 1f);
            RectTransform pill = Node(row, "Pill");
            At(pill, 1f, 0.5f, 0f, 0f, 130f, 62f);
            Image bg = pill.gameObject.AddComponent<Image>();
            bg.sprite = IconFactory.RoundedPanel(31);
            bg.type = Image.Type.Sliced;
            RectTransform knob = Node(pill, "Knob");
            Image k = knob.gameObject.AddComponent<Image>();
            k.sprite = IconFactory.CircleSprite();
            k.color = Color.white;
            k.raycastTarget = false;
            Button b = pill.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            bool state = value;
            Action refresh = () =>
            {
                bg.color = state ? Theme.Success : Theme.PanelLighter;
                At(knob, state ? 1f : 0f, 0.5f, state ? -6f : 6f, 0f, 50f, 50f);
            };
            refresh();
            b.onClick.AddListener(() =>
            {
                state = !state;
                refresh();
                if (Game.Audio != null) Game.Audio.Play(Sfx.UiClick, 0.7f);
                if (onChange != null) onChange(state);
            });
            pill.gameObject.AddComponent<ButtonFx>();
            return b;
        }

        /// <summary>Row of mutually exclusive options.</summary>
        public static List<Button> Segmented(Transform parent, string label, string[] options, int selected, Action<int> onChange)
        {
            RectTransform row = Node(parent, "Segmented_" + label);
            Stretch(row);
            float labelShare = string.IsNullOrEmpty(label) ? 0f : 0.34f;
            if (labelShare > 0f)
            {
                TMP_Text l = Label(row, label, 32, Theme.Text, TextAnchor.MiddleLeft);
                Region((RectTransform)l.transform, 0f, 0f, labelShare, 1f);
            }
            RectTransform box = Node(row, "Options");
            Region(box, labelShare, 0.05f, 1f, 0.95f);
            HorizontalLayoutGroup h = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            h.childControlWidth = true;
            h.childControlHeight = true;
            List<Button> buttons = new List<Button>();
            int current = selected;
            Action refresh = null;
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                Button b = Button(box, options[i], null, ButtonStyle.Secondary, () =>
                {
                    current = idx;
                    refresh();
                    if (onChange != null) onChange(idx);
                }, 28);
                buttons.Add(b);
            }
            refresh = () =>
            {
                for (int i = 0; i < buttons.Count; i++)
                {
                    Image img = buttons[i].GetComponent<Image>();
                    img.color = i == current ? Theme.Orange : Theme.PanelLighter;
                }
            };
            refresh();
            return buttons;
        }

        /// <summary>"- value +" control cycling through a list of values.</summary>
        public static TMP_Text Stepper(Transform parent, string label, string[] values, int index, Action<int> onChange)
        {
            RectTransform row = Node(parent, "Stepper_" + label);
            Stretch(row);
            TMP_Text l = Label(row, label, 32, Theme.Text, TextAnchor.MiddleLeft);
            Region((RectTransform)l.transform, 0f, 0f, 0.4f, 1f);
            TMP_Text v = Label(row, values[Mathf.Clamp(index, 0, values.Length - 1)], 34, Theme.Gold);
            Region((RectTransform)v.transform, 0.55f, 0f, 0.85f, 1f);
            int current = Mathf.Clamp(index, 0, values.Length - 1);
            Button minus = Button(row, "", IconId.Minus, ButtonStyle.Secondary, () =>
            {
                current = Mathf.Max(0, current - 1);
                v.text = values[current];
                if (onChange != null) onChange(current);
            });
            Region((RectTransform)minus.transform, 0.42f, 0.08f, 0.53f, 0.92f);
            Button plus = Button(row, "", IconId.Plus, ButtonStyle.Secondary, () =>
            {
                current = Mathf.Min(values.Length - 1, current + 1);
                v.text = values[current];
                if (onChange != null) onChange(current);
            });
            Region((RectTransform)plus.transform, 0.87f, 0.08f, 0.98f, 0.92f);
            return v;
        }

        public static ScrollRect Scroll(Transform parent, out RectTransform content, bool vertical = true)
        {
            RectTransform view = Node(parent, "Scroll");
            Image mask = view.gameObject.AddComponent<Image>();
            mask.color = new Color(1f, 1f, 1f, 0.003f);
            view.gameObject.AddComponent<RectMask2D>();
            content = Node(view, "Content");
            if (vertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 14f;
                v.padding = new RectOffset(6, 6, 6, 6);
                v.childControlHeight = false;
                v.childControlWidth = true;
                v.childForceExpandWidth = true;
                v.childForceExpandHeight = false;
                ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
                f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                HorizontalLayoutGroup h = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.spacing = 16f;
                h.padding = new RectOffset(6, 6, 6, 6);
                h.childControlWidth = false;
                h.childControlHeight = true;
                h.childForceExpandHeight = true;
                h.childForceExpandWidth = false;
                ContentSizeFitter f = content.gameObject.AddComponent<ContentSizeFitter>();
                f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            content.sizeDelta = Vector2.zero;
            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.horizontal = !vertical;
            sr.vertical = vertical;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30f;
            sr.viewport = view;
            return sr;
        }

        /// <summary>Gives a layout child a fixed preferred height (for vertical lists).</summary>
        public static RectTransform Height(RectTransform rt, float h)
        {
            LayoutElement le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            return rt;
        }

        public static RectTransform Width(RectTransform rt, float w)
        {
            LayoutElement le = rt.GetComponent<LayoutElement>();
            if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.minWidth = w;
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
            return rt;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing, int padding = 0)
        {
            VerticalLayoutGroup v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlHeight = false;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        /// <summary>Progress bar (0..1) with optional comparison ghost bar.</summary>
        public static Image Bar(Transform parent, float value, Color color, float ghost = -1f)
        {
            RectTransform track = Node(parent, "Bar");
            Image bg = track.gameObject.AddComponent<Image>();
            bg.sprite = IconFactory.RoundedPanel(10);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.08f);
            bg.raycastTarget = false;
            if (ghost >= 0f)
            {
                RectTransform g = Node(track, "Ghost");
                Region(g, 0f, 0f, Mathf.Clamp01(ghost), 1f);
                Image gi = g.gameObject.AddComponent<Image>();
                gi.sprite = IconFactory.RoundedPanel(10);
                gi.type = Image.Type.Sliced;
                gi.color = new Color(1f, 1f, 1f, 0.22f);
                gi.raycastTarget = false;
            }
            RectTransform f = Node(track, "Fill");
            Region(f, 0f, 0f, Mathf.Clamp01(value), 1f);
            Image fi = f.gameObject.AddComponent<Image>();
            fi.sprite = IconFactory.RoundedPanel(10);
            fi.type = Image.Type.Sliced;
            fi.color = color;
            fi.raycastTarget = false;
            return fi;
        }

        public static void SetBar(Image fill, float value)
        {
            RectTransform rt = (RectTransform)fill.transform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        /// <summary>Coin balance chip.</summary>
        public static TMP_Text CoinChip(Transform parent, int coins)
        {
            Image bg = Panel(parent, new Color(0f, 0f, 0f, 0.45f), 30, false, "Coins");
            Image ic = Icon(bg.transform, IconId.Coin, Theme.Gold);
            At((RectTransform)ic.transform, 0f, 0.5f, 14f, 0f, 54f, 54f);
            TMP_Text t = Label(bg.transform, coins.ToString("N0"), 36, Theme.Gold, TextAnchor.MiddleRight);
            Stretch((RectTransform)t.transform, 72f, 22f, 0f, 0f);
            return t;
        }
    }

    /// <summary>Press feedback for buttons (scale punch), unscaled time so it works while paused.</summary>
    public class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1f;
        float _scale = 1f;

        public void OnPointerDown(PointerEventData e) { _target = 0.93f; }
        public void OnPointerUp(PointerEventData e) { _target = 1f; }
        public void OnPointerExit(PointerEventData e) { _target = 1f; }

        void OnDisable()
        {
            _scale = _target = 1f;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            if (Mathf.Abs(_scale - _target) < 0.001f) return;
            _scale = Mathf.Lerp(_scale, _target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }
}

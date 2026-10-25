using System;
using System.Collections.Generic;
using Brooklyneir.Art;
using Brooklyneir.Audio;
using Brooklyneir.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Brooklyneir.UI
{
    /// <summary>Base class for full-screen menus. Screens are rebuilt every time they are shown.</summary>
    public abstract class UIScreen
    {
        public RectTransform Root { get; private set; }
        protected UIManager UI { get; private set; }
        CanvasGroup _group;
        float _appear;

        /// <summary>Screens that keep the 3D scene visible behind (darkened) instead of a solid backdrop.</summary>
        public virtual bool Transparent { get { return true; } }

        public void Build(UIManager ui, RectTransform parent)
        {
            UI = ui;
            Root = UIKit.Node(parent, GetType().Name);
            UIKit.Stretch(Root);
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _appear = 0f;
            OnBuild();
        }

        protected abstract void OnBuild();
        public virtual void OnShow() { }
        public virtual void OnHide() { }
        public virtual void Tick(float dt) { }

        /// <summary>Android back button. Return true if handled.</summary>
        public virtual bool OnBack()
        {
            UI.Pop();
            return true;
        }

        public void Animate(float dt)
        {
            if (_appear < 1f)
            {
                _appear = Mathf.Min(1f, _appear + dt / 0.18f);
                float k = 1f - (1f - _appear) * (1f - _appear);
                _group.alpha = k;
                Root.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, k);
            }
            Tick(dt);
        }

        public void SetInteractable(bool on)
        {
            if (_group != null) { _group.interactable = on; _group.blocksRaycasts = on; }
        }

        public void Destroy()
        {
            OnHide();
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }

        // Shared header: back button + title + coin chip.
        protected Text Header(string title, bool back = true, bool coins = true)
        {
            RectTransform bar = UIKit.Node(Root, "Header");
            UIKit.Region(bar, 0f, 0.86f, 1f, 1f);
            if (back)
            {
                Button b = UIKit.Button(bar, "", IconId.Back, ButtonStyle.Secondary, () => OnBack(), 30, Sfx.UiBack);
                UIKit.At((RectTransform)b.transform, 0f, 0.5f, 40f, 0f, 110f, 100f);
            }
            Text t = UIKit.Heading(bar, title, 64, Theme.Text, TextAnchor.MiddleLeft);
            UIKit.Stretch((RectTransform)t.transform, back ? 180f : 50f, 400f, 0f, 0f);
            if (coins)
            {
                Text c = UIKit.CoinChip(bar, Game.Economy != null ? Game.Economy.Coins : 0);
                UIKit.At((RectTransform)c.transform.parent, 1f, 0.5f, -40f, 0f, 280f, 84f);
                _coinText = c;
            }
            return t;
        }

        Text _coinText;

        protected void RefreshCoins()
        {
            if (_coinText != null && Game.Economy != null) _coinText.text = Game.Economy.Coins.ToString("N0");
        }
    }

    /// <summary>
    /// Builds the canvas (scaled for any landscape phone, with safe-area handling for notches),
    /// manages the screen stack, dialogs, toasts and the Android back button.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public RectTransform SafeRoot { get; private set; }
        public RectTransform ScreenLayer { get; private set; }
        public RectTransform HudLayer { get; private set; }
        public RectTransform DialogLayer { get; private set; }
        Image _backdrop;
        readonly List<UIScreen> _stack = new List<UIScreen>();
        Rect _lastSafe;
        Vector2Int _lastSize;
        RectTransform _toast;
        Text _toastText;
        float _toastTimer;
        RectTransform _dialog;

        public int Depth { get { return _stack.Count; } }
        public UIScreen Top { get { return _stack.Count > 0 ? _stack[_stack.Count - 1] : null; } }
        public bool DialogOpen { get { return _dialog != null; } }

        public void Init()
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            Canvas.pixelPerfect = false;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // landscape: keep heights consistent across phones
            canvasGo.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                UnityEngine.InputSystem.UI.InputSystemUIInputModule module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            RectTransform root = (RectTransform)canvasGo.transform;
            _backdrop = UIKit.Panel(root, Theme.Overlay, 2, false, "Backdrop");
            UIKit.Stretch((RectTransform)_backdrop.transform, -10f, -10f, -10f, -10f);
            _backdrop.sprite = null;
            _backdrop.gameObject.SetActive(false);

            SafeRoot = UIKit.Node(root, "SafeArea");
            UIKit.Stretch(SafeRoot);
            HudLayer = UIKit.Node(SafeRoot, "HUD");
            UIKit.Stretch(HudLayer);
            ScreenLayer = UIKit.Node(SafeRoot, "Screens");
            UIKit.Stretch(ScreenLayer);
            DialogLayer = UIKit.Node(root, "Dialogs");
            UIKit.Stretch(DialogLayer);
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            _lastSafe = safe;
            _lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            SafeRoot.anchorMin = min;
            SafeRoot.anchorMax = max;
            SafeRoot.offsetMin = SafeRoot.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafe || Screen.width != _lastSize.x || Screen.height != _lastSize.y) ApplySafeArea();
            float dt = Time.unscaledDeltaTime;
            UIScreen top = Top;
            if (top != null) top.Animate(dt);
            if (_toast != null && _toastTimer > 0f)
            {
                _toastTimer -= dt;
                CanvasGroup g = _toast.GetComponent<CanvasGroup>();
                g.alpha = Mathf.Clamp01(_toastTimer * 3f);
                if (_toastTimer <= 0f) _toast.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------ stack

        public T Push<T>(T screen) where T : UIScreen
        {
            if (Top != null) Top.Root.gameObject.SetActive(false);
            screen.Build(this, ScreenLayer);
            _stack.Add(screen);
            screen.OnShow();
            RefreshBackdrop();
            return screen;
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            UIScreen top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Destroy();
            if (Top != null)
            {
                // Rebuild the screen below so it shows fresh data (coins, unlocks).
                UIScreen below = Top;
                _stack.RemoveAt(_stack.Count - 1);
                below.Destroy();
                Push(Recreate(below));
            }
            RefreshBackdrop();
        }

        /// <summary>Screens are re-created by type so they always show fresh data.</summary>
        static UIScreen Recreate(UIScreen s)
        {
            IRecreatable r = s as IRecreatable;
            return r != null ? r.Recreate() : s;
        }

        public void Replace(UIScreen screen)
        {
            Clear();
            Push(screen);
        }

        public void Clear()
        {
            for (int i = _stack.Count - 1; i >= 0; i--) _stack[i].Destroy();
            _stack.Clear();
            RefreshBackdrop();
        }

        void RefreshBackdrop()
        {
            UIScreen top = Top;
            _backdrop.gameObject.SetActive(top != null);
            _backdrop.color = top != null && !top.Transparent ? Theme.Background : Theme.Overlay;
        }

        /// <summary>Android back: dialog first, then the top screen.</summary>
        public bool HandleBack()
        {
            if (_dialog != null)
            {
                CloseDialog();
                return true;
            }
            UIScreen top = Top;
            if (top != null) return top.OnBack();
            return false;
        }

        // ------------------------------------------------------------ toast & dialogs

        public void Toast(string text, Color color)
        {
            if (_toast == null)
            {
                Image bg = UIKit.Panel(DialogLayer, new Color(0.05f, 0.07f, 0.12f, 0.95f), 30, false, "Toast");
                _toast = (RectTransform)bg.transform;
                UIKit.At(_toast, 0.5f, 0f, 0f, 70f, 1000f, 110f);
                _toast.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
                _toastText = UIKit.Label(_toast, "", 36, Color.white);
                UIKit.Stretch((RectTransform)_toastText.transform, 20f, 20f, 0f, 0f);
            }
            _toast.SetAsLastSibling();
            _toastText.text = text;
            _toastText.color = color;
            _toast.gameObject.SetActive(true);
            _toastTimer = 2.2f;
        }

        public void Confirm(string title, string body, string okText, Action onOk, string cancelText = "CANCELAR", Action onCancel = null)
        {
            CloseDialog();
            RectTransform shade = UIKit.Node(DialogLayer, "Dialog");
            UIKit.Stretch(shade);
            Image dim = shade.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            dim.raycastTarget = true;
            _dialog = shade;

            Image card = UIKit.Panel(shade, Theme.PanelLight, 36, true, "Card");
            UIKit.At((RectTransform)card.transform, 0.5f, 0.5f, 0f, 0f, 1000f, 520f);
            UIKit.Outline(card.transform, new Color(1f, 1f, 1f, 0.12f), 36, 3);
            Text t = UIKit.Heading(card.transform, title, 52, Theme.Text);
            UIKit.Region((RectTransform)t.transform, 0.05f, 0.72f, 0.95f, 0.95f);
            Text b = UIKit.Label(card.transform, body, 34, Theme.TextDim, TextAnchor.MiddleCenter, FontStyle.Normal);
            UIKit.Region((RectTransform)b.transform, 0.07f, 0.34f, 0.93f, 0.72f);
            Button cancel = UIKit.Button(card.transform, cancelText, null, ButtonStyle.Secondary, () =>
            {
                CloseDialog();
                if (onCancel != null) onCancel();
            }, 34, Sfx.UiBack);
            UIKit.Region((RectTransform)cancel.transform, 0.06f, 0.07f, 0.47f, 0.27f);
            Button ok = UIKit.Button(card.transform, okText, null, ButtonStyle.Primary, () =>
            {
                CloseDialog();
                if (onOk != null) onOk();
            }, 34);
            UIKit.Region((RectTransform)ok.transform, 0.53f, 0.07f, 0.94f, 0.27f);
        }

        public void CloseDialog()
        {
            if (_dialog != null) Destroy(_dialog.gameObject);
            _dialog = null;
        }
    }

    /// <summary>Screens that can rebuild themselves with fresh data when returned to.</summary>
    public interface IRecreatable
    {
        UIScreen Recreate();
    }
}

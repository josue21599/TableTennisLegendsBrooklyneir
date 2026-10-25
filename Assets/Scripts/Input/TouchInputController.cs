using System;
using Brooklyneir.Gameplay;
using Brooklyneir.Physics;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace Brooklyneir.Controls
{
    /// <summary>
    /// Multi-touch input for one or two local players.
    ///
    /// Every touch is tracked by its id through <see cref="TouchZoneRouter"/>: player 1 owns one
    /// finger in the left half, player 2 one finger in the right half, independently and
    /// simultaneously. Dragging moves the racket by the same world distance the finger travels on
    /// the table plane (times the sensitivity setting), so the racket stays under control without
    /// being hidden under the finger. A quick tap serves. Touches that start on a button are left
    /// to the UI.
    ///
    /// Backend: Unity's Input System (EnhancedTouch) when the project uses it, otherwise the classic
    /// Input Manager; both paths feed the same router. In the editor the mouse drives player 1 and
    /// the keyboard drives both players (arrows / WASD, Space / Enter to serve) for testing.
    /// </summary>
    public class TouchInputController : MonoBehaviour
    {
        public event Action<int> ServeRequested;

        enum Phase { Began, Moved, Ended, Canceled }

        readonly TouchZoneRouter _router = new TouchZoneRouter();
        readonly Vector2[] _lastPos = new Vector2[2];
        readonly float[] _downTime = new float[2];
        readonly float[] _travel = new float[2];
        readonly RacketController[] _rackets = new RacketController[2];
        Camera _cam;
        bool _enabled;
        float _sensitivity = 1f;
        Plane _plane = new Plane(Vector3.up, new Vector3(0f, TableGeometry.TopY + 0.2f, 0f));
        const int MouseFinger = -1; // EventSystem id of the left mouse button

        public TouchZoneRouter Router { get { return _router; } }

        public void Init(Camera cam)
        {
            _cam = cam;
#if ENABLE_INPUT_SYSTEM
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
#else
            Input.multiTouchEnabled = true;
#endif
        }

        public void Bind(RacketController p1, RacketController p2, bool p1Human, bool p2Human)
        {
            _rackets[0] = p1;
            _rackets[1] = p2;
            _router.Reset();
            _router.SetHumanControlled(0, p1Human);
            _router.SetHumanControlled(1, p2Human);
        }

        public void SetEnabled(bool on)
        {
            _enabled = on;
            if (!on) _router.Reset();
        }

        public void SetSensitivity(float s)
        {
            _sensitivity = Mathf.Clamp(s, 0.5f, 2f);
        }

        void Update()
        {
            if (!_enabled || _cam == null) return;
            int count = 0;
#if ENABLE_INPUT_SYSTEM
            var touches = ETouch.activeTouches;
            count = touches.Count;
            for (int i = 0; i < count; i++)
            {
                ETouch t = touches[i];
                Phase p;
                switch (t.phase)
                {
                    case ETouchPhase.Began: p = Phase.Began; break;
                    case ETouchPhase.Ended: p = Phase.Ended; break;
                    case ETouchPhase.Canceled: p = Phase.Canceled; break;
                    case ETouchPhase.None: continue;
                    default: p = Phase.Moved; break;
                }
                HandleTouch(t.touchId, p, t.screenPosition);
            }
#else
            count = Input.touchCount;
            for (int i = 0; i < count; i++)
            {
                UnityEngine.Touch t = Input.GetTouch(i);
                Phase p;
                switch (t.phase)
                {
                    case UnityEngine.TouchPhase.Began: p = Phase.Began; break;
                    case UnityEngine.TouchPhase.Ended: p = Phase.Ended; break;
                    case UnityEngine.TouchPhase.Canceled: p = Phase.Canceled; break;
                    default: p = Phase.Moved; break;
                }
                HandleTouch(t.fingerId, p, t.position);
            }
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
            if (count == 0) HandleMouseAndKeys();
#endif
        }

        void HandleTouch(int finger, Phase phase, Vector2 pos)
        {
            switch (phase)
            {
                case Phase.Began:
                {
                    bool overUI = IsOverUI(pos);
                    int p = _router.Began(finger, pos.x, Screen.width, overUI);
                    if (p >= 0)
                    {
                        _lastPos[p] = pos;
                        _downTime[p] = Time.unscaledTime;
                        _travel[p] = 0f;
                    }
                    break;
                }
                case Phase.Moved:
                {
                    int p = _router.OwnerOf(finger);
                    if (p >= 0) Drag(p, pos);
                    break;
                }
                default:
                {
                    int p = _router.Ended(finger);
                    if (p >= 0)
                    {
                        Drag(p, pos);
                        bool quick = Time.unscaledTime - _downTime[p] < 0.3f;
                        if (phase == Phase.Ended && (quick || _travel[p] > 40f) && ServeRequested != null) ServeRequested(p);
                    }
                    break;
                }
            }
        }

        PointerEventData _pointer;
        readonly System.Collections.Generic.List<RaycastResult> _hits = new System.Collections.Generic.List<RaycastResult>(8);

        /// <summary>
        /// Immediate UI raycast at the touch position (does not depend on which input module is
        /// active or on last frame's hover state), so a touch that starts on a button never
        /// moves a racket.
        /// </summary>
        bool IsOverUI(Vector2 screenPos)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            if (_pointer == null) _pointer = new PointerEventData(es);
            _pointer.position = screenPos;
            _hits.Clear();
            es.RaycastAll(_pointer, _hits);
            return _hits.Count > 0;
        }

        void Drag(int p, Vector2 pos)
        {
            Vector2 last = _lastPos[p];
            if ((pos - last).sqrMagnitude < 0.01f) return;
            _travel[p] += (pos - last).magnitude;
            Vector3 a, b;
            if (ScreenToPlane(last, out a) && ScreenToPlane(pos, out b) && _rackets[p] != null)
            {
                Vector3 delta = (b - a) * _sensitivity;
                _rackets[p].MoveTargetBy(delta);
            }
            _lastPos[p] = pos;
        }

        bool ScreenToPlane(Vector2 screen, out Vector3 world)
        {
            Ray r = _cam.ScreenPointToRay(screen);
            float d;
            if (_plane.Raycast(r, out d))
            {
                world = r.GetPoint(d);
                return true;
            }
            world = Vector3.zero;
            return false;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        void HandleMouseAndKeys()
        {
            float dt = Time.deltaTime;
            const float keySpeed = 3.5f;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) HandleTouch(MouseFinger, Phase.Began, mp);
                else if (mouse.leftButton.wasReleasedThisFrame) HandleTouch(MouseFinger, Phase.Ended, mp);
                else if (mouse.leftButton.isPressed) HandleTouch(MouseFinger, Phase.Moved, mp);
            }
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            // Screen right = +z, screen up = -x (camera on the +x side of the table).
            Vector3 d0 = new Vector3(Key(kb.downArrowKey) - Key(kb.upArrowKey), 0f, Key(kb.rightArrowKey) - Key(kb.leftArrowKey));
            Vector3 d1 = new Vector3(Key(kb.sKey) - Key(kb.wKey), 0f, Key(kb.dKey) - Key(kb.aKey));
            bool serve0 = kb.spaceKey.wasPressedThisFrame, serve1 = kb.enterKey.wasPressedThisFrame;
#else
            Vector2 mp = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) HandleTouch(MouseFinger, Phase.Began, mp);
            else if (Input.GetMouseButtonUp(0)) HandleTouch(MouseFinger, Phase.Ended, mp);
            else if (Input.GetMouseButton(0)) HandleTouch(MouseFinger, Phase.Moved, mp);
            Vector3 d0 = new Vector3(Axis(KeyCode.DownArrow, KeyCode.UpArrow), 0f, Axis(KeyCode.RightArrow, KeyCode.LeftArrow));
            Vector3 d1 = new Vector3(Axis(KeyCode.S, KeyCode.W), 0f, Axis(KeyCode.D, KeyCode.A));
            bool serve0 = Input.GetKeyDown(KeyCode.Space), serve1 = Input.GetKeyDown(KeyCode.Return);
#endif
            if (_router.IsHuman(0) && _rackets[0] != null)
            {
                if (d0.sqrMagnitude > 0f) _rackets[0].MoveTargetBy(d0 * keySpeed * dt);
                if (serve0 && ServeRequested != null) ServeRequested(0);
            }
            if (_router.IsHuman(1) && _rackets[1] != null)
            {
                if (d1.sqrMagnitude > 0f) _rackets[1].MoveTargetBy(d1 * keySpeed * dt);
                if (serve1 && ServeRequested != null) ServeRequested(1);
            }
        }

#if ENABLE_INPUT_SYSTEM
        static float Key(UnityEngine.InputSystem.Controls.KeyControl k)
        {
            return k != null && k.isPressed ? 1f : 0f;
        }
#else
        static float Axis(KeyCode pos, KeyCode neg)
        {
            return (Input.GetKey(pos) ? 1f : 0f) - (Input.GetKey(neg) ? 1f : 0f);
        }
#endif
#endif
    }

    /// <summary>Back button / Escape, for both input backends (Android's back button arrives as Escape).</summary>
    public static class BackButton
    {
        public static bool PressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}

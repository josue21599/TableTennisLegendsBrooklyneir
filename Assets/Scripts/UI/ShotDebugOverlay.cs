#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Brooklyneir.UI
{
    /// <summary>
    /// Debug only (editor and development builds; it does not exist in release builds): the last
    /// hits with their type, stroke and the first missing condition for the other types, drawn
    /// at the bottom of the screen. Each line also goes to the Console.
    /// Show/hide in the editor with the G key.
    /// </summary>
    public class ShotDebugOverlay : MonoBehaviour
    {
        const int MaxLines = 4;
        static readonly string[] Lines = new string[MaxLines];
        static int _count;
        static bool _visible = true;
        GUIStyle _style;
        Texture2D _bg;

        public static void Push(string line)
        {
            for (int i = MaxLines - 1; i > 0; i--) Lines[i] = Lines[i - 1];
            Lines[0] = line;
            if (_count < MaxLines) _count++;
            Debug.Log("[TTLB golpe] " + line);
        }

        public static void Clear() { _count = 0; }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.gKey.wasPressedThisFrame) _visible = !_visible;
#else
            if (Input.GetKeyDown(KeyCode.G)) _visible = !_visible;
#endif
        }

        void OnGUI()
        {
            if (!_visible || _count == 0) return;
            if (_style == null)
            {
                _bg = new Texture2D(1, 1);
                _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
                _bg.Apply();
                _style = new GUIStyle(GUI.skin.label);
                _style.normal.textColor = Color.white;
                _style.normal.background = _bg;
                _style.wordWrap = true;
                _style.padding = new RectOffset(8, 8, 4, 4);
            }
            _style.fontSize = Mathf.Max(12, Screen.height / 45);
            float w = Screen.width * 0.9f;
            float y = Screen.height * 0.70f;
            for (int i = 0; i < _count; i++)
            {
                GUI.color = i == 0 ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                float h = _style.CalcHeight(new GUIContent(Lines[i]), w);
                GUI.Label(new Rect(Screen.width * 0.05f, y, w, h), Lines[i], _style);
                y += h + 2f;
            }
            GUI.color = Color.white;
        }

        void OnDestroy()
        {
            if (_bg != null) Destroy(_bg);
        }
    }
}
#endif

// Compile-only declarations for the Input System package API used (1.17). NOT shipped.
using UnityEngine;
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl { public bool isPressed { get { return false; } } public bool wasPressedThisFrame { get { return false; } } public bool wasReleasedThisFrame { get { return false; } } }
    public class KeyControl : ButtonControl { }
    public class Vector2Control { public Vector2 ReadValue() { return Vector2.zero; } }
}
namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<T> { public int Count { get { return 0; } } public T this[int index] { get { return default(T); } } }
}
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    public enum TouchPhase { None, Began, Moved, Ended, Canceled, Stationary }
    public class Keyboard
    {
        public static Keyboard current { get { return null; } }
        public KeyControl escapeKey { get { return null; } } public KeyControl spaceKey { get { return null; } } public KeyControl enterKey { get { return null; } }
        public KeyControl upArrowKey { get { return null; } } public KeyControl downArrowKey { get { return null; } } public KeyControl leftArrowKey { get { return null; } } public KeyControl rightArrowKey { get { return null; } }
        public KeyControl aKey { get { return null; } } public KeyControl dKey { get { return null; } } public KeyControl sKey { get { return null; } } public KeyControl wKey { get { return null; } }
    }
    public class Mouse { public static Mouse current { get { return null; } } public Vector2Control position { get { return null; } } public ButtonControl leftButton { get { return null; } } }
}
namespace UnityEngine.InputSystem.EnhancedTouch
{
    public static class EnhancedTouchSupport { public static bool enabled { get { return false; } } public static void Enable() { } }
    public struct Touch
    {
        public static UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch> activeTouches { get { return new UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch>(); } }
        public int touchId { get { return 0; } }
        public UnityEngine.InputSystem.TouchPhase phase { get { return UnityEngine.InputSystem.TouchPhase.None; } }
        public Vector2 screenPosition { get { return Vector2.zero; } }
    }
}
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { public void AssignDefaultActions() { } }
}

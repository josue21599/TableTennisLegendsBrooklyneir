// Compile-only declarations for UnityEngine.UI / EventSystems (uGUI 2.0). NOT shipped.
#pragma warning disable 0067, 0649
using System;
using UnityEngine;

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 arg0);
    public class UnityEvent { public void AddListener(UnityAction call) { } public void RemoveAllListeners() { } }
    public class UnityEvent<T0> { public void AddListener(UnityAction<T0> call) { } public void RemoveAllListeners() { } }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; }
        public bool IsPointerOverGameObject() { return false; }
        public bool IsPointerOverGameObject(int pointerId) { return false; }
        public void RaycastAll(PointerEventData eventData, System.Collections.Generic.List<RaycastResult> raycastResults) { }
    }
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class BaseEventData { public BaseEventData(EventSystem eventSystem) { } }
    public class PointerEventData : BaseEventData { public PointerEventData(EventSystem eventSystem) : base(eventSystem) { } public Vector2 position { get; set; } }
    public struct RaycastResult { }
    public interface IEventSystemHandler { }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData e); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData e); }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    using UnityEngine.Events;

    public abstract class Graphic : UIBehaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public Material material { get; set; }
    }
    public abstract class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public float fillAmount { get; set; }
    }

    public class RawImage : MaskableGraphic { public Texture texture { get; set; } }

    public class Text : MaskableGraphic
    {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public TextAnchor alignment { get; set; }
        public FontStyle fontStyle { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; }
    }

    public struct ColorBlock
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color disabledColor { get; set; }
        public float fadeDuration { get; set; }
    }

    public class Selectable : UIBehaviour
    {
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
        public bool interactable { get; set; }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get { return null; } }
    }

    public class Slider : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        public class SliderEvent : UnityEvent<float> { }
        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public Direction direction { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public float value { get; set; }
        public SliderEvent onValueChanged { get { return null; } }
    }

    public class ScrollRect : UIBehaviour
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float scrollSensitivity { get; set; }
    }

    public class RectMask2D : UIBehaviour { }
    public class GraphicRaycaster : UIBehaviour { }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class LayoutElement : UIBehaviour
    {
        public float preferredHeight { get; set; }
        public float preferredWidth { get; set; }
        public float minHeight { get; set; }
        public float minWidth { get; set; }
    }

    public abstract class LayoutGroup : UIBehaviour { public RectOffset padding { get; set; } }
    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
    }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }

    public class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }

    public class AspectRatioFitter : UIBehaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }
        public AspectMode aspectMode { get; set; }
        public float aspectRatio { get; set; }
    }

    public abstract class BaseMeshEffect : UIBehaviour { }
    public class Shadow : BaseMeshEffect
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
    }
    public class Outline : Shadow { }
}

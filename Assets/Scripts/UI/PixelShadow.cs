using TMPro;
using UnityEngine;

namespace Brooklyneir.UI
{
    /// <summary>
    /// Crisp drop shadow for a raster TextMeshPro text: a dark copy of the text drawn just behind
    /// it (a sibling placed before it), offset by a few pixels. Raster fonts cannot use TMP's SDF
    /// outline/underlay, and uGUI's Shadow/Outline effects do not apply to TMP meshes.
    ///
    /// The copy's text is updated only when the source text changes (TMP's TEXT_CHANGED event),
    /// never per frame. Layout, scale, alpha and visibility are mirrored in LateUpdate with plain
    /// value comparisons (no allocations).
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class PixelShadow : MonoBehaviour
    {
        TMP_Text _source;
        TextMeshProUGUI _shadow;
        RectTransform _src, _dst;
        Vector2 _offset;
        float _alpha;
        float _lastSourceAlpha = -1f;

        public TextMeshProUGUI ShadowText { get { return _shadow; } }

        public static PixelShadow Attach(TMP_Text source, float offsetPx, float alpha)
        {
            PixelShadow ps = source.gameObject.AddComponent<PixelShadow>();
            ps.Init(source, offsetPx, alpha);
            return ps;
        }

        void Init(TMP_Text source, float offsetPx, float alpha)
        {
            _source = source;
            _src = (RectTransform)source.transform;
            _offset = new Vector2(offsetPx, -offsetPx);
            _alpha = alpha;
            GameObject g = new GameObject(source.gameObject.name + "_Shadow", typeof(RectTransform));
            g.layer = source.gameObject.layer;
            _dst = (RectTransform)g.transform;
            _dst.SetParent(_src.parent, false);
            _dst.SetSiblingIndex(_src.GetSiblingIndex()); // drawn before (behind) the source
            _shadow = g.AddComponent<TextMeshProUGUI>();
            _shadow.raycastTarget = false;
            CopyStyle();
            CopyText();
            CopyLayout();
            CopyAlpha();
        }

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            if (_dst != null) _dst.gameObject.SetActive(true);
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            if (_dst != null) _dst.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_dst != null) Destroy(_dst.gameObject);
        }

        void OnTextChanged(Object changed)
        {
            if (changed != _source || _shadow == null) return;
            CopyText();
        }

        void CopyStyle()
        {
            _shadow.font = _source.font;
            _shadow.fontSize = _source.fontSize;
            _shadow.fontStyle = _source.fontStyle;
            _shadow.alignment = _source.alignment;
            _shadow.textWrappingMode = _source.textWrappingMode;
            _shadow.overflowMode = _source.overflowMode;
            _shadow.richText = _source.richText;
        }

        void CopyText()
        {
            _shadow.fontSize = _source.fontSize;
            _shadow.alignment = _source.alignment;
            _shadow.text = _source.text; // only on change
        }

        void CopyLayout()
        {
            if (_dst.anchorMin != _src.anchorMin) _dst.anchorMin = _src.anchorMin;
            if (_dst.anchorMax != _src.anchorMax) _dst.anchorMax = _src.anchorMax;
            if (_dst.pivot != _src.pivot) _dst.pivot = _src.pivot;
            if (_dst.sizeDelta != _src.sizeDelta) _dst.sizeDelta = _src.sizeDelta;
            Vector2 pos = _src.anchoredPosition + _offset;
            if (_dst.anchoredPosition != pos) _dst.anchoredPosition = pos;
            if (_dst.localScale != _src.localScale) _dst.localScale = _src.localScale;
            if (_dst.localRotation != _src.localRotation) _dst.localRotation = _src.localRotation;
            if (_shadow.alignment != _source.alignment) _shadow.alignment = _source.alignment;
            if (_shadow.fontSize != _source.fontSize) _shadow.fontSize = _source.fontSize;
            if (_shadow.textWrappingMode != _source.textWrappingMode) _shadow.textWrappingMode = _source.textWrappingMode;
            if (_shadow.overflowMode != _source.overflowMode) _shadow.overflowMode = _source.overflowMode;
        }

        void CopyAlpha()
        {
            float a = _source.color.a;
            if (a == _lastSourceAlpha) return;
            _lastSourceAlpha = a;
            _shadow.color = new Color(0f, 0f, 0f, _alpha * a);
        }

        void LateUpdate()
        {
            if (_shadow == null) return;
            CopyLayout();
            CopyAlpha();
        }
    }
}

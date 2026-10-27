// Compile-only declarations of the TextMeshPro API used by the project (Unity 6 / com.unity.ugui 2.0).
// Signatures written from the TextMeshPro scripting reference. NOT shipped; bodies are dummies.
#pragma warning disable 0067, 0649, 0169, 0414
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.TextCore
{
    public struct FaceInfo
    {
        public float pointSize { get; set; }
        public float capLine { get; set; }
        public float ascentLine { get; set; }
        public float descentLine { get; set; }
        public float lineHeight { get; set; }
    }
}

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260,
        Left = 513, Center = 514, Right = 516,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028,
        Midline = 4098
    }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3, ScrollRect = 4, Page = 5, Linked = 6 }

    public class TMP_Asset : ScriptableObject { }

    public class TMP_FontAsset : TMP_Asset
    {
        public Material material;
        public UnityEngine.TextCore.FaceInfo faceInfo { get { return default(UnityEngine.TextCore.FaceInfo); } }
        public static TMP_FontAsset CreateFontAsset(Font font) { return null; }
        public bool HasCharacters(string text, out List<char> missingCharacters) { missingCharacters = null; return false; }
        public bool HasCharacter(char character) { return false; }
    }

    public class TMP_Settings : ScriptableObject
    {
        public static TMP_FontAsset defaultFontAsset { get { return null; } }
    }

    public class FastAction<A>
    {
        public void Add(Action<A> rhs) { }
        public void Remove(Action<A> rhs) { }
    }

    public static class TMPro_EventManager
    {
        public static readonly FastAction<UnityEngine.Object> TEXT_CHANGED_EVENT = new FastAction<UnityEngine.Object>();
    }

    public abstract class TMP_Text : UnityEngine.UI.MaskableGraphic
    {
        public virtual string text { get; set; }
        public TMP_FontAsset font { get; set; }
        public float fontSize { get; set; }
        public FontStyles fontStyle { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public TextWrappingModes textWrappingMode { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public bool richText { get; set; }
        public bool enableAutoSizing { get; set; }
        public bool extraPadding { get; set; }
        public float preferredWidth { get { return 0f; } }
        public void SetText(string sourceText) { }
        public void SetText(string sourceText, float arg0) { }
        public void SetText(string sourceText, float arg0, float arg1) { }
        public void SetText(string sourceText, float arg0, float arg1, float arg2) { }
        public void SetCharArray(char[] sourceText, int start, int length) { }
    }

    public class TextMeshProUGUI : TMP_Text { }
}

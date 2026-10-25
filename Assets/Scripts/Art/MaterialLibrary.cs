using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Brooklyneir.Art
{
    public enum SurfaceKind { Wood, Metal, Rubber, Plastic, Fabric, Ball, Painted, Glass, Ground }

    /// <summary>
    /// Creates and caches materials for every surface kind.
    ///
    /// Shaders: the editor setup creates template materials in Resources/Materials (one per shader
    /// and keyword set). Loading those templates guarantees the shader variants are included in
    /// the Android build; runtime materials are copies of them with different colours/textures.
    /// If a template is missing (setup not run yet), Shader.Find is used, first for URP, then for
    /// the built-in pipeline, so the game still renders instead of showing pink errors.
    /// </summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Material _litTemplate, _litEmissiveTemplate, _unlitTemplate, _unlitTransparentTemplate, _additiveTemplate, _litTransparentTemplate, _particleTemplate;
        static bool _initialised;
        static bool _urp;

        public static bool UsingURP { get { Init(); return _urp; } }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color_ = Shader.PropertyToID("_Color");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int Cutoff = Shader.PropertyToID("_Cutoff");

        public static void Init()
        {
            if (_initialised) return;
            _initialised = true;
            _urp = GraphicsSettings.currentRenderPipeline != null;
            _litTemplate = Resources.Load<Material>("Materials/TTLB_Lit");
            _litEmissiveTemplate = Resources.Load<Material>("Materials/TTLB_LitEmissive");
            _litTransparentTemplate = Resources.Load<Material>("Materials/TTLB_LitTransparent");
            _unlitTemplate = Resources.Load<Material>("Materials/TTLB_Unlit");
            _unlitTransparentTemplate = Resources.Load<Material>("Materials/TTLB_UnlitTransparent");
            _additiveTemplate = Resources.Load<Material>("Materials/TTLB_Additive");
            _particleTemplate = Resources.Load<Material>("Materials/TTLB_ParticleAlpha");
            if (_litTemplate == null) _litTemplate = FromShader(_urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (_litEmissiveTemplate == null) _litEmissiveTemplate = _litTemplate;
            if (_litTransparentTemplate == null) _litTransparentTemplate = MakeTransparentLit(FromShader(_urp ? "Universal Render Pipeline/Lit" : "Standard"));
            if (_unlitTemplate == null) _unlitTemplate = FromShader(_urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
            if (_unlitTransparentTemplate == null) _unlitTransparentTemplate = MakeTransparentUnlit(FromShader(_urp ? "Universal Render Pipeline/Unlit" : "Unlit/Transparent"), false);
            if (_additiveTemplate == null) _additiveTemplate = MakeTransparentUnlit(FromShader(_urp ? "Universal Render Pipeline/Particles/Unlit" : "Legacy Shaders/Particles/Additive"), true);
            if (_particleTemplate == null) _particleTemplate = MakeTransparentUnlit(FromShader(_urp ? "Universal Render Pipeline/Particles/Unlit" : "Legacy Shaders/Particles/Alpha Blended"), false);
            if (_litTemplate == null) Debug.LogError("[TTLB] No lit shader found. Run 'Brooklyneir > Configurar proyecto' in the editor.");
        }

        static Material FromShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) return null;
            return new Material(s);
        }

        /// <summary>Configures a URP/Standard lit material for alpha blending. Used by the editor too.</summary>
        public static Material MakeTransparentLit(Material m)
        {
            if (m == null) return null;
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                // Built-in Standard: fade mode.
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        /// <summary>Configures an unlit/particle material for alpha or additive blending.</summary>
        public static Material MakeTransparentUnlit(Material m, bool additive)
        {
            if (m == null) return null;
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (additive) m.EnableKeyword("_BLENDMODE_ADD");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Material Clone(Material template, string key)
        {
            Init();
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            if (template == null) template = _litTemplate;
            if (template == null)
            {
                // Should never happen once the project setup has run; fail loudly but keep going.
                Debug.LogError("[TTLB] Material templates missing. Open the project in the Unity editor and run 'Brooklyneir > Configurar proyecto'.");
                Shader fallback = Shader.Find("Sprites/Default");
                if (fallback == null) return null;
                template = new Material(fallback);
            }
            m = new Material(template);
            m.name = key;
            Cache[key] = m;
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColor)) m.SetColor(BaseColor, c);
            if (m.HasProperty(Color_)) m.SetColor(Color_, c);
        }

        static void SetTexture(Material m, Texture t, Vector2 tiling)
        {
            if (t == null) return;
            if (m.HasProperty(BaseMap)) { m.SetTexture(BaseMap, t); m.SetTextureScale(BaseMap, tiling); }
            if (m.HasProperty(MainTex)) { m.SetTexture(MainTex, t); m.SetTextureScale(MainTex, tiling); }
        }

        static void SetPBR(Material m, float smooth, float metal)
        {
            if (m.HasProperty(Smoothness)) m.SetFloat(Smoothness, smooth);
            if (m.HasProperty(Glossiness)) m.SetFloat(Glossiness, smooth);
            if (m.HasProperty(Metallic)) m.SetFloat(Metallic, metal);
        }

        /// <summary>Physically-based surface tuned per kind (wood, metal, rubber...).</summary>
        public static Material Surface(SurfaceKind kind, Color color, Texture texture = null, float tiling = 1f, float metallicOverride = -1f)
        {
            string key = "lit_" + kind + "_" + ColorUtility.ToHtmlStringRGBA(color) + "_" + (texture != null ? texture.name : "-") + "_" + tiling + "_" + metallicOverride;
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_litTemplate, key);
            SetColor(m, color);
            SetTexture(m, texture, new Vector2(tiling, tiling));
            float smooth, metal;
            switch (kind)
            {
                case SurfaceKind.Wood: smooth = 0.35f; metal = 0f; break;
                case SurfaceKind.Metal: smooth = 0.7f; metal = 0.85f; break;
                case SurfaceKind.Rubber: smooth = 0.18f; metal = 0f; break;
                case SurfaceKind.Plastic: smooth = 0.55f; metal = 0f; break;
                case SurfaceKind.Fabric: smooth = 0.05f; metal = 0f; break;
                case SurfaceKind.Ball: smooth = 0.62f; metal = 0f; break;
                case SurfaceKind.Painted: smooth = 0.42f; metal = 0f; break;
                case SurfaceKind.Glass: smooth = 0.9f; metal = 0.1f; break;
                default: smooth = 0.15f; metal = 0f; break;
            }
            if (metallicOverride >= 0f) metal = metallicOverride;
            SetPBR(m, smooth, metal);
            return m;
        }

        /// <summary>Lit material with an emissive glow (neon strips, lit windows, fire ball).</summary>
        public static Material Emissive(Color baseColor, Color emission, float intensity, Texture texture = null)
        {
            string key = "emis_" + ColorUtility.ToHtmlStringRGB(baseColor) + ColorUtility.ToHtmlStringRGB(emission) + intensity + (texture != null ? texture.name : "-");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_litEmissiveTemplate, key);
            SetColor(m, baseColor);
            SetTexture(m, texture, Vector2.one);
            SetPBR(m, 0.5f, 0f);
            if (m.HasProperty(EmissionColor))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor(EmissionColor, emission * intensity);
                if (texture != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", texture);
            }
            return m;
        }

        /// <summary>A fresh (uncached) emissive instance whose colour the caller animates.</summary>
        public static Material EmissiveInstance(Color baseColor, Color emission)
        {
            Init();
            Material m = new Material(_litEmissiveTemplate);
            SetColor(m, baseColor);
            SetPBR(m, 0.6f, 0f);
            if (m.HasProperty(EmissionColor))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColor, emission);
            }
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            if (m != null && m.HasProperty(EmissionColor)) m.SetColor(EmissionColor, emission);
        }

        public static void SetBaseColor(Material m, Color c)
        {
            if (m != null) SetColor(m, c);
        }

        public static Material LitTransparent(Color color, Texture texture = null, float tiling = 1f)
        {
            string key = "littr_" + ColorUtility.ToHtmlStringRGBA(color) + (texture != null ? texture.name : "-") + tiling;
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_litTransparentTemplate, key);
            SetColor(m, color);
            SetTexture(m, texture, new Vector2(tiling, tiling));
            SetPBR(m, 0.6f, 0f);
            return m;
        }

        public static Material Unlit(Color color, Texture texture = null)
        {
            string key = "unlit_" + ColorUtility.ToHtmlStringRGBA(color) + (texture != null ? texture.name : "-");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_unlitTemplate, key);
            SetColor(m, color);
            SetTexture(m, texture, Vector2.one);
            return m;
        }

        public static Material UnlitTransparent(Color color, Texture texture = null, float tiling = 1f)
        {
            string key = "unlittr_" + ColorUtility.ToHtmlStringRGBA(color) + (texture != null ? texture.name : "-") + tiling;
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_unlitTransparentTemplate, key);
            SetColor(m, color);
            SetTexture(m, texture, new Vector2(tiling, tiling));
            return m;
        }

        /// <summary>Uncached transparent unlit instance for things that fade (markers, flashes).</summary>
        public static Material UnlitTransparentInstance(Color color, Texture texture)
        {
            Init();
            Material m = new Material(_unlitTransparentTemplate);
            SetColor(m, color);
            SetTexture(m, texture, Vector2.one);
            return m;
        }

        public static Material Additive(Color color, Texture texture)
        {
            string key = "add_" + ColorUtility.ToHtmlStringRGBA(color) + (texture != null ? texture.name : "-");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_additiveTemplate, key);
            SetColor(m, color);
            SetTexture(m, texture, Vector2.one);
            return m;
        }

        public static Material ParticleAlpha(Color color, Texture texture)
        {
            string key = "palpha_" + ColorUtility.ToHtmlStringRGBA(color) + (texture != null ? texture.name : "-");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = Clone(_particleTemplate, key);
            SetColor(m, color);
            SetTexture(m, texture, Vector2.one);
            return m;
        }

        public static void SetColorOn(Material m, Color c)
        {
            if (m != null) SetColor(m, c);
        }
    }
}

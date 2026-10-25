using Brooklyneir.SaveSystem;
using UnityEngine;
using QualityLevel = Brooklyneir.SaveSystem.QualityLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Brooklyneir.Settings
{
    public struct QualityProfile
    {
        public float RenderScale;
        public int Msaa;
        public LightShadows Shadows;
        public float ShadowDistance;
        public float ParticleMultiplier;
        public bool Crowd;
        public bool ExtraLights;
        public int TargetFps;
    }

    /// <summary>
    /// Three graphics profiles. 60 FPS is the target on Medium and High; Low targets 60 too but
    /// renders at 70 % resolution without shadows, MSAA, crowd or extra lights, for weak phones.
    /// Values are applied to the active URP asset at runtime (render scale, MSAA, shadow
    /// distance) and to the scene (light shadows, particles, crowd).
    /// </summary>
    public static class QualityManager
    {
        public static QualityLevel Current { get; private set; }

        public static QualityProfile ProfileFor(QualityLevel level)
        {
            QualityProfile p = new QualityProfile();
            switch (level)
            {
                case QualityLevel.Low:
                    p.RenderScale = 0.7f; p.Msaa = 1; p.Shadows = LightShadows.None; p.ShadowDistance = 0f;
                    p.ParticleMultiplier = 0.4f; p.Crowd = false; p.ExtraLights = false; p.TargetFps = 60;
                    break;
                case QualityLevel.High:
                    p.RenderScale = 1f; p.Msaa = 4; p.Shadows = LightShadows.Soft; p.ShadowDistance = 14f;
                    p.ParticleMultiplier = 1f; p.Crowd = true; p.ExtraLights = true; p.TargetFps = 60;
                    break;
                default:
                    p.RenderScale = 0.85f; p.Msaa = 2; p.Shadows = LightShadows.Hard; p.ShadowDistance = 10f;
                    p.ParticleMultiplier = 0.7f; p.Crowd = true; p.ExtraLights = true; p.TargetFps = 60;
                    break;
            }
            return p;
        }

        /// <summary>Picks a sensible default for a first launch from the device's memory and GPU.</summary>
        public static QualityLevel SuggestForDevice()
        {
            int mem = SystemInfo.systemMemorySize;
            int gpuMem = SystemInfo.graphicsMemorySize;
            if (mem > 0 && mem < 3000) return QualityLevel.Low;
            if (mem >= 6000 && gpuMem >= 1500) return QualityLevel.High;
            return QualityLevel.Medium;
        }

        public static QualityProfile Apply(QualityLevel level)
        {
            Current = level;
            QualityProfile p = ProfileFor(level);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = p.TargetFps;
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = p.RenderScale;
                urp.msaaSampleCount = p.Msaa;
                urp.shadowDistance = Mathf.Max(0.1f, p.ShadowDistance);
            }
            else
            {
                QualitySettings.antiAliasing = p.Msaa > 1 ? p.Msaa : 0;
                QualitySettings.shadowDistance = p.ShadowDistance;
            }
            return p;
        }
    }
}

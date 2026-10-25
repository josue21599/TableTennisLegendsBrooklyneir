using UnityEngine;

namespace Brooklyneir.Audio
{
    /// <summary>
    /// Optional vibration for important impacts. On Android 8+ it uses VibrationEffect with a
    /// duration and amplitude; older devices fall back to a plain vibrate(ms). The VIBRATE
    /// permission (a normal, install-time permission) is added by Unity because
    /// Handheld.Vibrate is referenced below. Never throws: failures just disable haptics.
    /// </summary>
    public class Haptics
    {
        bool _enabled = true;
        bool _broken;
        float _lastTime;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _vibrator;
        int _sdk;
#endif

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _sdk = version.GetStatic<int>("SDK_INT");
                using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TTLB] Haptics unavailable: " + e.Message);
                _broken = true;
            }
#endif
        }

        /// <summary>Short pulse. strength 0..1 maps to 12..45 ms and amplitude.</summary>
        public void Pulse(float strength)
        {
            if (!_enabled || _broken) return;
            if (Time.unscaledTime - _lastTime < 0.06f) return;
            _lastTime = Time.unscaledTime;
            strength = Mathf.Clamp01(strength);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null) { Handheld.Vibrate(); return; }
            long ms = (long)Mathf.Lerp(12f, 45f, strength);
            try
            {
                if (_sdk >= 26)
                {
                    int amplitude = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(60f, 255f, strength)), 1, 255);
                    using (AndroidJavaClass effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject e = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", e);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TTLB] Vibration failed, disabling: " + e.Message);
                _broken = true;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (strength > 0.6f) Handheld.Vibrate();
#endif
        }

        // Referenced so Unity's Android build adds android.permission.VIBRATE automatically.
        static void PermissionAnchor()
        {
#if UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }
    }
}

using System;

namespace Brooklyneir.Net
{
    /// <summary>
    /// LAN multiplayer settings, loaded from Resources/Config/net.json (pure data + validation,
    /// unit-tested). Out-of-range values fall back to the defaults; nothing here throws.
    /// </summary>
    [Serializable]
    public class NetConfig
    {
        public const string ResourcePath = "Config/net";

        /// <summary>UDP port for the "a match is here" announcement.</summary>
        public int discoveryPort = 47777;
        /// <summary>UDP port of the match connection (Unity Transport).</summary>
        public int gamePort = 47778;
        public float announceIntervalSeconds = 1f;
        /// <summary>Racket positions sent per second (unreliable channel).</summary>
        public int paddleSendRate = 30;
        public float pingIntervalSeconds = 2f;
        /// <summary>Ping samples kept for the clock (the lowest round trip wins).</summary>
        public int clockSamples = 16;
        /// <summary>Host waits half the round trip plus this before giving a point on the guest's side (ms)...</summary>
        public int pointGraceExtraMs = 60;
        /// <summary>...but never less or more than these (ms).</summary>
        public int pointGraceMinMs = 40;
        public int pointGraceMaxMs = 200;
        /// <summary>No data for this long pauses both phones (s).</summary>
        public float silenceTimeoutSeconds = 1.5f;
        /// <summary>Time to reconnect before the match is void (s).</summary>
        public float reconnectWindowSeconds = 30f;
        /// <summary>The other player's racket is drawn this far in the past (s).</summary>
        public float remoteRenderDelaySeconds = 0.1f;
        /// <summary>Visual-only smoothing of a corrected ball position (s).</summary>
        public float ballCorrectionSmoothingSeconds = 0.1f;
        /// <summary>Countdown after a pause before both phones resume (s).</summary>
        public float resumeCountdownSeconds = 3f;
        /// <summary>Editor only: artificial latency and loss to test a bad network at home.</summary>
        public int simulatedLatencyMs = 0;
        public float simulatedLossPercent = 0f;

        public void Validate()
        {
            NetConfig d = new NetConfig();
            if (discoveryPort < 1024 || discoveryPort > 65535) discoveryPort = d.discoveryPort;
            if (gamePort < 1024 || gamePort > 65535 || gamePort == discoveryPort) gamePort = discoveryPort == d.gamePort ? d.discoveryPort : d.gamePort;
            announceIntervalSeconds = Range(announceIntervalSeconds, 0.2f, 5f, d.announceIntervalSeconds);
            if (paddleSendRate < 5 || paddleSendRate > 60) paddleSendRate = d.paddleSendRate;
            pingIntervalSeconds = Range(pingIntervalSeconds, 0.25f, 10f, d.pingIntervalSeconds);
            if (clockSamples < 4 || clockSamples > 64) clockSamples = d.clockSamples;
            if (pointGraceMinMs < 0 || pointGraceMinMs > 500) pointGraceMinMs = d.pointGraceMinMs;
            if (pointGraceMaxMs < pointGraceMinMs || pointGraceMaxMs > 1000) pointGraceMaxMs = Math.Max(pointGraceMinMs, d.pointGraceMaxMs);
            if (pointGraceExtraMs < 0 || pointGraceExtraMs > 500) pointGraceExtraMs = d.pointGraceExtraMs;
            silenceTimeoutSeconds = Range(silenceTimeoutSeconds, 0.5f, 10f, d.silenceTimeoutSeconds);
            reconnectWindowSeconds = Range(reconnectWindowSeconds, 5f, 120f, d.reconnectWindowSeconds);
            remoteRenderDelaySeconds = Range(remoteRenderDelaySeconds, 0f, 0.5f, d.remoteRenderDelaySeconds);
            ballCorrectionSmoothingSeconds = Range(ballCorrectionSmoothingSeconds, 0f, 0.5f, d.ballCorrectionSmoothingSeconds);
            resumeCountdownSeconds = Range(resumeCountdownSeconds, 0f, 10f, d.resumeCountdownSeconds);
            if (simulatedLatencyMs < 0 || simulatedLatencyMs > 1000) simulatedLatencyMs = 0;
            simulatedLossPercent = Range(simulatedLossPercent, 0f, 50f, 0f);
        }

        /// <summary>How long the host keeps a point on the guest's side pending, for a measured round trip (ms).</summary>
        public int PointGraceMs(float roundTripMs)
        {
            int g = (int)(roundTripMs * 0.5f) + pointGraceExtraMs;
            return g < pointGraceMinMs ? pointGraceMinMs : (g > pointGraceMaxMs ? pointGraceMaxMs : g);
        }

        static float Range(float v, float min, float max, float def)
        {
            return v >= min && v <= max ? v : def; // also rejects NaN
        }
    }
}

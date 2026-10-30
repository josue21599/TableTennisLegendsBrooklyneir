using System;
using Brooklyneir.Art;
using Brooklyneir.Config;
using Brooklyneir.Gameplay;
using Brooklyneir.Tables;
using NUnit.Framework;
using UnityEngine;

namespace Brooklyneir.Tests
{
    [TestFixture]
    public class AnimationTests
    {
        static readonly ShotType[] AllShots = (ShotType[])Enum.GetValues(typeof(ShotType));

        static float Peak(StrokeAnim s, AnimationConfig c, float strength, Func<RacketPoseOffset, float> pick)
        {
            float best = 0f;
            for (int i = 1; i < 200; i++)
            {
                float v = pick(RacketAnimation.Stroke(s, s.duration * i / 200f, strength, c));
                if (Mathf.Abs(v) > Mathf.Abs(best)) best = v;
            }
            return best;
        }

        [Test]
        public void EveryShotTypeHasAStrokeThatStartsAndEndsAtRest()
        {
            AnimationConfig c = new AnimationConfig();
            for (int i = 0; i < AllShots.Length; i++)
            {
                StrokeAnim s = c.StrokeFor(AllShots[i]);
                Assert.IsTrue(s != null && s.duration > 0f, AllShots[i] + " has no stroke");
                RacketPoseOffset start = RacketAnimation.Stroke(s, 0f, 1f, c);
                RacketPoseOffset end = RacketAnimation.Stroke(s, s.duration, 1f, c);
                Assert.AreEqual(0f, start.Pitch + start.Roll + start.Yaw + start.Lift, 1e-5f, AllShots[i] + " start");
                Assert.AreEqual(0f, end.Pitch + end.Roll + end.Yaw + end.Lift, 1e-5f, AllShots[i] + " end");
                Assert.Greater(Mathf.Abs(Peak(s, c, 1f, o => o.Pitch)) + Mathf.Abs(Peak(s, c, 1f, o => o.Roll)), 10f, AllShots[i] + " barely moves");
            }
        }

        [Test]
        public void StrokesAreDifferentForEachKindOfShot()
        {
            AnimationConfig c = new AnimationConfig();
            float normal = Peak(c.normal, c, 0.5f, o => o.Pitch);
            Assert.Greater(Peak(c.smash, c, 0.5f, o => o.Pitch), normal, "smash sweeps further than a normal shot");
            Assert.Less(Peak(c.smash, c, 0.5f, o => o.Lift), 0f, "smash ends downwards");
            Assert.Greater(Mathf.Abs(Peak(c.spin, c, 0.5f, o => o.Roll)), 20f, "spin brushes sideways");
            Assert.Greater(Peak(c.spin, c, 0.5f, o => o.Lift), 0f, "spin brushes upwards");
            Assert.Less(Peak(c.defensive, c, 0.5f, o => o.Pitch), 0f, "lob opens the face (opposite to the smash)");
            Assert.Less(c.fast.duration, c.normal.duration, "fast shot is quicker");
            Assert.Greater(c.defensive.duration, c.normal.duration, "lob is slower");
        }

        [Test]
        public void StrongerHitsSwingFurther()
        {
            AnimationConfig c = new AnimationConfig();
            Assert.Greater(Peak(c.normal, c, 1f, o => o.Pitch), Peak(c.normal, c, 0f, o => o.Pitch));
        }

        [Test]
        public void IdleSwayIsSmallAndDisappearsWhenTheBallComes()
        {
            AnimationConfig c = new AnimationConfig();
            for (float t = 0f; t < 5f; t += 0.05f)
            {
                RacketPoseOffset o = RacketAnimation.Idle(t, 0.3f, 1f, c);
                Assert.LessOrEqual(Mathf.Abs(o.Pitch), c.idlePitch + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(o.Lift), c.idleLift + 1e-5f);
                RacketPoseOffset off = RacketAnimation.Idle(t, 0.3f, 0f, c);
                Assert.AreEqual(0f, off.Pitch + off.Roll + off.Yaw + off.Lift, 1e-6f);
            }
        }

        [Test]
        public void WinnerRaisesTheRacketLoserLetsItDropAndBothEndBeforeTheNextServe()
        {
            AnimationConfig c = new AnimationConfig();
            float maxLift = 0f, minLift = 0f;
            for (float t = 0f; t < 2f; t += 0.01f)
            {
                maxLift = Mathf.Max(maxLift, RacketAnimation.Celebration(true, t, c).Lift);
                minLift = Mathf.Min(minLift, RacketAnimation.Celebration(false, t, c).Lift);
            }
            Assert.Greater(maxLift, 0.1f);
            Assert.Less(minLift, -0.01f);
            RacketPoseOffset endWin = RacketAnimation.Celebration(true, c.celebrateSeconds, c);
            RacketPoseOffset endLose = RacketAnimation.Celebration(false, c.sadSeconds, c);
            Assert.AreEqual(0f, Mathf.Abs(endWin.Pitch) + Mathf.Abs(endWin.Lift) + Mathf.Abs(endLose.Pitch) + Mathf.Abs(endLose.Lift), 1e-5f);
            float pause = new BalanceConfig().match.pointPauseSeconds;
            Assert.LessOrEqual(c.celebrateSeconds, pause, "celebration must end before the next serve");
            Assert.LessOrEqual(c.sadSeconds, pause);
        }

        [Test]
        public void AnimationConfigRepairsBadValues()
        {
            AnimationConfig c = new AnimationConfig();
            c.smash = null;
            c.normal.duration = -1f;
            c.spin.pitch = float.NaN;
            c.strengthScaleMax = 0.1f;
            c.idleLift = 3f;
            c.crowdHeadSpeedMax = 0f;
            c.waveLaps = 0f;
            c.Validate();
            AnimationConfig d = new AnimationConfig();
            Assert.IsTrue(c.smash != null && c.smash.duration > 0f);
            Assert.AreEqual(d.normal.duration, c.normal.duration, 1e-6f);
            Assert.AreEqual(d.spin.pitch, c.spin.pitch, 1e-6f);
            Assert.GreaterOrEqual(c.strengthScaleMax, c.strengthScaleMin);
            Assert.AreEqual(d.idleLift, c.idleLift, 1e-6f);
            Assert.GreaterOrEqual(c.crowdHeadSpeedMax, c.crowdHeadSpeedMin);
            Assert.Greater(c.waveLaps, 0f);
        }

        [Test]
        public void CrowdHeadsTurnTowardsTheBallWithinLimits()
        {
            float yaw, pitch;
            CrowdMotion.LookAngles(new Vector3(1f, 0f, 1f), 55f, 20f, out yaw, out pitch);
            Assert.AreEqual(45f, yaw, 0.01f, "ball to the right and ahead");
            CrowdMotion.LookAngles(new Vector3(-1f, 0f, 0.01f), 55f, 20f, out yaw, out pitch);
            Assert.AreEqual(-55f, yaw, 0.01f, "clamped to the neck limit");
            CrowdMotion.LookAngles(new Vector3(0f, -1f, 1f), 55f, 20f, out yaw, out pitch);
            Assert.AreEqual(20f, pitch, 0.01f, "looks down (clamped)");
            CrowdMotion.LookAngles(Vector3.zero, 55f, 20f, out yaw, out pitch);
            Assert.AreEqual(0f, yaw + pitch, 1e-6f);
        }

        [Test]
        public void FansReactWithBoundedDelaysAndSpeeds()
        {
            AnimationConfig c = new AnimationConfig();
            float minD = 99f, maxD = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float h = i / 100f;
                float d = CrowdMotion.ReactionDelay(h, c.crowdReactionMaxDelay);
                float s = CrowdMotion.HeadSpeed(h, c.crowdHeadSpeedMin, c.crowdHeadSpeedMax);
                Assert.IsTrue(d >= 0f && d <= c.crowdReactionMaxDelay);
                Assert.IsTrue(s >= c.crowdHeadSpeedMin && s <= c.crowdHeadSpeedMax);
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d);
            }
            Assert.Greater(maxD - minD, 0.2f, "not everybody reacts at the same time");
            Assert.AreEqual(0f, CrowdMotion.ReactionDelay(-3f, 0.35f), 1e-6f);
            Assert.AreEqual(0.35f, CrowdMotion.ReactionDelay(9f, 0.35f), 1e-6f);
        }

        [Test]
        public void ApplauseAndCheerStopWhenTheirTimeIsOver()
        {
            AnimationConfig c = new AnimationConfig();
            float forward, close, jump, arms;
            float maxForward = 0f, maxJump = 0f;
            for (float t = -0.5f; t < 4f; t += 0.01f)
            {
                CrowdMotion.Clap(t, c.clapSeconds, c.clapHz, out forward, out close);
                CrowdMotion.Cheer(t, c.cheerSeconds, 1.3f, out jump, out arms);
                Assert.IsTrue(close >= 0f && close <= 1f && arms >= 0f && arms <= 1f);
                Assert.IsTrue(jump >= 0f && jump <= 0.18f + 1e-5f);
                maxForward = Mathf.Max(maxForward, forward);
                maxJump = Mathf.Max(maxJump, jump);
                if (t <= 0f || t >= c.clapSeconds) Assert.AreEqual(0f, forward + close, 1e-6f);
                if (t <= 0f || t >= c.cheerSeconds) Assert.AreEqual(0f, jump + arms, 1e-6f);
            }
            Assert.Greater(maxForward, 50f);
            Assert.Greater(maxJump, 0.1f);
        }

        [Test]
        public void TheWaveGoesRoundTheTableInOrder()
        {
            AnimationConfig c = new AnimationConfig();
            float previousPeak = -1f;
            for (int a = 0; a < 360; a += 30)
            {
                // First moment this fan is (almost) fully up.
                float upAt = -1f;
                for (float t = 0f; t < c.waveSeconds; t += 0.005f)
                {
                    float w = CrowdMotion.Wave(a, t, c.waveSeconds, c.waveLaps, c.waveWidthDeg);
                    Assert.IsTrue(w >= 0f && w <= 1f);
                    if (upAt < 0f && w > 0.95f) upAt = t;
                }
                Assert.GreaterOrEqual(upAt, 0f, "fan at " + a + "° never stands up");
                Assert.Greater(upAt, previousPeak, "fan at " + a + "° stands up out of order");
                previousPeak = upAt;
            }
            Assert.AreEqual(0f, CrowdMotion.Wave(10f, 0f, c.waveSeconds, c.waveLaps, c.waveWidthDeg), 1e-6f);
            Assert.AreEqual(0f, CrowdMotion.Wave(10f, c.waveSeconds, c.waveSeconds, c.waveLaps, c.waveWidthDeg), 1e-6f);
            float angle = CrowdMotion.AngleAround(new Vector3(0f, 0f, -3f));
            Assert.AreEqual(270f, angle, 0.01f);
        }
    }
}

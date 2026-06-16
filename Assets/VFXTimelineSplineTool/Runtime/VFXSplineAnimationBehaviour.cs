using System;
using UnityEngine;
using UnityEngine.Playables;

namespace VFXTimelineSplineTool
{
    [Serializable]
    public class VFXSplineAnimationBehaviour : PlayableBehaviour
    {
        public struct EvaluationResult
        {
            public float progress;
            public Vector3 position;
            public Quaternion rotation;
            public bool hasRotation;
        }

        [NonSerialized] public VFXSimpleSpline spline;

        [Range(0f, 1f)] public float startProgress = 0f;
        [Range(0f, 1f)] public float endProgress = 1f;

        public bool useSpeedCurve = true;
        public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public bool reverse = false;

        [Tooltip("旧版循环开关：保留兼容。建议新工程使用 Loop Playback + Seconds Per Loop。")]
        public bool loop = false;

        [Tooltip("开启后，Timeline Clip 可以拉长为多轮循环，运动速度由 Seconds Per Loop 决定，而不是由 Clip 总长度决定。")]
        public bool loopPlayback = false;

        [Min(0.01f)]
        [Tooltip("Loop Playback 开启时，一圈路径需要的秒数。Clip 拉多长都不会改变这条路径的单圈速度。")]
        public float secondsPerLoop = 1f;

        public bool useDistanceBasedProgress = true;
        public Vector3 positionOffset = Vector3.zero;

        public bool followRotation = true;
        public VFXSplineRotationMode rotationMode = VFXSplineRotationMode.Full3D;
        public VFXSplineForwardAxis forwardAxis = VFXSplineForwardAxis.ZPositive;
        public Vector3 rotationOffsetEuler = Vector3.zero;
        public Vector3 fallbackForward = Vector3.forward;

        [HideInInspector] public bool triggerEvents = false; // v2.0 起隐藏：保留旧版兼容。

        private bool hasPrevious;
        private float previousProgress;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            hasPrevious = false;
            if (spline != null)
                spline.ResetEventFireStates();
        }

        /// <summary>
        /// 保留直接播放单个 Playable 时的兼容行为。
        /// 在 Timeline Track 中，最终 Transform 写入由 VFXSplineAnimationMixer 完成，确保 Clip Blend 正常。
        /// </summary>
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            Transform target = playerData as Transform;
            if (target == null)
                return;

            EvaluationResult result;
            if (!Evaluate(playable, target, out result))
                return;

            // 注意：Track Mixer 会在之后再次按权重写入最终结果。
            // 这里保留写入是为了兼容极少数没有 Mixer 的直接 Playable 使用场景。
            target.position = result.position;
            if (result.hasRotation)
                target.rotation = result.rotation;

            if (triggerEvents && Application.isPlaying && spline != null && spline.events != null)
                CheckEvents(result.progress);

            previousProgress = result.progress;
            hasPrevious = true;
        }

        public bool Evaluate(Playable playable, Transform target, out EvaluationResult result)
        {
            result = default;

            if (target == null || spline == null)
                return false;

            float normalizedTime;
            if (loopPlayback)
            {
                float cycle = Mathf.Max(0.01f, secondsPerLoop);
                normalizedTime = Mathf.Repeat((float)playable.GetTime() / cycle, 1f);
            }
            else
            {
                double duration = playable.GetDuration();
                normalizedTime = duration > 0.00001 ? (float)(playable.GetTime() / duration) : 0f;
                normalizedTime = Mathf.Clamp01(normalizedTime);
            }

            float curveT = useSpeedCurve && speedCurve != null
                ? Mathf.Clamp01(speedCurve.Evaluate(normalizedTime))
                : normalizedTime;

            float p = Mathf.LerpUnclamped(startProgress, endProgress, curveT);

            if (reverse)
                p = 1f - p;

            if (loop || loopPlayback)
                p = Mathf.Repeat(p, 1f);
            else
                p = Mathf.Clamp01(p);

            result.progress = p;
            result.position = spline.GetPoint(p, useDistanceBasedProgress) + positionOffset;
            result.rotation = target.rotation;
            result.hasRotation = false;

            if (followRotation && rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = spline.GetTangent(p, useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = fallbackForward.sqrMagnitude > 0.000001f ? fallbackForward.normalized : Vector3.forward;

                result.rotation = BuildRotation(target, tangent) * Quaternion.Euler(rotationOffsetEuler);
                result.hasRotation = true;
            }

            return true;
        }

        private Quaternion BuildRotation(Transform target, Vector3 tangent)
        {
            Vector3 forward = tangent.normalized;

            if (rotationMode == VFXSplineRotationMode.YawOnly)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.000001f)
                    forward = target.forward;
            }

            Quaternion look = Quaternion.LookRotation(forward.normalized, Vector3.up);
            return look * Quaternion.Inverse(VFXSplineAnimator.AxisToRotation(forwardAxis));
        }

        private void CheckEvents(float current)
        {
            if (!hasPrevious)
            {
                previousProgress = current;
                hasPrevious = true;
                return;
            }

            float from = previousProgress;
            float to = current;

            if (Mathf.Abs(to - from) > 0.5f)
            {
                spline.ResetEventFireStates();
                return;
            }

            bool forward = to >= from;
            for (int i = 0; i < spline.events.Count; i++)
            {
                VFXSplineEvent e = spline.events[i];
                if (e == null || !e.enabled)
                    continue;

                float ep = Mathf.Clamp01(e.progress);
                bool crossed = forward ? (ep > from && ep <= to) : (ep < from && ep >= to);

                if (crossed && !e.fired)
                {
                    e.Trigger(null, spline, ep, useDistanceBasedProgress);
                    e.fired = true;
                }
            }
        }
    }
}

using UnityEngine;

namespace VFXTimelineSplineTool
{
    public enum VFXSplineAnchorMode
    {
        FixedProgress,
        FollowAnimatorProgress
    }

    public enum VFXSplineAnchorProgressWrapMode
    {
        Clamp,
        Loop,
        PingPong
    }

    /// <summary>
    /// 路径锚点 / 特效挂点。
    /// 用途：把一个空物体固定到 Spline 的某个 Progress 位置。
    /// 粒子、面片、爆点等特效可以作为它的子物体，再用 Timeline 原生 Control Track / Activation Track 控制播放。
    /// v2.0 正式工作流：Anchor 用于给粒子、面片、爆点提供路径上的稳定挂点。
    /// </summary>
    [ExecuteAlways]
    public class VFXSplineAnchor : MonoBehaviour
    {
        [Header("Spline")]
        public VFXSimpleSpline spline;

        [Header("Anchor Mode")]
        public VFXSplineAnchorMode anchorMode = VFXSplineAnchorMode.FixedProgress;
        public VFXSplineAnimator sourceAnimator;
        public bool autoUseSourceSpline = true;

        [Header("Fixed Progress")]
        [Range(0f, 1f)] public float progress = 0.5f;

        [Header("Follow Animator Progress")]
        public float progressOffset = 0f;
        public VFXSplineAnchorProgressWrapMode progressWrapMode = VFXSplineAnchorProgressWrapMode.Clamp;

        [Header("Progress Evaluation")]
        public bool useDistanceBasedProgress = true;
        public bool reverse = false;
        [HideInInspector] public bool loop = false; // 兼容旧版本序列化数据；v2.0 正式流程使用 progressWrapMode。

        [Header("Position")]
        public bool followPosition = true;
        public Vector3 positionOffset = Vector3.zero;

        [Header("Rotation")]
        public VFXSplineRotationMode rotationMode = VFXSplineRotationMode.None;
        public VFXSplineForwardAxis forwardAxis = VFXSplineForwardAxis.ZPositive;
        public Vector3 rotationOffsetEuler = Vector3.zero;
        public Vector3 fallbackForward = Vector3.forward;

        [Header("Editor Preview")]
        public bool previewInEditMode = true;
        public bool applyOnValidate = true;
        public bool showSceneLabel = true;
        public string label = "Spline Anchor";
        public Color labelColor = new Color(1f, 0.2f, 1f, 1f);
        [Min(0.02f)] public float gizmoSize = 0.18f;

        private void Reset()
        {
            spline = null;
            anchorMode = VFXSplineAnchorMode.FixedProgress;
            sourceAnimator = null;
            autoUseSourceSpline = true;
            progress = 0.5f;
            progressOffset = 0f;
            progressWrapMode = VFXSplineAnchorProgressWrapMode.Clamp;
            useDistanceBasedProgress = true;
            reverse = false;
            loop = false;
            followPosition = true;
            positionOffset = Vector3.zero;
            rotationMode = VFXSplineRotationMode.None;
            forwardAxis = VFXSplineForwardAxis.ZPositive;
            rotationOffsetEuler = Vector3.zero;
            fallbackForward = Vector3.forward;
            previewInEditMode = true;
            applyOnValidate = true;
            showSceneLabel = true;
            gizmoSize = 0.18f;
            if (string.IsNullOrEmpty(label)) label = name;
        }

        private void OnEnable()
        {
            ApplyAnchor();
        }

        private void OnValidate()
        {
            progress = Mathf.Clamp01(progress);
            gizmoSize = Mathf.Max(0.02f, gizmoSize);
            if (string.IsNullOrEmpty(label)) label = name;
            if (applyOnValidate && (Application.isPlaying || previewInEditMode))
                ApplyAnchor();
        }

        private void Update()
        {
            if (Application.isPlaying || previewInEditMode)
                ApplyAnchor();
        }

        public void SetProgress(float value)
        {
            SetEffectiveProgress(value);
        }

        /// <summary>
        /// 设置 Anchor 的最终 Progress。
        /// Fixed 模式下直接写入 progress；Follow 模式下会反推 progressOffset。
        /// </summary>
        public void SetEffectiveProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && sourceAnimator != null)
            {
                float source = GetSourceEvaluatedProgress();
                progressOffset = value - source;
            }
            else
            {
                progress = value;
            }
            ApplyAnchor();
        }

        public VFXSimpleSpline GetActiveSpline()
        {
            if (spline != null) return spline;
            if (autoUseSourceSpline && sourceAnimator != null) return sourceAnimator.spline;
            return null;
        }

        public float GetSourceEvaluatedProgress()
        {
            if (sourceAnimator == null) return 0f;
            return sourceAnimator.EvaluateProgressValue(sourceAnimator.progress);
        }

        public float GetRawAnchorProgress()
        {
            if (anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && sourceAnimator != null)
                return GetSourceEvaluatedProgress() + progressOffset;
            return progress;
        }

        public float GetEffectiveProgress()
        {
            return EvaluateProgressValue(GetRawAnchorProgress());
        }

        public float EvaluateProgressValue(float input)
        {
            float p = input;

            if (reverse)
                p = 1f - p;

            // 兼容旧版 loop 字段；如果旧工程里 loop=true，则仍然按循环处理。
            if (loop)
                return Mathf.Repeat(p, 1f);

            switch (progressWrapMode)
            {
                case VFXSplineAnchorProgressWrapMode.Loop:
                    return Mathf.Repeat(p, 1f);
                case VFXSplineAnchorProgressWrapMode.PingPong:
                    return Mathf.PingPong(p, 1f);
                case VFXSplineAnchorProgressWrapMode.Clamp:
                default:
                    return Mathf.Clamp01(p);
            }
        }

        public void ApplyAnchor()
        {
            VFXSimpleSpline activeSpline = GetActiveSpline();
            if (activeSpline == null) return;

            float p = GetEffectiveProgress();

            if (followPosition)
            {
                Vector3 pos = activeSpline.GetPoint(p, useDistanceBasedProgress) + positionOffset;
                transform.position = pos;
            }

            if (rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = activeSpline.GetTangent(p, useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = fallbackForward.sqrMagnitude > 0.000001f ? fallbackForward.normalized : Vector3.forward;

                Quaternion rot = BuildRotation(tangent);
                transform.rotation = rot * Quaternion.Euler(rotationOffsetEuler);
            }
        }

        public Quaternion BuildRotation(Vector3 tangent)
        {
            Vector3 forward = tangent.normalized;

            if (rotationMode == VFXSplineRotationMode.YawOnly)
            {
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.000001f)
                    forward = transform.forward;
            }

            Quaternion look = Quaternion.LookRotation(forward.normalized, Vector3.up);
            return look * Quaternion.Inverse(VFXSplineAnimator.AxisToRotation(forwardAxis));
        }
    }
}

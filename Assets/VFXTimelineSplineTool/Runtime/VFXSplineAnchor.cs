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

        [Header("Anchor 模式")]
        public VFXSplineAnchorMode anchorMode = VFXSplineAnchorMode.FixedProgress;
        public VFXSplineAnimator sourceAnimator;
        public bool autoUseSourceSpline = true;

        [Header("固定 Progress")]
        [Range(0f, 1f)] public float progress = 0.5f;

        [Header("跟随 Animator Progress")]
        public float progressOffset = 0f;
        public VFXSplineAnchorProgressWrapMode progressWrapMode = VFXSplineAnchorProgressWrapMode.Clamp;

        [Header("Progress 计算")]
        public bool useDistanceBasedProgress = true;
        public bool reverse = false;
        [HideInInspector] public bool loop = false; // 兼容旧版本序列化数据；v2.0 正式流程使用 progressWrapMode。

        [Header("位置")]
        public bool followPosition = true;
        public Vector3 positionOffset = Vector3.zero;

        [Header("旋转")]
        public VFXSplineRotationMode rotationMode = VFXSplineRotationMode.None;
        public VFXSplineForwardAxis forwardAxis = VFXSplineForwardAxis.ZPositive;
        public Vector3 rotationOffsetEuler = Vector3.zero;
        public Vector3 fallbackForward = Vector3.forward;
        public bool ignoreSplineTransformRotation = false;
        public bool followSourceRotation = false;
        public bool followSourceScale = false;

        [Header("编辑器预览")]
        public bool previewInEditMode = true;
        public bool applyOnValidate = true;
        public bool showSceneLabel = true;
        public string label = "Spline Anchor";
        public Color labelColor = new Color(1f, 0.2f, 1f, 1f);
        [Min(0.02f)] public float gizmoSize = 0.18f;

        [Header("Bake To AnimationClip / 烘焙")]
        [Tooltip("烘焙输出的 AnimationClip 帧率。")]
        public int bakeFrameRate = 60;
        [Tooltip("烘焙动画时长，单位秒。Follow Animator Progress 时可手动填成源运动物体的烘焙时长。")]
        public float bakeDuration = 1f;
        [Tooltip("是否烘焙 Transform Position。")]
        public bool bakePosition = true;
        [Tooltip("是否烘焙 Transform Rotation。")]
        public bool bakeRotation = true;
        [Tooltip("开启后，会把 Anchor 下所有子物体的 Local Transform 一起写进同一个 AnimationClip。")]
        public bool bakeChildren = false;
        [Tooltip("Bake Children 开启时，是否同时烘焙子物体 Local Scale。")]
        public bool bakeChildScale = true;
        [Tooltip("Bake Children 开启时，采样这个已有 AnimationClip 里的子物体动画，再写入最终烘焙 Clip。为空时使用当前子物体静态 Local Transform。")]
        public AnimationClip bakeChildAnimationClip;
        [Tooltip("Child Animation Clip 为空时，自动尝试采样子物体自身 Animation / Animator Controller 里的第一个 Clip。")]
        public bool bakeAutoSampleChildAnimations = true;
        [Tooltip("开启后，子物体动画 Clip 会按烘焙时长归一化采样；关闭后按真实秒数采样。")]
        public bool bakeChildAnimationUseNormalizedTime = true;
        [Tooltip("关闭 Normalized Time 时，超过子物体动画 Clip 长度后是否循环采样。")]
        public bool bakeChildAnimationLoop = true;
        [Tooltip("Follow Animator Progress 模式下，开启后会按 Source Animator 的 Bake Progress Source 采样进度。")]
        public bool bakeUseSourceAnimatorProgress = true;
        [Tooltip("烘焙文件保存目录。")]
        public string bakeSaveFolder = "Assets/Animations/SplineBakes";
        [Tooltip("烘焙 AnimationClip 文件名。为空时自动使用物体名。")]
        public string bakeClipName = "";
        [Tooltip("烘焙完成后，如果物体没有 Animator，则自动添加 Animator 组件。")]
        public bool bakeAddAnimatorIfMissing = true;
        [Tooltip("关键帧间隔。1=每帧记录，2=隔一帧记录一次。")]
        [Min(1)] public int bakeKeyframeStep = 1;
        [Tooltip("无论 Keyframe Step 是多少，都强制记录第一帧和最后一帧。")]
        public bool bakeAlwaysKeyStartAndEnd = true;
        [Tooltip("开启后，会根据误差阈值自动删除多余关键帧。")]
        public bool bakeOptimizeCurves = false;
        [Tooltip("位置优化误差阈值，单位为 Unity 距离。")]
        public float bakePositionTolerance = 0.005f;
        [Tooltip("旋转优化误差阈值，单位为角度。")]
        public float bakeRotationTolerance = 0.25f;

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
            ignoreSplineTransformRotation = false;
            followSourceRotation = false;
            followSourceScale = false;
            previewInEditMode = true;
            applyOnValidate = true;
            showSceneLabel = true;
            gizmoSize = 0.18f;
            bakeFrameRate = 60;
            bakeDuration = 1f;
            bakePosition = true;
            bakeRotation = true;
            bakeChildren = false;
            bakeChildScale = true;
            bakeChildAnimationClip = null;
            bakeAutoSampleChildAnimations = true;
            bakeChildAnimationUseNormalizedTime = true;
            bakeChildAnimationLoop = true;
            bakeUseSourceAnimatorProgress = true;
            bakeSaveFolder = "Assets/Animations/SplineBakes";
            bakeClipName = "";
            bakeAddAnimatorIfMissing = true;
            bakeKeyframeStep = 1;
            bakeAlwaysKeyStartAndEnd = true;
            bakeOptimizeCurves = false;
            bakePositionTolerance = 0.005f;
            bakeRotationTolerance = 0.25f;
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
            bakeFrameRate = Mathf.Clamp(bakeFrameRate, 1, 240);
            bakeDuration = Mathf.Max(0.01f, bakeDuration);
            bakeKeyframeStep = Mathf.Max(1, bakeKeyframeStep);
            bakePositionTolerance = Mathf.Max(0f, bakePositionTolerance);
            bakeRotationTolerance = Mathf.Max(0f, bakeRotationTolerance);
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

            if (followSourceRotation && sourceAnimator != null)
            {
                transform.rotation = sourceAnimator.transform.rotation;
            }
            else if (rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = activeSpline.GetTangent(p, useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = fallbackForward.sqrMagnitude > 0.000001f ? fallbackForward.normalized : Vector3.forward;

                Vector3 normal = activeSpline.GetNormal(p, useDistanceBasedProgress);
                VFXSplineAnimator.ApplySplineRotationLock(activeSpline, ignoreSplineTransformRotation, ref tangent, ref normal);
                Quaternion rot = BuildRotation(tangent, normal);
                transform.rotation = rot * Quaternion.Euler(rotationOffsetEuler);
            }

            if (followSourceScale && sourceAnimator != null)
                transform.localScale = sourceAnimator.transform.localScale;
        }

        public Quaternion BuildRotation(Vector3 tangent)
        {
            return BuildRotation(tangent, Vector3.up);
        }

        public Quaternion BuildRotation(Vector3 tangent, Vector3 up)
        {
            Vector3 forward = tangent.normalized;

            if (VFXSplineAnimator.IsPlanarRotationMode(rotationMode))
                forward = VFXSplineAnimator.ResolvePlanarForward(tangent, fallbackForward, rotationMode == VFXSplineRotationMode.PlanarStable);

            if (up.sqrMagnitude < 0.000001f)
                up = Vector3.up;

            Quaternion look = Quaternion.LookRotation(forward.normalized, up.normalized);
            return look * Quaternion.Inverse(VFXSplineAnimator.AxisToRotation(forwardAxis));
        }
    }
}

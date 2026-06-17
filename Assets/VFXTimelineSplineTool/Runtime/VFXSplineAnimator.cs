using UnityEngine;
using UnityEngine.Playables;

namespace VFXTimelineSplineTool
{
    public enum VFXSplineRotationMode
    {
        None,
        Full3D,
        YawOnly
    }

    public enum VFXSplineForwardAxis
    {
        ZPositive,
        ZNegative,
        XPositive,
        XNegative,
        YPositive,
        YNegative
    }

    public enum VFXSplineBakeProgressSource
    {
        Linear01,
        BakeProgressCurve,
        CurrentAnimatorProgress,
        ExistingAnimationClipProgressCurve,
        TimelineBoundAnimationTrack
    }

    public enum VFXSplineEditorPreviewMode
    {
        Off,
        Always,
        OnlyWhenSelected
    }

    public enum VFXSplineTimelineInfiniteClipBakeMode
    {
        HoldLastFrame,
        Loop
    }

    [ExecuteAlways]
    public class VFXSplineAnimator : MonoBehaviour
    {
        [Header("Spline")]
        public VFXSimpleSpline spline;

        [Header("Animation Track 可 K 参数")]
        [Range(0f, 1f)] public float progress = 0f;

        [Header("运动设置")]
        public bool reverse = false;
        public bool loop = false;
        public bool useDistanceBasedProgress = true;
        public Vector3 positionOffset = Vector3.zero;

        [Header("旋转设置")]
        public VFXSplineRotationMode rotationMode = VFXSplineRotationMode.Full3D;
        public VFXSplineForwardAxis forwardAxis = VFXSplineForwardAxis.ZPositive;
        public Vector3 rotationOffsetEuler = Vector3.zero;
        public Vector3 fallbackForward = Vector3.forward;

        [Header("编辑器预览")]
        [Tooltip("新版编辑模式预览控制。Off=编辑模式不自动写 Transform；Always=和旧版一样一直预览；Only When Selected=只有选中物体时才自动预览。")]
        public VFXSplineEditorPreviewMode editorPreviewMode = VFXSplineEditorPreviewMode.Always;

        [Tooltip("旧版开关：保留兼容。关闭后等同于 Editor Preview Mode = Off。")]
        public bool previewInEditMode = true;
        public bool applyOnValidate = true;
        public bool showCurrentProgressPoint = true;
        public Color previewColor = Color.green;

        [Header("Bake To AnimationClip / 烘焙")]
        [Tooltip("烘焙输出的 AnimationClip 帧率。特效建议 60。")]
        public int bakeFrameRate = 60;

        [Tooltip("烘焙动画时长，单位秒。")]
        public float bakeDuration = 1f;

        [Tooltip("是否烘焙 Transform Position。")]
        public bool bakePosition = true;

        [Tooltip("是否烘焙 Transform Rotation。")]
        public bool bakeRotation = true;

        [HideInInspector] public bool bakeUseProgressCurve = false; // 旧版本字段，保留兼容。

        [Tooltip("烘焙 Progress 来源。Linear=0到1匀速；Bake Progress Curve=使用下方曲线；Current Animator Progress=用当前 Progress 烘焙静态姿态；Existing AnimationClip Progress Curve=读取已有 AnimationClip 里的 VFXSplineAnimator.progress 曲线；Timeline Bound Animation Track=读取 Timeline 上绑定当前物体的 Animation Track。")]
        public VFXSplineBakeProgressSource bakeProgressSource = VFXSplineBakeProgressSource.Linear01;

        [Tooltip("烘焙用 Progress 曲线。X=时间比例，Y=路径 Progress。只在 Bake Progress Source = Bake Progress Curve 时使用。")]
        public AnimationCurve bakeProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("已有 Progress 动画片段。只在 Bake Progress Source = Existing AnimationClip Progress Curve 时使用。工具会读取其中 VFXSplineAnimator.progress 曲线。")]
        public AnimationClip bakeSourceProgressClip;

        [Tooltip("开启后把烘焙时间按比例映射到 Source Clip 长度；关闭后按真实秒数读取 Source Clip。")]
        public bool bakeSourceClipUseNormalizedTime = true;

        [Tooltip("自动读取 Timeline Animation Track 时使用的 PlayableDirector。为空时会自动在场景中查找绑定当前物体的 Timeline。")]
        public PlayableDirector bakePlayableDirector;

        [Tooltip("开启后，Timeline Bound Animation Track 模式会用识别到的 Timeline Clip 长度覆盖 Bake Duration。")]
        public bool bakeUseTimelineClipDuration = true;

        [Tooltip("Timeline Infinite Clip 超过源 AnimationClip 长度后的读取方式。Hold=停在最后一帧；Loop=循环读取。")]
        public VFXSplineTimelineInfiniteClipBakeMode bakeTimelineInfiniteClipMode = VFXSplineTimelineInfiniteClipBakeMode.HoldLastFrame;

        [Tooltip("烘焙文件保存目录。")]
        public string bakeSaveFolder = "Assets/Animations/SplineBakes";

        [Tooltip("烘焙 AnimationClip 文件名。为空时自动使用物体名。")]
        public string bakeClipName = "";

        [Tooltip("烘焙完成后，如果物体没有 Animator，则自动添加 Animator 组件。")]
        public bool bakeAddAnimatorIfMissing = true;

        [Tooltip("烘焙关键帧间隔。1=每帧记录，2=隔1帧记录一次，5=每5帧记录一次。")]
        [Min(1)] public int bakeKeyframeStep = 1;

        [Tooltip("开启后，无论 Keyframe Step 是多少，都会强制记录第一帧和最后一帧。")]
        public bool bakeAlwaysKeyStartAndEnd = true;

        [Tooltip("开启后，会在 Keyframe Step 的基础上用误差阈值自动删除多余关键帧。")]
        public bool bakeOptimizeCurves = false;

        [Tooltip("位置优化误差阈值，单位为 Unity 距离。值越大，关键帧越少，但路径误差越大。")]
        public float bakePositionTolerance = 0.005f;

        [Tooltip("旋转优化误差阈值，单位为角度。值越大，关键帧越少，但旋转误差越大。")]
        public float bakeRotationTolerance = 0.25f;

        [HideInInspector] public bool triggerEvents = false;
        [HideInInspector] public bool triggerEventsInEditMode = false;

        private float previousEvaluatedProgress;
        private bool hasPreviousProgress;

        private void Reset()
        {
            progress = 0f;
            useDistanceBasedProgress = true;
            editorPreviewMode = VFXSplineEditorPreviewMode.Always;
            previewInEditMode = true;
            applyOnValidate = true;
            showCurrentProgressPoint = true;
            triggerEvents = false;
            triggerEventsInEditMode = false;
            bakeFrameRate = 60;
            bakeDuration = 1f;
            bakePosition = true;
            bakeRotation = true;
            bakeUseProgressCurve = false;
            bakeProgressSource = VFXSplineBakeProgressSource.Linear01;
            bakeProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            bakeSourceProgressClip = null;
            bakeSourceClipUseNormalizedTime = true;
            bakePlayableDirector = null;
            bakeUseTimelineClipDuration = true;
            bakeTimelineInfiniteClipMode = VFXSplineTimelineInfiniteClipBakeMode.HoldLastFrame;
            bakeSaveFolder = "Assets/Animations/SplineBakes";
            bakeClipName = "";
            bakeAddAnimatorIfMissing = true;
            bakeKeyframeStep = 1;
            bakeAlwaysKeyStartAndEnd = true;
            bakeOptimizeCurves = false;
            bakePositionTolerance = 0.005f;
            bakeRotationTolerance = 0.25f;
        }

        private void OnEnable()
        {
            hasPreviousProgress = false;
            if (ShouldAutoApplyInCurrentMode())
                ApplyCurrentProgress(false);
        }

        private void OnValidate()
        {
            if (applyOnValidate && ShouldAutoApplyInCurrentMode())
                ApplyCurrentProgress(false);
        }

        private void Update()
        {
            if (ShouldAutoApplyInCurrentMode())
                ApplyCurrentProgress(true);
        }

        private bool ShouldAutoApplyInCurrentMode()
        {
            if (Application.isPlaying)
                return true;

            if (!previewInEditMode)
                return false;

            switch (editorPreviewMode)
            {
                case VFXSplineEditorPreviewMode.Off:
                    return false;
                case VFXSplineEditorPreviewMode.OnlyWhenSelected:
#if UNITY_EDITOR
                    return UnityEditor.Selection.activeGameObject == gameObject;
#else
                    return false;
#endif
                case VFXSplineEditorPreviewMode.Always:
                default:
                    return true;
            }
        }

        public void SetProgress(float value)
        {
            progress = Mathf.Clamp01(value);
            ApplyCurrentProgress(true);
        }

        public void ResetEventFireStates()
        {
            if (spline != null)
                spline.ResetEventFireStates();

            hasPreviousProgress = false;
        }

        public void ForceTriggerEventsAtCurrentProgress()
        {
            if (spline == null || spline.events == null)
                return;

            float p = EvaluateProgressValue(progress);
            for (int i = 0; i < spline.events.Count; i++)
            {
                VFXSplineEvent e = spline.events[i];
                if (e == null || !e.enabled)
                    continue;

                float ep = Mathf.Clamp01(e.progress);
                if (Mathf.Abs(ep - p) <= 0.02f)
                {
                    e.Trigger(this, spline, ep, useDistanceBasedProgress);
                    e.fired = true;
                }
            }
        }

        public void ApplyCurrentProgress(bool checkEvents)
        {
            if (spline == null)
                return;

            float p = EvaluateProgressValue(progress);
            Vector3 pos = spline.GetPoint(p, useDistanceBasedProgress) + positionOffset;
            transform.position = pos;

            if (rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = spline.GetTangent(p, useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = fallbackForward.sqrMagnitude > 0.000001f ? fallbackForward.normalized : Vector3.forward;

                Quaternion rot = BuildRotation(tangent);
                transform.rotation = rot * Quaternion.Euler(rotationOffsetEuler);
            }

            if (checkEvents && triggerEvents && spline.events != null)
            {
                bool allowInCurrentMode = Application.isPlaying || triggerEventsInEditMode;
                if (allowInCurrentMode)
                    CheckAndTriggerEvents(p);
            }

            previousEvaluatedProgress = p;
            hasPreviousProgress = true;
        }

        public float EvaluateProgressValue(float input)
        {
            float p = Mathf.Clamp01(input);
            if (reverse)
                p = 1f - p;

            if (loop)
                p = Mathf.Repeat(p, 1f);

            return p;
        }

        private void CheckAndTriggerEvents(float current)
        {
            if (spline == null || spline.events == null)
                return;

            if (!hasPreviousProgress)
            {
                previousEvaluatedProgress = current;
                hasPreviousProgress = true;
                return;
            }

            float from = previousEvaluatedProgress;
            float to = current;

            if (Mathf.Abs(to - from) > 0.5f)
            {
                // 可能是循环或 Timeline 跳帧；重置状态，避免一次性触发大量事件。
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
                    e.Trigger(this, spline, ep, useDistanceBasedProgress);
                    e.fired = true;
                }

                if (!crossed && ((forward && ep > to) || (!forward && ep < to)))
                    e.fired = false;
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
            return look * Quaternion.Inverse(AxisToRotation(forwardAxis));
        }

        public static Quaternion AxisToRotation(VFXSplineForwardAxis axis)
        {
            switch (axis)
            {
                case VFXSplineForwardAxis.ZPositive: return Quaternion.identity;
                case VFXSplineForwardAxis.ZNegative: return Quaternion.Euler(0f, 180f, 0f);
                case VFXSplineForwardAxis.XPositive: return Quaternion.Euler(0f, -90f, 0f);
                case VFXSplineForwardAxis.XNegative: return Quaternion.Euler(0f, 90f, 0f);
                case VFXSplineForwardAxis.YPositive: return Quaternion.Euler(90f, 0f, 0f);
                case VFXSplineForwardAxis.YNegative: return Quaternion.Euler(-90f, 0f, 0f);
                default: return Quaternion.identity;
            }
        }
    }
}

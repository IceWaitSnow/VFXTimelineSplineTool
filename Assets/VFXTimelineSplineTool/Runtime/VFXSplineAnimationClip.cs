using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace VFXTimelineSplineTool
{
    [System.Serializable]
    public class VFXSplineAnimationClip : PlayableAsset, ITimelineClipAsset
    {
        public ExposedReference<VFXSimpleSpline> spline;
        public VFXSplineAnimationBehaviour template = new VFXSplineAnimationBehaviour();

        [Header("Bake To AnimationClip / 烘焙")]
        [Tooltip("要写入烘焙 AnimationClip 的目标物体。通常就是这条 Timeline Track 绑定的物体。")]
        public ExposedReference<Transform> bakeTarget;

        [Tooltip("烘焙输出帧率。")]
        public int bakeFrameRate = 60;

        [Tooltip("开启后，会自动使用当前 Timeline Clip 的 Duration 作为烘焙时长。")]
        public bool bakeUseTimelineClipDuration = true;

        [Tooltip("烘焙时长，单位秒。请填成 Timeline Clip Timing 里的 Duration。")]
        public float bakeDuration = 1f;

        [Tooltip("是否烘焙 Local Position。")]
        public bool bakePosition = true;

        [Tooltip("是否烘焙 Local Rotation。")]
        public bool bakeRotation = true;

        [Tooltip("烘焙文件保存目录。")]
        public string bakeSaveFolder = "Assets/Animations/SplineBakes";

        [Tooltip("烘焙 AnimationClip 文件名。为空时自动使用目标物体名。")]
        public string bakeClipName = "";

        [Tooltip("烘焙完成后，如果目标物体没有 Animator，则自动添加 Animator 组件。")]
        public bool bakeAddAnimatorIfMissing = true;

        [Tooltip("烘焙关键帧间隔。1=每帧记录，2=每 2 帧记录一次，5=每 5 帧记录一次。")]
        [Min(1)] public int bakeKeyframeStep = 1;

        [Tooltip("开启后，无论 Keyframe Step 是多少，都会强制保留第一帧和最后一帧。")]
        public bool bakeAlwaysKeyStartAndEnd = true;

        [Tooltip("开启后，会在 Keyframe Step 的基础上按误差阈值自动删除多余关键帧。")]
        public bool bakeOptimizeCurves = false;

        [Tooltip("位置优化误差阈值，单位为 Unity 距离。值越大关键帧越少，但路径误差越大。")]
        public float bakePositionTolerance = 0.005f;

        [Tooltip("旋转优化误差阈值，单位为角度。值越大关键帧越少，但旋转误差越大。")]
        public float bakeRotationTolerance = 0.25f;

        public ClipCaps clipCaps
        {
            get { return ClipCaps.Blending | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier; }
        }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            ScriptPlayable<VFXSplineAnimationBehaviour> playable = ScriptPlayable<VFXSplineAnimationBehaviour>.Create(graph, template);
            VFXSplineAnimationBehaviour behaviour = playable.GetBehaviour();
            behaviour.spline = spline.Resolve(graph.GetResolver());
            return playable;
        }
    }
}

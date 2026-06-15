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

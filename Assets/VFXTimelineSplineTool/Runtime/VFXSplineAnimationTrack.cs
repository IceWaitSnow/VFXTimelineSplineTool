using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace VFXTimelineSplineTool
{
    [TrackColor(1f, 0.68f, 0.02f)]
    [TrackClipType(typeof(VFXSplineAnimationClip))]
    [TrackBindingType(typeof(Transform))]
    public class VFXSplineAnimationTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<VFXSplineAnimationMixer>.Create(graph, inputCount);
        }
    }
}

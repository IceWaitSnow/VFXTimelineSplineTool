using UnityEngine;
using UnityEngine.Playables;

namespace VFXTimelineSplineTool
{
    public class VFXSplineAnimationMixer : PlayableBehaviour
    {
        public VFXSplineAnimationMixer() { }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            Transform target = playerData as Transform;
            if (target == null)
                return;

            int inputCount = playable.GetInputCount();
            if (inputCount == 0)
                return;

            Vector3 blendedPosition = Vector3.zero;
            Quaternion blendedRotation = Quaternion.identity;
            bool hasBlendedRotation = false;
            float totalPositionWeight = 0f;
            float totalRotationWeight = 0f;

            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0.0001f)
                    continue;

                ScriptPlayable<VFXSplineAnimationBehaviour> inputPlayable = (ScriptPlayable<VFXSplineAnimationBehaviour>)playable.GetInput(i);
                VFXSplineAnimationBehaviour behaviour = inputPlayable.GetBehaviour();
                if (behaviour == null)
                    continue;

                Vector3 position;
                Quaternion rotation;
                bool hasRotation;
                float progress;
                if (!behaviour.EvaluatePose(inputPlayable, target, out position, out rotation, out hasRotation, out progress))
                    continue;

                blendedPosition += position * weight;
                totalPositionWeight += weight;

                if (hasRotation)
                {
                    if (!hasBlendedRotation)
                    {
                        blendedRotation = rotation;
                        hasBlendedRotation = true;
                        totalRotationWeight = weight;
                    }
                    else
                    {
                        float t = weight / (totalRotationWeight + weight);
                        blendedRotation = Quaternion.Slerp(blendedRotation, rotation, t);
                        totalRotationWeight += weight;
                    }
                }
            }

            if (totalPositionWeight > 0.0001f)
                target.position = blendedPosition / totalPositionWeight;

            if (hasBlendedRotation)
                target.rotation = blendedRotation;
        }
    }
}

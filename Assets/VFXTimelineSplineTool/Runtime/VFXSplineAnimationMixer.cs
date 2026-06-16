using UnityEngine;
using UnityEngine.Playables;

namespace VFXTimelineSplineTool
{
    /// <summary>
    /// Timeline Track Mixer.
    /// v2.6.4：真正处理 Timeline Clip Blending。
    /// 之前每个 Clip Behaviour 会各自写 Transform，Clip 重叠时谁最后执行谁生效；
    /// 现在由 Mixer 根据 input weight 统一混合位置和旋转，再最终写入绑定的 Transform。
    /// </summary>
    public class VFXSplineAnimationMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            Transform target = playerData as Transform;
            if (target == null)
                return;

            int inputCount = playable.GetInputCount();
            if (inputCount <= 0)
                return;

            bool hasAny = false;
            float totalWeight = 0f;
            Vector3 blendedPosition = Vector3.zero;
            Quaternion blendedRotation = Quaternion.identity;
            bool hasRotation = false;

            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0.0001f)
                    continue;

                Playable inputPlayable = playable.GetInput(i);
                if (!inputPlayable.IsValid())
                    continue;

                ScriptPlayable<VFXSplineAnimationBehaviour> scriptPlayable = (ScriptPlayable<VFXSplineAnimationBehaviour>)inputPlayable;
                VFXSplineAnimationBehaviour behaviour = scriptPlayable.GetBehaviour();
                if (behaviour == null)
                    continue;

                VFXSplineAnimationBehaviour.EvaluationResult result;
                if (!behaviour.Evaluate(scriptPlayable, target, out result))
                    continue;

                blendedPosition += result.position * weight;

                if (result.hasRotation)
                {
                    if (!hasRotation)
                    {
                        blendedRotation = result.rotation;
                        hasRotation = true;
                    }
                    else
                    {
                        float t = weight / Mathf.Max(0.0001f, totalWeight + weight);
                        blendedRotation = Quaternion.Slerp(blendedRotation, result.rotation, t);
                    }
                }

                totalWeight += weight;
                hasAny = true;
            }

            if (!hasAny || totalWeight <= 0.0001f)
                return;

            blendedPosition /= totalWeight;
            target.position = blendedPosition;

            if (hasRotation)
                target.rotation = NormalizeQuaternion(blendedRotation);
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 0.000001f)
                return Quaternion.identity;

            return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }
    }
}

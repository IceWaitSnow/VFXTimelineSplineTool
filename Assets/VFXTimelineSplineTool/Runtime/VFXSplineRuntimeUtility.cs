using UnityEngine;

namespace VFXTimelineSplineTool
{
    /// <summary>
    /// Animator 预览与烘焙共用的 Runtime 辅助方法。
    ///
    /// Animator 预览、Timeline 计算和烘焙共用的辅助方法。
    /// </summary>
    public static class VFXSplineRuntimeUtility
    {
        public static Vector3 GetPoint(VFXSimpleSpline spline, float progress, bool distanceBased)
        {
            if (spline == null)
                return Vector3.zero;

            progress = Mathf.Clamp01(progress);
            if (!distanceBased)
                return spline.GetPoint(progress, false);

            float rawProgress;
            if (TryDistanceProgressToRawProgress(spline, progress, out rawProgress))
                return spline.GetPointByRawProgress(rawProgress);

            return spline.GetPoint(progress, true);
        }

        public static Vector3 GetTangent(VFXSimpleSpline spline, float progress, bool distanceBased)
        {
            if (spline == null)
                return Vector3.forward;

            Vector3 tangent = spline.GetTangent(progress, distanceBased);

            if (tangent.sqrMagnitude < 0.000001f)
                tangent = spline.transform.forward;

            return tangent.sqrMagnitude < 0.000001f ? Vector3.forward : tangent.normalized;
        }

        public static bool TryDistanceProgressToRawProgress(VFXSimpleSpline spline, float distanceProgress, out float rawProgress)
        {
            rawProgress = Mathf.Clamp01(distanceProgress);

            if (spline == null)
                return false;

            return spline.TryDistanceProgressToRawProgress(distanceProgress, out rawProgress);
        }
    }
}

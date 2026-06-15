using UnityEngine;

namespace VFXTimelineSplineTool
{
    /// <summary>
    /// Runtime helper methods shared by animator preview and bake.
    ///
    /// Shared helpers for animator preview, Timeline evaluation, and bake.
    /// </summary>
    public static class VFXSplineRuntimeUtility
    {
        private const float TangentDelta = 0.001f;

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

            progress = Mathf.Clamp01(progress);
            float a = Mathf.Clamp01(progress - TangentDelta);
            float b = Mathf.Clamp01(progress + TangentDelta);
            Vector3 p0 = GetPoint(spline, a, distanceBased);
            Vector3 p1 = GetPoint(spline, b, distanceBased);
            Vector3 tangent = p1 - p0;

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

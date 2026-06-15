using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace VFXTimelineSplineTool
{
    /// <summary>
    /// Runtime helper methods shared by animator preview and bake.
    ///
    /// VFXSimpleSpline keeps its distance cache private for compatibility with existing scenes.
    /// This utility reads that cache through cached reflection and uses binary search when
    /// distance based progress is requested. If the internal cache layout changes, it safely
    /// falls back to VFXSimpleSpline.GetPoint / GetTangent.
    /// </summary>
    public static class VFXSplineRuntimeUtility
    {
        private const float TangentDelta = 0.001f;

        private static readonly FieldInfo CachedTField = typeof(VFXSimpleSpline).GetField("cachedT", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CachedDistanceField = typeof(VFXSimpleSpline).GetField("cachedDistance", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CachedLengthField = typeof(VFXSimpleSpline).GetField("cachedLength", BindingFlags.Instance | BindingFlags.NonPublic);

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

            if (spline == null || CachedTField == null || CachedDistanceField == null || CachedLengthField == null)
                return false;

            spline.RebuildDistanceCacheIfNeeded();

            List<float> cachedT = CachedTField.GetValue(spline) as List<float>;
            List<float> cachedDistance = CachedDistanceField.GetValue(spline) as List<float>;
            object lengthValue = CachedLengthField.GetValue(spline);

            if (cachedT == null || cachedDistance == null || cachedT.Count < 2 || cachedDistance.Count < 2 || cachedT.Count != cachedDistance.Count)
                return false;

            float cachedLength = lengthValue is float ? (float)lengthValue : 0f;
            if (cachedLength <= 0.00001f)
                return false;

            float targetDistance = Mathf.Clamp01(distanceProgress) * cachedLength;

            if (targetDistance <= 0f)
            {
                rawProgress = 0f;
                return true;
            }

            if (targetDistance >= cachedLength)
            {
                rawProgress = 1f;
                return true;
            }

            int index = cachedDistance.BinarySearch(targetDistance);
            if (index < 0)
                index = ~index;

            index = Mathf.Clamp(index, 1, cachedDistance.Count - 1);

            float d0 = cachedDistance[index - 1];
            float d1 = cachedDistance[index];
            float lerp = Mathf.Approximately(d0, d1) ? 0f : Mathf.InverseLerp(d0, d1, targetDistance);
            rawProgress = Mathf.Lerp(cachedT[index - 1], cachedT[index], lerp);
            rawProgress = Mathf.Clamp01(rawProgress);
            return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace VFXTimelineSplineTool
{
    /// <summary>
    /// 自定义路径预设资源。
    /// 只保存最终 Local Points，不保存 Shape Preset 参数，便于团队通过 SVN/Git 共享。
    /// </summary>
    public class VFXUserPathPreset : ScriptableObject
    {
        public string presetName = "New Path Preset";
        public List<Vector3> localPoints = new List<Vector3>();

        [Header("Optional Display Settings")]
        public bool saveDisplaySettings = false;
        public Color pathColor = new Color(1f, 0.68f, 0.02f, 1f);
        public Color progressMarkColor = new Color(0.1f, 0.72f, 1f, 1f);
        public float lineWidth = 3f;
        public float pointSize = 0.15f;
        public int resolution = 48;
        public int distanceSampleResolution = 256;

        public void CaptureFrom(VFXSimpleSpline spline, string displayName, bool includeDisplaySettings)
        {
            if (spline == null) return;

            presetName = string.IsNullOrEmpty(displayName) ? spline.name : displayName;
            localPoints = spline.localPoints != null ? new List<Vector3>(spline.localPoints) : new List<Vector3>();

            saveDisplaySettings = includeDisplaySettings;
            if (includeDisplaySettings)
            {
                pathColor = spline.pathColor;
                progressMarkColor = spline.progressMarkColor;
                lineWidth = spline.lineWidth;
                pointSize = spline.pointSize;
                resolution = spline.resolution;
                distanceSampleResolution = spline.distanceSampleResolution;
            }
        }

        public void ApplyTo(VFXSimpleSpline spline)
        {
            if (spline == null) return;

            spline.localPoints = localPoints != null ? new List<Vector3>(localPoints) : new List<Vector3>();
            if (spline.localPoints.Count < 2)
            {
                spline.localPoints.Clear();
                spline.localPoints.Add(Vector3.zero);
                spline.localPoints.Add(Vector3.right);
            }

            if (saveDisplaySettings)
            {
                spline.pathColor = pathColor;
                spline.progressMarkColor = progressMarkColor;
                spline.lineWidth = Mathf.Max(1f, lineWidth);
                spline.pointSize = Mathf.Max(0.01f, pointSize);
                spline.resolution = Mathf.Clamp(resolution, 8, 256);
                spline.distanceSampleResolution = Mathf.Clamp(distanceSampleResolution, 32, 2048);
            }

            spline.MarkDistanceCacheDirty();
        }
    }
}

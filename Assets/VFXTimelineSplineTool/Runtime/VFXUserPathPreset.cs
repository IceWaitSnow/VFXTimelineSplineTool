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
        public VFXSplinePathMode pathMode = VFXSplinePathMode.CatmullRom;
        public List<Vector3> localPoints = new List<Vector3>();
        public List<VFXBezierPoint> bezierPoints = new List<VFXBezierPoint>();

        [Header("可选显示设置")]
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
            pathMode = spline.pathMode;
            if (spline.pathMode == VFXSplinePathMode.Bezier && spline.bezierPoints != null)
            {
                localPoints = new List<Vector3>();
                for (int i = 0; i < spline.bezierPoints.Count; i++)
                    localPoints.Add(spline.bezierPoints[i] != null ? spline.bezierPoints[i].position : Vector3.zero);
            }
            else
            {
                localPoints = spline.localPoints != null ? new List<Vector3>(spline.localPoints) : new List<Vector3>();
            }

            bezierPoints = CloneBezierPoints(spline.bezierPoints);

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

            spline.pathMode = pathMode;
            spline.localPoints = localPoints != null ? new List<Vector3>(localPoints) : new List<Vector3>();
            if (spline.localPoints.Count < 2)
            {
                spline.localPoints.Clear();
                spline.localPoints.Add(Vector3.zero);
                spline.localPoints.Add(Vector3.right);
            }

            spline.bezierPoints = CloneBezierPoints(bezierPoints);
            if (spline.pathMode == VFXSplinePathMode.Bezier && (spline.bezierPoints == null || spline.bezierPoints.Count < 2))
                spline.ConvertCatmullRomToBezier();

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

        private static List<VFXBezierPoint> CloneBezierPoints(List<VFXBezierPoint> source)
        {
            List<VFXBezierPoint> result = new List<VFXBezierPoint>();
            if (source == null)
                return result;

            for (int i = 0; i < source.Count; i++)
            {
                VFXBezierPoint p = source[i];
                if (p == null)
                {
                    result.Add(new VFXBezierPoint(Vector3.right * i));
                    continue;
                }

                result.Add(new VFXBezierPoint()
                {
                    position = p.position,
                    inTangent = p.inTangent,
                    outTangent = p.outTangent,
                    handleMode = p.handleMode
                });
            }

            return result;
        }
    }
}

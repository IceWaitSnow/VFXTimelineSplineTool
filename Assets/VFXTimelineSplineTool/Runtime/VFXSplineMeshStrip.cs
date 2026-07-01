using System.Collections.Generic;
using UnityEngine;

namespace VFXTimelineSplineTool
{
    public enum VFXSplineMeshStripNormalMode
    {
        SplineNormal,
        Recalculate
    }

    public enum VFXSplineMeshStripShapeMode
    {
        Plane,
        Cross,
        Tube,
        Custom
    }

    public enum VFXSplineMeshStripControlMode
    {
        Points,
        Curve
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("VFX Timeline Spline/VFX Spline Mesh Strip")]
    public class VFXSplineMeshStrip : MonoBehaviour
    {
        [Header("Spline")]
        public VFXSimpleSpline spline;

        [Range(0f, 1f)] public float startProgress = 0f;
        [Range(0f, 1f)] public float endProgress = 1f;
        public bool useDistanceBasedProgress = true;

        [Header("Mesh")]
        [Min(0.001f)] public float width = 1f;
        public VFXSplineMeshStripShapeMode shapeMode = VFXSplineMeshStripShapeMode.Plane;
        [Range(1, 512)] public int segments = 64;
        [Range(1, 64)] public int widthSegments = 1;
        [Range(3, 64)] public int tubeSegments = 8;
        public bool customShapeClosed = false;
        public List<Vector2> customShapePoints = new List<Vector2>()
        {
            new Vector2(-0.5f, 0f),
            new Vector2(0.5f, 0f)
        };
        public bool doubleSided = false;
        public bool usePointWidthMultipliers = false;
        public VFXSplineMeshStripControlMode widthControlMode = VFXSplineMeshStripControlMode.Points;
        public AnimationCurve widthMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public bool smoothPointWidth = true;
        public List<float> pointWidthMultipliers = new List<float>();
        public bool usePointTwistDegrees = false;
        public VFXSplineMeshStripControlMode twistControlMode = VFXSplineMeshStripControlMode.Points;
        public AnimationCurve twistDegreesCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public bool smoothPointTwist = true;
        public List<float> pointTwistDegrees = new List<float>();

        [Header("Normals")]
        public VFXSplineMeshStripNormalMode normalMode = VFXSplineMeshStripNormalMode.SplineNormal;

        [Header("UV")]
        public Vector2 uvTiling = Vector2.one;
        public Vector2 uvOffset = Vector2.zero;
        public bool animateUvInPlayMode = false;
        public Vector2 uvScrollSpeed = new Vector2(1f, 0f);

        [Header("Preview")]
        public bool rebuildInEditMode = true;

        private const string GeneratedMeshName = "VFX Spline Mesh Strip";
        private MeshFilter meshFilter;
        private Mesh generatedMesh;
        private Vector2 runtimeUvOffset;

        private class SectionShape
        {
            public Vector2[] points;
            public Vector2[] normals;
            public float[] v;
            public bool closed;

            public int PointCount
            {
                get { return points != null ? points.Length : 0; }
            }

            public int SegmentCount
            {
                get
                {
                    int count = PointCount;
                    if (count < 2)
                        return 0;
                    return closed ? count : count - 1;
                }
            }
        }

        private void Reset()
        {
            spline = GetComponentInParent<VFXSimpleSpline>();
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            RebuildMesh();
        }

        private void OnValidate()
        {
            segments = Mathf.Clamp(segments, 1, 512);
            widthSegments = Mathf.Clamp(widthSegments, 1, 64);
            tubeSegments = Mathf.Clamp(tubeSegments, 3, 64);
            width = Mathf.Max(0.001f, width);
            EnsureCurves();
        }

        private void LateUpdate()
        {
            if (spline == null)
                return;

            if (Application.isPlaying)
            {
                if (animateUvInPlayMode)
                    runtimeUvOffset += uvScrollSpeed * Time.deltaTime;
                RebuildMesh();
            }
            else if (rebuildInEditMode)
            {
                RebuildMesh();
            }
        }

        public void RebuildMesh()
        {
            CacheComponents();
            EnsureMesh();

            if (generatedMesh == null)
                return;

            if (spline == null || segments < 1)
            {
                generatedMesh.Clear();
                return;
            }

            if (usePointWidthMultipliers && widthControlMode == VFXSplineMeshStripControlMode.Points)
                SyncPointWidthMultipliers();
            if (usePointTwistDegrees && twistControlMode == VFXSplineMeshStripControlMode.Points)
                SyncPointTwistDegrees();
            EnsureCurves();

            int sampleCount = segments + 1;
            Vector3[] worldPositions = new Vector3[sampleCount];
            Vector3[] normals = new Vector3[sampleCount];
            float[] rawProgressValues = new float[sampleCount];
            float[] cumulativeLengths = new float[sampleCount];
            float totalLength = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount <= 1 ? 0f : i / (float)(sampleCount - 1);
                float progress = EvaluateProgress(t);
                rawProgressValues[i] = GetRawProgress(progress);
                worldPositions[i] = spline.GetPoint(progress, useDistanceBasedProgress);
                normals[i] = spline.GetNormal(progress, useDistanceBasedProgress);

                if (i > 0)
                {
                    totalLength += Vector3.Distance(worldPositions[i - 1], worldPositions[i]);
                    cumulativeLengths[i] = totalLength;
                }
            }

            List<SectionShape> sectionShapes = BuildSectionShapes();
            int sectionVertexCount = 0;
            int sectionSegmentCount = 0;
            for (int i = 0; i < sectionShapes.Count; i++)
            {
                sectionVertexCount += sectionShapes[i].PointCount;
                sectionSegmentCount += sectionShapes[i].SegmentCount;
            }

            if (sectionVertexCount < 2 || sectionSegmentCount < 1)
            {
                generatedMesh.Clear();
                return;
            }

            int vertexCount = sampleCount * sectionVertexCount;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] meshNormals = normalMode == VFXSplineMeshStripNormalMode.SplineNormal ? new Vector3[vertexCount] : null;
            Vector2[] uvs = new Vector2[vertexCount];
            int triangleMultiplier = doubleSided ? 2 : 1;
            int[] triangles = new int[segments * sectionSegmentCount * 6 * triangleMultiplier];
            Vector2 finalUvOffset = uvOffset + runtimeUvOffset;

            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 tangent = GetStripTangent(worldPositions, i);
                Vector3 normal = GetSafeNormal(normals[i], tangent);
                float twist = EvaluatePointTwistDegrees(rawProgressValues[i]);
                if (!Mathf.Approximately(twist, 0f))
                    normal = Quaternion.AngleAxis(twist, tangent) * normal;

                Vector3 side = Vector3.Cross(normal, tangent);
                if (side.sqrMagnitude < 0.000001f)
                    side = Vector3.Cross(Vector3.up, tangent);
                if (side.sqrMagnitude < 0.000001f)
                    side = Vector3.right;
                side.Normalize();

                float sampleWidth = width * EvaluatePointWidthMultiplier(rawProgressValues[i]);
                float u01 = totalLength > 0.000001f ? cumulativeLengths[i] / totalLength : i / Mathf.Max(1f, sampleCount - 1f);
                float u = finalUvOffset.x + u01 * uvTiling.x;

                int sectionOffset = 0;
                for (int shapeIndex = 0; shapeIndex < sectionShapes.Count; shapeIndex++)
                {
                    SectionShape shape = sectionShapes[shapeIndex];
                    for (int p = 0; p < shape.PointCount; p++)
                    {
                        Vector2 sectionPoint = shape.points[p];
                        Vector3 world = worldPositions[i] + (side * sectionPoint.x + normal * sectionPoint.y) * sampleWidth;
                        int vertexIndex = i * sectionVertexCount + sectionOffset + p;
                        vertices[vertexIndex] = transform.InverseTransformPoint(world);
                        uvs[vertexIndex] = new Vector2(u, finalUvOffset.y + shape.v[p] * uvTiling.y);

                        if (meshNormals != null)
                        {
                            Vector2 sectionNormal2D = shape.normals[p];
                            Vector3 worldNormal = side * sectionNormal2D.x + normal * sectionNormal2D.y;
                            if (worldNormal.sqrMagnitude < 0.000001f)
                                worldNormal = normal;
                            meshNormals[vertexIndex] = transform.InverseTransformDirection(worldNormal.normalized).normalized;
                        }
                    }

                    sectionOffset += shape.PointCount;
                }
            }

            int tri = 0;
            for (int i = 0; i < segments; i++)
            {
                int row = i * sectionVertexCount;
                int nextRow = (i + 1) * sectionVertexCount;
                int sectionOffset = 0;
                for (int shapeIndex = 0; shapeIndex < sectionShapes.Count; shapeIndex++)
                {
                    SectionShape shape = sectionShapes[shapeIndex];
                    for (int s = 0; s < shape.SegmentCount; s++)
                    {
                        int p0 = s;
                        int p1 = s + 1;
                        if (p1 >= shape.PointCount)
                            p1 = 0;

                        int a = row + sectionOffset + p0;
                        int b = row + sectionOffset + p1;
                        int c = nextRow + sectionOffset + p0;
                        int d = nextRow + sectionOffset + p1;

                        triangles[tri++] = a;
                        triangles[tri++] = c;
                        triangles[tri++] = b;
                        triangles[tri++] = b;
                        triangles[tri++] = c;
                        triangles[tri++] = d;

                        if (doubleSided)
                        {
                            triangles[tri++] = a;
                            triangles[tri++] = b;
                            triangles[tri++] = c;
                            triangles[tri++] = b;
                            triangles[tri++] = d;
                            triangles[tri++] = c;
                        }
                    }

                    sectionOffset += shape.PointCount;
                }
            }

            generatedMesh.Clear();
            generatedMesh.vertices = vertices;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles;
            if (meshNormals != null)
                generatedMesh.normals = meshNormals;
            else
                generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();
        }

        public void SyncPointWidthMultipliers()
        {
            int pointCount = spline != null ? spline.GetActivePointCount() : 0;
            if (pointWidthMultipliers == null)
                pointWidthMultipliers = new List<float>();

            while (pointWidthMultipliers.Count < pointCount)
                pointWidthMultipliers.Add(1f);

            while (pointWidthMultipliers.Count > pointCount)
                pointWidthMultipliers.RemoveAt(pointWidthMultipliers.Count - 1);

            for (int i = 0; i < pointWidthMultipliers.Count; i++)
                pointWidthMultipliers[i] = Mathf.Max(0f, pointWidthMultipliers[i]);
        }

        public void SyncPointTwistDegrees()
        {
            int pointCount = spline != null ? spline.GetActivePointCount() : 0;
            if (pointTwistDegrees == null)
                pointTwistDegrees = new List<float>();

            while (pointTwistDegrees.Count < pointCount)
                pointTwistDegrees.Add(0f);

            while (pointTwistDegrees.Count > pointCount)
                pointTwistDegrees.RemoveAt(pointTwistDegrees.Count - 1);
        }

        private void EnsureCurves()
        {
            if (widthMultiplierCurve == null)
                widthMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            if (twistDegreesCurve == null)
                twistDegreesCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        }

        public void ResetCustomShapeToPlane()
        {
            if (customShapePoints == null)
                customShapePoints = new List<Vector2>();

            customShapePoints.Clear();
            customShapePoints.Add(new Vector2(-0.5f, 0f));
            customShapePoints.Add(new Vector2(0.5f, 0f));
            customShapeClosed = false;
        }

        public void ResetCustomShapeToDiamond()
        {
            if (customShapePoints == null)
                customShapePoints = new List<Vector2>();

            customShapePoints.Clear();
            customShapePoints.Add(new Vector2(0f, 0.5f));
            customShapePoints.Add(new Vector2(0.5f, 0f));
            customShapePoints.Add(new Vector2(0f, -0.5f));
            customShapePoints.Add(new Vector2(-0.5f, 0f));
            customShapeClosed = true;
        }

        private List<SectionShape> BuildSectionShapes()
        {
            List<SectionShape> shapes = new List<SectionShape>();
            switch (shapeMode)
            {
                case VFXSplineMeshStripShapeMode.Cross:
                    shapes.Add(BuildLineSection(widthSegments, false));
                    shapes.Add(BuildLineSection(widthSegments, true));
                    break;
                case VFXSplineMeshStripShapeMode.Tube:
                    shapes.Add(BuildTubeSection());
                    break;
                case VFXSplineMeshStripShapeMode.Custom:
                    shapes.Add(BuildCustomSection());
                    break;
                case VFXSplineMeshStripShapeMode.Plane:
                default:
                    shapes.Add(BuildLineSection(widthSegments, false));
                    break;
            }

            return shapes;
        }

        private static SectionShape BuildLineSection(int segmentCount, bool vertical)
        {
            segmentCount = Mathf.Clamp(segmentCount, 1, 64);
            int count = segmentCount + 1;
            SectionShape shape = new SectionShape();
            shape.points = new Vector2[count];
            shape.normals = new Vector2[count];
            shape.v = new float[count];
            shape.closed = false;

            for (int i = 0; i < count; i++)
            {
                float v = i / (float)segmentCount;
                float centered = v - 0.5f;
                shape.points[i] = vertical ? new Vector2(0f, centered) : new Vector2(centered, 0f);
                shape.normals[i] = vertical ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
                shape.v[i] = v;
            }

            return shape;
        }

        private SectionShape BuildTubeSection()
        {
            int segmentCount = Mathf.Clamp(tubeSegments, 3, 64);
            int count = segmentCount + 1;
            SectionShape shape = new SectionShape();
            shape.points = new Vector2[count];
            shape.normals = new Vector2[count];
            shape.v = new float[count];
            shape.closed = false;

            for (int i = 0; i < count; i++)
            {
                float v = i / (float)segmentCount;
                float angle = v * Mathf.PI * 2f;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                shape.points[i] = normal * 0.5f;
                shape.normals[i] = normal;
                shape.v[i] = v;
            }

            return shape;
        }

        private SectionShape BuildCustomSection()
        {
            if (customShapePoints == null || customShapePoints.Count < 2)
                ResetCustomShapeToPlane();

            int count = customShapePoints != null ? customShapePoints.Count : 0;
            SectionShape shape = new SectionShape();
            shape.points = new Vector2[count];
            shape.normals = new Vector2[count];
            shape.v = new float[count];
            shape.closed = customShapeClosed && count > 2;

            float totalLength = 0f;
            float[] distances = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 point = customShapePoints[i];
                shape.points[i] = point;

                if (i > 0)
                {
                    totalLength += Vector2.Distance(customShapePoints[i - 1], point);
                    distances[i] = totalLength;
                }
            }

            if (shape.closed)
                totalLength += Vector2.Distance(customShapePoints[count - 1], customShapePoints[0]);

            for (int i = 0; i < count; i++)
            {
                Vector2 previous = i == 0 ? (shape.closed ? customShapePoints[count - 1] : customShapePoints[i]) : customShapePoints[i - 1];
                Vector2 next = i == count - 1 ? (shape.closed ? customShapePoints[0] : customShapePoints[i]) : customShapePoints[i + 1];
                Vector2 tangent = next - previous;
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = Vector2.right;

                Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
                shape.normals[i] = normal;
                shape.v[i] = totalLength > 0.000001f ? distances[i] / totalLength : i / Mathf.Max(1f, count - 1f);
            }

            return shape;
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
        }

        private void EnsureMesh()
        {
            if (meshFilter == null)
                return;

            if (generatedMesh == null)
            {
                generatedMesh = meshFilter.sharedMesh;
                if (generatedMesh == null || generatedMesh.name != GeneratedMeshName)
                {
                    generatedMesh = new Mesh();
                    generatedMesh.name = GeneratedMeshName;
                    generatedMesh.MarkDynamic();
                    generatedMesh.hideFlags = HideFlags.DontSave;
                    meshFilter.sharedMesh = generatedMesh;
                }
            }
            else if (meshFilter.sharedMesh != generatedMesh)
            {
                meshFilter.sharedMesh = generatedMesh;
            }
        }

        private float EvaluateProgress(float t)
        {
            float start = Mathf.Clamp01(startProgress);
            float end = Mathf.Clamp01(endProgress);
            if (spline != null && spline.loop && end < start)
                return Mathf.Repeat(Mathf.Lerp(start, end + 1f, t), 1f);

            return Mathf.Clamp01(Mathf.Lerp(start, end, t));
        }

        private float GetRawProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (!useDistanceBasedProgress || spline == null)
                return progress;

            float rawProgress;
            return spline.TryDistanceProgressToRawProgress(progress, out rawProgress) ? rawProgress : progress;
        }

        private float EvaluatePointWidthMultiplier(float rawProgress)
        {
            if (!usePointWidthMultipliers || spline == null)
                return 1f;

            if (widthControlMode == VFXSplineMeshStripControlMode.Curve)
            {
                EnsureCurves();
                return Mathf.Max(0f, widthMultiplierCurve.Evaluate(Mathf.Clamp01(rawProgress)));
            }

            int count = pointWidthMultipliers != null ? pointWidthMultipliers.Count : 0;
            if (count <= 0)
                return 1f;
            if (count == 1)
                return Mathf.Max(0f, pointWidthMultipliers[0]);

            rawProgress = Mathf.Clamp01(rawProgress);
            if (spline.loop && rawProgress >= 1f)
                rawProgress = 0f;

            float scaled = rawProgress * (spline.loop ? count : count - 1);
            int index = Mathf.FloorToInt(scaled);
            float t = scaled - Mathf.Floor(scaled);
            int nextIndex;

            if (spline.loop)
            {
                if (index >= count)
                    index = 0;
                nextIndex = (index + 1) % count;
            }
            else
            {
                if (index >= count - 1)
                {
                    index = count - 2;
                    t = 1f;
                }
                nextIndex = Mathf.Min(index + 1, count - 1);
            }

            if (smoothPointWidth)
                t = t * t * (3f - 2f * t);

            float a = Mathf.Max(0f, pointWidthMultipliers[Mathf.Clamp(index, 0, count - 1)]);
            float b = Mathf.Max(0f, pointWidthMultipliers[Mathf.Clamp(nextIndex, 0, count - 1)]);
            return Mathf.Lerp(a, b, t);
        }

        private float EvaluatePointTwistDegrees(float rawProgress)
        {
            if (!usePointTwistDegrees || spline == null)
                return 0f;

            if (twistControlMode == VFXSplineMeshStripControlMode.Curve)
            {
                EnsureCurves();
                return twistDegreesCurve.Evaluate(Mathf.Clamp01(rawProgress));
            }

            int count = pointTwistDegrees != null ? pointTwistDegrees.Count : 0;
            if (count <= 0)
                return 0f;
            if (count == 1)
                return pointTwistDegrees[0];

            rawProgress = Mathf.Clamp01(rawProgress);
            if (spline.loop && rawProgress >= 1f)
                rawProgress = 0f;

            float scaled = rawProgress * (spline.loop ? count : count - 1);
            int index = Mathf.FloorToInt(scaled);
            float t = scaled - Mathf.Floor(scaled);
            int nextIndex;

            if (spline.loop)
            {
                if (index >= count)
                    index = 0;
                nextIndex = (index + 1) % count;
            }
            else
            {
                if (index >= count - 1)
                {
                    index = count - 2;
                    t = 1f;
                }
                nextIndex = Mathf.Min(index + 1, count - 1);
            }

            if (smoothPointTwist)
                t = t * t * (3f - 2f * t);

            float a = pointTwistDegrees[Mathf.Clamp(index, 0, count - 1)];
            float b = pointTwistDegrees[Mathf.Clamp(nextIndex, 0, count - 1)];
            return Mathf.LerpAngle(a, b, t);
        }

        private static Vector3 GetStripTangent(Vector3[] points, int index)
        {
            if (points == null || points.Length < 2)
                return Vector3.forward;

            if (index <= 0)
                return SafeDirection(points[1] - points[0]);
            if (index >= points.Length - 1)
                return SafeDirection(points[points.Length - 1] - points[points.Length - 2]);

            return SafeDirection(points[index + 1] - points[index - 1]);
        }

        private static Vector3 GetSafeNormal(Vector3 normal, Vector3 tangent)
        {
            normal -= tangent * Vector3.Dot(normal, tangent);
            if (normal.sqrMagnitude < 0.000001f)
                normal = Vector3.up - tangent * Vector3.Dot(Vector3.up, tangent);
            if (normal.sqrMagnitude < 0.000001f)
                normal = Vector3.forward - tangent * Vector3.Dot(Vector3.forward, tangent);
            if (normal.sqrMagnitude < 0.000001f)
                normal = Vector3.up;

            return normal.normalized;
        }

        private static Vector3 SafeDirection(Vector3 value)
        {
            if (value.sqrMagnitude < 0.000001f)
                return Vector3.forward;

            return value.normalized;
        }
    }
}

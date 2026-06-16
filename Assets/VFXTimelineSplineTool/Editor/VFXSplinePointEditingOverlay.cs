#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    /// <summary>
    /// Scene View point picker for VFXSimpleSpline.
    /// v2.6.5: adds offset selection handles so Point 0 is no longer hidden by the Spline object's transform gizmo.
    /// This file is intentionally independent from VFXSimpleSplineEditor so it can be dropped into existing projects safely.
    /// </summary>
    [InitializeOnLoad]
    public static class VFXSplinePointEditingOverlay
    {
        private const string EnabledKey = "VFXTimelineSplineTool.PointOverlay.Enabled";
        private const string OffsetKey = "VFXTimelineSplineTool.PointOverlay.OffsetScale";
        private const string LargerFirstKey = "VFXTimelineSplineTool.PointOverlay.LargerFirstPoint";

        private static readonly Color NormalPointColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color SelectedPointColor = new Color(1f, 0.86f, 0.1f, 1f);
        private static readonly Color FirstPointColor = new Color(0.2f, 1f, 1f, 1f);
        private static readonly Color LastPointColor = new Color(1f, 0.55f, 0.05f, 1f);
        private static readonly Color ConnectorColor = new Color(1f, 1f, 1f, 0.35f);

        public static bool Enabled
        {
            get { return EditorPrefs.GetBool(EnabledKey, true); }
            set
            {
                EditorPrefs.SetBool(EnabledKey, value);
                SceneView.RepaintAll();
            }
        }

        public static float OffsetScale
        {
            get { return EditorPrefs.GetFloat(OffsetKey, 0.28f); }
            set
            {
                EditorPrefs.SetFloat(OffsetKey, Mathf.Clamp(value, 0f, 1.5f));
                SceneView.RepaintAll();
            }
        }

        public static bool LargerFirstPoint
        {
            get { return EditorPrefs.GetBool(LargerFirstKey, true); }
            set
            {
                EditorPrefs.SetBool(LargerFirstKey, value);
                SceneView.RepaintAll();
            }
        }

        static VFXSplinePointEditingOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/VFX Timeline Spline/Toggle Point Selection Overlay")]
        private static void ToggleOverlay()
        {
            Enabled = !Enabled;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!Enabled || sceneView == null)
                return;

            List<VFXSimpleSpline> splines = GetSelectedSplines();
            if (splines.Count == 0)
                return;

            HandleKeyboardShortcuts(sceneView, splines[0]);

            for (int i = 0; i < splines.Count; i++)
                DrawSplinePointHandles(sceneView, splines[i]);
        }

        private static List<VFXSimpleSpline> GetSelectedSplines()
        {
            List<VFXSimpleSpline> result = new List<VFXSimpleSpline>();
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null)
                    continue;

                VFXSimpleSpline spline = selected[i].GetComponent<VFXSimpleSpline>();
                if (spline != null && !result.Contains(spline))
                    result.Add(spline);
            }
            return result;
        }

        private static void HandleKeyboardShortcuts(SceneView sceneView, VFXSimpleSpline spline)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown || spline == null)
                return;

            int count = GetPointCount(spline);
            if (count <= 0)
                return;

            int index = Mathf.Clamp(spline.selectedPointIndex, 0, count - 1);

            if (e.keyCode == KeyCode.F && !e.alt && !e.control && !e.command)
            {
                FramePoint(sceneView, spline, index);
                e.Use();
            }
            else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                DeletePoint(spline, index);
                e.Use();
            }
            else if (e.keyCode == KeyCode.A && e.shift)
            {
                AddPointAtEnd(spline);
                e.Use();
            }
        }

        private static void DrawSplinePointHandles(SceneView sceneView, VFXSimpleSpline spline)
        {
            if (spline == null)
                return;

            int count = GetPointCount(spline);
            if (count <= 0)
                return;

            if (spline.selectedPointIndex < 0 || spline.selectedPointIndex >= count)
                spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, count - 1);

            for (int i = 0; i < count; i++)
                DrawPointHandle(sceneView, spline, i, count);
        }

        private static void DrawPointHandle(SceneView sceneView, VFXSimpleSpline spline, int index, int count)
        {
            Vector3 world = GetPointWorld(spline, index);
            Vector3 offset = GetHandleOffset(sceneView, world, index);
            Vector3 displayPosition = world + offset;

            bool selected = spline.selectedPointIndex == index;
            float baseSize = HandleUtility.GetHandleSize(displayPosition) * Mathf.Max(0.03f, spline.pointSize);
            float capSize = baseSize * (selected ? 1.75f : 1.25f);
            if (index == 0 && LargerFirstPoint)
                capSize *= 1.45f;

            Handles.color = ConnectorColor;
            if (offset.sqrMagnitude > 0.000001f)
                Handles.DrawLine(world, displayPosition);

            Handles.color = GetPointColor(index, count, selected);
            bool clicked = Handles.Button(displayPosition, Quaternion.identity, capSize, capSize * 1.25f, Handles.SphereHandleCap);

            if (clicked)
            {
                Undo.RecordObject(spline, "Select Spline Point");
                spline.selectedPointIndex = index;
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }

            if (selected)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newDisplayPosition = Handles.PositionHandle(displayPosition, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 deltaWorld = newDisplayPosition - displayPosition;
                    MovePointByWorldDelta(spline, index, deltaWorld);
                }
            }

            if (spline.showPointLabels || selected || index == 0 || index == count - 1)
            {
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = GetPointColor(index, count, selected);
                string label = index == 0 ? "P0 Start" : (index == count - 1 ? "P" + index + " End" : "P" + index);
                Handles.Label(displayPosition + Vector3.up * capSize * 1.35f, label, style);
            }
        }

        private static Vector3 GetHandleOffset(SceneView sceneView, Vector3 world, int index)
        {
            float scale = OffsetScale;
            if (index == 0)
                scale = Mathf.Max(scale, 0.38f);

            if (scale <= 0.0001f || sceneView.camera == null)
                return Vector3.zero;

            Transform cam = sceneView.camera.transform;
            Vector3 dir = (cam.right * 0.86f + cam.up * 0.52f).normalized;
            return dir * HandleUtility.GetHandleSize(world) * scale;
        }

        private static Color GetPointColor(int index, int count, bool selected)
        {
            if (selected) return SelectedPointColor;
            if (index == 0) return FirstPointColor;
            if (index == count - 1) return LastPointColor;
            return NormalPointColor;
        }

        internal static int GetPointCount(VFXSimpleSpline spline)
        {
            if (spline == null)
                return 0;

            if (spline.pathMode == VFXSplinePathMode.Bezier)
                return spline.bezierPoints != null ? spline.bezierPoints.Count : 0;

            return spline.localPoints != null ? spline.localPoints.Count : 0;
        }

        internal static Vector3 GetPointLocal(VFXSimpleSpline spline, int index)
        {
            if (spline.pathMode == VFXSplinePathMode.Bezier)
                return spline.bezierPoints[index].position;
            return spline.localPoints[index];
        }

        internal static Vector3 GetPointWorld(VFXSimpleSpline spline, int index)
        {
            if (spline == null)
                return Vector3.zero;

            int count = GetPointCount(spline);
            if (count <= 0)
                return spline.transform.position;

            index = Mathf.Clamp(index, 0, count - 1);

            if (spline.enableDynamicStartEndBinding)
            {
                if (index == 0 && spline.dynamicStartTransform != null)
                    return spline.dynamicStartTransform.position;
                if (index == count - 1 && spline.dynamicEndTransform != null)
                    return spline.dynamicEndTransform.position;
            }

            return spline.transform.TransformPoint(GetPointLocal(spline, index));
        }

        internal static void SetPointWorld(VFXSimpleSpline spline, int index, Vector3 world)
        {
            if (spline == null)
                return;

            int count = GetPointCount(spline);
            if (count <= 0)
                return;

            index = Mathf.Clamp(index, 0, count - 1);

            if (spline.enableDynamicStartEndBinding)
            {
                if (index == 0 && spline.dynamicStartTransform != null)
                {
                    Undo.RecordObject(spline.dynamicStartTransform, "Move Dynamic Start Point");
                    spline.dynamicStartTransform.position = world;
                    EditorUtility.SetDirty(spline.dynamicStartTransform);
                    spline.MarkDistanceCacheDirty();
                    EditorUtility.SetDirty(spline);
                    return;
                }

                if (index == count - 1 && spline.dynamicEndTransform != null)
                {
                    Undo.RecordObject(spline.dynamicEndTransform, "Move Dynamic End Point");
                    spline.dynamicEndTransform.position = world;
                    EditorUtility.SetDirty(spline.dynamicEndTransform);
                    spline.MarkDistanceCacheDirty();
                    EditorUtility.SetDirty(spline);
                    return;
                }
            }

            Vector3 local = spline.transform.InverseTransformPoint(world);
            SetPointLocal(spline, index, local);
        }

        internal static void SetPointLocal(VFXSimpleSpline spline, int index, Vector3 local)
        {
            if (spline == null)
                return;

            Undo.RecordObject(spline, "Move Spline Point");
            int count = GetPointCount(spline);
            if (count <= 0)
                return;

            index = Mathf.Clamp(index, 0, count - 1);
            if (spline.pathMode == VFXSplinePathMode.Bezier)
            {
                VFXBezierPoint point = spline.bezierPoints[index];
                point.position = local;
                spline.bezierPoints[index] = point;
            }
            else
            {
                spline.localPoints[index] = local;
            }

            spline.selectedPointIndex = index;
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        internal static void MovePointByWorldDelta(VFXSimpleSpline spline, int index, Vector3 deltaWorld)
        {
            SetPointWorld(spline, index, GetPointWorld(spline, index) + deltaWorld);
        }

        internal static void FramePoint(SceneView sceneView, VFXSimpleSpline spline, int index)
        {
            if (sceneView == null || spline == null)
                return;

            Vector3 world = GetPointWorld(spline, index);
            float size = Mathf.Max(0.25f, HandleUtility.GetHandleSize(world) * 0.8f);
            sceneView.Frame(new Bounds(world, Vector3.one * size), false);
        }

        internal static void AddPointAtEnd(VFXSimpleSpline spline)
        {
            if (spline == null)
                return;

            Undo.RecordObject(spline, "Add Spline Point At End");
            int count = GetPointCount(spline);
            Vector3 local = count > 0 ? GetPointLocal(spline, count - 1) + Vector3.right : Vector3.zero;

            if (spline.pathMode == VFXSplinePathMode.Bezier)
            {
                if (spline.bezierPoints == null)
                    spline.bezierPoints = new List<VFXBezierPoint>();
                spline.bezierPoints.Add(new VFXBezierPoint(local));
            }
            else
            {
                if (spline.localPoints == null)
                    spline.localPoints = new List<Vector3>();
                spline.localPoints.Add(local);
            }

            spline.selectedPointIndex = GetPointCount(spline) - 1;
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        internal static void InsertPointAfter(VFXSimpleSpline spline, int index)
        {
            if (spline == null)
                return;

            int count = GetPointCount(spline);
            if (count <= 0)
            {
                AddPointAtEnd(spline);
                return;
            }

            index = Mathf.Clamp(index, 0, count - 1);
            int insertIndex = Mathf.Clamp(index + 1, 0, count);
            Vector3 a = GetPointLocal(spline, index);
            Vector3 b = index + 1 < count ? GetPointLocal(spline, index + 1) : a + Vector3.right;
            Vector3 local = Vector3.Lerp(a, b, 0.5f);

            Undo.RecordObject(spline, "Insert Spline Point");
            if (spline.pathMode == VFXSplinePathMode.Bezier)
                spline.bezierPoints.Insert(insertIndex, new VFXBezierPoint(local));
            else
                spline.localPoints.Insert(insertIndex, local);

            spline.selectedPointIndex = insertIndex;
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        internal static void DeletePoint(VFXSimpleSpline spline, int index)
        {
            if (spline == null)
                return;

            int count = GetPointCount(spline);
            if (count <= 2)
            {
                Debug.LogWarning("[VFX Timeline Spline Tool] 至少需要保留 2 个控制点。", spline);
                return;
            }

            index = Mathf.Clamp(index, 0, count - 1);
            Undo.RecordObject(spline, "Delete Spline Point");
            if (spline.pathMode == VFXSplinePathMode.Bezier)
                spline.bezierPoints.RemoveAt(index);
            else
                spline.localPoints.RemoveAt(index);

            spline.selectedPointIndex = Mathf.Clamp(index, 0, GetPointCount(spline) - 1);
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }
    }
}
#endif

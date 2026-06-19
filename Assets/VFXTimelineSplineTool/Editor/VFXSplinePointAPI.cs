#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    public enum VFXSplinePointEditMode
    {
        Object,
        Points
    }

    [InitializeOnLoad]
    public static class VFXSplinePointAPI
    {
        private const string EnabledKey = "VFXTimelineSplineTool.PointOverlay.Enabled";
        private const string EditModeKey = "VFXTimelineSplineTool.PointOverlay.EditMode";
        private const string ActiveSplineIdKey = "VFXTimelineSplineTool.PointOverlay.ActiveSplineId";
        private const string PickSizeMultiplierKey = "VFXTimelineSplineTool.PointOverlay.PickSizeMultiplier";
        private const string LargerFirstPointKey = "VFXTimelineSplineTool.PointOverlay.LargerFirstPoint";

        static VFXSplinePointAPI()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            ApplyToolVisibility();
        }

        private static void OnSelectionChanged()
        {
            ApplyToolVisibility();
            SceneView.RepaintAll();
        }

        public static bool Enabled
        {
            get { return EditorPrefs.GetBool(EnabledKey, true); }
            set
            {
                EditorPrefs.SetBool(EnabledKey, value);
                SceneView.RepaintAll();
            }
        }

        public static VFXSplinePointEditMode EditMode
        {
            get { return (VFXSplinePointEditMode)EditorPrefs.GetInt(EditModeKey, (int)VFXSplinePointEditMode.Object); }
            set
            {
                if (value == VFXSplinePointEditMode.Points)
                    EnterPointMode();
                else
                    EnterObjectMode();
            }
        }

        public static bool IsPointMode
        {
            get { return IsPointEditingActive; }
        }

        public static bool IsPointEditingActive
        {
            get { return GetSelectedSpline() != null; }
        }

        public static void ToggleEditMode()
        {
            Enabled = !Enabled;
        }

        public static float PickSizeMultiplier
        {
            get { return EditorPrefs.GetFloat(PickSizeMultiplierKey, 3f); }
            set
            {
                EditorPrefs.SetFloat(PickSizeMultiplierKey, Mathf.Clamp(value, 1f, 8f));
                SceneView.RepaintAll();
            }
        }

        public static float OffsetScale
        {
            get { return PickSizeMultiplier; }
            set { PickSizeMultiplier = value; }
        }

        public static bool LargerFirstPoint
        {
            get { return EditorPrefs.GetBool(LargerFirstPointKey, true); }
            set
            {
                EditorPrefs.SetBool(LargerFirstPointKey, value);
                SceneView.RepaintAll();
            }
        }

        public static VFXSimpleSpline GetSelectedSpline()
        {
            GameObject go = Selection.activeGameObject;
            return go != null ? go.GetComponent<VFXSimpleSpline>() : null;
        }

        public static VFXSimpleSpline ActiveSpline
        {
            get
            {
                VFXSimpleSpline selected = GetSelectedSpline();
                if (selected != null)
                    return selected;

                return GetStoredActiveSpline();
            }
        }

        public static void EnterPointMode(VFXSimpleSpline spline = null)
        {
            if (spline != null)
                SetActiveSpline(spline);
            else
                CaptureSelectedSplineAsActive();

            Enabled = true;
            EditorPrefs.SetInt(EditModeKey, (int)VFXSplinePointEditMode.Points);
            SceneView.RepaintAll();
        }

        public static void EnterObjectMode()
        {
            EditorPrefs.SetInt(EditModeKey, (int)VFXSplinePointEditMode.Object);
            VFXSplineSceneDrawer.ExitAppendPointMode();
            SceneView.RepaintAll();
        }

        public static void SetActiveSpline(VFXSimpleSpline spline)
        {
            if (spline == null)
                return;

            EditorPrefs.SetInt(ActiveSplineIdKey, spline.GetInstanceID());
            SceneView.RepaintAll();
        }

        public static void CaptureSelectedSplineAsActive()
        {
            VFXSimpleSpline selected = GetSelectedSpline();
            if (selected != null)
                SetActiveSpline(selected);
        }

        private static VFXSimpleSpline GetStoredActiveSpline()
        {
            int id = EditorPrefs.GetInt(ActiveSplineIdKey, 0);
            return id != 0 ? EditorUtility.InstanceIDToObject(id) as VFXSimpleSpline : null;
        }

        public static void ApplyToolVisibility()
        {
            Tools.hidden = false;
        }

        public static void RestoreToolVisibility()
        {
            Tools.hidden = false;
        }

        public static int GetPointCount(VFXSimpleSpline spline)
        {
            return spline != null ? spline.GetActivePointCount() : 0;
        }

        public static Vector3 GetPointLocal(VFXSimpleSpline spline, int index)
        {
            if (spline == null)
                return Vector3.zero;

            int count = spline.GetActivePointCount();
            if (count <= 0)
                return Vector3.zero;

            index = Mathf.Clamp(index, 0, count - 1);
            return spline.pathMode == VFXSplinePathMode.Bezier
                ? spline.GetEffectiveBezierPosition(index)
                : spline.GetEffectiveLocalPoint(index);
        }

        public static Vector3 GetPointWorld(VFXSimpleSpline spline, int index)
        {
            if (spline == null)
                return Vector3.zero;

            int count = spline.GetActivePointCount();
            if (count <= 0)
                return spline.transform.position;

            return spline.GetEffectiveWorldPoint(Mathf.Clamp(index, 0, count - 1));
        }

        public static void SelectPoint(VFXSimpleSpline spline, int index)
        {
            if (spline == null || spline.GetActivePointCount() <= 0)
                return;

            Undo.RecordObject(spline, "Select Spline Point");
            spline.selectedPointIndex = Mathf.Clamp(index, 0, spline.GetActivePointCount() - 1);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        public static void SetPointLocal(VFXSimpleSpline spline, int index, Vector3 localPosition)
        {
            if (spline == null)
                return;

            SetPointWorld(spline, index, spline.transform.TransformPoint(localPosition));
        }

        public static void SetPointWorld(VFXSimpleSpline spline, int index, Vector3 worldPosition)
        {
            if (spline == null)
                return;

            int count = spline.GetActivePointCount();
            if (count <= 0)
                return;

            index = Mathf.Clamp(index, 0, count - 1);
            Transform dynamicBinding = spline.GetDynamicBindingTransformForPoint(index);
            if (spline.IsPointDynamicallyBound(index) && dynamicBinding != null)
            {
                Undo.RecordObject(dynamicBinding, "Move Dynamic Spline Point Binding");
                dynamicBinding.position = worldPosition;
                EditorUtility.SetDirty(dynamicBinding);
            }
            else
            {
                Undo.RecordObject(spline, "Move Spline Point");
                spline.SetActivePointWorldPosition(index, worldPosition);
                EditorUtility.SetDirty(spline);
            }

            spline.MarkDistanceCacheDirty();
            SceneView.RepaintAll();
        }

        public static void AddPointAtEnd()
        {
            AddPointAtEnd(GetSelectedSpline());
        }

        public static void AddPointAtEnd(VFXSimpleSpline spline)
        {
            if (spline == null)
                return;

            Undo.RecordObject(spline, "Add Spline Point");
            Vector3 worldPosition;
            if (TryGetMouseWorldPointOnEditPlane(spline, out worldPosition))
                spline.AppendPointAtWorldPosition(worldPosition);
            else
                spline.AddPoint();

            spline.selectedPointIndex = Mathf.Clamp(spline.GetActivePointCount() - 1, 0, spline.GetActivePointCount() - 1);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static bool TryGetMouseWorldPointOnEditPlane(VFXSimpleSpline spline, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            Event e = Event.current;
            if (spline == null || e == null)
                return false;

            SceneView sceneView = SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView : SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
                return false;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 planePoint = spline.transform.position;
            int count = spline.GetActivePointCount();
            if (count > 0)
            {
                int selectedIndex = Mathf.Clamp(spline.selectedPointIndex, 0, count - 1);
                planePoint = spline.GetEffectiveWorldPoint(selectedIndex);
            }

            Plane plane = new Plane(sceneView.camera.transform.forward, planePoint);
            float distance;
            if (!plane.Raycast(ray, out distance))
                return false;

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        public static void InsertPointAfter()
        {
            VFXSimpleSpline spline = GetSelectedSpline();
            if (spline != null)
                InsertPointAfter(spline, spline.selectedPointIndex);
        }

        public static void InsertPointAfter(VFXSimpleSpline spline, int index)
        {
            if (spline == null)
                return;

            int count = spline.GetActivePointCount();
            if (count <= 0)
            {
                AddPointAtEnd(spline);
                return;
            }

            index = Mathf.Clamp(index, 0, count - 1);
            Undo.RecordObject(spline, "Insert Spline Point");
            spline.InsertPoint(index + 1);
            spline.selectedPointIndex = Mathf.Clamp(index + 1, 0, spline.GetActivePointCount() - 1);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        public static void DeletePoint()
        {
            VFXSimpleSpline spline = GetSelectedSpline();
            if (spline != null)
                DeletePoint(spline, spline.selectedPointIndex);
        }

        public static void DeletePoint(VFXSimpleSpline spline, int index)
        {
            if (spline == null || spline.GetActivePointCount() <= 2)
                return;

            index = Mathf.Clamp(index, 0, spline.GetActivePointCount() - 1);
            Undo.RecordObject(spline, "Delete Spline Point");
            spline.RemovePointAt(index);
            spline.selectedPointIndex = Mathf.Clamp(index, 0, spline.GetActivePointCount() - 1);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        public static void FramePoint()
        {
            VFXSimpleSpline spline = GetSelectedSpline();
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && spline != null)
                FramePoint(sceneView, spline, spline.selectedPointIndex);
        }

        public static void FramePoint(SceneView sceneView, VFXSimpleSpline spline, int index)
        {
            if (sceneView == null || spline == null || spline.GetActivePointCount() <= 0)
                return;

            Vector3 world = GetPointWorld(spline, index);
            float size = Mathf.Max(1f, HandleUtility.GetHandleSize(world) * 2f);
            sceneView.Frame(new Bounds(world, Vector3.one * size), false);
        }

        public static bool HandleShortcut(Event e, VFXSimpleSpline spline)
        {
            if (e == null || spline == null || e.type != EventType.KeyDown)
                return false;

            if (!IsPointEditingActive)
                return false;

            if (e.keyCode == KeyCode.F)
            {
                FramePoint();
                e.Use();
                return true;
            }

            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                DeletePoint(spline, spline.selectedPointIndex);
                e.Use();
                return true;
            }

            return false;
        }

    }
}
#endif

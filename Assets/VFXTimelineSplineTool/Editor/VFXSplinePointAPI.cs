#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    public static class VFXSplinePointAPI
    {
        private const string EnabledKey = "VFXTimelineSplineTool.PointOverlay.Enabled";
        private const string OffsetScaleKey = "VFXTimelineSplineTool.PointOverlay.OffsetScale";
        private const string LargerFirstPointKey = "VFXTimelineSplineTool.PointOverlay.LargerFirstPoint";

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
            get { return EditorPrefs.GetFloat(OffsetScaleKey, 0.25f); }
            set
            {
                EditorPrefs.SetFloat(OffsetScaleKey, Mathf.Clamp(value, 0f, 1.5f));
                SceneView.RepaintAll();
            }
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
            spline.AddPoint();
            spline.selectedPointIndex = Mathf.Clamp(spline.GetActivePointCount() - 1, 0, spline.GetActivePointCount() - 1);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
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
    }
}
#endif

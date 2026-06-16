
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// compatibility layer for old windows
namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    public static class VFXSplinePointEditingOverlay
    {
        public static bool Enabled
        {
            get => VFXSplinePointAPI.Enabled;
            set => VFXSplinePointAPI.Enabled = value;
        }

        public static float OffsetScale
        {
            get => VFXSplinePointAPI.OffsetScale;
            set => VFXSplinePointAPI.OffsetScale = value;
        }

        public static bool LargerFirstPoint
        {
            get => VFXSplinePointAPI.LargerFirstPoint;
            set => VFXSplinePointAPI.LargerFirstPoint = value;
        }

        public static void AddPointAtEnd() => VFXSplinePointAPI.AddPointAtEnd();
        public static void AddPointAtEnd(VFXSimpleSpline spline) => VFXSplinePointAPI.AddPointAtEnd(spline);
        public static void InsertPointAfter() => VFXSplinePointAPI.InsertPointAfter();
        public static void InsertPointAfter(VFXSimpleSpline spline, int index) => VFXSplinePointAPI.InsertPointAfter(spline, index);
        public static void DeletePoint() => VFXSplinePointAPI.DeletePoint();
        public static void DeletePoint(VFXSimpleSpline spline, int index) => VFXSplinePointAPI.DeletePoint(spline, index);
        public static void FramePoint() => VFXSplinePointAPI.FramePoint();
        public static void FramePoint(SceneView sceneView, VFXSimpleSpline spline, int index) => VFXSplinePointAPI.FramePoint(sceneView, spline, index);

        public static int GetPointCount(VFXSimpleSpline spline) => VFXSplinePointAPI.GetPointCount(spline);
        public static Vector3 GetPointLocal(VFXSimpleSpline spline, int index) => VFXSplinePointAPI.GetPointLocal(spline, index);
        public static Vector3 GetPointWorld(VFXSimpleSpline spline, int index) => VFXSplinePointAPI.GetPointWorld(spline, index);
        public static void SetPointLocal(VFXSimpleSpline spline, int index, Vector3 localPosition) => VFXSplinePointAPI.SetPointLocal(spline, index, localPosition);
    }
}
#endif

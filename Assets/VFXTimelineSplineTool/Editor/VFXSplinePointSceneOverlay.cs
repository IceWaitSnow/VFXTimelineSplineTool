
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [InitializeOnLoad]
    public static class VFXSplinePointSceneOverlay
    {
        static VFXSplinePointSceneOverlay()
        {
            SceneView.duringSceneGui -= OnGUI;
            SceneView.duringSceneGui += OnGUI;
        }

        [MenuItem("Tools/VFX Timeline Spline/Edit Points Mode")]
        public static void EnterPointMode()
        {
            VFXSplinePointAPI.EnterPointMode();
        }

        [MenuItem("Tools/VFX Timeline Spline/Object Mode")]
        public static void EnterObjectMode()
        {
            VFXSplinePointAPI.EnterObjectMode();
        }

        private static void OnGUI(SceneView view)
        {
            VFXSimpleSpline spline = VFXSplinePointAPI.ActiveSpline;
            if (spline == null) return;

            if (!VFXSplinePointAPI.Enabled) return;

            VFXSplinePointAPI.HandleShortcut(Event.current, spline);

            if (!VFXSplinePointAPI.IsPointMode)
                return;

            if (Selection.activeGameObject == spline.gameObject)
                return;

            VFXSplineSceneDrawer.DrawSpline(spline, true);
            VFXSplineSceneDrawer.DrawEditablePoints(spline);
        }
    }
}
#endif

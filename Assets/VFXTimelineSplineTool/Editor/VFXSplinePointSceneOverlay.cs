
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
#if UNITY_2019_1_OR_NEWER
using UnityEditor.ShortcutManagement;
#endif

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

#if UNITY_2019_1_OR_NEWER
        [Shortcut("VFX Timeline Spline/切换追加点模式", typeof(SceneView), KeyCode.A)]
        private static void ToggleAppendPointModeShortcut(ShortcutArguments args)
        {
            VFXSplineSceneDrawer.ToggleAppendPointModeFromShortcut();
        }

        [Shortcut("VFX Timeline Spline/点菜单或在线段插入点", typeof(SceneView), KeyCode.M)]
        private static void OpenPointMenuOrInsertOnCurveShortcut(ShortcutArguments args)
        {
            VFXSplineSceneDrawer.OpenContextMenuFromShortcut();
        }
#endif

        private static void OnGUI(SceneView view)
        {
            VFXSimpleSpline spline = VFXSplinePointAPI.ActiveSpline;
            if (spline == null) return;

            VFXSplinePointAPI.HandleShortcut(Event.current, spline);

            if (!VFXSplinePointAPI.IsPointEditingActive)
                return;

            if (Selection.activeGameObject == spline.gameObject)
                return;

            VFXSplineSceneDrawer.DrawSpline(spline, true);
            VFXSplineSceneDrawer.DrawEditablePoints(spline);
        }
    }
}
#endif

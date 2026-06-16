
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [InitializeOnLoad]
    public static class VFXSplinePointEditingOverlayV3
    {
        static VFXSplinePointEditingOverlayV3()
        {
            SceneView.duringSceneGui += OnGUI;
        }

        private static void OnGUI(SceneView view)
        {
            if (!VFXSplinePointAPI.Enabled) return;

            var go = Selection.activeGameObject;
            if (go == null) return;

            var spline = go.GetComponent<VFXSimpleSpline>();
            if (spline == null) return;

            int count = VFXSplinePointAPI.GetPointCount(spline);
            for (int i = 0; i < count; i++)
            {
                var w = VFXSplinePointAPI.GetPointWorld(spline, i);

                float size = HandleUtility.GetHandleSize(w) * 0.12f;
                float pick = size * 3f;

                if (Handles.Button(w, Quaternion.identity, size, pick, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(spline, "Select");
                    spline.selectedPointIndex = i;
                }

                if (spline.selectedPointIndex == i)
                {
                    EditorGUI.BeginChangeCheck();
                    var nw = Handles.PositionHandle(w, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        VFXSplinePointAPI.SetPointWorld(spline, i, nw);
                    }
                }
            }
        }
    }
}
#endif

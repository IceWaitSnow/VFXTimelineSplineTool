
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [InitializeOnLoad]
    public static class VFXSplinePointEditingOverlayV3
    {
        private static readonly Color Normal = new Color(1,1,1,0.9f);
        private static readonly Color Selected = new Color(1f,0.85f,0.2f,1f);
        private static readonly Color Start = new Color(0.2f,1f,1f,1f);
        private static readonly Color End = new Color(1f,0.6f,0.2f,1f);

        static VFXSplinePointEditingOverlayV3()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/VFX Spline 3.0.1/Toggle Overlay")]
        private static void Toggle()
        {
            VFXSplineEditorAPI.Enabled = !VFXSplineEditorAPI.Enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!VFXSplineEditorAPI.Enabled) return;

            var splines = VFXSplineEditorAPI.GetSelectedSplines();
            if (splines.Count == 0) return;

            foreach (var s in splines)
            {
                DrawSpline(s);
            }
        }

        private static void DrawSpline(VFXSimpleSpline spline)
        {
            int count = spline.localPoints.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 world = spline.transform.TransformPoint(spline.localPoints[i]);

                float size = HandleUtility.GetHandleSize(world) * 0.12f;
                float pick = size * VFXSplineEditorAPI.PickMultiplier;

                Handles.color = GetColor(i, count, spline.selectedPointIndex == i);

                if (Handles.Button(world, Quaternion.identity, size, pick, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(spline, "Select Point");
                    spline.selectedPointIndex = i;
                    EditorUtility.SetDirty(spline);
                }

                if (spline.selectedPointIndex == i)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newWorld = Handles.PositionHandle(world, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(spline, "Move Point");
                        spline.localPoints[i] = spline.transform.InverseTransformPoint(newWorld);
                        spline.MarkDistanceCacheDirty();
                        EditorUtility.SetDirty(spline);
                    }
                }

                Handles.Label(world + Vector3.up * size * 2f, $"P{i}");
            }
        }

        private static Color GetColor(int i, int count, bool selected)
        {
            if (selected) return Selected;
            if (i == 0) return Start;
            if (i == count - 1) return End;
            return Normal;
        }
    }
}
#endif

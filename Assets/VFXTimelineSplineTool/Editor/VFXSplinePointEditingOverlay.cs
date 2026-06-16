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
        private const string EnabledKey = "VFXSpline3.Enabled";
        private const string PickMultiplierKey = "VFXSpline3.PickMultiplier";

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        private static float PickMultiplier
        {
            get => EditorPrefs.GetFloat(PickMultiplierKey, 2.8f);
            set => EditorPrefs.SetFloat(PickMultiplierKey, Mathf.Clamp(value, 1f, 8f));
        }

        private static readonly Color Normal = new Color(1,1,1,0.9f);
        private static readonly Color Selected = new Color(1f,0.85f,0.2f,1f);
        private static readonly Color Start = new Color(0.2f,1f,1f,1f);
        private static readonly Color End = new Color(1f,0.6f,0.2f,1f);

        static VFXSplinePointEditingOverlayV3()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/VFX Spline 3.0/Toggle Overlay")]
        private static void Toggle()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!Enabled) return;

            var splines = GetSplines();
            if (splines.Count == 0) return;

            Event e = Event.current;

            foreach (var s in splines)
            {
                DrawSpline(s, e);
                HandleKeys(s, view, e);
            }
        }

        private static List<VFXSimpleSpline> GetSplines()
        {
            var result = new List<VFXSimpleSpline>();
            foreach (var go in Selection.gameObjects)
            {
                var s = go.GetComponent<VFXSimpleSpline>();
                if (s != null) result.Add(s);
            }
            return result;
        }

        private static void DrawSpline(VFXSimpleSpline spline, Event e)
        {
            int count = spline.localPoints.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 world = spline.transform.TransformPoint(spline.localPoints[i]);

                float size = HandleUtility.GetHandleSize(world) * 0.12f;
                float pick = size * PickMultiplier;

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

        private static void HandleKeys(VFXSimpleSpline spline, SceneView view, Event e)
        {
            if (e == null || e.type != EventType.KeyDown) return;

            int idx = spline.selectedPointIndex;

            if (e.keyCode == KeyCode.Delete)
            {
                if (idx >= 0 && idx < spline.localPoints.Count)
                {
                    Undo.RecordObject(spline, "Delete Point");
                    spline.localPoints.RemoveAt(idx);
                    spline.selectedPointIndex = Mathf.Clamp(idx - 1, 0, spline.localPoints.Count - 1);
                    spline.MarkDistanceCacheDirty();
                    e.Use();
                }
            }

            if (e.keyCode == KeyCode.F)
            {
                if (idx >= 0 && idx < spline.localPoints.Count)
                {
                    view.Frame(new Bounds(
                        spline.transform.TransformPoint(spline.localPoints[idx]),
                        Vector3.one
                    ), false);
                    e.Use();
                }
            }

            if (e.shift && e.keyCode == KeyCode.A)
            {
                Undo.RecordObject(spline, "Add Point");
                spline.localPoints.Add(spline.localPoints[^1] + Vector3.right);
                spline.MarkDistanceCacheDirty();
                e.Use();
            }
        }
    }
}
#endif

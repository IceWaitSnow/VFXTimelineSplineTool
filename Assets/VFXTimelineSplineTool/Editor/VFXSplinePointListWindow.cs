#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    /// <summary>
    /// Inspector-like point list window.
    /// v2.6.5: gives a reliable fallback when a Scene View handle is hidden by another gizmo.
    /// </summary>
    public class VFXSplinePointListWindow : EditorWindow
    {
        private VFXSimpleSpline spline;
        private Vector2 scroll;
        private bool autoFollowSelection = true;

        [MenuItem("Tools/VFX Timeline Spline/Point List Window")]
        public static void Open()
        {
            VFXSplinePointListWindow window = GetWindow<VFXSplinePointListWindow>("Spline Points");
            window.minSize = new Vector2(360f, 320f);
            window.RefreshFromSelection();
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            RefreshFromSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (autoFollowSelection)
                RefreshFromSelection();
            Repaint();
        }

        private void RefreshFromSelection()
        {
            GameObject go = Selection.activeGameObject;
            if (go != null)
            {
                VFXSimpleSpline selected = go.GetComponent<VFXSimpleSpline>();
                if (selected != null)
                    spline = selected;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("VFX Spline Point List v" + VFXSplineToolVersion.Version, EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                autoFollowSelection = EditorGUILayout.ToggleLeft("Auto Follow Selection", autoFollowSelection, GUILayout.Width(170));
                if (GUILayout.Button("Use Selected", GUILayout.Width(110)))
                    RefreshFromSelection();
            }

            spline = (VFXSimpleSpline)EditorGUILayout.ObjectField("Spline", spline, typeof(VFXSimpleSpline), true);

            EditorGUILayout.Space(4);
            DrawOverlaySettings();

            if (spline == null)
            {
                EditorGUILayout.HelpBox("请选择一个带 VFXSimpleSpline 的物体，或者把 Spline 拖到上方字段。", MessageType.Info);
                return;
            }

            int count = VFXSplinePointEditingOverlay.GetPointCount(spline);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Path Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mode", spline.pathMode.ToString());
            EditorGUILayout.LabelField("Point Count", count.ToString());
            EditorGUILayout.LabelField("Approx Length", spline.ApproxLength.ToString("F3"));

            DrawToolbar(count);
            DrawPointList(count);
        }

        private void DrawOverlaySettings()
        {
            EditorGUILayout.LabelField("Scene Handle Overlay", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Enable Offset Handles", VFXSplinePointEditingOverlay.Enabled);
            float offset = EditorGUILayout.Slider("Handle Offset", VFXSplinePointEditingOverlay.OffsetScale, 0f, 1.5f);
            bool largerFirst = EditorGUILayout.Toggle("Larger First Point", VFXSplinePointEditingOverlay.LargerFirstPoint);
            if (EditorGUI.EndChangeCheck())
            {
                VFXSplinePointEditingOverlay.Enabled = enabled;
                VFXSplinePointEditingOverlay.OffsetScale = offset;
                VFXSplinePointEditingOverlay.LargerFirstPoint = largerFirst;
            }

            EditorGUILayout.HelpBox("Scene 里的可点击点会向相机方向偏移一点，并用细线连回真实顶点。这样 P0 和 Spline 物体坐标轴重合时也能选中。快捷键：F 聚焦当前点，Shift+A 末尾加点，Delete 删除当前点。", MessageType.None);
        }

        private void DrawToolbar(int count)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Point Tools", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add End"))
                    VFXSplinePointEditingOverlay.AddPointAtEnd(spline);

                using (new EditorGUI.DisabledScope(count <= 0))
                {
                    if (GUILayout.Button("Insert After"))
                        VFXSplinePointEditingOverlay.InsertPointAfter(spline, spline.selectedPointIndex);
                    if (GUILayout.Button("Delete Selected"))
                        VFXSplinePointEditingOverlay.DeletePoint(spline, spline.selectedPointIndex);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(count <= 0))
                {
                    if (GUILayout.Button("Focus Selected"))
                        FocusSelectedPoint();
                    if (GUILayout.Button("Rebuild Cache"))
                    {
                        Undo.RecordObject(spline, "Rebuild Distance Cache");
                        spline.RebuildDistanceCache();
                        EditorUtility.SetDirty(spline);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void DrawPointList(int count)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Control Points", EditorStyles.boldLabel);

            if (count <= 0)
            {
                EditorGUILayout.HelpBox("当前路径没有控制点。", MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < count; i++)
            {
                DrawPointRow(i, count);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPointRow(int index, int count)
        {
            bool selected = spline.selectedPointIndex == index;
            GUIStyle boxStyle = new GUIStyle(EditorStyles.helpBox);
            if (selected)
                GUI.backgroundColor = new Color(1f, 0.86f, 0.2f, 0.35f);

            EditorGUILayout.BeginVertical(boxStyle);
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.HorizontalScope())
            {
                string name = index == 0 ? "P0 Start" : (index == count - 1 ? "P" + index + " End" : "P" + index);
                EditorGUILayout.LabelField(name, selected ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(90));

                if (GUILayout.Button("Select", GUILayout.Width(64)))
                    SelectPoint(index);
                if (GUILayout.Button("Focus", GUILayout.Width(58)))
                {
                    SelectPoint(index);
                    FocusSelectedPoint();
                }
                if (GUILayout.Button("Insert", GUILayout.Width(58)))
                    VFXSplinePointEditingOverlay.InsertPointAfter(spline, index);
                using (new EditorGUI.DisabledScope(count <= 2))
                {
                    if (GUILayout.Button("Delete", GUILayout.Width(58)))
                        VFXSplinePointEditingOverlay.DeletePoint(spline, index);
                }
            }

            EditorGUI.BeginChangeCheck();
            Vector3 local = VFXSplinePointEditingOverlay.GetPointLocal(spline, index);
            Vector3 newLocal = EditorGUILayout.Vector3Field("Local", local);
            if (EditorGUI.EndChangeCheck())
                VFXSplinePointEditingOverlay.SetPointLocal(spline, index, newLocal);

            using (new EditorGUI.DisabledScope(true))
            {
                Vector3 world = VFXSplinePointEditingOverlay.GetPointWorld(spline, index);
                EditorGUILayout.Vector3Field("World", world);
            }

            if (spline.enableDynamicStartEndBinding)
            {
                if (index == 0 && spline.dynamicStartTransform != null)
                    EditorGUILayout.HelpBox("此点由 Dynamic Start Transform 驱动。移动点会移动绑定 Transform。", MessageType.None);
                else if (index == count - 1 && spline.dynamicEndTransform != null)
                    EditorGUILayout.HelpBox("此点由 Dynamic End Transform 驱动。移动点会移动绑定 Transform。", MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void SelectPoint(int index)
        {
            if (spline == null)
                return;
            Undo.RecordObject(spline, "Select Spline Point");
            spline.selectedPointIndex = Mathf.Clamp(index, 0, VFXSplinePointEditingOverlay.GetPointCount(spline) - 1);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
            Repaint();
        }

        private void FocusSelectedPoint()
        {
            if (spline == null)
                return;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null && SceneView.sceneViews.Count > 0)
                sceneView = SceneView.sceneViews[0] as SceneView;

            if (sceneView != null)
                VFXSplinePointEditingOverlay.FramePoint(sceneView, spline, spline.selectedPointIndex);
        }
    }
}
#endif

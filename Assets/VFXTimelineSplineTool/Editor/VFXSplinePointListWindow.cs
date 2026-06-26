#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    /// <summary>
    /// 类 Inspector 的控制点列表窗口。
    /// v2.6.5：当 Scene View 里的 Handle 被其他 Gizmo 挡住时，提供一个可靠的备用编辑入口。
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
            EditorGUILayout.LabelField("VFX Spline 控制点列表 v" + VFXSplineToolVersion.Version, EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                autoFollowSelection = EditorGUILayout.ToggleLeft("自动跟随选择", autoFollowSelection, GUILayout.Width(170));
                if (GUILayout.Button("使用当前选择", GUILayout.Width(110)))
                    RefreshFromSelection();
            }

            spline = (VFXSimpleSpline)EditorGUILayout.ObjectField("Spline", spline, typeof(VFXSimpleSpline), true);

            if (spline == null)
            {
                EditorGUILayout.HelpBox("请选择一个带 VFXSimpleSpline 的物体，或者把 Spline 拖到上方字段。", MessageType.Info);
                return;
            }

            int count = VFXSplinePointAPI.GetPointCount(spline);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("路径信息", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("模式", spline.pathMode.ToString());
            EditorGUILayout.LabelField("控制点数量", count.ToString());
            EditorGUILayout.LabelField("近似长度", spline.ApproxLength.ToString("F3"));

            DrawToolbar(count);
            DrawPointList(count);
        }

        private void DrawToolbar(int count)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("控制点工具", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(count <= 0))
                {
                    if (GUILayout.Button("选择 P0"))
                    {
                        VFXSplinePointAPI.SelectPoint(spline, 0);
                        Repaint();
                    }
                }

                if (GUILayout.Button("末尾加点"))
                    VFXSplinePointAPI.AddPointAtEnd(spline);

                using (new EditorGUI.DisabledScope(count <= 0))
                {
                    if (GUILayout.Button("在后方插入"))
                        VFXSplinePointAPI.InsertPointAfter(spline, spline.selectedPointIndex);
                    if (GUILayout.Button("删除选中点"))
                        VFXSplinePointAPI.DeletePoint(spline, spline.selectedPointIndex);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(count <= 0))
                {
                    if (GUILayout.Button("聚焦选中点"))
                        FocusSelectedPoint();
                    if (GUILayout.Button("重建缓存"))
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
            EditorGUILayout.LabelField("控制点", EditorStyles.boldLabel);

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

                if (GUILayout.Button("选择", GUILayout.Width(64)))
                    SelectPoint(index);
                if (GUILayout.Button("聚焦", GUILayout.Width(58)))
                {
                    SelectPoint(index);
                    FocusSelectedPoint();
                }
                if (GUILayout.Button("插入", GUILayout.Width(58)))
                    VFXSplinePointAPI.InsertPointAfter(spline, index);
                using (new EditorGUI.DisabledScope(count <= 2))
                {
                    if (GUILayout.Button("删除", GUILayout.Width(58)))
                        VFXSplinePointAPI.DeletePoint(spline, index);
                }
            }

            EditorGUI.BeginChangeCheck();
            Vector3 local = VFXSplinePointAPI.GetPointLocal(spline, index);
            Vector3 newLocal = EditorGUILayout.Vector3Field("Local", local);
            if (EditorGUI.EndChangeCheck())
                VFXSplinePointAPI.SetPointLocal(spline, index, newLocal);

            using (new EditorGUI.DisabledScope(true))
            {
                Vector3 world = VFXSplinePointAPI.GetPointWorld(spline, index);
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
            spline.selectedPointIndex = Mathf.Clamp(index, 0, VFXSplinePointAPI.GetPointCount(spline) - 1);
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
                VFXSplinePointAPI.FramePoint(sceneView, spline, spline.selectedPointIndex);
        }
    }
}
#endif

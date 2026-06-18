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

            EditorGUILayout.Space(4);
            DrawOverlaySettings();

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

        private void DrawOverlaySettings()
        {
            EditorGUILayout.LabelField("Scene 控制点覆盖层", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            VFXSplinePointEditMode mode = VFXSimpleSplineEditor.DrawPointEditModePopup(VFXSplinePointAPI.EditMode);
            bool enabled = EditorGUILayout.Toggle(new GUIContent("启用控制点 Handle", "开启后可以在 Scene 视图中选择和移动 Spline 控制点。"), VFXSplinePointAPI.Enabled);
            float pickSize = EditorGUILayout.Slider(new GUIContent("拾取尺寸倍数", "控制点的可点击区域放大倍数。点和 Transform Gizmo 重叠时，可以调大这个值。"), VFXSplinePointAPI.PickSizeMultiplier, 1f, 8f);
            bool largerFirst = EditorGUILayout.Toggle(new GUIContent("放大第一个点", "让第一个控制点更容易被看到和点中。"), VFXSplinePointAPI.LargerFirstPoint);
            if (EditorGUI.EndChangeCheck())
            {
                if (mode == VFXSplinePointEditMode.Points)
                    VFXSplinePointAPI.EnterPointMode(spline);
                else
                    VFXSplinePointAPI.EnterObjectMode();
                VFXSplinePointAPI.Enabled = enabled;
                VFXSplinePointAPI.PickSizeMultiplier = pickSize;
                VFXSplinePointAPI.LargerFirstPoint = largerFirst;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("编辑控制点"))
                    VFXSplinePointAPI.EnterPointMode(spline);
                if (GUILayout.Button("移动 Spline 物体"))
                    VFXSplinePointAPI.EnterObjectMode();
            }

            string toggleKey = VFXSplinePointAPI.GetShortcutLabel(VFXSplinePointAPI.TogglePointModeShortcut);
            string appendKey = VFXSplinePointAPI.GetShortcutLabel(VFXSplinePointAPI.AppendModeShortcut);
            string menuKey = VFXSplinePointAPI.GetShortcutLabel(VFXSplinePointAPI.ContextMenuShortcut);
            EditorGUILayout.HelpBox("物体模式显示 Unity Transform Gizmo，用来移动整条 Spline。点编辑模式会锁定当前 Spline，即使点击 Scene 空白处也继续编辑。快捷键：" + toggleKey + " 切换点编辑，" + appendKey + " 追加点模式，" + menuKey + " 打开菜单，Esc 返回物体模式，F 聚焦当前点，Delete 删除当前点。", MessageType.None);
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

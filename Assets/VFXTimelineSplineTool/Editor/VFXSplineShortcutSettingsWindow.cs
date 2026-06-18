#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    public class VFXSplineShortcutSettingsWindow : EditorWindow
    {
        [MenuItem("Tools/VFX Timeline Spline/Shortcut Settings")]
        public static void Open()
        {
            VFXSplineShortcutSettingsWindow window = GetWindow<VFXSplineShortcutSettingsWindow>("Spline Shortcuts");
            window.minSize = new Vector2(360f, 190f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VFX Timeline Spline 快捷键", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这里配置的是此工具在 Scene View 中监听的单键快捷键。建议避开 Unity 常用视角键和全局工具键。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            KeyCode toggleKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("切换点编辑模式", "默认 P。Object Mode / Point Mode 来回切换。"), VFXSplinePointAPI.TogglePointModeShortcut);
            KeyCode appendKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("追加点模式", "默认 A。进入后左键连续追加点。"), VFXSplinePointAPI.AppendModeShortcut);
            KeyCode menuKey = (KeyCode)EditorGUILayout.EnumPopup(new GUIContent("打开点/曲线菜单", "默认 M。鼠标靠近点或曲线时弹出上下文菜单。"), VFXSplinePointAPI.ContextMenuShortcut);

            if (EditorGUI.EndChangeCheck())
            {
                VFXSplinePointAPI.TogglePointModeShortcut = toggleKey;
                VFXSplinePointAPI.AppendModeShortcut = appendKey;
                VFXSplinePointAPI.ContextMenuShortcut = menuKey;
            }

            DrawConflictWarning(toggleKey, appendKey, menuKey);

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("恢复默认 P / A / M"))
                VFXSplinePointAPI.ResetShortcutSettings();
            if (GUILayout.Button("刷新 Scene View"))
                SceneView.RepaintAll();
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawConflictWarning(KeyCode toggleKey, KeyCode appendKey, KeyCode menuKey)
        {
            if (toggleKey == appendKey || toggleKey == menuKey || appendKey == menuKey)
                EditorGUILayout.HelpBox("有快捷键重复。重复时排在事件流前面的功能会先响应，建议改成不同按键。", MessageType.Warning);
        }
    }
}
#endif

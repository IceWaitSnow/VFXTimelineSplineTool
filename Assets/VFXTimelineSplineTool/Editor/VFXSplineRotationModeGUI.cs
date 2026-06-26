#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    internal static class VFXSplineRotationModeGUI
    {
        private static readonly GUIContent[] ModeLabels =
        {
            new GUIContent("不旋转 (None)", "不自动改旋转，只沿路径更新位置。"),
            new GUIContent("3D 跟随路径 (Full 3D)", "用路径切线和 Normal 生成完整 3D 旋转，适合真正有上下起伏的 3D 路径。"),
            new GUIContent("卡牌/平面稳定 (Planar Stable)", "推荐卡牌/面片使用。用路径 Normal 稳定牌面，并避免切线反向时翻面。")
        };

        private static readonly VFXSplineRotationMode[] ModeValues =
        {
            VFXSplineRotationMode.None,
            VFXSplineRotationMode.Full3D,
            VFXSplineRotationMode.PlanarStable
        };

        public static void Draw(SerializedObject serializedObject, string label)
        {
            SerializedProperty property = serializedObject.FindProperty("rotationMode");
            if (property == null)
                return;

            VFXSplineRotationMode mode = (VFXSplineRotationMode)property.enumValueIndex;
            if (mode == VFXSplineRotationMode.YawOnly || mode == VFXSplineRotationMode.PlanarForward)
            {
                property.enumValueIndex = (int)VFXSplineRotationMode.PlanarStable;
                mode = VFXSplineRotationMode.PlanarStable;
            }

            int index = GetDisplayIndex(mode);

            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup(new GUIContent(label, "控制物体如何根据路径切线和 Normal 自动旋转。"), index, ModeLabels);
            if (EditorGUI.EndChangeCheck())
                property.enumValueIndex = (int)ModeValues[Mathf.Clamp(index, 0, ModeValues.Length - 1)];

            DrawHint((VFXSplineRotationMode)property.enumValueIndex);
        }

        private static int GetDisplayIndex(VFXSplineRotationMode mode)
        {
            for (int i = 0; i < ModeValues.Length; i++)
            {
                if (ModeValues[i] == mode)
                    return i;
            }

            if (mode == VFXSplineRotationMode.YawOnly || mode == VFXSplineRotationMode.PlanarForward)
                return 2;

            return 0;
        }

        private static void DrawHint(VFXSplineRotationMode mode)
        {
            if (mode == VFXSplineRotationMode.PlanarStable)
                EditorGUILayout.HelpBox("推荐卡牌/面片使用：位置沿路径走，旋转使用路径 Normal 稳定牌面，并避免切线反向时翻面。", MessageType.None);
            else if (mode == VFXSplineRotationMode.Full3D)
                EditorGUILayout.HelpBox("3D 跟随路径：用于路径有明显上下起伏，需要完整 3D 朝向的情况。普通平面卡牌通常不需要。", MessageType.None);
        }
    }
}
#endif

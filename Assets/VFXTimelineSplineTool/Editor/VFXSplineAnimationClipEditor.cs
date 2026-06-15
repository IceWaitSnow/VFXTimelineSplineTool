#if UNITY_EDITOR
using UnityEditor;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [CustomEditor(typeof(VFXSplineAnimationClip))]
    public class VFXSplineAnimationClipEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("VFX Spline Animation Clip - 快速模式", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这是 Timeline 自定义 Track 的快速模式。默认情况下 Clip 长度控制从 Start Progress 到 End Progress 的时间；如果开启 Loop Playback，则单圈速度由 Seconds Per Loop 控制，Clip 可以任意拉长用于循环。Spline 字段请直接从 Hierarchy 拖入。", MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spline"), new UnityEngine.GUIContent("Spline"));
            SerializedProperty t = serializedObject.FindProperty("template");
            EditorGUILayout.PropertyField(t.FindPropertyRelative("startProgress"), new UnityEngine.GUIContent("Start Progress"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("endProgress"), new UnityEngine.GUIContent("End Progress"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("useSpeedCurve"), new UnityEngine.GUIContent("Use Speed Curve"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("speedCurve"), new UnityEngine.GUIContent("Speed Curve"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(t.FindPropertyRelative("reverse"), new UnityEngine.GUIContent("Reverse"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("loopPlayback"), new UnityEngine.GUIContent("Loop Playback"));
            SerializedProperty loopPlayback = t.FindPropertyRelative("loopPlayback");
            if (loopPlayback != null && loopPlayback.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(t.FindPropertyRelative("secondsPerLoop"), new UnityEngine.GUIContent("Seconds Per Loop"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(t.FindPropertyRelative("useDistanceBasedProgress"), new UnityEngine.GUIContent("Use Distance Based Progress"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("positionOffset"), new UnityEngine.GUIContent("Position Offset"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(t.FindPropertyRelative("followRotation"), new UnityEngine.GUIContent("Follow Rotation"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("rotationMode"), new UnityEngine.GUIContent("Rotation Mode"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("forwardAxis"), new UnityEngine.GUIContent("Forward Axis"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("rotationOffsetEuler"), new UnityEngine.GUIContent("Rotation Offset Euler"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

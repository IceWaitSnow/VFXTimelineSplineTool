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
            EditorGUILayout.LabelField("VFX Spline Animation Clip 快速模式", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这是 Timeline 自定义 Track 的快速模式。默认情况下 Clip 长度控制从 Start Progress 到 End Progress 的时间；如果开启 Loop Playback，则单圈速度由 Seconds Per Loop 控制，Clip 可以任意拉长用于循环。Spline 字段请直接从 Hierarchy 拖入。", MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spline"), new UnityEngine.GUIContent("Spline 路径", "Timeline Clip 使用的 Spline 路径。请从 Hierarchy 拖入场景中的 VFXSimpleSpline。"));
            SerializedProperty t = serializedObject.FindProperty("template");
            EditorGUILayout.PropertyField(t.FindPropertyRelative("startProgress"), new UnityEngine.GUIContent("起始 Progress", "Clip 开始时的路径进度。0 为起点，1 为终点。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("endProgress"), new UnityEngine.GUIContent("结束 Progress", "Clip 结束时的路径进度。0 为起点，1 为终点。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("useSpeedCurve"), new UnityEngine.GUIContent("使用速度曲线", "开启后用下方曲线重新映射 Clip 内部时间，可制作缓入缓出。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("speedCurve"), new UnityEngine.GUIContent("速度曲线", "X 为 Clip 内部时间比例，Y 为 Start Progress 到 End Progress 之间的插值比例。"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(t.FindPropertyRelative("reverse"), new UnityEngine.GUIContent("反向", "反向播放路径进度。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("loopPlayback"), new UnityEngine.GUIContent("循环播放", "开启后 Clip 可以拉长为多轮循环，单圈速度由 Seconds Per Loop 控制。"));
            SerializedProperty loopPlayback = t.FindPropertyRelative("loopPlayback");
            if (loopPlayback != null && loopPlayback.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(t.FindPropertyRelative("secondsPerLoop"), new UnityEngine.GUIContent("每圈秒数", "Loop Playback 开启时，一圈路径需要的秒数。"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(t.FindPropertyRelative("useDistanceBasedProgress"), new UnityEngine.GUIContent("使用距离等速 Progress", "开启后按路径实际距离计算 Progress，让运动速度更均匀。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("positionOffset"), new UnityEngine.GUIContent("位置偏移", "沿路径计算位置后额外叠加的世界坐标偏移。"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(t.FindPropertyRelative("followRotation"), new UnityEngine.GUIContent("跟随旋转", "开启后物体会根据路径切线旋转。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("rotationMode"), new UnityEngine.GUIContent("旋转模式", "控制如何根据路径切线计算旋转。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("forwardAxis"), new UnityEngine.GUIContent("前向轴", "指定模型哪根本地轴作为前进方向。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("rotationOffsetEuler"), new UnityEngine.GUIContent("旋转偏移 Euler", "在路径方向旋转之后额外叠加的 Euler 角偏移。"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

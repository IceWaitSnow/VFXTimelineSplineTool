#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [CustomEditor(typeof(VFXSplineAnchor))]
    public class VFXSplineAnchorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VFXSplineAnchor anchor = (VFXSplineAnchor)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("VFX Spline Anchor - 路径特效挂点 v2.6.2", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把这个物体固定到 Spline 的某个 Progress 位置。粒子、面片、爆点可以作为它的子物体，再用 Timeline 原生 Control Track / Activation Track 控制播放。Follow Animator Progress 模式可以跟随运动物体的路径进度，并用 Progress Offset 做前后错位。", MessageType.Info);

            serializedObject.Update();

            EditorGUILayout.LabelField("Spline", EditorStyles.boldLabel);
            DrawProperty("spline", "Spline");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Anchor Mode", EditorStyles.boldLabel);
            DrawProperty("anchorMode", "Anchor Mode");

            VFXSplineAnchorMode mode = (VFXSplineAnchorMode)serializedObject.FindProperty("anchorMode").enumValueIndex;
            if (mode == VFXSplineAnchorMode.FixedProgress)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Fixed Progress", EditorStyles.boldLabel);
                DrawProperty("progress", "Progress");
            }
            else
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Follow Animator Progress", EditorStyles.boldLabel);
                DrawProperty("sourceAnimator", "Source Animator");
                DrawProperty("autoUseSourceSpline", "Auto Use Source Spline");
                DrawProperty("progressOffset", "Progress Offset");
                DrawProperty("progressWrapMode", "Progress Wrap Mode");
                EditorGUILayout.HelpBox("最终进度 = Source Animator 当前进度 + Progress Offset。\n例如：Offset = -0.05 表示落后运动物体 5%；Offset = 0.10 表示提前 10%。", MessageType.None);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Progress Evaluation", EditorStyles.boldLabel);
            DrawProperty("useDistanceBasedProgress", "Use Distance Based Progress");
            DrawProperty("reverse", "Reverse");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Position", EditorStyles.boldLabel);
            DrawProperty("followPosition", "Follow Position");
            DrawProperty("positionOffset", "Position Offset");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Rotation", EditorStyles.boldLabel);
            DrawProperty("rotationMode", "Rotation Mode");
            DrawProperty("forwardAxis", "Forward Axis");
            DrawProperty("rotationOffsetEuler", "Rotation Offset Euler");
            DrawProperty("fallbackForward", "Fallback Forward");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);
            DrawProperty("previewInEditMode", "Preview In Edit Mode");
            DrawProperty("applyOnValidate", "Apply On Validate");
            DrawProperty("showSceneLabel", "Show Scene Label");
            DrawProperty("label", "Label");
            DrawProperty("labelColor", "Label Color");
            DrawProperty("gizmoSize", "Gizmo Size");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Current Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Spline", anchor.GetActiveSpline() != null ? anchor.GetActiveSpline().name : "None");
            EditorGUILayout.LabelField("Raw Progress", anchor.GetRawAnchorProgress().ToString("F3"));
            EditorGUILayout.LabelField("Effective Progress", anchor.GetEffectiveProgress().ToString("F3"));
            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && anchor.sourceAnimator != null)
                EditorGUILayout.LabelField("Source Progress", anchor.GetSourceEvaluatedProgress().ToString("F3"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progress 快捷设置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set 0%")) SetEffectiveProgress(anchor, 0f);
                if (GUILayout.Button("Set 25%")) SetEffectiveProgress(anchor, 0.25f);
                if (GUILayout.Button("Set 50%")) SetEffectiveProgress(anchor, 0.5f);
                if (GUILayout.Button("Set 75%")) SetEffectiveProgress(anchor, 0.75f);
                if (GUILayout.Button("Set 100%")) SetEffectiveProgress(anchor, 1f);
            }

            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Offset -10%")) SetOffset(anchor, -0.10f);
                    if (GUILayout.Button("Offset -5%")) SetOffset(anchor, -0.05f);
                    if (GUILayout.Button("Offset 0")) SetOffset(anchor, 0f);
                    if (GUILayout.Button("Offset +5%")) SetOffset(anchor, 0.05f);
                    if (GUILayout.Button("Offset +10%")) SetOffset(anchor, 0.10f);
                }
            }

            if (GUILayout.Button("Apply Current Progress"))
            {
                Undo.RecordObject(anchor.transform, "Apply Spline Anchor");
                anchor.ApplyAnchor();
                EditorUtility.SetDirty(anchor);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Ping Spline"))
            {
                VFXSimpleSpline activeSpline = anchor.GetActiveSpline();
                if (activeSpline != null)
                {
                    Selection.activeGameObject = activeSpline.gameObject;
                    EditorGUIUtility.PingObject(activeSpline.gameObject);
                }
            }
        }

        private void DrawProperty(string name, string label)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
        }

        private static void SetEffectiveProgress(VFXSplineAnchor anchor, float value)
        {
            Undo.RecordObject(anchor, "Set Anchor Progress");
            Undo.RecordObject(anchor.transform, "Apply Anchor Progress");
            anchor.SetEffectiveProgress(value);
            EditorUtility.SetDirty(anchor);
            SceneView.RepaintAll();
        }

        private static void SetOffset(VFXSplineAnchor anchor, float value)
        {
            Undo.RecordObject(anchor, "Set Anchor Offset");
            Undo.RecordObject(anchor.transform, "Apply Anchor Offset");
            anchor.progressOffset = value;
            anchor.ApplyAnchor();
            EditorUtility.SetDirty(anchor);
            SceneView.RepaintAll();
        }

        private void OnSceneGUI()
        {
            VFXSplineAnchor anchor = (VFXSplineAnchor)target;
            // 多选 Anchor 时，只让当前 Active 对象显示可拖动坐标轴，避免 Scene 视图里满屏 Handle。
            bool editable = Selection.activeGameObject == anchor.gameObject;
            DrawAnchorHandle(anchor, editable);
        }

        public static void DrawAnchorHandle(VFXSplineAnchor anchor, bool editable)
        {
            if (anchor == null) return;
            VFXSimpleSpline activeSpline = anchor.GetActiveSpline();
            if (activeSpline == null) return;

            float p = anchor.GetEffectiveProgress();
            Vector3 pos = activeSpline.GetPoint(p, anchor.useDistanceBasedProgress);
            Vector3 tangent = activeSpline.GetTangent(p, anchor.useDistanceBasedProgress);
            float size = HandleUtility.GetHandleSize(pos) * anchor.gizmoSize;

            Handles.color = anchor.labelColor;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
            if (tangent.sqrMagnitude > 0.000001f)
                Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(tangent, Vector3.up), size * 2.4f, EventType.Repaint);

            if (anchor.showSceneLabel)
            {
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = anchor.labelColor;
                string label = string.IsNullOrEmpty(anchor.label) ? anchor.name : anchor.label;
                string suffix = Mathf.RoundToInt(p * 100f) + "%";
                if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress)
                    suffix += "  Offset " + (anchor.progressOffset >= 0f ? "+" : "") + Mathf.RoundToInt(anchor.progressOffset * 100f) + "%";
                Handles.Label(pos + Vector3.up * size * 2.2f, label + "  " + suffix, style);
            }

            if (!editable) return;

            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.PositionHandle(pos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(anchor, "Move Spline Anchor");
                Undo.RecordObject(anchor.transform, "Apply Spline Anchor");
                float nearest = FindNearestProgressOnSpline(activeSpline, newWorld, anchor.useDistanceBasedProgress);
                anchor.SetEffectiveProgress(nearest);
                EditorUtility.SetDirty(anchor);
            }
        }

        private static float FindNearestProgressOnSpline(VFXSimpleSpline spline, Vector3 world, bool distanceBased)
        {
            int samples = Mathf.Max(64, spline.distanceSampleResolution);
            float bestP = 0f;
            float bestD = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float p = i / (float)samples;
                Vector3 pos = spline.GetPoint(p, distanceBased);
                float d = (pos - world).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    bestP = p;
                }
            }
            return bestP;
        }
    }

    [InitializeOnLoad]
    public static class VFXSplineAnchorSceneDrawer
    {
        static VFXSplineAnchorSceneDrawer()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private static void DuringSceneGUI(SceneView view)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            VFXSplineAnchor[] anchors = Object.FindObjectsOfType<VFXSplineAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                VFXSplineAnchor anchor = anchors[i];
                if (anchor == null || anchor.GetActiveSpline() == null || !anchor.showSceneLabel) continue;
                if (Selection.activeGameObject == anchor.gameObject) continue;
                VFXSplineAnchorEditor.DrawAnchorHandle(anchor, false);
            }
        }
    }
}
#endif

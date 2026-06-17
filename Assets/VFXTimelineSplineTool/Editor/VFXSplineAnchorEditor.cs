#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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
            EditorGUILayout.LabelField("VFX Spline Anchor - 路径特效挂点 v" + VFXSplineToolVersion.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把这个物体固定到 Spline 的某个 Progress 位置。粒子、面片、爆点可以作为它的子物体，再用 Timeline 原生 Control Track / Activation Track 控制播放。Follow Animator Progress 模式可以跟随运动物体的路径进度，并用 Progress Offset 做前后错位。", MessageType.Info);

            serializedObject.Update();

            EditorGUILayout.LabelField("Spline 路径", EditorStyles.boldLabel);
            DrawProperty("spline", "Spline 路径");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Anchor 模式", EditorStyles.boldLabel);
            DrawProperty("anchorMode", "Anchor 模式");

            VFXSplineAnchorMode mode = (VFXSplineAnchorMode)serializedObject.FindProperty("anchorMode").enumValueIndex;
            if (mode == VFXSplineAnchorMode.FixedProgress)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("固定 Progress", EditorStyles.boldLabel);
                DrawProperty("progress", "Progress 进度");
            }
            else
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("跟随 Animator Progress", EditorStyles.boldLabel);
                DrawProperty("sourceAnimator", "Source Animator");
                DrawProperty("autoUseSourceSpline", "自动使用 Source Spline");
                DrawProperty("progressOffset", "Progress 偏移");
                DrawProperty("progressWrapMode", "Progress 循环模式");
                EditorGUILayout.HelpBox("最终进度 = Source Animator 当前进度 + Progress Offset。\n例如：Offset = -0.05 表示落后运动物体 5%；Offset = 0.10 表示提前 10%。", MessageType.None);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Progress 计算", EditorStyles.boldLabel);
            DrawProperty("useDistanceBasedProgress", "使用距离等速 Progress");
            DrawProperty("reverse", "反向");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("位置", EditorStyles.boldLabel);
            DrawProperty("followPosition", "跟随位置");
            DrawProperty("positionOffset", "位置偏移");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("旋转", EditorStyles.boldLabel);
            DrawProperty("rotationMode", "旋转模式");
            DrawProperty("forwardAxis", "前向轴");
            DrawProperty("rotationOffsetEuler", "旋转偏移 Euler");
            DrawProperty("fallbackForward", "备用前向");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("编辑器预览", EditorStyles.boldLabel);
            DrawProperty("previewInEditMode", "编辑模式预览");
            DrawProperty("applyOnValidate", "参数变化时自动应用");
            DrawProperty("showSceneLabel", "显示 Scene 标签");
            DrawProperty("label", "标签文本");
            DrawProperty("labelColor", "标签颜色");
            DrawProperty("gizmoSize", "Gizmo 大小");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("当前信息", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("当前 Spline", anchor.GetActiveSpline() != null ? anchor.GetActiveSpline().name : "无");
            EditorGUILayout.LabelField("原始 Progress", anchor.GetRawAnchorProgress().ToString("F3"));
            EditorGUILayout.LabelField("最终 Progress", anchor.GetEffectiveProgress().ToString("F3"));
            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && anchor.sourceAnimator != null)
                EditorGUILayout.LabelField("Source Progress", anchor.GetSourceEvaluatedProgress().ToString("F3"));

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progress 快捷设置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("设为 0%")) SetEffectiveProgress(anchor, 0f);
                if (GUILayout.Button("设为 25%")) SetEffectiveProgress(anchor, 0.25f);
                if (GUILayout.Button("设为 50%")) SetEffectiveProgress(anchor, 0.5f);
                if (GUILayout.Button("设为 75%")) SetEffectiveProgress(anchor, 0.75f);
                if (GUILayout.Button("设为 100%")) SetEffectiveProgress(anchor, 1f);
            }

            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("偏移 -10%")) SetOffset(anchor, -0.10f);
                    if (GUILayout.Button("偏移 -5%")) SetOffset(anchor, -0.05f);
                    if (GUILayout.Button("偏移 0")) SetOffset(anchor, 0f);
                    if (GUILayout.Button("偏移 +5%")) SetOffset(anchor, 0.05f);
                    if (GUILayout.Button("偏移 +10%")) SetOffset(anchor, 0.10f);
                }
            }

            if (GUILayout.Button("应用当前 Progress"))
            {
                Undo.RecordObject(anchor.transform, "Apply Spline Anchor");
                anchor.ApplyAnchor();
                EditorUtility.SetDirty(anchor);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("定位 Spline"))
            {
                VFXSimpleSpline activeSpline = anchor.GetActiveSpline();
                if (activeSpline != null)
                {
                    Selection.activeGameObject = activeSpline.gameObject;
                    EditorGUIUtility.PingObject(activeSpline.gameObject);
                }
            }

            DrawBakeTools(anchor);
        }

        private void DrawProperty(string name, string label)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, BuildContent(label, name, p));
        }

        private static GUIContent BuildContent(string label, string propertyName, SerializedProperty property)
        {
            string tooltip = !string.IsNullOrEmpty(property.tooltip) ? property.tooltip : GetAnchorPropertyTooltip(propertyName);
            return new GUIContent(label, tooltip);
        }

        private static string GetAnchorPropertyTooltip(string propertyName)
        {
            switch (propertyName)
            {
                case "spline": return "Anchor 所依附的 Spline 路径。";
                case "anchorMode": return "Fixed Progress 使用固定路径进度；Follow Animator Progress 会跟随 Source Animator 的进度。";
                case "progress": return "Anchor 在路径上的固定进度，0 为起点，1 为终点。";
                case "sourceAnimator": return "要跟随的 VFXSplineAnimator。Anchor 会读取它的 Progress。";
                case "autoUseSourceSpline": return "开启后，如果本 Anchor 没有直接指定 Spline，会自动使用 Source Animator 上的 Spline。";
                case "progressOffset": return "在 Source Animator Progress 基础上额外加减的偏移。负数表示落后，正数表示提前。";
                case "progressWrapMode": return "Progress 超出 0-1 后的处理方式：Clamp 限制、Loop 循环、PingPong 往返。";
                case "useDistanceBasedProgress": return "开启后按路径实际距离计算 Progress，让挂点沿路径更均匀分布。";
                case "reverse": return "反向读取 Progress，让 0 对应终点、1 对应起点。";
                case "followPosition": return "开启后 Anchor 的位置会跟随 Spline 上的点。";
                case "positionOffset": return "在路径点位置基础上额外叠加的世界坐标偏移。";
                case "rotationMode": return "是否根据路径切线自动旋转 Anchor。";
                case "forwardAxis": return "指定 Anchor 哪根本地轴作为前进方向。";
                case "rotationOffsetEuler": return "在路径方向旋转之后额外叠加的 Euler 角偏移。";
                case "fallbackForward": return "路径切线过短时使用的备用前向方向。";
                case "previewInEditMode": return "编辑模式下实时预览 Anchor 在路径上的位置。";
                case "applyOnValidate": return "Inspector 参数变化时立即应用 Anchor。";
                case "showSceneLabel": return "在 Scene 视图中显示 Anchor 名称和进度标签。";
                case "label": return "Scene 标签显示的文本。为空时使用物体名。";
                case "labelColor": return "Scene 标签、点和方向箭头的颜色。";
                case "gizmoSize": return "Anchor 在 Scene 视图中的点和方向箭头大小。";
                default: return "";
            }
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

        private void DrawBakeTools(VFXSplineAnchor anchor)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("烘焙 Anchor 为 AnimationClip", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把当前 Anchor 的路径挂点结果烘焙成 Unity 原生 Transform AnimationClip。Follow Animator Progress 模式会优先按 Source Animator 的 Bake Progress Source 采样进度。", MessageType.Info);

            serializedObject.Update();
            DrawProperty("bakeFrameRate", "帧率 Frame Rate");
            DrawProperty("bakeDuration", "时长 Duration");
            DrawBakeDurationShortcuts(anchor);
            DrawProperty("bakePosition", "烘焙位置");
            DrawProperty("bakeRotation", "烘焙旋转");
            DrawProperty("bakeChildren", "烘焙子物体");
            if (serializedObject.FindProperty("bakeChildren").boolValue)
            {
                DrawProperty("bakeChildScale", "烘焙子物体缩放");
                DrawProperty("bakeChildAnimationClip", "子物体 Animation Clip");
                if (serializedObject.FindProperty("bakeChildAnimationClip").objectReferenceValue != null)
                {
                    DrawProperty("bakeChildAnimationUseNormalizedTime", "使用归一化子动画时间");
                    if (!serializedObject.FindProperty("bakeChildAnimationUseNormalizedTime").boolValue)
                        DrawProperty("bakeChildAnimationLoop", "循环子动画");
                }
                else
                {
                    DrawProperty("bakeAutoSampleChildAnimations", "自动采样子物体动画");
                    if (serializedObject.FindProperty("bakeAutoSampleChildAnimations").boolValue)
                    {
                        DrawProperty("bakeChildAnimationUseNormalizedTime", "使用归一化子动画时间");
                        if (!serializedObject.FindProperty("bakeChildAnimationUseNormalizedTime").boolValue)
                            DrawProperty("bakeChildAnimationLoop", "循环子动画");
                    }
                }
            }
            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress)
                DrawProperty("bakeUseSourceAnimatorProgress", "使用 Source Animator 的 Progress 来源");
            DrawProperty("bakeSaveFolder", "保存目录");
            DrawProperty("bakeClipName", "Clip 名称");
            DrawProperty("bakeAddAnimatorIfMissing", "缺少 Animator 时自动添加");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("烘焙关键帧简化", EditorStyles.boldLabel);
            DrawProperty("bakeKeyframeStep", "关键帧间隔 Keyframe Step");
            DrawProperty("bakeAlwaysKeyStartAndEnd", "强制记录首尾帧");
            DrawProperty("bakeOptimizeCurves", "优化曲线");
            if (serializedObject.FindProperty("bakeOptimizeCurves").boolValue)
            {
                DrawProperty("bakePositionTolerance", "位置误差阈值");
                DrawProperty("bakeRotationTolerance", "旋转误差阈值");
            }
            serializedObject.ApplyModifiedProperties();

            string preflightMessage;
            MessageType preflightType;
            bool canBake = GetAnchorBakePreflight(anchor, out preflightMessage, out preflightType);
            EditorGUILayout.HelpBox(preflightMessage, preflightType);

            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("烘焙 Anchor 为 AnimationClip"))
                    BakeAnchorToAnimationClip(anchor);
            }
        }

        private void DrawBakeDurationShortcuts(VFXSplineAnchor anchor)
        {
            if (anchor == null)
                return;

            SerializedProperty durationProp = serializedObject.FindProperty("bakeDuration");
            if (durationProp == null)
                return;

            bool hasSourceDuration = anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && anchor.sourceAnimator != null;
            bool hasChildClipDuration = anchor.bakeChildren && anchor.bakeChildAnimationClip != null;
            if (!hasSourceDuration && !hasChildClipDuration)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasSourceDuration))
                {
                    if (GUILayout.Button("使用 Source Animator 时长"))
                        durationProp.floatValue = Mathf.Max(0.01f, anchor.sourceAnimator.bakeDuration);
                }

                using (new EditorGUI.DisabledScope(!hasChildClipDuration))
                {
                    if (GUILayout.Button("使用子 Clip 时长"))
                        durationProp.floatValue = Mathf.Max(0.01f, anchor.bakeChildAnimationClip.length);
                }
            }
        }

        private struct AnchorTimelineProgressClip
        {
            public TimelineClip timelineClip;
            public AnimationClip clip;
            public double timelineStart;
            public double timelineEnd;
            public double clipIn;
            public bool isInfiniteClip;
        }

        private struct AnchorTimelineProgressSource
        {
            public List<AnchorTimelineProgressClip> clips;
            public double timelineStart;
            public double timelineEnd;
            public double timelineDuration;
            public bool isInfiniteClip;
        }

        private struct AnchorBakeSample
        {
            public float time;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        private struct TransformSnapshot
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        private struct AnchorStateSnapshot
        {
            public string componentJson;
            public Dictionary<Transform, TransformSnapshot> hierarchyTransforms;
            public VFXSimpleSpline spline;
            public VFXSplineAnimator sourceAnimator;
            public AnimationClip bakeChildAnimationClip;
        }

        private static void BakeAnchorToAnimationClip(VFXSplineAnchor anchor)
        {
            VFXSimpleSpline activeSpline = anchor != null ? anchor.GetActiveSpline() : null;
            if (anchor == null || activeSpline == null)
            {
                EditorUtility.DisplayDialog("烘焙 Anchor 为 AnimationClip", "请先指定有效的 Spline。", "确定");
                return;
            }

            int frameRate = Mathf.Clamp(anchor.bakeFrameRate, 1, 240);
            float duration = Mathf.Max(0.01f, anchor.bakeDuration);

            AnchorTimelineProgressSource sourceTimeline = default;
            bool hasSourceTimeline = false;
            VFXSplineAnimator sourceAnimator = anchor.sourceAnimator;
            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress &&
                anchor.bakeUseSourceAnimatorProgress &&
                sourceAnimator != null &&
                sourceAnimator.bakeProgressSource == VFXSplineBakeProgressSource.TimelineBoundAnimationTrack)
            {
                string timelineMessage;
                hasSourceTimeline = TryFindSourceTimelineProgressSource(sourceAnimator, out sourceTimeline, out timelineMessage);
                if (!hasSourceTimeline)
                {
                    EditorUtility.DisplayDialog("从 Source Timeline 烘焙 Anchor", timelineMessage, "确定");
                    return;
                }

                if (sourceAnimator.bakeUseTimelineClipDuration)
                    duration = Mathf.Max(0.01f, (float)sourceTimeline.timelineDuration);
            }

            int frameCount = Mathf.Max(1, Mathf.RoundToInt(duration * frameRate));
            int keyframeStep = Mathf.Max(1, anchor.bakeKeyframeStep);

            AnchorTimelineProgressSource anchorTimeline = default;
            bool hasAnchorTimeline = TryFindAnchorTimelineAnimationSource(anchor, out anchorTimeline);
            if (hasAnchorTimeline && !hasSourceTimeline)
            {
                duration = Mathf.Max(duration, (float)anchorTimeline.timelineDuration);
                frameCount = Mathf.Max(1, Mathf.RoundToInt(duration * frameRate));
            }

            string folder = NormalizeAssetFolder(string.IsNullOrEmpty(anchor.bakeSaveFolder) ? "Assets/Animations/SplineBakes" : anchor.bakeSaveFolder.Trim());
            EnsureAssetFolder(folder);

            string baseName = string.IsNullOrEmpty(anchor.bakeClipName) ? anchor.gameObject.name + "_AnchorBake" : anchor.bakeClipName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SanitizeFileName(baseName) + ".anim");

            AnimationClip clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(path);
            clip.frameRate = frameRate;

            List<AnchorBakeSample> samples = new List<AnchorBakeSample>();
            Transform tr = anchor.transform;
            Transform parent = tr.parent;
            Vector3 originalWorldPosition = tr.position;
            Quaternion originalWorldRotation = tr.rotation;
            AnchorStateSnapshot originalAnchorState = CaptureAnchorState(anchor);

            try
            {
                for (int i = 0; i <= frameCount; i++)
                {
                    bool shouldKey = (i % keyframeStep == 0);
                    if (anchor.bakeAlwaysKeyStartAndEnd && (i == 0 || i == frameCount))
                        shouldKey = true;
                    if (!shouldKey)
                        continue;

                    samples.Add(EvaluateAnchorBakeSample(anchor, activeSpline, parent, originalWorldPosition, originalWorldRotation, i, frameCount, frameRate, hasSourceTimeline, sourceTimeline, hasAnchorTimeline, anchorTimeline));
                }

                if (samples.Count == 0)
                    samples.Add(EvaluateAnchorBakeSample(anchor, activeSpline, parent, originalWorldPosition, originalWorldRotation, 0, frameCount, frameRate, hasSourceTimeline, sourceTimeline, hasAnchorTimeline, anchorTimeline));

                if (anchor.bakeAlwaysKeyStartAndEnd)
                {
                    if (samples[0].time > 0f)
                        samples.Insert(0, EvaluateAnchorBakeSample(anchor, activeSpline, parent, originalWorldPosition, originalWorldRotation, 0, frameCount, frameRate, hasSourceTimeline, sourceTimeline, hasAnchorTimeline, anchorTimeline));

                    float endTime = frameCount / (float)frameRate;
                    if (Mathf.Abs(samples[samples.Count - 1].time - endTime) > 0.0001f)
                        samples.Add(EvaluateAnchorBakeSample(anchor, activeSpline, parent, originalWorldPosition, originalWorldRotation, frameCount, frameCount, frameRate, hasSourceTimeline, sourceTimeline, hasAnchorTimeline, anchorTimeline));
                }
            }
            finally
            {
                RestoreAnchorState(anchor, originalAnchorState);
            }

            List<AnchorBakeSample> childSampleTimes = samples;
            if (anchor.bakeOptimizeCurves && samples.Count > 2)
                samples = OptimizeSamples(samples, anchor.bakePosition, anchor.bakeRotation, Mathf.Max(0f, anchor.bakePositionTolerance), Mathf.Max(0f, anchor.bakeRotationTolerance));

            WriteTransformCurves(clip, "", samples, anchor.bakePosition, anchor.bakeRotation, false);

            int bakedChildCount = 0;
            if (anchor.bakeChildren)
                bakedChildCount = WriteChildTransformCurves(clip, anchor, childSampleTimes, anchor.bakePosition, anchor.bakeRotation, anchor.bakeChildScale, duration);

            if (anchor.bakeRotation)
                clip.EnsureQuaternionContinuity();

            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (anchor.bakeAddAnimatorIfMissing && anchor.gameObject.GetComponent<Animator>() == null)
                Undo.AddComponent<Animator>(anchor.gameObject);

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            string report = BuildAnchorBakeReport(path, duration, frameRate, frameCount, samples.Count, keyframeStep, bakedChildCount, hasSourceTimeline, sourceTimeline, hasAnchorTimeline, anchorTimeline, anchor.bakeOptimizeCurves);
            Debug.Log(report, clip);
        }

        private static bool GetAnchorBakePreflight(VFXSplineAnchor anchor, out string message, out MessageType messageType)
        {
            if (anchor == null)
            {
                message = "Anchor 烘焙预检查：缺少 VFXSplineAnchor。";
                messageType = MessageType.Error;
                return false;
            }

            if (anchor.GetActiveSpline() == null)
            {
                message = "Anchor 烘焙预检查：请指定 Spline，或启用“自动使用 Source Spline”并指定有效的 Source Animator。";
                messageType = MessageType.Warning;
                return false;
            }

            bool hasBakeChannel = anchor.bakePosition || anchor.bakeRotation || (anchor.bakeChildren && anchor.bakeChildScale);
            if (!hasBakeChannel)
            {
                message = "Anchor 烘焙预检查：请开启“烘焙位置”“烘焙旋转”，或开启“烘焙子物体 + 烘焙子物体缩放”。";
                messageType = MessageType.Warning;
                return false;
            }

            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress)
            {
                if (anchor.sourceAnimator == null)
                {
                    message = "Anchor 烘焙预检查：Follow Animator Progress 模式需要指定 Source Animator。";
                    messageType = MessageType.Warning;
                    return false;
                }

                if (anchor.bakeUseSourceAnimatorProgress && anchor.sourceAnimator.bakeProgressSource == VFXSplineBakeProgressSource.ExistingAnimationClipProgressCurve)
                {
                    if (anchor.sourceAnimator.bakeSourceProgressClip == null)
                    {
                        message = "Anchor 烘焙预检查：Source Animator 使用 Existing AnimationClip Progress Curve，但缺少 Source Progress Clip。";
                        messageType = MessageType.Warning;
                        return false;
                    }

                    if (FindProgressCurve(anchor.sourceAnimator.bakeSourceProgressClip) == null)
                    {
                        message = "Anchor 烘焙预检查：Source Animator 的 Progress Clip 中没有 VFXSplineAnimator.progress 曲线，烘焙时会退回 Linear 0->1。";
                        messageType = MessageType.Warning;
                        return true;
                    }
                }

                if (anchor.bakeUseSourceAnimatorProgress && anchor.sourceAnimator.bakeProgressSource == VFXSplineBakeProgressSource.TimelineBoundAnimationTrack)
                {
                    AnchorTimelineProgressSource found;
                    string timelineMessage;
                    if (!TryFindSourceTimelineProgressSource(anchor.sourceAnimator, out found, out timelineMessage))
                    {
                        message = "Anchor 烘焙预检查：" + timelineMessage;
                        messageType = MessageType.Warning;
                        return false;
                    }
                }
            }

            int childCount = anchor.bakeChildren ? Mathf.Max(0, anchor.transform.GetComponentsInChildren<Transform>(true).Length - 1) : 0;
            string childInfo = anchor.bakeChildren ? "，子物体 Transform 数量：" + childCount : "";
            message = "Anchor 烘焙预检查：已就绪。时长 " + Mathf.Max(0.01f, anchor.bakeDuration).ToString("F2") + " 秒，帧率 " + Mathf.Clamp(anchor.bakeFrameRate, 1, 240) + " fps" + childInfo + "，输出目录：" + NormalizeAssetFolder(string.IsNullOrEmpty(anchor.bakeSaveFolder) ? "Assets/Animations/SplineBakes" : anchor.bakeSaveFolder.Trim());
            messageType = MessageType.Info;
            return true;
        }

        private static string BuildAnchorBakeReport(string path, float duration, int frameRate, int frameCount, int keyframes, int keyframeStep, int bakedChildCount, bool hasSourceTimeline, AnchorTimelineProgressSource sourceTimeline, bool hasAnchorTimeline, AnchorTimelineProgressSource anchorTimeline, bool optimized)
        {
            string report = "[VFX Timeline Spline Tool] Bake Anchor To AnimationClip 完成\n" +
                            "路径：" + path + "\n" +
                            "时长：" + duration.ToString("F3") + " 秒\n" +
                            "帧率：" + frameRate + "\n" +
                            "总帧数：" + frameCount + "\n" +
                            "Anchor 关键帧数：" + keyframes + "\n" +
                            "关键帧间隔：" + keyframeStep + "\n" +
                            "已烘焙子物体数量：" + bakedChildCount + "\n" +
                            "优化曲线：" + optimized;

            if (hasSourceTimeline)
                report += "\nSource Animator Timeline：" + BuildAnchorTimelineSummary(sourceTimeline);
            if (hasAnchorTimeline)
                report += "\nAnchor Timeline：" + BuildAnchorTimelineSummary(anchorTimeline);

            return report;
        }

        private static string BuildAnchorTimelineSummary(AnchorTimelineProgressSource source)
        {
            int clipCount = source.clips != null ? source.clips.Count : 0;
            return (source.isInfiniteClip ? "Infinite Clip" : clipCount + " 个 Clip") +
                   "，范围 " + source.timelineStart.ToString("F2") + "s - " + source.timelineEnd.ToString("F2") + "s" +
                   "，时长 " + source.timelineDuration.ToString("F2") + "s";
        }

        private static AnchorBakeSample EvaluateAnchorBakeSample(VFXSplineAnchor anchor, VFXSimpleSpline activeSpline, Transform parent, Vector3 originalWorldPosition, Quaternion originalWorldRotation, int frameIndex, int frameCount, int frameRate, bool hasSourceTimeline, AnchorTimelineProgressSource sourceTimeline, bool hasAnchorTimeline, AnchorTimelineProgressSource anchorTimeline)
        {
            float time = frameIndex / (float)frameRate;
            float normalized = frameCount <= 0 ? 0f : frameIndex / (float)frameCount;
            if (hasAnchorTimeline)
                SampleAnchorTimelineAnimation(anchor, anchorTimeline, time, normalized);

            float p = EvaluateAnchorProgressForBake(anchor, time, normalized, hasSourceTimeline, sourceTimeline);

            Vector3 worldPosition = originalWorldPosition;
            if (anchor.followPosition)
                worldPosition = activeSpline.GetPoint(p, anchor.useDistanceBasedProgress) + anchor.positionOffset;

            Quaternion worldRotation = originalWorldRotation;
            if (anchor.rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = activeSpline.GetTangent(p, anchor.useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = anchor.fallbackForward.sqrMagnitude > 0.000001f ? anchor.fallbackForward.normalized : Vector3.forward;
                worldRotation = anchor.BuildRotation(tangent) * Quaternion.Euler(anchor.rotationOffsetEuler);
            }

            AnchorBakeSample sample = new AnchorBakeSample();
            sample.time = time;
            sample.localPosition = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
            sample.localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * worldRotation : worldRotation;
            sample.localRotation = NormalizeQuaternion(sample.localRotation);
            sample.localScale = anchor.transform.localScale;
            return sample;
        }

        private static float EvaluateAnchorProgressForBake(VFXSplineAnchor anchor, float bakeTime, float normalizedTime, bool hasSourceTimeline, AnchorTimelineProgressSource sourceTimeline)
        {
            if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && anchor.sourceAnimator != null)
            {
                float sourceProgress = anchor.bakeUseSourceAnimatorProgress
                    ? EvaluateSourceAnimatorProgressForBake(anchor.sourceAnimator, bakeTime, normalizedTime, hasSourceTimeline, sourceTimeline)
                    : anchor.sourceAnimator.progress;

                float sourceEvaluated = anchor.sourceAnimator.EvaluateProgressValue(sourceProgress);
                return anchor.EvaluateProgressValue(sourceEvaluated + anchor.progressOffset);
            }

            return anchor.EvaluateProgressValue(anchor.progress);
        }

        private static float EvaluateSourceAnimatorProgressForBake(VFXSplineAnimator animator, float bakeTime, float normalizedTime, bool hasSourceTimeline, AnchorTimelineProgressSource sourceTimeline)
        {
            if (animator == null)
                return normalizedTime;

            switch (animator.bakeProgressSource)
            {
                case VFXSplineBakeProgressSource.BakeProgressCurve:
                    return animator.bakeProgressCurve != null ? animator.bakeProgressCurve.Evaluate(normalizedTime) : normalizedTime;

                case VFXSplineBakeProgressSource.CurrentAnimatorProgress:
                    return animator.progress;

                case VFXSplineBakeProgressSource.ExistingAnimationClipProgressCurve:
                    return EvaluateProgressFromSourceClip(animator, bakeTime, normalizedTime);

                case VFXSplineBakeProgressSource.TimelineBoundAnimationTrack:
                    return hasSourceTimeline ? EvaluateProgressFromTimelineSource(animator, sourceTimeline, bakeTime, normalizedTime) : normalizedTime;

                case VFXSplineBakeProgressSource.Linear01:
                default:
                    return normalizedTime;
            }
        }

        private static float EvaluateProgressFromSourceClip(VFXSplineAnimator animator, float bakeTime, float normalizedTime)
        {
            AnimationClip sourceClip = animator != null ? animator.bakeSourceProgressClip : null;
            if (sourceClip == null)
                return normalizedTime;

            AnimationCurve progressCurve = FindProgressCurve(sourceClip);
            if (progressCurve == null)
                return normalizedTime;

            float sourceTime = animator.bakeSourceClipUseNormalizedTime
                ? normalizedTime * Mathf.Max(0.0001f, sourceClip.length)
                : bakeTime;

            return progressCurve.Evaluate(sourceTime);
        }

        private static float EvaluateProgressFromTimelineSource(VFXSplineAnimator animator, AnchorTimelineProgressSource source, float bakeTime, float normalizedTime)
        {
            if (source.clips == null || source.clips.Count == 0)
                return normalizedTime;

            double timelineTime = source.timelineStart + bakeTime;
            float weightedProgress = 0f;
            float totalWeight = 0f;

            for (int i = 0; i < source.clips.Count; i++)
            {
                AnchorTimelineProgressClip progressClip = source.clips[i];
                if (progressClip.clip == null)
                    continue;

                if (timelineTime < progressClip.timelineStart || timelineTime > progressClip.timelineEnd)
                    continue;

                AnimationCurve progressCurve = FindProgressCurve(progressClip.clip);
                if (progressCurve == null)
                    continue;

                float weight = GetTimelineClipWeight(progressClip, timelineTime);
                if (weight <= 0.0001f)
                    continue;

                float sourceTime = GetSourceClipTime(animator, progressClip, timelineTime, bakeTime);
                weightedProgress += progressCurve.Evaluate(sourceTime) * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0.0001f)
                return weightedProgress / totalWeight;

            return normalizedTime;
        }

        private static float GetTimelineClipWeight(AnchorTimelineProgressClip progressClip, double timelineTime)
        {
            if (progressClip.isInfiniteClip || progressClip.timelineClip == null)
                return 1f;

            return Mathf.Clamp01(progressClip.timelineClip.EvaluateMixIn(timelineTime) * progressClip.timelineClip.EvaluateMixOut(timelineTime));
        }

        private static float GetSourceClipTime(VFXSplineAnimator animator, AnchorTimelineProgressClip progressClip, double timelineTime, float bakeTime)
        {
            float sourceLength = Mathf.Max(0.0001f, progressClip.clip != null ? progressClip.clip.length : 0f);
            if (progressClip.isInfiniteClip || progressClip.timelineClip == null)
            {
                if (animator != null && animator.bakeTimelineInfiniteClipMode == VFXSplineTimelineInfiniteClipBakeMode.Loop)
                    return Mathf.Repeat(bakeTime, sourceLength);

                return Mathf.Clamp(bakeTime, 0f, sourceLength);
            }

            double localTimelineTime = timelineTime - progressClip.timelineStart;
            return Mathf.Clamp((float)(progressClip.clipIn + localTimelineTime * progressClip.timelineClip.timeScale), 0f, sourceLength);
        }

        private static bool TryFindSourceTimelineProgressSource(VFXSplineAnimator animator, out AnchorTimelineProgressSource result, out string message)
        {
            result = default;
            message = "没有找到绑定到 Source Animator 的 Timeline Animation Track，或者 Track 上没有 VFXSplineAnimator.progress 曲线。";
            if (animator == null)
                return false;

            PlayableDirector[] directors;
            if (animator.bakePlayableDirector != null)
            {
                directors = new[] { animator.bakePlayableDirector };
            }
            else
            {
#if UNITY_2023_1_OR_NEWER
                directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                directors = Object.FindObjectsOfType<PlayableDirector>(true);
#endif
            }

            Animator targetAnimator = animator.GetComponent<Animator>();
            GameObject targetGameObject = animator.gameObject;
            Transform targetTransform = animator.transform;

            for (int d = 0; d < directors.Length; d++)
            {
                PlayableDirector director = directors[d];
                TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
                if (timeline == null)
                    continue;

                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    AnimationTrack animationTrack = track as AnimationTrack;
                    if (animationTrack == null)
                        continue;

                    Object binding = director.GetGenericBinding(animationTrack);
                    if (!IsTimelineBindingMatch(binding, targetAnimator, targetGameObject, targetTransform))
                        continue;

                    if (!animationTrack.inClipMode && animationTrack.infiniteClip != null && FindProgressCurve(animationTrack.infiniteClip) != null)
                    {
                        float clipLength = Mathf.Max(0.01f, animationTrack.infiniteClip.length);
                        result.clips = new List<AnchorTimelineProgressClip>()
                        {
                            new AnchorTimelineProgressClip()
                            {
                                timelineClip = null,
                                clip = animationTrack.infiniteClip,
                                timelineStart = 0.0,
                                timelineEnd = clipLength,
                                clipIn = 0.0,
                                isInfiniteClip = true
                            }
                        };
                        result.timelineStart = 0.0;
                        result.timelineEnd = clipLength;
                        result.timelineDuration = clipLength;
                        result.isInfiniteClip = true;
                        return true;
                    }

                    List<AnchorTimelineProgressClip> progressClips = new List<AnchorTimelineProgressClip>();
                    foreach (TimelineClip timelineClip in animationTrack.GetClips())
                    {
                        AnimationPlayableAsset playableAsset = timelineClip != null ? timelineClip.asset as AnimationPlayableAsset : null;
                        if (playableAsset == null || playableAsset.clip == null || FindProgressCurve(playableAsset.clip) == null)
                            continue;

                        progressClips.Add(new AnchorTimelineProgressClip()
                        {
                            timelineClip = timelineClip,
                            clip = playableAsset.clip,
                            timelineStart = timelineClip.start,
                            timelineEnd = timelineClip.end,
                            clipIn = timelineClip.clipIn,
                            isInfiniteClip = false
                        });
                    }

                    if (progressClips.Count <= 0)
                        continue;

                    progressClips.Sort((a, b) => a.timelineStart.CompareTo(b.timelineStart));
                    double start = progressClips[0].timelineStart;
                    double end = progressClips[0].timelineEnd;
                    for (int i = 1; i < progressClips.Count; i++)
                    {
                        start = System.Math.Min(start, progressClips[i].timelineStart);
                        end = System.Math.Max(end, progressClips[i].timelineEnd);
                    }

                    result.clips = progressClips;
                    result.timelineStart = start;
                    result.timelineEnd = end;
                    result.timelineDuration = System.Math.Max(0.01, end - start);
                    result.isInfiniteClip = false;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindAnchorTimelineAnimationSource(VFXSplineAnchor anchor, out AnchorTimelineProgressSource result)
        {
            result = default;
            if (anchor == null)
                return false;

            PlayableDirector[] directors;
#if UNITY_2023_1_OR_NEWER
            directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            directors = Object.FindObjectsOfType<PlayableDirector>(true);
#endif

            Animator targetAnimator = anchor.GetComponent<Animator>();
            GameObject targetGameObject = anchor.gameObject;
            Transform targetTransform = anchor.transform;

            for (int d = 0; d < directors.Length; d++)
            {
                PlayableDirector director = directors[d];
                TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
                if (timeline == null)
                    continue;

                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    AnimationTrack animationTrack = track as AnimationTrack;
                    if (animationTrack == null)
                        continue;

                    Object binding = director.GetGenericBinding(animationTrack);
                    if (!IsTimelineBindingMatch(binding, targetAnimator, targetGameObject, targetTransform))
                        continue;

                    if (!animationTrack.inClipMode && animationTrack.infiniteClip != null)
                    {
                        float clipLength = Mathf.Max(0.01f, animationTrack.infiniteClip.length);
                        result.clips = new List<AnchorTimelineProgressClip>()
                        {
                            new AnchorTimelineProgressClip()
                            {
                                timelineClip = null,
                                clip = animationTrack.infiniteClip,
                                timelineStart = 0.0,
                                timelineEnd = clipLength,
                                clipIn = 0.0,
                                isInfiniteClip = true
                            }
                        };
                        result.timelineStart = 0.0;
                        result.timelineEnd = clipLength;
                        result.timelineDuration = clipLength;
                        result.isInfiniteClip = true;
                        return true;
                    }

                    List<AnchorTimelineProgressClip> clips = new List<AnchorTimelineProgressClip>();
                    foreach (TimelineClip timelineClip in animationTrack.GetClips())
                    {
                        AnimationPlayableAsset playableAsset = timelineClip != null ? timelineClip.asset as AnimationPlayableAsset : null;
                        if (playableAsset == null || playableAsset.clip == null)
                            continue;

                        clips.Add(new AnchorTimelineProgressClip()
                        {
                            timelineClip = timelineClip,
                            clip = playableAsset.clip,
                            timelineStart = timelineClip.start,
                            timelineEnd = timelineClip.end,
                            clipIn = timelineClip.clipIn,
                            isInfiniteClip = false
                        });
                    }

                    if (clips.Count <= 0)
                        continue;

                    clips.Sort((a, b) => a.timelineStart.CompareTo(b.timelineStart));
                    double start = clips[0].timelineStart;
                    double end = clips[0].timelineEnd;
                    for (int i = 1; i < clips.Count; i++)
                    {
                        start = System.Math.Min(start, clips[i].timelineStart);
                        end = System.Math.Max(end, clips[i].timelineEnd);
                    }

                    result.clips = clips;
                    result.timelineStart = start;
                    result.timelineEnd = end;
                    result.timelineDuration = System.Math.Max(0.01, end - start);
                    result.isInfiniteClip = false;
                    return true;
                }
            }

            return false;
        }

        private static void SampleAnchorTimelineAnimation(VFXSplineAnchor anchor, AnchorTimelineProgressSource source, float bakeTime, float normalizedTime)
        {
            if (anchor == null || source.clips == null || source.clips.Count == 0)
                return;

            double timelineTime = source.timelineStart + bakeTime;
            AnchorTimelineProgressClip bestClip = default;
            bool hasClip = false;

            for (int i = 0; i < source.clips.Count; i++)
            {
                AnchorTimelineProgressClip progressClip = source.clips[i];
                if (progressClip.clip == null)
                    continue;

                if (timelineTime < progressClip.timelineStart || timelineTime > progressClip.timelineEnd)
                    continue;

                bestClip = progressClip;
                hasClip = true;
                break;
            }

            if (!hasClip)
            {
                int index = Mathf.Clamp(Mathf.RoundToInt(normalizedTime * (source.clips.Count - 1)), 0, source.clips.Count - 1);
                bestClip = source.clips[index];
                if (bestClip.clip == null)
                    return;
            }

            float sourceTime = GetSourceClipTime(null, bestClip, timelineTime, bakeTime);
            AnchorStateSnapshot objectReferences = CaptureAnchorObjectReferences(anchor);
            try
            {
                bestClip.clip.SampleAnimation(anchor.gameObject, sourceTime);
            }
            finally
            {
                RestoreAnchorObjectReferences(anchor, objectReferences);
            }
        }

        private static AnchorStateSnapshot CaptureAnchorState(VFXSplineAnchor anchor)
        {
            AnchorStateSnapshot snapshot = new AnchorStateSnapshot();
            if (anchor == null)
                return snapshot;

            snapshot.componentJson = EditorJsonUtility.ToJson(anchor);
            CaptureAnchorObjectReferences(anchor, ref snapshot);
            snapshot.hierarchyTransforms = CaptureTransformSnapshots(anchor.transform.GetComponentsInChildren<Transform>(true));
            return snapshot;
        }

        private static AnchorStateSnapshot CaptureAnchorObjectReferences(VFXSplineAnchor anchor)
        {
            AnchorStateSnapshot snapshot = new AnchorStateSnapshot();
            CaptureAnchorObjectReferences(anchor, ref snapshot);
            return snapshot;
        }

        private static void CaptureAnchorObjectReferences(VFXSplineAnchor anchor, ref AnchorStateSnapshot snapshot)
        {
            if (anchor == null)
                return;

            snapshot.spline = anchor.spline;
            snapshot.sourceAnimator = anchor.sourceAnimator;
            snapshot.bakeChildAnimationClip = anchor.bakeChildAnimationClip;
        }

        private static void RestoreAnchorState(VFXSplineAnchor anchor, AnchorStateSnapshot snapshot)
        {
            if (anchor == null)
                return;

            if (!string.IsNullOrEmpty(snapshot.componentJson))
                EditorJsonUtility.FromJsonOverwrite(snapshot.componentJson, anchor);

            RestoreAnchorObjectReferences(anchor, snapshot);
            RestoreTransformSnapshots(snapshot.hierarchyTransforms);
            EditorUtility.SetDirty(anchor);
        }

        private static void RestoreAnchorObjectReferences(VFXSplineAnchor anchor, AnchorStateSnapshot snapshot)
        {
            if (anchor == null)
                return;

            anchor.spline = snapshot.spline;
            anchor.sourceAnimator = snapshot.sourceAnimator;
            anchor.bakeChildAnimationClip = snapshot.bakeChildAnimationClip;
        }

        private static bool IsTimelineBindingMatch(Object binding, Animator targetAnimator, GameObject targetGameObject, Transform targetTransform)
        {
            if (binding == null)
                return false;
            if (targetAnimator != null && binding == targetAnimator)
                return true;
            if (binding == targetGameObject || binding == targetTransform)
                return true;

            GameObject boundGameObject = null;
            if (binding is Component component)
                boundGameObject = component.gameObject;
            else if (binding is GameObject gameObject)
                boundGameObject = gameObject;

            return boundGameObject != null && boundGameObject == targetGameObject;
        }

        private static AnimationCurve FindProgressCurve(AnimationClip clip)
        {
            if (clip == null)
                return null;

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding b = bindings[i];
                bool typeMatch = b.type == typeof(VFXSplineAnimator) || b.type.Name == "VFXSplineAnimator";
                bool propertyMatch = b.propertyName == "progress" || b.propertyName.EndsWith(".progress");
                if (typeMatch && propertyMatch)
                    return AnimationUtility.GetEditorCurve(clip, b);
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding b = bindings[i];
                if (b.propertyName == "progress" || b.propertyName.EndsWith(".progress"))
                    return AnimationUtility.GetEditorCurve(clip, b);
            }

            return null;
        }

        private static int WriteChildTransformCurves(AnimationClip clip, VFXSplineAnchor anchor, List<AnchorBakeSample> timeSamples, bool bakePosition, bool bakeRotation, bool bakeScale, float bakeDuration)
        {
            Transform root = anchor != null ? anchor.transform : null;
            if (root == null || timeSamples == null || timeSamples.Count == 0)
                return 0;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Dictionary<Transform, List<AnchorBakeSample>> animatedSamples = anchor.bakeChildAnimationClip != null
                ? BuildAnimatedLocalSampleMap(anchor, transforms, timeSamples, bakeDuration)
                : null;
            if (animatedSamples == null && anchor.bakeAutoSampleChildAnimations)
                animatedSamples = BuildAnimatedLocalSampleMap(anchor, transforms, timeSamples, bakeDuration);

            int count = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform child = transforms[i];
                if (child == null || child == root)
                    continue;

                string path = AnimationUtility.CalculateTransformPath(child, root);
                if (string.IsNullOrEmpty(path))
                    continue;

                List<AnchorBakeSample> childSamples;
                if (animatedSamples == null || !animatedSamples.TryGetValue(child, out childSamples))
                {
                    childSamples = BuildStaticLocalSamples(child, timeSamples);
                }
                else if (!HasAnimatedVariation(childSamples, bakePosition, bakeRotation, bakeScale))
                {
                    Debug.LogWarning("[VFX Timeline Spline Tool] 子物体动画采样后没有 Transform 变化：" + path + "。请检查子物体 AnimationClip 的路径是否匹配当前层级。", child);
                }
                WriteTransformCurves(clip, path, childSamples, bakePosition, bakeRotation, bakeScale);
                count++;
            }

            return count;
        }

        private static Dictionary<Transform, List<AnchorBakeSample>> BuildAnimatedLocalSampleMap(VFXSplineAnchor anchor, Transform[] transforms, List<AnchorBakeSample> timeSamples, float bakeDuration)
        {
            Dictionary<Transform, List<AnchorBakeSample>> result = new Dictionary<Transform, List<AnchorBakeSample>>();
            AnimationClip sourceClip = anchor != null ? anchor.bakeChildAnimationClip : null;
            if (anchor == null || transforms == null || timeSamples == null)
                return result;

            Dictionary<GameObject, AnimationClip> childAnimationSources = sourceClip == null
                ? FindChildAnimationSources(anchor.transform, transforms)
                : null;
            if (sourceClip == null && (childAnimationSources == null || childAnimationSources.Count == 0))
                return result;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform tr = transforms[i];
                if (tr != null && tr != anchor.transform)
                    result[tr] = new List<AnchorBakeSample>(timeSamples.Count);
            }

            if (result.Count == 0)
                return result;

            Dictionary<Transform, TransformSnapshot> originalTransforms = CaptureTransformSnapshots(transforms);
            AnchorStateSnapshot anchorObjectReferences = CaptureAnchorObjectReferences(anchor);
            try
            {
                for (int sampleIndex = 0; sampleIndex < timeSamples.Count; sampleIndex++)
                {
                    if (sourceClip != null)
                    {
                        float sourceTime = GetChildAnimationSampleTime(anchor, sourceClip, timeSamples[sampleIndex].time, bakeDuration);
                        sourceClip.SampleAnimation(anchor.gameObject, sourceTime);
                        RestoreAnchorObjectReferences(anchor, anchorObjectReferences);
                    }
                    else
                    {
                        foreach (KeyValuePair<GameObject, AnimationClip> pair in childAnimationSources)
                        {
                            if (pair.Key == null || pair.Value == null)
                                continue;

                            float childSourceTime = GetChildAnimationSampleTime(anchor, pair.Value, timeSamples[sampleIndex].time, bakeDuration);
                            pair.Value.SampleAnimation(pair.Key, childSourceTime);
                        }
                        RestoreAnchorObjectReferences(anchor, anchorObjectReferences);
                    }

                    for (int i = 0; i < transforms.Length; i++)
                    {
                        Transform tr = transforms[i];
                        if (tr == null || tr == anchor.transform)
                            continue;

                        List<AnchorBakeSample> samples;
                        if (!result.TryGetValue(tr, out samples))
                            continue;

                        AnchorBakeSample sample = new AnchorBakeSample();
                        sample.time = timeSamples[sampleIndex].time;
                        sample.localPosition = tr.localPosition;
                        sample.localRotation = NormalizeQuaternion(tr.localRotation);
                        sample.localScale = tr.localScale;
                        samples.Add(sample);
                    }
                }
            }
            finally
            {
                RestoreAnchorObjectReferences(anchor, anchorObjectReferences);
                RestoreTransformSnapshots(originalTransforms);
            }

            return result;
        }

        private static Dictionary<Transform, TransformSnapshot> CaptureTransformSnapshots(Transform[] transforms)
        {
            Dictionary<Transform, TransformSnapshot> snapshots = new Dictionary<Transform, TransformSnapshot>();
            if (transforms == null)
                return snapshots;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform tr = transforms[i];
                if (tr == null || snapshots.ContainsKey(tr))
                    continue;

                TransformSnapshot snapshot = new TransformSnapshot();
                snapshot.localPosition = tr.localPosition;
                snapshot.localRotation = tr.localRotation;
                snapshot.localScale = tr.localScale;
                snapshots.Add(tr, snapshot);
            }

            return snapshots;
        }

        private static void RestoreTransformSnapshots(Dictionary<Transform, TransformSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (KeyValuePair<Transform, TransformSnapshot> pair in snapshots)
            {
                Transform tr = pair.Key;
                if (tr == null)
                    continue;

                tr.localPosition = pair.Value.localPosition;
                tr.localRotation = pair.Value.localRotation;
                tr.localScale = pair.Value.localScale;
            }
        }

        private static bool HasAnimatedVariation(List<AnchorBakeSample> samples, bool checkPosition, bool checkRotation, bool checkScale)
        {
            if (samples == null || samples.Count <= 1)
                return false;

            AnchorBakeSample first = samples[0];
            for (int i = 1; i < samples.Count; i++)
            {
                AnchorBakeSample sample = samples[i];
                if (checkPosition && Vector3.Distance(first.localPosition, sample.localPosition) > 0.00001f)
                    return true;
                if (checkRotation && Quaternion.Angle(first.localRotation, sample.localRotation) > 0.001f)
                    return true;
                if (checkScale && Vector3.Distance(first.localScale, sample.localScale) > 0.00001f)
                    return true;
            }

            return false;
        }

        private static Dictionary<GameObject, AnimationClip> FindChildAnimationSources(Transform root, Transform[] transforms)
        {
            Dictionary<GameObject, AnimationClip> result = new Dictionary<GameObject, AnimationClip>();
            if (root == null || transforms == null)
                return result;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform tr = transforms[i];
                if (tr == null || tr == root)
                    continue;

                AnimationClip clip = FindAnimationClipOnObject(tr.gameObject);
                if (clip != null && !result.ContainsKey(tr.gameObject))
                    result.Add(tr.gameObject, clip);
            }

            return result;
        }

        private static AnimationClip FindAnimationClipOnObject(GameObject target)
        {
            if (target == null)
                return null;

            Animation legacyAnimation = target.GetComponent<Animation>();
            if (legacyAnimation != null && legacyAnimation.clip != null)
                return legacyAnimation.clip;

            Animator animator = target.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null)
                        return clips[i];
                }
            }

            return null;
        }

        private static float GetChildAnimationSampleTime(VFXSplineAnchor anchor, AnimationClip sourceClip, float bakeTime, float bakeDuration)
        {
            float clipLength = Mathf.Max(0.0001f, sourceClip.length);
            if (anchor.bakeChildAnimationUseNormalizedTime)
            {
                float normalized = bakeDuration <= 0.0001f ? 0f : Mathf.Clamp01(bakeTime / bakeDuration);
                return normalized * clipLength;
            }

            if (anchor.bakeChildAnimationLoop)
                return Mathf.Repeat(bakeTime, clipLength);

            return Mathf.Clamp(bakeTime, 0f, clipLength);
        }

        private static List<AnchorBakeSample> BuildStaticLocalSamples(Transform target, List<AnchorBakeSample> timeSamples)
        {
            List<AnchorBakeSample> samples = new List<AnchorBakeSample>(timeSamples.Count);
            Vector3 localPosition = target.localPosition;
            Quaternion localRotation = NormalizeQuaternion(target.localRotation);
            Vector3 localScale = target.localScale;

            for (int i = 0; i < timeSamples.Count; i++)
            {
                AnchorBakeSample sample = new AnchorBakeSample();
                sample.time = timeSamples[i].time;
                sample.localPosition = localPosition;
                sample.localRotation = localRotation;
                sample.localScale = localScale;
                samples.Add(sample);
            }

            return samples;
        }

        private static void WriteTransformCurves(AnimationClip clip, string path, List<AnchorBakeSample> samples, bool bakePosition, bool bakeRotation, bool bakeScale)
        {
            if (clip == null || samples == null || samples.Count == 0)
                return;

            AnimationCurve px = new AnimationCurve();
            AnimationCurve py = new AnimationCurve();
            AnimationCurve pz = new AnimationCurve();
            AnimationCurve rx = new AnimationCurve();
            AnimationCurve ry = new AnimationCurve();
            AnimationCurve rz = new AnimationCurve();
            AnimationCurve rw = new AnimationCurve();
            AnimationCurve sx = new AnimationCurve();
            AnimationCurve sy = new AnimationCurve();
            AnimationCurve sz = new AnimationCurve();

            for (int i = 0; i < samples.Count; i++)
            {
                AnchorBakeSample sample = samples[i];
                if (bakePosition)
                {
                    px.AddKey(sample.time, sample.localPosition.x);
                    py.AddKey(sample.time, sample.localPosition.y);
                    pz.AddKey(sample.time, sample.localPosition.z);
                }

                if (bakeRotation)
                {
                    Quaternion q = NormalizeQuaternion(sample.localRotation);
                    rx.AddKey(sample.time, q.x);
                    ry.AddKey(sample.time, q.y);
                    rz.AddKey(sample.time, q.z);
                    rw.AddKey(sample.time, q.w);
                }

                if (bakeScale)
                {
                    sx.AddKey(sample.time, sample.localScale.x);
                    sy.AddKey(sample.time, sample.localScale.y);
                    sz.AddKey(sample.time, sample.localScale.z);
                }
            }

            if (bakePosition)
            {
                SetTransformCurve(clip, path, "m_LocalPosition.x", px);
                SetTransformCurve(clip, path, "m_LocalPosition.y", py);
                SetTransformCurve(clip, path, "m_LocalPosition.z", pz);
            }

            if (bakeRotation)
            {
                SetTransformCurve(clip, path, "m_LocalRotation.x", rx);
                SetTransformCurve(clip, path, "m_LocalRotation.y", ry);
                SetTransformCurve(clip, path, "m_LocalRotation.z", rz);
                SetTransformCurve(clip, path, "m_LocalRotation.w", rw);
            }

            if (bakeScale)
            {
                SetTransformCurve(clip, path, "m_LocalScale.x", sx);
                SetTransformCurve(clip, path, "m_LocalScale.y", sy);
                SetTransformCurve(clip, path, "m_LocalScale.z", sz);
            }
        }

        private static List<AnchorBakeSample> OptimizeSamples(List<AnchorBakeSample> input, bool checkPosition, bool checkRotation, float positionTolerance, float rotationTolerance)
        {
            if (input == null || input.Count <= 2)
                return input;

            List<AnchorBakeSample> result = new List<AnchorBakeSample>();
            result.Add(input[0]);

            for (int i = 1; i < input.Count - 1; i++)
            {
                AnchorBakeSample prev = input[i - 1];
                AnchorBakeSample current = input[i];
                AnchorBakeSample next = input[i + 1];

                float span = next.time - prev.time;
                if (span <= 0.0001f)
                {
                    result.Add(current);
                    continue;
                }

                float t = Mathf.Clamp01((current.time - prev.time) / span);
                bool keep = false;

                if (checkPosition)
                {
                    Vector3 estimatedPosition = Vector3.Lerp(prev.localPosition, next.localPosition, t);
                    keep |= Vector3.Distance(estimatedPosition, current.localPosition) > positionTolerance;
                }

                if (checkRotation)
                {
                    Quaternion estimatedRotation = Quaternion.Slerp(prev.localRotation, next.localRotation, t);
                    keep |= Quaternion.Angle(estimatedRotation, current.localRotation) > rotationTolerance;
                }

                if (keep)
                    result.Add(current);
            }

            result.Add(input[input.Count - 1]);
            return result;
        }

        private static void SetTransformCurve(AnimationClip clip, string propertyName, AnimationCurve curve)
        {
            SetTransformCurve(clip, "", propertyName, curve);
        }

        private static void SetTransformCurve(AnimationClip clip, string path, string propertyName, AnimationCurve curve)
        {
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = path,
                type = typeof(Transform),
                propertyName = propertyName
            };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag <= 0.000001f)
                return Quaternion.identity;

            float inv = 1f / mag;
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return "Assets/Animations/SplineBakes";

            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (folder.StartsWith(Application.dataPath))
                folder = "Assets" + folder.Substring(Application.dataPath.Length);
            if (!folder.StartsWith("Assets"))
                folder = "Assets/" + folder.TrimStart('/');

            return string.IsNullOrEmpty(folder) ? "Assets/Animations/SplineBakes" : folder;
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetFolder(folder);
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "AnchorBake";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
                name = name.Replace(invalidChars[i], '_');

            return string.IsNullOrEmpty(name.Trim()) ? "AnchorBake" : name.Trim();
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
        private static readonly List<VFXSplineAnchor> cachedAnchors = new List<VFXSplineAnchor>();
        private static bool anchorsCacheDirty = true;

        static VFXSplineAnchorSceneDrawer()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
            EditorApplication.hierarchyChanged -= MarkAnchorsCacheDirty;
            EditorApplication.hierarchyChanged += MarkAnchorsCacheDirty;
        }

        private static void MarkAnchorsCacheDirty()
        {
            anchorsCacheDirty = true;
        }

        private static void RefreshAnchorsCacheIfNeeded()
        {
            if (!anchorsCacheDirty)
                return;

            cachedAnchors.Clear();
            cachedAnchors.AddRange(Object.FindObjectsOfType<VFXSplineAnchor>());
            anchorsCacheDirty = false;
        }

        private static void DuringSceneGUI(SceneView view)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            RefreshAnchorsCacheIfNeeded();
            for (int i = cachedAnchors.Count - 1; i >= 0; i--)
            {
                VFXSplineAnchor anchor = cachedAnchors[i];
                if (anchor == null)
                {
                    cachedAnchors.RemoveAt(i);
                    continue;
                }

                if (anchor.GetActiveSpline() == null || !anchor.showSceneLabel) continue;
                if (Selection.activeGameObject == anchor.gameObject) continue;
                VFXSplineAnchorEditor.DrawAnchorHandle(anchor, false);
            }
        }
    }
}
#endif

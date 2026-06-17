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

    [CustomEditor(typeof(VFXSplineAnimator))]
    public class VFXSplineAnimatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VFXSplineAnimator animator = (VFXSplineAnimator)target;

            EditorGUILayout.LabelField("VFX Spline Animator - 路径运动控制 v" + VFXSplineToolVersion.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "推荐流程：用 Unity 原生 AnimationClip / Timeline Animation Track 给 Progress 打关键帧，控制物体沿 Spline 运动。当前版本支持 Timeline Clip Blending、Timeline Infinite Clip Hold/Loop 烘焙，以及烘焙前检查。",
                MessageType.Info);

            serializedObject.Update();

            DrawProperty("spline", "Spline 路径");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Animation Track 可 K 参数", EditorStyles.boldLabel);
            DrawProperty("progress", "Progress 进度");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("运动设置", EditorStyles.boldLabel);
            DrawProperty("reverse", "反向");
            DrawProperty("loop", "循环");
            DrawProperty("useDistanceBasedProgress", "使用距离等速 Progress");
            DrawProperty("positionOffset", "位置偏移");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("旋转设置", EditorStyles.boldLabel);
            DrawProperty("rotationMode", "旋转模式");
            DrawProperty("forwardAxis", "前向轴");
            DrawProperty("rotationOffsetEuler", "旋转偏移 Euler");
            DrawProperty("fallbackForward", "备用前向");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("编辑器预览", EditorStyles.boldLabel);
            DrawProperty("editorPreviewMode", "编辑器预览模式");
            DrawProperty("previewInEditMode", "编辑模式预览");
            DrawProperty("applyOnValidate", "参数变化时自动应用");
            DrawProperty("showCurrentProgressPoint", "显示当前 Progress 点");
            DrawProperty("previewColor", "预览颜色");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progress 快捷设置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("设为 0%")) SetProgress(animator, 0f);
                if (GUILayout.Button("设为 25%")) SetProgress(animator, 0.25f);
                if (GUILayout.Button("设为 50%")) SetProgress(animator, 0.5f);
                if (GUILayout.Button("设为 75%")) SetProgress(animator, 0.75f);
                if (GUILayout.Button("设为 100%")) SetProgress(animator, 1f);
            }

            if (GUILayout.Button("应用当前 Progress"))
            {
                Undo.RecordObject(animator.transform, "Apply Current Progress");
                animator.ApplyCurrentProgress(true);
                EditorUtility.SetDirty(animator);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Progress 设为起点")) SetProgress(animator, 0f);
            if (GUILayout.Button("Progress 设为终点")) SetProgress(animator, 1f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
            if (GUILayout.Button("缺少 Animator 时自动添加"))
            {
                AddAnimatorIfMissing(animator.gameObject);
            }

            if (GUILayout.Button("创建默认 Progress AnimationClip"))
            {
                CreateDefaultProgressClip(animator);
            }

            if (GUILayout.Button("设置 Animation Track 工作流（推荐）"))
            {
                AddAnimatorIfMissing(animator.gameObject);
                CreateDefaultProgressClip(animator);
                SetProgress(animator, 0f);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("烘焙为 AnimationClip", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "把当前 Spline 驱动结果烘焙成 Unity 原生 Transform AnimationClip。Bake Progress Source 用来决定烘焙时 Progress 从哪里来；Keyframe Step / Optimize Curves 用来减少关键帧。",
                MessageType.Info);

            serializedObject.Update();
            DrawProperty("bakeFrameRate", "帧率 Frame Rate");
            DrawProperty("bakeDuration", "时长 Duration");
            DrawProperty("bakePosition", "烘焙位置");
            DrawProperty("bakeRotation", "烘焙旋转");
            DrawProperty("bakeProgressSource", "Progress 来源");

            SerializedProperty progressSourceProp = serializedObject.FindProperty("bakeProgressSource");
            VFXSplineBakeProgressSource progressSource = (VFXSplineBakeProgressSource)progressSourceProp.enumValueIndex;

            if (progressSource == VFXSplineBakeProgressSource.BakeProgressCurve)
            {
                DrawProperty("bakeProgressCurve", "烘焙 Progress 曲线");
                EditorGUILayout.HelpBox("使用 Bake Progress Curve 控制烘焙节奏。适合没有单独 K Progress、只想快速做缓入缓出/停顿/回弹的情况。", MessageType.None);
            }
            else if (progressSource == VFXSplineBakeProgressSource.CurrentAnimatorProgress)
            {
                EditorGUILayout.HelpBox("使用当前 Inspector 里的 Progress 值烘焙静态姿态。通常用于烘焙路径上的某个固定点，不适合生成完整飞行动画。", MessageType.None);
            }
            else if (progressSource == VFXSplineBakeProgressSource.ExistingAnimationClipProgressCurve)
            {
                DrawProperty("bakeSourceProgressClip", "源 Progress Clip");
                DrawProperty("bakeSourceClipUseNormalizedTime", "使用归一化源时间");
                EditorGUILayout.HelpBox("读取已有 AnimationClip 里的 VFXSplineAnimator.progress 曲线。适合你已经用 Animation Track 精细 K 好 Progress 后，再烘焙成普通 Transform AnimationClip。", MessageType.Info);
            }
            else if (progressSource == VFXSplineBakeProgressSource.TimelineBoundAnimationTrack)
            {
                DrawProperty("bakePlayableDirector", "Playable Director");
                DrawProperty("bakeUseTimelineClipDuration", "使用 Timeline Clip 时长");
                DrawProperty("bakeTimelineInfiniteClipMode", "Infinite Clip 模式");

                TimelineProgressSource found;
                string message;
                bool ok = TryFindTimelineProgressSource(animator, out found, out message);
                if (ok)
                {
                    EditorGUILayout.HelpBox(BuildTimelineFoundMessage(found), MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(message, MessageType.Warning);
                }

                using (new EditorGUI.DisabledScope(!ok))
                {
                    if (GUILayout.Button("使用找到的 Timeline Clip 时长"))
                    {
                        Undo.RecordObject(animator, "Use Timeline Clip Duration");
                        animator.bakeDuration = Mathf.Max(0.01f, (float)found.timelineDuration);
                        EditorUtility.SetDirty(animator);
                    }
                }

                EditorGUILayout.HelpBox("此模式会自动读取绑定当前物体的 Timeline Animation Track。普通 Clip 会读取 ClipIn；Infinite Clip 可选择 Hold Last Frame 或 Loop。", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Linear 0→1：烘焙时 Progress 会在 Duration 内从 0 匀速走到 1。", MessageType.None);
            }

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
            bool canBake = GetBakePreflight(animator, out preflightMessage, out preflightType);
            EditorGUILayout.HelpBox(preflightMessage, preflightType);

            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("烘焙为 AnimationClip"))
                {
                    BakeToAnimationClip(animator);
                }
            }
        }

        private void DrawProperty(string name, string label)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null)
                EditorGUILayout.PropertyField(p, BuildContent(label, name, p));
        }

        private static GUIContent BuildContent(string label, string propertyName, SerializedProperty property)
        {
            string tooltip = !string.IsNullOrEmpty(property.tooltip) ? property.tooltip : GetAnimatorPropertyTooltip(propertyName);
            return new GUIContent(label, tooltip);
        }

        private static string GetAnimatorPropertyTooltip(string propertyName)
        {
            switch (propertyName)
            {
                case "spline": return "驱动物体运动的 Spline 路径。";
                case "progress": return "物体在路径上的进度，0 为起点，1 为终点。通常由 Animation Track 或 Timeline 控制。";
                case "reverse": return "反向读取 Progress，让 0 对应终点、1 对应起点。";
                case "loop": return "Progress 超出 0-1 时循环回路径范围内。";
                case "useDistanceBasedProgress": return "开启后按路径实际距离计算 Progress，让运动速度更均匀。";
                case "positionOffset": return "沿路径计算位置后额外叠加的世界坐标偏移。";
                case "rotationMode": return "是否根据路径切线自动旋转物体。";
                case "forwardAxis": return "指定模型哪根本地轴作为前进方向。";
                case "rotationOffsetEuler": return "在路径方向旋转之后额外叠加的 Euler 角偏移。";
                case "fallbackForward": return "路径切线过短时使用的备用前向方向。";
                case "editorPreviewMode": return "控制编辑模式下是否自动预览路径运动结果。";
                case "previewInEditMode": return "旧版预览开关，保留兼容。关闭后等同于编辑器预览关闭。";
                case "applyOnValidate": return "Inspector 参数变化时立即把当前 Progress 应用到 Transform。";
                case "showCurrentProgressPoint": return "在 Scene 视图中显示当前 Progress 所在点。";
                case "previewColor": return "当前 Progress 点和方向箭头的显示颜色。";
                default: return "";
            }
        }

        private static void SetProgress(VFXSplineAnimator animator, float value)
        {
            Undo.RecordObject(animator, "Set Spline Progress");
            Undo.RecordObject(animator.transform, "Apply Spline Progress");
            animator.SetProgress(value);
            EditorUtility.SetDirty(animator);
            SceneView.RepaintAll();
        }

        private static void AddAnimatorIfMissing(GameObject go)
        {
            if (go == null)
                return;

            if (go.GetComponent<Animator>() == null)
            {
                Undo.AddComponent<Animator>(go);
                EditorUtility.SetDirty(go);
            }
        }

        private static void CreateDefaultProgressClip(VFXSplineAnimator animator)
        {
            string folder = "Assets/Animations";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Animations");

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Spline_Progress_Clip.anim");
            AnimationClip clip = new AnimationClip { frameRate = 60f };
            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(VFXSplineAnimator),
                path = "",
                propertyName = "progress"
            };
            AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
        }

        private struct TimelineProgressSource
        {
            public PlayableDirector director;
            public AnimationClip clip;
            public List<TimelineProgressClip> clips;
            public double timelineStart;
            public double timelineEnd;
            public double timelineDuration;
            public double clipIn;
            public string trackName;
            public bool isInfiniteClip;
        }

        private struct TimelineProgressClip
        {
            public TimelineClip timelineClip;
            public AnimationClip clip;
            public double timelineStart;
            public double timelineEnd;
            public double timelineDuration;
            public double clipIn;
            public bool isInfiniteClip;
        }

        private struct BakeSample
        {
            public float time;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        private static void BakeToAnimationClip(VFXSplineAnimator animator)
        {
            if (animator == null || animator.spline == null)
            {
                EditorUtility.DisplayDialog("烘焙为 AnimationClip", "请先指定 Spline。", "确定");
                return;
            }

            int frameRate = Mathf.Clamp(animator.bakeFrameRate, 1, 240);
            float duration = Mathf.Max(0.01f, animator.bakeDuration);

            TimelineProgressSource timelineSource = default;
            bool hasTimelineSource = false;
            if (animator.bakeProgressSource == VFXSplineBakeProgressSource.TimelineBoundAnimationTrack)
            {
                string timelineMessage;
                hasTimelineSource = TryFindTimelineProgressSource(animator, out timelineSource, out timelineMessage);
                if (!hasTimelineSource)
                {
                    EditorUtility.DisplayDialog("从 Timeline 烘焙", timelineMessage, "确定");
                    return;
                }

                if (animator.bakeUseTimelineClipDuration)
                    duration = Mathf.Max(0.01f, (float)timelineSource.timelineDuration);
            }

            int frameCount = Mathf.Max(1, Mathf.RoundToInt(duration * frameRate));
            int keyframeStep = Mathf.Max(1, animator.bakeKeyframeStep);

            string folder = string.IsNullOrEmpty(animator.bakeSaveFolder) ? "Assets/Animations/SplineBakes" : animator.bakeSaveFolder.Trim();
            folder = NormalizeAssetFolder(folder);
            EnsureAssetFolder(folder);

            string baseName = string.IsNullOrEmpty(animator.bakeClipName) ? animator.gameObject.name + "_SplineBake" : animator.bakeClipName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SanitizeFileName(baseName) + ".anim");

            AnimationClip clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(path);
            clip.frameRate = frameRate;

            List<BakeSample> samples = new List<BakeSample>();
            Transform tr = animator.transform;
            Transform parent = tr.parent;
            Quaternion originalWorldRotation = tr.rotation;

            for (int i = 0; i <= frameCount; i++)
            {
                bool shouldKey = (i % keyframeStep == 0);
                if (animator.bakeAlwaysKeyStartAndEnd && (i == 0 || i == frameCount))
                    shouldKey = true;

                if (!shouldKey)
                    continue;

                samples.Add(EvaluateBakeSample(animator, parent, originalWorldRotation, i, frameCount, frameRate, hasTimelineSource, timelineSource));
            }

            if (samples.Count == 0)
                samples.Add(EvaluateBakeSample(animator, parent, originalWorldRotation, 0, frameCount, frameRate, hasTimelineSource, timelineSource));

            if (animator.bakeAlwaysKeyStartAndEnd)
            {
                if (samples[0].time > 0f)
                    samples.Insert(0, EvaluateBakeSample(animator, parent, originalWorldRotation, 0, frameCount, frameRate, hasTimelineSource, timelineSource));

                float endTime = frameCount / (float)frameRate;
                if (Mathf.Abs(samples[samples.Count - 1].time - endTime) > 0.0001f)
                    samples.Add(EvaluateBakeSample(animator, parent, originalWorldRotation, frameCount, frameCount, frameRate, hasTimelineSource, timelineSource));
            }

            if (animator.bakeOptimizeCurves && samples.Count > 2)
            {
                samples = OptimizeSamples(samples, animator.bakePosition, animator.bakeRotation, Mathf.Max(0f, animator.bakePositionTolerance), Mathf.Max(0f, animator.bakeRotationTolerance));
            }

            AnimationCurve px = new AnimationCurve();
            AnimationCurve py = new AnimationCurve();
            AnimationCurve pz = new AnimationCurve();
            AnimationCurve rx = new AnimationCurve();
            AnimationCurve ry = new AnimationCurve();
            AnimationCurve rz = new AnimationCurve();
            AnimationCurve rw = new AnimationCurve();

            for (int i = 0; i < samples.Count; i++)
            {
                BakeSample sample = samples[i];
                if (animator.bakePosition)
                {
                    px.AddKey(sample.time, sample.localPosition.x);
                    py.AddKey(sample.time, sample.localPosition.y);
                    pz.AddKey(sample.time, sample.localPosition.z);
                }

                if (animator.bakeRotation)
                {
                    Quaternion q = NormalizeQuaternion(sample.localRotation);
                    rx.AddKey(sample.time, q.x);
                    ry.AddKey(sample.time, q.y);
                    rz.AddKey(sample.time, q.z);
                    rw.AddKey(sample.time, q.w);
                }
            }

            if (animator.bakePosition)
            {
                SetTransformCurve(clip, "m_LocalPosition.x", px);
                SetTransformCurve(clip, "m_LocalPosition.y", py);
                SetTransformCurve(clip, "m_LocalPosition.z", pz);
            }

            if (animator.bakeRotation)
            {
                SetTransformCurve(clip, "m_LocalRotation.x", rx);
                SetTransformCurve(clip, "m_LocalRotation.y", ry);
                SetTransformCurve(clip, "m_LocalRotation.z", rz);
                SetTransformCurve(clip, "m_LocalRotation.w", rw);
                clip.EnsureQuaternionContinuity();
            }

            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (animator.bakeAddAnimatorIfMissing)
                AddAnimatorIfMissing(animator.gameObject);

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);

            string report = BuildBakeReport(path, duration, frameRate, frameCount, samples.Count, keyframeStep, animator.bakeProgressSource.ToString(), hasTimelineSource ? BuildTimelineFoundMessage(timelineSource) : null, animator.bakeOptimizeCurves);
            Debug.Log(report, clip);
        }

        private static bool GetBakePreflight(VFXSplineAnimator animator, out string message, out MessageType messageType)
        {
            if (animator == null)
            {
                message = "烘焙预检查：缺少 VFXSplineAnimator。";
                messageType = MessageType.Error;
                return false;
            }

            if (animator.spline == null)
            {
                message = "烘焙预检查：请先指定 Spline。";
                messageType = MessageType.Warning;
                return false;
            }

            if (!animator.bakePosition && !animator.bakeRotation)
            {
                message = "烘焙预检查：请至少开启“烘焙位置”或“烘焙旋转”。";
                messageType = MessageType.Warning;
                return false;
            }

            if (animator.bakeProgressSource == VFXSplineBakeProgressSource.ExistingAnimationClipProgressCurve)
            {
                if (animator.bakeSourceProgressClip == null)
                {
                    message = "烘焙预检查：缺少源 Progress Clip。请指定 Clip，或改用其他 Progress 来源。";
                    messageType = MessageType.Warning;
                    return false;
                }

                if (FindProgressCurve(animator.bakeSourceProgressClip) == null)
                {
                    message = "烘焙预检查：源 Progress Clip 中没有 VFXSplineAnimator.progress 曲线，烘焙时会退回 Linear 0->1。";
                    messageType = MessageType.Warning;
                    return true;
                }
            }

            if (animator.bakeProgressSource == VFXSplineBakeProgressSource.TimelineBoundAnimationTrack)
            {
                TimelineProgressSource found;
                string timelineMessage;
                if (!TryFindTimelineProgressSource(animator, out found, out timelineMessage))
                {
                    message = "烘焙预检查：" + timelineMessage;
                    messageType = MessageType.Warning;
                    return false;
                }

                message = "烘焙预检查：已就绪。Timeline 时长 " + found.timelineDuration.ToString("F2") + " 秒，Progress Clip 数量：" + GetTimelineClipCount(found) + "，输出目录：" + NormalizeAssetFolder(string.IsNullOrEmpty(animator.bakeSaveFolder) ? "Assets/Animations/SplineBakes" : animator.bakeSaveFolder.Trim());
                messageType = MessageType.Info;
                return true;
            }

            message = "烘焙预检查：已就绪。时长 " + Mathf.Max(0.01f, animator.bakeDuration).ToString("F2") + " 秒，帧率 " + Mathf.Clamp(animator.bakeFrameRate, 1, 240) + " fps，输出目录：" + NormalizeAssetFolder(string.IsNullOrEmpty(animator.bakeSaveFolder) ? "Assets/Animations/SplineBakes" : animator.bakeSaveFolder.Trim());
            messageType = MessageType.Info;
            return true;
        }

        private static string BuildBakeReport(string path, float duration, int frameRate, int frameCount, int keyframes, int keyframeStep, string progressSource, string timelineInfo, bool optimized)
        {
            string report = "[VFX Timeline Spline Tool] Bake To AnimationClip 完成\n" +
                            "路径：" + path + "\n" +
                            "时长：" + duration.ToString("F3") + " 秒\n" +
                            "帧率：" + frameRate + "\n" +
                            "总帧数：" + frameCount + "\n" +
                            "关键帧数：" + keyframes + "\n" +
                            "关键帧间隔：" + keyframeStep + "\n" +
                            "Progress 来源：" + progressSource + "\n" +
                            "优化曲线：" + optimized;

            if (!string.IsNullOrEmpty(timelineInfo))
                report += "\n\n" + timelineInfo;

            return report;
        }

        private static float EvaluateBakeProgress(VFXSplineAnimator animator, float bakeTime, float normalizedTime, bool hasTimelineSource, TimelineProgressSource timelineSource)
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
                    return hasTimelineSource ? EvaluateProgressFromTimelineSource(animator, timelineSource, bakeTime, normalizedTime) : normalizedTime;
                case VFXSplineBakeProgressSource.Linear01:
                default:
                    return normalizedTime;
            }
        }

        private static float EvaluateProgressFromTimelineSource(VFXSplineAnimator animator, TimelineProgressSource source, float bakeTime, float normalizedTime)
        {
            if (source.clips == null || source.clips.Count == 0)
                return normalizedTime;

            double timelineTime = source.timelineStart + bakeTime;
            float weightedProgress = 0f;
            float totalWeight = 0f;

            for (int i = 0; i < source.clips.Count; i++)
            {
                TimelineProgressClip progressClip = source.clips[i];
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

            return EvaluateNearestTimelineProgress(animator, source, timelineTime, normalizedTime);
        }

        private static float GetTimelineClipWeight(TimelineProgressClip progressClip, double timelineTime)
        {
            if (progressClip.isInfiniteClip || progressClip.timelineClip == null)
                return 1f;

            float mixIn = progressClip.timelineClip.EvaluateMixIn(timelineTime);
            float mixOut = progressClip.timelineClip.EvaluateMixOut(timelineTime);
            return Mathf.Clamp01(mixIn * mixOut);
        }

        private static float GetSourceClipTime(VFXSplineAnimator animator, TimelineProgressClip progressClip, double timelineTime, float bakeTime)
        {
            float sourceLength = Mathf.Max(0.0001f, progressClip.clip != null ? progressClip.clip.length : 0f);

            if (progressClip.isInfiniteClip || progressClip.timelineClip == null)
            {
                if (animator != null && animator.bakeTimelineInfiniteClipMode == VFXSplineTimelineInfiniteClipBakeMode.Loop)
                    return Mathf.Repeat(bakeTime, sourceLength);

                return Mathf.Clamp(bakeTime, 0f, sourceLength);
            }

            double localTimelineTime = timelineTime - progressClip.timelineStart;
            float sourceTime = (float)(progressClip.clipIn + localTimelineTime * progressClip.timelineClip.timeScale);
            return Mathf.Clamp(sourceTime, 0f, sourceLength);
        }

        private static float EvaluateNearestTimelineProgress(VFXSplineAnimator animator, TimelineProgressSource source, double timelineTime, float fallback)
        {
            if (source.clips == null || source.clips.Count == 0)
                return fallback;

            TimelineProgressClip nearest = source.clips[0];
            double nearestDistance = double.MaxValue;

            for (int i = 0; i < source.clips.Count; i++)
            {
                TimelineProgressClip progressClip = source.clips[i];
                double distance;
                if (timelineTime < progressClip.timelineStart)
                    distance = progressClip.timelineStart - timelineTime;
                else if (timelineTime > progressClip.timelineEnd)
                    distance = timelineTime - progressClip.timelineEnd;
                else
                    distance = 0.0;

                if (distance < nearestDistance)
                {
                    nearest = progressClip;
                    nearestDistance = distance;
                }
            }

            if (nearest.clip == null)
                return fallback;

            AnimationCurve progressCurve = FindProgressCurve(nearest.clip);
            if (progressCurve == null)
                return fallback;

            double clampedTimelineTime = System.Math.Max(nearest.timelineStart, System.Math.Min(nearest.timelineEnd, timelineTime));
            float sourceTime = GetSourceClipTime(animator, nearest, clampedTimelineTime, 0f);
            return progressCurve.Evaluate(sourceTime);
        }

        private static string BuildTimelineFoundMessage(TimelineProgressSource found)
        {
            string type = found.isInfiniteClip ? "Infinite Clip" : "Timeline Clips";
            string modeInfo = found.isInfiniteClip ? "\nInfinite Clip Mode：" + found.trackName : "";
            return "已找到 Timeline Progress Source：" + GetTimelineClipSummary(found) +
                   "\nTrack：" + found.trackName +
                   "\nType：" + type +
                   "\nClip Count：" + GetTimelineClipCount(found) +
                   "\nTimeline Range：" + found.timelineStart.ToString("F2") + "s - " + found.timelineEnd.ToString("F2") + "s" +
                   "\nDuration：" + found.timelineDuration.ToString("F2") + "s" + modeInfo;
        }

        private static int GetTimelineClipCount(TimelineProgressSource source)
        {
            return source.clips != null ? source.clips.Count : (source.clip != null ? 1 : 0);
        }

        private static string GetTimelineClipSummary(TimelineProgressSource source)
        {
            if (source.clips != null && source.clips.Count > 0)
            {
                if (source.clips.Count == 1 && source.clips[0].clip != null)
                    return source.clips[0].clip.name;

                return source.clips.Count + " clips";
            }

            return source.clip != null ? source.clip.name : "None";
        }

        private static bool TryFindTimelineProgressSource(VFXSplineAnimator animator, out TimelineProgressSource result, out string message)
        {
            result = default;
            message = "没有找到绑定到当前物体的 Timeline Animation Track，或者 Track 上的 AnimationClip / Infinite Clip 没有 VFXSplineAnimator.progress 曲线。";
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
                if (director == null)
                    continue;

                TimelineAsset timeline = director.playableAsset as TimelineAsset;
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
                        AnimationCurve infiniteProgressCurve = FindProgressCurve(animationTrack.infiniteClip);
                        if (infiniteProgressCurve != null)
                        {
                            float clipLength = Mathf.Max(0.01f, animationTrack.infiniteClip.length);
                            TimelineProgressClip infiniteClip = new TimelineProgressClip()
                            {
                                timelineClip = null,
                                clip = animationTrack.infiniteClip,
                                timelineStart = 0.0,
                                timelineDuration = clipLength,
                                timelineEnd = clipLength,
                                clipIn = 0.0,
                                isInfiniteClip = true
                            };

                            result.director = director;
                            result.clip = animationTrack.infiniteClip;
                            result.clips = new List<TimelineProgressClip>() { infiniteClip };
                            result.timelineStart = 0.0;
                            result.timelineDuration = clipLength;
                            result.timelineEnd = clipLength;
                            result.clipIn = 0.0;
                            result.trackName = animationTrack.name + " (Infinite Clip)";
                            result.isInfiniteClip = true;
                            message = "已找到 Timeline Infinite Progress Clip。";
                            return true;
                        }
                    }

                    List<TimelineProgressClip> progressClips = new List<TimelineProgressClip>();
                    foreach (TimelineClip timelineClip in animationTrack.GetClips())
                    {
                        if (timelineClip == null)
                            continue;

                        AnimationPlayableAsset playableAsset = timelineClip.asset as AnimationPlayableAsset;
                        if (playableAsset == null || playableAsset.clip == null)
                            continue;

                        AnimationCurve progressCurve = FindProgressCurve(playableAsset.clip);
                        if (progressCurve == null)
                            continue;

                        progressClips.Add(new TimelineProgressClip()
                        {
                            timelineClip = timelineClip,
                            clip = playableAsset.clip,
                            timelineStart = timelineClip.start,
                            timelineDuration = timelineClip.duration,
                            timelineEnd = timelineClip.end,
                            clipIn = timelineClip.clipIn,
                            isInfiniteClip = false
                        });
                    }

                    if (progressClips.Count <= 0)
                        continue;

                    progressClips.Sort((a, b) => a.timelineStart.CompareTo(b.timelineStart));

                    double timelineStart = progressClips[0].timelineStart;
                    double timelineEnd = progressClips[0].timelineEnd;
                    for (int i = 1; i < progressClips.Count; i++)
                    {
                        timelineStart = System.Math.Min(timelineStart, progressClips[i].timelineStart);
                        timelineEnd = System.Math.Max(timelineEnd, progressClips[i].timelineEnd);
                    }

                    result.director = director;
                    result.clip = progressClips[0].clip;
                    result.clips = progressClips;
                    result.timelineStart = timelineStart;
                    result.timelineEnd = timelineEnd;
                    result.timelineDuration = System.Math.Max(0.01, timelineEnd - timelineStart);
                    result.clipIn = progressClips[0].clipIn;
                    result.trackName = animationTrack.name;
                    result.isInfiniteClip = false;
                    message = "已找到 Timeline Progress Clips。";
                    return true;
                }
            }

            return false;
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

        private static float EvaluateProgressFromSourceClip(VFXSplineAnimator animator, float bakeTime, float normalizedTime)
        {
            AnimationClip sourceClip = animator != null ? animator.bakeSourceProgressClip : null;
            if (sourceClip == null)
                return normalizedTime;

            AnimationCurve progressCurve = FindProgressCurve(sourceClip);
            if (progressCurve == null)
            {
                Debug.LogWarning("[VFX Timeline Spline Tool] Source Progress Clip 中没有找到 VFXSplineAnimator.progress 曲线，将退回 Linear 0→1。", sourceClip);
                return normalizedTime;
            }

            float sourceTime = animator.bakeSourceClipUseNormalizedTime
                ? normalizedTime * Mathf.Max(0.0001f, sourceClip.length)
                : bakeTime;

            return progressCurve.Evaluate(sourceTime);
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

            // 兼容少数情况下 Unity 只保存 propertyName，没有正确 type 的情况。
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding b = bindings[i];
                if (b.propertyName == "progress" || b.propertyName.EndsWith(".progress"))
                    return AnimationUtility.GetEditorCurve(clip, b);
            }

            return null;
        }

        private static BakeSample EvaluateBakeSample(VFXSplineAnimator animator, Transform parent, Quaternion originalWorldRotation, int frameIndex, int frameCount, int frameRate, bool hasTimelineSource, TimelineProgressSource timelineSource)
        {
            float time = frameIndex / (float)frameRate;
            float normalized = frameCount <= 0 ? 0f : frameIndex / (float)frameCount;
            float sourceProgress = EvaluateBakeProgress(animator, time, normalized, hasTimelineSource, timelineSource);

            float p = Mathf.Clamp01(sourceProgress);
            if (animator.reverse)
                p = 1f - p;

            Vector3 worldPos = animator.spline.GetPoint(p, animator.useDistanceBasedProgress) + animator.positionOffset;
            Quaternion worldRot = originalWorldRotation;

            if (animator.rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = animator.spline.GetTangent(p, animator.useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = animator.fallbackForward.sqrMagnitude > 0.000001f ? animator.fallbackForward.normalized : Vector3.forward;

                worldRot = animator.BuildRotation(tangent) * Quaternion.Euler(animator.rotationOffsetEuler);
            }

            BakeSample sample = new BakeSample();
            sample.time = time;
            sample.localPosition = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
            sample.localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * worldRot : worldRot;
            sample.localRotation = NormalizeQuaternion(sample.localRotation);
            return sample;
        }

        private static List<BakeSample> OptimizeSamples(List<BakeSample> input, bool checkPosition, bool checkRotation, float positionTolerance, float rotationTolerance)
        {
            if (input == null || input.Count <= 2)
                return input;

            List<BakeSample> result = new List<BakeSample>();
            result.Add(input[0]);

            int anchorIndex = 0;
            int testIndex = 1;

            while (testIndex < input.Count - 1)
            {
                int nextIndex = testIndex + 1;
                bool canRemove = true;

                for (int i = anchorIndex + 1; i < nextIndex; i++)
                {
                    float span = input[nextIndex].time - input[anchorIndex].time;
                    float t = span <= 0.000001f ? 0f : Mathf.InverseLerp(input[anchorIndex].time, input[nextIndex].time, input[i].time);

                    if (checkPosition)
                    {
                        Vector3 predicted = Vector3.Lerp(input[anchorIndex].localPosition, input[nextIndex].localPosition, t);
                        float error = Vector3.Distance(predicted, input[i].localPosition);
                        if (error > positionTolerance)
                        {
                            canRemove = false;
                            break;
                        }
                    }

                    if (checkRotation)
                    {
                        Quaternion predicted = Quaternion.Slerp(input[anchorIndex].localRotation, input[nextIndex].localRotation, t);
                        float error = Quaternion.Angle(predicted, input[i].localRotation);
                        if (error > rotationTolerance)
                        {
                            canRemove = false;
                            break;
                        }
                    }
                }

                if (canRemove)
                {
                    testIndex++;
                }
                else
                {
                    result.Add(input[testIndex]);
                    anchorIndex = testIndex;
                    testIndex++;
                }
            }

            result.Add(input[input.Count - 1]);
            return result;
        }

        private static void SetTransformCurve(AnimationClip clip, string propertyName, AnimationCurve curve)
        {
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(Transform),
                propertyName = propertyName
            };
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        private static Quaternion NormalizeQuaternion(Quaternion q)
        {
            float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (mag < 0.000001f)
                return Quaternion.identity;

            return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
        }

        private static string NormalizeAssetFolder(string folder)
        {
            folder = folder.Replace("\\", "/");
            if (!folder.StartsWith("Assets"))
                folder = "Assets/" + folder.TrimStart('/');

            return folder.TrimEnd('/');
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetFolder(folder);
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
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");

            return string.IsNullOrEmpty(name) ? "SplineBake" : name;
        }

        private void OnSceneGUI()
        {
            VFXSplineAnimator animator = (VFXSplineAnimator)target;
            if (animator == null || animator.spline == null || !animator.showCurrentProgressPoint)
                return;

            float p = animator.EvaluateProgressValue(animator.progress);
            Vector3 pos = animator.spline.GetPoint(p, animator.useDistanceBasedProgress);
            Vector3 tangent = animator.spline.GetTangent(p, animator.useDistanceBasedProgress);

            Handles.color = animator.previewColor;
            float size = HandleUtility.GetHandleSize(pos) * 0.18f;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);

            if (tangent.sqrMagnitude > 0.000001f)
                Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(tangent, Vector3.up), size * 2.5f, EventType.Repaint);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = animator.previewColor;
            Handles.Label(pos + Vector3.up * size * 2.2f, "Progress: " + animator.progress.ToString("F2"), style);
        }
    }
}
#endif

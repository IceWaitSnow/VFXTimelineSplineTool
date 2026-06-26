#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [CustomEditor(typeof(VFXSplineAnimationClip))]
    public class VFXSplineAnimationClipEditor : Editor
    {
        private struct BakeSample
        {
            public float time;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("VFX Spline Animation Clip 快速模式", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这是 Timeline 自定义 Track 的快速模式。Clip 长度控制从 Start Progress 到 End Progress 的时间；如果开启 Loop Playback，则单圈速度由 Seconds Per Loop 控制，Clip 可以任意拉长用于循环。", MessageType.Info);

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("spline"), new GUIContent("Spline 路径", "Timeline Clip 使用的 Spline 路径。请从 Hierarchy 拖入场景中的 VFXSimpleSpline。"));

            SerializedProperty t = serializedObject.FindProperty("template");
            DrawTemplateProperties(t);

            EditorGUILayout.Space(8);
            DrawBakeProperties();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTemplateProperties(SerializedProperty t)
        {
            EditorGUILayout.PropertyField(t.FindPropertyRelative("startProgress"), new GUIContent("起始 Progress", "Clip 开始时的路径进度。0 为起点，1 为终点。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("endProgress"), new GUIContent("结束 Progress", "Clip 结束时的路径进度。0 为起点，1 为终点。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("useSpeedCurve"), new GUIContent("使用速度曲线", "开启后，用下方曲线重新映射 Clip 内部时间，可制作缓入缓出。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("speedCurve"), new GUIContent("速度曲线", "X 为 Clip 内部时间比例，Y 为 Start Progress 到 End Progress 之间的插值比例。"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(t.FindPropertyRelative("reverse"), new GUIContent("反向", "反向播放路径进度。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("loopPlayback"), new GUIContent("循环播放", "开启后，Clip 可以拉长为多轮循环，单圈速度由 Seconds Per Loop 控制。"));

            SerializedProperty loopPlayback = t.FindPropertyRelative("loopPlayback");
            if (loopPlayback != null && loopPlayback.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(t.FindPropertyRelative("secondsPerLoop"), new GUIContent("每圈秒数", "Loop Playback 开启时，一圈路径需要的秒数。"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(t.FindPropertyRelative("useDistanceBasedProgress"), new GUIContent("使用距离等速 Progress", "开启后按路径实际距离计算 Progress，让运动速度更均匀。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("positionOffset"), new GUIContent("位置偏移", "沿路径计算位置后额外叠加的世界坐标偏移。"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(t.FindPropertyRelative("followRotation"), new GUIContent("跟随旋转", "开启后，物体会根据路径切线旋转。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("rotationMode"), new GUIContent("旋转模式", "控制如何根据路径切线计算旋转。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("forwardAxis"), new GUIContent("前向轴", "指定模型哪根本地轴作为前进方向。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("rotationOffsetEuler"), new GUIContent("旋转偏移 Euler", "在路径方向旋转之后额外叠加的 Euler 角偏移。"));
            EditorGUILayout.PropertyField(t.FindPropertyRelative("ignoreSplineTransformRotation"), new GUIContent("忽略 Spline 旋转", "位置仍跟随旋转后的 Spline，但旋转计算会忽略 Spline 物体自身的 Transform 旋转。"));
        }

        private void DrawBakeProperties()
        {
            EditorGUILayout.LabelField("烘焙为 AnimationClip", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把这个 Timeline Clip 快速模式的路径运动直接烘焙成普通 Unity Transform AnimationClip。烘焙时长请填 Timeline Clip Timing 里的 Duration。", MessageType.Info);

            DrawProperty("bakeTarget", "烘焙目标");
            DrawProperty("bakeFrameRate", "帧率");
            DrawProperty("bakeUseTimelineClipDuration", "自动使用 Clip 时长");
            TrySyncTimelineClipDuration();
            DrawProperty("bakeDuration", "烘焙时长");
            DrawProperty("bakePosition", "烘焙位置");
            DrawProperty("bakeRotation", "烘焙旋转");
            DrawProperty("bakeSaveFolder", "保存目录");
            DrawProperty("bakeClipName", "文件名");
            DrawProperty("bakeAddAnimatorIfMissing", "自动添加 Animator");
            EditorGUILayout.Space(4);
            DrawProperty("bakeKeyframeStep", "关键帧间隔");
            DrawProperty("bakeAlwaysKeyStartAndEnd", "强制保留首尾帧");
            DrawProperty("bakeOptimizeCurves", "优化曲线");

            SerializedProperty optimizeProp = serializedObject.FindProperty("bakeOptimizeCurves");
            if (optimizeProp != null && optimizeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                DrawProperty("bakePositionTolerance", "位置误差阈值");
                DrawProperty("bakeRotationTolerance", "旋转误差阈值");
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();

            VFXSplineAnimationClip clipAsset = (VFXSplineAnimationClip)target;
            string message;
            MessageType messageType;
            bool canBake = GetBakePreflight(clipAsset, out message, out messageType);
            EditorGUILayout.HelpBox(message, messageType);

            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("烘焙为 AnimationClip"))
                    BakeToAnimationClip(clipAsset);
            }
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty prop = serializedObject.FindProperty(propertyName);
            if (prop == null)
                return;

            EditorGUILayout.PropertyField(prop, new GUIContent(label, GetTooltip(propertyName)));
        }

        private static string GetTooltip(string propertyName)
        {
            switch (propertyName)
            {
                case "bakeTarget": return "要写入 Transform 动画曲线的目标物体。通常就是 Timeline Track 绑定的物体。";
                case "bakeFrameRate": return "烘焙输出帧率。特效运动通常建议 60。";
                case "bakeUseTimelineClipDuration": return "开启后，自动识别当前 Timeline Clip 的 Duration 并写入烘焙时长。";
                case "bakeDuration": return "烘焙时长，单位秒。请填成 Timeline Clip Timing 的 Duration。";
                case "bakePosition": return "开启后写入 m_LocalPosition 曲线。";
                case "bakeRotation": return "开启后写入 m_LocalRotation 四元数曲线。";
                case "bakeSaveFolder": return "输出 AnimationClip 的 Assets 目录。不存在时会自动创建。";
                case "bakeClipName": return "输出文件名。为空时使用目标物体名加 _SplineClipBake。";
                case "bakeAddAnimatorIfMissing": return "烘焙完成后，如果目标物体没有 Animator，则自动添加 Animator 组件，方便直接挂 Clip。";
                case "bakeKeyframeStep": return "烘焙关键帧间隔。1=每帧记录，2=每 2 帧记录一次，5=每 5 帧记录一次。";
                case "bakeAlwaysKeyStartAndEnd": return "开启后，无论关键帧间隔是多少，都会保留第一帧和最后一帧。";
                case "bakeOptimizeCurves": return "开启后，会按误差阈值自动删除对运动影响很小的中间关键帧。";
                case "bakePositionTolerance": return "位置优化允许的最大误差，单位为 Unity 距离。";
                case "bakeRotationTolerance": return "旋转优化允许的最大误差，单位为角度。";
                default: return "";
            }
        }

        private static bool GetBakePreflight(VFXSplineAnimationClip clipAsset, out string message, out MessageType messageType)
        {
            if (clipAsset == null)
            {
                message = "烘焙预检查：缺少 VFXSplineAnimationClip。";
                messageType = MessageType.Error;
                return false;
            }

            VFXSimpleSpline spline = GetBakeSpline(clipAsset);
            if (spline == null)
            {
                message = "烘焙预检查：请先指定 Spline 路径。";
                messageType = MessageType.Warning;
                return false;
            }

            Transform bakeTarget = GetBakeTarget(clipAsset);
            if (bakeTarget == null)
            {
                message = "烘焙预检查：请先指定烘焙目标。通常拖入这条 Timeline Track 绑定的物体。";
                messageType = MessageType.Warning;
                return false;
            }

            if (!clipAsset.bakePosition && !clipAsset.bakeRotation)
            {
                message = "烘焙预检查：请至少开启“烘焙位置”或“烘焙旋转”。";
                messageType = MessageType.Warning;
                return false;
            }

            float duration = GetEffectiveBakeDuration(clipAsset);
            TimelineClip sourceTimelineClip;
            double sourceTimelineDuration;
            string durationSource = clipAsset.bakeUseTimelineClipDuration && TryGetTimelineClipDuration(clipAsset, out sourceTimelineClip, out sourceTimelineDuration) ? "Timeline Clip" : "手动设置";
            int frameRate = Mathf.Clamp(clipAsset.bakeFrameRate, 1, 240);
            int frameCount = Mathf.Max(1, Mathf.RoundToInt(duration * frameRate));
            int estimatedKeys = EstimateKeyCount(frameCount, Mathf.Max(1, clipAsset.bakeKeyframeStep), clipAsset.bakeAlwaysKeyStartAndEnd);
            string optimizeText = clipAsset.bakeOptimizeCurves ? "，会继续按误差阈值优化" : "";
            message = "烘焙预检查：已就绪。目标：" + bakeTarget.name + "，时长 " + duration.ToString("F2") + " 秒（" + durationSource + "），帧率 " + frameRate + " fps，预计关键帧约 " + estimatedKeys + " 个" + optimizeText + "。";
            messageType = MessageType.Info;
            return true;
        }

        private void TrySyncTimelineClipDuration()
        {
            SerializedProperty useTimelineDurationProp = serializedObject.FindProperty("bakeUseTimelineClipDuration");
            SerializedProperty durationProp = serializedObject.FindProperty("bakeDuration");
            if (useTimelineDurationProp == null || durationProp == null || !useTimelineDurationProp.boolValue)
                return;

            VFXSplineAnimationClip clipAsset = (VFXSplineAnimationClip)target;
            TimelineClip timelineClip;
            double duration;
            if (!TryGetTimelineClipDuration(clipAsset, out timelineClip, out duration))
                return;

            float detectedDuration = Mathf.Max(0.01f, (float)duration);
            if (Mathf.Abs(durationProp.floatValue - detectedDuration) > 0.0001f)
                durationProp.floatValue = detectedDuration;

            EditorGUILayout.HelpBox("已自动识别 Timeline Clip 时长：" + detectedDuration.ToString("F3") + " 秒。", MessageType.None);
        }

        private static float GetEffectiveBakeDuration(VFXSplineAnimationClip clipAsset)
        {
            if (clipAsset != null && clipAsset.bakeUseTimelineClipDuration)
            {
                TimelineClip timelineClip;
                double duration;
                if (TryGetTimelineClipDuration(clipAsset, out timelineClip, out duration))
                    return Mathf.Max(0.01f, (float)duration);
            }

            return Mathf.Max(0.01f, clipAsset != null ? clipAsset.bakeDuration : 1f);
        }

        private static VFXSimpleSpline GetBakeSpline(VFXSplineAnimationClip clipAsset)
        {
            if (clipAsset == null)
                return null;

            IExposedPropertyTable resolver = GetCurrentTimelineResolver();
            if (resolver != null)
            {
                VFXSimpleSpline resolved = clipAsset.spline.Resolve(resolver);
                if (resolved != null)
                    return resolved;
            }

            return clipAsset.spline.defaultValue as VFXSimpleSpline;
        }

        private static Transform GetBakeTarget(VFXSplineAnimationClip clipAsset)
        {
            if (clipAsset == null)
                return null;

            IExposedPropertyTable resolver = GetCurrentTimelineResolver();
            if (resolver != null)
            {
                Transform resolved = clipAsset.bakeTarget.Resolve(resolver);
                if (resolved != null)
                    return resolved;
            }

            return clipAsset.bakeTarget.defaultValue as Transform;
        }

        private static IExposedPropertyTable GetCurrentTimelineResolver()
        {
            PlayableDirector director = GetCurrentTimelineDirector();
            if (director != null)
                return director;

            return null;
        }

        private static PlayableDirector GetCurrentTimelineDirector()
        {
            System.Type timelineEditorType = System.Type.GetType("UnityEditor.Timeline.TimelineEditor, Unity.Timeline.Editor");
            if (timelineEditorType == null)
                return null;

            PropertyInfo inspectedDirectorProperty = timelineEditorType.GetProperty("inspectedDirector", BindingFlags.Public | BindingFlags.Static);
            if (inspectedDirectorProperty == null)
                return null;

            return inspectedDirectorProperty.GetValue(null, null) as PlayableDirector;
        }

        private static TimelineAsset GetCurrentTimelineAsset()
        {
            System.Type timelineEditorType = System.Type.GetType("UnityEditor.Timeline.TimelineEditor, Unity.Timeline.Editor");
            if (timelineEditorType != null)
            {
                PropertyInfo inspectedAssetProperty = timelineEditorType.GetProperty("inspectedAsset", BindingFlags.Public | BindingFlags.Static);
                if (inspectedAssetProperty != null)
                {
                    TimelineAsset inspectedAsset = inspectedAssetProperty.GetValue(null, null) as TimelineAsset;
                    if (inspectedAsset != null)
                        return inspectedAsset;
                }
            }

            PlayableDirector director = GetCurrentTimelineDirector();
            return director != null ? director.playableAsset as TimelineAsset : null;
        }

        private static bool TryGetTimelineClipDuration(VFXSplineAnimationClip clipAsset, out TimelineClip timelineClip, out double duration)
        {
            duration = 0.0;
            if (!TryGetTimelineClip(clipAsset, out timelineClip))
                return false;

            duration = timelineClip.duration;
            return duration > 0.00001;
        }

        private static bool TryGetTimelineClip(VFXSplineAnimationClip clipAsset, out TimelineClip timelineClip)
        {
            timelineClip = null;
            if (clipAsset == null)
                return false;

            TimelineAsset timeline = GetCurrentTimelineAsset();
            if (timeline == null)
                return false;

            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track == null)
                    continue;

                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip == null || clip.asset != clipAsset)
                        continue;

                    timelineClip = clip;
                    return true;
                }
            }

            return false;
        }

        private static void BakeToAnimationClip(VFXSplineAnimationClip clipAsset)
        {
            VFXSimpleSpline spline = GetBakeSpline(clipAsset);
            Transform targetTransform = GetBakeTarget(clipAsset);
            if (spline == null || targetTransform == null)
            {
                EditorUtility.DisplayDialog("烘焙为 AnimationClip", "请先指定 Spline 路径和烘焙目标。", "确定");
                return;
            }

            int frameRate = Mathf.Clamp(clipAsset.bakeFrameRate, 1, 240);
            float duration = GetEffectiveBakeDuration(clipAsset);
            int frameCount = Mathf.Max(1, Mathf.RoundToInt(duration * frameRate));
            int keyframeStep = Mathf.Max(1, clipAsset.bakeKeyframeStep);

            string folder = NormalizeAssetFolder(string.IsNullOrEmpty(clipAsset.bakeSaveFolder) ? "Assets/Animations/SplineBakes" : clipAsset.bakeSaveFolder.Trim());
            EnsureAssetFolder(folder);

            string baseName = string.IsNullOrEmpty(clipAsset.bakeClipName) ? targetTransform.gameObject.name + "_SplineClipBake" : clipAsset.bakeClipName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SanitizeFileName(baseName) + ".anim");

            AnimationClip output = new AnimationClip();
            output.name = Path.GetFileNameWithoutExtension(path);
            output.frameRate = frameRate;

            List<BakeSample> samples = new List<BakeSample>();
            Transform parent = targetTransform.parent;
            Quaternion originalWorldRotation = targetTransform.rotation;
            VFXSplineAnimationBehaviour template = clipAsset.template;

            for (int i = 0; i <= frameCount; i++)
            {
                bool shouldKey = (i % keyframeStep == 0);
                if (clipAsset.bakeAlwaysKeyStartAndEnd && (i == 0 || i == frameCount))
                    shouldKey = true;

                if (shouldKey)
                    samples.Add(EvaluateBakeSample(template, spline, targetTransform, parent, originalWorldRotation, i, frameCount, frameRate));
            }

            if (samples.Count == 0)
                samples.Add(EvaluateBakeSample(template, spline, targetTransform, parent, originalWorldRotation, 0, frameCount, frameRate));

            if (clipAsset.bakeAlwaysKeyStartAndEnd)
            {
                if (samples[0].time > 0f)
                    samples.Insert(0, EvaluateBakeSample(template, spline, targetTransform, parent, originalWorldRotation, 0, frameCount, frameRate));

                float endTime = frameCount / (float)frameRate;
                if (Mathf.Abs(samples[samples.Count - 1].time - endTime) > 0.0001f)
                    samples.Add(EvaluateBakeSample(template, spline, targetTransform, parent, originalWorldRotation, frameCount, frameCount, frameRate));
            }

            int rawKeyCount = samples.Count;
            if (clipAsset.bakeOptimizeCurves && samples.Count > 2)
            {
                samples = OptimizeSamples(samples, clipAsset.bakePosition, clipAsset.bakeRotation, Mathf.Max(0f, clipAsset.bakePositionTolerance), Mathf.Max(0f, clipAsset.bakeRotationTolerance));
            }

            WriteTransformCurves(output, samples, clipAsset.bakePosition, clipAsset.bakeRotation);

            AssetDatabase.CreateAsset(output, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (clipAsset.bakeAddAnimatorIfMissing && targetTransform.GetComponent<Animator>() == null)
                Undo.AddComponent<Animator>(targetTransform.gameObject);

            Selection.activeObject = output;
            EditorGUIUtility.PingObject(output);

            Debug.Log("[VFX Timeline Spline Tool] VFX Spline Animation Clip 烘焙完成\n路径：" + path + "\n时长：" + duration.ToString("F3") + " 秒\n帧率：" + frameRate + "\n关键帧间隔：" + keyframeStep + "\n优化前关键帧数：" + rawKeyCount + "\n最终关键帧数：" + samples.Count, output);
        }

        private static int EstimateKeyCount(int frameCount, int keyframeStep, bool alwaysKeyStartAndEnd)
        {
            int count = 0;
            for (int i = 0; i <= frameCount; i++)
            {
                bool shouldKey = (i % keyframeStep == 0);
                if (alwaysKeyStartAndEnd && (i == 0 || i == frameCount))
                    shouldKey = true;

                if (shouldKey)
                    count++;
            }

            return Mathf.Max(1, count);
        }

        private static BakeSample EvaluateBakeSample(VFXSplineAnimationBehaviour template, VFXSimpleSpline spline, Transform targetTransform, Transform parent, Quaternion originalWorldRotation, int frameIndex, int frameCount, int frameRate)
        {
            float time = frameIndex / (float)frameRate;
            float duration = frameCount / (float)frameRate;
            float normalizedTime;

            if (template.loopPlayback)
            {
                float cycle = Mathf.Max(0.01f, template.secondsPerLoop);
                normalizedTime = Mathf.Repeat(time / cycle, 1f);
            }
            else
            {
                normalizedTime = duration > 0.00001f ? Mathf.Clamp01(time / duration) : 0f;
            }

            float curveT = template.useSpeedCurve && template.speedCurve != null
                ? Mathf.Clamp01(template.speedCurve.Evaluate(normalizedTime))
                : normalizedTime;

            float progress = Mathf.LerpUnclamped(template.startProgress, template.endProgress, curveT);
            if (template.reverse)
                progress = 1f - progress;

            progress = template.loop || template.loopPlayback ? Mathf.Repeat(progress, 1f) : Mathf.Clamp01(progress);

            Vector3 worldPosition = spline.GetPoint(progress, template.useDistanceBasedProgress) + template.positionOffset;
            Quaternion worldRotation = originalWorldRotation;

            if (template.followRotation && template.rotationMode != VFXSplineRotationMode.None)
            {
                Vector3 tangent = spline.GetTangent(progress, template.useDistanceBasedProgress);
                if (tangent.sqrMagnitude < 0.000001f)
                    tangent = template.fallbackForward.sqrMagnitude > 0.000001f ? template.fallbackForward.normalized : Vector3.forward;

                Vector3 normal = spline.GetNormal(progress, template.useDistanceBasedProgress);
                VFXSplineAnimator.ApplySplineRotationLock(spline, template.ignoreSplineTransformRotation, ref tangent, ref normal);
                worldRotation = BuildRotation(template, targetTransform, tangent, normal) * Quaternion.Euler(template.rotationOffsetEuler);
            }

            BakeSample sample = new BakeSample();
            sample.time = time;
            sample.localPosition = parent != null ? parent.InverseTransformPoint(worldPosition) : worldPosition;
            sample.localRotation = parent != null ? Quaternion.Inverse(parent.rotation) * worldRotation : worldRotation;
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

        private static Quaternion BuildRotation(VFXSplineAnimationBehaviour template, Transform targetTransform, Vector3 tangent, Vector3 up)
        {
            Vector3 forward = tangent.normalized;

            if (VFXSplineAnimator.IsPlanarRotationMode(template.rotationMode))
            {
                Vector3 reference = template.fallbackForward.sqrMagnitude > 0.000001f ? template.fallbackForward : (targetTransform != null ? targetTransform.forward : Vector3.forward);
                forward = VFXSplineAnimator.ResolvePlanarForward(tangent, reference, template.rotationMode == VFXSplineRotationMode.PlanarStable);
            }

            if (up.sqrMagnitude < 0.000001f)
                up = Vector3.up;

            Quaternion look = Quaternion.LookRotation(forward.normalized, up.normalized);
            return look * Quaternion.Inverse(VFXSplineAnimator.AxisToRotation(template.forwardAxis));
        }

        private static void WriteTransformCurves(AnimationClip clip, List<BakeSample> samples, bool bakePosition, bool bakeRotation)
        {
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
            }

            if (bakePosition)
            {
                SetTransformCurve(clip, "m_LocalPosition.x", px);
                SetTransformCurve(clip, "m_LocalPosition.y", py);
                SetTransformCurve(clip, "m_LocalPosition.z", pz);
            }

            if (bakeRotation)
            {
                SetTransformCurve(clip, "m_LocalRotation.x", rx);
                SetTransformCurve(clip, "m_LocalRotation.y", ry);
                SetTransformCurve(clip, "m_LocalRotation.z", rz);
                SetTransformCurve(clip, "m_LocalRotation.w", rw);
                clip.EnsureQuaternionContinuity();
            }
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

            return string.IsNullOrEmpty(name) ? "SplineClipBake" : name;
        }
    }
}
#endif

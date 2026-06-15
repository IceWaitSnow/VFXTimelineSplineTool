#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
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
            EditorGUILayout.HelpBox("推荐流程：Spline 负责路径，Unity 原生 AnimationClip / Timeline Animation Track 给 Progress 打关键帧，最后 Bake 成普通 Transform AnimationClip。v" + VFXSplineToolVersion.Version + " 加强了 Timeline Infinite Clip 自动识别和 Progress 曲线读取。", MessageType.Info);

            serializedObject.Update();
            DrawProperty("spline", "Spline");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Animation Track 可K参数", EditorStyles.boldLabel);
            DrawProperty("progress", "Progress");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);
            DrawProperty("reverse", "Reverse");
            DrawProperty("loop", "Loop");
            DrawProperty("useDistanceBasedProgress", "Use Distance Based Progress");
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
            DrawProperty("showCurrentProgressPoint", "Show Current Progress Point");
            DrawProperty("previewColor", "Preview Color");


            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progress 快捷设置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set 0%")) SetProgress(animator, 0f);
                if (GUILayout.Button("Set 25%")) SetProgress(animator, 0.25f);
                if (GUILayout.Button("Set 50%")) SetProgress(animator, 0.5f);
                if (GUILayout.Button("Set 75%")) SetProgress(animator, 0.75f);
                if (GUILayout.Button("Set 100%")) SetProgress(animator, 1f);
            }
            if (GUILayout.Button("Apply Current Progress"))
            {
                Undo.RecordObject(animator.transform, "Apply Current Progress");
                animator.ApplyCurrentProgress(true);
                EditorUtility.SetDirty(animator);
            }
            if (GUILayout.Button("Set Progress To Start")) SetProgress(animator, 0f);
            if (GUILayout.Button("Set Progress To End")) SetProgress(animator, 1f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Animator If Missing"))
            {
                AddAnimatorIfMissing(animator.gameObject);
            }
            if (GUILayout.Button("Create Default Progress AnimationClip"))
            {
                CreateDefaultProgressClip(animator);
            }
            if (GUILayout.Button("Setup Animation Track Mode（推荐）"))
            {
                AddAnimatorIfMissing(animator.gameObject);
                CreateDefaultProgressClip(animator);
                SetProgress(animator, 0f);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Bake To AnimationClip / 烘焙", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把当前 Spline 驱动结果烘焙成 Unity 原生 Transform AnimationClip。Bake Progress Source 用来决定烘焙时 Progress 从哪里来；Keyframe Step / Optimize Curves 用来减少关键帧。", MessageType.Info);

            serializedObject.Update();
            DrawProperty("bakeFrameRate", "Frame Rate");
            DrawProperty("bakeDuration", "Duration");
            DrawProperty("bakePosition", "Bake Position");
            DrawProperty("bakeRotation", "Bake Rotation");
            DrawProperty("bakeProgressSource", "Bake Progress Source");
            SerializedProperty progressSourceProp = serializedObject.FindProperty("bakeProgressSource");
            VFXSplineBakeProgressSource progressSource = (VFXSplineBakeProgressSource)progressSourceProp.enumValueIndex;
            if (progressSource == VFXSplineBakeProgressSource.BakeProgressCurve)
            {
                DrawProperty("bakeProgressCurve", "Bake Progress Curve");
                EditorGUILayout.HelpBox("使用 Bake Progress Curve 控制烘焙节奏。适合没有单独 K Progress、只想快速做缓入缓出/停顿/回弹的情况。", MessageType.None);
            }
            else if (progressSource == VFXSplineBakeProgressSource.CurrentAnimatorProgress)
            {
                EditorGUILayout.HelpBox("使用当前 Inspector 里的 Progress 值烘焙静态姿态。通常用于烘焙路径上的某个固定点，不适合生成完整飞行动画。", MessageType.None);
            }
            else if (progressSource == VFXSplineBakeProgressSource.ExistingAnimationClipProgressCurve)
            {
                DrawProperty("bakeSourceProgressClip", "Source Progress Clip");
                DrawProperty("bakeSourceClipUseNormalizedTime", "Use Normalized Source Time");
                EditorGUILayout.HelpBox("读取已有 AnimationClip 里的 VFXSplineAnimator.progress 曲线。适合你已经用 Animation Track 精细 K 好 Progress 后，再烘焙成普通 Transform AnimationClip。", MessageType.Info);
            }
            else if (progressSource == VFXSplineBakeProgressSource.TimelineBoundAnimationTrack)
            {
                DrawProperty("bakePlayableDirector", "Playable Director");
                DrawProperty("bakeUseTimelineClipDuration", "Use Timeline Clip Duration");
                TimelineProgressSource found;
                string message;
                bool ok = TryFindTimelineProgressSource(animator, out found, out message);
                EditorGUILayout.HelpBox(ok ? ("已找到 Timeline Progress Clip：" + found.clip.name + "\nTrack：" + found.trackName + "\nClip Range：" + found.timelineStart.ToString("F2") + "s - " + found.timelineEnd.ToString("F2") + "s") : message, ok ? MessageType.Info : MessageType.Warning);
                using (new EditorGUI.DisabledScope(!ok))
                {
                    if (GUILayout.Button("Use Found Timeline Clip Duration"))
                    {
                        Undo.RecordObject(animator, "Use Timeline Clip Duration");
                        animator.bakeDuration = Mathf.Max(0.01f, (float)found.timelineDuration);
                        EditorUtility.SetDirty(animator);
                    }
                }
                EditorGUILayout.HelpBox("此模式会自动读取绑定当前物体的 Timeline Animation Track，不需要手动从 Project 里找 AnimationClip。", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Linear 0→1：烘焙时 Progress 会在 Duration 内从 0 匀速走到 1。", MessageType.None);
            }
            DrawProperty("bakeSaveFolder", "Save Folder");
            DrawProperty("bakeClipName", "Clip Name");
            DrawProperty("bakeAddAnimatorIfMissing", "Add Animator If Missing");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Bake Simplify / 关键帧简化", EditorStyles.boldLabel);
            DrawProperty("bakeKeyframeStep", "Keyframe Step");
            DrawProperty("bakeAlwaysKeyStartAndEnd", "Always Key Start And End");
            DrawProperty("bakeOptimizeCurves", "Optimize Curves");
            if (serializedObject.FindProperty("bakeOptimizeCurves").boolValue)
            {
                DrawProperty("bakePositionTolerance", "Position Tolerance");
                DrawProperty("bakeRotationTolerance", "Rotation Tolerance");
            }
            serializedObject.ApplyModifiedProperties();

            using (new EditorGUI.DisabledScope(animator.spline == null || (!animator.bakePosition && !animator.bakeRotation)))
            {
                if (GUILayout.Button("Bake To AnimationClip"))
                {
                    BakeToAnimationClip(animator);
                }
            }
        }

        private void DrawProperty(string name, string label)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
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
            if (go == null) return;
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
            {
                AssetDatabase.CreateFolder("Assets", "Animations");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Spline_Progress_Clip.anim");
            AnimationClip clip = new AnimationClip();
            clip.frameRate = 60f;

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
            public double timelineStart;
            public double timelineEnd;
            public double timelineDuration;
            public double clipIn;
            public string trackName;
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
                EditorUtility.DisplayDialog("Bake To AnimationClip", "请先指定 Spline。", "OK");
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
                    EditorUtility.DisplayDialog("Bake From Timeline", timelineMessage, "OK");
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

            for (int i = 0; i <= frameCount; i++)
            {
                bool shouldKey = (i % keyframeStep == 0);
                if (animator.bakeAlwaysKeyStartAndEnd && (i == 0 || i == frameCount))
                    shouldKey = true;
                if (!shouldKey)
                    continue;

                samples.Add(EvaluateBakeSample(animator, parent, tr.rotation, i, frameCount, frameRate, hasTimelineSource, timelineSource));
            }

            if (samples.Count == 0)
                samples.Add(EvaluateBakeSample(animator, parent, tr.rotation, 0, frameCount, frameRate, hasTimelineSource, timelineSource));

            if (animator.bakeAlwaysKeyStartAndEnd)
            {
                if (samples[0].time > 0f)
                    samples.Insert(0, EvaluateBakeSample(animator, parent, tr.rotation, 0, frameCount, frameRate, hasTimelineSource, timelineSource));
                float endTime = frameCount / (float)frameRate;
                if (Mathf.Abs(samples[samples.Count - 1].time - endTime) > 0.0001f)
                    samples.Add(EvaluateBakeSample(animator, parent, tr.rotation, frameCount, frameCount, frameRate, hasTimelineSource, timelineSource));
            }

            if (animator.bakeOptimizeCurves && samples.Count > 2)
            {
                float positionTolerance = Mathf.Max(0f, animator.bakePositionTolerance);
                float rotationTolerance = Mathf.Max(0f, animator.bakeRotationTolerance);
                samples = OptimizeSamples(samples, animator.bakePosition, animator.bakeRotation, positionTolerance, rotationTolerance);
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
            Debug.Log("[VFX Timeline Spline Tool] Bake To AnimationClip 完成: " + path + " | Keyframes: " + samples.Count + " | Step: " + keyframeStep + " | ProgressSource: " + animator.bakeProgressSource, clip);
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
                    return hasTimelineSource ? EvaluateProgressFromTimelineSource(timelineSource, bakeTime, normalizedTime) : normalizedTime;

                case VFXSplineBakeProgressSource.Linear01:
                default:
                    return normalizedTime;
            }
        }

        private static float EvaluateProgressFromTimelineSource(TimelineProgressSource source, float bakeTime, float normalizedTime)
        {
            if (source.clip == null)
                return normalizedTime;

            AnimationCurve progressCurve = FindProgressCurve(source.clip);
            if (progressCurve == null)
                return normalizedTime;

            float sourceLength = Mathf.Max(0.0001f, source.clip.length);
            float sourceTime = source.isInfiniteClip ? bakeTime : Mathf.Clamp01(normalizedTime) * sourceLength + (float)source.clipIn;
            sourceTime = Mathf.Clamp(sourceTime, 0f, sourceLength);
            return progressCurve.Evaluate(sourceTime);
        }

        private static bool TryFindTimelineProgressSource(VFXSplineAnimator animator, out TimelineProgressSource result, out string message)
        {
            result = default;
            message = "没有找到绑定到当前物体的 Timeline Animation Track，或者 Track 上的 AnimationClip 没有 VFXSplineAnimator.progress 曲线。";

            if (animator == null)
                return false;

            PlayableDirector[] directors;
            if (animator.bakePlayableDirector != null)
            {
                directors = new PlayableDirector[] { animator.bakePlayableDirector };
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
                if (director == null) continue;

                TimelineAsset timeline = director.playableAsset as TimelineAsset;
                if (timeline == null) continue;

                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    AnimationTrack animationTrack = track as AnimationTrack;
                    if (animationTrack == null) continue;

                    Object binding = director.GetGenericBinding(animationTrack);
                    if (!IsTimelineBindingMatch(binding, targetAnimator, targetGameObject, targetTransform))
                        continue;

                    if (!animationTrack.inClipMode && animationTrack.infiniteClip != null)
                    {
                        AnimationCurve infiniteProgressCurve = FindProgressCurve(animationTrack.infiniteClip);
                        if (infiniteProgressCurve != null)
                        {
                            float clipLength = Mathf.Max(0.01f, animationTrack.infiniteClip.length);

                            result.director = director;
                            result.clip = animationTrack.infiniteClip;
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

                    foreach (TimelineClip timelineClip in animationTrack.GetClips())
                    {
                        AnimationPlayableAsset playableAsset = timelineClip.asset as AnimationPlayableAsset;
                        if (playableAsset == null || playableAsset.clip == null)
                            continue;

                        AnimationCurve progressCurve = FindProgressCurve(playableAsset.clip);
                        if (progressCurve == null)
                            continue;

                        result.director = director;
                        result.clip = playableAsset.clip;
                        result.timelineStart = timelineClip.start;
                        result.timelineDuration = timelineClip.duration;
                        result.timelineEnd = timelineClip.end;
                        result.clipIn = timelineClip.clipIn;
                        result.trackName = animationTrack.name;
                        result.isInfiniteClip = false;
                        message = "已找到 Timeline Progress Clip。";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsTimelineBindingMatch(Object binding, Animator targetAnimator, GameObject targetGameObject, Transform targetTransform)
        {
            if (binding == null) return false;
            if (targetAnimator != null && binding == targetAnimator) return true;
            if (binding == targetGameObject) return true;
            if (binding == targetTransform) return true;

            GameObject boundGameObject = null;
            if (binding is Component component) boundGameObject = component.gameObject;
            else if (binding is GameObject gameObject) boundGameObject = gameObject;

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
            if (mag < 0.000001f) return Quaternion.identity;
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
            if (animator == null || animator.spline == null || !animator.showCurrentProgressPoint) return;

            float p = animator.EvaluateProgressValue(animator.progress);
            Vector3 pos = animator.spline.GetPoint(p, animator.useDistanceBasedProgress);
            Vector3 tangent = animator.spline.GetTangent(p, animator.useDistanceBasedProgress);

            Handles.color = animator.previewColor;
            float size = HandleUtility.GetHandleSize(pos) * 0.18f;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
            Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(tangent, Vector3.up), size * 2.5f, EventType.Repaint);

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = animator.previewColor;
            Handles.Label(pos + Vector3.up * size * 2.2f, "Progress: " + animator.progress.ToString("F2"), style);
        }
    }
}
#endif

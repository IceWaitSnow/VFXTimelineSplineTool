#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    /// <summary>
    /// VFXSplineAnimator 烘焙流程使用的 Timeline 来源报告窗口。
    /// v2.6.5：提供显式 Refresh 按钮，并显示完整的混合 Clip 范围。
    /// </summary>
    public class VFXSplineTimelineSourceReporterWindow : EditorWindow
    {
        private VFXSplineAnimator animator;
        private PlayableDirector director;
        private Vector2 scroll;
        private string report = "尚未扫描。";
        private bool lastScanOk;
        private double foundStart;
        private double foundEnd;
        private double foundDuration;
        private int foundClipCount;
        private string foundTrackName;
        private string foundDirectorName;
        private bool foundInfiniteClip;

        private struct ProgressClipInfo
        {
            public string name;
            public double start;
            public double end;
            public double duration;
            public double clipIn;
            public double timeScale;
            public bool infinite;
        }

        private readonly List<ProgressClipInfo> clips = new List<ProgressClipInfo>();

        [MenuItem("Tools/VFX Timeline Spline/Timeline Source Reporter")]
        public static void Open()
        {
            VFXSplineTimelineSourceReporterWindow window = GetWindow<VFXSplineTimelineSourceReporterWindow>("Spline Timeline Source");
            window.minSize = new Vector2(420f, 360f);
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
            RefreshFromSelection();
            Repaint();
        }

        private void RefreshFromSelection()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null)
                return;

            VFXSplineAnimator selected = go.GetComponent<VFXSplineAnimator>();
            if (selected != null)
            {
                animator = selected;
                if (director == null)
                    director = selected.bakePlayableDirector;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Timeline 来源报告 v" + VFXSplineToolVersion.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于检查 Timeline Bound Animation Track 烘焙源。它会扫描绑定当前物体的 Animation Track，并按整条 Track 上所有带 progress 曲线的 Clip 计算总范围。", MessageType.Info);

            animator = (VFXSplineAnimator)EditorGUILayout.ObjectField("Spline Animator", animator, typeof(VFXSplineAnimator), true);
            director = (PlayableDirector)EditorGUILayout.ObjectField("Playable Director", director, typeof(PlayableDirector), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新 Timeline 来源"))
                    RefreshReport();

                using (new EditorGUI.DisabledScope(!lastScanOk || animator == null))
                {
                    if (GUILayout.Button("使用找到的时长"))
                    {
                        Undo.RecordObject(animator, "Use Found Timeline Duration");
                        animator.bakeDuration = Mathf.Max(0.01f, (float)foundDuration);
                        if (director != null)
                            animator.bakePlayableDirector = director;
                        EditorUtility.SetDirty(animator);
                    }
                }
            }

            EditorGUILayout.Space(8);
            DrawSummary();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("报告", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.MinHeight(160));
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            if (!lastScanOk)
            {
                EditorGUILayout.HelpBox(report, MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "已找到 Timeline Progress Source\n" +
                "Director：" + foundDirectorName + "\n" +
                "Track：" + foundTrackName + "\n" +
                "Type：" + (foundInfiniteClip ? "Infinite Clip" : "Timeline Clips") + "\n" +
                "Clip Count：" + foundClipCount + "\n" +
                "Timeline Range：" + foundStart.ToString("F2") + "s - " + foundEnd.ToString("F2") + "s\n" +
                "Duration：" + foundDuration.ToString("F3") + "s",
                MessageType.Info);
        }

        private void RefreshReport()
        {
            clips.Clear();
            lastScanOk = false;
            foundStart = 0.0;
            foundEnd = 0.0;
            foundDuration = 0.0;
            foundClipCount = 0;
            foundTrackName = "None";
            foundDirectorName = "None";
            foundInfiniteClip = false;

            if (animator == null)
            {
                report = "没有指定 VFXSplineAnimator。";
                return;
            }

            string message;
            if (!TryFindTimelineSource(animator, director, out message))
            {
                report = message;
                return;
            }

            BuildReportText();
        }

        private bool TryFindTimelineSource(VFXSplineAnimator target, PlayableDirector preferredDirector, out string message)
        {
            message = "没有找到绑定到当前物体的 Timeline Animation Track，或者 Clip 中没有 VFXSplineAnimator.progress 曲线。";

            List<PlayableDirector> directors = new List<PlayableDirector>();
            if (preferredDirector != null)
            {
                directors.Add(preferredDirector);
            }
            else
            {
#if UNITY_2023_1_OR_NEWER
                directors.AddRange(Object.FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None));
#else
                directors.AddRange(Object.FindObjectsOfType<PlayableDirector>(true));
#endif
            }

            Animator targetAnimator = target.GetComponent<Animator>();
            GameObject targetGameObject = target.gameObject;
            Transform targetTransform = target.transform;

            for (int d = 0; d < directors.Count; d++)
            {
                PlayableDirector pd = directors[d];
                if (pd == null)
                    continue;

                TimelineAsset timeline = pd.playableAsset as TimelineAsset;
                if (timeline == null)
                    continue;

                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    AnimationTrack animationTrack = track as AnimationTrack;
                    if (animationTrack == null)
                        continue;

                    Object binding = pd.GetGenericBinding(animationTrack);
                    if (!IsBindingMatch(binding, targetAnimator, targetGameObject, targetTransform))
                        continue;

                    if (!animationTrack.inClipMode && animationTrack.infiniteClip != null)
                    {
                        AnimationCurve curve = FindProgressCurve(animationTrack.infiniteClip);
                        if (curve != null)
                        {
                            float len = Mathf.Max(0.01f, animationTrack.infiniteClip.length);
                            foundDirectorName = pd.name;
                            foundTrackName = animationTrack.name + " (Infinite Clip)";
                            foundStart = 0.0;
                            foundEnd = len;
                            foundDuration = len;
                            foundClipCount = 1;
                            foundInfiniteClip = true;
                            lastScanOk = true;
                            director = pd;
                            clips.Add(new ProgressClipInfo(){ name = animationTrack.infiniteClip.name, start = 0.0, end = len, duration = len, clipIn = 0.0, timeScale = 1.0, infinite = true });
                            return true;
                        }
                    }

                    List<ProgressClipInfo> localClips = new List<ProgressClipInfo>();
                    foreach (TimelineClip timelineClip in animationTrack.GetClips())
                    {
                        AnimationPlayableAsset playableAsset = timelineClip.asset as AnimationPlayableAsset;
                        if (playableAsset == null || playableAsset.clip == null)
                            continue;

                        if (FindProgressCurve(playableAsset.clip) == null)
                            continue;

                        localClips.Add(new ProgressClipInfo(){ name = playableAsset.clip.name, start = timelineClip.start, end = timelineClip.end, duration = timelineClip.duration, clipIn = timelineClip.clipIn, timeScale = timelineClip.timeScale, infinite = false });
                    }

                    if (localClips.Count == 0)
                        continue;

                    localClips.Sort((a, b) => a.start.CompareTo(b.start));
                    double start = localClips[0].start;
                    double end = localClips[0].end;
                    for (int i = 1; i < localClips.Count; i++)
                    {
                        start = System.Math.Min(start, localClips[i].start);
                        end = System.Math.Max(end, localClips[i].end);
                    }

                    clips.AddRange(localClips);
                    foundDirectorName = pd.name;
                    foundTrackName = animationTrack.name;
                    foundStart = start;
                    foundEnd = end;
                    foundDuration = System.Math.Max(0.01, end - start);
                    foundClipCount = localClips.Count;
                    foundInfiniteClip = false;
                    lastScanOk = true;
                    director = pd;
                    return true;
                }
            }

            return false;
        }

        private void BuildReportText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Timeline Progress Source Report");
            sb.AppendLine("Director: " + foundDirectorName);
            sb.AppendLine("Track: " + foundTrackName);
            sb.AppendLine("Type: " + (foundInfiniteClip ? "Infinite Clip" : "Timeline Clips"));
            sb.AppendLine("Clip Count: " + foundClipCount);
            sb.AppendLine("Timeline Range: " + foundStart.ToString("F3") + "s - " + foundEnd.ToString("F3") + "s");
            sb.AppendLine("Duration: " + foundDuration.ToString("F3") + "s");
            sb.AppendLine();
            sb.AppendLine("Clips:");
            for (int i = 0; i < clips.Count; i++)
            {
                ProgressClipInfo c = clips[i];
                sb.AppendLine("  [" + i + "] " + c.name + " | Start: " + c.start.ToString("F3") + " | End: " + c.end.ToString("F3") + " | Duration: " + c.duration.ToString("F3") + " | ClipIn: " + c.clipIn.ToString("F3") + " | Speed: " + c.timeScale.ToString("F3"));
            }
            report = sb.ToString();
        }

        private static bool IsBindingMatch(Object binding, Animator targetAnimator, GameObject targetGameObject, Transform targetTransform)
        {
            if (binding == null) return false;
            if (targetAnimator != null && binding == targetAnimator) return true;
            if (binding == targetGameObject) return true;
            if (binding == targetTransform) return true;

            Component component = binding as Component;
            if (component != null && component.gameObject == targetGameObject) return true;

            GameObject gameObject = binding as GameObject;
            return gameObject != null && gameObject == targetGameObject;
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
    }
}
#endif

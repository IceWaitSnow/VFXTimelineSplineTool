using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // 仅用于兼容旧版 Path Events 数据。v2.0 正式工作流不再推荐事件系统。

namespace VFXTimelineSplineTool
{
    public enum VFXSplineEventTriggerType
    {
        None,
        Particle,
        Audio,
        SetActive,
        SendMessage,
        UnityEvent
    }

    [Serializable]
    public class VFXSplineEvent
    {
        public string eventName = "Event";
        [Range(0f, 1f)] public float progress = 0.5f;
        public bool enabled = true;
        public VFXSplineEventTriggerType triggerType = VFXSplineEventTriggerType.Particle;
        public GameObject targetObject;
        public ParticleSystem particleSystem;
        public AudioSource audioSource;
        public bool setActiveValue = true;
        public string sendMessageName = "OnSplineEvent";
        [HideInInspector] public Color eventColor = new Color(1f, 0.25f, 0.1f, 1f); // v2.0 起隐藏：事件系统保留为旧工程兼容。

        [Header("Spawn Position")]
        public bool placeTargetAtEventPoint = true;
        public bool placeParticleAtEventPoint = true;
        public bool placeAudioAtEventPoint = true;
        public bool alignTargetToPath = false;
        public Vector3 eventPositionOffset = Vector3.zero;
        public bool restartParticleOnTrigger = true;

        public UnityEvent unityEvent = new UnityEvent();

        [NonSerialized] public bool fired;

        public void Trigger(Component sender, VFXSimpleSpline spline, float evaluatedProgress, bool distanceBased)
        {
            if (!enabled) return;

            evaluatedProgress = Mathf.Clamp01(evaluatedProgress);
            Vector3 eventPosition = Vector3.zero;
            Quaternion eventRotation = Quaternion.identity;
            bool hasSplinePosition = spline != null;

            if (hasSplinePosition)
            {
                eventPosition = spline.GetPoint(evaluatedProgress, distanceBased) + eventPositionOffset;
                Vector3 tangent = spline.GetTangent(evaluatedProgress, distanceBased);
                if (tangent.sqrMagnitude > 0.000001f)
                    eventRotation = Quaternion.LookRotation(tangent, Vector3.up);
            }

            switch (triggerType)
            {
                case VFXSplineEventTriggerType.Particle:
                {
                    ParticleSystem ps = particleSystem;
                    if (ps == null && targetObject != null)
                        ps = targetObject.GetComponent<ParticleSystem>();
                    if (ps == null && targetObject != null)
                        ps = targetObject.GetComponentInChildren<ParticleSystem>(true);

                    if (ps != null)
                    {
                        if (hasSplinePosition && placeParticleAtEventPoint)
                            ApplyTransformAtEvent(ps.transform, eventPosition, eventRotation, alignTargetToPath);

                        if (restartParticleOnTrigger)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                            ps.Clear(true);
                        }
                        ps.Play(true);
                    }
                    break;
                }

                case VFXSplineEventTriggerType.Audio:
                {
                    AudioSource audio = audioSource;
                    if (audio == null && targetObject != null)
                        audio = targetObject.GetComponent<AudioSource>();
                    if (audio == null && targetObject != null)
                        audio = targetObject.GetComponentInChildren<AudioSource>(true);

                    if (audio != null)
                    {
                        if (hasSplinePosition && placeAudioAtEventPoint)
                            ApplyTransformAtEvent(audio.transform, eventPosition, eventRotation, alignTargetToPath);
                        audio.Play();
                    }
                    break;
                }

                case VFXSplineEventTriggerType.SetActive:
                    if (targetObject != null)
                    {
                        if (hasSplinePosition && placeTargetAtEventPoint)
                            ApplyTransformAtEvent(targetObject.transform, eventPosition, eventRotation, alignTargetToPath);
                        targetObject.SetActive(setActiveValue);
                    }
                    break;

                case VFXSplineEventTriggerType.SendMessage:
                    if (targetObject != null && !string.IsNullOrEmpty(sendMessageName))
                    {
                        if (hasSplinePosition && placeTargetAtEventPoint)
                            ApplyTransformAtEvent(targetObject.transform, eventPosition, eventRotation, alignTargetToPath);
                        targetObject.SendMessage(sendMessageName, sender, SendMessageOptions.DontRequireReceiver);
                    }
                    break;

                case VFXSplineEventTriggerType.UnityEvent:
                    if (unityEvent != null) unityEvent.Invoke();
                    break;
            }
        }

        private static void ApplyTransformAtEvent(Transform target, Vector3 position, Quaternion rotation, bool align)
        {
            if (target == null) return;
            target.position = position;
            if (align) target.rotation = rotation;
        }
    }

    [ExecuteAlways]
    public class VFXSimpleSpline : MonoBehaviour
    {
        public const string ToolVersion = "2.6";

        [Header("Path Settings")]
        public Color pathColor = new Color(1f, 0.68f, 0.02f, 1f);
        public Color progressMarkColor = new Color(0.1f, 0.72f, 1f, 1f);
        [HideInInspector] public Color eventColor = new Color(1f, 0.25f, 0.1f, 1f); // v2.0 起隐藏：事件系统保留为旧工程兼容。
        [Min(1f)] public float lineWidth = 3f;
        [Min(0.01f)] public float pointSize = 0.15f;
        [Range(8, 256)] public int resolution = 48;

        [Header("Display")]
        public bool alwaysShowPathInSceneView = true;
        public bool showPointLabels = true;
        public bool showAllPointHandles = false;
        [HideInInspector] public int selectedPointIndex = 0;
        public bool showProgressMarks = true;
        [Range(1, 20)] public int progressMarkCount = 4;
        public bool progressMarksUseDistance = true;
        public bool showDirectionArrows = true;
        [Range(1, 64)] public int arrowCount = 8;
        [Min(0.01f)] public float arrowSize = 0.35f;
        [HideInInspector] public bool showEvents = false; // v2.0 起隐藏：事件系统不作为正式教程流程。
        [HideInInspector] public bool eventMarksUseDistance = true;

        [Header("Dynamic Start / End Binding")]
        [Tooltip("开启后，路径第一个点会跟随 Start Transform，最后一个点会跟随 End Transform。适合飞向目标、奖励飞行、吸入路径等场景。")]
        public bool enableDynamicStartEndBinding = false;
        [Tooltip("动态起点。为空时继续使用 Local Points 的第一个点。") ]
        public Transform dynamicStartTransform;
        [Tooltip("动态终点。为空时继续使用 Local Points 的最后一个点。") ]
        public Transform dynamicEndTransform;
        [Tooltip("编辑模式下实时刷新动态端点。通常保持开启即可。") ]
        public bool dynamicUpdateInEditMode = true;
        [Tooltip("Scene 视图中显示动态绑定端点的标签。") ]
        public bool showDynamicBindingLabels = true;

        [Header("Distance Based Progress")]
        [Range(32, 2048)] public int distanceSampleResolution = 256;

        [Header("Points - Local Space")]
        public List<Vector3> localPoints = new List<Vector3>()
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 1f, 0f),
            new Vector3(4f, 1f, 0f),
            new Vector3(6f, 0f, 0f)
        };

        [HideInInspector] public List<VFXSplineEvent> events = new List<VFXSplineEvent>(); // 保留旧版数据兼容，Inspector 默认不显示。

        [NonSerialized] private bool cacheDirty = true;
        [NonSerialized] private readonly List<float> cachedT = new List<float>();
        [NonSerialized] private readonly List<float> cachedDistance = new List<float>();
        [NonSerialized] private float cachedLength;
        [NonSerialized] private Vector3 lastDynamicStartWorld;
        [NonSerialized] private Vector3 lastDynamicEndWorld;
        [NonSerialized] private bool hasLastDynamicWorld;

        public float ApproxLength
        {
            get
            {
                RebuildDistanceCacheIfNeeded();
                return cachedLength;
            }
        }

        private void OnValidate()
        {
            if (localPoints == null) localPoints = new List<Vector3>();
            if (localPoints.Count < 2)
            {
                while (localPoints.Count < 2)
                    localPoints.Add(Vector3.right * localPoints.Count);
            }

            resolution = Mathf.Clamp(resolution, 8, 256);
            distanceSampleResolution = Mathf.Clamp(distanceSampleResolution, 32, 2048);
            progressMarkCount = Mathf.Clamp(progressMarkCount, 1, 20);
            arrowCount = Mathf.Clamp(arrowCount, 1, 64);
            MarkDistanceCacheDirty();
        }

        private void Reset()
        {
            ResetPath();
        }

        public void MarkDistanceCacheDirty()
        {
            cacheDirty = true;
        }

        public void RebuildDistanceCacheIfNeeded()
        {
            RefreshDynamicBindingCacheState();
            if (cacheDirty) RebuildDistanceCache();
        }

        public void RebuildDistanceCache()
        {
            cachedT.Clear();
            cachedDistance.Clear();
            cachedLength = 0f;

            int samples = Mathf.Max(2, distanceSampleResolution);
            Vector3 prev = GetPointByRawProgress(0f);
            cachedT.Add(0f);
            cachedDistance.Add(0f);

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 p = GetPointByRawProgress(t);
                cachedLength += Vector3.Distance(prev, p);
                cachedT.Add(t);
                cachedDistance.Add(cachedLength);
                prev = p;
            }

            cacheDirty = false;
        }

        public Vector3 GetPoint(float progress, bool distanceBased)
        {
            progress = Mathf.Clamp01(progress);
            return distanceBased ? GetPointByDistanceProgress(progress) : GetPointByRawProgress(progress);
        }

        public Vector3 GetTangent(float progress, bool distanceBased)
        {
            progress = Mathf.Clamp01(progress);
            const float delta = 0.001f;
            float a = Mathf.Clamp01(progress - delta);
            float b = Mathf.Clamp01(progress + delta);
            Vector3 p0 = GetPoint(a, distanceBased);
            Vector3 p1 = GetPoint(b, distanceBased);
            Vector3 tangent = p1 - p0;
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = transform.forward;
            return tangent.normalized;
        }

        public Vector3 GetPointByDistanceProgress(float distanceProgress)
        {
            distanceProgress = Mathf.Clamp01(distanceProgress);
            RebuildDistanceCacheIfNeeded();

            if (cachedDistance.Count < 2 || cachedLength <= 0.00001f)
                return GetPointByRawProgress(distanceProgress);

            float targetDistance = distanceProgress * cachedLength;

            for (int i = 1; i < cachedDistance.Count; i++)
            {
                if (cachedDistance[i] >= targetDistance)
                {
                    float d0 = cachedDistance[i - 1];
                    float d1 = cachedDistance[i];
                    float lerp = Mathf.Approximately(d0, d1) ? 0f : Mathf.InverseLerp(d0, d1, targetDistance);
                    float rawT = Mathf.Lerp(cachedT[i - 1], cachedT[i], lerp);
                    return GetPointByRawProgress(rawT);
                }
            }

            return GetPointByRawProgress(1f);
        }

        public Vector3 GetPointByRawProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            int count = localPoints != null ? localPoints.Count : 0;
            if (count == 0) return transform.position;
            if (count == 1) return transform.TransformPoint(GetEffectiveLocalPoint(0));

            float scaled = progress * (count - 1);
            int p1 = Mathf.FloorToInt(scaled);
            if (p1 >= count - 1) p1 = count - 2;
            int p2 = Mathf.Min(p1 + 1, count - 1);
            int p0 = Mathf.Max(p1 - 1, 0);
            int p3 = Mathf.Min(p2 + 1, count - 1);
            float t = scaled - p1;

            Vector3 a = GetEffectiveLocalPoint(p0);
            Vector3 b = GetEffectiveLocalPoint(p1);
            Vector3 c = GetEffectiveLocalPoint(p2);
            Vector3 d = GetEffectiveLocalPoint(p3);

            Vector3 local = CatmullRom(a, b, c, d, t);
            return transform.TransformPoint(local);
        }

        public Vector3 GetEffectiveLocalPoint(int index)
        {
            if (localPoints == null || localPoints.Count == 0) return Vector3.zero;
            index = Mathf.Clamp(index, 0, localPoints.Count - 1);

            if (enableDynamicStartEndBinding)
            {
                if (index == 0 && dynamicStartTransform != null)
                    return transform.InverseTransformPoint(dynamicStartTransform.position);

                if (index == localPoints.Count - 1 && dynamicEndTransform != null)
                    return transform.InverseTransformPoint(dynamicEndTransform.position);
            }

            return localPoints[index];
        }

        public Vector3 GetEffectiveWorldPoint(int index)
        {
            return transform.TransformPoint(GetEffectiveLocalPoint(index));
        }

        public bool IsPointDynamicallyBound(int index)
        {
            if (!enableDynamicStartEndBinding || localPoints == null || localPoints.Count == 0) return false;
            if (index == 0 && dynamicStartTransform != null) return true;
            if (index == localPoints.Count - 1 && dynamicEndTransform != null) return true;
            return false;
        }

        public Transform GetDynamicBindingTransformForPoint(int index)
        {
            if (!enableDynamicStartEndBinding || localPoints == null || localPoints.Count == 0) return null;
            if (index == 0) return dynamicStartTransform;
            if (index == localPoints.Count - 1) return dynamicEndTransform;
            return null;
        }

        public void ApplyDynamicBindingToLocalPoints()
        {
            if (localPoints == null || localPoints.Count < 2) return;

            if (enableDynamicStartEndBinding)
            {
                if (dynamicStartTransform != null)
                    localPoints[0] = transform.InverseTransformPoint(dynamicStartTransform.position);

                if (dynamicEndTransform != null)
                    localPoints[localPoints.Count - 1] = transform.InverseTransformPoint(dynamicEndTransform.position);
            }

            MarkDistanceCacheDirty();
        }

        private void RefreshDynamicBindingCacheState()
        {
            if (!enableDynamicStartEndBinding)
            {
                hasLastDynamicWorld = false;
                return;
            }

            Vector3 start = dynamicStartTransform != null ? dynamicStartTransform.position : Vector3.positiveInfinity;
            Vector3 end = dynamicEndTransform != null ? dynamicEndTransform.position : Vector3.negativeInfinity;

            if (!hasLastDynamicWorld || start != lastDynamicStartWorld || end != lastDynamicEndWorld)
            {
                cacheDirty = true;
                lastDynamicStartWorld = start;
                lastDynamicEndWorld = end;
                hasLastDynamicWorld = true;
            }
        }

        public Vector3 GetLocalPoint(float progress, bool distanceBased)
        {
            return transform.InverseTransformPoint(GetPoint(progress, distanceBased));
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f *
                   ((2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public void ResetPath()
        {
            localPoints = new List<Vector3>()
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 1f, 0f),
                new Vector3(4f, 1f, 0f),
                new Vector3(6f, 0f, 0f)
            };
            MarkDistanceCacheDirty();
        }

        public void AddPoint()
        {
            if (localPoints == null) localPoints = new List<Vector3>();
            Vector3 newPoint = localPoints.Count > 0 ? localPoints[localPoints.Count - 1] + Vector3.right : Vector3.zero;
            localPoints.Add(newPoint);
            MarkDistanceCacheDirty();
        }

        public void InsertPoint(int index)
        {
            if (localPoints == null) localPoints = new List<Vector3>();
            index = Mathf.Clamp(index, 0, localPoints.Count);
            Vector3 p;
            if (localPoints.Count == 0) p = Vector3.zero;
            else if (index <= 0) p = localPoints[0] - Vector3.right;
            else if (index >= localPoints.Count) p = localPoints[localPoints.Count - 1] + Vector3.right;
            else p = (localPoints[index - 1] + localPoints[index]) * 0.5f;
            localPoints.Insert(index, p);
            MarkDistanceCacheDirty();
        }

        public void RemovePointAt(int index)
        {
            if (localPoints == null || localPoints.Count <= 2) return;
            if (index < 0 || index >= localPoints.Count) return;
            localPoints.RemoveAt(index);
            MarkDistanceCacheDirty();
        }

        public void RemoveLastPoint()
        {
            RemovePointAt(localPoints.Count - 1);
        }

        public void ReversePath()
        {
            if (localPoints == null) return;
            localPoints.Reverse();
            MarkDistanceCacheDirty();
        }

        public void FlattenY()
        {
            if (localPoints == null) return;
            for (int i = 0; i < localPoints.Count; i++)
                localPoints[i] = new Vector3(localPoints[i].x, 0f, localPoints[i].z);
            MarkDistanceCacheDirty();
        }

        public void CenterPathToObject()
        {
            if (localPoints == null || localPoints.Count == 0) return;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < localPoints.Count; i++) center += localPoints[i];
            center /= localPoints.Count;
            for (int i = 0; i < localPoints.Count; i++) localPoints[i] -= center;
            MarkDistanceCacheDirty();
        }

        public void AddEvent()
        {
            if (events == null) events = new List<VFXSplineEvent>();
            events.Add(new VFXSplineEvent()
            {
                eventName = "Event " + events.Count,
                progress = events.Count == 0 ? 0.5f : Mathf.Clamp01(events[events.Count - 1].progress + 0.1f),
                eventColor = eventColor
            });
        }

        public void RemoveEventAt(int index)
        {
            if (events == null) return;
            if (index < 0 || index >= events.Count) return;
            events.RemoveAt(index);
        }

        public void ResetEventFireStates()
        {
            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
                events[i].fired = false;
        }
    }
}

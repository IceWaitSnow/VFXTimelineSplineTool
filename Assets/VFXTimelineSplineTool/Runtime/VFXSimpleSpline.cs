using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events; // 仅用于兼容旧版 Path Events 数据。v2.0 正式工作流不再推荐事件系统。

namespace VFXTimelineSplineTool
{
    public enum VFXSplinePathMode
    {
        CatmullRom,
        Bezier
    }

    public enum VFXBezierHandleMode
    {
        Free,
        Aligned,
        Mirrored,
        AutoSmooth
    }

    public enum VFXBezierPointPreset
    {
        Corner,
        Smooth,
        Symmetric,
        AutoSmooth
    }

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

        [Header("生成位置")]
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

    [Serializable]
    public class VFXBezierPoint
    {
        public Vector3 position;
        public Vector3 inTangent;
        public Vector3 outTangent;
        public VFXBezierHandleMode handleMode = VFXBezierHandleMode.Aligned;

        public VFXBezierPoint() { }

        public VFXBezierPoint(Vector3 position)
        {
            this.position = position;
            inTangent = Vector3.left * 0.5f;
            outTangent = Vector3.right * 0.5f;
        }
    }

    [ExecuteAlways]
    public class VFXSimpleSpline : MonoBehaviour
    {
        public const string ToolVersion = VFXSplineToolVersion.Version;

        [Header("路径设置")]
        public VFXSplinePathMode pathMode = VFXSplinePathMode.CatmullRom;
        public Color pathColor = new Color(1f, 0.68f, 0.02f, 1f);
        public Color progressMarkColor = new Color(0.1f, 0.72f, 1f, 1f);
        [HideInInspector] public Color eventColor = new Color(1f, 0.25f, 0.1f, 1f); // v2.0 起隐藏：事件系统保留为旧工程兼容。
        [Min(1f)] public float lineWidth = 3f;
        [Min(0.01f)] public float pointSize = 0.15f;
        [Range(8, 256)] public int resolution = 48;

        [Header("显示设置")]
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

        [Header("动态起点 / 终点绑定")]
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

        [Header("距离等速 Progress")]
        [Range(32, 2048)] public int distanceSampleResolution = 256;

        [Header("控制点 - Local Space")]
        public List<Vector3> localPoints = new List<Vector3>()
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 1f, 0f),
            new Vector3(4f, 1f, 0f),
            new Vector3(6f, 0f, 0f)
        };

        [Header("Bezier 控制点 - Local Space")]
        public List<VFXBezierPoint> bezierPoints = new List<VFXBezierPoint>();

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
            EnsureLocalPoints();
            EnsureBezierPoints();

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

            float rawProgress;
            if (!TryDistanceProgressToRawProgress(distanceProgress, out rawProgress))
                return GetPointByRawProgress(distanceProgress);

            return GetPointByRawProgress(rawProgress);
        }

        public bool TryDistanceProgressToRawProgress(float distanceProgress, out float rawProgress)
        {
            distanceProgress = Mathf.Clamp01(distanceProgress);
            rawProgress = distanceProgress;
            RebuildDistanceCacheIfNeeded();

            if (cachedDistance.Count < 2 || cachedT.Count != cachedDistance.Count || cachedLength <= 0.00001f)
                return false;

            float targetDistance = distanceProgress * cachedLength;

            if (targetDistance <= 0f)
            {
                rawProgress = 0f;
                return true;
            }

            if (targetDistance >= cachedLength)
            {
                rawProgress = 1f;
                return true;
            }

            int index = cachedDistance.BinarySearch(targetDistance);
            if (index < 0)
                index = ~index;

            index = Mathf.Clamp(index, 1, cachedDistance.Count - 1);

            float d0 = cachedDistance[index - 1];
            float d1 = cachedDistance[index];
            float lerp = Mathf.Approximately(d0, d1) ? 0f : Mathf.InverseLerp(d0, d1, targetDistance);
            rawProgress = Mathf.Clamp01(Mathf.Lerp(cachedT[index - 1], cachedT[index], lerp));
            return true;
        }

        public Vector3 GetPointByRawProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (pathMode == VFXSplinePathMode.Bezier)
                return GetBezierPointByRawProgress(progress);

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

        private Vector3 GetBezierPointByRawProgress(float progress)
        {
            EnsureBezierPoints();

            int count = bezierPoints != null ? bezierPoints.Count : 0;
            if (count == 0) return transform.position;
            if (count == 1) return transform.TransformPoint(GetEffectiveBezierPosition(0));

            float scaled = progress * (count - 1);
            int index = Mathf.FloorToInt(scaled);
            if (index >= count - 1) index = count - 2;
            float t = scaled - index;

            Vector3 p0 = GetEffectiveBezierPosition(index);
            Vector3 p1 = p0 + bezierPoints[index].outTangent;
            Vector3 p3 = GetEffectiveBezierPosition(index + 1);
            Vector3 p2 = p3 + bezierPoints[index + 1].inTangent;
            return transform.TransformPoint(CubicBezier(p0, p1, p2, p3, t));
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

        public Vector3 GetEffectiveBezierPosition(int index)
        {
            if (bezierPoints == null || bezierPoints.Count == 0) return Vector3.zero;
            index = Mathf.Clamp(index, 0, bezierPoints.Count - 1);

            if (enableDynamicStartEndBinding)
            {
                if (index == 0 && dynamicStartTransform != null)
                    return transform.InverseTransformPoint(dynamicStartTransform.position);

                if (index == bezierPoints.Count - 1 && dynamicEndTransform != null)
                    return transform.InverseTransformPoint(dynamicEndTransform.position);
            }

            VFXBezierPoint point = bezierPoints[index];
            return point != null ? point.position : Vector3.zero;
        }

        public Vector3 GetEffectiveWorldPoint(int index)
        {
            return transform.TransformPoint(pathMode == VFXSplinePathMode.Bezier ? GetEffectiveBezierPosition(index) : GetEffectiveLocalPoint(index));
        }

        public bool IsPointDynamicallyBound(int index)
        {
            int count = GetActivePointCount();
            if (!enableDynamicStartEndBinding || count == 0) return false;
            if (index == 0 && dynamicStartTransform != null) return true;
            if (index == count - 1 && dynamicEndTransform != null) return true;
            return false;
        }

        public Transform GetDynamicBindingTransformForPoint(int index)
        {
            int count = GetActivePointCount();
            if (!enableDynamicStartEndBinding || count == 0) return null;
            if (index == 0) return dynamicStartTransform;
            if (index == count - 1) return dynamicEndTransform;
            return null;
        }

        public void ApplyDynamicBindingToLocalPoints()
        {
            if (enableDynamicStartEndBinding)
            {
                if (pathMode == VFXSplinePathMode.Bezier)
                {
                    EnsureBezierPoints();
                    if (bezierPoints.Count < 2) return;

                    if (dynamicStartTransform != null)
                        bezierPoints[0].position = transform.InverseTransformPoint(dynamicStartTransform.position);

                    if (dynamicEndTransform != null)
                        bezierPoints[bezierPoints.Count - 1].position = transform.InverseTransformPoint(dynamicEndTransform.position);
                }
                else
                {
                    EnsureLocalPoints();
                    if (localPoints.Count < 2) return;

                    if (dynamicStartTransform != null)
                        localPoints[0] = transform.InverseTransformPoint(dynamicStartTransform.position);

                    if (dynamicEndTransform != null)
                        localPoints[localPoints.Count - 1] = transform.InverseTransformPoint(dynamicEndTransform.position);
                }
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
                if (pathMode == VFXSplinePathMode.Bezier)
                    RefreshAllAutoSmoothBezierPoints();
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

        private static Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
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
            ConvertCatmullRomToBezier();
            MarkDistanceCacheDirty();
        }

        public void AddPoint()
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                EnsureBezierPoints();
                Vector3 newPoint = bezierPoints.Count > 0 ? bezierPoints[bezierPoints.Count - 1].position + Vector3.right : Vector3.zero;
                bezierPoints.Add(CreateBezierPointForBezierList(newPoint, bezierPoints.Count));
                RefreshAllAutoSmoothBezierPoints();
            }
            else
            {
                EnsureLocalPoints();
                Vector3 newPoint = localPoints.Count > 0 ? localPoints[localPoints.Count - 1] + Vector3.right : Vector3.zero;
                localPoints.Add(newPoint);
            }
            MarkDistanceCacheDirty();
        }

        public int AppendPointAtWorldPosition(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);

            if (pathMode == VFXSplinePathMode.Bezier)
            {
                EnsureBezierPoints();
                int index = bezierPoints.Count;
                bezierPoints.Add(CreateBezierPointForBezierList(localPosition, index));
                selectedPointIndex = index;
                RefreshAllAutoSmoothBezierPoints();
                MarkDistanceCacheDirty();
                return index;
            }

            EnsureLocalPoints();
            localPoints.Add(localPosition);
            selectedPointIndex = localPoints.Count - 1;
            MarkDistanceCacheDirty();
            return selectedPointIndex;
        }

        public void InsertPoint(int index)
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                EnsureBezierPoints();
                index = Mathf.Clamp(index, 0, bezierPoints.Count);
                Vector3 p;
                if (bezierPoints.Count == 0) p = Vector3.zero;
                else if (index <= 0) p = bezierPoints[0].position - Vector3.right;
                else if (index >= bezierPoints.Count) p = bezierPoints[bezierPoints.Count - 1].position + Vector3.right;
                else p = (bezierPoints[index - 1].position + bezierPoints[index].position) * 0.5f;
                bezierPoints.Insert(index, CreateBezierPointForBezierList(p, index));
                RefreshAllAutoSmoothBezierPoints();
            }
            else
            {
                EnsureLocalPoints();
                index = Mathf.Clamp(index, 0, localPoints.Count);
                Vector3 p;
                if (localPoints.Count == 0) p = Vector3.zero;
                else if (index <= 0) p = localPoints[0] - Vector3.right;
                else if (index >= localPoints.Count) p = localPoints[localPoints.Count - 1] + Vector3.right;
                else p = (localPoints[index - 1] + localPoints[index]) * 0.5f;
                localPoints.Insert(index, p);
            }
            MarkDistanceCacheDirty();
        }

        public int InsertBezierPointAtRawProgress(float progress)
        {
            EnsureBezierPoints();
            if (bezierPoints.Count < 2)
                return -1;

            progress = Mathf.Clamp01(progress);
            float scaled = progress * (bezierPoints.Count - 1);
            int index = Mathf.FloorToInt(scaled);
            if (index >= bezierPoints.Count - 1) index = bezierPoints.Count - 2;
            float t = Mathf.Clamp01(scaled - index);

            VFXBezierPoint leftPoint = bezierPoints[index];
            VFXBezierPoint rightPoint = bezierPoints[index + 1];
            if (leftPoint == null || rightPoint == null)
                return -1;

            Vector3 p0 = GetEffectiveBezierPosition(index);
            Vector3 p1 = p0 + leftPoint.outTangent;
            Vector3 p3 = GetEffectiveBezierPosition(index + 1);
            Vector3 p2 = p3 + rightPoint.inTangent;

            Vector3 a = Vector3.Lerp(p0, p1, t);
            Vector3 b = Vector3.Lerp(p1, p2, t);
            Vector3 c = Vector3.Lerp(p2, p3, t);
            Vector3 d = Vector3.Lerp(a, b, t);
            Vector3 e = Vector3.Lerp(b, c, t);
            Vector3 split = Vector3.Lerp(d, e, t);

            leftPoint.outTangent = a - p0;
            rightPoint.inTangent = c - p3;
            leftPoint.handleMode = VFXBezierHandleMode.Free;
            rightPoint.handleMode = VFXBezierHandleMode.Free;

            VFXBezierPoint newPoint = new VFXBezierPoint()
            {
                position = split,
                inTangent = d - split,
                outTangent = e - split,
                handleMode = VFXBezierHandleMode.Free
            };

            int insertIndex = index + 1;
            bezierPoints.Insert(insertIndex, newPoint);
            selectedPointIndex = insertIndex;
            MarkDistanceCacheDirty();
            return insertIndex;
        }

        public void RemovePointAt(int index)
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                if (bezierPoints == null || bezierPoints.Count <= 2) return;
                if (index < 0 || index >= bezierPoints.Count) return;
                bezierPoints.RemoveAt(index);
                RefreshAllAutoSmoothBezierPoints();
            }
            else
            {
                if (localPoints == null || localPoints.Count <= 2) return;
                if (index < 0 || index >= localPoints.Count) return;
                localPoints.RemoveAt(index);
            }
            MarkDistanceCacheDirty();
        }

        public void RemoveLastPoint()
        {
            RemovePointAt(GetActivePointCount() - 1);
        }

        public void ReversePath()
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                if (bezierPoints == null) return;
                bezierPoints.Reverse();
                for (int i = 0; i < bezierPoints.Count; i++)
                {
                    VFXBezierPoint point = bezierPoints[i];
                    if (point == null) continue;
                    Vector3 oldIn = point.inTangent;
                    point.inTangent = point.outTangent;
                    point.outTangent = oldIn;
                }
                RefreshAllAutoSmoothBezierPoints();
            }
            else
            {
                if (localPoints == null) return;
                localPoints.Reverse();
            }
            MarkDistanceCacheDirty();
        }

        public void FlattenY()
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                if (bezierPoints == null) return;
                for (int i = 0; i < bezierPoints.Count; i++)
                {
                    VFXBezierPoint point = bezierPoints[i];
                    if (point == null) continue;
                    point.position = new Vector3(point.position.x, 0f, point.position.z);
                    point.inTangent = new Vector3(point.inTangent.x, 0f, point.inTangent.z);
                    point.outTangent = new Vector3(point.outTangent.x, 0f, point.outTangent.z);
                }
            }
            else
            {
                if (localPoints == null) return;
                for (int i = 0; i < localPoints.Count; i++)
                    localPoints[i] = new Vector3(localPoints[i].x, 0f, localPoints[i].z);
            }
            MarkDistanceCacheDirty();
        }

        public void CenterPathToObject()
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                if (bezierPoints == null || bezierPoints.Count == 0) return;
                Vector3 bezierCenter = Vector3.zero;
                for (int i = 0; i < bezierPoints.Count; i++)
                    bezierCenter += bezierPoints[i] != null ? bezierPoints[i].position : Vector3.zero;
                bezierCenter /= bezierPoints.Count;
                for (int i = 0; i < bezierPoints.Count; i++)
                {
                    if (bezierPoints[i] != null)
                        bezierPoints[i].position -= bezierCenter;
                }
                MarkDistanceCacheDirty();
                return;
            }

            if (localPoints == null || localPoints.Count == 0) return;
            Vector3 catmullCenter = Vector3.zero;
            for (int i = 0; i < localPoints.Count; i++) catmullCenter += localPoints[i];
            catmullCenter /= localPoints.Count;
            for (int i = 0; i < localPoints.Count; i++) localPoints[i] -= catmullCenter;
            MarkDistanceCacheDirty();
        }

        public int GetActivePointCount()
        {
            if (pathMode == VFXSplinePathMode.Bezier)
                return bezierPoints != null ? bezierPoints.Count : 0;

            return localPoints != null ? localPoints.Count : 0;
        }

        public void SetActivePointWorldPosition(int index, Vector3 worldPosition)
        {
            if (pathMode == VFXSplinePathMode.Bezier)
            {
                EnsureBezierPoints();
                if (index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null) return;
                bezierPoints[index].position = transform.InverseTransformPoint(worldPosition);
                RefreshAutoSmoothAround(index);
            }
            else
            {
                EnsureLocalPoints();
                if (index < 0 || index >= localPoints.Count) return;
                localPoints[index] = transform.InverseTransformPoint(worldPosition);
            }
            MarkDistanceCacheDirty();
        }

        public void SetBezierInTangentWorldPosition(int index, Vector3 worldPosition)
        {
            SetBezierTangentWorldPosition(index, worldPosition, true);
        }

        public void SetBezierOutTangentWorldPosition(int index, Vector3 worldPosition)
        {
            SetBezierTangentWorldPosition(index, worldPosition, false);
        }

        public void SetBezierHandleMode(int index, VFXBezierHandleMode mode)
        {
            EnsureBezierPoints();
            if (index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null) return;

            VFXBezierPoint point = bezierPoints[index];
            point.handleMode = mode;
            if (mode == VFXBezierHandleMode.AutoSmooth)
                ApplyAutoSmoothBezierPoint(index);
            else if (mode != VFXBezierHandleMode.Free)
                ApplyBezierHandleMode(point, false);

            MarkDistanceCacheDirty();
        }

        public void AutoSmoothAllBezierPoints()
        {
            EnsureBezierPoints();
            for (int i = 0; i < bezierPoints.Count; i++)
            {
                if (bezierPoints[i] == null) continue;
                bezierPoints[i].handleMode = VFXBezierHandleMode.AutoSmooth;
                ApplyAutoSmoothBezierPoint(i);
            }

            MarkDistanceCacheDirty();
        }

        public void ApplyBezierPointPreset(int index, VFXBezierPointPreset preset)
        {
            EnsureBezierPoints();
            if (index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null) return;

            VFXBezierPoint point = bezierPoints[index];
            switch (preset)
            {
                case VFXBezierPointPreset.Corner:
                    point.handleMode = VFXBezierHandleMode.Free;
                    point.inTangent = Vector3.zero;
                    point.outTangent = Vector3.zero;
                    break;

                case VFXBezierPointPreset.Smooth:
                    point.handleMode = VFXBezierHandleMode.Aligned;
                    SetBezierTangentsFromNeighbors(index, false);
                    break;

                case VFXBezierPointPreset.Symmetric:
                    point.handleMode = VFXBezierHandleMode.Mirrored;
                    SetBezierTangentsFromNeighbors(index, true);
                    break;

                case VFXBezierPointPreset.AutoSmooth:
                    point.handleMode = VFXBezierHandleMode.AutoSmooth;
                    ApplyAutoSmoothBezierPoint(index);
                    break;
            }

            MarkDistanceCacheDirty();
        }

        public Vector3 GetBezierInTangentWorldPosition(int index)
        {
            if (bezierPoints == null || index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null)
                return transform.position;

            return transform.TransformPoint(GetEffectiveBezierPosition(index) + bezierPoints[index].inTangent);
        }

        public Vector3 GetBezierOutTangentWorldPosition(int index)
        {
            if (bezierPoints == null || index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null)
                return transform.position;

            return transform.TransformPoint(GetEffectiveBezierPosition(index) + bezierPoints[index].outTangent);
        }

        private void SetBezierTangentWorldPosition(int index, Vector3 worldPosition, bool isInTangent)
        {
            EnsureBezierPoints();
            if (index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null) return;

            VFXBezierPoint point = bezierPoints[index];
            if (point.handleMode == VFXBezierHandleMode.AutoSmooth)
                point.handleMode = VFXBezierHandleMode.Free;

            Vector3 localHandle = transform.InverseTransformPoint(worldPosition);
            Vector3 localTangent = localHandle - GetEffectiveBezierPosition(index);
            if (isInTangent)
                point.inTangent = localTangent;
            else
                point.outTangent = localTangent;

            ApplyBezierHandleMode(point, isInTangent);
            MarkDistanceCacheDirty();
        }

        private static void ApplyBezierHandleMode(VFXBezierPoint point, bool changedInTangent)
        {
            if (point == null || point.handleMode == VFXBezierHandleMode.Free || point.handleMode == VFXBezierHandleMode.AutoSmooth)
                return;

            Vector3 changed = changedInTangent ? point.inTangent : point.outTangent;
            if (changed.sqrMagnitude < 0.000001f)
                return;

            Vector3 other = changedInTangent ? point.outTangent : point.inTangent;
            float otherLength = point.handleMode == VFXBezierHandleMode.Mirrored ? changed.magnitude : Mathf.Max(0.0001f, other.magnitude);
            Vector3 mirrored = -changed.normalized * otherLength;

            if (changedInTangent)
                point.outTangent = mirrored;
            else
                point.inTangent = mirrored;
        }

        private void RefreshAutoSmoothAround(int index)
        {
            ApplyAutoSmoothBezierPoint(index - 1);
            ApplyAutoSmoothBezierPoint(index);
            ApplyAutoSmoothBezierPoint(index + 1);
        }

        private void RefreshAllAutoSmoothBezierPoints()
        {
            if (bezierPoints == null)
                return;

            for (int i = 0; i < bezierPoints.Count; i++)
                ApplyAutoSmoothBezierPoint(i);
        }

        private void ApplyAutoSmoothBezierPoint(int index)
        {
            if (bezierPoints == null || index < 0 || index >= bezierPoints.Count)
                return;

            VFXBezierPoint point = bezierPoints[index];
            if (point == null || point.handleMode != VFXBezierHandleMode.AutoSmooth)
                return;

            int count = bezierPoints.Count;
            if (count < 2)
                return;

            Vector3 position = GetEffectiveBezierPosition(index);
            Vector3 tangent;

            if (index <= 0)
            {
                Vector3 next = bezierPoints[1] != null ? GetEffectiveBezierPosition(1) : position + Vector3.right;
                tangent = (next - position) / 3f;
            }
            else if (index >= count - 1)
            {
                Vector3 prev = bezierPoints[count - 2] != null ? GetEffectiveBezierPosition(count - 2) : position - Vector3.right;
                tangent = (position - prev) / 3f;
            }
            else
            {
                Vector3 prev = bezierPoints[index - 1] != null ? GetEffectiveBezierPosition(index - 1) : position - Vector3.right;
                Vector3 next = bezierPoints[index + 1] != null ? GetEffectiveBezierPosition(index + 1) : position + Vector3.right;
                tangent = (next - prev) / 6f;
            }

            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.right * 0.5f;

            point.inTangent = -tangent;
            point.outTangent = tangent;
        }

        private void SetBezierTangentsFromNeighbors(int index, bool mirrored)
        {
            if (bezierPoints == null || index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null)
                return;

            VFXBezierPoint point = bezierPoints[index];
            Vector3 tangent = EstimateBezierTangentFromBezierList(index);
            point.inTangent = -tangent;
            point.outTangent = mirrored ? tangent.normalized * tangent.magnitude : tangent;
        }

        public void ConvertCatmullRomToBezier()
        {
            EnsureLocalPoints();
            bezierPoints = new List<VFXBezierPoint>();
            for (int i = 0; i < localPoints.Count; i++)
                bezierPoints.Add(CreateBezierPoint(localPoints[i], i));
            MarkDistanceCacheDirty();
        }

        public void ConvertBezierToCatmullRom()
        {
            EnsureBezierPoints();
            localPoints = new List<Vector3>();
            for (int i = 0; i < bezierPoints.Count; i++)
                localPoints.Add(bezierPoints[i] != null ? bezierPoints[i].position : Vector3.zero);
            MarkDistanceCacheDirty();
        }

        private void EnsureLocalPoints()
        {
            if (localPoints == null) localPoints = new List<Vector3>();
            if (localPoints.Count < 2)
            {
                while (localPoints.Count < 2)
                    localPoints.Add(Vector3.right * localPoints.Count);
            }
        }

        private void EnsureBezierPoints()
        {
            if (bezierPoints == null)
                bezierPoints = new List<VFXBezierPoint>();

            if (bezierPoints.Count < 2)
            {
                EnsureLocalPoints();
                ConvertCatmullRomToBezier();
            }

            for (int i = 0; i < bezierPoints.Count; i++)
            {
                if (bezierPoints[i] == null)
                    bezierPoints[i] = CreateBezierPoint(Vector3.right * i, i);
            }
        }

        private VFXBezierPoint CreateBezierPoint(Vector3 position, int index)
        {
            VFXBezierPoint point = new VFXBezierPoint(position);
            Vector3 tangent = EstimateBezierTangent(index, position);
            point.inTangent = -tangent;
            point.outTangent = tangent;
            point.handleMode = VFXBezierHandleMode.Aligned;
            return point;
        }

        private VFXBezierPoint CreateBezierPointForBezierList(Vector3 position, int index)
        {
            VFXBezierPoint point = new VFXBezierPoint(position);
            Vector3 tangent = EstimateBezierTangentFromBezierList(index, position);
            point.inTangent = -tangent;
            point.outTangent = tangent;
            point.handleMode = VFXBezierHandleMode.Aligned;
            return point;
        }

        private Vector3 EstimateBezierTangentFromBezierList(int index)
        {
            if (bezierPoints == null || index < 0 || index >= bezierPoints.Count || bezierPoints[index] == null)
                return Vector3.right * 0.5f;

            Vector3 position = GetEffectiveBezierPosition(index);
            Vector3 tangent;
            if (index <= 0)
            {
                Vector3 next = bezierPoints.Count > 1 && bezierPoints[1] != null ? GetEffectiveBezierPosition(1) : position + Vector3.right;
                tangent = (next - position) / 3f;
            }
            else if (index >= bezierPoints.Count - 1)
            {
                Vector3 prev = bezierPoints[index - 1] != null ? GetEffectiveBezierPosition(index - 1) : position - Vector3.right;
                tangent = (position - prev) / 3f;
            }
            else
            {
                Vector3 prev = bezierPoints[index - 1] != null ? GetEffectiveBezierPosition(index - 1) : position - Vector3.right;
                Vector3 next = bezierPoints[index + 1] != null ? GetEffectiveBezierPosition(index + 1) : position + Vector3.right;
                tangent = (next - prev) / 6f;
            }

            return tangent.sqrMagnitude < 0.000001f ? Vector3.right * 0.5f : tangent;
        }

        private Vector3 EstimateBezierTangentFromBezierList(int index, Vector3 position)
        {
            Vector3 tangent = Vector3.right * 0.5f;

            if (bezierPoints != null && bezierPoints.Count > 0)
            {
                if (index <= 0)
                {
                    Vector3 next = bezierPoints[0] != null ? bezierPoints[0].position : position + Vector3.right;
                    tangent = (next - position) / 3f;
                }
                else if (index >= bezierPoints.Count)
                {
                    Vector3 prev = bezierPoints[bezierPoints.Count - 1] != null ? bezierPoints[bezierPoints.Count - 1].position : position - Vector3.right;
                    tangent = (position - prev) / 3f;
                }
                else
                {
                    Vector3 prev = bezierPoints[index - 1] != null ? GetEffectiveBezierPosition(index - 1) : position - Vector3.right;
                    Vector3 next = bezierPoints[index] != null ? GetEffectiveBezierPosition(index) : position + Vector3.right;
                    tangent = (next - prev) / 6f;
                }
            }

            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.right * 0.5f;

            return tangent;
        }

        private Vector3 EstimateBezierTangent(int index, Vector3 position)
        {
            List<Vector3> source = localPoints;
            if (source == null || source.Count < 2)
                return Vector3.right * 0.5f;

            int prevIndex = Mathf.Clamp(index - 1, 0, source.Count - 1);
            int nextIndex = Mathf.Clamp(index + 1, 0, source.Count - 1);
            Vector3 prev = source[prevIndex];
            Vector3 next = source[nextIndex];

            if (index <= 0)
                return (next - position) / 3f;

            if (index >= source.Count - 1)
                return (position - prev) / 3f;

            return (next - prev) / 6f;
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

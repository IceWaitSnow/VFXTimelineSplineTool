#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [CustomEditor(typeof(VFXSimpleSpline))]
    public class VFXSimpleSplineEditor : Editor
    {
        private static int batchAnchorCount = 5;
        private static VFXSplineAnimator batchAnchorSourceAnimator;
        private static GameObject batchAnchorChildTemplate;
        private static bool batchAnchorRemoveTemplateSplineComponents = true;
        private static float batchAnchorStartOffset = 0f;
        private static float batchAnchorOffsetStep = -0.01f;
        private static VFXSplineAnchorProgressWrapMode batchAnchorWrapMode = VFXSplineAnchorProgressWrapMode.Clamp;
        private const int MaxBatchAnchorCount = 500;
        private const float HealthDuplicatePointDistance = 0.001f;
        private const float HealthClosePointDistance = 0.05f;
        private const float HealthLoopEndpointDistance = 0.15f;
        private const float HealthLongHandleRatio = 1.25f;
        private const float HealthClampedHandleRatio = 0.5f;
        private const float HealthLoopSeamAngle = 60f;

        private enum ShapePreset
        {
            Line,
            Arc,
            S_Curve,
            Wave,
            Zigzag,
            Circle,
            Ring,
            Ellipse,
            Square,
            Rectangle,
            Triangle,
            Diamond,
            Infinity,
            Spiral,
            Star,
            Heart,
            U_Shape
        }

        private static ShapePreset selectedShapePreset = ShapePreset.S_Curve;
        private static float presetScale = 3f;
        private static float presetWidth = 6f;
        private static float presetHeight = 3f;
        private static float presetRotationY = 0f;
        private static Vector3 presetOffset = Vector3.zero;
        private static int presetPointCount = 12;
        private static int presetWaveCount = 2;
        private static float presetSpiralTurns = 1.5f;
        private static bool livePreviewShapePreset = false;

        private const string UserPresetFolder = "Assets/VFXTimelineSplineTool/UserPresets";
        private static string userPresetName = "MySplinePath_01";
        private static bool saveDisplaySettingsWithPreset = false;
        private static VFXUserPathPreset selectedUserPreset;

        public override void OnInspectorGUI()
        {
            VFXSimpleSpline spline = (VFXSimpleSpline)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("VFX Timeline Spline Tool v" + VFXSimpleSpline.ToolVersion, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("3D Catmull-Rom / Bezier 自由曲线路径。v" + VFXSplineToolVersion.Version + " 工作流：Spline Path 负责路径，VFXSplineAnimator 负责路径运动，VFXSplineAnchor 负责粒子/面片/爆点挂点；Dynamic Start / End Binding 可让路径起点和终点跟随场景物体。", MessageType.Info);

            serializedObject.Update();

            DrawPathModeProperty(spline);
            DrawProperty("loop", "Loop 闭合路径");
            DrawProperty("pathColor", "路径颜色");
            DrawProperty("progressMarkColor", "Progress 标记颜色");
            DrawProperty("lineWidth", "线宽");
            DrawProperty("pointSize", "控制点大小");
            DrawProperty("resolution", "曲线精度");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("显示设置", EditorStyles.boldLabel);
            DrawProperty("alwaysShowPathInSceneView", "Scene 中始终显示路径");
            DrawProperty("showPointLabels", "显示控制点编号");
            DrawProperty("showAllPointHandles", "显示全部控制点坐标轴");
            EditorGUILayout.HelpBox("默认只显示当前选中控制点的移动坐标轴，避免 Scene 视图太乱。点击路径控制点可切换当前编辑点；需要同时显示全部坐标轴时再勾选 Show All Point Handles。", MessageType.None);
            DrawProperty("showProgressMarks", "显示 Progress 标记");
            DrawProperty("progressMarkCount", "Progress 标记数量");
            DrawProperty("progressMarksUseDistance", "Progress 标记按距离分布");
            DrawProperty("showDirectionArrows", "显示方向箭头");
            DrawProperty("arrowCount", "方向箭头数量");
            DrawProperty("arrowSize", "方向箭头大小");
            DrawProperty("showNormals", "显示 Normal");
            if (spline.showNormals)
            {
                DrawProperty("normalColor", "Normal 颜色");
                DrawProperty("normalLength", "Normal 长度");
                DrawProperty("normalCount", "Normal 数量");
                DrawProperty("normalReferenceUseWorldSpace", "Normal 使用世界方向");
                DrawProperty("normalReference", "Normal 参考方向");
                DrawProperty("normalAngle", "Normal 旋转角度");
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("动态起点 / 终点绑定", EditorStyles.boldLabel);
            DrawProperty("enableDynamicStartEndBinding", "启用动态起点 / 终点");
            if (spline.enableDynamicStartEndBinding)
            {
                EditorGUILayout.HelpBox("开启后，路径第一个点跟随 Start Transform，最后一个点跟随 End Transform。中间点仍保留当前 Local Points，用来控制弧度。适合飞向目标、奖励飞行、吸入路径。", MessageType.Info);
                DrawProperty("dynamicStartTransform", "起点 Transform");
                DrawProperty("dynamicEndTransform", "终点 Transform");
                DrawProperty("dynamicUpdateInEditMode", "编辑模式实时更新");
                DrawProperty("showDynamicBindingLabels", "显示绑定标签");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("烘焙动态端点到控制点"))
                    {
                        ModifySpline(spline, "Bake Dynamic Ends To Points", () => spline.ApplyDynamicBindingToLocalPoints());
                    }
                    if (GUILayout.Button("重建距离缓存"))
                    {
                        Undo.RecordObject(spline, "Rebuild Distance Cache");
                        spline.RebuildDistanceCache();
                        EditorUtility.SetDirty(spline);
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("距离等速 Progress", EditorStyles.boldLabel);
            DrawProperty("distanceSampleResolution", "距离采样精度");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("路径数据", EditorStyles.boldLabel);
            if (spline.pathMode == VFXSplinePathMode.Bezier)
            {
                DrawProperty("bezierPoints", "Bezier 控制点", true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("转换 Bezier 为 Catmull-Rom"))
                        ModifySpline(spline, "Convert Bezier To Catmull-Rom", () =>
                        {
                            spline.ConvertBezierToCatmullRom();
                            spline.pathMode = VFXSplinePathMode.CatmullRom;
                        });
                }
            }
            else
            {
                DrawProperty("localPoints", "Local 控制点", true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("转换 Catmull-Rom 为 Bezier"))
                        ModifySpline(spline, "Convert Catmull-Rom To Bezier", () =>
                        {
                            spline.ConvertCatmullRomToBezier();
                            spline.pathMode = VFXSplinePathMode.Bezier;
                        });
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("距离信息", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("近似长度", spline.ApproxLength.ToString("F3"));
            if (GUILayout.Button("重建距离缓存"))
            {
                Undo.RecordObject(spline, "Rebuild Distance Cache");
                spline.RebuildDistanceCache();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("路径工具", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加点")) ModifySpline(spline, "Add Point", () => spline.AddPoint());
                if (GUILayout.Button("插入点")) ModifySpline(spline, "Insert Point", () => spline.InsertPoint(Mathf.Max(1, spline.GetActivePointCount() - 1)));
                if (GUILayout.Button("删除末尾点")) ModifySpline(spline, "Remove Last Point", () => spline.RemoveLastPoint());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("反转路径")) ModifySpline(spline, "Reverse Path", () => spline.ReversePath());
                if (GUILayout.Button("重置路径")) ModifySpline(spline, "Reset Path", () => spline.ResetPath());
                if (GUILayout.Button("压平 Y")) ModifySpline(spline, "Flatten Y", () => spline.FlattenY());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                string loopButton = spline.loop ? "关闭 Loop" : "开启 Loop";
                if (GUILayout.Button(loopButton)) ModifySpline(spline, "Toggle Spline Loop", () =>
                {
                    spline.loop = !spline.loop;
                    spline.MarkDistanceCacheDirty();
                });
            }
            if (GUILayout.Button("路径居中到物体")) ModifySpline(spline, "Center Path To Object", () => spline.CenterPathToObject());

            DrawSplineHealthTools(spline);
            DrawShapePresetTools(spline);
            DrawUserPresetTools(spline);
            DrawAnchorTools(spline);
            DrawPointTools(spline);
        }
        private void DrawProperty(string name, string label, bool includeChildren = false)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, BuildContent(label, name, p), includeChildren);
        }

        private static GUIContent BuildContent(string label, string propertyName, SerializedProperty property)
        {
            string tooltip = !string.IsNullOrEmpty(property.tooltip) ? property.tooltip : GetSplinePropertyTooltip(propertyName);
            return new GUIContent(label, tooltip);
        }

        private static string GetSplinePropertyTooltip(string propertyName)
        {
            switch (propertyName)
            {
                case "loop": return "开启后，路径最后一个控制点会连接回第一个控制点，适合循环运动，避免 0% / 100% 首尾跳变。";
                case "pathColor": return "Scene 视图中绘制 Spline 路径线的颜色。";
                case "progressMarkColor": return "Scene 视图中 Progress 百分比标记的颜色。";
                case "lineWidth": return "Spline 路径线在 Scene 视图中的显示宽度。";
                case "pointSize": return "控制点在 Scene 视图中的显示大小，也会影响点选区域的基础尺寸。";
                case "resolution": return "绘制和采样曲线时使用的分段数量。值越高曲线越平滑，但 Scene 绘制开销也越高。";
                case "alwaysShowPathInSceneView": return "开启后，即使没有选中此 Spline，也会在 Scene 视图中显示路径。";
                case "showPointLabels": return "在 Scene 视图中显示控制点编号，方便定位和编辑。";
                case "showAllPointHandles": return "开启后所有控制点都会显示 PositionHandle。关闭时只显示当前选中点的坐标轴，Scene 视图更干净。";
                case "showProgressMarks": return "在路径上显示 0%、25%、50% 等 Progress 标记。";
                case "progressMarkCount": return "Progress 标记数量。值为 4 时会显示 0%、25%、50%、75%、100%。";
                case "progressMarksUseDistance": return "开启后，Progress 标记按路径实际距离均匀分布；关闭后按曲线参数均匀分布。";
                case "showDirectionArrows": return "在路径上显示方向箭头，帮助判断运动方向。";
                case "arrowCount": return "路径方向箭头的数量。";
                case "arrowSize": return "路径方向箭头在 Scene 视图中的显示大小。";
                case "showNormals": return "在 Scene 视图中显示路径 Normal 方向。旋转跟随路径时会使用这个 Normal 作为稳定上方向。";
                case "normalColor": return "Scene 视图中 Normal 线的颜色。";
                case "normalLength": return "Normal 线的显示长度。";
                case "normalCount": return "沿路径显示的 Normal 数量。";
                case "normalReferenceUseWorldSpace": return "开启后 Normal 参考方向按世界坐标解释，旋转 Spline 物体时 Normal 不会跟着一起旋转。";
                case "normalReference": return "用于生成 Normal 的参考方向，会投影到路径切线的垂直平面上。可通过 Normal 使用世界方向切换本地/世界解释。";
                case "normalAngle": return "绕路径切线额外旋转 Normal 的角度。";
                case "enableDynamicStartEndBinding": return "开启后，路径第一个点会跟随起点 Transform，最后一个点会跟随终点 Transform。";
                case "dynamicStartTransform": return "动态起点绑定对象。为空时使用 Local 控制点中的第一个点。";
                case "dynamicEndTransform": return "动态终点绑定对象。为空时使用 Local 控制点中的最后一个点。";
                case "dynamicUpdateInEditMode": return "编辑模式下也实时读取动态起点 / 终点 Transform。";
                case "showDynamicBindingLabels": return "在 Scene 视图中显示动态端点绑定标签。";
                case "distanceSampleResolution": return "距离等速 Progress 的采样精度。值越高等速效果越稳定，但重建缓存开销越高。";
                case "bezierPoints": return "Bezier 模式下使用的控制点数据，包含位置、入/出手柄和手柄模式。";
                case "localPoints": return "Catmull-Rom 模式下使用的 Local Space 控制点。";
                default: return "";
            }
        }
        private void DrawPathModeProperty(VFXSimpleSpline spline)
        {
            SerializedProperty pathModeProp = serializedObject.FindProperty("pathMode");
            if (pathModeProp == null)
                return;

            VFXSplinePathMode oldMode = (VFXSplinePathMode)pathModeProp.enumValueIndex;
            EditorGUI.BeginChangeCheck();
            VFXSplinePathMode newMode = (VFXSplinePathMode)EditorGUILayout.EnumPopup(new GUIContent("路径模式", "选择路径的数学表示方式。Catmull-Rom 适合快速拉形状，Bezier 适合用手柄精细控制曲线。"), oldMode);
            if (!EditorGUI.EndChangeCheck() || newMode == oldMode)
                return;

            serializedObject.ApplyModifiedProperties();
            ModifySpline(spline, "Change Spline Path Mode", () =>
            {
                if (newMode == VFXSplinePathMode.Bezier)
                {
                    spline.ConvertCatmullRomToBezier();
                    spline.pathMode = VFXSplinePathMode.Bezier;
                    spline.showAllPointHandles = false;
                }
                else
                {
                    spline.ConvertBezierToCatmullRom();
                    spline.pathMode = VFXSplinePathMode.CatmullRom;
                }
            });
            serializedObject.Update();
        }

        private static void ModifySpline(VFXSimpleSpline spline, string undoName, System.Action action)
        {
            Undo.RecordObject(spline, undoName);
            action();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private void DrawSplineHealthTools(VFXSimpleSpline spline)
        {
            SplineHealthReport report = BuildSplineHealthReport(spline);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("路径健康检查", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(report.Message, report.HasIssues ? MessageType.Warning : MessageType.Info);

            GUI.enabled = report.HasIssueLocation;
            if (GUILayout.Button("选中第一个问题点"))
                SelectHealthIssuePoints(spline, report);
            GUI.enabled = true;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = report.OverlappingPairCount > 0;
                if (GUILayout.Button("删除重叠控制点"))
                    RemoveOverlappingPoints(spline);

                GUI.enabled = report.TooClosePairCount > 0;
                if (GUILayout.Button("按距离均匀重排"))
                    RedistributeHealthPointsByDistance(spline);

                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = report.LongBezierHandleCount > 0;
                if (GUILayout.Button("收紧过长 Bezier 手柄"))
                    ClampLongBezierHandles(spline);

                GUI.enabled = report.CanEnableLoop;
                if (GUILayout.Button("开启 Loop"))
                    ModifySpline(spline, "Enable Spline Loop", () =>
                    {
                        spline.loop = true;
                        spline.MarkDistanceCacheDirty();
                    });

                GUI.enabled = true;
            }
        }

        private static SplineHealthReport BuildSplineHealthReport(VFXSimpleSpline spline)
        {
            SplineHealthReport report = new SplineHealthReport();
            if (spline == null)
            {
                report.Message = "没有可检查的 Spline。";
                return report;
            }

            List<Vector3> points = GetHealthLocalPoints(spline);
            int count = points.Count;
            if (count < 2)
            {
                report.Message = "控制点少于 2 个，路径无法形成有效线段。";
                report.HasIssues = true;
                return report;
            }

            for (int i = 0; i < count - 1; i++)
                CountPointDistanceIssue(points[i], points[i + 1], report);

            if (spline.loop && count > 2)
                CountPointDistanceIssue(points[count - 1], points[0], report);

            if (!spline.loop && count > 2)
            {
                float endpointDistance = Vector3.Distance(points[0], points[count - 1]);
                report.CanEnableLoop = endpointDistance <= HealthLoopEndpointDistance;
            }

            if (spline.pathMode == VFXSplinePathMode.Bezier)
                report.LongBezierHandleCount = CountLongBezierHandles(spline, points);

            if (spline.loop && count > 2)
            {
                Vector3 startTangent = spline.GetTangent(0.001f, true);
                Vector3 endTangent = spline.GetTangent(0.999f, true);
                if (startTangent.sqrMagnitude > 0.000001f && endTangent.sqrMagnitude > 0.000001f)
                    report.HasLoopSeamAngleWarning = Vector3.Angle(startTangent, endTangent) > HealthLoopSeamAngle;
            }

            AssignFirstHealthIssueLocation(spline, points, report);

            report.HasIssues = report.OverlappingPairCount > 0 ||
                               report.TooClosePairCount > 0 ||
                               report.LongBezierHandleCount > 0 ||
                               report.CanEnableLoop ||
                               report.HasLoopSeamAngleWarning;

            report.Message = BuildSplineHealthMessage(report, count, spline.pathMode);
            return report;
        }

        private static List<Vector3> GetHealthLocalPoints(VFXSimpleSpline spline)
        {
            List<Vector3> points = new List<Vector3>();
            if (spline == null)
                return points;

            int count = spline.GetActivePointCount();
            for (int i = 0; i < count; i++)
            {
                if (spline.pathMode == VFXSplinePathMode.Bezier)
                    points.Add(spline.GetEffectiveBezierPosition(i));
                else
                    points.Add(spline.GetEffectiveLocalPoint(i));
            }

            return points;
        }

        private static void CountPointDistanceIssue(Vector3 a, Vector3 b, SplineHealthReport report)
        {
            float distance = Vector3.Distance(a, b);
            if (distance <= HealthDuplicatePointDistance)
                report.OverlappingPairCount++;
            else if (distance <= HealthClosePointDistance)
                report.TooClosePairCount++;
        }

        private static int CountLongBezierHandles(VFXSimpleSpline spline, List<Vector3> points)
        {
            if (spline == null || spline.bezierPoints == null || points == null)
                return 0;

            int count = Mathf.Min(spline.bezierPoints.Count, points.Count);
            int longHandleCount = 0;
            for (int i = 0; i < count; i++)
            {
                VFXBezierPoint point = spline.bezierPoints[i];
                if (point == null || point.handleMode == VFXBezierHandleMode.AutoSmooth)
                    continue;

                float prevDistance = GetNeighborDistance(points, i, -1, spline.loop);
                float nextDistance = GetNeighborDistance(points, i, 1, spline.loop);
                if (prevDistance > HealthDuplicatePointDistance && point.inTangent.magnitude > prevDistance * HealthLongHandleRatio)
                    longHandleCount++;
                if (nextDistance > HealthDuplicatePointDistance && point.outTangent.magnitude > nextDistance * HealthLongHandleRatio)
                    longHandleCount++;
            }

            return longHandleCount;
        }

        private static float GetNeighborDistance(List<Vector3> points, int index, int direction, bool loop)
        {
            if (points == null || points.Count < 2)
                return 0f;

            int neighbor = index + direction;
            if (loop)
                neighbor = (neighbor + points.Count) % points.Count;
            else
                neighbor = Mathf.Clamp(neighbor, 0, points.Count - 1);

            if (neighbor == index)
                return 0f;

            return Vector3.Distance(points[index], points[neighbor]);
        }

        private static string BuildSplineHealthMessage(SplineHealthReport report, int pointCount, VFXSplinePathMode pathMode)
        {
            if (!report.HasIssues)
                return "未发现明显问题。当前 " + pathMode + " 路径共有 " + pointCount + " 个控制点。";

            List<string> lines = new List<string>();
            lines.Add("发现 " + pathMode + " 路径可能存在以下问题：");
            if (report.OverlappingPairCount > 0)
                lines.Add("- 有 " + report.OverlappingPairCount + " 处相邻控制点重叠，可能导致速度或切线异常。");
            if (report.TooClosePairCount > 0)
                lines.Add("- 有 " + report.TooClosePairCount + " 处相邻控制点距离过近，运动时可能出现局部抖动。");
            if (report.LongBezierHandleCount > 0)
                lines.Add("- 有 " + report.LongBezierHandleCount + " 个 Bezier 手柄相对线段过长，可能造成过冲或打圈。");
            if (report.CanEnableLoop)
                lines.Add("- 首尾控制点距离很近，但 Loop 还没开启。");
            if (report.HasLoopSeamAngleWarning)
                lines.Add("- Loop 首尾切线变化较大，循环播放时可能有方向跳变。");

            return string.Join("\n", lines.ToArray());
        }

        private static void AssignFirstHealthIssueLocation(VFXSimpleSpline spline, List<Vector3> points, SplineHealthReport report)
        {
            if (spline == null || points == null || report == null || points.Count < 2)
                return;

            if (TryFindHealthDistanceIssue(points, spline.loop, HealthDuplicatePointDistance, true, report))
                return;

            if (TryFindHealthDistanceIssue(points, spline.loop, HealthClosePointDistance, false, report))
                return;

            if (spline.pathMode == VFXSplinePathMode.Bezier && TryFindLongBezierHandleIssue(spline, points, report))
                return;

            if ((report.CanEnableLoop || report.HasLoopSeamAngleWarning) && points.Count > 2)
                SetHealthIssueLocation(report, 0, points.Count - 1);
        }

        private static bool TryFindHealthDistanceIssue(List<Vector3> points, bool loop, float threshold, bool duplicateOnly, SplineHealthReport report)
        {
            int count = points != null ? points.Count : 0;
            if (count < 2)
                return false;

            for (int i = 0; i < count - 1; i++)
            {
                if (IsHealthDistanceIssue(points[i], points[i + 1], threshold, duplicateOnly))
                {
                    SetHealthIssueLocation(report, i, i + 1);
                    return true;
                }
            }

            if (loop && count > 2 && IsHealthDistanceIssue(points[count - 1], points[0], threshold, duplicateOnly))
            {
                SetHealthIssueLocation(report, count - 1, 0);
                return true;
            }

            return false;
        }

        private static bool IsHealthDistanceIssue(Vector3 a, Vector3 b, float threshold, bool duplicateOnly)
        {
            float distance = Vector3.Distance(a, b);
            if (duplicateOnly)
                return distance <= HealthDuplicatePointDistance;

            return distance > HealthDuplicatePointDistance && distance <= threshold;
        }

        private static bool TryFindLongBezierHandleIssue(VFXSimpleSpline spline, List<Vector3> points, SplineHealthReport report)
        {
            if (spline == null || spline.bezierPoints == null || points == null)
                return false;

            int count = Mathf.Min(spline.bezierPoints.Count, points.Count);
            for (int i = 0; i < count; i++)
            {
                VFXBezierPoint point = spline.bezierPoints[i];
                if (point == null || point.handleMode == VFXBezierHandleMode.AutoSmooth)
                    continue;

                float prevDistance = GetNeighborDistance(points, i, -1, spline.loop);
                if (prevDistance > HealthDuplicatePointDistance && point.inTangent.magnitude > prevDistance * HealthLongHandleRatio)
                {
                    SetHealthIssueLocation(report, i, -1);
                    return true;
                }

                float nextDistance = GetNeighborDistance(points, i, 1, spline.loop);
                if (nextDistance > HealthDuplicatePointDistance && point.outTangent.magnitude > nextDistance * HealthLongHandleRatio)
                {
                    SetHealthIssueLocation(report, i, -1);
                    return true;
                }
            }

            return false;
        }

        private static void SetHealthIssueLocation(SplineHealthReport report, int firstIndex, int secondIndex)
        {
            report.HasIssueLocation = firstIndex >= 0;
            report.FirstIssuePointIndex = firstIndex;
            report.SecondIssuePointIndex = secondIndex;
        }

        private static void SelectHealthIssuePoints(VFXSimpleSpline spline, SplineHealthReport report)
        {
            if (spline == null || report == null || !report.HasIssueLocation)
                return;

            int count = spline.GetActivePointCount();
            if (count <= 0)
                return;

            int firstIndex = Mathf.Clamp(report.FirstIssuePointIndex, 0, count - 1);
            int secondIndex = report.SecondIssuePointIndex >= 0 ? Mathf.Clamp(report.SecondIssuePointIndex, 0, count - 1) : -1;

            Undo.RecordObject(spline, "Select Spline Health Issue");
            spline.selectedPointIndex = firstIndex;
            spline.selectedPointIndices = new List<int>() { firstIndex };
            if (secondIndex >= 0 && secondIndex != firstIndex)
                spline.selectedPointIndices.Add(secondIndex);

            VFXSplinePointAPI.EnterPointMode(spline);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void RemoveOverlappingPoints(VFXSimpleSpline spline)
        {
            if (spline == null)
                return;

            if (spline.pathMode == VFXSplinePathMode.Bezier)
                RemoveOverlappingBezierPoints(spline);
            else
                RemoveOverlappingCatmullRomPoints(spline);
        }

        private static void RedistributeHealthPointsByDistance(VFXSimpleSpline spline)
        {
            if (spline == null)
                return;

            int pointCount = spline.GetActivePointCount();
            if (pointCount < 2)
                return;

            if (spline.pathMode == VFXSplinePathMode.Bezier)
                RedistributeHealthBezierPointsByDistance(spline, pointCount);
            else
                RedistributeHealthCatmullRomPointsByDistance(spline, pointCount);
        }

        private static void RedistributeHealthCatmullRomPointsByDistance(VFXSimpleSpline spline, int pointCount)
        {
            List<Vector3> resampled = new List<Vector3>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float progress = spline.loop ? i / (float)pointCount : i / (float)(pointCount - 1);
                resampled.Add(spline.GetLocalPoint(progress, true));
            }

            Undo.RecordObject(spline, "Redistribute Spline Points");
            spline.localPoints = resampled;
            spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, pointCount - 1);
            spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void RedistributeHealthBezierPointsByDistance(VFXSimpleSpline spline, int pointCount)
        {
            List<VFXBezierPoint> resampled = new List<VFXBezierPoint>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float progress = spline.loop ? i / (float)pointCount : i / (float)(pointCount - 1);
                VFXBezierPoint point = new VFXBezierPoint(spline.GetLocalPoint(progress, true));
                point.handleMode = VFXBezierHandleMode.AutoSmooth;
                resampled.Add(point);
            }

            Undo.RecordObject(spline, "Redistribute Bezier Points");
            spline.bezierPoints = resampled;
            spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, pointCount - 1);
            spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
            spline.AutoSmoothAllBezierPoints();
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void RemoveOverlappingCatmullRomPoints(VFXSimpleSpline spline)
        {
            if (spline.localPoints == null || spline.localPoints.Count <= 2)
                return;

            List<Vector3> cleaned = new List<Vector3>();
            for (int i = 0; i < spline.localPoints.Count; i++)
            {
                Vector3 point = spline.localPoints[i];
                if (cleaned.Count == 0 || Vector3.Distance(cleaned[cleaned.Count - 1], point) > HealthDuplicatePointDistance)
                    cleaned.Add(point);
            }

            if (spline.loop && cleaned.Count > 2 && Vector3.Distance(cleaned[0], cleaned[cleaned.Count - 1]) <= HealthDuplicatePointDistance)
                cleaned.RemoveAt(cleaned.Count - 1);

            if (cleaned.Count == spline.localPoints.Count || cleaned.Count < 2)
                return;

            Undo.RecordObject(spline, "Remove Overlapping Spline Points");
            spline.localPoints = cleaned;
            spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, cleaned.Count - 1);
            spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void RemoveOverlappingBezierPoints(VFXSimpleSpline spline)
        {
            if (spline.bezierPoints == null || spline.bezierPoints.Count <= 2)
                return;

            List<VFXBezierPoint> cleaned = new List<VFXBezierPoint>();
            for (int i = 0; i < spline.bezierPoints.Count; i++)
            {
                VFXBezierPoint point = spline.bezierPoints[i];
                if (point == null)
                    continue;

                if (cleaned.Count == 0 || Vector3.Distance(cleaned[cleaned.Count - 1].position, point.position) > HealthDuplicatePointDistance)
                    cleaned.Add(point);
            }

            if (spline.loop && cleaned.Count > 2 && Vector3.Distance(cleaned[0].position, cleaned[cleaned.Count - 1].position) <= HealthDuplicatePointDistance)
                cleaned.RemoveAt(cleaned.Count - 1);

            if (cleaned.Count == spline.bezierPoints.Count || cleaned.Count < 2)
                return;

            Undo.RecordObject(spline, "Remove Overlapping Bezier Points");
            spline.bezierPoints = cleaned;
            spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, cleaned.Count - 1);
            spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void ClampLongBezierHandles(VFXSimpleSpline spline)
        {
            if (spline == null || spline.pathMode != VFXSplinePathMode.Bezier || spline.bezierPoints == null)
                return;

            List<Vector3> points = GetHealthLocalPoints(spline);
            int count = Mathf.Min(spline.bezierPoints.Count, points.Count);
            Undo.RecordObject(spline, "Clamp Long Bezier Handles");
            for (int i = 0; i < count; i++)
            {
                VFXBezierPoint point = spline.bezierPoints[i];
                if (point == null || point.handleMode == VFXBezierHandleMode.AutoSmooth)
                    continue;

                float prevDistance = GetNeighborDistance(points, i, -1, spline.loop);
                float nextDistance = GetNeighborDistance(points, i, 1, spline.loop);
                point.inTangent = ClampBezierTangent(point.inTangent, prevDistance);
                point.outTangent = ClampBezierTangent(point.outTangent, nextDistance);
            }

            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static Vector3 ClampBezierTangent(Vector3 tangent, float neighborDistance)
        {
            if (neighborDistance <= HealthDuplicatePointDistance)
                return Vector3.zero;

            float warningLength = neighborDistance * HealthLongHandleRatio;
            if (tangent.magnitude <= warningLength)
                return tangent;

            float clampedLength = neighborDistance * HealthClampedHandleRatio;
            return tangent.normalized * clampedLength;
        }

        private class SplineHealthReport
        {
            public bool HasIssues;
            public bool HasIssueLocation;
            public int FirstIssuePointIndex = -1;
            public int SecondIssuePointIndex = -1;
            public int OverlappingPairCount;
            public int TooClosePairCount;
            public int LongBezierHandleCount;
            public bool CanEnableLoop;
            public bool HasLoopSeamAngleWarning;
            public string Message;
        }

        private void DrawShapePresetTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("路径形状预设 Shape Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一键生成常用路径形状。Apply 会覆盖当前 Local Points；Live Preview 开启后，参数变化会实时覆盖当前路径，建议复制路径后使用。", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            selectedShapePreset = (ShapePreset)EditorGUILayout.EnumPopup(new GUIContent("形状预设", "选择要生成的路径形状。"), selectedShapePreset);
            presetScale = EditorGUILayout.FloatField(new GUIContent("整体缩放", "整体缩放。"), Mathf.Max(0.01f, presetScale));
            presetWidth = EditorGUILayout.FloatField(new GUIContent("宽度", "横向宽度，主要用于 Line / S / Wave / Rectangle / Infinity 等形状。"), Mathf.Max(0.01f, presetWidth));
            presetHeight = EditorGUILayout.FloatField(new GUIContent("高度", "纵向高度，主要用于 S / Wave / Rectangle / Ellipse / Infinity 等形状。"), Mathf.Max(0.01f, presetHeight));
            presetPointCount = EditorGUILayout.IntSlider(new GUIContent("控制点数量", "生成点数量。圆、波浪、螺旋等形状建议 8 到 24。"), presetPointCount, 4, 64);
            presetWaveCount = EditorGUILayout.IntSlider(new GUIContent("波段数量", "Wave / Zigzag 的波段数量。"), presetWaveCount, 1, 8);
            presetSpiralTurns = EditorGUILayout.Slider(new GUIContent("螺旋圈数", "Spiral 形状的圈数。"), presetSpiralTurns, 0.5f, 5f);
            presetRotationY = EditorGUILayout.FloatField(new GUIContent("Y 轴旋转", "绕 Y 轴旋转预设形状。"), presetRotationY);
            presetOffset = EditorGUILayout.Vector3Field(new GUIContent("Local 偏移", "生成后在 Spline 本地空间中的偏移。"), presetOffset);
            livePreviewShapePreset = EditorGUILayout.Toggle(new GUIContent("实时预览形状预设", "开启后，修改上方参数会立即刷新当前路径。注意：这会持续覆盖 Local Points。"), livePreviewShapePreset);

            bool shapeParamChanged = EditorGUI.EndChangeCheck();

            if (livePreviewShapePreset)
            {
                EditorGUILayout.HelpBox("实时预览已开启：修改预设参数会实时覆盖当前 Local Points。正式路径建议先复制一份再试。", MessageType.Warning);
                if (shapeParamChanged)
                    ApplyShapePresetToSpline(spline, "Live Preview Shape Preset");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用形状预设"))
                    ApplyShapePresetToSpline(spline, "Apply Shape Preset");

                if (GUILayout.Button("重置预设参数"))
                {
                    presetScale = 3f;
                    presetWidth = 6f;
                    presetHeight = 3f;
                    presetRotationY = 0f;
                    presetOffset = Vector3.zero;
                    presetPointCount = 12;
                    presetWaveCount = 2;
                    presetSpiralTurns = 1.5f;
                    if (livePreviewShapePreset)
                        ApplyShapePresetToSpline(spline, "Reset Shape Preset Params");
                }
            }
        }

        private static void ApplyShapePresetToSpline(VFXSimpleSpline spline, string undoName)
        {
            ModifySpline(spline, undoName, () =>
            {
                spline.loop = IsClosedShapePreset(selectedShapePreset);
                spline.localPoints = GeneratePresetPoints(selectedShapePreset);
                if (IsCornerShapePreset(selectedShapePreset))
                {
                    spline.pathMode = VFXSplinePathMode.Bezier;
                    spline.ConvertCatmullRomToBezier();
                    ApplyCornerToAllBezierPoints(spline);
                    spline.showAllPointHandles = false;
                }
                else if (spline.pathMode == VFXSplinePathMode.Bezier)
                {
                    spline.ConvertCatmullRomToBezier();
                }
                int activePointCount = spline.GetActivePointCount();
                if (activePointCount > 0)
                {
                    spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, activePointCount - 1);
                    spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
                }
                spline.MarkDistanceCacheDirty();
            });
        }

        private static bool IsClosedShapePreset(ShapePreset preset)
        {
            switch (preset)
            {
                case ShapePreset.Circle:
                case ShapePreset.Ring:
                case ShapePreset.Ellipse:
                case ShapePreset.Square:
                case ShapePreset.Rectangle:
                case ShapePreset.Triangle:
                case ShapePreset.Diamond:
                case ShapePreset.Infinity:
                case ShapePreset.Star:
                case ShapePreset.Heart:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCornerShapePreset(ShapePreset preset)
        {
            switch (preset)
            {
                case ShapePreset.Square:
                case ShapePreset.Rectangle:
                case ShapePreset.Triangle:
                case ShapePreset.Diamond:
                case ShapePreset.Star:
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplyCornerToAllBezierPoints(VFXSimpleSpline spline)
        {
            if (spline == null || spline.bezierPoints == null)
                return;

            for (int i = 0; i < spline.bezierPoints.Count; i++)
            {
                VFXBezierPoint point = spline.bezierPoints[i];
                if (point == null)
                    continue;

                point.handleMode = VFXBezierHandleMode.Free;
                point.inTangent = Vector3.zero;
                point.outTangent = Vector3.zero;
            }
        }

        private static List<Vector3> GeneratePresetPoints(ShapePreset preset)
        {
            List<Vector3> pts = new List<Vector3>();
            int count = Mathf.Max(4, presetPointCount);
            float w = Mathf.Max(0.01f, presetWidth) * 0.5f;
            float h = Mathf.Max(0.01f, presetHeight) * 0.5f;
            float s = Mathf.Max(0.01f, presetScale);

            switch (preset)
            {
                case ShapePreset.Line:
                    pts.Add(new Vector3(-w, 0f, 0f));
                    pts.Add(new Vector3(w, 0f, 0f));
                    break;

                case ShapePreset.Arc:
                {
                    int n = Mathf.Max(5, count);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float angle = Mathf.Lerp(210f, -30f, t) * Mathf.Deg2Rad;
                        pts.Add(new Vector3(Mathf.Cos(angle) * w, 0f, Mathf.Sin(angle) * h));
                    }
                    break;
                }

                case ShapePreset.S_Curve:
                {
                    int n = Mathf.Max(5, count);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float x = Mathf.Lerp(-w, w, t);
                        float z = Mathf.Sin((t - 0.5f) * Mathf.PI * 2f) * h;
                        pts.Add(new Vector3(x, 0f, z));
                    }
                    break;
                }

                case ShapePreset.Wave:
                {
                    int n = Mathf.Max(6, count);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float x = Mathf.Lerp(-w, w, t);
                        float z = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(1, presetWaveCount)) * h;
                        pts.Add(new Vector3(x, 0f, z));
                    }
                    break;
                }

                case ShapePreset.Zigzag:
                {
                    int n = Mathf.Max(3, presetWaveCount * 2 + 1);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float x = Mathf.Lerp(-w, w, t);
                        float z = (i % 2 == 0 ? -h : h);
                        pts.Add(new Vector3(x, 0f, z));
                    }
                    break;
                }

                case ShapePreset.Circle:
                    AddClosedParametric(pts, count, t => new Vector3(Mathf.Cos(t * Mathf.PI * 2f) * s, 0f, Mathf.Sin(t * Mathf.PI * 2f) * s));
                    break;

                case ShapePreset.Ring:
                    AddClosedParametric(pts, Mathf.Max(16, count), t => new Vector3(Mathf.Cos(t * Mathf.PI * 2f) * s, 0f, Mathf.Sin(t * Mathf.PI * 2f) * s));
                    break;

                case ShapePreset.Ellipse:
                    AddClosedParametric(pts, count, t => new Vector3(Mathf.Cos(t * Mathf.PI * 2f) * w, 0f, Mathf.Sin(t * Mathf.PI * 2f) * h));
                    break;

                case ShapePreset.Square:
                    pts.Add(new Vector3(-s, 0f, -s));
                    pts.Add(new Vector3(s, 0f, -s));
                    pts.Add(new Vector3(s, 0f, s));
                    pts.Add(new Vector3(-s, 0f, s));
                    break;

                case ShapePreset.Rectangle:
                    pts.Add(new Vector3(-w, 0f, -h));
                    pts.Add(new Vector3(w, 0f, -h));
                    pts.Add(new Vector3(w, 0f, h));
                    pts.Add(new Vector3(-w, 0f, h));
                    break;

                case ShapePreset.Triangle:
                    pts.Add(new Vector3(0f, 0f, h));
                    pts.Add(new Vector3(w, 0f, -h));
                    pts.Add(new Vector3(-w, 0f, -h));
                    break;

                case ShapePreset.Diamond:
                    pts.Add(new Vector3(0f, 0f, h));
                    pts.Add(new Vector3(w, 0f, 0f));
                    pts.Add(new Vector3(0f, 0f, -h));
                    pts.Add(new Vector3(-w, 0f, 0f));
                    break;

                case ShapePreset.Infinity:
                {
                    int n = Mathf.Max(12, count);
                    AddClosedParametric(pts, n, t =>
                    {
                        float a = t * Mathf.PI * 2f;
                        return new Vector3(Mathf.Sin(a) * w, 0f, Mathf.Sin(a) * Mathf.Cos(a) * h);
                    });
                    break;
                }

                case ShapePreset.Spiral:
                {
                    int n = Mathf.Max(12, count);
                    float turns = Mathf.Max(0.5f, presetSpiralTurns);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float a = t * Mathf.PI * 2f * turns;
                        float r = Mathf.Lerp(0.15f, 1f, t) * s;
                        pts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    }
                    break;
                }

                case ShapePreset.Star:
                {
                    int tips = 5;
                    int n = tips * 2;
                    for (int i = 0; i < n; i++)
                    {
                        float a = (i / (float)n) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                        float r = (i % 2 == 0) ? s : s * 0.42f;
                        pts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    }
                    break;
                }

                case ShapePreset.Heart:
                {
                    int n = Mathf.Max(16, count);
                    for (int i = 0; i < n; i++)
                    {
                        float t = (i / (float)n) * Mathf.PI * 2f;
                        float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                        float z = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                        pts.Add(new Vector3(x / 16f * s, 0f, z / 16f * s));
                    }
                    break;
                }

                case ShapePreset.U_Shape:
                {
                    int n = Mathf.Max(8, count);
                    pts.Add(new Vector3(-w, 0f, h));
                    pts.Add(new Vector3(-w, 0f, 0f));
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)(n - 1);
                        float a = Mathf.Lerp(Mathf.PI, Mathf.PI * 2f, t);
                        pts.Add(new Vector3(Mathf.Cos(a) * w, 0f, Mathf.Sin(a) * h));
                    }
                    pts.Add(new Vector3(w, 0f, 0f));
                    pts.Add(new Vector3(w, 0f, h));
                    break;
                }
            }

            if (ShouldPresetStartAtOrigin(preset))
                MovePresetStartToOrigin(pts);

            ApplyPresetTransform(pts);
            if (pts.Count < 2)
            {
                pts.Clear();
                pts.Add(Vector3.zero);
                pts.Add(Vector3.right);
            }
            return pts;
        }

        private static void AddClosedParametric(List<Vector3> pts, int count, System.Func<float, Vector3> func)
        {
            count = Mathf.Max(4, count);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                pts.Add(func(t));
            }
        }

        private static bool ShouldPresetStartAtOrigin(ShapePreset preset)
        {
            switch (preset)
            {
                case ShapePreset.Line:
                case ShapePreset.Arc:
                case ShapePreset.S_Curve:
                case ShapePreset.Wave:
                case ShapePreset.Zigzag:
                case ShapePreset.Spiral:
                case ShapePreset.U_Shape:
                    return true;
                default:
                    return false;
            }
        }

        private static void MovePresetStartToOrigin(List<Vector3> pts)
        {
            if (pts == null || pts.Count == 0)
                return;

            Vector3 offset = pts[0];
            for (int i = 0; i < pts.Count; i++)
                pts[i] -= offset;
        }

        private static void ApplyPresetTransform(List<Vector3> pts)
        {
            Quaternion rot = Quaternion.Euler(0f, presetRotationY, 0f);
            for (int i = 0; i < pts.Count; i++)
                pts[i] = rot * pts[i] + presetOffset;
        }

        private void DrawUserPresetTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("自定义路径预设 User Path Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把当前手调好的 Local Points 保存为 .asset 资源，方便下次复用，也方便提交到 SVN/Git 给团队共享。", MessageType.Info);

            userPresetName = EditorGUILayout.TextField(new GUIContent("预设名称", "保存自定义路径预设时使用的文件名。"), userPresetName);
            selectedUserPreset = (VFXUserPathPreset)EditorGUILayout.ObjectField(new GUIContent("自定义路径预设", "选择已经保存的自定义路径预设资源。"), selectedUserPreset, typeof(VFXUserPathPreset), false);
            saveDisplaySettingsWithPreset = EditorGUILayout.Toggle(new GUIContent("同时保存显示设置", "开启后，保存路径点的同时保存路径颜色、线宽、采样精度等显示参数。一般只保存 Local Points 即可。"), saveDisplaySettingsWithPreset);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存当前路径为预设"))
                    SaveCurrentPathAsPreset(spline);

                EditorGUI.BeginDisabledGroup(selectedUserPreset == null);
                if (GUILayout.Button("加载选中预设"))
                    LoadSelectedPreset(spline);
                EditorGUI.EndDisabledGroup();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(selectedUserPreset == null);
                if (GUILayout.Button("删除选中预设"))
                    DeleteSelectedPreset();
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("刷新预设列表"))
                    AssetDatabase.Refresh();
            }

            EditorGUILayout.HelpBox("保存位置：" + UserPresetFolder + "\n提示：加载预设会覆盖当前 Local Points，操作支持 Undo。", MessageType.None);
        }

        private static void EnsureUserPresetFolder()
        {
            if (AssetDatabase.IsValidFolder(UserPresetFolder)) return;

            string[] parts = UserPresetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeAssetName(string raw)
        {
            string name = string.IsNullOrWhiteSpace(raw) ? "SplinePathPreset" : raw.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "_");
            return name;
        }

        private static void SaveCurrentPathAsPreset(VFXSimpleSpline spline)
        {
            if (spline == null) return;

            EnsureUserPresetFolder();
            string safeName = SanitizeAssetName(userPresetName);
            string path = AssetDatabase.GenerateUniqueAssetPath(UserPresetFolder + "/" + safeName + ".asset");

            VFXUserPathPreset preset = ScriptableObject.CreateInstance<VFXUserPathPreset>();
            preset.CaptureFrom(spline, safeName, saveDisplaySettingsWithPreset);

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            selectedUserPreset = preset;
            EditorGUIUtility.PingObject(preset);
        }

        private static void LoadSelectedPreset(VFXSimpleSpline spline)
        {
            if (spline == null || selectedUserPreset == null) return;

            ModifySpline(spline, "Load User Path Preset", () =>
            {
                selectedUserPreset.ApplyTo(spline);
            });
        }

        private static void DeleteSelectedPreset()
        {
            if (selectedUserPreset == null) return;

            string path = AssetDatabase.GetAssetPath(selectedUserPreset);
            if (string.IsNullOrEmpty(path)) return;

            bool ok = EditorUtility.DisplayDialog("删除自定义路径预设", "确认删除这个自定义路径预设？\n" + path, "删除", "取消");
            if (!ok) return;

            selectedUserPreset = null;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void DrawAnchorTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("特效挂点工具 Anchor Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于快速在当前路径上创建 VFX Spline Anchor。粒子、面片、爆点可以挂到 Anchor 子物体下，再用 Timeline 原生 Control Track / Activation Track 控制播放。", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建 0%")) CreateAnchorAtProgress(spline, 0f);
                if (GUILayout.Button("创建 25%")) CreateAnchorAtProgress(spline, 0.25f);
                if (GUILayout.Button("创建 50%")) CreateAnchorAtProgress(spline, 0.5f);
                if (GUILayout.Button("创建 75%")) CreateAnchorAtProgress(spline, 0.75f);
                if (GUILayout.Button("创建 100%")) CreateAnchorAtProgress(spline, 1f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                batchAnchorCount = EditorGUILayout.IntField(new GUIContent("批量数量", "批量创建 Anchor 的数量。"), batchAnchorCount);
                batchAnchorCount = Mathf.Clamp(batchAnchorCount, 1, MaxBatchAnchorCount);
            }

            if (GUILayout.Button("均分创建 Anchor"))
                CreateEvenlySpacedAnchors(spline, batchAnchorCount);

            if (GUILayout.Button("隐藏本路径 Anchor 标记"))
                SetSplineAnchorsSceneLabel(spline, false);

            if (GUILayout.Button("修正本路径 Anchor 缩放"))
                ResetSplineAnchorScale(spline);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("跟随 Animator 批量 Offset", EditorStyles.boldLabel);
            batchAnchorSourceAnimator = (VFXSplineAnimator)EditorGUILayout.ObjectField(new GUIContent("Source Animator", "Anchor 会跟随这个 VFXSplineAnimator 的当前 Progress。"), batchAnchorSourceAnimator, typeof(VFXSplineAnimator), true);
            batchAnchorChildTemplate = (GameObject)EditorGUILayout.ObjectField(new GUIContent("子模型模板", "可选。批量创建时会复制一份到每个 Anchor 下面。"), batchAnchorChildTemplate, typeof(GameObject), true);
            batchAnchorRemoveTemplateSplineComponents = EditorGUILayout.Toggle(new GUIContent("移除复制件路径组件", "开启后，会移除复制出来的子模型上的 VFXSplineAnimator / VFXSplineAnchor，让父 Anchor 负责跟随。"), batchAnchorRemoveTemplateSplineComponents);
            batchAnchorStartOffset = EditorGUILayout.FloatField(new GUIContent("起始 Offset", "第一个 Anchor 的 Progress Offset。"), batchAnchorStartOffset);
            batchAnchorOffsetStep = EditorGUILayout.FloatField(new GUIContent("Offset 步长", "每生成一个 Anchor 后递增的 Offset。例如 -0.01 表示每个 Anchor 比前一个落后 1%。"), batchAnchorOffsetStep);
            batchAnchorWrapMode = (VFXSplineAnchorProgressWrapMode)EditorGUILayout.EnumPopup(new GUIContent("循环模式", "Offset 后的最终 Progress 超出 0-1 时如何处理。"), batchAnchorWrapMode);

            using (new EditorGUI.DisabledScope(batchAnchorSourceAnimator == null))
            {
                if (GUILayout.Button("固定本路径 Anchor 当前主物体旋转/缩放"))
                    SyncSplineAnchorRotationSettings(spline, batchAnchorSourceAnimator);

                if (GUILayout.Button("按 Offset 批量创建 Follow Anchor"))
                    CreateOffsetFollowAnchors(spline, batchAnchorCount, batchAnchorSourceAnimator, batchAnchorChildTemplate, batchAnchorRemoveTemplateSplineComponents, batchAnchorStartOffset, batchAnchorOffsetStep, batchAnchorWrapMode);
            }
        }

        private static void CreateAnchorAtProgress(VFXSimpleSpline spline, float progress)
        {
            if (spline == null) return;

            GameObject go = new GameObject("VFX Spline Anchor_" + Mathf.RoundToInt(progress * 100f).ToString("000"));
            Undo.RegisterCreatedObjectUndo(go, "Create VFX Spline Anchor");
            go.transform.SetParent(spline.transform.parent, false);
            go.transform.localScale = Vector3.one;

            VFXSplineAnchor anchor = go.AddComponent<VFXSplineAnchor>();
            anchor.spline = spline;
            anchor.progress = Mathf.Clamp01(progress);
            anchor.useDistanceBasedProgress = true;
            anchor.showSceneLabel = false;
            anchor.label = go.name;
            anchor.ApplyAnchor();
            go.transform.localScale = Vector3.one;

            EditorUtility.SetDirty(anchor);
            Selection.activeGameObject = go;
            SceneView.RepaintAll();
        }

        private static void CreateEvenlySpacedAnchors(VFXSimpleSpline spline, int count)
        {
            if (spline == null) return;
            count = Mathf.Clamp(count, 1, MaxBatchAnchorCount);

            GameObject group = new GameObject(spline.name + "_Anchors");
            Undo.RegisterCreatedObjectUndo(group, "Create Spline Anchor Group");
            group.transform.SetParent(spline.transform.parent, true);
            group.transform.position = Vector3.zero;
            group.transform.rotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;

            for (int i = 0; i < count; i++)
            {
                float progress = count == 1 ? 0f : i / (float)(count - 1);
                GameObject go = new GameObject("VFX Spline Anchor_" + Mathf.RoundToInt(progress * 100f).ToString("000"));
                Undo.RegisterCreatedObjectUndo(go, "Create VFX Spline Anchor");
                go.transform.SetParent(group.transform, false);
                go.transform.localScale = Vector3.one;

                VFXSplineAnchor anchor = go.AddComponent<VFXSplineAnchor>();
                anchor.spline = spline;
                anchor.progress = progress;
                anchor.useDistanceBasedProgress = true;
                anchor.showSceneLabel = false;
                anchor.label = go.name;
                anchor.ApplyAnchor();
                go.transform.localScale = Vector3.one;
                EditorUtility.SetDirty(anchor);
            }

            Selection.activeGameObject = group;
            SceneView.RepaintAll();
        }

        private static void CreateOffsetFollowAnchors(VFXSimpleSpline spline, int count, VFXSplineAnimator sourceAnimator, GameObject childTemplate, bool removeTemplateSplineComponents, float startOffset, float offsetStep, VFXSplineAnchorProgressWrapMode wrapMode)
        {
            if (spline == null || sourceAnimator == null) return;
            count = Mathf.Clamp(count, 1, MaxBatchAnchorCount);

            GameObject group = new GameObject(spline.name + "_FollowOffsetAnchors");
            Undo.RegisterCreatedObjectUndo(group, "Create Follow Offset Anchor Group");
            group.transform.SetParent(spline.transform.parent, true);
            group.transform.position = Vector3.zero;
            group.transform.rotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;

            for (int i = 0; i < count; i++)
            {
                float offset = startOffset + offsetStep * i;
                GameObject go = new GameObject("VFX Spline Anchor_Offset_" + i.ToString("000"));
                Undo.RegisterCreatedObjectUndo(go, "Create VFX Spline Anchor");
                go.transform.SetParent(group.transform, false);
                go.transform.localScale = Vector3.one;

                VFXSplineAnchor anchor = go.AddComponent<VFXSplineAnchor>();
                anchor.spline = spline;
                anchor.anchorMode = VFXSplineAnchorMode.FollowAnimatorProgress;
                anchor.sourceAnimator = sourceAnimator;
                anchor.autoUseSourceSpline = true;
                anchor.progressOffset = offset;
                anchor.progressWrapMode = wrapMode;
                anchor.useDistanceBasedProgress = true;
                anchor.showSceneLabel = false;
                anchor.label = go.name;
                ConfigureAnchorToFollowSourceTransform(sourceAnimator, anchor);
                anchor.ApplyAnchor();
                go.transform.localScale = sourceAnimator.transform.localScale;
                CreateAnchorChildFromTemplate(anchor.transform, childTemplate, sourceAnimator.transform, removeTemplateSplineComponents);
                EditorUtility.SetDirty(anchor);
            }

            Selection.activeGameObject = group;
            SceneView.RepaintAll();
        }

        private static void ConfigureAnchorToFollowSourceTransform(VFXSplineAnimator sourceAnimator, VFXSplineAnchor anchor)
        {
            if (sourceAnimator == null || anchor == null) return;

            anchor.rotationMode = sourceAnimator.rotationMode;
            anchor.forwardAxis = sourceAnimator.forwardAxis;
            anchor.rotationOffsetEuler = sourceAnimator.rotationOffsetEuler;
            anchor.fallbackForward = sourceAnimator.fallbackForward;
            anchor.ignoreSplineTransformRotation = sourceAnimator.ignoreSplineTransformRotation;
            anchor.followSourceRotation = false;
            anchor.followSourceScale = false;
        }

        private static void CreateAnchorChildFromTemplate(Transform parent, GameObject template, Transform sourceTransform, bool removeSplineComponents)
        {
            if (parent == null || template == null) return;

            GameObject child = null;
            if (AssetDatabase.Contains(template))
                child = PrefabUtility.InstantiatePrefab(template) as GameObject;
            if (child == null)
                child = UnityEngine.Object.Instantiate(template);

            Undo.RegisterCreatedObjectUndo(child, "Create Anchor Child");
            child.name = template.name;
            child.transform.SetParent(parent, false);

            if (removeSplineComponents)
                RemoveSplineComponents(child);

            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }

        private static void RemoveSplineComponents(GameObject root)
        {
            if (root == null) return;

            VFXSplineAnimator[] animators = root.GetComponentsInChildren<VFXSplineAnimator>(true);
            for (int i = 0; i < animators.Length; i++)
                Undo.DestroyObjectImmediate(animators[i]);

            VFXSplineAnchor[] anchors = root.GetComponentsInChildren<VFXSplineAnchor>(true);
            for (int i = 0; i < anchors.Length; i++)
                Undo.DestroyObjectImmediate(anchors[i]);
        }

        private static void SetSplineAnchorsSceneLabel(VFXSimpleSpline spline, bool show)
        {
            if (spline == null) return;

            VFXSplineAnchor[] anchors = UnityEngine.Object.FindObjectsOfType<VFXSplineAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                VFXSplineAnchor anchor = anchors[i];
                if (anchor == null || anchor.GetActiveSpline() != spline) continue;

                Undo.RecordObject(anchor, "Set Anchor Scene Label");
                anchor.showSceneLabel = show;
                EditorUtility.SetDirty(anchor);
            }

            SceneView.RepaintAll();
        }

        private static void ResetSplineAnchorScale(VFXSimpleSpline spline)
        {
            if (spline == null) return;

            VFXSplineAnchor[] anchors = UnityEngine.Object.FindObjectsOfType<VFXSplineAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                VFXSplineAnchor anchor = anchors[i];
                if (anchor == null || anchor.GetActiveSpline() != spline) continue;

                Undo.RecordObject(anchor.transform, "Reset Anchor Scale");
                anchor.transform.localScale = GetAnchorResetScale(anchor);
                anchor.ApplyAnchor();
                anchor.transform.localScale = GetAnchorResetScale(anchor);
                EditorUtility.SetDirty(anchor);
            }

            SceneView.RepaintAll();
        }

        private static Vector3 GetAnchorResetScale(VFXSplineAnchor anchor)
        {
            if (anchor != null && anchor.followSourceScale && anchor.sourceAnimator != null)
                return anchor.sourceAnimator.transform.localScale;

            return Vector3.one;
        }

        private static void SyncSplineAnchorRotationSettings(VFXSimpleSpline spline, VFXSplineAnimator sourceAnimator)
        {
            if (spline == null || sourceAnimator == null) return;

            VFXSplineAnchor[] anchors = UnityEngine.Object.FindObjectsOfType<VFXSplineAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                VFXSplineAnchor anchor = anchors[i];
                if (anchor == null || anchor.GetActiveSpline() != spline) continue;
                if (anchor.anchorMode == VFXSplineAnchorMode.FollowAnimatorProgress && anchor.sourceAnimator != sourceAnimator) continue;

                Undo.RecordObject(anchor, "Sync Anchor Rotation Settings");
                Undo.RecordObject(anchor.transform, "Apply Anchor Rotation Settings");
                ConfigureAnchorToFollowSourceTransform(sourceAnimator, anchor);
                anchor.ApplyAnchor();
                EditorUtility.SetDirty(anchor);
            }

            SceneView.RepaintAll();
        }

        private void DrawPointTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("控制点工具", EditorStyles.boldLabel);
            int pointCount = spline.GetActivePointCount();
            if (pointCount <= 0) return;

            DrawSelectedBezierPointTools(spline, pointCount);

            for (int i = 0; i < pointCount; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("点 " + i, GUILayout.Width(80));
                    if (GUILayout.Button("在后方插入"))
                    {
                        int index = i + 1;
                        ModifySpline(spline, "Insert Point After", () => spline.InsertPoint(index));
                        break;
                    }
                    EditorGUI.BeginDisabledGroup(pointCount <= 2);
                    if (GUILayout.Button("删除"))
                    {
                        int index = i;
                        ModifySpline(spline, "Delete Point", () => spline.RemovePointAt(index));
                        break;
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private static void DrawSelectedBezierPointTools(VFXSimpleSpline spline, int pointCount)
        {
            if (spline.pathMode != VFXSplinePathMode.Bezier || spline.bezierPoints == null || spline.bezierPoints.Count == 0)
                return;

            int selected = Mathf.Clamp(spline.selectedPointIndex, 0, pointCount - 1);
            if (selected < 0 || selected >= spline.bezierPoints.Count || spline.bezierPoints[selected] == null)
                return;

            VFXBezierPoint point = spline.bezierPoints[selected];

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("当前 Bezier 点 " + selected, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            VFXBezierHandleMode mode = (VFXBezierHandleMode)EditorGUILayout.EnumPopup(new GUIContent("手柄模式", "控制当前 Bezier 点入/出手柄的联动方式。"), point.handleMode);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Change Bezier Handle Mode");
                spline.SetBezierHandleMode(selected, mode);
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("自由手柄"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.Free);

                if (GUILayout.Button("对齐"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.Aligned);

                if (GUILayout.Button("镜像"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.Mirrored);

                if (GUILayout.Button("自动平滑"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.AutoSmooth);
            }

            EditorGUILayout.LabelField("点类型预设", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("拐角"))
                    ApplySelectedBezierPointPreset(spline, selected, VFXBezierPointPreset.Corner);

                if (GUILayout.Button("平滑"))
                    ApplySelectedBezierPointPreset(spline, selected, VFXBezierPointPreset.Smooth);

                if (GUILayout.Button("对称"))
                    ApplySelectedBezierPointPreset(spline, selected, VFXBezierPointPreset.Symmetric);

                if (GUILayout.Button("自动"))
                    ApplySelectedBezierPointPreset(spline, selected, VFXBezierPointPreset.AutoSmooth);
            }

            if (GUILayout.Button("自动平滑全部 Bezier 点"))
            {
                Undo.RecordObject(spline, "Auto Smooth All Bezier Points");
                spline.AutoSmoothAllBezierPoints();
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }
        }

        private static void SetSelectedBezierHandleMode(VFXSimpleSpline spline, int selected, VFXBezierHandleMode mode)
        {
            Undo.RecordObject(spline, "Change Bezier Handle Mode");
            spline.SetBezierHandleMode(selected, mode);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void ApplySelectedBezierPointPreset(VFXSimpleSpline spline, int selected, VFXBezierPointPreset preset)
        {
            Undo.RecordObject(spline, "Apply Bezier Point Preset");
            spline.ApplyBezierPointPreset(selected, preset);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoad]
    public static class VFXSplineSceneDrawer
    {
        private static double suppressBezierToolbarUntil;
        private static Rect currentBezierToolbarRect;
        private static int currentBezierToolbarButtonIndex;
        private static bool boxSelecting;
        private static VFXSimpleSpline boxSelectingSpline;
        private static Vector2 boxSelectStart;
        private static Vector2 boxSelectCurrent;
        private static bool appendPointMode;
        private static VFXSimpleSpline appendPointModeSpline;
        private static bool suppressObjectGizmoDuringPointDrag;
        private static Vector2 lastSceneMousePosition = new Vector2(120f, 120f);
        private const float SceneStatusDuplicatePointDistance = 0.001f;
        private const float SceneStatusClosePointDistance = 0.05f;
        private const float SceneStatusLongHandleRatio = 1.25f;
        private const float SceneStatusLoopSeamAngle = 60f;
        private static readonly List<VFXSimpleSpline> cachedSplines = new List<VFXSimpleSpline>();
        private static bool splinesCacheDirty = true;

        static VFXSplineSceneDrawer()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
            EditorApplication.hierarchyChanged -= MarkSplinesCacheDirty;
            EditorApplication.hierarchyChanged += MarkSplinesCacheDirty;
        }

        private static void MarkSplinesCacheDirty()
        {
            splinesCacheDirty = true;
        }

        private static void RefreshSplinesCacheIfNeeded()
        {
            if (!splinesCacheDirty)
                return;

            cachedSplines.Clear();
            cachedSplines.AddRange(Object.FindObjectsOfType<VFXSimpleSpline>());
            splinesCacheDirty = false;
        }

        public static void ToggleAppendPointModeFromShortcut()
        {
            VFXSimpleSpline spline = VFXSplinePointAPI.ActiveSpline;
            if (spline == null)
                return;

            if (!VFXSplinePointAPI.IsPointEditingActive)
                VFXSplinePointAPI.EnterPointMode(spline);
            else
                VFXSplinePointAPI.SetActiveSpline(spline);
            bool currentSplineOwnsMode = appendPointMode && appendPointModeSpline == spline;
            appendPointMode = !currentSplineOwnsMode;
            appendPointModeSpline = appendPointMode ? spline : null;
            SceneView.RepaintAll();
        }

        public static void ExitAppendPointMode()
        {
            if (!appendPointMode && appendPointModeSpline == null)
                return;

            appendPointMode = false;
            appendPointModeSpline = null;
            SceneView.RepaintAll();
        }

        public static void OpenContextMenuFromShortcut()
        {
            VFXSimpleSpline spline = VFXSplinePointAPI.ActiveSpline;
            if (spline == null)
                return;

            if (!VFXSplinePointAPI.IsPointEditingActive)
                VFXSplinePointAPI.EnterPointMode(spline);
            else
                VFXSplinePointAPI.SetActiveSpline(spline);
            int count = spline.GetActivePointCount();
            int pointIndex;
            if (TryFindNearestPointIndex(spline, count, lastSceneMousePosition, out pointIndex))
            {
                ShowPointContextMenu(spline, pointIndex);
                return;
            }

            float rawProgress;
            if (TryFindNearestRawProgressOnCurve(spline, lastSceneMousePosition, out rawProgress))
            {
                InsertPointOnCurveAtRawProgress(spline, rawProgress);
                return;
            }

            if (count > 0)
            {
                ShowPointContextMenu(spline, Mathf.Clamp(spline.selectedPointIndex, 0, count - 1));
                return;
            }

            SceneView.RepaintAll();
        }

        private static void DuringSceneGUI(SceneView view)
        {
            Event e = Event.current;
            if (e == null) return;

            RefreshSplinesCacheIfNeeded();
            VFXSimpleSpline selectedSpline = VFXSplinePointAPI.GetSelectedSpline();
            for (int i = cachedSplines.Count - 1; i >= 0; i--)
            {
                VFXSimpleSpline spline = cachedSplines[i];
                if (spline == null)
                {
                    cachedSplines.RemoveAt(i);
                    continue;
                }

                if (spline == selectedSpline) continue;
                if (!spline.alwaysShowPathInSceneView) continue;
                DrawSpline(spline, false);
            }

            if (selectedSpline != null)
            {
                DrawSpline(selectedSpline, true);
                DrawEditablePoints(selectedSpline);
            }
        }

        public static void DrawSpline(VFXSimpleSpline spline, bool selected)
        {
            if (spline == null || spline.GetActivePointCount() < 2) return;

            HandleSplineLineSelection(spline, selected);

            Event e = Event.current;
            if (e == null || e.type != EventType.Repaint)
                return;

            int steps = Mathf.Max(8, spline.resolution);
            Vector3[] points = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
                points[i] = spline.GetPoint(i / (float)steps, false);

            Handles.color = spline.pathColor;
            Handles.DrawAAPolyLine(Mathf.Max(1f, spline.lineWidth), points);

            if (spline.showProgressMarks)
                DrawProgressMarks(spline);

            if (spline.showDirectionArrows)
                DrawDirectionArrows(spline);

            if (spline.showNormals)
                DrawNormals(spline);
        }

        private static void HandleSplineLineSelection(VFXSimpleSpline spline, bool selected)
        {
            Event e = Event.current;
            if (e == null || spline == null || selected || e.alt)
                return;

            int controlId = GUIUtility.GetControlID(spline.GetInstanceID(), FocusType.Passive);
            if (e.type == EventType.Layout)
            {
                float rawProgress;
                float distance;
                if (!TryFindNearestRawProgressOnCurve(spline, e.mousePosition, out rawProgress, out distance))
                    distance = float.MaxValue;
                HandleUtility.AddControl(controlId, distance);
                return;
            }

            if (e.type != EventType.MouseDown || e.button != 0 || HandleUtility.nearestControl != controlId)
                return;

            Selection.activeGameObject = spline.gameObject;
            VFXSplinePointAPI.SetActiveSpline(spline);
            e.Use();
            SceneView.RepaintAll();
        }

        private static void DrawProgressMarks(VFXSimpleSpline spline)
        {
            Handles.color = spline.progressMarkColor;
            int count = Mathf.Max(1, spline.progressMarkCount);
            int max = spline.loop ? count - 1 : count;
            for (int i = 0; i <= max; i++)
            {
                float p = i / (float)count;
                Vector3 pos = spline.GetPoint(p, spline.progressMarksUseDistance);
                float size = HandleUtility.GetHandleSize(pos) * spline.pointSize * 0.9f;
                Handles.SphereHandleCap(0, pos, Quaternion.identity, size, EventType.Repaint);
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = spline.progressMarkColor;
                Handles.Label(pos + Vector3.up * size * 2f, Mathf.RoundToInt(p * 100f) + "%", style);
            }
        }

        private static void DrawDirectionArrows(VFXSimpleSpline spline)
        {
            Handles.color = Color.white;
            int count = Mathf.Max(1, spline.arrowCount);
            for (int i = 1; i <= count; i++)
            {
                float p = i / (float)(count + 1);
                Vector3 pos = spline.GetPoint(p, spline.progressMarksUseDistance);
                Vector3 tangent = spline.GetTangent(p, spline.progressMarksUseDistance);
                float size = HandleUtility.GetHandleSize(pos) * spline.arrowSize;
                Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(tangent, Vector3.up), size, EventType.Repaint);
            }
        }

        private static void DrawNormals(VFXSimpleSpline spline)
        {
            Handles.color = spline.normalColor;
            int count = Mathf.Max(1, spline.normalCount);
            for (int i = 1; i <= count; i++)
            {
                float p = i / (float)(count + 1);
                Vector3 pos = spline.GetPoint(p, spline.progressMarksUseDistance);
                Vector3 normal = spline.GetNormal(p, spline.progressMarksUseDistance);
                float length = HandleUtility.GetHandleSize(pos) * spline.normalLength;
                Handles.DrawAAPolyLine(2f, pos, pos + normal * length);
            }
        }

        public static void DrawEditablePoints(VFXSimpleSpline spline)
        {
            if (spline == null) return;

            Event currentEvent = Event.current;
            if (currentEvent != null)
                lastSceneMousePosition = currentEvent.mousePosition;

            int count = spline.GetActivePointCount();
            if (count == 0) return;

            if (spline.selectedPointIndex < 0 || spline.selectedPointIndex >= count)
                spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, count - 1);
            NormalizeSelectedPointIndices(spline, count);

            bool mouseNearPoint = currentEvent != null && IsMouseNearAnyPoint(spline, count, currentEvent.mousePosition);
            bool mouseNearBezierHandle = currentEvent != null && IsMouseNearVisibleBezierTangentHandle(spline, count, currentEvent.mousePosition);
            bool mouseNearMultiPointHandle = currentEvent != null && IsMouseNearMultiPointMoveHandle(spline, count, currentEvent.mousePosition);
            if (currentEvent != null)
            {
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && !currentEvent.alt && (mouseNearPoint || mouseNearBezierHandle || mouseNearMultiPointHandle))
                    suppressObjectGizmoDuringPointDrag = true;
                else if (currentEvent.type == EventType.MouseUp || currentEvent.type == EventType.Ignore)
                    suppressObjectGizmoDuringPointDrag = false;
            }
            Tools.hidden = suppressObjectGizmoDuringPointDrag || mouseNearBezierHandle || mouseNearMultiPointHandle;

            if (VFXSplinePointAPI.HandleShortcut(Event.current, spline))
                return;

            DrawSceneStatusHint(spline, count);

            if (HandleAppendPointMode(spline, count))
                return;

            if (HandleBoxSelection(spline, count))
                return;

            bool hasMultiSelection = spline.selectedPointIndices != null && spline.selectedPointIndices.Count > 1;
            if (hasMultiSelection)
                DrawMultiPointMoveHandle(spline, count);

            for (int i = 0; i < count; i++)
            {
                Vector3 world = spline.GetEffectiveWorldPoint(i);
                bool isDynamicBoundPoint = spline.IsPointDynamicallyBound(i);
                Transform boundTransform = spline.GetDynamicBindingTransformForPoint(i);
                bool isPrimarySelectedPoint = spline.selectedPointIndex == i;
                bool isMultiSelectedPoint = IsPointMultiSelected(spline, i);
                bool isSelectedPoint = spline.showAllPointHandles || isPrimarySelectedPoint || isMultiSelectedPoint;
                float baseSize = HandleUtility.GetHandleSize(world) * spline.pointSize;
                float size = isPrimarySelectedPoint || isMultiSelectedPoint ? baseSize * 1.6f : baseSize;
                float pickSize = Mathf.Max(size * 1.25f, baseSize * VFXSplinePointAPI.PickSizeMultiplier);

                Handles.color = isDynamicBoundPoint ? new Color(0.2f, 1f, 0.35f, 1f) : (isSelectedPoint ? Color.yellow : spline.pathColor);

                // 未选中的控制点只显示小球；选中后才显示 PositionHandle。
                bool additiveSelect = Event.current != null && (Event.current.control || Event.current.command);
                if (Handles.Button(world, Quaternion.identity, size, pickSize, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(spline, "Select Spline Point");
                    if (additiveSelect)
                        TogglePointSelection(spline, i);
                    else
                        SetSinglePointSelection(spline, i);
                    EditorUtility.SetDirty(spline);
                    SceneView.RepaintAll();
                }

                if (spline.showPointLabels)
                {
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                    style.normal.textColor = isDynamicBoundPoint ? new Color(0.2f, 1f, 0.35f, 1f) : (isSelectedPoint ? Color.yellow : Color.white);
                    string label = isSelectedPoint ? (i + "  ●") : i.ToString();
                    if (isDynamicBoundPoint && spline.showDynamicBindingLabels)
                        label += i == 0 ? "  Start Bound" : "  End Bound";
                    Handles.Label(world + Vector3.up * size * 1.5f, label, style);
                }

                if (!isSelectedPoint)
                    continue;

                if (spline.pathMode == VFXSplinePathMode.Bezier && !hasMultiSelection)
                {
                    DrawBezierHandles(spline, i, count, world, size);
                    if (isPrimarySelectedPoint)
                        DrawBezierHandleModeToolbar(spline, i, world, size);
                }

                if (hasMultiSelection)
                    continue;

                EditorGUI.BeginChangeCheck();
                Vector3 newWorld = Handles.PositionHandle(world, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    if (isDynamicBoundPoint && boundTransform != null)
                    {
                        Undo.RecordObject(boundTransform, "Move Dynamic Binding Transform");
                        boundTransform.position = newWorld;
                        EditorUtility.SetDirty(boundTransform);
                    }
                    else
                    {
                        Undo.RecordObject(spline, "Move Spline Point");
                        spline.SetActivePointWorldPosition(i, newWorld);
                        EditorUtility.SetDirty(spline);
                    }

                    spline.MarkDistanceCacheDirty();
                    SceneView.RepaintAll();
                }
            }

            TryForceSelectNearestPoint(spline, count);
        }

        private static bool TryForceSelectNearestPoint(VFXSimpleSpline spline, int count)
        {
            Event e = Event.current;
            if (e == null || e.alt || e.type != EventType.MouseDown || e.button != 0 || spline == null || count <= 0)
                return false;

            if (IsMouseNearVisibleBezierTangentHandle(spline, count, e.mousePosition))
                return false;

            if (IsMouseNearMultiPointMoveHandle(spline, count, e.mousePosition))
                return false;

            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            float maxDistance = Mathf.Max(14f, 12f * VFXSplinePointAPI.PickSizeMultiplier);
            for (int i = 0; i < count; i++)
            {
                Vector2 pointGui = HandleUtility.WorldToGUIPoint(spline.GetEffectiveWorldPoint(i));
                float distance = Vector2.Distance(e.mousePosition, pointGui);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestDistance > maxDistance)
                return false;

            Undo.RecordObject(spline, "Select Spline Point");
            bool additiveSelect = e.control || e.command;
            if (additiveSelect)
                TogglePointSelection(spline, bestIndex);
            else
                SetSinglePointSelection(spline, bestIndex);
            EditorUtility.SetDirty(spline);
            e.Use();
            SceneView.RepaintAll();
            return true;
        }

        private static void DrawSceneStatusHint(VFXSimpleSpline spline, int count)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.Repaint || spline == null)
                return;

            bool pointMode = VFXSplinePointAPI.IsPointEditingActive;
            bool appendModeActive = appendPointMode && appendPointModeSpline == spline;
            int selectedCount = spline.selectedPointIndices != null ? spline.selectedPointIndices.Count : 0;
            string modeName = pointMode ? (appendModeActive ? "\u8ffd\u52a0\u70b9" : "Spline \u7f16\u8f91") : "Spline";
            string pathName = spline.pathMode == VFXSplinePathMode.Bezier ? "Bezier" : "Catmull-Rom";
            string loopInfo = spline.loop ? "Loop On" : "Loop Off";
            string selectionInfo = selectedCount > 1 ? "\u5df2\u9009 " + selectedCount + " \u4e2a\u70b9" : "\u5f53\u524d\u70b9 " + Mathf.Clamp(spline.selectedPointIndex, 0, Mathf.Max(0, count - 1));
            string title = "VFX Spline | " + modeName + " | " + pathName + " | " + loopInfo;
            string status = selectionInfo + " | \u5171 " + count + " \u4e2a\u70b9 | " + BuildSceneHealthSummary(spline, count);
            string shortcuts = appendModeActive
                ? "\u5de6\u952e\uff1a\u6dfb\u52a0\u70b9    A / Esc\uff1a\u9000\u51fa\u8ffd\u52a0    Alt + \u9f20\u6807\uff1a\u89c6\u89d2"
                : "A\uff1a\u8ffd\u52a0\u6a21\u5f0f    M\uff1a\u70b9\u83dc\u5355/\u7ebf\u6bb5\u63d2\u70b9    Ctrl\uff1a\u591a\u9009/\u6846\u9009    F\uff1a\u805a\u7126    Del\uff1a\u5220\u9664";

            Rect rect = new Rect(10f, 10f, 680f, 62f);
            Handles.BeginGUI();
            Color oldColor = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.82f);
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            GUI.color = oldColor;

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.normal.textColor = Color.white;
            GUIStyle bodyStyle = new GUIStyle(EditorStyles.miniLabel);
            bodyStyle.normal.textColor = new Color(0.86f, 0.86f, 0.86f, 1f);

            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 18f), title, titleStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, 16f), status, bodyStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 42f, rect.width - 16f, 16f), shortcuts, bodyStyle);
            Handles.EndGUI();
        }

        private static string BuildSceneHealthSummary(VFXSimpleSpline spline, int count)
        {
            if (spline == null || count < 2)
                return "\u5065\u5eb7\uff1a\u63a7\u5236\u70b9\u4e0d\u8db3";

            int duplicatePairs = 0;
            int closePairs = 0;
            for (int i = 0; i < count - 1; i++)
                CountScenePointDistanceIssue(GetSceneStatusLocalPoint(spline, i), GetSceneStatusLocalPoint(spline, i + 1), ref duplicatePairs, ref closePairs);

            if (spline.loop && count > 2)
                CountScenePointDistanceIssue(GetSceneStatusLocalPoint(spline, count - 1), GetSceneStatusLocalPoint(spline, 0), ref duplicatePairs, ref closePairs);

            int longHandles = spline.pathMode == VFXSplinePathMode.Bezier ? CountSceneLongBezierHandles(spline, count) : 0;
            bool loopSeamWarning = HasSceneLoopSeamWarning(spline, count);

            if (duplicatePairs == 0 && closePairs == 0 && longHandles == 0 && !loopSeamWarning)
                return "\u5065\u5eb7\uff1aOK";

            List<string> issues = new List<string>();
            if (duplicatePairs > 0)
                issues.Add(duplicatePairs + " \u5904\u91cd\u53e0");
            if (closePairs > 0)
                issues.Add(closePairs + " \u5904\u8fc7\u8fd1");
            if (longHandles > 0)
                issues.Add(longHandles + " \u4e2a\u957f\u624b\u67c4");
            if (loopSeamWarning)
                issues.Add("Loop \u5207\u7ebf\u8df3\u53d8");

            return "\u5065\u5eb7\uff1a" + string.Join(" / ", issues.ToArray());
        }

        private static Vector3 GetSceneStatusLocalPoint(VFXSimpleSpline spline, int index)
        {
            return spline.pathMode == VFXSplinePathMode.Bezier
                ? spline.GetEffectiveBezierPosition(index)
                : spline.GetEffectiveLocalPoint(index);
        }

        private static void CountScenePointDistanceIssue(Vector3 a, Vector3 b, ref int duplicatePairs, ref int closePairs)
        {
            float distance = Vector3.Distance(a, b);
            if (distance <= SceneStatusDuplicatePointDistance)
                duplicatePairs++;
            else if (distance <= SceneStatusClosePointDistance)
                closePairs++;
        }

        private static int CountSceneLongBezierHandles(VFXSimpleSpline spline, int count)
        {
            if (spline == null || spline.bezierPoints == null)
                return 0;

            int longHandleCount = 0;
            int safeCount = Mathf.Min(count, spline.bezierPoints.Count);
            for (int i = 0; i < safeCount; i++)
            {
                VFXBezierPoint point = spline.bezierPoints[i];
                if (point == null || point.handleMode == VFXBezierHandleMode.AutoSmooth)
                    continue;

                float prevDistance = GetSceneNeighborDistance(spline, i, -1, safeCount);
                float nextDistance = GetSceneNeighborDistance(spline, i, 1, safeCount);
                if (prevDistance > SceneStatusDuplicatePointDistance && point.inTangent.magnitude > prevDistance * SceneStatusLongHandleRatio)
                    longHandleCount++;
                if (nextDistance > SceneStatusDuplicatePointDistance && point.outTangent.magnitude > nextDistance * SceneStatusLongHandleRatio)
                    longHandleCount++;
            }

            return longHandleCount;
        }

        private static float GetSceneNeighborDistance(VFXSimpleSpline spline, int index, int direction, int count)
        {
            int neighbor = index + direction;
            if (spline.loop)
                neighbor = (neighbor + count) % count;
            else
                neighbor = Mathf.Clamp(neighbor, 0, count - 1);

            if (neighbor == index)
                return 0f;

            return Vector3.Distance(GetSceneStatusLocalPoint(spline, index), GetSceneStatusLocalPoint(spline, neighbor));
        }

        private static bool HasSceneLoopSeamWarning(VFXSimpleSpline spline, int count)
        {
            if (spline == null || !spline.loop || count < 3)
                return false;

            Vector3 startTangent = spline.GetTangent(0.001f, true);
            Vector3 endTangent = spline.GetTangent(0.999f, true);
            if (startTangent.sqrMagnitude <= 0.000001f || endTangent.sqrMagnitude <= 0.000001f)
                return false;

            return Vector3.Angle(startTangent, endTangent) > SceneStatusLoopSeamAngle;
        }

        private static bool HandleAppendPointMode(VFXSimpleSpline spline, int count)
        {
            if (!appendPointMode || appendPointModeSpline != spline)
                return false;

            Event e = Event.current;
            if (e == null)
                return false;

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                return false;
            }

            if (e.type == EventType.Repaint)
                DrawAppendPointModeHint(e.mousePosition);

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                ExitAppendPointMode();
                e.Use();
                return true;
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                if (count > 0 && IsMouseNearAnyPoint(spline, count, e.mousePosition))
                    return false;

                AppendPointAtMouse(spline, e.mousePosition);
                e.Use();
                return true;
            }

            return false;
        }

        private static bool TryFindNearestPointIndex(VFXSimpleSpline spline, int count, Vector2 mousePosition, out int pointIndex)
        {
            pointIndex = -1;
            if (spline == null || count <= 0)
                return false;

            float bestDistance = float.MaxValue;
            float maxDistance = Mathf.Max(18f, 12f * VFXSplinePointAPI.PickSizeMultiplier);
            for (int i = 0; i < count; i++)
            {
                Vector2 pointGui = HandleUtility.WorldToGUIPoint(spline.GetEffectiveWorldPoint(i));
                float distance = Vector2.Distance(mousePosition, pointGui);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    pointIndex = i;
                }
            }

            return pointIndex >= 0 && bestDistance <= maxDistance;
        }

        private static void AppendPointAtMouse(VFXSimpleSpline spline, Vector2 mousePosition)
        {
            Vector3 worldPosition;
            if (!TryGetWorldPointOnSplineEditPlane(spline, mousePosition, out worldPosition))
                return;

            Undo.RecordObject(spline, "Append Spline Point");
            spline.AppendPointAtWorldPosition(worldPosition);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void DrawAppendPointModeHint(Vector2 mousePosition)
        {
            Rect rect = new Rect(mousePosition.x + 16f, mousePosition.y + 16f, 230f, 24f);
            Handles.BeginGUI();
            Color oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            GUI.color = oldColor;
            GUI.Label(rect, " \u8ffd\u52a0\u70b9\uff1a\u5de6\u952e / A / Esc", EditorStyles.whiteLabel);
            Handles.EndGUI();
        }

        private static void NormalizeSelectedPointIndices(VFXSimpleSpline spline, int count)
        {
            if (spline.selectedPointIndices == null)
                spline.selectedPointIndices = new List<int>();

            for (int i = spline.selectedPointIndices.Count - 1; i >= 0; i--)
            {
                int index = spline.selectedPointIndices[i];
                if (index < 0 || index >= count || spline.selectedPointIndices.IndexOf(index) != i)
                    spline.selectedPointIndices.RemoveAt(i);
            }

            if (spline.selectedPointIndices.Count == 0 && count > 0)
                spline.selectedPointIndices.Add(Mathf.Clamp(spline.selectedPointIndex, 0, count - 1));

            if (spline.selectedPointIndices.Count > 0)
                spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndices[spline.selectedPointIndices.Count - 1], 0, count - 1);
        }

        private static bool IsPointMultiSelected(VFXSimpleSpline spline, int index)
        {
            return spline != null && spline.selectedPointIndices != null && spline.selectedPointIndices.Contains(index);
        }

        private static void SetSinglePointSelection(VFXSimpleSpline spline, int index)
        {
            if (spline.selectedPointIndices == null)
                spline.selectedPointIndices = new List<int>();

            spline.selectedPointIndices.Clear();
            spline.selectedPointIndices.Add(index);
            spline.selectedPointIndex = index;
        }

        private static void TogglePointSelection(VFXSimpleSpline spline, int index)
        {
            if (spline.selectedPointIndices == null)
                spline.selectedPointIndices = new List<int>();

            if (spline.selectedPointIndices.Contains(index))
            {
                if (spline.selectedPointIndices.Count > 1)
                    spline.selectedPointIndices.Remove(index);
            }
            else
            {
                spline.selectedPointIndices.Add(index);
            }

            spline.selectedPointIndex = index;
        }

        private static void DrawMultiPointMoveHandle(VFXSimpleSpline spline, int count)
        {
            Vector3 center = Vector3.zero;
            int selectedCount = 0;
            for (int i = 0; i < spline.selectedPointIndices.Count; i++)
            {
                int index = spline.selectedPointIndices[i];
                if (index < 0 || index >= count)
                    continue;

                center += spline.GetEffectiveWorldPoint(index);
                selectedCount++;
            }

            if (selectedCount <= 1)
                return;

            center /= selectedCount;
            Handles.color = Color.yellow;
            float size = HandleUtility.GetHandleSize(center) * spline.pointSize * 1.8f;
            Handles.SphereHandleCap(0, center, Quaternion.identity, size, EventType.Repaint);

            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 delta = newCenter - center;
                Undo.RecordObject(spline, "Move Selected Spline Points");

                for (int i = 0; i < spline.selectedPointIndices.Count; i++)
                {
                    int index = spline.selectedPointIndices[i];
                    if (index < 0 || index >= count)
                        continue;

                    Transform boundTransform = spline.GetDynamicBindingTransformForPoint(index);
                    if (spline.IsPointDynamicallyBound(index) && boundTransform != null)
                    {
                        Undo.RecordObject(boundTransform, "Move Dynamic Binding Transform");
                        boundTransform.position += delta;
                        EditorUtility.SetDirty(boundTransform);
                    }
                    else
                    {
                        spline.SetActivePointWorldPosition(index, spline.GetEffectiveWorldPoint(index) + delta);
                    }
                }

                spline.MarkDistanceCacheDirty();
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }
        }

        private static bool HandleBoxSelection(VFXSimpleSpline spline, int count)
        {
            Event e = Event.current;
            if (e == null || spline == null || count <= 0)
                return false;

            bool modifier = e.control || e.command;
            if (!boxSelecting && modifier && e.type == EventType.MouseDown && e.button == 0 && !IsMouseNearAnyPoint(spline, count, e.mousePosition))
            {
                boxSelecting = true;
                boxSelectingSpline = spline;
                boxSelectStart = e.mousePosition;
                boxSelectCurrent = e.mousePosition;
                e.Use();
                return true;
            }

            if (!boxSelecting || boxSelectingSpline != spline)
                return false;

            if (e.type == EventType.MouseDrag)
            {
                boxSelectCurrent = e.mousePosition;
                SceneView.RepaintAll();
                e.Use();
                return true;
            }

            if (e.type == EventType.Repaint)
                DrawBoxSelectionRect();

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                Rect rect = GetBoxSelectionRect();
                List<int> selected = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    Vector2 pointGui = HandleUtility.WorldToGUIPoint(spline.GetEffectiveWorldPoint(i));
                    if (rect.Contains(pointGui, true))
                        selected.Add(i);
                }

                if (selected.Count > 0)
                {
                    Undo.RecordObject(spline, "Box Select Spline Points");
                    spline.selectedPointIndices = selected;
                    spline.selectedPointIndex = selected[selected.Count - 1];
                    EditorUtility.SetDirty(spline);
                }

                boxSelecting = false;
                boxSelectingSpline = null;
                SceneView.RepaintAll();
                e.Use();
                return true;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                boxSelecting = false;
                boxSelectingSpline = null;
                SceneView.RepaintAll();
                e.Use();
                return true;
            }

            return boxSelecting;
        }

        private static bool IsMouseNearAnyPoint(VFXSimpleSpline spline, int count, Vector2 mousePosition)
        {
            float maxDistance = Mathf.Max(14f, 12f * VFXSplinePointAPI.PickSizeMultiplier);
            for (int i = 0; i < count; i++)
            {
                Vector2 pointGui = HandleUtility.WorldToGUIPoint(spline.GetEffectiveWorldPoint(i));
                if (Vector2.Distance(mousePosition, pointGui) <= maxDistance)
                    return true;
            }

            return false;
        }

        private static bool IsMouseNearVisibleBezierTangentHandle(VFXSimpleSpline spline, int count, Vector2 mousePosition)
        {
            if (spline == null || spline.pathMode != VFXSplinePathMode.Bezier || spline.bezierPoints == null)
                return false;

            float maxDistance = 18f;
            for (int i = 0; i < count; i++)
            {
                bool isVisiblePoint = spline.showAllPointHandles || spline.selectedPointIndex == i || IsPointMultiSelected(spline, i);
                if (!isVisiblePoint || i < 0 || i >= spline.bezierPoints.Count || spline.bezierPoints[i] == null)
                    continue;

                bool canUseInTangent = i > 0 || spline.loop;
                bool canUseOutTangent = i < count - 1 || spline.loop;

                if (canUseInTangent)
                {
                    Vector2 inGui = HandleUtility.WorldToGUIPoint(spline.GetBezierInTangentWorldPosition(i));
                    if (Vector2.Distance(mousePosition, inGui) <= maxDistance)
                        return true;
                }

                if (canUseOutTangent)
                {
                    Vector2 outGui = HandleUtility.WorldToGUIPoint(spline.GetBezierOutTangentWorldPosition(i));
                    if (Vector2.Distance(mousePosition, outGui) <= maxDistance)
                        return true;
                }
            }

            return false;
        }

        private static bool IsMouseNearMultiPointMoveHandle(VFXSimpleSpline spline, int count, Vector2 mousePosition)
        {
            if (spline == null || spline.selectedPointIndices == null || spline.selectedPointIndices.Count <= 1)
                return false;

            Vector3 center = Vector3.zero;
            int selectedCount = 0;
            for (int i = 0; i < spline.selectedPointIndices.Count; i++)
            {
                int index = spline.selectedPointIndices[i];
                if (index < 0 || index >= count)
                    continue;

                center += spline.GetEffectiveWorldPoint(index);
                selectedCount++;
            }

            if (selectedCount <= 1)
                return false;

            center /= selectedCount;
            Vector2 centerGui = HandleUtility.WorldToGUIPoint(center);
            return Vector2.Distance(mousePosition, centerGui) <= Mathf.Max(36f, 12f * VFXSplinePointAPI.PickSizeMultiplier);
        }

        private static Rect GetBoxSelectionRect()
        {
            float xMin = Mathf.Min(boxSelectStart.x, boxSelectCurrent.x);
            float xMax = Mathf.Max(boxSelectStart.x, boxSelectCurrent.x);
            float yMin = Mathf.Min(boxSelectStart.y, boxSelectCurrent.y);
            float yMax = Mathf.Max(boxSelectStart.y, boxSelectCurrent.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void DrawBoxSelectionRect()
        {
            Rect rect = GetBoxSelectionRect();
            Handles.BeginGUI();
            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 0.8f, 0.05f, 0.18f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.8f, 0.05f, 0.85f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = oldColor;
            Handles.EndGUI();
        }

        private static void AddAlignSelectedPointsMenu(GenericMenu menu, VFXSimpleSpline spline)
        {
            menu.AddItem(new GUIContent("多选对齐/上对齐 (Y 最大)"), false, () => AlignSelectedPoints(spline, 1, true));
            menu.AddItem(new GUIContent("多选对齐/下对齐 (Y 最小)"), false, () => AlignSelectedPoints(spline, 1, false));
            menu.AddItem(new GUIContent("多选对齐/右对齐 (X 最大)"), false, () => AlignSelectedPoints(spline, 0, true));
            menu.AddItem(new GUIContent("多选对齐/左对齐 (X 最小)"), false, () => AlignSelectedPoints(spline, 0, false));
            menu.AddItem(new GUIContent("多选对齐/前对齐 (Z 最大)"), false, () => AlignSelectedPoints(spline, 2, true));
            menu.AddItem(new GUIContent("多选对齐/后对齐 (Z 最小)"), false, () => AlignSelectedPoints(spline, 2, false));
            menu.AddSeparator("多选归零/");
            menu.AddItem(new GUIContent("多选归零/Local X = 0"), false, () => ZeroSelectedPointsLocalAxis(spline, 0));
            menu.AddItem(new GUIContent("多选归零/Local Y = 0"), false, () => ZeroSelectedPointsLocalAxis(spline, 1));
            menu.AddItem(new GUIContent("多选归零/Local Z = 0"), false, () => ZeroSelectedPointsLocalAxis(spline, 2));
        }
        private static void AlignSelectedPoints(VFXSimpleSpline spline, int axis, bool useMax)
        {
            if (spline == null || spline.selectedPointIndices == null || spline.selectedPointIndices.Count <= 1)
                return;

            int count = spline.GetActivePointCount();
            float target = useMax ? float.NegativeInfinity : float.PositiveInfinity;
            for (int i = 0; i < spline.selectedPointIndices.Count; i++)
            {
                int index = spline.selectedPointIndices[i];
                if (index < 0 || index >= count)
                    continue;

                float value = GetAxisValue(spline.GetEffectiveWorldPoint(index), axis);
                target = useMax ? Mathf.Max(target, value) : Mathf.Min(target, value);
            }

            if (float.IsInfinity(target))
                return;

            Undo.RecordObject(spline, "Align Selected Spline Points");
            for (int i = 0; i < spline.selectedPointIndices.Count; i++)
            {
                int index = spline.selectedPointIndices[i];
                if (index < 0 || index >= count)
                    continue;

                Vector3 world = spline.GetEffectiveWorldPoint(index);
                SetAxisValue(ref world, axis, target);

                Transform boundTransform = spline.GetDynamicBindingTransformForPoint(index);
                if (spline.IsPointDynamicallyBound(index) && boundTransform != null)
                {
                    Undo.RecordObject(boundTransform, "Align Dynamic Binding Transform");
                    boundTransform.position = world;
                    EditorUtility.SetDirty(boundTransform);
                }
                else
                {
                    spline.SetActivePointWorldPosition(index, world);
                }
            }

            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void ZeroSelectedPointsLocalAxis(VFXSimpleSpline spline, int axis)
        {
            if (spline == null || spline.selectedPointIndices == null || spline.selectedPointIndices.Count <= 1)
                return;

            int count = spline.GetActivePointCount();
            Undo.RecordObject(spline, "Zero Selected Spline Points Axis");
            for (int i = 0; i < spline.selectedPointIndices.Count; i++)
            {
                int index = spline.selectedPointIndices[i];
                if (index < 0 || index >= count)
                    continue;

                Vector3 local = spline.transform.InverseTransformPoint(spline.GetEffectiveWorldPoint(index));
                SetAxisValue(ref local, axis, 0f);
                Vector3 world = spline.transform.TransformPoint(local);

                Transform boundTransform = spline.GetDynamicBindingTransformForPoint(index);
                if (spline.IsPointDynamicallyBound(index) && boundTransform != null)
                {
                    Undo.RecordObject(boundTransform, "Zero Dynamic Binding Transform Axis");
                    boundTransform.position = world;
                    EditorUtility.SetDirty(boundTransform);
                }
                else
                {
                    spline.SetActivePointWorldPosition(index, world);
                }
            }

            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static float GetAxisValue(Vector3 value, int axis)
        {
            if (axis == 0) return value.x;
            if (axis == 1) return value.y;
            return value.z;
        }

        private static void SetAxisValue(ref Vector3 value, int axis, float axisValue)
        {
            if (axis == 0) value.x = axisValue;
            else if (axis == 1) value.y = axisValue;
            else value.z = axisValue;
        }

        private static void InsertPointOnCurveAtRawProgress(VFXSimpleSpline spline, float rawProgress)
        {
            if (spline == null)
                return;

            Undo.RecordObject(spline, spline.pathMode == VFXSplinePathMode.Bezier ? "Insert Bezier Point" : "Insert Catmull-Rom Point");
            int inserted;
            if (spline.pathMode == VFXSplinePathMode.Bezier)
                inserted = spline.InsertBezierPointAtRawProgress(rawProgress);
            else
                inserted = spline.InsertCatmullRomPointAtRawProgress(rawProgress);

            if (inserted >= 0)
            {
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }
        }

        private static bool TryGetWorldPointOnSplineEditPlane(VFXSimpleSpline spline, Vector2 mousePosition, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (spline == null)
                return false;

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Vector3 planePoint = spline.transform.position;
            int count = spline.GetActivePointCount();
            if (count > 0)
            {
                int selectedIndex = Mathf.Clamp(spline.selectedPointIndex, 0, count - 1);
                planePoint = spline.GetEffectiveWorldPoint(selectedIndex);
            }

            Vector3 planeNormal = SceneView.currentDrawingSceneView != null && SceneView.currentDrawingSceneView.camera != null
                ? SceneView.currentDrawingSceneView.camera.transform.forward
                : Vector3.up;

            Plane plane = new Plane(planeNormal, planePoint);
            float distance;
            if (!plane.Raycast(ray, out distance))
                return false;

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private static bool TryFindNearestRawProgressOnCurve(VFXSimpleSpline spline, Vector2 mousePosition, out float rawProgress)
        {
            float bestDistance;
            return TryFindNearestRawProgressOnCurve(spline, mousePosition, out rawProgress, out bestDistance);
        }

        private static bool TryFindNearestRawProgressOnCurve(VFXSimpleSpline spline, Vector2 mousePosition, out float rawProgress, out float bestDistance)
        {
            rawProgress = 0f;
            bestDistance = float.MaxValue;
            if (spline == null || spline.GetActivePointCount() < 2)
                return false;

            int samples = Mathf.Max(24, spline.resolution * 2);
            float bestProgress = 0f;
            Vector2 previousGui = HandleUtility.WorldToGUIPoint(spline.GetPoint(0f, false));

            for (int i = 1; i <= samples; i++)
            {
                float progress = i / (float)samples;
                Vector2 currentGui = HandleUtility.WorldToGUIPoint(spline.GetPoint(progress, false));
                float segmentLerp;
                float distance = DistanceToGuiSegment(mousePosition, previousGui, currentGui, out segmentLerp);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestProgress = Mathf.Lerp((i - 1) / (float)samples, progress, segmentLerp);
                }

                previousGui = currentGui;
            }

            if (bestDistance > 10f)
                return false;

            rawProgress = Mathf.Clamp01(bestProgress);
            return true;
        }

        private static float DistanceToGuiSegment(Vector2 point, Vector2 a, Vector2 b, out float segmentLerp)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq <= 0.0001f)
            {
                segmentLerp = 0f;
                return Vector2.Distance(point, a);
            }

            segmentLerp = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            Vector2 projected = a + ab * segmentLerp;
            return Vector2.Distance(point, projected);
        }

        private static void ShowPointContextMenu(VFXSimpleSpline spline, int index)
        {
            if (spline == null || index < 0 || index >= spline.GetActivePointCount())
                return;

            Undo.RecordObject(spline, "Select Spline Point");
            bool clickedSelectedMultiPoint = spline.selectedPointIndices != null &&
                                             spline.selectedPointIndices.Count > 1 &&
                                             spline.selectedPointIndices.Contains(index);
            if (!clickedSelectedMultiPoint)
                SetSinglePointSelection(spline, index);
            EditorUtility.SetDirty(spline);

            GenericMenu menu = new GenericMenu();
            bool hasMultiSelection = spline.selectedPointIndices != null && spline.selectedPointIndices.Count > 1;
            if (hasMultiSelection)
            {
                AddAlignSelectedPointsMenu(menu, spline);
                menu.AddSeparator("");
            }

            AddPathModeMenuItems(menu, spline);
            menu.AddSeparator("");

            if (spline.pathMode == VFXSplinePathMode.Bezier)
            {
                menu.AddItem(new GUIContent("重排控制点/按距离均匀重排"), false, () =>
                {
                    RedistributeBezierPointsByDistance(spline);
                });
                AddResamplePointCountMenuItems(menu, spline);
                menu.AddSeparator("");

                AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.AutoSmooth, "Bezier/自动平滑");
                menu.AddItem(new GUIContent("Bezier/自动平滑全部"), false, () =>
                {
                    Undo.RecordObject(spline, "Auto Smooth All Bezier Points");
                    spline.AutoSmoothAllBezierPoints();
                    EditorUtility.SetDirty(spline);
                    SceneView.RepaintAll();
                });
                menu.AddSeparator("Bezier 点类型/");
                AddBezierPointPresetMenuItem(menu, spline, index, VFXBezierPointPreset.Corner, "Bezier 点类型/拐角");
                AddBezierPointPresetMenuItem(menu, spline, index, VFXBezierPointPreset.Smooth, "Bezier 点类型/平滑");
                AddBezierPointPresetMenuItem(menu, spline, index, VFXBezierPointPreset.Symmetric, "Bezier 点类型/对称");
                menu.AddSeparator("Bezier 手柄模式/");
                AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.Free, "Bezier 手柄模式/自由手柄");
                AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.Aligned, "Bezier 手柄模式/对齐");
                AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.Mirrored, "Bezier 手柄模式/镜像");
                menu.AddSeparator("");
            }
            else
            {
                menu.AddItem(new GUIContent("重排控制点/按距离均匀重排"), false, () =>
                {
                    RedistributeCatmullRomPointsByDistance(spline);
                });
                AddResamplePointCountMenuItems(menu, spline);
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("点操作/在当前点后插入点"), false, () =>
                {
                    Undo.RecordObject(spline, "Insert Catmull-Rom Point After");
                    spline.InsertPoint(index + 1);
                    EditorUtility.SetDirty(spline);
                    SceneView.RepaintAll();
                });
                menu.AddSeparator("");
            }

            List<int> deleteIndices = GetContextDeleteIndices(spline, index);
            string deleteLabel = deleteIndices.Count > 1 ? "删除选中的点" : "删除当前点";
            if (spline.GetActivePointCount() > 2 && deleteIndices.Count > 0)
            {
                menu.AddItem(new GUIContent(deleteLabel), false, () =>
                {
                    Undo.RecordObject(spline, deleteIndices.Count > 1 ? "Delete Selected Spline Points" : "Delete Spline Point");
                    DeleteSplinePoints(spline, deleteIndices);
                    EditorUtility.SetDirty(spline);
                    SceneView.RepaintAll();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent(deleteLabel));
            }

            suppressBezierToolbarUntil = EditorApplication.timeSinceStartup + 0.8;
            SceneView.RepaintAll();
            menu.ShowAsContext();
        }
        private static void AddPathModeMenuItems(GenericMenu menu, VFXSimpleSpline spline)
        {
            bool isBezier = spline != null && spline.pathMode == VFXSplinePathMode.Bezier;
            if (isBezier)
            {
                menu.AddItem(new GUIContent("路径模式/Bezier"), true, () => { });
                menu.AddItem(new GUIContent("路径模式/转换为 Catmull-Rom"), false, () =>
                {
                    ConvertSplinePathMode(spline, VFXSplinePathMode.CatmullRom, false);
                });
            }
            else
            {
                menu.AddItem(new GUIContent("路径模式/Catmull-Rom"), true, () => { });
                menu.AddItem(new GUIContent("路径模式/转换为 Bezier"), false, () =>
                {
                    ConvertSplinePathMode(spline, VFXSplinePathMode.Bezier, false);
                });
            }
        }
        private static void ConvertSplinePathMode(VFXSimpleSpline spline, VFXSplinePathMode mode, bool autoSmooth)
        {
            if (spline == null || (spline.pathMode == mode && !autoSmooth))
                return;

            Undo.RecordObject(spline, mode == VFXSplinePathMode.Bezier ? "Convert Catmull-Rom To Bezier" : "Convert Bezier To Catmull-Rom");
            if (mode == VFXSplinePathMode.Bezier)
            {
                if (spline.pathMode != VFXSplinePathMode.Bezier)
                    spline.ConvertCatmullRomToBezier();
                spline.pathMode = VFXSplinePathMode.Bezier;
                if (autoSmooth)
                    spline.AutoSmoothAllBezierPoints();
            }
            else
            {
                spline.ConvertBezierToCatmullRom();
                spline.pathMode = VFXSplinePathMode.CatmullRom;
            }

            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void RedistributeCatmullRomPointsByDistance(VFXSimpleSpline spline)
        {
            if (spline == null || spline.pathMode != VFXSplinePathMode.CatmullRom)
                return;

            int count = spline.GetActivePointCount();
            ResampleCatmullRomPointsByDistance(spline, count, "Redistribute Catmull-Rom Points");
        }

        private static void RedistributeBezierPointsByDistance(VFXSimpleSpline spline)
        {
            if (spline == null || spline.pathMode != VFXSplinePathMode.Bezier)
                return;

            int count = spline.GetActivePointCount();
            ResampleBezierPointsByDistance(spline, count, "Redistribute Bezier Points");
        }

        private static void AddResamplePointCountMenuItems(GenericMenu menu, VFXSimpleSpline spline)
        {
            AddResamplePointCountMenuItem(menu, spline, 8);
            AddResamplePointCountMenuItem(menu, spline, 16);
            AddResamplePointCountMenuItem(menu, spline, 32);
        }

        private static void AddResamplePointCountMenuItem(GenericMenu menu, VFXSimpleSpline spline, int pointCount)
        {
            bool current = spline != null && spline.GetActivePointCount() == pointCount;
            menu.AddItem(new GUIContent("重排控制点/重采样为 " + pointCount + " 个点"), current, () =>
            {
                ResampleSplinePointsByDistance(spline, pointCount);
            });
        }

        private static void ResampleSplinePointsByDistance(VFXSimpleSpline spline, int pointCount)
        {
            if (spline == null)
                return;

            if (spline.pathMode == VFXSplinePathMode.Bezier)
                ResampleBezierPointsByDistance(spline, pointCount, "Resample Bezier Points");
            else
                ResampleCatmullRomPointsByDistance(spline, pointCount, "Resample Catmull-Rom Points");
        }

        private static void ResampleCatmullRomPointsByDistance(VFXSimpleSpline spline, int pointCount, string undoName)
        {
            if (spline == null || spline.pathMode != VFXSplinePathMode.CatmullRom)
                return;

            pointCount = Mathf.Clamp(pointCount, 2, 256);
            List<Vector3> resampled = new List<Vector3>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float progress = spline.loop ? i / (float)pointCount : i / (float)(pointCount - 1);
                resampled.Add(spline.GetLocalPoint(progress, true));
            }

            Undo.RecordObject(spline, undoName);
            spline.localPoints = resampled;
            spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, pointCount - 1);
            spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void ResampleBezierPointsByDistance(VFXSimpleSpline spline, int pointCount, string undoName)
        {
            if (spline == null || spline.pathMode != VFXSplinePathMode.Bezier)
                return;

            pointCount = Mathf.Clamp(pointCount, 2, 256);
            List<VFXBezierPoint> resampled = new List<VFXBezierPoint>(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                float progress = spline.loop ? i / (float)pointCount : i / (float)(pointCount - 1);
                VFXBezierPoint point = new VFXBezierPoint(spline.GetLocalPoint(progress, true));
                point.handleMode = VFXBezierHandleMode.AutoSmooth;
                resampled.Add(point);
            }

            Undo.RecordObject(spline, undoName);
            spline.bezierPoints = resampled;
            spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, pointCount - 1);
            spline.selectedPointIndices = new List<int>() { spline.selectedPointIndex };
            spline.AutoSmoothAllBezierPoints();
            spline.MarkDistanceCacheDirty();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static List<int> GetContextDeleteIndices(VFXSimpleSpline spline, int clickedIndex)
        {
            List<int> indices = new List<int>();
            if (spline == null)
                return indices;

            int count = spline.GetActivePointCount();
            bool deleteSelection = spline.selectedPointIndices != null &&
                                   spline.selectedPointIndices.Count > 1 &&
                                   spline.selectedPointIndices.Contains(clickedIndex);

            if (deleteSelection)
            {
                for (int i = 0; i < spline.selectedPointIndices.Count; i++)
                {
                    int index = spline.selectedPointIndices[i];
                    if (index >= 0 && index < count && !indices.Contains(index))
                        indices.Add(index);
                }
            }
            else if (clickedIndex >= 0 && clickedIndex < count)
            {
                indices.Add(clickedIndex);
            }

            indices.Sort();
            indices.Reverse();
            return indices;
        }

        private static void DeleteSplinePoints(VFXSimpleSpline spline, List<int> indices)
        {
            if (spline == null || indices == null)
                return;

            for (int i = 0; i < indices.Count; i++)
            {
                if (spline.GetActivePointCount() <= 2)
                    break;

                int index = indices[i];
                if (index >= 0 && index < spline.GetActivePointCount())
                    spline.RemovePointAt(index);
            }
        }

        private static void AddBezierPointPresetMenuItem(GenericMenu menu, VFXSimpleSpline spline, int index, VFXBezierPointPreset preset, string label)
        {
            menu.AddItem(new GUIContent(label), false, () =>
            {
                Undo.RecordObject(spline, "Apply Bezier Point Preset");
                List<int> targetIndices = GetBezierContextTargetIndices(spline, index);
                for (int i = 0; i < targetIndices.Count; i++)
                    spline.ApplyBezierPointPreset(targetIndices[i], preset);
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            });
        }

        private static void AddBezierModeMenuItem(GenericMenu menu, VFXSimpleSpline spline, int index, VFXBezierHandleMode mode, string label)
        {
            bool current = spline.bezierPoints != null &&
                           index >= 0 &&
                           index < spline.bezierPoints.Count &&
                           spline.bezierPoints[index] != null &&
                           spline.bezierPoints[index].handleMode == mode;

            menu.AddItem(new GUIContent(label), current, () => ApplyBezierHandleMode(spline, index, mode));
        }

        private static List<int> GetBezierContextTargetIndices(VFXSimpleSpline spline, int clickedIndex)
        {
            List<int> indices = new List<int>();
            if (spline == null || spline.bezierPoints == null)
                return indices;

            bool useSelection = spline.selectedPointIndices != null &&
                                spline.selectedPointIndices.Count > 1 &&
                                spline.selectedPointIndices.Contains(clickedIndex);
            if (useSelection)
            {
                for (int i = 0; i < spline.selectedPointIndices.Count; i++)
                {
                    int index = spline.selectedPointIndices[i];
                    if (index >= 0 && index < spline.bezierPoints.Count && !indices.Contains(index))
                        indices.Add(index);
                }
            }
            else if (clickedIndex >= 0 && clickedIndex < spline.bezierPoints.Count)
            {
                indices.Add(clickedIndex);
            }

            return indices;
        }

        private static void DrawBezierHandleModeToolbar(VFXSimpleSpline spline, int index, Vector3 pointWorld, float pointSize)
        {
            if (spline.bezierPoints == null || index < 0 || index >= spline.bezierPoints.Count || spline.bezierPoints[index] == null)
                return;

            if (EditorApplication.timeSinceStartup < suppressBezierToolbarUntil)
                return;

            Vector2 guiPoint = HandleUtility.WorldToGUIPoint(pointWorld + Vector3.up * pointSize * 2.2f);
            Rect rect = new Rect(guiPoint.x + 12f, guiPoint.y - 18f, 174f, 22f);
            Event e = Event.current;
            if (e != null)
            {
                Vector2 pointGui = HandleUtility.WorldToGUIPoint(pointWorld);
                float hoverRadius = Mathf.Max(42f, pointSize * 140f);
                bool hoverPoint = Vector2.Distance(e.mousePosition, pointGui) <= hoverRadius;
                bool hoverToolbar = rect.Contains(e.mousePosition);
                if (!hoverPoint && !hoverToolbar)
                    return;
            }

            Handles.BeginGUI();
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);
            currentBezierToolbarRect = rect;
            currentBezierToolbarButtonIndex = 0;
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.Free, "自由");
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.Aligned, "对齐");
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.Mirrored, "镜像");
            Handles.EndGUI();
        }

        private static void DrawBezierModeButton(VFXSimpleSpline spline, int index, VFXBezierHandleMode mode, string label)
        {
            int buttonIndex = currentBezierToolbarButtonIndex++;
            Rect rect = new Rect(currentBezierToolbarRect.x + buttonIndex * 58f, currentBezierToolbarRect.y, 58f, currentBezierToolbarRect.height);
            DrawBezierModeButton(spline, index, mode, label, rect);
        }

        private static void DrawBezierModeButton(VFXSimpleSpline spline, int index, VFXBezierHandleMode mode, string label, Rect rect)
        {
            bool selected = spline.bezierPoints[index].handleMode == mode;
            using (new EditorGUI.DisabledScope(selected))
            {
                if (GUI.Button(rect, label, EditorStyles.toolbarButton))
                {
                    ApplyBezierHandleMode(spline, index, mode);
                    Event.current.Use();
                }
            }
        }

        private static void ApplyBezierHandleMode(VFXSimpleSpline spline, int index, VFXBezierHandleMode mode)
        {
            Undo.RecordObject(spline, "Change Bezier Handle Mode");
            List<int> targetIndices = GetBezierContextTargetIndices(spline, index);
            for (int i = 0; i < targetIndices.Count; i++)
                spline.SetBezierHandleMode(targetIndices[i], mode);
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private static void DrawBezierHandles(VFXSimpleSpline spline, int index, int pointCount, Vector3 pointWorld, float pointSize)
        {
            if (spline == null || spline.bezierPoints == null || index < 0 || index >= spline.bezierPoints.Count)
                return;

            VFXBezierPoint point = spline.bezierPoints[index];
            if (point == null)
                return;

            Handles.color = new Color(0.35f, 0.85f, 1f, 0.85f);

            bool canUseInTangent = index > 0 || spline.loop;
            bool canUseOutTangent = index < pointCount - 1 || spline.loop;

            if (canUseInTangent)
                DrawBezierTangentHandle(spline, index, true, pointWorld, pointSize);

            if (canUseOutTangent)
                DrawBezierTangentHandle(spline, index, false, pointWorld, pointSize);
        }

        private static void DrawBezierTangentHandle(VFXSimpleSpline spline, int index, bool isInTangent, Vector3 pointWorld, float pointSize)
        {
            Vector3 handleWorld = isInTangent ? spline.GetBezierInTangentWorldPosition(index) : spline.GetBezierOutTangentWorldPosition(index);
            Handles.color = isInTangent ? new Color(0.2f, 0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.25f, 0.9f);
            Handles.DrawAAPolyLine(2f, pointWorld, handleWorld);

            float handleSize = Mathf.Max(pointSize * 0.75f, HandleUtility.GetHandleSize(handleWorld) * 0.06f);

            EditorGUI.BeginChangeCheck();
            Vector3 newHandleWorld = Handles.FreeMoveHandle(handleWorld, handleSize, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, isInTangent ? "Move Bezier In Tangent" : "Move Bezier Out Tangent");
                if (isInTangent)
                    spline.SetBezierInTangentWorldPosition(index, newHandleWorld);
                else
                    spline.SetBezierOutTangentWorldPosition(index, newHandleWorld);

                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }
        }

    }
}
#endif

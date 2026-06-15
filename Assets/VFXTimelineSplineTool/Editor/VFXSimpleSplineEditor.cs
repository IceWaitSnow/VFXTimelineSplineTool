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
            EditorGUILayout.HelpBox("3D Catmull-Rom 自由曲线路径。v2.6.2 正式工作流：Spline Path 负责路径，VFXSplineAnimator 负责路径运动，VFXSplineAnchor 负责粒子/面片/爆点挂点；Dynamic Start / End Binding 可让路径起点和终点跟随场景物体。", MessageType.Info);

            serializedObject.Update();

            DrawProperty("pathMode", "Path Mode");
            DrawProperty("pathColor", "Path Color");
            DrawProperty("progressMarkColor", "Progress Mark Color");
            DrawProperty("lineWidth", "Line Width");
            DrawProperty("pointSize", "Point Size");
            DrawProperty("resolution", "Resolution");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
            DrawProperty("alwaysShowPathInSceneView", "Always Show Path In Scene View");
            DrawProperty("showPointLabels", "Show Point Labels");
            DrawProperty("showAllPointHandles", "Show All Point Handles");
            EditorGUILayout.HelpBox("默认只显示当前选中控制点的移动坐标轴，避免 Scene 视图太乱。点击路径控制点可切换当前编辑点；需要同时显示全部坐标轴时再勾选 Show All Point Handles。", MessageType.None);
            DrawProperty("showProgressMarks", "Show Progress Marks");
            DrawProperty("progressMarkCount", "Progress Mark Count");
            DrawProperty("progressMarksUseDistance", "Progress Marks Use Distance");
            DrawProperty("showDirectionArrows", "Show Direction Arrows");
            DrawProperty("arrowCount", "Arrow Count");
            DrawProperty("arrowSize", "Arrow Size");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Dynamic Start / End Binding", EditorStyles.boldLabel);
            DrawProperty("enableDynamicStartEndBinding", "Enable Dynamic Start / End");
            if (spline.enableDynamicStartEndBinding)
            {
                EditorGUILayout.HelpBox("开启后，路径第一个点跟随 Start Transform，最后一个点跟随 End Transform。中间点仍保留当前 Local Points，用来控制弧度。适合飞向目标、奖励飞行、吸入路径。", MessageType.Info);
                DrawProperty("dynamicStartTransform", "Start Transform");
                DrawProperty("dynamicEndTransform", "End Transform");
                DrawProperty("dynamicUpdateInEditMode", "Update In Edit Mode");
                DrawProperty("showDynamicBindingLabels", "Show Binding Labels");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Bake Dynamic Ends To Points"))
                    {
                        ModifySpline(spline, "Bake Dynamic Ends To Points", () => spline.ApplyDynamicBindingToLocalPoints());
                    }
                    if (GUILayout.Button("Rebuild Distance Cache"))
                    {
                        Undo.RecordObject(spline, "Rebuild Distance Cache");
                        spline.RebuildDistanceCache();
                        EditorUtility.SetDirty(spline);
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Distance Based Progress", EditorStyles.boldLabel);
            DrawProperty("distanceSampleResolution", "Distance Sample Resolution");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Path Data", EditorStyles.boldLabel);
            if (spline.pathMode == VFXSplinePathMode.Bezier)
            {
                DrawProperty("bezierPoints", "Bezier Points", true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Convert Catmull-Rom To Bezier"))
                        ModifySpline(spline, "Convert Catmull-Rom To Bezier", () => spline.ConvertCatmullRomToBezier());

                    if (GUILayout.Button("Copy Bezier Points To Catmull-Rom"))
                        ModifySpline(spline, "Copy Bezier Points To Catmull-Rom", () => spline.ConvertBezierToCatmullRom());
                }
            }
            else
            {
                DrawProperty("localPoints", "Local Points", true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Convert Current Points To Bezier"))
                        ModifySpline(spline, "Convert Current Points To Bezier", () =>
                        {
                            spline.ConvertCatmullRomToBezier();
                            spline.pathMode = VFXSplinePathMode.Bezier;
                        });
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Distance Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Approx Length", spline.ApproxLength.ToString("F3"));
            if (GUILayout.Button("Rebuild Distance Cache"))
            {
                Undo.RecordObject(spline, "Rebuild Distance Cache");
                spline.RebuildDistanceCache();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Path Tools", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point")) ModifySpline(spline, "Add Point", () => spline.AddPoint());
                if (GUILayout.Button("Insert Point")) ModifySpline(spline, "Insert Point", () => spline.InsertPoint(Mathf.Max(1, spline.GetActivePointCount() - 1)));
                if (GUILayout.Button("Remove Last Point")) ModifySpline(spline, "Remove Last Point", () => spline.RemoveLastPoint());
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reverse Path")) ModifySpline(spline, "Reverse Path", () => spline.ReversePath());
                if (GUILayout.Button("Reset Path")) ModifySpline(spline, "Reset Path", () => spline.ResetPath());
                if (GUILayout.Button("Flatten Y")) ModifySpline(spline, "Flatten Y", () => spline.FlattenY());
            }
            if (GUILayout.Button("Center Path To Object")) ModifySpline(spline, "Center Path To Object", () => spline.CenterPathToObject());

            DrawShapePresetTools(spline);
            DrawUserPresetTools(spline);
            DrawAnchorTools(spline);
            DrawPointTools(spline);
        }

        private void DrawProperty(string name, string label, bool includeChildren = false)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label), includeChildren);
        }

        private static void ModifySpline(VFXSimpleSpline spline, string undoName, System.Action action)
        {
            Undo.RecordObject(spline, undoName);
            action();
            EditorUtility.SetDirty(spline);
            SceneView.RepaintAll();
        }

        private void DrawShapePresetTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Shape Presets / 路径形状预设", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("一键生成常用路径形状。Apply 会覆盖当前 Local Points；Live Preview 开启后，参数变化会实时覆盖当前路径，建议复制路径后使用。", MessageType.Info);

            EditorGUI.BeginChangeCheck();

            selectedShapePreset = (ShapePreset)EditorGUILayout.EnumPopup(new GUIContent("Shape Preset", "选择要生成的路径形状。"), selectedShapePreset);
            presetScale = EditorGUILayout.FloatField(new GUIContent("Scale", "整体缩放。"), Mathf.Max(0.01f, presetScale));
            presetWidth = EditorGUILayout.FloatField(new GUIContent("Width", "横向宽度，主要用于 Line / S / Wave / Rectangle / Infinity 等形状。"), Mathf.Max(0.01f, presetWidth));
            presetHeight = EditorGUILayout.FloatField(new GUIContent("Height", "纵向高度，主要用于 S / Wave / Rectangle / Ellipse / Infinity 等形状。"), Mathf.Max(0.01f, presetHeight));
            presetPointCount = EditorGUILayout.IntSlider(new GUIContent("Point Count", "生成点数量。圆、波浪、螺旋等形状建议 8 到 24。"), presetPointCount, 4, 64);
            presetWaveCount = EditorGUILayout.IntSlider(new GUIContent("Wave Count", "Wave / Zigzag 的波段数量。"), presetWaveCount, 1, 8);
            presetSpiralTurns = EditorGUILayout.Slider(new GUIContent("Spiral Turns", "螺旋圈数。"), presetSpiralTurns, 0.5f, 5f);
            presetRotationY = EditorGUILayout.FloatField(new GUIContent("Rotation Y", "绕 Y 轴旋转预设形状。"), presetRotationY);
            presetOffset = EditorGUILayout.Vector3Field(new GUIContent("Local Offset", "生成后在 Spline 本地空间中的偏移。"), presetOffset);
            livePreviewShapePreset = EditorGUILayout.Toggle(new GUIContent("Live Preview Shape Preset", "开启后，修改上方参数会立即刷新当前路径。注意：这会持续覆盖 Local Points。"), livePreviewShapePreset);

            bool shapeParamChanged = EditorGUI.EndChangeCheck();

            if (livePreviewShapePreset)
            {
                EditorGUILayout.HelpBox("Live Preview 已开启：修改预设参数会实时覆盖当前 Local Points。正式路径建议先复制一份再试。", MessageType.Warning);
                if (shapeParamChanged)
                    ApplyShapePresetToSpline(spline, "Live Preview Shape Preset");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Shape Preset / 应用形状预设"))
                    ApplyShapePresetToSpline(spline, "Apply Shape Preset");

                if (GUILayout.Button("Reset Preset Params / 重置参数"))
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
                spline.localPoints = GeneratePresetPoints(selectedShapePreset);
                if (spline.pathMode == VFXSplinePathMode.Bezier)
                    spline.ConvertCatmullRomToBezier();
                spline.MarkDistanceCacheDirty();
            });
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
                    pts.Add(new Vector3(-s, 0f, -s));
                    break;

                case ShapePreset.Rectangle:
                    pts.Add(new Vector3(-w, 0f, -h));
                    pts.Add(new Vector3(w, 0f, -h));
                    pts.Add(new Vector3(w, 0f, h));
                    pts.Add(new Vector3(-w, 0f, h));
                    pts.Add(new Vector3(-w, 0f, -h));
                    break;

                case ShapePreset.Triangle:
                    pts.Add(new Vector3(0f, 0f, h));
                    pts.Add(new Vector3(w, 0f, -h));
                    pts.Add(new Vector3(-w, 0f, -h));
                    pts.Add(new Vector3(0f, 0f, h));
                    break;

                case ShapePreset.Diamond:
                    pts.Add(new Vector3(0f, 0f, h));
                    pts.Add(new Vector3(w, 0f, 0f));
                    pts.Add(new Vector3(0f, 0f, -h));
                    pts.Add(new Vector3(-w, 0f, 0f));
                    pts.Add(new Vector3(0f, 0f, h));
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
                    for (int i = 0; i <= n; i++)
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
                    for (int i = 0; i <= n; i++)
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
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                pts.Add(func(t));
            }
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
            EditorGUILayout.LabelField("User Path Presets / 自定义路径预设", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("把当前手调好的 Local Points 保存为 .asset 资源，方便下次复用，也方便提交到 SVN/Git 给团队共享。", MessageType.Info);

            userPresetName = EditorGUILayout.TextField(new GUIContent("Preset Name", "保存自定义路径预设时使用的文件名。"), userPresetName);
            selectedUserPreset = (VFXUserPathPreset)EditorGUILayout.ObjectField(new GUIContent("User Preset", "选择已经保存的自定义路径预设资源。"), selectedUserPreset, typeof(VFXUserPathPreset), false);
            saveDisplaySettingsWithPreset = EditorGUILayout.Toggle(new GUIContent("Save Display Settings", "开启后，保存路径点的同时保存路径颜色、线宽、采样精度等显示参数。一般只保存 Local Points 即可。"), saveDisplaySettingsWithPreset);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Current Path As Preset"))
                    SaveCurrentPathAsPreset(spline);

                EditorGUI.BeginDisabledGroup(selectedUserPreset == null);
                if (GUILayout.Button("Load Selected Preset"))
                    LoadSelectedPreset(spline);
                EditorGUI.EndDisabledGroup();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(selectedUserPreset == null);
                if (GUILayout.Button("Delete Selected Preset"))
                    DeleteSelectedPreset();
                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Refresh Presets"))
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

            bool ok = EditorUtility.DisplayDialog("Delete User Path Preset", "确认删除这个自定义路径预设？\n" + path, "Delete", "Cancel");
            if (!ok) return;

            selectedUserPreset = null;
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void DrawAnchorTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Anchor Tools / 特效挂点工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("用于快速在当前路径上创建 VFX Spline Anchor。粒子、面片、爆点可以挂到 Anchor 子物体下，再用 Timeline 原生 Control Track / Activation Track 控制播放。", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create 0%")) CreateAnchorAtProgress(spline, 0f);
                if (GUILayout.Button("Create 25%")) CreateAnchorAtProgress(spline, 0.25f);
                if (GUILayout.Button("Create 50%")) CreateAnchorAtProgress(spline, 0.5f);
                if (GUILayout.Button("Create 75%")) CreateAnchorAtProgress(spline, 0.75f);
                if (GUILayout.Button("Create 100%")) CreateAnchorAtProgress(spline, 1f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                batchAnchorCount = EditorGUILayout.IntSlider("Batch Count", batchAnchorCount, 2, 30);
            }

            if (GUILayout.Button("Create Evenly Spaced Anchors / 均分创建 Anchor"))
                CreateEvenlySpacedAnchors(spline, batchAnchorCount);
        }

        private static void CreateAnchorAtProgress(VFXSimpleSpline spline, float progress)
        {
            if (spline == null) return;

            GameObject go = new GameObject("VFX Spline Anchor_" + Mathf.RoundToInt(progress * 100f).ToString("000"));
            Undo.RegisterCreatedObjectUndo(go, "Create VFX Spline Anchor");
            go.transform.SetParent(spline.transform.parent, true);

            VFXSplineAnchor anchor = go.AddComponent<VFXSplineAnchor>();
            anchor.spline = spline;
            anchor.progress = Mathf.Clamp01(progress);
            anchor.useDistanceBasedProgress = true;
            anchor.label = go.name;
            anchor.ApplyAnchor();

            EditorUtility.SetDirty(anchor);
            Selection.activeGameObject = go;
            SceneView.RepaintAll();
        }

        private static void CreateEvenlySpacedAnchors(VFXSimpleSpline spline, int count)
        {
            if (spline == null) return;
            count = Mathf.Clamp(count, 2, 30);

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
                go.transform.SetParent(group.transform, true);

                VFXSplineAnchor anchor = go.AddComponent<VFXSplineAnchor>();
                anchor.spline = spline;
                anchor.progress = progress;
                anchor.useDistanceBasedProgress = true;
                anchor.label = go.name;
                anchor.ApplyAnchor();
                EditorUtility.SetDirty(anchor);
            }

            Selection.activeGameObject = group;
            SceneView.RepaintAll();
        }

        private void DrawPointTools(VFXSimpleSpline spline)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Point Tools", EditorStyles.boldLabel);
            int pointCount = spline.GetActivePointCount();
            if (pointCount <= 0) return;

            DrawSelectedBezierPointTools(spline, pointCount);

            for (int i = 0; i < pointCount; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Point " + i, GUILayout.Width(80));
                    if (GUILayout.Button("Insert After"))
                    {
                        int index = i + 1;
                        ModifySpline(spline, "Insert Point After", () => spline.InsertPoint(index));
                        break;
                    }
                    EditorGUI.BeginDisabledGroup(pointCount <= 2);
                    if (GUILayout.Button("Delete"))
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
            EditorGUILayout.LabelField("Selected Bezier Point " + selected, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            VFXBezierHandleMode mode = (VFXBezierHandleMode)EditorGUILayout.EnumPopup("Handle Mode", point.handleMode);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spline, "Change Bezier Handle Mode");
                spline.SetBezierHandleMode(selected, mode);
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Free Handles"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.Free);

                if (GUILayout.Button("Aligned"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.Aligned);

                if (GUILayout.Button("Mirrored"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.Mirrored);

                if (GUILayout.Button("Auto Smooth"))
                    SetSelectedBezierHandleMode(spline, selected, VFXBezierHandleMode.AutoSmooth);
            }

            if (GUILayout.Button("Auto Smooth All Bezier Points"))
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

        private void OnSceneGUI()
        {
            VFXSimpleSpline spline = (VFXSimpleSpline)target;
            VFXSplineSceneDrawer.DrawSpline(spline, true);
            VFXSplineSceneDrawer.DrawEditablePoints(spline);
        }
    }

    [InitializeOnLoad]
    public static class VFXSplineSceneDrawer
    {
        static VFXSplineSceneDrawer()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.duringSceneGui += DuringSceneGUI;
        }

        private static void DuringSceneGUI(SceneView view)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;

            VFXSimpleSpline[] splines = Object.FindObjectsOfType<VFXSimpleSpline>();
            for (int i = 0; i < splines.Length; i++)
            {
                VFXSimpleSpline spline = splines[i];
                if (spline == null || !spline.alwaysShowPathInSceneView) continue;
                if (Selection.activeGameObject == spline.gameObject) continue;
                DrawSpline(spline, false);
            }
        }

        public static void DrawSpline(VFXSimpleSpline spline, bool selected)
        {
            if (spline == null || spline.GetActivePointCount() < 2) return;

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
        }

        private static void DrawProgressMarks(VFXSimpleSpline spline)
        {
            Handles.color = spline.progressMarkColor;
            int count = Mathf.Max(1, spline.progressMarkCount);
            for (int i = 0; i <= count; i++)
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

        public static void DrawEditablePoints(VFXSimpleSpline spline)
        {
            if (spline == null) return;

            int count = spline.GetActivePointCount();
            if (count == 0) return;

            if (spline.selectedPointIndex < 0 || spline.selectedPointIndex >= count)
                spline.selectedPointIndex = Mathf.Clamp(spline.selectedPointIndex, 0, count - 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 world = spline.GetEffectiveWorldPoint(i);
                bool isDynamicBoundPoint = spline.IsPointDynamicallyBound(i);
                Transform boundTransform = spline.GetDynamicBindingTransformForPoint(i);
                float size = HandleUtility.GetHandleSize(world) * spline.pointSize;
                bool isPrimarySelectedPoint = spline.selectedPointIndex == i;
                bool isSelectedPoint = spline.showAllPointHandles || isPrimarySelectedPoint;

                Handles.color = isDynamicBoundPoint ? new Color(0.2f, 1f, 0.35f, 1f) : (isSelectedPoint ? Color.yellow : spline.pathColor);

                if (spline.pathMode == VFXSplinePathMode.Bezier)
                    HandleBezierPointContextMenu(spline, i, world, size);

                // 未选中的控制点只显示小球；点击小球后，才显示该点的 Position Handle。
                if (Handles.Button(world, Quaternion.identity, size, size * 1.25f, Handles.SphereHandleCap))
                {
                    Undo.RecordObject(spline, "Select Spline Point");
                    spline.selectedPointIndex = i;
                    EditorUtility.SetDirty(spline);
                    SceneView.RepaintAll();
                }

                if (spline.showPointLabels)
                {
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                    style.normal.textColor = isDynamicBoundPoint ? new Color(0.2f, 1f, 0.35f, 1f) : (isSelectedPoint ? Color.yellow : Color.white);
                    string label = isSelectedPoint ? (i + "  ◀") : i.ToString();
                    if (isDynamicBoundPoint && spline.showDynamicBindingLabels)
                        label += i == 0 ? "  Start Bound" : "  End Bound";
                    Handles.Label(world + Vector3.up * size * 1.5f, label, style);
                }

                if (!isSelectedPoint)
                    continue;

                if (spline.pathMode == VFXSplinePathMode.Bezier)
                {
                    DrawBezierHandles(spline, i, count, world, size);
                    if (isPrimarySelectedPoint)
                        DrawBezierHandleModeToolbar(spline, i, world, size);
                }

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
        }

        private static void HandleBezierPointContextMenu(VFXSimpleSpline spline, int index, Vector3 pointWorld, float pointSize)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 1)
                return;

            Vector2 pointGui = HandleUtility.WorldToGUIPoint(pointWorld);
            float radius = Mathf.Max(18f, pointSize * 90f);
            if (Vector2.Distance(e.mousePosition, pointGui) > radius)
                return;

            Undo.RecordObject(spline, "Select Spline Point");
            spline.selectedPointIndex = index;
            EditorUtility.SetDirty(spline);

            GenericMenu menu = new GenericMenu();
            AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.Free, "Handle Mode/Free Handles");
            AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.Aligned, "Handle Mode/Aligned");
            AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.Mirrored, "Handle Mode/Mirrored");
            AddBezierModeMenuItem(menu, spline, index, VFXBezierHandleMode.AutoSmooth, "Handle Mode/Auto Smooth");
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Auto Smooth All"), false, () =>
            {
                Undo.RecordObject(spline, "Auto Smooth All Bezier Points");
                spline.AutoSmoothAllBezierPoints();
                EditorUtility.SetDirty(spline);
                SceneView.RepaintAll();
            });
            e.Use();
            menu.ShowAsContext();
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

        private static void DrawBezierHandleModeToolbar(VFXSimpleSpline spline, int index, Vector3 pointWorld, float pointSize)
        {
            if (spline.bezierPoints == null || index < 0 || index >= spline.bezierPoints.Count || spline.bezierPoints[index] == null)
                return;

            Vector2 guiPoint = HandleUtility.WorldToGUIPoint(pointWorld + Vector3.up * pointSize * 2.2f);
            Rect rect = new Rect(guiPoint.x + 12f, guiPoint.y - 18f, 228f, 22f);
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
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.Free, "Free");
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.Aligned, "Align");
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.Mirrored, "Mirror");
            DrawBezierModeButton(spline, index, VFXBezierHandleMode.AutoSmooth, "Auto");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void DrawBezierModeButton(VFXSimpleSpline spline, int index, VFXBezierHandleMode mode, string label)
        {
            bool selected = spline.bezierPoints[index].handleMode == mode;
            EditorGUI.BeginDisabledGroup(selected);
            if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(54f)))
            {
                ApplyBezierHandleMode(spline, index, mode);
                Event.current.Use();
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void ApplyBezierHandleMode(VFXSimpleSpline spline, int index, VFXBezierHandleMode mode)
        {
            Undo.RecordObject(spline, "Change Bezier Handle Mode");
            spline.SetBezierHandleMode(index, mode);
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

            if (index > 0)
                DrawBezierTangentHandle(spline, index, true, pointWorld, pointSize);

            if (index < pointCount - 1)
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

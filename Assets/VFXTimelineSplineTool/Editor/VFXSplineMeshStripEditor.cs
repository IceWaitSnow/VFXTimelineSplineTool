#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VFXTimelineSplineTool.EditorTools
{
    using VFXTimelineSplineTool;

    [CustomEditor(typeof(VFXSplineMeshStrip))]
    public class VFXSplineMeshStripEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            VFXSplineMeshStrip strip = (VFXSplineMeshStrip)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("VFX Spline Mesh Strip - 路径面片生成 v" + VFXSplineToolVersion.Version, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("从 Spline 的一段 Progress 实时生成带状 Mesh。UV 的 U 方向按路径长度铺开，适合做光带、流动贴图、路径面片和烘焙 FBX。", MessageType.Info);

            serializedObject.Update();

            EditorGUILayout.LabelField("Spline 路径", EditorStyles.boldLabel);
            DrawProperty("spline", "Spline 路径");
            DrawProperty("startProgress", "起始 Progress");
            DrawProperty("endProgress", "结束 Progress");
            DrawProperty("useDistanceBasedProgress", "使用距离等速 Progress");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Mesh 网格", EditorStyles.boldLabel);
            DrawProperty("width", "宽度");
            DrawShapeModeProperty();
            DrawProperty("segments", "路径细分");
            DrawShapeSettings(strip);
            DrawProperty("doubleSided", "双面三角面");
            DrawPointWidthControls(strip);
            DrawPointTwistControls(strip);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("法线", EditorStyles.boldLabel);
            DrawNormalModeProperty();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("UV", EditorStyles.boldLabel);
            DrawProperty("uvTiling", "UV 平铺");
            DrawProperty("uvOffset", "UV 偏移");
            DrawProperty("animateUvInPlayMode", "运行时滚动 UV");
            DrawProperty("uvScrollSpeed", "UV 滚动速度");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
            DrawProperty("rebuildInEditMode", "编辑模式实时重建");

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("使用父级 Spline", "自动查找当前物体或父级上的 VFXSimpleSpline，并填入 Spline 路径字段。")))
                {
                    Undo.RecordObject(strip, "Assign Spline Mesh Strip Spline");
                    strip.spline = strip.GetComponentInParent<VFXSimpleSpline>();
                    EditorUtility.SetDirty(strip);
                    strip.RebuildMesh();
                }

                if (GUILayout.Button(new GUIContent("重建 Mesh", "按当前参数立即重新生成预览 Mesh。修改参数后通常会自动更新，也可以手动点这里刷新。")))
                {
                    strip.RebuildMesh();
                    EditorUtility.SetDirty(strip);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("烘焙 Mesh 资源", "把当前生成结果复制成一个真正的 .asset Mesh 文件，适合在 Unity 内复用。")))
                    BakeMeshAsset(strip, false);

                if (GUILayout.Button(new GUIContent("烘焙 Mesh 物体", "先保存 .asset Mesh，再在场景中创建一个普通 MeshFilter/MeshRenderer 物体。")))
                    BakeMeshAsset(strip, true);
            }

            if (GUILayout.Button(new GUIContent("烘焙 FBX", "需要安装 Unity FBX Exporter 包。安装后会把当前生成结果导出为 .fbx 文件。")))
                BakeFbx(strip);

            MeshFilter meshFilter = strip.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("顶点数", mesh.vertexCount.ToString());
                EditorGUILayout.LabelField("三角面数", (mesh.triangles.Length / 3).ToString());
            }
        }

        private void DrawProperty(string name, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label, GetPropertyTooltip(name)));
        }

        private void DrawNormalModeProperty()
        {
            SerializedProperty property = serializedObject.FindProperty("normalMode");
            if (property == null)
                return;

            string[] labels = { "使用 Spline 法线", "重新计算法线" };
            GUIContent content = new GUIContent("法线模式", GetPropertyTooltip("normalMode"));
            property.enumValueIndex = EditorGUILayout.Popup(content, property.enumValueIndex, labels);
        }

        private void DrawShapeModeProperty()
        {
            SerializedProperty property = serializedObject.FindProperty("shapeMode");
            if (property == null)
                return;

            string[] labels = { "单个片", "十字片", "管子", "自定义截面" };
            GUIContent content = new GUIContent("形状预设", GetPropertyTooltip("shapeMode"));
            property.enumValueIndex = EditorGUILayout.Popup(content, property.enumValueIndex, labels);
        }

        private void DrawShapeSettings(VFXSplineMeshStrip strip)
        {
            SerializedProperty shapeModeProperty = serializedObject.FindProperty("shapeMode");
            if (shapeModeProperty == null)
                return;

            VFXSplineMeshStripShapeMode mode = (VFXSplineMeshStripShapeMode)shapeModeProperty.enumValueIndex;
            if (mode == VFXSplineMeshStripShapeMode.Plane || mode == VFXSplineMeshStripShapeMode.Cross)
                DrawProperty("widthSegments", mode == VFXSplineMeshStripShapeMode.Cross ? "单片横向细分" : "横向细分");
            else if (mode == VFXSplineMeshStripShapeMode.Tube)
                DrawProperty("tubeSegments", "管子边数");
            else if (mode == VFXSplineMeshStripShapeMode.Custom)
                DrawCustomShapeSettings(strip);
        }

        private void DrawCustomShapeSettings(VFXSplineMeshStrip strip)
        {
            DrawProperty("customShapeClosed", "闭合自定义截面");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("重置为线段", "把自定义截面重置为一条横向线，效果接近单个片。")))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(strip, "Reset Custom Spline Mesh Shape");
                    strip.ResetCustomShapeToPlane();
                    strip.RebuildMesh();
                    EditorUtility.SetDirty(strip);
                    serializedObject.Update();
                }

                if (GUILayout.Button(new GUIContent("重置为菱形", "把自定义截面重置为闭合菱形，可作为管子或特殊截面的起点。")))
                {
                    serializedObject.ApplyModifiedProperties();
                    Undo.RecordObject(strip, "Reset Custom Spline Mesh Shape");
                    strip.ResetCustomShapeToDiamond();
                    strip.RebuildMesh();
                    EditorUtility.SetDirty(strip);
                    serializedObject.Update();
                }
            }

            SerializedProperty points = serializedObject.FindProperty("customShapePoints");
            if (points != null)
                EditorGUILayout.PropertyField(points, new GUIContent("自定义截面点", GetPropertyTooltip("customShapePoints")), true);
        }

        private static string GetPropertyTooltip(string name)
        {
            switch (name)
            {
                case "spline": return "用于生成面片的 VFXSimpleSpline 路径。可以手动指定，也可以点击“使用父级 Spline”自动填入。";
                case "startProgress": return "截取曲线的起始进度，0 表示路径起点。";
                case "endProgress": return "截取曲线的结束进度，1 表示路径终点。闭合路径下，如果结束值小于起始值，会跨过 1 再回到 0。";
                case "useDistanceBasedProgress": return "开启后按距离均匀采样，面片沿路径分布更稳定；关闭后按原始曲线参数采样。";
                case "width": return "面片基础宽度。最终宽度还会乘以每个控制点的宽度倍率。";
                case "shapeMode": return "选择沿路径扫描的截面形状。单个片适合普通条带；十字片适合交叉 UV 条带；管子会生成圆形管状网格；自定义截面可手动编辑 2D 截面点。";
                case "segments": return "沿路径方向的细分数量。数值越高越平滑，顶点和三角面也越多。";
                case "widthSegments": return "横向宽度方向的细分数量。1 表示只有左右两列顶点；提高后会增加横向线段，方便做横向渐变或顶点动画。";
                case "tubeSegments": return "管子截面的圆周边数。数值越高越圆滑，顶点和三角面也越多。";
                case "customShapeClosed": return "开启后会连接自定义截面的最后一个点和第一个点，形成闭合截面。";
                case "customShapePoints": return "自定义 2D 截面点。X 表示横向，Y 表示法线方向，数值会乘以宽度后沿路径扫描生成 Mesh。";
                case "doubleSided": return "生成正反两面的三角面。材质不支持双面显示时可以开启，但三角面数量会翻倍。";
                case "usePointWidthMultipliers": return "开启后，每个 Spline 控制点都可以设置一个宽度倍率，用来控制局部变粗或变细。";
                case "smoothPointWidth": return "开启后，控制点之间的宽度倍率会平滑过渡；关闭后使用线性过渡。";
                case "usePointTwistDegrees": return "开启后，每个 Spline 控制点都可以设置一个截面旋转角度，用来控制面片翻转或扭转。";
                case "smoothPointTwist": return "开启后，控制点之间的旋转角度会平滑过渡；关闭后使用线性过渡。";
                case "normalMode": return "使用 Spline 法线会写入更平滑的自定义法线；重新计算法线会让 Unity 按三角面几何重新生成法线。";
                case "uvTiling": return "UV 平铺倍数。X 控制沿路径方向重复次数，Y 控制横向宽度方向重复次数。";
                case "uvOffset": return "UV 静态偏移。可用来手动调整贴图位置。";
                case "animateUvInPlayMode": return "运行时自动累加 UV 偏移，适合快速预览流动效果。";
                case "uvScrollSpeed": return "运行时 UV 滚动速度。X 通常表示沿路径方向流动，Y 表示横向流动。";
                case "rebuildInEditMode": return "编辑模式下实时重建 Mesh。关闭后需要点击“重建 Mesh”手动刷新。";
                default: return "";
            }
        }

        private void DrawPointWidthControls(VFXSplineMeshStrip strip)
        {
            DrawProperty("usePointWidthMultipliers", "按控制点调宽度");

            SerializedProperty enabledProperty = serializedObject.FindProperty("usePointWidthMultipliers");
            if (enabledProperty == null || !enabledProperty.boolValue)
                return;

            DrawProperty("smoothPointWidth", "平滑宽度过渡");

            SerializedProperty multipliers = serializedObject.FindProperty("pointWidthMultipliers");
            int pointCount = strip != null && strip.spline != null ? strip.spline.GetActivePointCount() : 0;
            bool countMismatch = multipliers != null && multipliers.arraySize != pointCount;
            if (countMismatch)
                EditorGUILayout.HelpBox("宽度倍率数量和当前 Spline 控制点数量不一致，请点击“同步宽度点”。", MessageType.None);

            if (GUILayout.Button(new GUIContent("同步宽度点", "按当前 Spline 控制点数量补齐或裁掉宽度倍率列表。新增点默认宽度倍率为 1。")))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(strip, "Sync Spline Mesh Strip Width Points");
                strip.SyncPointWidthMultipliers();
                strip.RebuildMesh();
                EditorUtility.SetDirty(strip);
                serializedObject.Update();
            }

            if (multipliers == null)
                return;

            EditorGUILayout.Space(2);
            using (new EditorGUI.DisabledScope(countMismatch))
            {
                for (int i = 0; i < multipliers.arraySize; i++)
                {
                    SerializedProperty value = multipliers.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(value, new GUIContent("P" + i + " 宽度倍率", "控制点 P" + i + " 附近的面片宽度倍率。1 为原始宽度，0.5 为一半，0 可收尖，2 为两倍。"));
                    if (value.floatValue < 0f)
                        value.floatValue = 0f;
                }
            }
        }

        private void DrawPointTwistControls(VFXSplineMeshStrip strip)
        {
            DrawProperty("usePointTwistDegrees", "按控制点调旋转");

            SerializedProperty enabledProperty = serializedObject.FindProperty("usePointTwistDegrees");
            if (enabledProperty == null || !enabledProperty.boolValue)
                return;

            DrawProperty("smoothPointTwist", "平滑旋转过渡");

            SerializedProperty twists = serializedObject.FindProperty("pointTwistDegrees");
            int pointCount = strip != null && strip.spline != null ? strip.spline.GetActivePointCount() : 0;
            bool countMismatch = twists != null && twists.arraySize != pointCount;
            if (countMismatch)
                EditorGUILayout.HelpBox("旋转角度数量和当前 Spline 控制点数量不一致，请点击“同步旋转点”。", MessageType.None);

            if (GUILayout.Button(new GUIContent("同步旋转点", "按当前 Spline 控制点数量补齐或裁掉旋转角度列表。新增点默认角度为 0。")))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(strip, "Sync Spline Mesh Strip Twist Points");
                strip.SyncPointTwistDegrees();
                strip.RebuildMesh();
                EditorUtility.SetDirty(strip);
                serializedObject.Update();
            }

            if (twists == null)
                return;

            EditorGUILayout.Space(2);
            using (new EditorGUI.DisabledScope(countMismatch))
            {
                for (int i = 0; i < twists.arraySize; i++)
                {
                    SerializedProperty value = twists.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(value, new GUIContent("P" + i + " 旋转角度", "控制点 P" + i + " 附近的截面扭转角度，单位为度。正负值会绕路径切线反向旋转。"));
                }
            }
        }

        private static void BakeMeshAsset(VFXSplineMeshStrip strip, bool createObject)
        {
            if (strip == null)
                return;

            strip.RebuildMesh();

            MeshFilter sourceFilter = strip.GetComponent<MeshFilter>();
            Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (sourceMesh == null || sourceMesh.vertexCount == 0)
            {
                EditorUtility.DisplayDialog("烘焙 Mesh", "当前没有可烘焙的生成 Mesh。请先指定 Spline 并重建 Mesh。", "确定");
                return;
            }

            string defaultName = ObjectNames.NicifyVariableName(strip.gameObject.name) + "_SplineStrip.asset";
            string path = EditorUtility.SaveFilePanelInProject("烘焙 Spline Mesh", defaultName, "asset", "选择烘焙 Mesh 资源的保存位置。");
            if (string.IsNullOrEmpty(path))
                return;

            Mesh bakedMesh = UnityEngine.Object.Instantiate(sourceMesh);
            bakedMesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            bakedMesh.hideFlags = HideFlags.None;

            AssetDatabase.CreateAsset(bakedMesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (createObject)
                CreateBakedMeshObject(strip, bakedMesh);

            EditorGUIUtility.PingObject(bakedMesh);
        }

        private static void CreateBakedMeshObject(VFXSplineMeshStrip strip, Mesh bakedMesh)
        {
            GameObject bakedObject = new GameObject(strip.gameObject.name + "_BakedMesh");
            Undo.RegisterCreatedObjectUndo(bakedObject, "Create Baked Spline Mesh Object");

            bakedObject.transform.SetParent(strip.transform.parent, false);
            bakedObject.transform.SetPositionAndRotation(strip.transform.position, strip.transform.rotation);
            bakedObject.transform.localScale = strip.transform.localScale;

            MeshFilter filter = bakedObject.AddComponent<MeshFilter>();
            bakedObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = bakedMesh;

            Selection.activeGameObject = bakedObject;
        }

        private static void BakeFbx(VFXSplineMeshStrip strip)
        {
            if (strip == null)
                return;

            Type exporterType = FindFbxModelExporterType();
            MethodInfo exportMethod = FindFbxExportObjectMethod(exporterType);
            if (exportMethod == null)
            {
                EditorUtility.DisplayDialog(
                    "烘焙 FBX",
                    "当前项目没有安装 Unity FBX Exporter。\n\n请在 Package Manager 中安装：\ncom.unity.formats.fbx\n\nUnity 重新编译后，就可以用这个按钮导出真正的 .fbx 文件。",
                    "确定");
                return;
            }

            strip.RebuildMesh();

            MeshFilter sourceFilter = strip.GetComponent<MeshFilter>();
            Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (sourceMesh == null || sourceMesh.vertexCount == 0)
            {
                EditorUtility.DisplayDialog("烘焙 FBX", "当前没有可导出的生成 Mesh。请先指定 Spline 并重建 Mesh。", "确定");
                return;
            }

            string defaultName = ObjectNames.NicifyVariableName(strip.gameObject.name) + "_SplineStrip.fbx";
            string path = EditorUtility.SaveFilePanelInProject("烘焙 Spline FBX", defaultName, "fbx", "选择烘焙 FBX 文件的保存位置。");
            if (string.IsNullOrEmpty(path))
                return;

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }

            GameObject exportObject = null;
            Mesh exportMesh = null;
            try
            {
                exportObject = new GameObject(strip.gameObject.name + "_FBX");
                exportObject.hideFlags = HideFlags.HideAndDontSave;
                exportObject.transform.SetPositionAndRotation(strip.transform.position, strip.transform.rotation);
                exportObject.transform.localScale = strip.transform.localScale;

                exportMesh = UnityEngine.Object.Instantiate(sourceMesh);
                exportMesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
                exportMesh.hideFlags = HideFlags.HideAndDontSave;

                MeshFilter exportFilter = exportObject.AddComponent<MeshFilter>();
                exportFilter.sharedMesh = exportMesh;

                exportMethod.Invoke(null, new object[] { path, exportObject });
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();

                UnityEngine.Object exportedAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (exportedAsset != null)
                    EditorGUIUtility.PingObject(exportedAsset);
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                EditorUtility.DisplayDialog("烘焙 FBX 失败", inner.Message, "确定");
                Debug.LogException(inner);
            }
            finally
            {
                if (exportObject != null)
                    UnityEngine.Object.DestroyImmediate(exportObject);
                if (exportMesh != null)
                    UnityEngine.Object.DestroyImmediate(exportMesh);
            }
        }

        private static Type FindFbxModelExporterType()
        {
            Type type = Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter");
                if (type != null)
                    return type;
            }

            return null;
        }

        private static MethodInfo FindFbxExportObjectMethod(Type exporterType)
        {
            if (exporterType == null)
                return null;

            MethodInfo exact = exporterType.GetMethod("ExportObject", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(GameObject) }, null);
            if (exact != null)
                return exact;

            MethodInfo[] methods = exporterType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "ExportObject")
                    continue;

                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(GameObject))
                    return methods[i];
            }

            return null;
        }

    }
}
#endif

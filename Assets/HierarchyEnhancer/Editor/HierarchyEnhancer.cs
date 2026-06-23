using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityTool.HierarchyEnhancer
{
    [InitializeOnLoad]
    public static class HierarchyEnhancer
    {
        private const string EnabledKey = "UnityTool.HierarchyEnhancer.Enabled";
        private const string GuideLinesKey = "UnityTool.HierarchyEnhancer.GuideLines";
        private const string BadgesKey = "UnityTool.HierarchyEnhancer.Badges";
        private const string InactiveFadeKey = "UnityTool.HierarchyEnhancer.InactiveFade";
        private const string RowStripesKey = "UnityTool.HierarchyEnhancer.RowStripes";
        private const string RowSeparatorsKey = "UnityTool.HierarchyEnhancer.RowSeparators";
        private const string SettingsPath = "Assets/HierarchyEnhancer/Editor/HierarchyEnhancerSettings.asset";

        private const string MenuRoot = "Tools/层级增强";
        private const string ContextMenuRoot = "GameObject/层级增强";
        private const string EnabledMenu = MenuRoot + "/启用";
        private const string GuideLinesMenu = MenuRoot + "/显示层级辅助线";
        private const string BadgesMenu = MenuRoot + "/显示组件标签";
        private const string InactiveFadeMenu = MenuRoot + "/淡化未激活对象";
        private const string RowStripesMenu = MenuRoot + "/显示隔行底色";
        private const string RowSeparatorsMenu = MenuRoot + "/显示行分隔线";
        private const string RepaintMenu = MenuRoot + "/刷新 Hierarchy";
        private const string MarkManagerMenu = MenuRoot + "/标记管理器";
        private const string ObjectLabelMenu = MenuRoot + "/设置选中对象标签";
        private const string ComponentBadgesMenu = MenuRoot + "/自定义组件标签";
        private const string AddComponentsMenu = MenuRoot + "/把选中对象组件加入标签";
        private const string ContextSetLabelMenu = ContextMenuRoot + "/设置标签";
        private const string ContextClearLabelMenu = ContextMenuRoot + "/清除标签";
        private const string ContextSetColorMenu = ContextMenuRoot + "/设置颜色";
        private const string ContextClearColorMenu = ContextMenuRoot + "/清除颜色";
        private const string ContextCopyStyleMenu = ContextMenuRoot + "/复制样式";
        private const string ContextPasteStyleMenu = ContextMenuRoot + "/粘贴样式";
        private const string ContextApplyTemplateMenu = ContextMenuRoot + "/应用模板";
        private const string ContextRedPresetMenu = ContextMenuRoot + "/颜色预设/红色";
        private const string ContextYellowPresetMenu = ContextMenuRoot + "/颜色预设/黄色";
        private const string ContextGreenPresetMenu = ContextMenuRoot + "/颜色预设/绿色";
        private const string ContextBluePresetMenu = ContextMenuRoot + "/颜色预设/蓝色";
        private const string ContextPurplePresetMenu = ContextMenuRoot + "/颜色预设/紫色";

        private const float BadgeHeight = 15f;
        private const float BadgePadding = 6f;
        private const float BadgeGap = 3f;
        private const float ObjectIconSize = 16f;
        private const float IndentWidth = 14f;

        private static readonly Dictionary<Type, GUIContent> TypeNameCache = new Dictionary<Type, GUIContent>();
        private static readonly Dictionary<int, Texture> ObjectIconCache = new Dictionary<int, Texture>();
        private static HierarchyEnhancerSettings cachedSettings;
        private static CopiedObjectStyle copiedStyle;
        private static GUIStyle badgeStyle;
        private static GUIStyle nameStyle;
        private static Texture2D whiteTexture;

        static HierarchyEnhancer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
            EditorApplication.hierarchyChanged += ClearObjectCaches;
            AssemblyReloadEvents.beforeAssemblyReload += ClearObjectCaches;
        }

        [MenuItem(EnabledMenu)]
        private static void ToggleEnabled()
        {
            SetBool(EnabledKey, !IsEnabled);
        }

        [MenuItem(EnabledMenu, true)]
        private static bool ValidateToggleEnabled()
        {
            Menu.SetChecked(EnabledMenu, IsEnabled);
            return true;
        }

        [MenuItem(GuideLinesMenu)]
        private static void ToggleGuideLines()
        {
            SetBool(GuideLinesKey, !ShowGuideLines);
        }

        [MenuItem(GuideLinesMenu, true)]
        private static bool ValidateToggleGuideLines()
        {
            Menu.SetChecked(GuideLinesMenu, ShowGuideLines);
            return IsEnabled;
        }

        [MenuItem(BadgesMenu)]
        private static void ToggleBadges()
        {
            SetBool(BadgesKey, !ShowBadges);
        }

        [MenuItem(BadgesMenu, true)]
        private static bool ValidateToggleBadges()
        {
            Menu.SetChecked(BadgesMenu, ShowBadges);
            return IsEnabled;
        }

        [MenuItem(InactiveFadeMenu)]
        private static void ToggleInactiveFade()
        {
            SetBool(InactiveFadeKey, !FadeInactive);
        }

        [MenuItem(InactiveFadeMenu, true)]
        private static bool ValidateToggleInactiveFade()
        {
            Menu.SetChecked(InactiveFadeMenu, FadeInactive);
            return IsEnabled;
        }

        [MenuItem(RowStripesMenu)]
        private static void ToggleRowStripes()
        {
            SetBool(RowStripesKey, !ShowRowStripes);
        }

        [MenuItem(RowStripesMenu, true)]
        private static bool ValidateToggleRowStripes()
        {
            Menu.SetChecked(RowStripesMenu, ShowRowStripes);
            return IsEnabled;
        }

        [MenuItem(RowSeparatorsMenu)]
        private static void ToggleRowSeparators()
        {
            SetBool(RowSeparatorsKey, !ShowRowSeparators);
        }

        [MenuItem(RowSeparatorsMenu, true)]
        private static bool ValidateToggleRowSeparators()
        {
            Menu.SetChecked(RowSeparatorsMenu, ShowRowSeparators);
            return IsEnabled;
        }

        [MenuItem(RepaintMenu)]
        private static void RepaintHierarchy()
        {
            ClearObjectCaches();
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MarkManagerMenu)]
        private static void OpenMarkManager()
        {
            MarkManagerWindow.Open();
        }

        [MenuItem(ObjectLabelMenu)]
        private static void SetSelectedObjectLabel()
        {
            ObjectLabelWindow.Open(Selection.gameObjects);
        }

        [MenuItem(ContextSetLabelMenu, false, 48)]
        private static void SetSelectedObjectLabelFromContext()
        {
            SetSelectedObjectLabel();
        }

        [MenuItem(ObjectLabelMenu, true)]
        private static bool ValidateSetSelectedObjectLabel()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ContextSetLabelMenu, true)]
        private static bool ValidateSetSelectedObjectLabelFromContext()
        {
            return ValidateSetSelectedObjectLabel();
        }

        [MenuItem(ContextClearLabelMenu, false, 49)]
        private static void ClearSelectedObjectLabelFromContext()
        {
            GetOrCreateSettings().SetObjectLabel(Selection.gameObjects, string.Empty);
        }

        [MenuItem(ContextClearLabelMenu, true)]
        private static bool ValidateClearSelectedObjectLabelFromContext()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ContextSetColorMenu, false, 50)]
        private static void SetSelectedObjectColorFromContext()
        {
            ObjectColorWindow.Open(Selection.gameObjects);
        }

        [MenuItem(ContextSetColorMenu, true)]
        private static bool ValidateSetSelectedObjectColorFromContext()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ContextClearColorMenu, false, 51)]
        private static void ClearSelectedObjectColorFromContext()
        {
            GetOrCreateSettings().SetObjectColor(Selection.gameObjects, false, Color.white, false, Color.clear);
        }

        [MenuItem(ContextClearColorMenu, true)]
        private static bool ValidateClearSelectedObjectColorFromContext()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ContextCopyStyleMenu, false, 52)]
        private static void CopySelectedObjectStyleFromContext()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            GetSettingsStyle(selected, out copiedStyle);
        }

        [MenuItem(ContextCopyStyleMenu, true)]
        private static bool ValidateCopySelectedObjectStyleFromContext()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem(ContextPasteStyleMenu, false, 53)]
        private static void PasteObjectStyleFromContext()
        {
            if (copiedStyle == null)
            {
                return;
            }

            GetOrCreateSettings().ApplyCopiedStyle(Selection.gameObjects, copiedStyle);
        }

        [MenuItem(ContextPasteStyleMenu, true)]
        private static bool ValidatePasteObjectStyleFromContext()
        {
            return copiedStyle != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem(ContextApplyTemplateMenu, false, 54)]
        private static void ApplyTemplateFromContext()
        {
            TemplatePickerWindow.Open(Selection.gameObjects);
        }

        [MenuItem(ContextApplyTemplateMenu, true)]
        private static bool ValidateApplyTemplateFromContext()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ContextRedPresetMenu, false, 55)]
        private static void ApplyRedPreset()
        {
            ApplyColorPreset(new Color(1f, 0.28f, 0.22f, 1f), new Color(1f, 0.22f, 0.15f, 0.28f));
        }

        [MenuItem(ContextYellowPresetMenu, false, 56)]
        private static void ApplyYellowPreset()
        {
            ApplyColorPreset(new Color(1f, 0.78f, 0.22f, 1f), new Color(1f, 0.64f, 0.12f, 0.24f));
        }

        [MenuItem(ContextGreenPresetMenu, false, 57)]
        private static void ApplyGreenPreset()
        {
            ApplyColorPreset(new Color(0.45f, 0.9f, 0.48f, 1f), new Color(0.15f, 0.65f, 0.25f, 0.24f));
        }

        [MenuItem(ContextBluePresetMenu, false, 58)]
        private static void ApplyBluePreset()
        {
            ApplyColorPreset(new Color(0.45f, 0.72f, 1f, 1f), new Color(0.15f, 0.42f, 1f, 0.24f));
        }

        [MenuItem(ContextPurplePresetMenu, false, 59)]
        private static void ApplyPurplePreset()
        {
            ApplyColorPreset(new Color(0.82f, 0.58f, 1f, 1f), new Color(0.52f, 0.25f, 0.85f, 0.24f));
        }

        [MenuItem(ContextRedPresetMenu, true)]
        private static bool ValidateRedPreset()
        {
            return HasSelectedGameObjects();
        }

        [MenuItem(ContextYellowPresetMenu, true)]
        private static bool ValidateYellowPreset()
        {
            return HasSelectedGameObjects();
        }

        [MenuItem(ContextGreenPresetMenu, true)]
        private static bool ValidateGreenPreset()
        {
            return HasSelectedGameObjects();
        }

        [MenuItem(ContextBluePresetMenu, true)]
        private static bool ValidateBluePreset()
        {
            return HasSelectedGameObjects();
        }

        [MenuItem(ContextPurplePresetMenu, true)]
        private static bool ValidatePurplePreset()
        {
            return HasSelectedGameObjects();
        }

        private static bool HasSelectedGameObjects()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ComponentBadgesMenu)]
        private static void CustomizeComponentBadges()
        {
            Selection.activeObject = GetOrCreateSettings();
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem(AddComponentsMenu)]
        private static void AddSelectedComponentsToBadges()
        {
            var settings = GetOrCreateSettings();
            settings.AddComponentsFrom(Selection.gameObjects);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem(AddComponentsMenu, true)]
        private static bool ValidateAddSelectedComponentsToBadges()
        {
            return Selection.gameObjects.Length > 0;
        }

        private static bool IsEnabled
        {
            get { return EditorPrefs.GetBool(EnabledKey, true); }
        }

        private static bool ShowGuideLines
        {
            get { return EditorPrefs.GetBool(GuideLinesKey, true); }
        }

        private static bool ShowBadges
        {
            get { return EditorPrefs.GetBool(BadgesKey, false); }
        }

        private static bool FadeInactive
        {
            get { return EditorPrefs.GetBool(InactiveFadeKey, true); }
        }

        private static bool ShowRowStripes
        {
            get { return EditorPrefs.GetBool(RowStripesKey, true); }
        }

        private static bool ShowRowSeparators
        {
            get { return EditorPrefs.GetBool(RowSeparatorsKey, true); }
        }

        private static void SetBool(string key, bool value)
        {
            EditorPrefs.SetBool(key, value);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static bool DrawPreferenceToggle(string label, string key, bool currentValue)
        {
            var nextValue = EditorGUILayout.ToggleLeft(label, currentValue, GUILayout.Width(120f));
            if (nextValue != currentValue)
            {
                SetBool(key, nextValue);
            }

            return nextValue;
        }

        private static void ApplyColorPreset(Color textColor, Color rowColor)
        {
            GetOrCreateSettings().SetObjectColor(Selection.gameObjects, true, textColor, true, rowColor);
        }

        private static void GetSettingsStyle(GameObject gameObject, out CopiedObjectStyle style)
        {
            style = new CopiedObjectStyle();
            var settings = GetSettings();
            if (settings == null)
            {
                return;
            }

            settings.TryGetObjectLabel(gameObject, out style.label);

            ObjectStyle objectStyle;
            if (settings.TryGetObjectStyle(gameObject, out objectStyle))
            {
                style.useTextColor = objectStyle.useTextColor;
                style.textColor = objectStyle.textColor;
                style.useRowColor = objectStyle.useRowColor;
                style.rowColor = objectStyle.rowColor;
            }
        }

        private static HierarchyEnhancerSettings GetSettings()
        {
            if (cachedSettings == null)
            {
                cachedSettings = AssetDatabase.LoadAssetAtPath<HierarchyEnhancerSettings>(SettingsPath);
                if (cachedSettings != null)
                {
                    if (cachedSettings.EnsureDefaultTemplates())
                    {
                        EditorUtility.SetDirty(cachedSettings);
                        AssetDatabase.SaveAssets();
                    }

                    cachedSettings.RebuildLookupCache();
                }
            }

            return cachedSettings;
        }

        private static HierarchyEnhancerSettings GetOrCreateSettings()
        {
            var settings = GetSettings();
            if (settings != null)
            {
                return settings;
            }

            settings = HierarchyEnhancerSettings.CreateDefaultAsset();
            SetCachedSettings(settings);
            return settings;
        }

        private static void SetCachedSettings(HierarchyEnhancerSettings settings)
        {
            cachedSettings = settings;
            if (cachedSettings != null)
            {
                if (cachedSettings.EnsureDefaultTemplates())
                {
                    EditorUtility.SetDirty(cachedSettings);
                    AssetDatabase.SaveAssets();
                }

                cachedSettings.RebuildLookupCache();
            }
        }

        private static void MarkSettingsChanged(HierarchyEnhancerSettings settings)
        {
            SetCachedSettings(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void ClearObjectCaches()
        {
            ObjectIconCache.Clear();
            HierarchyEnhancerSettings.ClearObjectIdCache();
        }

        private static void OnHierarchyWindowItemGUI(int instanceId, Rect selectionRect)
        {
            if (!IsEnabled || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (gameObject == null)
            {
                return;
            }

            var isSelected = IsSelected(gameObject);
            var settings = GetSettings();
            ObjectStyle objectStyle;
            var hasObjectStyle = settings != null;
            if (hasObjectStyle)
            {
                hasObjectStyle = settings.TryGetObjectStyle(gameObject, out objectStyle);
            }
            else
            {
                objectStyle = default(ObjectStyle);
            }

            if (hasObjectStyle && objectStyle.useRowColor && !isSelected)
            {
                DrawOverlay(selectionRect, objectStyle.rowColor);
            }

            if (ShowRowStripes && !isSelected && (!hasObjectStyle || !objectStyle.useRowColor))
            {
                DrawRowStripe(selectionRect, gameObject.transform.GetSiblingIndex());
            }

            if (ShowGuideLines)
            {
                DrawGuideLines(selectionRect, gameObject.transform);
            }

            if (FadeInactive && !gameObject.activeInHierarchy && !isSelected)
            {
                DrawOverlay(selectionRect, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.18f) : new Color(1f, 1f, 1f, 0.35f));
            }

            if (hasObjectStyle && objectStyle.useTextColor && !isSelected)
            {
                DrawObjectName(selectionRect, gameObject, objectStyle, hasObjectStyle);
            }

            if (ShowRowSeparators)
            {
                DrawRowSeparator(selectionRect);
            }

            DrawBadges(selectionRect, gameObject, isSelected, settings);
        }

        private static bool IsSelected(GameObject gameObject)
        {
            var selected = Selection.gameObjects;
            for (var i = 0; i < selected.Length; i++)
            {
                if (selected[i] == gameObject)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawRowStripe(Rect rect, int siblingIndex)
        {
            if ((siblingIndex & 1) != 0)
            {
                return;
            }

            DrawOverlay(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.035f));
        }

        private static void DrawRowSeparator(Rect rect)
        {
            var color = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.28f)
                : new Color(0f, 0f, 0f, 0.13f);
            var y = Mathf.Floor(rect.yMax - 1f);
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1f), color);
        }

        private static void DrawGuideLines(Rect rect, Transform transform)
        {
            var depth = GetDepth(transform);
            if (depth <= 0)
            {
                return;
            }

            var lineColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.13f) : new Color(0f, 0f, 0f, 0.13f);
            var startX = rect.x - (depth * IndentWidth) - 9f;
            var centerY = Mathf.Floor(rect.y + rect.height * 0.5f) + 0.5f;

            for (var i = 0; i < depth; i++)
            {
                var x = Mathf.Floor(startX + i * IndentWidth) + 0.5f;
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), lineColor);
            }

            var elbowX = Mathf.Floor(startX + (depth - 1) * IndentWidth) + 0.5f;
            EditorGUI.DrawRect(new Rect(elbowX, centerY, IndentWidth * 0.75f, 1f), lineColor);
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            var parent = transform.parent;
            while (parent != null)
            {
                depth++;
                parent = parent.parent;
            }

            return depth;
        }

        private static void DrawBadges(Rect rect, GameObject gameObject, bool isSelected, HierarchyEnhancerSettings settings)
        {
            var contents = CollectBadgeContents(gameObject, ShowBadges, settings);
            var objectIcon = GetObjectIcon(gameObject);
            if (contents.Count == 0 && objectIcon == null)
            {
                return;
            }

            EnsureStyles();

            var right = rect.xMax - 4f;
            var y = rect.y + Mathf.Floor((rect.height - BadgeHeight) * 0.5f);
            if (objectIcon != null)
            {
                right = DrawObjectIcon(rect, right, objectIcon);
            }

            var backgroundColor = isSelected
                ? new Color(1f, 1f, 1f, 0.22f)
                : EditorGUIUtility.isProSkin ? new Color(0.08f, 0.08f, 0.08f, 0.75f) : new Color(1f, 1f, 1f, 0.85f);
            var textColor = isSelected
                ? Color.white
                : EditorGUIUtility.isProSkin ? new Color(0.72f, 0.82f, 1f, 1f) : new Color(0.18f, 0.33f, 0.58f, 1f);

            var oldColor = badgeStyle.normal.textColor;
            badgeStyle.normal.textColor = textColor;

            for (var i = contents.Count - 1; i >= 0; i--)
            {
                var content = contents[i];
                var width = Mathf.Ceil(badgeStyle.CalcSize(content).x + BadgePadding * 2f);
                right -= width;

                if (right < rect.x + 80f)
                {
                    break;
                }

                var badgeRect = new Rect(right, y, width, BadgeHeight);
                DrawRoundedLikeRect(badgeRect, backgroundColor);
                GUI.Label(badgeRect, content, badgeStyle);
                right -= BadgeGap;
            }

            badgeStyle.normal.textColor = oldColor;
        }

        private static float DrawObjectIcon(Rect rowRect, float right, Texture icon)
        {
            var iconY = rowRect.y + Mathf.Floor((rowRect.height - ObjectIconSize) * 0.5f);
            var iconRect = new Rect(right - ObjectIconSize, iconY, ObjectIconSize, ObjectIconSize);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            return iconRect.x - BadgeGap;
        }

        private static Texture GetObjectIcon(GameObject gameObject)
        {
            var instanceId = gameObject.GetInstanceID();
            if (ObjectIconCache.TryGetValue(instanceId, out var icon))
            {
                return icon;
            }

            icon = EditorGUIUtility.GetIconForObject(gameObject);
            ObjectIconCache[instanceId] = icon;
            return icon;
        }

        private static List<GUIContent> CollectBadgeContents(GameObject gameObject, bool includeComponents, HierarchyEnhancerSettings settings)
        {
            var result = new List<GUIContent>();

            if (settings != null && settings.TryGetObjectLabel(gameObject, out var objectLabel))
            {
                result.Add(new GUIContent(objectLabel));
            }

            if (!includeComponents)
            {
                return result;
            }

            var components = gameObject.GetComponents<Component>();

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null || component is Transform)
                {
                    continue;
                }

                var content = GetBadgeContent(component.GetType(), settings);
                if (content != null)
                {
                    result.Add(content);
                }

                if (result.Count >= 4)
                {
                    break;
                }
            }

            if (PrefabUtility.GetPrefabInstanceStatus(gameObject) != PrefabInstanceStatus.NotAPrefab)
            {
                result.Insert(0, new GUIContent("PF"));
            }

            return result;
        }

        private static GUIContent GetBadgeContent(Type type, HierarchyEnhancerSettings settings)
        {
            if (settings != null && settings.TryGetLabel(type, out var customLabel))
            {
                return string.IsNullOrEmpty(customLabel) ? null : new GUIContent(customLabel);
            }

            if (TypeNameCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var label = ShortenTypeName(type.Name);
            var content = string.IsNullOrEmpty(label) ? null : new GUIContent(label);
            TypeNameCache[type] = content;
            return content;
        }

        private static string ShortenTypeName(string typeName)
        {
            switch (typeName)
            {
                case "RectTransform":
                    return "Rect";
                case "CanvasRenderer":
                    return null;
                case "Image":
                    return "Img";
                case "RawImage":
                    return "Raw";
                case "Button":
                    return "Btn";
                case "Text":
                case "TextMeshPro":
                case "TextMeshProUGUI":
                    return "Txt";
                case "Animator":
                    return "Anim";
                case "Animation":
                    return "Clip";
                case "ParticleSystem":
                    return "PS";
                case "PlayableDirector":
                    return "TL";
                case "Camera":
                    return "Cam";
                case "Light":
                    return "Lgt";
                case "AudioSource":
                    return "Aud";
                case "Collider":
                case "BoxCollider":
                case "SphereCollider":
                case "CapsuleCollider":
                case "MeshCollider":
                case "Collider2D":
                case "BoxCollider2D":
                case "CircleCollider2D":
                case "PolygonCollider2D":
                    return "Col";
                case "Rigidbody":
                case "Rigidbody2D":
                    return "Rb";
                default:
                    return MakeInitials(typeName);
            }
        }

        private static string MakeInitials(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            typeName = typeName.Replace("Component", string.Empty).Replace("Behaviour", string.Empty);

            var chars = new List<char>(3);
            for (var i = 0; i < typeName.Length && chars.Count < 3; i++)
            {
                var c = typeName[i];
                if (i == 0 || char.IsUpper(c))
                {
                    chars.Add(char.ToUpperInvariant(c));
                }
            }

            if (chars.Count == 0)
            {
                chars.Add(char.ToUpperInvariant(typeName[0]));
            }

            return new string(chars.ToArray());
        }

        private static void EnsureStyles()
        {
            if (badgeStyle != null)
            {
                return;
            }

            badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 1)
            };

            nameStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };

            whiteTexture = Texture2D.whiteTexture;
        }

        private static void DrawOverlay(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
        }

        private static void DrawRoundedLikeRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawObjectName(Rect rect, GameObject gameObject, ObjectStyle style, bool hasObjectStyle)
        {
            EnsureStyles();

            var nameRect = new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height);
            DrawOverlay(nameRect, GetNameBackgroundColor(gameObject, style, hasObjectStyle));

            var oldColor = nameStyle.normal.textColor;
            nameStyle.normal.textColor = style.textColor;
            GUI.Label(nameRect, gameObject.name, nameStyle);
            nameStyle.normal.textColor = oldColor;
        }

        private static Color GetNameBackgroundColor(GameObject gameObject, ObjectStyle style, bool hasObjectStyle)
        {
            var baseColor = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f, 1f)
                : new Color(0.76f, 0.76f, 0.76f, 1f);

            if (ShowRowStripes && (gameObject.transform.GetSiblingIndex() & 1) == 0)
            {
                var stripe = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.035f)
                    : new Color(0f, 0f, 0f, 0.035f);
                baseColor = Blend(baseColor, stripe);
            }

            if (hasObjectStyle && style.useRowColor)
            {
                baseColor = Blend(baseColor, style.rowColor);
            }

            return baseColor;
        }

        private static Color Blend(Color background, Color foreground)
        {
            var alpha = Mathf.Clamp01(foreground.a);
            return new Color(
                Mathf.Lerp(background.r, foreground.r, alpha),
                Mathf.Lerp(background.g, foreground.g, alpha),
                Mathf.Lerp(background.b, foreground.b, alpha),
                1f);
        }

        [Serializable]
        public sealed class BadgeRule
        {
            public bool enabled = true;
            public string componentTypeName;
            public string label;
        }

        [Serializable]
        public sealed class ObjectLabelRule
        {
            public string objectId;
            public string objectName;
            public string label;
            public bool useTextColor;
            public Color textColor = Color.white;
            public bool useRowColor;
            public Color rowColor = new Color(1f, 0.35f, 0.2f, 0.25f);
        }

        [Serializable]
        public sealed class StyleTemplate
        {
            public string name;
            public string label;
            public bool useTextColor = true;
            public Color textColor = Color.white;
            public bool useRowColor = true;
            public Color rowColor = new Color(1f, 0.35f, 0.2f, 0.25f);
        }

        public struct ObjectStyle
        {
            public bool useTextColor;
            public Color textColor;
            public bool useRowColor;
            public Color rowColor;
        }

        public sealed class CopiedObjectStyle
        {
            public string label;
            public bool useTextColor;
            public Color textColor = Color.white;
            public bool useRowColor;
            public Color rowColor = Color.clear;
        }

        public sealed class HierarchyEnhancerSettings : ScriptableObject
        {
            public List<BadgeRule> badgeRules = new List<BadgeRule>();
            public List<ObjectLabelRule> objectLabelRules = new List<ObjectLabelRule>();
            public List<StyleTemplate> styleTemplates = new List<StyleTemplate>();

            [NonSerialized]
            private readonly Dictionary<string, ObjectLabelRule> objectRuleById = new Dictionary<string, ObjectLabelRule>();

            [NonSerialized]
            private readonly Dictionary<string, BadgeRule> badgeRuleByTypeName = new Dictionary<string, BadgeRule>();

            [NonSerialized]
            private bool lookupCacheReady;

            private static readonly Dictionary<int, string> ObjectIdCache = new Dictionary<int, string>();

            public static HierarchyEnhancerSettings Get()
            {
                return GetSettings();
            }

            public static HierarchyEnhancerSettings GetOrCreate()
            {
                return GetOrCreateSettings();
            }

            public static HierarchyEnhancerSettings CreateDefaultAsset()
            {
                var settings = CreateInstance<HierarchyEnhancerSettings>();
                settings.badgeRules.Add(new BadgeRule { componentTypeName = "Camera", label = "Cam" });
                settings.badgeRules.Add(new BadgeRule { componentTypeName = "Light", label = "Lgt" });
                settings.badgeRules.Add(new BadgeRule { componentTypeName = "Animator", label = "Anim" });
                settings.badgeRules.Add(new BadgeRule { componentTypeName = "VFXSimpleSpline", label = "VFX" });
                settings.EnsureDefaultTemplates();

                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
                return settings;
            }

            public bool EnsureDefaultTemplates()
            {
                if (styleTemplates.Count > 0)
                {
                    return false;
                }

                styleTemplates.Add(new StyleTemplate
                {
                    name = "红色粒子",
                    label = "红色粒子",
                    textColor = new Color(1f, 0.38f, 0.32f, 1f),
                    rowColor = new Color(1f, 0.22f, 0.15f, 0.26f)
                });
                styleTemplates.Add(new StyleTemplate
                {
                    name = "路径节点",
                    label = "路径",
                    textColor = new Color(0.45f, 0.72f, 1f, 1f),
                    rowColor = new Color(0.15f, 0.42f, 1f, 0.22f)
                });
                styleTemplates.Add(new StyleTemplate
                {
                    name = "临时对象",
                    label = "临时",
                    textColor = new Color(1f, 0.78f, 0.22f, 1f),
                    rowColor = new Color(1f, 0.64f, 0.12f, 0.22f)
                });
                styleTemplates.Add(new StyleTemplate
                {
                    name = "待整理",
                    label = "待整理",
                    textColor = new Color(0.82f, 0.58f, 1f, 1f),
                    rowColor = new Color(0.52f, 0.25f, 0.85f, 0.22f)
                });

                return true;
            }

            public static void ClearObjectIdCache()
            {
                ObjectIdCache.Clear();
            }

            public void RebuildLookupCache()
            {
                objectRuleById.Clear();
                badgeRuleByTypeName.Clear();

                for (var i = 0; i < objectLabelRules.Count; i++)
                {
                    var rule = objectLabelRules[i];
                    if (rule == null || string.IsNullOrEmpty(rule.objectId))
                    {
                        continue;
                    }

                    objectRuleById[rule.objectId] = rule;
                }

                for (var i = 0; i < badgeRules.Count; i++)
                {
                    var rule = badgeRules[i];
                    if (rule == null || string.IsNullOrWhiteSpace(rule.componentTypeName))
                    {
                        continue;
                    }

                    badgeRuleByTypeName[rule.componentTypeName] = rule;
                }

                lookupCacheReady = true;
            }

            public bool TryGetLabel(Type type, out string label)
            {
                RebuildLookupCacheIfNeeded();
                var fullName = type.FullName;
                var name = type.Name;

                BadgeRule rule;
                if ((fullName != null && badgeRuleByTypeName.TryGetValue(fullName, out rule)) ||
                    badgeRuleByTypeName.TryGetValue(name, out rule))
                {
                    if (rule != null && rule.enabled)
                    {
                        label = rule.label;
                        return true;
                    }
                }

                label = null;
                return false;
            }

            public bool TryGetObjectLabel(GameObject gameObject, out string label)
            {
                ObjectLabelRule rule;
                if (!TryGetObjectRule(gameObject, out rule) || string.IsNullOrEmpty(rule.label))
                {
                    label = null;
                    return false;
                }

                label = rule.label;
                return true;
            }

            public bool TryGetObjectStyle(GameObject gameObject, out ObjectStyle style)
            {
                ObjectLabelRule rule;
                if (!TryGetObjectRule(gameObject, out rule))
                {
                    style = default(ObjectStyle);
                    return false;
                }

                style = new ObjectStyle
                {
                    useTextColor = rule.useTextColor,
                    textColor = rule.textColor,
                    useRowColor = rule.useRowColor,
                    rowColor = rule.rowColor
                };
                return style.useTextColor || style.useRowColor;
            }

            public void SetObjectLabel(GameObject[] gameObjects, string label)
            {
                if (gameObjects == null || gameObjects.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Set Hierarchy Object Label");

                for (var i = 0; i < gameObjects.Length; i++)
                {
                    SetObjectLabel(gameObjects[i], label);
                }

                MarkSettingsChanged(this);
            }

            public void SetObjectColor(GameObject[] gameObjects, bool useTextColor, Color textColor, bool useRowColor, Color rowColor)
            {
                if (gameObjects == null || gameObjects.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Set Hierarchy Object Color");

                for (var i = 0; i < gameObjects.Length; i++)
                {
                    SetObjectColor(gameObjects[i], useTextColor, textColor, useRowColor, rowColor);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ApplyCopiedStyle(GameObject[] gameObjects, CopiedObjectStyle style)
            {
                if (gameObjects == null || gameObjects.Length == 0 || style == null)
                {
                    return;
                }

                Undo.RecordObject(this, "Paste Hierarchy Object Style");

                for (var i = 0; i < gameObjects.Length; i++)
                {
                    ApplyCopiedStyle(gameObjects[i], style);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ApplyTemplate(GameObject[] gameObjects, StyleTemplate template)
            {
                if (gameObjects == null || gameObjects.Length == 0 || template == null)
                {
                    return;
                }

                Undo.RecordObject(this, "Apply Hierarchy Style Template");

                for (var i = 0; i < gameObjects.Length; i++)
                {
                    ApplyTemplate(gameObjects[i], template);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ClearLabel(ObjectLabelRule rule)
            {
                if (rule == null)
                {
                    return;
                }

                Undo.RecordObject(this, "Clear Hierarchy Object Label");
                rule.label = string.Empty;
                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ClearColor(ObjectLabelRule rule)
            {
                if (rule == null)
                {
                    return;
                }

                Undo.RecordObject(this, "Clear Hierarchy Object Color");
                rule.useTextColor = false;
                rule.textColor = Color.white;
                rule.useRowColor = false;
                rule.rowColor = Color.clear;
                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ClearLabels(IList<ObjectLabelRule> rules)
            {
                if (rules == null || rules.Count == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Clear Hierarchy Object Labels");
                for (var i = 0; i < rules.Count; i++)
                {
                    if (rules[i] != null)
                    {
                        rules[i].label = string.Empty;
                    }
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ClearColors(IList<ObjectLabelRule> rules)
            {
                if (rules == null || rules.Count == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Clear Hierarchy Object Colors");
                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule == null)
                    {
                        continue;
                    }

                    rule.useTextColor = false;
                    rule.textColor = Color.white;
                    rule.useRowColor = false;
                    rule.rowColor = Color.clear;
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void RemoveObjectRules(IList<ObjectLabelRule> rules)
            {
                if (rules == null || rules.Count == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Clear Hierarchy Object Marks");
                for (var i = 0; i < rules.Count; i++)
                {
                    objectLabelRules.Remove(rules[i]);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void RemoveInvalidObjectRules()
            {
                Undo.RecordObject(this, "Clean Invalid Hierarchy Marks");
                objectLabelRules.RemoveAll(rule => rule == null || ResolveObject(rule) == null);
                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public static GameObject ResolveObject(ObjectLabelRule rule)
            {
                if (rule == null || string.IsNullOrEmpty(rule.objectId))
                {
                    return null;
                }

                GlobalObjectId globalObjectId;
                if (!GlobalObjectId.TryParse(rule.objectId, out globalObjectId))
                {
                    return null;
                }

                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as GameObject;
            }

            private void SetObjectLabel(GameObject gameObject, string label)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                if (string.IsNullOrWhiteSpace(label))
                {
                    rule.label = string.Empty;
                    CleanupEmptyRules();
                    return;
                }

                rule.objectName = gameObject.name;
                rule.label = label.Trim();
            }

            private void SetObjectColor(GameObject gameObject, bool useTextColor, Color textColor, bool useRowColor, Color rowColor)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                rule.objectName = gameObject.name;
                rule.useTextColor = useTextColor;
                rule.textColor = textColor;
                rule.useRowColor = useRowColor;
                rule.rowColor = rowColor;
            }

            private void ApplyCopiedStyle(GameObject gameObject, CopiedObjectStyle style)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                rule.objectName = gameObject.name;
                rule.label = string.IsNullOrWhiteSpace(style.label) ? string.Empty : style.label.Trim();
                rule.useTextColor = style.useTextColor;
                rule.textColor = style.textColor;
                rule.useRowColor = style.useRowColor;
                rule.rowColor = style.rowColor;
            }

            private void ApplyTemplate(GameObject gameObject, StyleTemplate template)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                rule.objectName = gameObject.name;
                rule.label = string.IsNullOrWhiteSpace(template.label) ? string.Empty : template.label.Trim();
                rule.useTextColor = template.useTextColor;
                rule.textColor = template.textColor;
                rule.useRowColor = template.useRowColor;
                rule.rowColor = template.rowColor;
            }

            private ObjectLabelRule GetOrCreateObjectRule(GameObject gameObject, string objectId)
            {
                RebuildLookupCacheIfNeeded();

                ObjectLabelRule existingRule;
                if (objectRuleById.TryGetValue(objectId, out existingRule))
                {
                    return existingRule;
                }

                for (var i = 0; i < objectLabelRules.Count; i++)
                {
                    var rule = objectLabelRules[i];
                    if (rule != null && rule.objectId == objectId)
                    {
                        return rule;
                    }
                }

                var newRule = new ObjectLabelRule
                {
                    objectId = objectId,
                    objectName = gameObject.name
                };
                objectLabelRules.Add(newRule);
                objectRuleById[objectId] = newRule;
                return newRule;
            }

            private void CleanupEmptyRules()
            {
                objectLabelRules.RemoveAll(rule =>
                    rule == null ||
                    (string.IsNullOrEmpty(rule.label) && !rule.useTextColor && !rule.useRowColor));
                lookupCacheReady = false;
            }

            private static string GetObjectId(GameObject gameObject)
            {
                if (gameObject == null)
                {
                    return null;
                }

                var instanceId = gameObject.GetInstanceID();
                string objectId;
                if (ObjectIdCache.TryGetValue(instanceId, out objectId))
                {
                    return objectId;
                }

                var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
                objectId = globalObjectId.ToString();
                ObjectIdCache[instanceId] = objectId;
                return objectId;
            }

            private bool TryGetObjectRule(GameObject gameObject, out ObjectLabelRule rule)
            {
                RebuildLookupCacheIfNeeded();

                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    rule = null;
                    return false;
                }

                return objectRuleById.TryGetValue(objectId, out rule) && rule != null;
            }

            private void RebuildLookupCacheIfNeeded()
            {
                if (!lookupCacheReady)
                {
                    RebuildLookupCache();
                }
            }

            public void AddComponentsFrom(GameObject[] gameObjects)
            {
                if (gameObjects == null || gameObjects.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Add Hierarchy Badge Rules");

                for (var objectIndex = 0; objectIndex < gameObjects.Length; objectIndex++)
                {
                    var gameObject = gameObjects[objectIndex];
                    if (gameObject == null)
                    {
                        continue;
                    }

                    var components = gameObject.GetComponents<Component>();

                    for (var i = 0; i < components.Length; i++)
                    {
                        AddComponentRule(components[i]);
                    }
                }

                MarkSettingsChanged(this);
            }

            private void AddComponentRule(Component component)
            {
                if (component == null || component is Transform)
                {
                    return;
                }

                var typeName = component.GetType().Name;
                if (badgeRules.Exists(rule => rule != null && rule.componentTypeName == typeName))
                {
                    return;
                }

                badgeRules.Add(new BadgeRule
                {
                    componentTypeName = typeName,
                    label = ShortenTypeName(typeName)
                });
                lookupCacheReady = false;
            }
        }

        [CustomEditor(typeof(HierarchyEnhancerSettings))]
        private sealed class HierarchyEnhancerSettingsEditor : Editor
        {
            private SerializedProperty badgeRules;
            private SerializedProperty objectLabelRules;
            private SerializedProperty styleTemplates;

            private void OnEnable()
            {
                badgeRules = serializedObject.FindProperty("badgeRules");
                objectLabelRules = serializedObject.FindProperty("objectLabelRules");
                styleTemplates = serializedObject.FindProperty("styleTemplates");
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                EditorGUILayout.PropertyField(objectLabelRules, true);
                EditorGUILayout.Space(8f);
                EditorGUILayout.PropertyField(styleTemplates, true);
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("组件标签是可选功能。组件类型名可以填 C# 类名（例如 Camera），也可以填完整类型名。标签留空会隐藏这个组件标签。", MessageType.Info);
                EditorGUILayout.PropertyField(badgeRules, true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("添加选中对象组件"))
                    {
                        ((HierarchyEnhancerSettings)target).AddComponentsFrom(Selection.gameObjects);
                    }

                    if (GUILayout.Button("刷新 Hierarchy"))
                    {
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }

                if (serializedObject.ApplyModifiedProperties())
                {
                    SetCachedSettings((HierarchyEnhancerSettings)target);
                    EditorApplication.RepaintHierarchyWindow();
                }
            }
        }

        private sealed class ObjectLabelWindow : EditorWindow
        {
            private GameObject[] targets;
            private string label;

            public static void Open(GameObject[] gameObjects)
            {
                var window = GetWindow<ObjectLabelWindow>(true, "层级对象标签");
                window.targets = gameObjects;
                window.label = GetSharedLabel(gameObjects);
                window.minSize = new Vector2(320f, 95f);
                window.ShowUtility();
            }

            private void OnGUI()
            {
                if (targets == null || targets.Length == 0)
                {
                    EditorGUILayout.HelpBox("请先选中一个或多个 GameObject。", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField("选中对象", targets.Length.ToString());
                EditorGUI.BeginChangeCheck();
                label = EditorGUILayout.TextField("标签", label);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("应用"))
                    {
                        GetOrCreateSettings().SetObjectLabel(targets, label);
                        Close();
                    }

                    if (GUILayout.Button("清除"))
                    {
                        GetOrCreateSettings().SetObjectLabel(targets, string.Empty);
                        Close();
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    Repaint();
                }
            }

            private static string GetSharedLabel(GameObject[] gameObjects)
            {
                var settings = GetSettings();
                if (settings == null || gameObjects == null || gameObjects.Length == 0)
                {
                    return string.Empty;
                }

                return settings.TryGetObjectLabel(gameObjects[0], out var currentLabel) ? currentLabel : string.Empty;
            }
        }

        private sealed class ObjectColorWindow : EditorWindow
        {
            private GameObject[] targets;
            private bool useTextColor;
            private Color textColor = Color.white;
            private bool useRowColor = true;
            private Color rowColor = new Color(1f, 0.35f, 0.2f, 0.25f);

            public static void Open(GameObject[] gameObjects)
            {
                var window = GetWindow<ObjectColorWindow>(true, "层级对象颜色");
                window.targets = gameObjects;
                window.LoadCurrentStyle(gameObjects);
                window.minSize = new Vector2(330f, 135f);
                window.ShowUtility();
            }

            private void OnGUI()
            {
                if (targets == null || targets.Length == 0)
                {
                    EditorGUILayout.HelpBox("请先选中一个或多个 GameObject。", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField("选中对象", targets.Length.ToString());
                useTextColor = EditorGUILayout.Toggle("文字颜色", useTextColor);
                using (new EditorGUI.DisabledScope(!useTextColor))
                {
                    textColor = EditorGUILayout.ColorField(textColor);
                }

                useRowColor = EditorGUILayout.Toggle("整行颜色", useRowColor);
                using (new EditorGUI.DisabledScope(!useRowColor))
                {
                    rowColor = EditorGUILayout.ColorField(rowColor);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("应用"))
                    {
                        GetOrCreateSettings().SetObjectColor(targets, useTextColor, textColor, useRowColor, rowColor);
                        Close();
                    }

                    if (GUILayout.Button("清除"))
                    {
                        GetOrCreateSettings().SetObjectColor(targets, false, Color.white, false, Color.clear);
                        Close();
                    }
                }
            }

            private void LoadCurrentStyle(GameObject[] gameObjects)
            {
                var settings = GetSettings();
                if (settings == null || gameObjects == null || gameObjects.Length == 0)
                {
                    return;
                }

                if (!settings.TryGetObjectStyle(gameObjects[0], out var currentStyle))
                {
                    return;
                }

                useTextColor = currentStyle.useTextColor;
                textColor = currentStyle.textColor;
                useRowColor = currentStyle.useRowColor;
                rowColor = currentStyle.rowColor;
            }
        }

        private sealed class TemplatePickerWindow : EditorWindow
        {
            private GameObject[] targets;
            private Vector2 scrollPosition;

            public static void Open(GameObject[] gameObjects)
            {
                var window = GetWindow<TemplatePickerWindow>(true, "应用层级模板");
                window.targets = gameObjects;
                window.minSize = new Vector2(320f, 260f);
                window.ShowUtility();
            }

            private void OnGUI()
            {
                if (targets == null || targets.Length == 0)
                {
                    EditorGUILayout.HelpBox("请先选中一个或多个 GameObject。", MessageType.Info);
                    return;
                }

                var settings = GetOrCreateSettings();
                EditorGUILayout.LabelField("选中对象", targets.Length.ToString());
                EditorGUILayout.Space(4f);

                if (settings.styleTemplates.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无模板。可以在层级增强设置资产里添加模板。", MessageType.Info);
                    return;
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                for (var i = 0; i < settings.styleTemplates.Count; i++)
                {
                    var template = settings.styleTemplates[i];
                    if (template == null)
                    {
                        continue;
                    }

                    DrawTemplateButton(settings, template);
                }

                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("编辑模板"))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }

            private void DrawTemplateButton(HierarchyEnhancerSettings settings, StyleTemplate template)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    DrawTemplatePreview(template);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(string.IsNullOrEmpty(template.name) ? "未命名模板" : template.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(string.IsNullOrEmpty(template.label) ? "无标签" : template.label, EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button("应用", GUILayout.Width(58f)))
                    {
                        settings.ApplyTemplate(targets, template);
                        Close();
                    }
                }
            }

            private static void DrawTemplatePreview(StyleTemplate template)
            {
                var rect = GUILayoutUtility.GetRect(34f, 32f, GUILayout.Width(34f), GUILayout.Height(32f));
                var oldColor = GUI.color;

                GUI.color = template.useRowColor ? template.rowColor : new Color(0f, 0f, 0f, 0.12f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                if (template.useTextColor)
                {
                    GUI.color = template.textColor;
                    GUI.Label(rect, "T", EditorStyles.boldLabel);
                }

                GUI.color = oldColor;
            }
        }

        private sealed class MarkManagerWindow : EditorWindow
        {
            private Vector2 scrollPosition;
            private string searchText = string.Empty;
            private bool showGlobalActions;
            private bool showSelectedActions = true;
            private bool showMarkList = true;

            public static void Open()
            {
                var window = GetWindow<MarkManagerWindow>("层级标记管理器");
                window.minSize = new Vector2(520f, 320f);
                window.Show();
            }

            private void OnGUI()
            {
                var settings = GetSettings();
                if (settings == null)
                {
                    DrawEmptyState();
                    return;
                }

                showGlobalActions = EditorGUILayout.Foldout(showGlobalActions, "全局显示设置", true);
                if (showGlobalActions)
                {
                    DrawGlobalActions(settings);
                }

                showSelectedActions = EditorGUILayout.Foldout(showSelectedActions, "当前选中对象", true);
                if (showSelectedActions)
                {
                    DrawSelectedObjectActions(settings);
                }

                DrawToolbar(settings);
                showMarkList = EditorGUILayout.Foldout(showMarkList, "标记列表", true);
                if (showMarkList)
                {
                    DrawRuleList(settings);
                }
            }

            private void DrawEmptyState()
            {
                EditorGUILayout.HelpBox("还没有层级增强设置资产。先给任意对象设置标签或颜色后，这里会显示标记列表。", MessageType.Info);
                if (GUILayout.Button("创建设置资产"))
                {
                    GetOrCreateSettings();
                    Repaint();
                }
            }

            private void DrawGlobalActions(HierarchyEnhancerSettings settings)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawPreferenceToggle("启用", EnabledKey, IsEnabled);
                        DrawPreferenceToggle("层级辅助线", GuideLinesKey, ShowGuideLines);
                        DrawPreferenceToggle("组件标签", BadgesKey, ShowBadges);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawPreferenceToggle("行分隔线", RowSeparatorsKey, ShowRowSeparators);
                        DrawPreferenceToggle("隔行底色", RowStripesKey, ShowRowStripes);
                        DrawPreferenceToggle("淡化未激活", InactiveFadeKey, FadeInactive);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("刷新 Hierarchy"))
                        {
                            RepaintHierarchy();
                        }

                        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
                        {
                            if (GUILayout.Button("选中组件加入标签"))
                            {
                                settings.AddComponentsFrom(Selection.gameObjects);
                                Selection.activeObject = settings;
                                EditorGUIUtility.PingObject(settings);
                            }
                        }

                        if (GUILayout.Button("自定义组件标签"))
                        {
                            Selection.activeObject = settings;
                            EditorGUIUtility.PingObject(settings);
                        }
                    }
                }
            }

            private void DrawSelectedObjectActions(HierarchyEnhancerSettings settings)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var selectedCount = Selection.gameObjects.Length;
                    EditorGUILayout.LabelField("选中数量", selectedCount > 0 ? selectedCount.ToString() : "无", EditorStyles.boldLabel);

                    using (new EditorGUI.DisabledScope(selectedCount == 0))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("设置标签"))
                            {
                                ObjectLabelWindow.Open(Selection.gameObjects);
                            }

                            if (GUILayout.Button("设置颜色"))
                            {
                                ObjectColorWindow.Open(Selection.gameObjects);
                            }

                            if (GUILayout.Button("清标签"))
                            {
                                settings.SetObjectLabel(Selection.gameObjects, string.Empty);
                            }

                            if (GUILayout.Button("清颜色"))
                            {
                                settings.SetObjectColor(Selection.gameObjects, false, Color.white, false, Color.clear);
                            }
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("复制样式"))
                            {
                                GetSettingsStyle(Selection.activeGameObject, out copiedStyle);
                            }

                            using (new EditorGUI.DisabledScope(copiedStyle == null))
                            {
                                if (GUILayout.Button("粘贴样式"))
                                {
                                    settings.ApplyCopiedStyle(Selection.gameObjects, copiedStyle);
                                }
                            }

                            if (GUILayout.Button("应用模板"))
                            {
                                TemplatePickerWindow.Open(Selection.gameObjects);
                            }

                            if (GUILayout.Button("编辑模板"))
                            {
                                Selection.activeObject = settings;
                                EditorGUIUtility.PingObject(settings);
                            }
                        }

                        DrawInlineTemplates(settings);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("红色"))
                            {
                                settings.SetObjectColor(Selection.gameObjects, true, new Color(1f, 0.28f, 0.22f, 1f), true, new Color(1f, 0.22f, 0.15f, 0.28f));
                            }

                            if (GUILayout.Button("黄色"))
                            {
                                settings.SetObjectColor(Selection.gameObjects, true, new Color(1f, 0.78f, 0.22f, 1f), true, new Color(1f, 0.64f, 0.12f, 0.24f));
                            }

                            if (GUILayout.Button("绿色"))
                            {
                                settings.SetObjectColor(Selection.gameObjects, true, new Color(0.45f, 0.9f, 0.48f, 1f), true, new Color(0.15f, 0.65f, 0.25f, 0.24f));
                            }

                            if (GUILayout.Button("蓝色"))
                            {
                                settings.SetObjectColor(Selection.gameObjects, true, new Color(0.45f, 0.72f, 1f, 1f), true, new Color(0.15f, 0.42f, 1f, 0.24f));
                            }

                            if (GUILayout.Button("紫色"))
                            {
                                settings.SetObjectColor(Selection.gameObjects, true, new Color(0.82f, 0.58f, 1f, 1f), true, new Color(0.52f, 0.25f, 0.85f, 0.24f));
                            }
                        }
                    }
                }
            }

            private void DrawInlineTemplates(HierarchyEnhancerSettings settings)
            {
                if (settings.styleTemplates.Count == 0)
                {
                    return;
                }

                EditorGUILayout.LabelField("模板", EditorStyles.miniBoldLabel);

                const int buttonsPerRow = 4;
                var visibleIndex = 0;
                EditorGUILayout.BeginHorizontal();
                for (var i = 0; i < settings.styleTemplates.Count; i++)
                {
                    var template = settings.styleTemplates[i];
                    if (template == null)
                    {
                        continue;
                    }

                    if (visibleIndex > 0 && visibleIndex % buttonsPerRow == 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                    }

                    var buttonLabel = string.IsNullOrEmpty(template.name) ? "未命名模板" : template.name;
                    if (GUILayout.Button(buttonLabel))
                    {
                        settings.ApplyTemplate(Selection.gameObjects, template);
                    }

                    visibleIndex++;
                }

                EditorGUILayout.EndHorizontal();
            }

            private void DrawToolbar(HierarchyEnhancerSettings settings)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("搜索", GUILayout.Width(32f));
                    searchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField);

                    if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    {
                        searchText = string.Empty;
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("清筛选标签", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        settings.ClearLabels(GetVisibleRules(settings));
                        Repaint();
                    }

                    if (GUILayout.Button("清筛选颜色", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        settings.ClearColors(GetVisibleRules(settings));
                        Repaint();
                    }

                    if (GUILayout.Button("清筛选全部", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        settings.RemoveObjectRules(GetVisibleRules(settings));
                        Repaint();
                    }

                    if (GUILayout.Button("清理失效", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        settings.RemoveInvalidObjectRules();
                        Repaint();
                    }
                }
            }

            private List<ObjectLabelRule> GetVisibleRules(HierarchyEnhancerSettings settings)
            {
                var result = new List<ObjectLabelRule>();
                var rules = settings.objectLabelRules;
                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule != null && MatchesSearch(rule))
                    {
                        result.Add(rule);
                    }
                }

                return result;
            }

            private void DrawRuleList(HierarchyEnhancerSettings settings)
            {
                var rules = settings.objectLabelRules;
                if (rules.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无标记。", MessageType.Info);
                    return;
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule == null || !MatchesSearch(rule))
                    {
                        continue;
                    }

                    DrawRuleRow(settings, rule);
                }

                EditorGUILayout.EndScrollView();
            }

            private bool MatchesSearch(ObjectLabelRule rule)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    return true;
                }

                var search = searchText.Trim();
                return Contains(rule.objectName, search) || Contains(rule.label, search);
            }

            private static bool Contains(string value, string search)
            {
                return !string.IsNullOrEmpty(value) &&
                    value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private void DrawRuleRow(HierarchyEnhancerSettings settings, ObjectLabelRule rule)
            {
                var gameObject = HierarchyEnhancerSettings.ResolveObject(rule);
                var isMissing = gameObject == null;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    DrawColorPreview(rule);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        var objectName = isMissing ? rule.objectName + " (失效)" : gameObject.name;
                        EditorGUILayout.LabelField(objectName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(string.IsNullOrEmpty(rule.label) ? "无标签" : rule.label, EditorStyles.miniLabel);
                    }

                    using (new EditorGUI.DisabledScope(isMissing))
                    {
                        if (GUILayout.Button("选中", GUILayout.Width(48f)))
                        {
                            Selection.activeGameObject = gameObject;
                            EditorGUIUtility.PingObject(gameObject);
                        }

                        if (GUILayout.Button("同标签", GUILayout.Width(58f)))
                        {
                            SelectMatchingRules(settings, rule, MatchMode.Label);
                        }

                        if (GUILayout.Button("同样式", GUILayout.Width(58f)))
                        {
                            SelectMatchingRules(settings, rule, MatchMode.Style);
                        }
                    }

                    if (GUILayout.Button("清标签", GUILayout.Width(58f)))
                    {
                        settings.ClearLabel(rule);
                        Repaint();
                    }

                    if (GUILayout.Button("清颜色", GUILayout.Width(58f)))
                    {
                        settings.ClearColor(rule);
                        Repaint();
                    }
                }
            }

            private enum MatchMode
            {
                Label,
                Style
            }

            private void SelectMatchingRules(HierarchyEnhancerSettings settings, ObjectLabelRule sourceRule, MatchMode mode)
            {
                var matches = new List<UnityEngine.Object>();
                for (var i = 0; i < settings.objectLabelRules.Count; i++)
                {
                    var rule = settings.objectLabelRules[i];
                    if (rule == null || !IsMatch(sourceRule, rule, mode))
                    {
                        continue;
                    }

                    var gameObject = HierarchyEnhancerSettings.ResolveObject(rule);
                    if (gameObject != null)
                    {
                        matches.Add(gameObject);
                    }
                }

                Selection.objects = matches.ToArray();
                if (matches.Count > 0)
                {
                    EditorGUIUtility.PingObject(matches[0]);
                }
            }

            private static bool IsMatch(ObjectLabelRule sourceRule, ObjectLabelRule rule, MatchMode mode)
            {
                if (!StringEquals(sourceRule.label, rule.label))
                {
                    return false;
                }

                if (mode == MatchMode.Label)
                {
                    return true;
                }

                return sourceRule.useTextColor == rule.useTextColor &&
                    ColorEquals(sourceRule.textColor, rule.textColor) &&
                    sourceRule.useRowColor == rule.useRowColor &&
                    ColorEquals(sourceRule.rowColor, rule.rowColor);
            }

            private static bool StringEquals(string a, string b)
            {
                return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
            }

            private static bool ColorEquals(Color a, Color b)
            {
                return Mathf.Approximately(a.r, b.r) &&
                    Mathf.Approximately(a.g, b.g) &&
                    Mathf.Approximately(a.b, b.b) &&
                    Mathf.Approximately(a.a, b.a);
            }

            private static void DrawColorPreview(ObjectLabelRule rule)
            {
                var rect = GUILayoutUtility.GetRect(34f, 32f, GUILayout.Width(34f), GUILayout.Height(32f));
                var oldColor = GUI.color;

                GUI.color = rule.useRowColor ? rule.rowColor : new Color(0f, 0f, 0f, 0.12f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                if (rule.useTextColor)
                {
                    GUI.color = rule.textColor;
                    GUI.Label(rect, "T", EditorStyles.boldLabel);
                }

                GUI.color = oldColor;
            }
        }
    }
}

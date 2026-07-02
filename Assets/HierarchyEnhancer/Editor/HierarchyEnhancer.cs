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
        private const string ColoredGuideLinesKey = "UnityTool.HierarchyEnhancer.ColoredGuideLines";
        private const string BadgesKey = "UnityTool.HierarchyEnhancer.Badges";
        private const string InactiveFadeKey = "UnityTool.HierarchyEnhancer.InactiveFade";
        private const string RowStripesKey = "UnityTool.HierarchyEnhancer.RowStripes";
        private const string RowSeparatorsKey = "UnityTool.HierarchyEnhancer.RowSeparators";
        private const string ActiveToggleKey = "UnityTool.HierarchyEnhancer.ActiveToggle";
        private const string SceneBookmarkXKey = "UnityTool.HierarchyEnhancer.SceneBookmarkX";
        private const string SceneBookmarkYKey = "UnityTool.HierarchyEnhancer.SceneBookmarkY";
        private const string SettingsPath = "Assets/HierarchyEnhancer/Editor/HierarchyEnhancerSettings.asset";

        private const string MenuRoot = "Tools/层级增强";
        private const string ContextMenuRoot = "GameObject/层级增强";
        private const string EnabledMenu = MenuRoot + "/启用";
        private const string GuideLinesMenu = MenuRoot + "/显示层级辅助线";
        private const string ColoredGuideLinesMenu = MenuRoot + "/彩色层级辅助线";
        private const string BadgesMenu = MenuRoot + "/显示组件标签";
        private const string InactiveFadeMenu = MenuRoot + "/淡化未激活对象";
        private const string RowStripesMenu = MenuRoot + "/显示隔行底色";
        private const string RowSeparatorsMenu = MenuRoot + "/显示行分隔线";
        private const string ActiveToggleMenu = MenuRoot + "/显示激活开关";
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
        private const string ProjectFolderRedMenu = "Assets/层级增强/文件夹红色";
        private const string ProjectFolderYellowMenu = "Assets/层级增强/文件夹黄色";
        private const string ProjectFolderGreenMenu = "Assets/层级增强/文件夹绿色";
        private const string ProjectFolderBlueMenu = "Assets/层级增强/文件夹蓝色";
        private const string ProjectFolderPurpleMenu = "Assets/层级增强/文件夹紫色";
        private const string ProjectFolderCustomMenu = "Assets/层级增强/文件夹自定义颜色...";
        private const string ProjectFolderApplyChildrenMenu = "Assets/层级增强/文件夹颜色应用到子文件夹";
        private const string ProjectFolderClearChildrenMenu = "Assets/层级增强/清除子文件夹颜色";
        private const string ProjectFolderClearMenu = "Assets/层级增强/清除文件夹颜色";

        private const float BadgeHeight = 15f;
        private const float BadgePadding = 6f;
        private const float BadgeGap = 3f;
        private const float ObjectIconSize = 16f;
        private const float ActiveToggleSize = 16f;
        private const float ActiveTogglePadding = 6f;
        private const float BookmarkIconSize = 16f;
        private const float BookmarkIconGap = 4f;
        private const float IndentWidth = 14f;
        private const float MarkManagerRowHeight = 58f;

        private static readonly Dictionary<Type, GUIContent> TypeNameCache = new Dictionary<Type, GUIContent>();
        private static readonly Dictionary<int, Texture> ObjectIconCache = new Dictionary<int, Texture>();
        private static HierarchyEnhancerSettings cachedSettings;
        private static CopiedObjectStyle copiedStyle;
        private static GUIStyle badgeStyle;
        private static GUIStyle nameStyle;
        private static Texture2D whiteTexture;
        private static int markListVersion;
        private static bool isDraggingSceneBookmark;
        private static Vector2 sceneBookmarkDragOffset;

        static HierarchyEnhancer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            SceneView.duringSceneGui += OnSceneViewGUI;
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

        [MenuItem(ColoredGuideLinesMenu)]
        private static void ToggleColoredGuideLines()
        {
            SetBool(ColoredGuideLinesKey, !ShowColoredGuideLines);
        }

        [MenuItem(ColoredGuideLinesMenu, true)]
        private static bool ValidateToggleColoredGuideLines()
        {
            Menu.SetChecked(ColoredGuideLinesMenu, ShowColoredGuideLines);
            return IsEnabled && ShowGuideLines;
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

        [MenuItem(ActiveToggleMenu)]
        private static void ToggleActiveToggle()
        {
            SetBool(ActiveToggleKey, !ShowActiveToggle);
        }

        [MenuItem(ActiveToggleMenu, true)]
        private static bool ValidateToggleActiveToggle()
        {
            Menu.SetChecked(ActiveToggleMenu, ShowActiveToggle);
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
            ApplyColorPreset(new Color(1f, 0.22f, 0.15f, 0.28f));
        }

        [MenuItem(ContextYellowPresetMenu, false, 56)]
        private static void ApplyYellowPreset()
        {
            ApplyColorPreset(new Color(1f, 0.64f, 0.12f, 0.24f));
        }

        [MenuItem(ContextGreenPresetMenu, false, 57)]
        private static void ApplyGreenPreset()
        {
            ApplyColorPreset(new Color(0.15f, 0.65f, 0.25f, 0.24f));
        }

        [MenuItem(ContextBluePresetMenu, false, 58)]
        private static void ApplyBluePreset()
        {
            ApplyColorPreset(new Color(0.15f, 0.42f, 1f, 0.24f));
        }

        [MenuItem(ContextPurplePresetMenu, false, 59)]
        private static void ApplyPurplePreset()
        {
            ApplyColorPreset(new Color(0.52f, 0.25f, 0.85f, 0.24f));
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

        [MenuItem(ProjectFolderRedMenu, false, 2000)]
        private static void SetSelectedProjectFoldersRed()
        {
            SetSelectedProjectFolderColor(new Color(1f, 0.22f, 0.15f, 0.34f));
        }

        [MenuItem(ProjectFolderYellowMenu, false, 2001)]
        private static void SetSelectedProjectFoldersYellow()
        {
            SetSelectedProjectFolderColor(new Color(1f, 0.64f, 0.12f, 0.32f));
        }

        [MenuItem(ProjectFolderGreenMenu, false, 2002)]
        private static void SetSelectedProjectFoldersGreen()
        {
            SetSelectedProjectFolderColor(new Color(0.15f, 0.65f, 0.25f, 0.32f));
        }

        [MenuItem(ProjectFolderBlueMenu, false, 2003)]
        private static void SetSelectedProjectFoldersBlue()
        {
            SetSelectedProjectFolderColor(new Color(0.15f, 0.42f, 1f, 0.32f));
        }

        [MenuItem(ProjectFolderPurpleMenu, false, 2004)]
        private static void SetSelectedProjectFoldersPurple()
        {
            SetSelectedProjectFolderColor(new Color(0.52f, 0.25f, 0.85f, 0.32f));
        }

        [MenuItem(ProjectFolderCustomMenu, false, 2005)]
        private static void EditSelectedProjectFolderColor()
        {
            var folderPaths = GetSelectedProjectFolderPaths();
            if (folderPaths.Length == 0)
            {
                return;
            }

            ProjectFolderColorWindow.Open(folderPaths);
        }

        [MenuItem(ProjectFolderApplyChildrenMenu, false, 2006)]
        private static void ApplySelectedProjectFolderColorToChildren()
        {
            var folderPaths = GetSelectedProjectFolderPaths();
            if (folderPaths.Length == 0)
            {
                return;
            }

            GetOrCreateSettings().ApplyFolderColorsToChildren(folderPaths);
        }

        [MenuItem(ProjectFolderClearChildrenMenu, false, 2007)]
        private static void ClearSelectedProjectChildrenFolderColor()
        {
            var folderPaths = GetSelectedProjectFolderPaths();
            if (folderPaths.Length == 0)
            {
                return;
            }

            GetOrCreateSettings().ClearFolderColorsFromChildren(folderPaths);
        }

        [MenuItem(ProjectFolderClearMenu, false, 2008)]
        private static void ClearSelectedProjectFolderColor()
        {
            var folderPaths = GetSelectedProjectFolderPaths();
            if (folderPaths.Length == 0)
            {
                return;
            }

            GetOrCreateSettings().SetFolderColor(folderPaths, false, Color.clear);
        }

        [MenuItem(ProjectFolderRedMenu, true)]
        private static bool ValidateSetSelectedProjectFoldersRed()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderYellowMenu, true)]
        private static bool ValidateSetSelectedProjectFoldersYellow()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderGreenMenu, true)]
        private static bool ValidateSetSelectedProjectFoldersGreen()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderBlueMenu, true)]
        private static bool ValidateSetSelectedProjectFoldersBlue()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderPurpleMenu, true)]
        private static bool ValidateSetSelectedProjectFoldersPurple()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderCustomMenu, true)]
        private static bool ValidateEditSelectedProjectFolderColor()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderApplyChildrenMenu, true)]
        private static bool ValidateApplySelectedProjectFolderColorToChildren()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderClearChildrenMenu, true)]
        private static bool ValidateClearSelectedProjectChildrenFolderColor()
        {
            return HasSelectedProjectFolders();
        }

        [MenuItem(ProjectFolderClearMenu, true)]
        private static bool ValidateClearSelectedProjectFolderColor()
        {
            return HasSelectedProjectFolders();
        }

        private static bool HasSelectedProjectFolders()
        {
            return GetSelectedProjectFolderPaths().Length > 0;
        }

        private static void SetSelectedProjectFolderColor(Color color)
        {
            var folderPaths = GetSelectedProjectFolderPaths();
            if (folderPaths.Length == 0)
            {
                return;
            }

            GetOrCreateSettings().SetFolderColor(folderPaths, true, color);
        }

        private static string[] GetSelectedProjectFolderPaths()
        {
            var paths = new List<string>();
            var guids = Selection.assetGUIDs;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    paths.Add(path);
                }
            }

            return paths.ToArray();
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

        private static bool ShowColoredGuideLines
        {
            get { return EditorPrefs.GetBool(ColoredGuideLinesKey, true); }
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

        private static bool ShowActiveToggle
        {
            get { return EditorPrefs.GetBool(ActiveToggleKey, true); }
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

        private static void ApplyColorPreset(Color rowColor)
        {
            GetOrCreateSettings().SetObjectColor(Selection.gameObjects, false, Color.white, true, rowColor);
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
            markListVersion++;
            SetCachedSettings(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }

        private static void ClearObjectCaches()
        {
            markListVersion++;
            ObjectIconCache.Clear();
            HierarchyEnhancerSettings.ClearObjectIdCache();
        }

        private static void OnHierarchyWindowItemGUI(int instanceId, Rect selectionRect)
        {
            if (!IsEnabled)
            {
                return;
            }

            var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (gameObject == null)
            {
                return;
            }

            if (ShowActiveToggle && HandleActiveToggle(selectionRect, gameObject))
            {
                return;
            }

            var settings = GetSettings();
            if (HandleBookmarkIcon(selectionRect, gameObject, settings))
            {
                return;
            }

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var isSelected = IsSelected(gameObject);
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

            var isLocallyInactive = !gameObject.activeSelf;
            var isHierarchyInactive = !gameObject.activeInHierarchy;
            if (isLocallyInactive)
            {
                objectStyle = GetInactiveObjectStyle(objectStyle);
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
                DrawGuideLines(selectionRect, gameObject.transform, settings);
            }

            if (FadeInactive && isHierarchyInactive && !isSelected)
            {
                DrawOverlay(selectionRect, GetInactiveFadeColor());
            }

            if (hasObjectStyle && objectStyle.useTextColor && !isSelected)
            {
                DrawObjectName(selectionRect, gameObject, objectStyle, hasObjectStyle);
            }

            if (ShowRowSeparators)
            {
                DrawRowSeparator(selectionRect);
            }

            DrawBadges(selectionRect, gameObject, isSelected, settings, ShowActiveToggle);

            DrawBookmarkIcon(selectionRect, gameObject, settings);

            if (ShowActiveToggle)
            {
                DrawActiveToggle(selectionRect, gameObject);
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            if (!IsEnabled || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var settings = GetSettings();
            if (settings == null || !settings.TryGetFolderColor(path, out var color))
            {
                return;
            }

            DrawProjectFolderColor(selectionRect, color);
        }

        private static void DrawProjectFolderColor(Rect rect, Color color)
        {
            var rowRect = rect.height <= 20f
                ? new Rect(0f, rect.y, Mathf.Max(EditorGUIUtility.currentViewWidth, rect.xMax + 64f), rect.height)
                : rect;
            var accentRect = new Rect(rowRect.x, rowRect.y, 4f, rowRect.height);

            DrawOverlay(rowRect, color);
            DrawOverlay(accentRect, new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a + 0.28f)));
        }

        private static void OnSceneViewGUI(SceneView sceneView)
        {
            if (!IsEnabled || sceneView == null)
            {
                return;
            }

            var settings = GetSettings();
            var bookmarkCount = CountValidBookmarks(settings);
            if (bookmarkCount == 0)
            {
                return;
            }

            Handles.BeginGUI();
            var panelRect = GetSceneBookmarkPanelRect(sceneView);
            HandleSceneBookmarkPanelDrag(sceneView, panelRect);
            GUI.Box(panelRect, GUIContent.none, EditorStyles.toolbar);

            var dragRect = new Rect(panelRect.x, panelRect.y, 16f, panelRect.height);
            GUI.Label(dragRect, "⋮", EditorStyles.centeredGreyMiniLabel);

            var buttonRect = new Rect(panelRect.x + 16f, panelRect.y, panelRect.width - 16f, panelRect.height);
            var content = new GUIContent("★ " + bookmarkCount, "层级书签");
            if (GUI.Button(buttonRect, content, EditorStyles.toolbarDropDown))
            {
                ShowSceneBookmarkMenu(buttonRect, sceneView, settings);
            }
            Handles.EndGUI();
        }

        private static Rect GetSceneBookmarkPanelRect(SceneView sceneView)
        {
            const float width = 86f;
            const float height = 24f;
            var defaultX = sceneView.position.width - width - 20f;
            var defaultY = sceneView.position.height - height - 18f;
            var x = EditorPrefs.HasKey(SceneBookmarkXKey) ? EditorPrefs.GetFloat(SceneBookmarkXKey) : defaultX;
            var y = EditorPrefs.HasKey(SceneBookmarkYKey) ? EditorPrefs.GetFloat(SceneBookmarkYKey) : defaultY;

            x = Mathf.Clamp(x, 4f, Mathf.Max(4f, sceneView.position.width - width - 4f));
            y = Mathf.Clamp(y, 22f, Mathf.Max(22f, sceneView.position.height - height - 4f));
            return new Rect(x, y, width, height);
        }

        private static void HandleSceneBookmarkPanelDrag(SceneView sceneView, Rect panelRect)
        {
            var currentEvent = Event.current;
            var dragRect = new Rect(panelRect.x, panelRect.y, 16f, panelRect.height);

            EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.Pan);

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && dragRect.Contains(currentEvent.mousePosition))
            {
                isDraggingSceneBookmark = true;
                sceneBookmarkDragOffset = currentEvent.mousePosition - panelRect.position;
                currentEvent.Use();
            }

            if (isDraggingSceneBookmark && currentEvent.type == EventType.MouseDrag)
            {
                var nextPosition = currentEvent.mousePosition - sceneBookmarkDragOffset;
                nextPosition.x = Mathf.Clamp(nextPosition.x, 4f, Mathf.Max(4f, sceneView.position.width - panelRect.width - 4f));
                nextPosition.y = Mathf.Clamp(nextPosition.y, 22f, Mathf.Max(22f, sceneView.position.height - panelRect.height - 4f));
                EditorPrefs.SetFloat(SceneBookmarkXKey, nextPosition.x);
                EditorPrefs.SetFloat(SceneBookmarkYKey, nextPosition.y);
                sceneView.Repaint();
                currentEvent.Use();
            }

            if (currentEvent.type == EventType.MouseUp || currentEvent.rawType == EventType.MouseUp)
            {
                isDraggingSceneBookmark = false;
            }
        }

        private static int CountValidBookmarks(HierarchyEnhancerSettings settings)
        {
            if (settings == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < settings.objectLabelRules.Count; i++)
            {
                var rule = settings.objectLabelRules[i];
                if (rule != null && rule.bookmarked && HierarchyEnhancerSettings.ResolveObject(rule) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ShowSceneBookmarkMenu(Rect buttonRect, SceneView sceneView, HierarchyEnhancerSettings settings)
        {
            var menu = new GenericMenu();
            var added = false;

            for (var i = 0; i < settings.objectLabelRules.Count; i++)
            {
                var rule = settings.objectLabelRules[i];
                if (rule == null || !rule.bookmarked)
                {
                    continue;
                }

                var gameObject = HierarchyEnhancerSettings.ResolveObject(rule);
                if (gameObject == null)
                {
                    continue;
                }

                var itemObject = gameObject;
                var itemView = sceneView;
                menu.AddItem(new GUIContent(GetSceneBookmarkMenuName(rule, gameObject)), false, () =>
                {
                    SelectSceneBookmark(itemObject, itemView);
                });
                added = true;
            }

            if (!added)
            {
                menu.AddDisabledItem(new GUIContent("暂无有效书签"));
            }

            menu.DropDown(buttonRect);
        }

        private static string GetSceneBookmarkMenuName(ObjectLabelRule rule, GameObject gameObject)
        {
            var label = string.IsNullOrWhiteSpace(rule.label) ? string.Empty : "  [" + rule.label.Trim() + "]";
            return gameObject.name + label;
        }

        private static void SelectSceneBookmark(GameObject gameObject, SceneView sceneView)
        {
            if (gameObject == null)
            {
                return;
            }

            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);
            if (sceneView != null)
            {
                sceneView.FrameSelected();
            }
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

        private static bool HandleActiveToggle(Rect rowRect, GameObject gameObject)
        {
            var toggleRect = GetActiveToggleRect(rowRect);
            EditorGUIUtility.AddCursorRect(toggleRect, MouseCursor.Link);

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && toggleRect.Contains(currentEvent.mousePosition))
            {
                Undo.RecordObject(gameObject, "Toggle Active State");
                gameObject.SetActive(!gameObject.activeSelf);
                EditorUtility.SetDirty(gameObject);
                EditorApplication.RepaintHierarchyWindow();
                currentEvent.Use();
                return true;
            }

            return false;
        }

        private static bool HandleBookmarkIcon(Rect rowRect, GameObject gameObject, HierarchyEnhancerSettings settings)
        {
            if (settings == null || !settings.TryGetObjectRuleForGUI(gameObject, out var rule) || rule == null || !rule.bookmarked)
            {
                return false;
            }

            var rect = GetBookmarkIconRect(rowRect);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
            {
                settings.SetBookmark(rule, false);
                currentEvent.Use();
                return true;
            }

            return false;
        }

        private static void DrawActiveToggle(Rect rowRect, GameObject gameObject)
        {
            EditorGUI.Toggle(GetActiveToggleRect(rowRect), gameObject.activeSelf);
        }

        private static void DrawBookmarkIcon(Rect rowRect, GameObject gameObject, HierarchyEnhancerSettings settings)
        {
            if (settings == null || !settings.TryGetObjectRuleForGUI(gameObject, out var rule) || rule == null || !rule.bookmarked)
            {
                return;
            }

            var rect = GetBookmarkIconRect(rowRect);
            var oldColor = GUI.color;
            GUI.color = EditorGUIUtility.isProSkin
                ? new Color(1f, 0.82f, 0.18f, 1f)
                : new Color(0.95f, 0.58f, 0.05f, 1f);
            GUI.Label(rect, "★", EditorStyles.boldLabel);
            GUI.color = oldColor;
        }

        private static Rect GetActiveToggleRect(Rect rowRect)
        {
            var y = rowRect.y + Mathf.Floor((rowRect.height - ActiveToggleSize) * 0.5f);
            return new Rect(rowRect.xMax - ActiveToggleSize - ActiveTogglePadding, y, ActiveToggleSize, ActiveToggleSize);
        }

        private static Rect GetBookmarkIconRect(Rect rowRect)
        {
            var toggleOffset = ShowActiveToggle ? ActiveToggleSize + BookmarkIconGap : 0f;
            var x = rowRect.xMax - ActiveTogglePadding - toggleOffset - BookmarkIconSize;
            var y = rowRect.y + Mathf.Floor((rowRect.height - BookmarkIconSize) * 0.5f);
            return new Rect(x, y, BookmarkIconSize, BookmarkIconSize);
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

        private static void DrawGuideLines(Rect rect, Transform transform, HierarchyEnhancerSettings settings)
        {
            var depth = GetDepth(transform);
            if (depth <= 0)
            {
                return;
            }

            var lineColor = GetGuideLineColor(transform, settings);
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

        private static Color GetGuideLineColor(Transform transform, HierarchyEnhancerSettings settings)
        {
            if (ShowColoredGuideLines && settings != null)
            {
                var current = transform;
                while (current != null)
                {
                    if (settings.TryGetObjectStyle(current.gameObject, out var style) && style.useRowColor)
                    {
                        return GetSubtleGuideLineColor(style.rowColor);
                    }

                    current = current.parent;
                }
            }

            return EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.13f) : new Color(0f, 0f, 0f, 0.13f);
        }

        private static Color GetSubtleGuideLineColor(Color color)
        {
            return new Color(color.r, color.g, color.b, EditorGUIUtility.isProSkin ? 0.28f : 0.34f);
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

        private static void DrawBadges(Rect rect, GameObject gameObject, bool isSelected, HierarchyEnhancerSettings settings, bool reserveActiveToggle)
        {
            var contents = CollectBadgeContents(gameObject, ShowBadges, settings);
            var objectIcon = GetObjectIcon(gameObject);
            if (contents.Count == 0 && objectIcon == null)
            {
                return;
            }

            EnsureStyles();

            var rightReservedWidth = reserveActiveToggle ? ActiveToggleSize + ActiveTogglePadding * 2f : ActiveTogglePadding;
            rightReservedWidth += BookmarkIconSize + BookmarkIconGap;
            var right = rect.xMax - 4f - rightReservedWidth;
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

        private static ObjectStyle GetInactiveObjectStyle(ObjectStyle style)
        {
            if (style.useRowColor)
            {
                style.rowColor = GetInactiveRowColor(style.rowColor);
            }

            if (style.useTextColor)
            {
                style.textColor = GetInactiveTextColor(style.textColor);
            }

            return style;
        }

        private static Color GetInactiveRowColor(Color color)
        {
            var gray = color.grayscale;
            return new Color(
                Mathf.Lerp(color.r, gray, 0.55f),
                Mathf.Lerp(color.g, gray, 0.55f),
                Mathf.Lerp(color.b, gray, 0.55f),
                Mathf.Clamp01(color.a * 0.45f));
        }

        private static Color GetInactiveTextColor(Color color)
        {
            var target = EditorGUIUtility.isProSkin
                ? new Color(0.55f, 0.55f, 0.55f, 1f)
                : new Color(0.45f, 0.45f, 0.45f, 1f);
            return new Color(
                Mathf.Lerp(color.r, target.r, 0.72f),
                Mathf.Lerp(color.g, target.g, 0.72f),
                Mathf.Lerp(color.b, target.b, 0.72f),
                Mathf.Clamp01(color.a * 0.75f));
        }

        private static Color GetInactiveFadeColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.24f)
                : new Color(1f, 1f, 1f, 0.42f);
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
            public string scenePath;
            public string hierarchyPath;
            public string label;
            public bool bookmarked;
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

        [Serializable]
        public sealed class FolderColorRule
        {
            public string folderPath;
            public Color color = new Color(1f, 0.22f, 0.15f, 0.34f);
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

        public class HierarchyEnhancerSettings : ScriptableObject
        {
            public List<BadgeRule> badgeRules = new List<BadgeRule>();
            public List<ObjectLabelRule> objectLabelRules = new List<ObjectLabelRule>();
            public List<StyleTemplate> styleTemplates = new List<StyleTemplate>();
            public List<FolderColorRule> folderColorRules = new List<FolderColorRule>();

            [NonSerialized]
            private readonly Dictionary<string, ObjectLabelRule> objectRuleById = new Dictionary<string, ObjectLabelRule>();

            [NonSerialized]
            private readonly Dictionary<string, BadgeRule> badgeRuleByTypeName = new Dictionary<string, BadgeRule>();

            [NonSerialized]
            private readonly Dictionary<string, FolderColorRule> folderColorRuleByPath = new Dictionary<string, FolderColorRule>();

            [NonSerialized]
            private bool lookupCacheReady;

            private static readonly Dictionary<int, string> ObjectIdCache = new Dictionary<int, string>();
            private static readonly Dictionary<string, GameObject> ResolvedObjectCache = new Dictionary<string, GameObject>();

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
                var settings = CreateInstance<global::UnityTool.HierarchyEnhancer.HierarchyEnhancerSettings>();
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
                ResolvedObjectCache.Clear();
            }

            public void RebuildLookupCache()
            {
                objectRuleById.Clear();
                badgeRuleByTypeName.Clear();
                folderColorRuleByPath.Clear();

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

                for (var i = 0; i < folderColorRules.Count; i++)
                {
                    var rule = folderColorRules[i];
                    if (rule == null || string.IsNullOrEmpty(rule.folderPath))
                    {
                        continue;
                    }

                    folderColorRuleByPath[NormalizeAssetPath(rule.folderPath)] = rule;
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

            public bool TryGetFolderColor(string folderPath, out Color color)
            {
                RebuildLookupCacheIfNeeded();

                FolderColorRule rule;
                if (!folderColorRuleByPath.TryGetValue(NormalizeAssetPath(folderPath), out rule) || rule == null)
                {
                    color = Color.clear;
                    return false;
                }

                color = rule.color;
                return true;
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

            public bool TryGetObjectRuleForGUI(GameObject gameObject, out ObjectLabelRule rule)
            {
                return TryGetObjectRule(gameObject, out rule);
            }

            public static string GetObjectIdentifier(GameObject gameObject)
            {
                return GetObjectId(gameObject);
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

            public void SetFolderColor(string[] folderPaths, bool enabled, Color color)
            {
                if (folderPaths == null || folderPaths.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, enabled ? "Set Project Folder Color" : "Clear Project Folder Color");

                for (var i = 0; i < folderPaths.Length; i++)
                {
                    SetFolderColor(folderPaths[i], enabled, color);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ApplyFolderColorsToChildren(string[] folderPaths)
            {
                if (folderPaths == null || folderPaths.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Apply Project Folder Color To Children");

                for (var i = 0; i < folderPaths.Length; i++)
                {
                    ApplyFolderColorToChildren(folderPaths[i]);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void ClearFolderColorsFromChildren(string[] folderPaths)
            {
                if (folderPaths == null || folderPaths.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, "Clear Project Child Folder Colors");

                for (var i = 0; i < folderPaths.Length; i++)
                {
                    ClearFolderColorFromChildren(folderPaths[i]);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void SetBookmark(GameObject[] gameObjects, bool bookmarked)
            {
                if (gameObjects == null || gameObjects.Length == 0)
                {
                    return;
                }

                Undo.RecordObject(this, bookmarked ? "Add Hierarchy Bookmarks" : "Remove Hierarchy Bookmarks");

                for (var i = 0; i < gameObjects.Length; i++)
                {
                    SetBookmark(gameObjects[i], bookmarked);
                }

                CleanupEmptyRules();
                MarkSettingsChanged(this);
            }

            public void SetBookmark(ObjectLabelRule rule, bool bookmarked)
            {
                if (rule == null)
                {
                    return;
                }

                Undo.RecordObject(this, bookmarked ? "Add Hierarchy Bookmark" : "Remove Hierarchy Bookmark");
                rule.bookmarked = bookmarked;
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

            public void ApplyStyleToChildren(GameObject parent, CopiedObjectStyle style, bool recursive)
            {
                if (parent == null || style == null)
                {
                    return;
                }

                var children = new List<GameObject>();
                CollectChildren(parent.transform, recursive, children);
                if (children.Count == 0)
                {
                    return;
                }

                ApplyCopiedStyle(children.ToArray(), style);
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

            public void AddTemplateFromStyle(string templateName, CopiedObjectStyle style)
            {
                if (style == null)
                {
                    return;
                }

                Undo.RecordObject(this, "Add Hierarchy Style Template");
                var template = new StyleTemplate();
                ApplyStyleToTemplate(template, templateName, style);
                styleTemplates.Add(template);
                MarkSettingsChanged(this);
            }

            public void OverwriteTemplateFromStyle(int index, CopiedObjectStyle style)
            {
                if (style == null || index < 0 || index >= styleTemplates.Count || styleTemplates[index] == null)
                {
                    return;
                }

                Undo.RecordObject(this, "Overwrite Hierarchy Style Template");
                ApplyStyleToTemplate(styleTemplates[index], styleTemplates[index].name, style);
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
                if (rule == null)
                {
                    return null;
                }

                GameObject cachedObject;
                if (!string.IsNullOrEmpty(rule.objectId) && ResolvedObjectCache.TryGetValue(rule.objectId, out cachedObject))
                {
                    return cachedObject;
                }

                GlobalObjectId globalObjectId;
                if (!string.IsNullOrEmpty(rule.objectId) && GlobalObjectId.TryParse(rule.objectId, out globalObjectId))
                {
                    cachedObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as GameObject;
                    if (cachedObject != null)
                    {
                        ResolvedObjectCache[rule.objectId] = cachedObject;
                        return cachedObject;
                    }
                }

                cachedObject = ResolveObjectByHierarchyPath(rule);
                if (cachedObject != null && !string.IsNullOrEmpty(rule.objectId))
                {
                    ResolvedObjectCache[rule.objectId] = cachedObject;
                }

                return cachedObject;
            }

            private void SetObjectLabel(GameObject gameObject, string label)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                UpdateObjectRuleIdentity(rule, gameObject, objectId);
                if (string.IsNullOrWhiteSpace(label))
                {
                    rule.label = string.Empty;
                    CleanupEmptyRules();
                    return;
                }

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
                UpdateObjectRuleIdentity(rule, gameObject, objectId);
                rule.useTextColor = useTextColor;
                rule.textColor = textColor;
                rule.useRowColor = useRowColor;
                rule.rowColor = rowColor;
            }

            private void SetBookmark(GameObject gameObject, bool bookmarked)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                UpdateObjectRuleIdentity(rule, gameObject, objectId);
                rule.bookmarked = bookmarked;
            }

            private void ApplyCopiedStyle(GameObject gameObject, CopiedObjectStyle style)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                UpdateObjectRuleIdentity(rule, gameObject, objectId);
                rule.label = string.IsNullOrWhiteSpace(style.label) ? string.Empty : style.label.Trim();
                rule.useTextColor = style.useTextColor;
                rule.textColor = style.textColor;
                rule.useRowColor = style.useRowColor;
                rule.rowColor = style.rowColor;
            }

            private static void CollectChildren(Transform parent, bool recursive, List<GameObject> children)
            {
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    children.Add(child.gameObject);

                    if (recursive)
                    {
                        CollectChildren(child, true, children);
                    }
                }
            }

            private void ApplyTemplate(GameObject gameObject, StyleTemplate template)
            {
                var objectId = GetObjectId(gameObject);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rule = GetOrCreateObjectRule(gameObject, objectId);
                UpdateObjectRuleIdentity(rule, gameObject, objectId);
                rule.label = string.IsNullOrWhiteSpace(template.label) ? string.Empty : template.label.Trim();
                rule.useTextColor = template.useTextColor;
                rule.textColor = template.textColor;
                rule.useRowColor = template.useRowColor;
                rule.rowColor = template.rowColor;
            }

            private static void ApplyStyleToTemplate(StyleTemplate template, string templateName, CopiedObjectStyle style)
            {
                template.name = string.IsNullOrWhiteSpace(templateName) ? "未命名模板" : templateName.Trim();
                template.label = string.IsNullOrWhiteSpace(style.label) ? string.Empty : style.label.Trim();
                template.useTextColor = style.useTextColor;
                template.textColor = style.textColor;
                template.useRowColor = style.useRowColor;
                template.rowColor = style.rowColor;
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
                UpdateObjectRuleIdentity(newRule, gameObject, objectId);
                objectLabelRules.Add(newRule);
                objectRuleById[objectId] = newRule;
                return newRule;
            }

            private void SetFolderColor(string folderPath, bool enabled, Color color)
            {
                folderPath = NormalizeAssetPath(folderPath);
                if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                {
                    return;
                }

                if (!enabled)
                {
                    folderColorRules.RemoveAll(rule => rule == null || NormalizeAssetPath(rule.folderPath) == folderPath);
                    folderColorRuleByPath.Remove(folderPath);
                    return;
                }

                var rule = GetOrCreateFolderColorRule(folderPath);
                rule.color = color;
            }

            private void ApplyFolderColorToChildren(string folderPath)
            {
                folderPath = NormalizeAssetPath(folderPath);
                if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                {
                    return;
                }

                Color color;
                if (!TryGetFolderColor(folderPath, out color))
                {
                    return;
                }

                var childFolderPaths = new List<string>();
                CollectChildFolderPaths(folderPath, childFolderPaths);
                for (var i = 0; i < childFolderPaths.Count; i++)
                {
                    SetFolderColor(childFolderPaths[i], true, color);
                }
            }

            private void ClearFolderColorFromChildren(string folderPath)
            {
                folderPath = NormalizeAssetPath(folderPath);
                if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                {
                    return;
                }

                var childFolderPaths = new List<string>();
                CollectChildFolderPaths(folderPath, childFolderPaths);
                for (var i = 0; i < childFolderPaths.Count; i++)
                {
                    SetFolderColor(childFolderPaths[i], false, Color.clear);
                }
            }

            private FolderColorRule GetOrCreateFolderColorRule(string folderPath)
            {
                RebuildLookupCacheIfNeeded();

                FolderColorRule existingRule;
                if (folderColorRuleByPath.TryGetValue(folderPath, out existingRule) && existingRule != null)
                {
                    return existingRule;
                }

                var newRule = new FolderColorRule
                {
                    folderPath = folderPath
                };
                folderColorRules.Add(newRule);
                folderColorRuleByPath[folderPath] = newRule;
                return newRule;
            }

            private static void CollectChildFolderPaths(string folderPath, List<string> result)
            {
                if (result == null || string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                {
                    return;
                }

                var directories = System.IO.Directory.GetDirectories(folderPath, "*", System.IO.SearchOption.AllDirectories);
                for (var i = 0; i < directories.Length; i++)
                {
                    var childPath = NormalizeAssetPath(directories[i]);
                    if (AssetDatabase.IsValidFolder(childPath))
                    {
                        result.Add(childPath);
                    }
                }
            }

            private static void UpdateObjectRuleIdentity(ObjectLabelRule rule, GameObject gameObject, string objectId)
            {
                if (rule == null || gameObject == null)
                {
                    return;
                }

                rule.objectId = objectId;
                rule.objectName = gameObject.name;
                rule.scenePath = gameObject.scene.path;
                rule.hierarchyPath = GetHierarchyPath(gameObject);
            }

            private static GameObject ResolveObjectByHierarchyPath(ObjectLabelRule rule)
            {
                if (string.IsNullOrEmpty(rule.hierarchyPath))
                {
                    return null;
                }

                var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                for (var i = 0; i < gameObjects.Length; i++)
                {
                    var gameObject = gameObjects[i];
                    if (gameObject == null || !gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    if (!string.Equals(gameObject.scene.path, rule.scenePath ?? string.Empty, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(GetHierarchyPath(gameObject), rule.hierarchyPath, StringComparison.Ordinal))
                    {
                        return gameObject;
                    }
                }

                return null;
            }

            private ObjectLabelRule FindRuleByHierarchyPath(GameObject gameObject)
            {
                var hierarchyPath = GetHierarchyPath(gameObject);
                var scenePath = gameObject.scene.path;
                for (var i = 0; i < objectLabelRules.Count; i++)
                {
                    var rule = objectLabelRules[i];
                    if (rule == null)
                    {
                        continue;
                    }

                    if (string.Equals(rule.scenePath ?? string.Empty, scenePath, StringComparison.Ordinal) &&
                        string.Equals(rule.hierarchyPath, hierarchyPath, StringComparison.Ordinal))
                    {
                        return rule;
                    }
                }

                return null;
            }

            private static string GetHierarchyPath(GameObject gameObject)
            {
                if (gameObject == null)
                {
                    return string.Empty;
                }

                var path = gameObject.name;
                var parent = gameObject.transform.parent;
                while (parent != null)
                {
                    path = parent.name + "/" + path;
                    parent = parent.parent;
                }

                return path;
            }

            private void CleanupEmptyRules()
            {
                objectLabelRules.RemoveAll(rule =>
                    rule == null ||
                    (string.IsNullOrEmpty(rule.label) && !rule.bookmarked && !rule.useTextColor && !rule.useRowColor));
                folderColorRules.RemoveAll(rule => rule == null || string.IsNullOrEmpty(rule.folderPath));
                lookupCacheReady = false;
            }

            private static string NormalizeAssetPath(string path)
            {
                return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
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

                if (objectRuleById.TryGetValue(objectId, out rule) && rule != null)
                {
                    return true;
                }

                rule = FindRuleByHierarchyPath(gameObject);
                if (rule == null)
                {
                    return false;
                }

                UpdateObjectRuleIdentity(rule, gameObject, objectId);
                objectRuleById[objectId] = rule;
                return true;
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

        [CustomEditor(typeof(HierarchyEnhancerSettings), true)]
        private sealed class HierarchyEnhancerSettingsEditor : Editor
        {
            private SerializedProperty badgeRules;
            private SerializedProperty objectLabelRules;
            private SerializedProperty styleTemplates;
            private SerializedProperty folderColorRules;

            private void OnEnable()
            {
                badgeRules = serializedObject.FindProperty("badgeRules");
                objectLabelRules = serializedObject.FindProperty("objectLabelRules");
                styleTemplates = serializedObject.FindProperty("styleTemplates");
                folderColorRules = serializedObject.FindProperty("folderColorRules");
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();

                EditorGUILayout.PropertyField(objectLabelRules, true);
                EditorGUILayout.Space(8f);
                EditorGUILayout.PropertyField(folderColorRules, true);
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

        private sealed class ProjectFolderColorWindow : EditorWindow
        {
            private string[] folderPaths;
            private Color color = new Color(1f, 0.22f, 0.15f, 0.34f);

            public static void Open(string[] paths)
            {
                var window = GetWindow<ProjectFolderColorWindow>(true, "文件夹颜色");
                window.folderPaths = paths;
                window.LoadInitialValues();
                window.minSize = new Vector2(360f, 112f);
                window.ShowUtility();
            }

            private void LoadInitialValues()
            {
                color = new Color(1f, 0.22f, 0.15f, 0.34f);

                if (folderPaths == null || folderPaths.Length == 0)
                {
                    return;
                }

                var settings = GetSettings();
                if (settings != null && settings.TryGetFolderColor(folderPaths[0], out var currentColor))
                {
                    color = currentColor;
                }
            }

            private void OnGUI()
            {
                if (folderPaths == null || folderPaths.Length == 0)
                {
                    EditorGUILayout.HelpBox("请先在 Project 面板选中一个或多个文件夹。", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField("选中文件夹", folderPaths.Length.ToString());
                color = EditorGUILayout.ColorField("颜色", color);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("应用"))
                    {
                        GetOrCreateSettings().SetFolderColor(folderPaths, true, color);
                        Close();
                    }

                    if (GUILayout.Button("清除标记"))
                    {
                        GetOrCreateSettings().SetFolderColor(folderPaths, false, Color.clear);
                        Close();
                    }
                }
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
                    EditorGUILayout.HelpBox("暂无模板。可以在层级标记管理器里新增模板。", MessageType.Info);
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

        [Serializable]
        public class MarkManagerWindow : EditorWindow
        {
            private Vector2 scrollPosition;
            private string searchText = string.Empty;
            private bool showGlobalActions;
            private bool showSelectedActions = true;
            private bool showMarkList = true;
            private string selectedLabelDraft = string.Empty;
            private string syncedSelectionKey = string.Empty;
            private MarkFilter markFilter = MarkFilter.All;
            private MarkSortMode sortMode = MarkSortMode.Default;
            private int currentPage;
            private int pageSize = 50;
            private int selectedTemplateIndex;
            private string templateDraftName = string.Empty;
            private string focusedObjectId = string.Empty;
            private double focusedObjectUntil;
            private readonly Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
            private readonly List<ObjectLabelRule> visibleRuleCache = new List<ObjectLabelRule>();
            private HierarchyEnhancerSettings cachedVisibleRuleSettings;
            private string cachedVisibleRuleSearch = string.Empty;
            private MarkFilter cachedVisibleRuleFilter = MarkFilter.All;
            private MarkSortMode cachedVisibleRuleSort = MarkSortMode.Default;
            private int cachedVisibleRuleCount = -1;
            private int cachedVisibleRuleVersion = -1;
            private static readonly int[] PageSizeValues = { 25, 50, 100, 200 };
            private static readonly string[] PageSizeLabels = { "25", "50", "100", "200" };
            private static readonly string[] SortModeLabels = { "默认", "名称", "标签", "颜色", "书签优先", "失效优先" };

            public static void Open()
            {
                var window = GetWindow<global::UnityTool.HierarchyEnhancer.MarkManagerWindow>("层级标记管理器");
                window.ConfigureWindow();
                window.Show();
            }

            protected void OnEnable()
            {
                ConfigureWindow();
            }

            protected void ConfigureWindow()
            {
                titleContent = new GUIContent("层级标记管理器");
                minSize = new Vector2(520f, 320f);
            }

            protected void OnGUI()
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
                        DrawPreferenceToggle("彩色辅助线", ColoredGuideLinesKey, ShowColoredGuideLines);
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
                        DrawPreferenceToggle("激活开关", ActiveToggleKey, ShowActiveToggle);
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
                        DrawQuickLabelEditor(settings);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("加入书签"))
                            {
                                settings.SetBookmark(Selection.gameObjects, true);
                            }

                            if (GUILayout.Button("取消书签"))
                            {
                                settings.SetBookmark(Selection.gameObjects, false);
                            }
                        }

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
                        }

                        DrawApplyToChildrenActions(settings);
                        DrawTemplateManager(settings);
                        DrawInlineTemplates(settings);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("红色"))
                            {
                                SetSelectionRowColor(settings, new Color(1f, 0.22f, 0.15f, 0.28f));
                            }

                            if (GUILayout.Button("橙色"))
                            {
                                SetSelectionRowColor(settings, new Color(1f, 0.45f, 0.08f, 0.26f));
                            }

                            if (GUILayout.Button("黄色"))
                            {
                                SetSelectionRowColor(settings, new Color(1f, 0.64f, 0.12f, 0.24f));
                            }

                            if (GUILayout.Button("绿色"))
                            {
                                SetSelectionRowColor(settings, new Color(0.15f, 0.65f, 0.25f, 0.24f));
                            }

                            if (GUILayout.Button("青色"))
                            {
                                SetSelectionRowColor(settings, new Color(0.05f, 0.72f, 0.72f, 0.24f));
                            }

                            if (GUILayout.Button("蓝色"))
                            {
                                SetSelectionRowColor(settings, new Color(0.15f, 0.42f, 1f, 0.24f));
                            }

                            if (GUILayout.Button("紫色"))
                            {
                                SetSelectionRowColor(settings, new Color(0.52f, 0.25f, 0.85f, 0.24f));
                            }
                        }
                    }
                }
            }

            private static void SetSelectionRowColor(HierarchyEnhancerSettings settings, Color rowColor)
            {
                settings.SetObjectColor(Selection.gameObjects, false, Color.white, true, rowColor);
            }

            private void DrawApplyToChildrenActions(HierarchyEnhancerSettings settings)
            {
                var active = Selection.activeGameObject;
                var canApply = Selection.gameObjects.Length == 1 && active != null && active.transform.childCount > 0;

                using (new EditorGUI.DisabledScope(!canApply))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("应用到直接子级"))
                        {
                            GetSettingsStyle(active, out var style);
                            settings.ApplyStyleToChildren(active, style, false);
                        }

                        if (GUILayout.Button("应用到全部子级"))
                        {
                            GetSettingsStyle(active, out var style);
                            settings.ApplyStyleToChildren(active, style, true);
                        }
                    }
                }
            }

            private void DrawQuickLabelEditor(HierarchyEnhancerSettings settings)
            {
                SyncSelectedLabelDraft(settings);

                using (new EditorGUILayout.HorizontalScope())
                {
                    selectedLabelDraft = EditorGUILayout.TextField("快速标签", selectedLabelDraft);

                    if (GUILayout.Button("应用标签", GUILayout.Width(72f)))
                    {
                        settings.SetObjectLabel(Selection.gameObjects, selectedLabelDraft);
                    }
                }
            }

            private void SyncSelectedLabelDraft(HierarchyEnhancerSettings settings)
            {
                var selectionKey = GetSelectionKey();
                if (selectionKey == syncedSelectionKey)
                {
                    return;
                }

                syncedSelectionKey = selectionKey;
                var selected = Selection.gameObjects;
                if (selected.Length == 1 && settings.TryGetObjectLabel(selected[0], out var label))
                {
                    selectedLabelDraft = label;
                }
                else
                {
                    selectedLabelDraft = string.Empty;
                }
            }

            private static string GetSelectionKey()
            {
                var selected = Selection.gameObjects;
                if (selected.Length == 0)
                {
                    return string.Empty;
                }

                var ids = new int[selected.Length];
                for (var i = 0; i < selected.Length; i++)
                {
                    ids[i] = selected[i].GetInstanceID();
                }

                Array.Sort(ids);
                return string.Join(",", Array.ConvertAll(ids, id => id.ToString()));
            }

            private void DrawTemplateManager(HierarchyEnhancerSettings settings)
            {
                EditorGUILayout.LabelField("模板管理", EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    templateDraftName = EditorGUILayout.TextField("模板名", templateDraftName);

                    using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                    {
                        if (GUILayout.Button("新增", GUILayout.Width(54f)))
                        {
                            GetSettingsStyle(Selection.activeGameObject, out var style);
                            settings.AddTemplateFromStyle(GetTemplateNameForCreate(), style);
                            selectedTemplateIndex = settings.styleTemplates.Count - 1;
                            templateDraftName = string.Empty;
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var hasTemplates = settings.styleTemplates.Count > 0;
                    selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, Mathf.Max(0, settings.styleTemplates.Count - 1));

                    if (!hasTemplates)
                    {
                        EditorGUILayout.LabelField("现有模板", "暂无模板");
                        return;
                    }

                    using (new EditorGUI.DisabledScope(!hasTemplates))
                    {
                        selectedTemplateIndex = EditorGUILayout.Popup("现有模板", selectedTemplateIndex, GetTemplateNames(settings));

                        if (GUILayout.Button("覆盖", GUILayout.Width(54f)))
                        {
                            GetSettingsStyle(Selection.activeGameObject, out var style);
                            settings.OverwriteTemplateFromStyle(selectedTemplateIndex, style);
                        }
                    }
                }
            }

            private string GetTemplateNameForCreate()
            {
                if (!string.IsNullOrWhiteSpace(templateDraftName))
                {
                    return templateDraftName;
                }

                return Selection.activeGameObject != null ? Selection.activeGameObject.name + " 样式" : "未命名模板";
            }

            private static string[] GetTemplateNames(HierarchyEnhancerSettings settings)
            {
                var names = new string[settings.styleTemplates.Count];
                for (var i = 0; i < settings.styleTemplates.Count; i++)
                {
                    names[i] = GetTemplateName(settings, i);
                }

                return names;
            }

            private static string GetTemplateName(HierarchyEnhancerSettings settings, int index)
            {
                if (index < 0 || index >= settings.styleTemplates.Count || settings.styleTemplates[index] == null)
                {
                    return "未命名模板";
                }

                var template = settings.styleTemplates[index];
                return string.IsNullOrWhiteSpace(template.name) ? "未命名模板" : template.name;
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
                    var nextSearchText = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField);
                    if (!string.Equals(searchText, nextSearchText, StringComparison.Ordinal))
                    {
                        searchText = nextSearchText;
                        ResetListPage();
                    }

                    if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    {
                        searchText = string.Empty;
                        ResetListPage();
                        GUI.FocusControl(null);
                    }

                    using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                    {
                        if (GUILayout.Button("定位选中", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        {
                            FocusSelectedRule(settings);
                        }
                    }

                    if (GUILayout.Button("清筛选标签", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        var visibleRules = GetVisibleRules(settings);
                        if (ConfirmBatchAction("清筛选标签", "将清除当前筛选结果中 " + visibleRules.Count + " 个对象的标签。"))
                        {
                            settings.ClearLabels(visibleRules);
                            Repaint();
                        }
                    }

                    if (GUILayout.Button("清筛选颜色", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        var visibleRules = GetVisibleRules(settings);
                        if (ConfirmBatchAction("清筛选颜色", "将清除当前筛选结果中 " + visibleRules.Count + " 个对象的颜色样式。"))
                        {
                            settings.ClearColors(visibleRules);
                            Repaint();
                        }
                    }

                    if (GUILayout.Button("清筛选全部", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                    {
                        var visibleRules = GetVisibleRules(settings);
                        if (ConfirmBatchAction("清筛选全部", "将移除当前筛选结果中 " + visibleRules.Count + " 条标记记录。"))
                        {
                            settings.RemoveObjectRules(visibleRules);
                            Repaint();
                        }
                    }

                    if (GUILayout.Button("清理失效", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        if (ConfirmBatchAction("清理失效", "将移除所有找不到对象的失效标记。"))
                        {
                            settings.RemoveInvalidObjectRules();
                            Repaint();
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    DrawFilterButton("全部", MarkFilter.All);
                    DrawFilterButton("书签", MarkFilter.Bookmarked);
                    DrawFilterButton("有标签", MarkFilter.Labeled);
                    DrawFilterButton("有颜色", MarkFilter.Colored);
                    DrawFilterButton("失效", MarkFilter.Missing);

                    GUILayout.FlexibleSpace();
                    GUILayout.Label("排序", GUILayout.Width(32f));
                    EditorGUI.BeginChangeCheck();
                    sortMode = (MarkSortMode)EditorGUILayout.Popup((int)sortMode, SortModeLabels, GUILayout.Width(88f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        ResetListPage();
                    }
                }
            }

            private static bool ConfirmBatchAction(string title, string message)
            {
                return EditorUtility.DisplayDialog(title, message, "确认", "取消");
            }

            private void DrawFilterButton(string label, MarkFilter filter)
            {
                var oldColor = GUI.color;
                if (markFilter == filter)
                {
                    GUI.color = new Color(0.72f, 0.86f, 1f, 1f);
                }

                if (GUILayout.Button(label, EditorStyles.toolbarButton))
                {
                    markFilter = filter;
                    ResetListPage();
                }

                GUI.color = oldColor;
            }

            private void FocusSelectedRule(HierarchyEnhancerSettings settings)
            {
                var selected = Selection.activeGameObject;
                var objectId = HierarchyEnhancerSettings.GetObjectIdentifier(selected);
                if (string.IsNullOrEmpty(objectId))
                {
                    return;
                }

                var rules = GetVisibleRules(settings);
                var index = FindRuleIndex(rules, objectId);
                if (index < 0)
                {
                    searchText = string.Empty;
                    markFilter = MarkFilter.All;
                    ResetVisibleRuleCache();
                    rules = GetVisibleRules(settings);
                    index = FindRuleIndex(rules, objectId);
                }

                if (index < 0)
                {
                    EditorUtility.DisplayDialog("定位选中", "当前选中对象还没有标记记录。", "知道了");
                    return;
                }

                var rule = rules[index];
                groupFoldouts[ShouldGroupByLabel() ? GetGroupName(rule) : "排序结果"] = true;
                currentPage = index / pageSize;
                scrollPosition = new Vector2(0f, GetFocusedRuleScrollY(rules, index));
                focusedObjectId = objectId;
                focusedObjectUntil = EditorApplication.timeSinceStartup + 2.5d;
                Repaint();
            }

            private float GetFocusedRuleScrollY(List<ObjectLabelRule> rules, int ruleIndex)
            {
                var pageStart = currentPage * pageSize;
                var pageIndex = ruleIndex - pageStart;
                var groupHeadersBeforeTarget = ShouldGroupByLabel()
                    ? CountGroupHeadersBeforeTarget(rules, pageStart, ruleIndex)
                    : 1;

                return Mathf.Max(0f, (pageIndex - 1) * MarkManagerRowHeight + groupHeadersBeforeTarget * EditorGUIUtility.singleLineHeight);
            }

            private static int CountGroupHeadersBeforeTarget(List<ObjectLabelRule> rules, int pageStart, int targetIndex)
            {
                var groupNames = new HashSet<string>();
                for (var i = pageStart; i <= targetIndex && i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule != null)
                    {
                        groupNames.Add(GetGroupName(rule));
                    }
                }

                return groupNames.Count;
            }

            private static int FindRuleIndex(List<ObjectLabelRule> rules, string objectId)
            {
                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule != null && string.Equals(rule.objectId, objectId, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }

                return -1;
            }

            private void ResetVisibleRuleCache()
            {
                cachedVisibleRuleSettings = null;
                cachedVisibleRuleCount = -1;
            }

            private List<ObjectLabelRule> GetVisibleRules(HierarchyEnhancerSettings settings)
            {
                if (IsVisibleRuleCacheValid(settings))
                {
                    return visibleRuleCache;
                }

                visibleRuleCache.Clear();
                var rules = settings.objectLabelRules;
                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule != null && MatchesFilter(rule) && MatchesSearch(rule))
                    {
                        visibleRuleCache.Add(rule);
                    }
                }

                SortVisibleRules(visibleRuleCache);
                cachedVisibleRuleSettings = settings;
                cachedVisibleRuleSearch = searchText;
                cachedVisibleRuleFilter = markFilter;
                cachedVisibleRuleSort = sortMode;
                cachedVisibleRuleCount = rules.Count;
                cachedVisibleRuleVersion = markListVersion;
                return visibleRuleCache;
            }

            private bool IsVisibleRuleCacheValid(HierarchyEnhancerSettings settings)
            {
                return cachedVisibleRuleSettings == settings &&
                    cachedVisibleRuleCount == settings.objectLabelRules.Count &&
                    cachedVisibleRuleFilter == markFilter &&
                    cachedVisibleRuleSort == sortMode &&
                    cachedVisibleRuleVersion == markListVersion &&
                    string.Equals(cachedVisibleRuleSearch, searchText, StringComparison.Ordinal);
            }

            private void SortVisibleRules(List<ObjectLabelRule> rules)
            {
                if (sortMode == MarkSortMode.Default)
                {
                    return;
                }

                rules.Sort(CompareRules);
            }

            private int CompareRules(ObjectLabelRule a, ObjectLabelRule b)
            {
                switch (sortMode)
                {
                    case MarkSortMode.Name:
                        return CompareText(a.objectName, b.objectName);
                    case MarkSortMode.Label:
                        return CompareText(a.label, b.label);
                    case MarkSortMode.Color:
                        return CompareText(GetColorSortKey(a), GetColorSortKey(b));
                    case MarkSortMode.BookmarkFirst:
                        return CompareBoolDescending(a.bookmarked, b.bookmarked, a, b);
                    case MarkSortMode.MissingFirst:
                        return CompareBoolDescending(IsMissing(a), IsMissing(b), a, b);
                    default:
                        return 0;
                }
            }

            private static int CompareBoolDescending(bool a, bool b, ObjectLabelRule ruleA, ObjectLabelRule ruleB)
            {
                var result = b.CompareTo(a);
                return result != 0 ? result : CompareText(ruleA.objectName, ruleB.objectName);
            }

            private static int CompareText(string a, string b)
            {
                return string.Compare(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            private static string GetColorSortKey(ObjectLabelRule rule)
            {
                if (rule == null || (!rule.useTextColor && !rule.useRowColor))
                {
                    return string.Empty;
                }

                var color = rule.useRowColor ? rule.rowColor : rule.textColor;
                return color.r.ToString("F3") + "," + color.g.ToString("F3") + "," + color.b.ToString("F3") + "," + color.a.ToString("F3");
            }

            private static bool IsMissing(ObjectLabelRule rule)
            {
                return HierarchyEnhancerSettings.ResolveObject(rule) == null;
            }

            private void DrawRuleList(HierarchyEnhancerSettings settings)
            {
                var rules = GetVisibleRules(settings);
                if (rules.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无匹配标记。", MessageType.Info);
                    return;
                }

                DrawPaginationBar(rules.Count);
                var pageRules = GetPageRules(rules);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                var groups = ShouldGroupByLabel() ? BuildGroups(pageRules) : BuildSingleGroup(pageRules);

                foreach (var group in groups)
                {
                    if (!groupFoldouts.ContainsKey(group.name))
                    {
                        groupFoldouts[group.name] = true;
                    }

                    groupFoldouts[group.name] = EditorGUILayout.Foldout(groupFoldouts[group.name], group.name + " (" + group.rules.Count + ")", true);
                    if (!groupFoldouts[group.name])
                    {
                        continue;
                    }

                    for (var i = 0; i < group.rules.Count; i++)
                    {
                        DrawRuleRow(settings, group.rules[i]);
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawPaginationBar(int totalCount)
            {
                var pageCount = Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)pageSize));
                currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("显示", GUILayout.Width(28f));

                    EditorGUI.BeginChangeCheck();
                    pageSize = EditorGUILayout.IntPopup(pageSize, PageSizeLabels, PageSizeValues, GUILayout.Width(56f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        ResetListPage();
                        pageCount = Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)pageSize));
                    }

                    GUILayout.Label("条/页", GUILayout.Width(42f));
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(currentPage == 0))
                    {
                        if (GUILayout.Button("上一页", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                        {
                            currentPage--;
                            scrollPosition = Vector2.zero;
                        }
                    }

                    GUILayout.Label((currentPage + 1) + " / " + pageCount + "  共 " + totalCount + " 条", EditorStyles.miniLabel, GUILayout.Width(110f));

                    using (new EditorGUI.DisabledScope(currentPage >= pageCount - 1))
                    {
                        if (GUILayout.Button("下一页", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                        {
                            currentPage++;
                            scrollPosition = Vector2.zero;
                        }
                    }
                }
            }

            private void ResetListPage()
            {
                currentPage = 0;
                scrollPosition = Vector2.zero;
            }

            private List<ObjectLabelRule> GetPageRules(List<ObjectLabelRule> rules)
            {
                var pageRules = new List<ObjectLabelRule>();
                var startIndex = currentPage * pageSize;
                var endIndex = Mathf.Min(startIndex + pageSize, rules.Count);

                for (var i = startIndex; i < endIndex; i++)
                {
                    pageRules.Add(rules[i]);
                }

                return pageRules;
            }

            private bool MatchesFilter(ObjectLabelRule rule)
            {
                switch (markFilter)
                {
                    case MarkFilter.Bookmarked:
                        return rule.bookmarked;
                    case MarkFilter.Labeled:
                        return !string.IsNullOrEmpty(rule.label);
                    case MarkFilter.Colored:
                        return rule.useTextColor || rule.useRowColor;
                    case MarkFilter.Missing:
                        return HierarchyEnhancerSettings.ResolveObject(rule) == null;
                    default:
                        return true;
                }
            }

            private static List<RuleGroup> BuildGroups(List<ObjectLabelRule> rules)
            {
                var groups = new List<RuleGroup>();
                for (var i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    var groupName = GetGroupName(rule);
                    var group = groups.Find(item => item.name == groupName);
                    if (group == null)
                    {
                        group = new RuleGroup(groupName);
                        groups.Add(group);
                    }

                    group.rules.Add(rule);
                }

                groups.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                return groups;
            }

            private static string GetGroupName(ObjectLabelRule rule)
            {
                if (!string.IsNullOrEmpty(rule.label))
                {
                    return rule.label;
                }

                if (rule.bookmarked)
                {
                    return "书签";
                }

                return "无标签";
            }

            private bool ShouldGroupByLabel()
            {
                return sortMode == MarkSortMode.Default || sortMode == MarkSortMode.Label;
            }

            private static List<RuleGroup> BuildSingleGroup(List<ObjectLabelRule> rules)
            {
                var group = new RuleGroup("排序结果");
                group.rules.AddRange(rules);
                return new List<RuleGroup> { group };
            }

            private bool MatchesSearch(ObjectLabelRule rule)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    return true;
                }

                var search = searchText.Trim();
                return Contains(rule.objectName, search) ||
                    Contains(rule.label, search) ||
                    (rule.bookmarked && Contains("书签", search));
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
                var isFocused = IsFocusedRule(rule);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (DrawColorPreview(rule) && !isMissing)
                    {
                        ObjectColorWindow.Open(new[] { gameObject });
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        var objectName = isMissing ? rule.objectName + " (失效)" : gameObject.name;
                        if (isFocused)
                        {
                            objectName += "  已定位";
                        }

                        EditorGUILayout.LabelField(objectName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(GetRuleSubtitle(rule), EditorStyles.miniLabel);
                    }

                    using (new EditorGUI.DisabledScope(isMissing))
                    {
                        if (GUILayout.Button("选中", GUILayout.Width(48f)))
                        {
                            Selection.activeGameObject = gameObject;
                            EditorGUIUtility.PingObject(gameObject);
                        }
                    }

                    if (GUILayout.Button("操作", GUILayout.Width(48f)))
                    {
                        ShowRuleActionMenu(settings, rule, isMissing);
                    }
                }

                if (isFocused)
                {
                    DrawFocusedRowHighlight(GUILayoutUtility.GetLastRect());
                    Repaint();
                }
            }

            private bool IsFocusedRule(ObjectLabelRule rule)
            {
                return rule != null &&
                    EditorApplication.timeSinceStartup <= focusedObjectUntil &&
                    string.Equals(rule.objectId, focusedObjectId, StringComparison.Ordinal);
            }

            private static void DrawFocusedRowHighlight(Rect rect)
            {
                var line = EditorGUIUtility.isProSkin
                    ? new Color(0.5f, 0.78f, 1f, 0.85f)
                    : new Color(0.05f, 0.25f, 0.85f, 0.75f);

                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), line);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), line);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), line);
            }

            private void ShowRuleActionMenu(HierarchyEnhancerSettings settings, ObjectLabelRule rule, bool isMissing)
            {
                var menu = new GenericMenu();

                if (isMissing)
                {
                    menu.AddDisabledItem(new GUIContent("选中同标签"));
                    menu.AddDisabledItem(new GUIContent("选中同颜色"));
                    menu.AddDisabledItem(new GUIContent("选中同样式"));
                }
                else
                {
                    menu.AddItem(new GUIContent("选中同标签"), false, () => SelectMatchingRules(settings, rule, MatchMode.Label));
                    menu.AddItem(new GUIContent("选中同颜色"), false, () => SelectMatchingRules(settings, rule, MatchMode.Color));
                    menu.AddItem(new GUIContent("选中同样式"), false, () => SelectMatchingRules(settings, rule, MatchMode.Style));
                }

                menu.AddSeparator(string.Empty);

                if (rule.bookmarked)
                {
                    menu.AddItem(new GUIContent("取消书签"), false, () =>
                    {
                        settings.SetBookmark(rule, false);
                        Repaint();
                    });
                }
                else
                {
                    menu.AddItem(new GUIContent("加入书签"), false, () =>
                    {
                        settings.SetBookmark(rule, true);
                        Repaint();
                    });
                }

                menu.AddItem(new GUIContent("清标签"), false, () =>
                {
                    settings.ClearLabel(rule);
                    Repaint();
                });
                menu.AddItem(new GUIContent("清颜色"), false, () =>
                {
                    settings.ClearColor(rule);
                    Repaint();
                });

                menu.ShowAsContext();
            }

            private static string GetRuleSubtitle(ObjectLabelRule rule)
            {
                var label = string.IsNullOrEmpty(rule.label) ? "无标签" : rule.label;
                return rule.bookmarked ? label + " / 书签" : label;
            }

            private enum MarkFilter
            {
                All,
                Bookmarked,
                Labeled,
                Colored,
                Missing
            }

            private enum MarkSortMode
            {
                Default,
                Name,
                Label,
                Color,
                BookmarkFirst,
                MissingFirst
            }

            private sealed class RuleGroup
            {
                public readonly string name;
                public readonly List<ObjectLabelRule> rules = new List<ObjectLabelRule>();

                public RuleGroup(string name)
                {
                    this.name = name;
                }
            }

            private enum MatchMode
            {
                Label,
                Color,
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
                if (mode == MatchMode.Label)
                {
                    return StringEquals(sourceRule.label, rule.label);
                }

                if (mode == MatchMode.Color)
                {
                    return ColorStyleEquals(sourceRule, rule);
                }

                return StringEquals(sourceRule.label, rule.label) &&
                    ColorStyleEquals(sourceRule, rule);
            }

            private static bool ColorStyleEquals(ObjectLabelRule sourceRule, ObjectLabelRule rule)
            {
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

            private static bool DrawColorPreview(ObjectLabelRule rule)
            {
                var rect = GUILayoutUtility.GetRect(34f, 32f, GUILayout.Width(34f), GUILayout.Height(32f));
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                var oldColor = GUI.color;

                GUI.color = rule.useRowColor ? rule.rowColor : new Color(0f, 0f, 0f, 0.12f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);

                if (rule.useTextColor)
                {
                    GUI.color = rule.textColor;
                    GUI.Label(rect, "T", EditorStyles.boldLabel);
                }

                GUI.color = oldColor;
                DrawPreviewBorder(rect);
                return GUI.Button(rect, GUIContent.none, GUIStyle.none);
            }

            private static void DrawPreviewBorder(Rect rect)
            {
                var color = EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.18f)
                    : new Color(0f, 0f, 0f, 0.18f);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
                EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
            }
        }
    }
}

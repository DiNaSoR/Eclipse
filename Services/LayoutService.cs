using BepInEx;
using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace Eclipse.Services;
internal static class LayoutService
{
    const string WindowTitle = "Eclipse Layout Mode";
    const string ProfilePrefix = "profile_";
    const string ProfileExtension = ".json";
    const float PanelHeaderHeight = 24f;

    static readonly string LayoutPath = Path.Combine(Paths.ConfigPath, $"{MyPluginInfo.PLUGIN_GUID}.layout.json");
    static readonly string ProfilesDir = Path.Combine(Paths.ConfigPath, $"{MyPluginInfo.PLUGIN_GUID}.layouts");
    static readonly Dictionary<string, LayoutElement> Elements = new();
    static Dictionary<string, LayoutEntry> SavedLayoutsKeyboard = new();
    static Dictionary<string, LayoutEntry> SavedLayoutsGamepad = new();
    static Dictionary<string, ElementState> SavedStates = new();
    static LayoutOptions Options = new();

    static LayoutEntry ClipboardEntry;
    static string ClipboardKey = string.Empty;

    static LayoutModeBehaviour _behaviour;

    internal static LayoutOptions CurrentOptions => Options;
    static Dictionary<string, LayoutEntry> ActiveLayouts => GetLayouts(Options.LayoutMode);
    internal static bool IsLayoutModeActive => _behaviour != null && _behaviour.IsActive;

    public static void Initialize()
    {
        if (_behaviour != null)
        {
            return;
        }

        LoadLayouts();

        if (!ClassInjector.IsTypeRegisteredInIl2Cpp(typeof(LayoutModeBehaviour)))
        {
            ClassInjector.RegisterTypeInIl2Cpp<LayoutModeBehaviour>();
        }

        var go = new GameObject("EclipseLayoutMode");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _behaviour = go.AddComponent<LayoutModeBehaviour>();
    }

    public static void RegisterElement(string key, RectTransform rect)
    {
        if (string.IsNullOrWhiteSpace(key) || rect == null)
        {
            return;
        }

        Initialize();
        RectTransform hitRect = FindHitRect(rect);
        var element = new LayoutElement(key, rect, hitRect);
        Elements[key] = element;

        LayoutModeType runtimeMode = GetRuntimeMode(CanvasService.InputHUD.IsGamepad);
        ApplySavedLayout(key, rect, runtimeMode);
        ApplyElementState(element);
    }

    public static void Reset()
    {
        Elements.Clear();
        _behaviour?.ResetState();
    }

    public static void SaveLayouts()
    {
        CacheLayouts(Options.LayoutMode);

        var config = new LayoutConfig
        {
            KeyboardLayouts = SavedLayoutsKeyboard,
            GamepadLayouts = SavedLayoutsGamepad,
            ElementStates = SavedStates,
            Options = Options
        };

        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(LayoutPath, json);
        DumpLayouts();
    }

    public static void DumpLayouts()
    {
        var layouts = ActiveLayouts;
        if (layouts.Count == 0)
        {
            Core.Log.LogInfo("[Layout] No saved layout entries.");
            return;
        }

        foreach (var entry in layouts)
        {
            LayoutEntry data = entry.Value;
            Core.Log.LogInfo($"[Layout] {entry.Key}: anchorMin=({data.AnchorMinX:F3},{data.AnchorMinY:F3}) " +
                $"anchorMax=({data.AnchorMaxX:F3},{data.AnchorMaxY:F3}) pivot=({data.PivotX:F3},{data.PivotY:F3}) " +
                $"pos=({data.AnchoredPosX:F1},{data.AnchoredPosY:F1}) scale=({data.ScaleX:F2},{data.ScaleY:F2},{data.ScaleZ:F2})");
        }

        Core.Log.LogInfo($"[Layout] Mode={Options.LayoutMode}, VerticalBars={Options.VerticalBars}, CompactQuests={Options.CompactQuests}");
    }

    public static void ClearLayoutFile()
    {
        if (File.Exists(LayoutPath))
        {
            File.Delete(LayoutPath);
            SavedLayoutsKeyboard.Clear();
            SavedLayoutsGamepad.Clear();
            SavedStates.Clear();
            Core.Log.LogInfo($"[Layout] Deleted layout file: {LayoutPath}");
        }
    }

    public static void ResetDefaults()
    {
        if (File.Exists(LayoutPath))
        {
            File.Delete(LayoutPath);
        }

        SavedLayoutsKeyboard.Clear();
        SavedLayoutsGamepad.Clear();
        SavedStates.Clear();
        Options = new LayoutOptions();
        Core.Log.LogInfo("[Layout] Reset to defaults.");
        ApplyOptions(false);
    }

    static void LoadLayouts()
    {
        if (!File.Exists(LayoutPath))
        {
            return;
        }

        string json;
        try
        {
            json = File.ReadAllText(LayoutPath);
        }
        catch (Exception ex)
        {
            SavedLayoutsKeyboard = new();
            SavedLayoutsGamepad = new();
            SavedStates = new();
            Core.Log.LogWarning($"[Layout] Failed to read {LayoutPath}: {ex.Message}");
            return;
        }

        try
        {
            LayoutConfig config = JsonSerializer.Deserialize<LayoutConfig>(json);
            if (config != null)
            {
                SavedLayoutsKeyboard = config.KeyboardLayouts ?? new();
                SavedLayoutsGamepad = config.GamepadLayouts ?? new();
                SavedStates = config.ElementStates ?? new();

                if (config.Elements != null && config.Elements.Count > 0 && SavedLayoutsKeyboard.Count == 0)
                {
                    SavedLayoutsKeyboard = config.Elements;
                }

                Options = config.Options ?? new LayoutOptions();
                if (Options.AutoProfileByResolution)
                {
                    TryLoadAutoProfile(true);
                }

                return;
            }
        }
        catch
        {
            // Fall back to legacy layout file format.
        }

        try
        {
            SavedLayoutsKeyboard = JsonSerializer.Deserialize<Dictionary<string, LayoutEntry>>(json) ?? new();
        }
        catch (Exception ex)
        {
            SavedLayoutsKeyboard = new();
            SavedLayoutsGamepad = new();
            SavedStates = new();
            Core.Log.LogWarning($"[Layout] Failed to parse {LayoutPath}: {ex.Message}");
        }
    }

    static bool SaveProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        CacheLayouts(Options.LayoutMode);

        Directory.CreateDirectory(ProfilesDir);
        string path = GetProfilePath(name);
        var config = new LayoutConfig
        {
            KeyboardLayouts = SavedLayoutsKeyboard,
            GamepadLayouts = SavedLayoutsGamepad,
            ElementStates = SavedStates,
            Options = Options
        };

        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        Options.ActiveProfile = name;
        Core.Log.LogInfo($"[Layout] Saved profile: {name}");
        return true;
    }

    static bool LoadProfile(string name, bool preserveAutoProfile)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string path = GetProfilePath(name);
        if (!File.Exists(path))
        {
            return false;
        }

        bool previousAuto = Options.AutoProfileByResolution;
        LayoutModeType previousMode = Options.LayoutMode;

        string json = File.ReadAllText(path);
        LayoutConfig config = JsonSerializer.Deserialize<LayoutConfig>(json);
        if (config == null)
        {
            return false;
        }

        SavedLayoutsKeyboard = config.KeyboardLayouts ?? new();
        SavedLayoutsGamepad = config.GamepadLayouts ?? new();
        SavedStates = config.ElementStates ?? new();
        Options = config.Options ?? new LayoutOptions();

        if (preserveAutoProfile)
        {
            Options.AutoProfileByResolution = previousAuto;
            Options.LayoutMode = previousMode;
        }

        Options.ActiveProfile = name;
        ApplyOptions(false);
        Core.Log.LogInfo($"[Layout] Loaded profile: {name}");
        return true;
    }

    static bool DeleteProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string path = GetProfilePath(name);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        Core.Log.LogInfo($"[Layout] Deleted profile: {name}");
        return true;
    }

    static bool TryLoadAutoProfile(bool preserveAutoProfile)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return false;
        }

        string resolutionProfile = $"{Screen.width}x{Screen.height}";
        if (LoadProfile(resolutionProfile, preserveAutoProfile))
        {
            return true;
        }

        float aspect = Screen.width / (float)Screen.height;
        if (aspect >= 2.3f && LoadProfile("UltraWide", preserveAutoProfile))
        {
            return true;
        }

        return false;
    }

    internal static void ApplyLayoutsForInput(bool isGamepad)
    {
        LayoutModeType mode = GetRuntimeMode(isGamepad);
        ApplyLayoutsForMode(mode);
    }

    internal static void ApplyLayoutsForMode(LayoutModeType mode)
    {
        Dictionary<string, LayoutEntry> layouts = GetLayouts(mode);
        foreach (LayoutElement element in Elements.Values)
        {
            if (element.Rect == null)
            {
                continue;
            }

            if (layouts.TryGetValue(element.Key, out LayoutEntry data))
            {
                ApplyLayoutEntry(element.Rect, data);
            }

            ApplyElementState(element);
        }
    }

    static LayoutModeType GetRuntimeMode(bool isGamepad)
    {
        if (IsLayoutModeActive)
        {
            return Options.LayoutMode;
        }

        return isGamepad ? LayoutModeType.Gamepad : LayoutModeType.KeyboardMouse;
    }

    static Dictionary<string, LayoutEntry> GetLayouts(LayoutModeType mode)
    {
        return mode == LayoutModeType.Gamepad ? SavedLayoutsGamepad : SavedLayoutsKeyboard;
    }

    static ElementState GetElementState(string key)
    {
        if (!SavedStates.TryGetValue(key, out ElementState state))
        {
            state = new ElementState();
            SavedStates[key] = state;
        }

        return state;
    }

    static void ApplyElementState(LayoutElement element)
    {
        ElementState state = GetElementState(element.Key);
        RectTransform rect = element.ResolveHitRect() ?? element.Rect;
        if (rect == null)
        {
            return;
        }

        CanvasGroup group = EnsureCanvasGroup(rect.gameObject);
        if (state.Hidden)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        else
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    static CanvasGroup EnsureCanvasGroup(GameObject obj)
    {
        CanvasGroup group = obj.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = obj.AddComponent<CanvasGroup>();
        }

        return group;
    }

    static void ApplySavedLayout(string key, RectTransform rect, LayoutModeType mode)
    {
        if (!GetLayouts(mode).TryGetValue(key, out LayoutEntry data))
        {
            return;
        }

        ApplyLayoutEntry(rect, data);
    }

    static void ApplyLayoutEntry(RectTransform rect, LayoutEntry data)
    {
        rect.anchorMin = new Vector2(data.AnchorMinX, data.AnchorMinY);
        rect.anchorMax = new Vector2(data.AnchorMaxX, data.AnchorMaxY);
        rect.pivot = new Vector2(data.PivotX, data.PivotY);
        rect.anchoredPosition = new Vector2(data.AnchoredPosX, data.AnchoredPosY);
        rect.localScale = new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ);
    }

    static void CacheLayouts(LayoutModeType mode)
    {
        Dictionary<string, LayoutEntry> target = GetLayouts(mode);
        foreach (KeyValuePair<string, LayoutElement> entry in Elements)
        {
            RectTransform rect = entry.Value.Rect;
            if (rect == null)
            {
                continue;
            }

            target[entry.Key] = LayoutEntry.FromRect(rect);
        }
    }

    static void ApplyOptions(bool cacheCurrent)
    {
        if (cacheCurrent)
        {
            CacheLayouts(Options.LayoutMode);
        }

        if (Core.CanvasService != null)
        {
            Core.CanvasService.RebuildLayout();
        }

        ApplyLayoutsForMode(Options.LayoutMode);
    }

    static string GetProfilePath(string name)
    {
        string safeName = SanitizeProfileName(name);
        return Path.Combine(ProfilesDir, $"{ProfilePrefix}{safeName}{ProfileExtension}");
    }

    static string SanitizeProfileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = name.Trim();
        for (int i = 0; i < invalid.Length; i++)
        {
            safe = safe.Replace(invalid[i], '_');
        }

        return safe.Replace(' ', '_');
    }

    static List<string> GetProfileNames()
    {
        var result = new List<string>();
        if (!Directory.Exists(ProfilesDir))
        {
            return result;
        }

        string[] files = Directory.GetFiles(ProfilesDir, $"{ProfilePrefix}*{ProfileExtension}", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(files[i]);
            if (fileName.StartsWith(ProfilePrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(fileName[ProfilePrefix.Length..]);
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    static Camera ResolveCamera(RectTransform rect)
    {
        if (rect == null)
        {
            return Camera.main;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera ?? Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
    }

    static RectTransform FindHitRect(RectTransform rect)
    {
        if (rect == null)
        {
            return null;
        }

        if (HasSize(rect))
        {
            return rect;
        }

        RectTransform best = rect;
        float bestArea = 0f;

        RectTransform[] children = rect.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform child = children[i];
            if (child == rect)
            {
                continue;
            }

            if (!HasSize(child))
            {
                continue;
            }

            float area = Mathf.Abs(child.rect.width * child.rect.height);
            if (area > bestArea)
            {
                bestArea = area;
                best = child;
            }
        }

        return best;
    }

    static bool HasSize(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        return rect.rect.width > 1f && rect.rect.height > 1f;
    }

    sealed class LayoutModeBehaviour : MonoBehaviour
    {
        const float GuideThickness = 2f;
        static readonly Color GuideColor = new(0.1f, 0.8f, 0.9f, 0.9f);
        static readonly Color GridColor = new(1f, 1f, 1f, 0.08f);
        static readonly Color HighlightColor = new(1f, 0.7f, 0.1f, 0.9f);
        static readonly Color SelectedColor = new(0.25f, 1f, 1f, 0.95f);
        static readonly Color OutlineColor = new(1f, 1f, 1f, 0.65f);
        static readonly Color SafeAreaColor = new(0.1f, 1f, 0.4f, 0.4f);

        Rect _windowRect = new(20f, 20f, 560f, 760f);
        string _draggingKey = string.Empty;
        RectTransform _draggingRect;
        Camera _draggingCamera;
        Vector2 _dragOffset;
        Vector3 _draggingWorldOffset;
        bool _draggingPanel;
        Vector2 _panelDragOffset;
        bool _active;
        string _selectedKey = string.Empty;
        string _selectedProfile = string.Empty;
        int _elementPage;
        int _profilePage;
        readonly List<GuideLine> _guides = new();

        public bool IsActive => _active;

        public void ResetState()
        {
            _draggingKey = string.Empty;
            _draggingRect = null;
            _draggingCamera = null;
            _dragOffset = default;
            _draggingWorldOffset = default;
            _draggingPanel = false;
            _panelDragOffset = default;
            _guides.Clear();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _active = !_active;
                Core.Log.LogInfo($"[Layout] {WindowTitle}: {(_active ? "ON" : "OFF")}");
                ResetState();
                SyncInputFields();
                if (_active)
                {
                    ApplyLayoutsForMode(Options.LayoutMode);
                }
                else
                {
                    ApplyLayoutsForInput(CanvasService.InputHUD.IsGamepad);
                }
            }

            if (!_active)
            {
                return;
            }

            HandleNudge();

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f && !_draggingPanel && !IsPointerOverWindow())
            {
                ResizeHovered(scroll);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (TryBeginPanelDrag())
                {
                    return;
                }

                if (!IsPointerOverWindow())
                {
                    TryBeginDrag();
                }
            }
            else if (Input.GetMouseButton(0))
            {
                if (_draggingPanel)
                {
                    DragPanel();
                }
                else
                {
                    Drag();
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                ResetState();
            }
        }

        void OnGUI()
        {
            if (!_active)
            {
                return;
            }

            DrawPanel();
            DrawGrid();
            DrawSafeArea();
            DrawGuides();
            DrawOutlines();
        }

        void SyncInputFields()
        {
            if (!string.IsNullOrEmpty(Options.ActiveProfile))
            {
                _selectedProfile = Options.ActiveProfile;
                return;
            }

            if (string.IsNullOrEmpty(_selectedProfile))
            {
                List<string> profiles = GetProfileNames();
                if (profiles.Count > 0)
                {
                    _selectedProfile = profiles[0];
                }
            }
        }

        void DrawPanel()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(_windowRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Label(new Rect(_windowRect.x + 8f, _windowRect.y + 4f, _windowRect.width - 16f, 18f), WindowTitle);

            const float padding = 10f;
            const float lineHeight = 18f;
            const float buttonHeight = 22f;
            const float buttonGap = 6f;

            float x = _windowRect.x + padding;
            float y = _windowRect.y + PanelHeaderHeight;
            float width = _windowRect.width - (padding * 2f);

            GUI.Label(new Rect(x, y, width, lineHeight), "LMB drag • Wheel resize • F8 toggle");
            y += lineHeight;

            if (Elements.Count == 0)
            {
                GUI.Label(new Rect(x, y, width, lineHeight), "No elements registered.");
                y += lineHeight;
            }
            else
            {
                GUI.Label(new Rect(x, y, width, lineHeight), string.IsNullOrEmpty(_selectedKey) ? "Selected: none" : $"Selected: {_selectedKey}");
                y += lineHeight;
            }

            float columnGap = 12f;
            float columnWidth = (width - columnGap) / 2f;
            float leftX = x;
            float rightX = x + columnWidth + columnGap;
            float leftY = y + 4f;
            float rightY = y + 4f;

            DrawLayoutModeToggle(ref leftX, ref leftY, columnWidth, lineHeight);
            DrawSnapOptions(ref leftX, ref leftY, columnWidth, lineHeight);
            DrawOptionsToggles(ref leftX, ref leftY, columnWidth, lineHeight);

            DrawProfilesSection(ref rightX, ref rightY, columnWidth, lineHeight, buttonHeight, buttonGap);
            DrawElementList(ref rightX, ref rightY, columnWidth, lineHeight, buttonHeight, buttonGap);

            float footerX = x;
            float footerY = Math.Max(leftY, rightY) + 6f;
            DrawFooterButtons(ref footerX, ref footerY, width, buttonHeight, buttonGap);
        }

        void DrawLayoutModeToggle(ref float x, ref float y, float width, float lineHeight)
        {
            GUI.Label(new Rect(x, y, width, lineHeight), "Edit layout for:");
            y += lineHeight;

            float half = (width - 6f) / 2f;
            bool editKeyboard = GUI.Toggle(new Rect(x, y, half, lineHeight), Options.LayoutMode == LayoutModeType.KeyboardMouse, "Keyboard/Mouse");
            bool editGamepad = GUI.Toggle(new Rect(x + half + 6f, y, half, lineHeight), Options.LayoutMode == LayoutModeType.Gamepad, "Gamepad");
            y += lineHeight + 2f;

            if (editKeyboard && Options.LayoutMode != LayoutModeType.KeyboardMouse)
            {
                CacheLayouts(Options.LayoutMode);
                Options.LayoutMode = LayoutModeType.KeyboardMouse;
                ApplyLayoutsForMode(Options.LayoutMode);
            }
            else if (editGamepad && Options.LayoutMode != LayoutModeType.Gamepad)
            {
                CacheLayouts(Options.LayoutMode);
                Options.LayoutMode = LayoutModeType.Gamepad;
                ApplyLayoutsForMode(Options.LayoutMode);
            }
        }

        void DrawSnapOptions(ref float x, ref float y, float width, float lineHeight)
        {
            GUI.Label(new Rect(x, y, width, lineHeight), "Snap + guides:");
            y += lineHeight;

            bool snapGrid = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.SnapToGrid, "Snap to grid");
            y += lineHeight;
            bool showGrid = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.ShowGrid, "Show grid");
            y += lineHeight;
            float gridSize = Options.GridSize;
            DrawValueStepper(ref x, ref y, width, lineHeight, "Grid size (px)", ref gridSize, 1f, 2f, 200f, "F0");

            bool snapEdges = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.SnapToEdges, "Snap to edges");
            y += lineHeight;
            bool snapElements = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.SnapToElements, "Snap to elements");
            y += lineHeight;
            float snapThreshold = Options.SnapThreshold;
            DrawValueStepper(ref x, ref y, width, lineHeight, "Snap threshold (px)", ref snapThreshold, 1f, 1f, 30f, "F0");
            bool showGuides = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.ShowGuides, "Show guides");
            y += lineHeight;
            bool showSafeArea = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.ShowSafeArea, "Show safe area");
            y += lineHeight;
            float safeAreaPadding = Options.SafeAreaPadding;
            DrawValueStepper(ref x, ref y, width, lineHeight, "Safe area padding (px)", ref safeAreaPadding, 5f, 0f, 500f, "F0");

            if (snapGrid != Options.SnapToGrid)
            {
                Options.SnapToGrid = snapGrid;
            }
            if (showGrid != Options.ShowGrid)
            {
                Options.ShowGrid = showGrid;
            }
            if (snapEdges != Options.SnapToEdges)
            {
                Options.SnapToEdges = snapEdges;
            }
            if (snapElements != Options.SnapToElements)
            {
                Options.SnapToElements = snapElements;
            }
            if (showGuides != Options.ShowGuides)
            {
                Options.ShowGuides = showGuides;
            }
            if (showSafeArea != Options.ShowSafeArea)
            {
                Options.ShowSafeArea = showSafeArea;
            }

            if (Mathf.Abs(gridSize - Options.GridSize) > 0.001f)
            {
                Options.GridSize = gridSize;
            }
            if (Mathf.Abs(snapThreshold - Options.SnapThreshold) > 0.001f)
            {
                Options.SnapThreshold = snapThreshold;
            }
            if (Mathf.Abs(safeAreaPadding - Options.SafeAreaPadding) > 0.001f)
            {
                Options.SafeAreaPadding = safeAreaPadding;
            }
        }

        void DrawOptionsToggles(ref float x, ref float y, float width, float lineHeight)
        {
            bool verticalBars = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.VerticalBars, "Bars: vertical text");
            y += lineHeight;
            bool compactQuests = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.CompactQuests, "Quests: compact");
            y += lineHeight;

            float nudgeStep = Options.NudgeStep;
            DrawValueStepper(ref x, ref y, width, lineHeight, "Nudge step (px)", ref nudgeStep, 0.1f, 0.1f, 50f, "F1");

            if (verticalBars != Options.VerticalBars)
            {
                Options.VerticalBars = verticalBars;
                ApplyOptions(true);
            }

            if (compactQuests != Options.CompactQuests)
            {
                Options.CompactQuests = compactQuests;
                ApplyOptions(true);
            }

            if (Mathf.Abs(nudgeStep - Options.NudgeStep) > 0.001f)
            {
                Options.NudgeStep = nudgeStep;
            }
        }

        void DrawProfilesSection(ref float x, ref float y, float width, float lineHeight, float buttonHeight, float buttonGap)
        {
            GUI.Label(new Rect(x, y, width, lineHeight), "Profiles:");
            y += lineHeight;
            string currentRes = $"{Screen.width}x{Screen.height}";
            string activeLabel = string.IsNullOrEmpty(Options.ActiveProfile) ? "<none>" : Options.ActiveProfile;
            string selectedLabel = string.IsNullOrEmpty(_selectedProfile) ? "<none>" : _selectedProfile;
            string profileLine = string.Equals(activeLabel, selectedLabel, StringComparison.OrdinalIgnoreCase)
                ? activeLabel
                : $"{activeLabel} (active) / {selectedLabel} (sel)";
            GUI.Label(new Rect(x, y, width, lineHeight), $"Profile: {profileLine}");
            y += lineHeight;

            float half = (width - buttonGap) / 2f;
            if (GUI.Button(new Rect(x, y, half, buttonHeight), $"Save {currentRes}"))
            {
                SaveProfile(currentRes);
                _selectedProfile = currentRes;
            }
            if (GUI.Button(new Rect(x + half + buttonGap, y, half, buttonHeight), $"Load {currentRes}"))
            {
                LoadProfile(currentRes, false);
                _selectedProfile = currentRes;
            }
            y += buttonHeight + 2f;

            if (GUI.Button(new Rect(x, y, half, buttonHeight), "Save Selected"))
            {
                if (!string.IsNullOrEmpty(_selectedProfile))
                {
                    SaveProfile(_selectedProfile);
                }
            }
            if (GUI.Button(new Rect(x + half + buttonGap, y, half, buttonHeight), "Load Selected"))
            {
                if (!string.IsNullOrEmpty(_selectedProfile))
                {
                    LoadProfile(_selectedProfile, false);
                }
            }
            y += buttonHeight + 2f;

            if (GUI.Button(new Rect(x, y, half, buttonHeight), "Delete Selected"))
            {
                if (!string.IsNullOrEmpty(_selectedProfile))
                {
                    DeleteProfile(_selectedProfile);
                    _selectedProfile = string.Empty;
                }
            }
            if (GUI.Button(new Rect(x + half + buttonGap, y, half, buttonHeight), "Save New"))
            {
                string newName = $"Custom_{DateTime.Now:yyyyMMdd_HHmmss}";
                SaveProfile(newName);
                _selectedProfile = newName;
            }
            y += buttonHeight + 2f;

            bool autoProfile = GUI.Toggle(new Rect(x, y, width, lineHeight), Options.AutoProfileByResolution, $"Auto profile ({currentRes})");
            y += lineHeight + 2f;

            float presetWidth = (width - buttonGap * 2f) / 3f;
            if (GUI.Button(new Rect(x, y, presetWidth, buttonHeight), "1080p"))
            {
                _selectedProfile = "1920x1080";
            }
            if (GUI.Button(new Rect(x + presetWidth + buttonGap, y, presetWidth, buttonHeight), "4K"))
            {
                _selectedProfile = "3840x2160";
            }
            if (GUI.Button(new Rect(x + (presetWidth + buttonGap) * 2f, y, presetWidth, buttonHeight), "UltraWide"))
            {
                _selectedProfile = "UltraWide";
            }
            y += buttonHeight + 4f;

            if (autoProfile != Options.AutoProfileByResolution)
            {
                Options.AutoProfileByResolution = autoProfile;
                if (autoProfile)
                {
                    TryLoadAutoProfile(true);
                }
            }

            List<string> profiles = GetProfileNames();
            if (profiles.Count == 0)
            {
                GUI.Label(new Rect(x, y, width, lineHeight), "No saved profiles.");
                y += lineHeight + 2f;
                return;
            }

            float listHeight = 60f;
            float rowHeight = 20f;
            int itemsPerPage = Mathf.Max(1, Mathf.FloorToInt(listHeight / rowHeight));
            int maxPage = Math.Max(0, (profiles.Count - 1) / itemsPerPage);
            _profilePage = Mathf.Clamp(_profilePage, 0, maxPage);

            GUI.Box(new Rect(x, y, width, listHeight), GUIContent.none);
            int startIndex = _profilePage * itemsPerPage;
            float rowY = y;
            for (int i = 0; i < itemsPerPage; i++)
            {
                int index = startIndex + i;
                if (index >= profiles.Count)
                {
                    break;
                }

                string name = profiles[index];
                if (GUI.Button(new Rect(x, rowY, width, rowHeight), name))
                {
                    _selectedProfile = name;
                }
                rowY += rowHeight;
            }
            y += listHeight + 4f;

            if (maxPage > 0)
            {
                float navWidth = (width - buttonGap) / 2f;
                if (GUI.Button(new Rect(x, y, navWidth, buttonHeight), "< Prev"))
                {
                    _profilePage = Mathf.Max(0, _profilePage - 1);
                }
                if (GUI.Button(new Rect(x + navWidth + buttonGap, y, navWidth, buttonHeight), "Next >"))
                {
                    _profilePage = Mathf.Min(maxPage, _profilePage + 1);
                }
                y += buttonHeight + 2f;
                GUI.Label(new Rect(x, y, width, lineHeight), $"Page {_profilePage + 1}/{maxPage + 1}");
                y += lineHeight + 2f;
            }

            if (string.IsNullOrEmpty(_selectedProfile))
            {
                _selectedProfile = profiles[0];
            }
        }

        void DrawElementList(ref float x, ref float y, float width, float lineHeight, float buttonHeight, float buttonGap)
        {
            GUI.Label(new Rect(x, y, width, lineHeight), "Elements:");
            y += lineHeight;

            float listHeight = 100f;
            string[] keys = Elements.Keys.OrderBy(key => key).ToArray();
            float rowHeight = 20f;
            int itemsPerPage = Mathf.Max(1, Mathf.FloorToInt(listHeight / rowHeight));
            int maxPage = keys.Length > 0 ? (keys.Length - 1) / itemsPerPage : 0;
            _elementPage = Mathf.Clamp(_elementPage, 0, maxPage);

            GUI.Box(new Rect(x, y, width, listHeight), GUIContent.none);
            int startIndex = _elementPage * itemsPerPage;
            float rowY = y;
            for (int i = 0; i < itemsPerPage; i++)
            {
                int index = startIndex + i;
                if (index >= keys.Length)
                {
                    break;
                }

                string key = keys[index];
                ElementState state = GetElementState(key);
                float nameWidth = width - 100f;
                Rect nameRect = new(x, rowY, nameWidth, rowHeight);
                if (GUI.Button(nameRect, key))
                {
                    _selectedKey = key;
                }

                bool highlight = GUI.Toggle(new Rect(x + nameWidth + 4f, rowY, 28f, rowHeight), state.Highlight, "H");
                bool locked = GUI.Toggle(new Rect(x + nameWidth + 34f, rowY, 28f, rowHeight), state.Locked, "L");
                bool hidden = GUI.Toggle(new Rect(x + nameWidth + 64f, rowY, 36f, rowHeight), state.Hidden, "Hide");

                if (highlight != state.Highlight)
                {
                    state.Highlight = highlight;
                }
                if (locked != state.Locked)
                {
                    state.Locked = locked;
                }
                if (hidden != state.Hidden)
                {
                    state.Hidden = hidden;
                    if (Elements.TryGetValue(key, out LayoutElement element))
                    {
                        ApplyElementState(element);
                    }
                }

                SavedStates[key] = state;
                rowY += rowHeight;
            }

            y += listHeight + 4f;

            if (maxPage > 0)
            {
                float navWidth = (width - buttonGap) / 2f;
                if (GUI.Button(new Rect(x, y, navWidth, buttonHeight), "< Prev"))
                {
                    _elementPage = Mathf.Max(0, _elementPage - 1);
                }
                if (GUI.Button(new Rect(x + navWidth + buttonGap, y, navWidth, buttonHeight), "Next >"))
                {
                    _elementPage = Mathf.Min(maxPage, _elementPage + 1);
                }
                y += buttonHeight + 2f;
                GUI.Label(new Rect(x, y, width, lineHeight), $"Page {_elementPage + 1}/{maxPage + 1}");
                y += lineHeight + 2f;
            }

            if (TryGetSelectedElement(out LayoutElement selected) && selected.Rect != null)
            {
                Vector2 pos = selected.Rect.anchoredPosition;
                Vector3 scale = selected.Rect.localScale;
                GUI.Label(new Rect(x, y, width, lineHeight), $"Pos: {pos.x:F1}, {pos.y:F1}");
                y += lineHeight;
                GUI.Label(new Rect(x, y, width, lineHeight), $"Scale: {scale.x:F2}, {scale.y:F2}");
                y += lineHeight;
            }
            else
            {
                y += lineHeight * 2f;
            }

            float itemButtonWidth = (width - buttonGap * 2f) / 3f;
            if (GUI.Button(new Rect(x, y, itemButtonWidth, buttonHeight), "Reset Item"))
            {
                ResetSelectedElement();
            }
            if (GUI.Button(new Rect(x + itemButtonWidth + buttonGap, y, itemButtonWidth, buttonHeight), "Copy"))
            {
                CopySelectedElement();
            }
            if (GUI.Button(new Rect(x + (itemButtonWidth + buttonGap) * 2f, y, itemButtonWidth, buttonHeight), "Paste"))
            {
                PasteSelectedElement();
            }
            y += buttonHeight + 6f;
        }

        void DrawFooterButtons(ref float x, ref float y, float width, float buttonHeight, float buttonGap)
        {
            float buttonWidth = (width - buttonGap) / 2f;
            if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "Save Layout"))
            {
                SaveLayouts();
            }
            if (GUI.Button(new Rect(x + buttonWidth + buttonGap, y, buttonWidth, buttonHeight), "Dump"))
            {
                DumpLayouts();
            }
            y += buttonHeight + 4f;

            if (GUI.Button(new Rect(x, y, width, buttonHeight), "Defaults"))
            {
                ResetDefaults();
            }
            y += buttonHeight + 4f;

            if (GUI.Button(new Rect(x, y, width, buttonHeight), "Delete Saved Layout"))
            {
                ClearLayoutFile();
            }
        }

        bool TryBeginPanelDrag()
        {
            Vector2 guiPoint = ScreenToGui(Input.mousePosition);
            Rect headerRect = new(_windowRect.x, _windowRect.y, _windowRect.width, PanelHeaderHeight);
            if (headerRect.Contains(guiPoint))
            {
                _draggingPanel = true;
                _panelDragOffset = guiPoint - new Vector2(_windowRect.x, _windowRect.y);
                return true;
            }

            return false;
        }

        void DragPanel()
        {
            Vector2 guiPoint = ScreenToGui(Input.mousePosition);
            _windowRect.x = guiPoint.x - _panelDragOffset.x;
            _windowRect.y = guiPoint.y - _panelDragOffset.y;
            ClampWindowToScreen();
        }

        void ResizeHovered(float scroll)
        {
            RectTransform target = _draggingRect;
            if (target == null)
            {
                if (TryGetHoveredElement(out LayoutElement hovered))
                {
                    if (IsLocked(hovered.Key))
                    {
                        return;
                    }

                    target = hovered.Rect;
                }
            }

            if (target == null)
            {
                return;
            }

            const float step = 0.05f;
            const float minScale = 0.2f;
            const float maxScale = 3f;

            Vector3 current = target.localScale;
            float baseScale = Mathf.Max(0.0001f, current.x);
            float next = Mathf.Clamp(baseScale * (1f + scroll * step), minScale, maxScale);
            float factor = next / baseScale;

            target.localScale = new Vector3(current.x * factor, current.y * factor, current.z * factor);
        }

        void DrawSafeArea()
        {
            if (!Options.ShowSafeArea)
            {
                return;
            }

            Rect safeRect = GetSafeAreaRect();
            DrawRectOutline(safeRect, SafeAreaColor, GuideThickness);
        }

        void DrawGrid()
        {
            if (!Options.ShowGrid || Options.GridSize < 2f)
            {
                return;
            }

            Rect bounds = Options.ShowSafeArea ? GetSafeAreaRect() : new Rect(0f, 0f, Screen.width, Screen.height);
            float grid = Options.GridSize;

            for (float x = bounds.x; x <= bounds.xMax; x += grid)
            {
                DrawRectFilled(new Rect(x, bounds.y, 1f, bounds.height), GridColor);
            }

            for (float y = bounds.y; y <= bounds.yMax; y += grid)
            {
                DrawRectFilled(new Rect(bounds.x, y, bounds.width, 1f), GridColor);
            }
        }

        void DrawGuides()
        {
            if (!Options.ShowGuides || _guides.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _guides.Count; i++)
            {
                DrawRectFilled(_guides[i].Rect, _guides[i].Color);
            }
        }

        void DrawOutlines()
        {
            foreach (LayoutElement entry in Elements.Values)
            {
                RectTransform rect = entry.ResolveHitRect();
                if (rect == null || !rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Canvas canvas = rect.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

                Rect screenRect = GetScreenRect(rect, cam);
                if (screenRect.width <= 0f || screenRect.height <= 0f)
                {
                    continue;
                }

                Color color = OutlineColor;
                ElementState state = GetElementState(entry.Key);
                if (state.Highlight)
                {
                    color = HighlightColor;
                }
                if (_selectedKey == entry.Key)
                {
                    color = SelectedColor;
                }

                DrawRectOutline(screenRect, color, 1f);
            }
        }

        void TryBeginDrag()
        {
            Vector2 mousePos = Input.mousePosition;
            LayoutElement best = null;
            Camera bestCamera = null;

            foreach (LayoutElement entry in Elements.Values)
            {
                RectTransform rect = entry.ResolveHitRect();
                if (rect == null || !rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsLocked(entry.Key))
                {
                    continue;
                }

                if (TryGetHitCamera(entry, mousePos, out Camera hitCamera))
                {
                    best = entry;
                    bestCamera = hitCamera;
                    break;
                }
            }

            if (best == null)
            {
                return;
            }

            _selectedKey = best.Key;
            _draggingKey = best.Key;
            _draggingRect = best.Rect;
            _draggingCamera = bestCamera;

            if (_draggingRect != null)
            {
                RectTransform parentRect = _draggingRect.parent as RectTransform;
                if (parentRect == null)
                {
                    parentRect = _draggingRect;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, _draggingCamera, out Vector2 localPoint))
                {
                    _dragOffset = _draggingRect.anchoredPosition - localPoint;
                }

                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_draggingRect, mousePos, _draggingCamera, out Vector3 worldPoint))
                {
                    _draggingWorldOffset = _draggingRect.position - worldPoint;
                }
            }
        }

        void Drag()
        {
            if (_draggingRect == null)
            {
                return;
            }

            Vector2 mousePos = Input.mousePosition;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_draggingRect, mousePos, _draggingCamera, out Vector3 worldPoint))
            {
                Vector3 targetWorld = worldPoint + _draggingWorldOffset;
                if (Options.SnapToGrid || Options.SnapToEdges || Options.SnapToElements)
                {
                    targetWorld = ApplySnappingWorld(targetWorld, _draggingRect, _draggingCamera);
                }

                _draggingRect.position = targetWorld;
                return;
            }

            RectTransform parentRect = _draggingRect.parent as RectTransform;
            if (parentRect == null)
            {
                parentRect = _draggingRect;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, _draggingCamera, out Vector2 localPoint))
            {
                Vector2 target = localPoint + _dragOffset;
                Vector2 snapped = ApplySnappingLocal(target, _draggingRect, parentRect, _draggingCamera);
                _draggingRect.anchoredPosition = snapped;
            }
        }

        Vector3 ApplySnappingWorld(Vector3 targetWorld, RectTransform rect, Camera cam)
        {
            _guides.Clear();

            Camera resolvedCam = cam ?? ResolveCamera(rect);
            Rect screenRect = GetScreenRectAtWorldPosition(rect, targetWorld, resolvedCam);
            float deltaX = 0f;
            float deltaY = 0f;

            if (Options.SnapToGrid && Options.GridSize > 1f)
            {
                float grid = Options.GridSize;
                float snapX = Mathf.Round(screenRect.x / grid) * grid;
                float snapY = Mathf.Round(screenRect.y / grid) * grid;
                deltaX = snapX - screenRect.x;
                deltaY = snapY - screenRect.y;
            }

            float bestXDist = Options.SnapThreshold;
            float bestYDist = Options.SnapThreshold;
            GuideLine? bestXGuide = null;
            GuideLine? bestYGuide = null;

            if (Options.SnapToEdges)
            {
                Rect bounds = GetSafeAreaRect();
                float left = bounds.x;
                float right = bounds.x + bounds.width;
                float top = bounds.y;
                float bottom = bounds.y + bounds.height;

                ConsiderSnap(ref deltaX, ref bestXDist, left - screenRect.x, left, top, bottom, true, ref bestXGuide);
                ConsiderSnap(ref deltaX, ref bestXDist, right - (screenRect.x + screenRect.width), right, top, bottom, true, ref bestXGuide);
                ConsiderSnap(ref deltaY, ref bestYDist, top - screenRect.y, top, left, right, false, ref bestYGuide);
                ConsiderSnap(ref deltaY, ref bestYDist, bottom - (screenRect.y + screenRect.height), bottom, left, right, false, ref bestYGuide);
            }

            if (Options.SnapToElements)
            {
                foreach (LayoutElement element in Elements.Values)
                {
                    if (element.Rect == null || element.Key == _draggingKey)
                    {
                        continue;
                    }

                    if (GetElementState(element.Key).Hidden)
                    {
                        continue;
                    }

                    RectTransform targetRect = element.ResolveHitRect();
                    if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Rect other = GetScreenRect(targetRect, element.GetCamera());
                    float otherLeft = other.x;
                    float otherRight = other.x + other.width;
                    float otherTop = other.y;
                    float otherBottom = other.y + other.height;
                    float otherCenterX = other.x + other.width * 0.5f;
                    float otherCenterY = other.y + other.height * 0.5f;

                    float thisLeft = screenRect.x;
                    float thisRight = screenRect.x + screenRect.width;
                    float thisTop = screenRect.y;
                    float thisBottom = screenRect.y + screenRect.height;
                    float thisCenterX = screenRect.x + screenRect.width * 0.5f;
                    float thisCenterY = screenRect.y + screenRect.height * 0.5f;

                    ConsiderSnap(ref deltaX, ref bestXDist, otherLeft - thisLeft, otherLeft, otherTop, otherBottom, true, ref bestXGuide);
                    ConsiderSnap(ref deltaX, ref bestXDist, otherRight - thisRight, otherRight, otherTop, otherBottom, true, ref bestXGuide);
                    ConsiderSnap(ref deltaX, ref bestXDist, otherCenterX - thisCenterX, otherCenterX, otherTop, otherBottom, true, ref bestXGuide);

                    ConsiderSnap(ref deltaY, ref bestYDist, otherTop - thisTop, otherTop, otherLeft, otherRight, false, ref bestYGuide);
                    ConsiderSnap(ref deltaY, ref bestYDist, otherBottom - thisBottom, otherBottom, otherLeft, otherRight, false, ref bestYGuide);
                    ConsiderSnap(ref deltaY, ref bestYDist, otherCenterY - thisCenterY, otherCenterY, otherLeft, otherRight, false, ref bestYGuide);
                }
            }

            if (Options.ShowGuides)
            {
                if (bestXGuide.HasValue)
                {
                    _guides.Add(bestXGuide.Value);
                }
                if (bestYGuide.HasValue)
                {
                    _guides.Add(bestYGuide.Value);
                }
            }

            Vector2 screenDelta = new(deltaX, deltaY);
            Vector3 worldDelta = ScreenDeltaToWorldDelta(rect, resolvedCam, screenRect.center, screenDelta);
            return targetWorld + worldDelta;
        }

        static void ConsiderSnap(ref float delta, ref float bestDist, float candidateDelta, float guidePos, float guideStart, float guideEnd, bool vertical, ref GuideLine? bestGuide)
        {
            float dist = Mathf.Abs(candidateDelta);
            if (dist <= bestDist)
            {
                bestDist = dist;
                delta = candidateDelta;
                bestGuide = new GuideLine(BuildGuideRect(guidePos, guideStart, guideEnd, vertical), GuideColor);
            }
        }

        Vector2 ApplySnappingLocal(Vector2 anchoredPos, RectTransform rect, RectTransform parentRect, Camera cam)
        {
            _guides.Clear();

            Camera resolvedCam = cam ?? ResolveCamera(rect);
            Rect screenRect = GetScreenRectAtPosition(rect, anchoredPos, resolvedCam);
            float deltaX = 0f;
            float deltaY = 0f;

            if (Options.SnapToGrid && Options.GridSize > 1f)
            {
                float grid = Options.GridSize;
                float snapX = Mathf.Round(screenRect.x / grid) * grid;
                float snapY = Mathf.Round(screenRect.y / grid) * grid;
                deltaX = snapX - screenRect.x;
                deltaY = snapY - screenRect.y;
            }

            float bestXDist = Options.SnapThreshold;
            float bestYDist = Options.SnapThreshold;
            GuideLine? bestXGuide = null;
            GuideLine? bestYGuide = null;

            if (Options.SnapToEdges)
            {
                Rect bounds = GetSafeAreaRect();
                float left = bounds.x;
                float right = bounds.x + bounds.width;
                float top = bounds.y;
                float bottom = bounds.y + bounds.height;

                ConsiderSnap(ref deltaX, ref bestXDist, left - screenRect.x, left, top, bottom, true, ref bestXGuide);
                ConsiderSnap(ref deltaX, ref bestXDist, right - (screenRect.x + screenRect.width), right, top, bottom, true, ref bestXGuide);
                ConsiderSnap(ref deltaY, ref bestYDist, top - screenRect.y, top, left, right, false, ref bestYGuide);
                ConsiderSnap(ref deltaY, ref bestYDist, bottom - (screenRect.y + screenRect.height), bottom, left, right, false, ref bestYGuide);
            }

            if (Options.SnapToElements)
            {
                foreach (LayoutElement element in Elements.Values)
                {
                    if (element.Rect == null || element.Key == _draggingKey)
                    {
                        continue;
                    }

                    if (GetElementState(element.Key).Hidden)
                    {
                        continue;
                    }

                    RectTransform targetRect = element.ResolveHitRect();
                    if (targetRect == null || !targetRect.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Rect other = GetScreenRect(targetRect, element.GetCamera());
                    float otherLeft = other.x;
                    float otherRight = other.x + other.width;
                    float otherTop = other.y;
                    float otherBottom = other.y + other.height;
                    float otherCenterX = other.x + other.width * 0.5f;
                    float otherCenterY = other.y + other.height * 0.5f;

                    float thisLeft = screenRect.x;
                    float thisRight = screenRect.x + screenRect.width;
                    float thisTop = screenRect.y;
                    float thisBottom = screenRect.y + screenRect.height;
                    float thisCenterX = screenRect.x + screenRect.width * 0.5f;
                    float thisCenterY = screenRect.y + screenRect.height * 0.5f;

                    ConsiderSnap(ref deltaX, ref bestXDist, otherLeft - thisLeft, otherLeft, otherTop, otherBottom, true, ref bestXGuide);
                    ConsiderSnap(ref deltaX, ref bestXDist, otherRight - thisRight, otherRight, otherTop, otherBottom, true, ref bestXGuide);
                    ConsiderSnap(ref deltaX, ref bestXDist, otherCenterX - thisCenterX, otherCenterX, otherTop, otherBottom, true, ref bestXGuide);

                    ConsiderSnap(ref deltaY, ref bestYDist, otherTop - thisTop, otherTop, otherLeft, otherRight, false, ref bestYGuide);
                    ConsiderSnap(ref deltaY, ref bestYDist, otherBottom - thisBottom, otherBottom, otherLeft, otherRight, false, ref bestYGuide);
                    ConsiderSnap(ref deltaY, ref bestYDist, otherCenterY - thisCenterY, otherCenterY, otherLeft, otherRight, false, ref bestYGuide);
                }
            }

            if (Options.ShowGuides)
            {
                if (bestXGuide.HasValue)
                {
                    _guides.Add(bestXGuide.Value);
                }
                if (bestYGuide.HasValue)
                {
                    _guides.Add(bestYGuide.Value);
                }
            }

            Vector2 screenDelta = new(deltaX, deltaY);
            Vector2 localDelta = ScreenDeltaToLocalDelta(parentRect, resolvedCam, screenRect.center, screenDelta);
            return anchoredPos + localDelta;
        }

        bool IsPointerOverWindow()
        {
            if (_draggingPanel)
            {
                return true;
            }

            Vector2 guiPoint = ScreenToGui(Input.mousePosition);
            return _windowRect.Contains(guiPoint);
        }

        void HandleNudge()
        {
            if (_draggingRect != null || _draggingPanel)
            {
                return;
            }

            LayoutElement target;
            if (!TryGetSelectedElement(out target))
            {
                if (!TryGetHoveredElement(out target))
                {
                    return;
                }
            }

            if (target == null || target.Rect == null || IsLocked(target.Key))
            {
                return;
            }

            float step = Options.NudgeStep;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                step *= 5f;
            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                step *= 0.2f;
            }

            Vector2 delta = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow)) delta.x -= step;
            if (Input.GetKey(KeyCode.RightArrow)) delta.x += step;
            if (Input.GetKey(KeyCode.UpArrow)) delta.y += step;
            if (Input.GetKey(KeyCode.DownArrow)) delta.y -= step;

            if (delta != Vector2.zero)
            {
                target.Rect.anchoredPosition += delta;
            }
        }

        bool TryGetSelectedElement(out LayoutElement element)
        {
            if (string.IsNullOrEmpty(_selectedKey))
            {
                element = null;
                return false;
            }

            return Elements.TryGetValue(_selectedKey, out element);
        }

        void ResetSelectedElement()
        {
            if (!TryGetSelectedElement(out LayoutElement element) || element.Rect == null)
            {
                return;
            }

            ApplyLayoutEntry(element.Rect, element.DefaultLayout);
            ActiveLayouts.Remove(element.Key);
        }

        void CopySelectedElement()
        {
            if (!TryGetSelectedElement(out LayoutElement element) || element.Rect == null)
            {
                return;
            }

            ClipboardEntry = LayoutEntry.FromRect(element.Rect);
            ClipboardKey = element.Key;
        }

        void PasteSelectedElement()
        {
            if (!TryGetSelectedElement(out LayoutElement element) || element.Rect == null)
            {
                return;
            }

            if (ClipboardEntry == null)
            {
                return;
            }

            ApplyLayoutEntry(element.Rect, ClipboardEntry);
            ActiveLayouts[element.Key] = LayoutEntry.FromRect(element.Rect);
        }

        bool IsLocked(string key)
        {
            return GetElementState(key).Locked;
        }

        static Vector2 ScreenToGui(Vector2 screenPos)
        {
            return new Vector2(screenPos.x, Screen.height - screenPos.y);
        }

        static Vector2 GuiToScreen(Vector2 guiPos)
        {
            return new Vector2(guiPos.x, Screen.height - guiPos.y);
        }

        static Vector2 GuiDeltaToScreenDelta(Vector2 guiDelta)
        {
            return new Vector2(guiDelta.x, -guiDelta.y);
        }

        void ClampWindowToScreen()
        {
            float maxX = Mathf.Max(0f, Screen.width - _windowRect.width);
            float maxY = Mathf.Max(0f, Screen.height - _windowRect.height);
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, maxY);
        }

        static Rect GetScreenRect(RectTransform rect, Camera cam)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float width = max.x - min.x;
            float height = max.y - min.y;

            return new Rect(min.x, Screen.height - max.y, width, height);
        }

        static Rect GetScreenRectAtPosition(RectTransform rect, Vector2 anchoredPos, Camera cam)
        {
            Vector2 original = rect.anchoredPosition;
            rect.anchoredPosition = anchoredPos;
            Rect screenRect = GetScreenRect(rect, cam);
            rect.anchoredPosition = original;
            return screenRect;
        }

        static Rect GetScreenRectAtWorldPosition(RectTransform rect, Vector3 worldPos, Camera cam)
        {
            Vector3 original = rect.position;
            rect.position = worldPos;
            Rect screenRect = GetScreenRect(rect, cam);
            rect.position = original;
            return screenRect;
        }

        static Vector2 ScreenDeltaToLocalDelta(RectTransform parentRect, Camera cam, Vector2 screenPoint, Vector2 screenDelta)
        {
            if (parentRect == null)
            {
                return screenDelta;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out Vector2 localA)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint + screenDelta, cam, out Vector2 localB))
            {
                return localB - localA;
            }

            return screenDelta;
        }

        static Vector3 ScreenDeltaToWorldDelta(RectTransform rect, Camera cam, Vector2 guiPoint, Vector2 guiDelta)
        {
            Vector2 screenPoint = GuiToScreen(guiPoint);
            Vector2 screenDelta = GuiDeltaToScreenDelta(guiDelta);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPoint, cam, out Vector3 worldA)
                && RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPoint + screenDelta, cam, out Vector3 worldB))
            {
                return worldB - worldA;
            }

            return new Vector3(guiDelta.x, -guiDelta.y, 0f);
        }

        static Rect GetSafeAreaRect()
        {
            float padding = Mathf.Max(0f, Options.SafeAreaPadding);
            return new Rect(padding, padding, Screen.width - padding * 2f, Screen.height - padding * 2f);
        }

        static Rect BuildGuideRect(float guidePos, float start, float end, bool vertical)
        {
            if (vertical)
            {
                float height = Mathf.Max(1f, end - start);
                return new Rect(guidePos - GuideThickness * 0.5f, start, GuideThickness, height);
            }

            float width = Mathf.Max(1f, end - start);
            return new Rect(start, guidePos - GuideThickness * 0.5f, width, GuideThickness);
        }

        static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);

            GUI.color = previous;
        }

        static void DrawRectFilled(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static bool TryGetHitCamera(LayoutElement entry, Vector2 mousePos, out Camera hitCamera)
        {
            RectTransform rect = entry.ResolveHitRect();
            if (rect == null)
            {
                hitCamera = null;
                return false;
            }

            List<Camera> candidates = new();
            Camera primary = entry.GetCamera();
            if (primary != null)
            {
                candidates.Add(primary);
            }

            Camera resolved = ResolveCamera(rect);
            if (resolved != null && resolved != primary)
            {
                candidates.Add(resolved);
            }

            Camera[] allCameras = Camera.allCameras;
            for (int i = 0; i < allCameras.Length; i++)
            {
                Camera cam = allCameras[i];
                if (cam != null && !candidates.Contains(cam))
                {
                    candidates.Add(cam);
                }
            }

            candidates.Add(null);

            for (int i = 0; i < candidates.Count; i++)
            {
                Camera cam = candidates[i];
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos, cam))
                {
                    hitCamera = cam;
                    return true;
                }
            }

            Vector2 guiPoint = new(mousePos.x, Screen.height - mousePos.y);
            for (int i = 0; i < candidates.Count; i++)
            {
                Camera cam = candidates[i];
                Rect screenRect = GetScreenRect(rect, cam);
                if (screenRect.Contains(guiPoint))
                {
                    hitCamera = cam;
                    return true;
                }
            }

            hitCamera = primary ?? resolved;
            return false;
        }

        static bool TryGetHoveredElement(out LayoutElement hovered)
        {
            Vector2 mousePos = Input.mousePosition;
            foreach (LayoutElement entry in Elements.Values)
            {
                RectTransform rect = entry.ResolveHitRect();
                if (rect == null || !rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (TryGetHitCamera(entry, mousePos, out _))
                {
                    hovered = entry;
                    return true;
                }
            }

            hovered = null;
            return false;
        }

        void DrawValueStepper(ref float x, ref float y, float width, float lineHeight, string label, ref float value, float step, float min, float max, string format)
        {
            float buttonWidth = 28f;
            float labelWidth = width - (buttonWidth * 2f) - 6f;
            float multiplier = GetAdjustMultiplier();

            GUI.Label(new Rect(x, y, labelWidth, lineHeight), $"{label}: {value.ToString(format, CultureInfo.InvariantCulture)}");
            if (GUI.Button(new Rect(x + labelWidth + 2f, y, buttonWidth, lineHeight), "-"))
            {
                value = Mathf.Max(min, value - (step * multiplier));
            }
            if (GUI.Button(new Rect(x + labelWidth + buttonWidth + 4f, y, buttonWidth, lineHeight), "+"))
            {
                value = Mathf.Min(max, value + (step * multiplier));
            }
            y += lineHeight + 2f;
        }

        static float GetAdjustMultiplier()
        {
            float multiplier = 1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                multiplier *= 5f;
            }
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                multiplier *= 0.2f;
            }
            return multiplier;
        }
    }

    sealed class LayoutElement
    {
        public string Key { get; }
        public RectTransform Rect { get; }
        public RectTransform HitRect { get; }
        public LayoutEntry DefaultLayout { get; }

        public LayoutElement(string key, RectTransform rect, RectTransform hitRect)
        {
            Key = key;
            Rect = rect;
            HitRect = hitRect;
            DefaultLayout = rect != null ? LayoutEntry.FromRect(rect) : new LayoutEntry();
        }

        public RectTransform ResolveHitRect()
        {
            return HitRect != null ? HitRect : Rect;
        }

        public Camera GetCamera()
        {
            return ResolveCamera(ResolveHitRect());
        }
    }

    sealed class LayoutEntry
    {
        public float AnchorMinX { get; set; }
        public float AnchorMinY { get; set; }
        public float AnchorMaxX { get; set; }
        public float AnchorMaxY { get; set; }
        public float PivotX { get; set; }
        public float PivotY { get; set; }
        public float AnchoredPosX { get; set; }
        public float AnchoredPosY { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float ScaleZ { get; set; }

        public static LayoutEntry FromRect(RectTransform rect)
        {
            return new LayoutEntry
            {
                AnchorMinX = rect.anchorMin.x,
                AnchorMinY = rect.anchorMin.y,
                AnchorMaxX = rect.anchorMax.x,
                AnchorMaxY = rect.anchorMax.y,
                PivotX = rect.pivot.x,
                PivotY = rect.pivot.y,
                AnchoredPosX = rect.anchoredPosition.x,
                AnchoredPosY = rect.anchoredPosition.y,
                ScaleX = rect.localScale.x,
                ScaleY = rect.localScale.y,
                ScaleZ = rect.localScale.z
            };
        }
    }

    sealed class ElementState
    {
        public bool Hidden { get; set; }
        public bool Locked { get; set; }
        public bool Highlight { get; set; }
    }

    sealed class LayoutConfig
    {
        public Dictionary<string, LayoutEntry> Elements { get; set; } = new();
        public Dictionary<string, LayoutEntry> KeyboardLayouts { get; set; } = new();
        public Dictionary<string, LayoutEntry> GamepadLayouts { get; set; } = new();
        public Dictionary<string, ElementState> ElementStates { get; set; } = new();
        public LayoutOptions Options { get; set; } = new();
    }

    internal sealed class LayoutOptions
    {
        public LayoutModeType LayoutMode { get; set; } = LayoutModeType.KeyboardMouse;
        public bool VerticalBars { get; set; }
        public bool CompactQuests { get; set; }
        public bool SnapToGrid { get; set; } = true;
        public bool ShowGrid { get; set; } = true;
        public float GridSize { get; set; } = 10f;
        public bool SnapToEdges { get; set; } = true;
        public bool SnapToElements { get; set; } = true;
        public float SnapThreshold { get; set; } = 6f;
        public bool ShowGuides { get; set; } = true;
        public bool ShowSafeArea { get; set; }
        public float SafeAreaPadding { get; set; }
        public bool AutoProfileByResolution { get; set; }
        public string ActiveProfile { get; set; } = string.Empty;
        public float NudgeStep { get; set; } = 1f;
    }

    internal enum LayoutModeType
    {
        KeyboardMouse,
        Gamepad
    }

    readonly struct GuideLine
    {
        public Rect Rect { get; }
        public Color Color { get; }

        public GuideLine(Rect rect, Color color)
        {
            Rect = rect;
            Color = color;
        }
    }
}

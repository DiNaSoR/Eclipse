using Eclipse.Utilities;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ProjectM.UI;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Stunlock.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Eclipse.Services.CanvasService.DataHUD;
using static Eclipse.Services.CanvasService.UtilitiesHUD;
using static Eclipse.Services.DataService;

namespace Eclipse.Services;

/// <summary>
/// Adds the Bloodcraft tab to the Character menu and renders Bloodcraft data inside it.
/// </summary>
internal static class CharacterMenuService
{
    const string CharacterMenuName = "CharacterInventorySubMenu(Clone)";
    const string TabButtonsPath = "MotionRoot/TabButtons";
    const string TabContentPath = "Scroll View/Viewport/AttributesTabContent";
    const string AttributesTabButtonName = "AttributesTabButton";
    const string BloodcraftTabButtonName = "BloodcraftTabButton";
    const string BloodcraftTabName = "BloodcraftTab";

    static InventorySubMenu inventorySubMenu;
    static RectTransform bloodcraftTab;
    static SimpleStunButton bloodcraftTabButton;
    static Transform entriesRoot;
    static GameObject entryTemplate;
    static readonly List<TextMeshProUGUI> entries = [];
    static readonly List<SimpleStunButton> entryButtons = [];
    static TabType activeTab = TabType.Prestige;
    static int bloodcraftTabIndex = -1;
    static int lastKnownTabIndex = -1;
    static bool initialized;
    static bool manualActive;

    /// <summary>
    /// Initializes the Bloodcraft tab inside the Character menu.
    /// </summary>
    /// <param name="menu">The inventory sub-menu instance.</param>
    public static void TryInitialize(InventorySubMenu menu)
    {
        if (menu == null || !IsCharacterMenu(menu))
        {
            return;
        }

        if (initialized && inventorySubMenu == menu)
        {
            return;
        }

        Reset();
        inventorySubMenu = menu;

        Il2CppReferenceArray<RectTransform> tabs = menu.Tabs;
        Il2CppReferenceArray<SimpleStunButton> tabButtons = menu.TabButtons;
        if (tabs == null || tabButtons == null || tabs.Length == 0 || tabButtons.Length == 0)
        {
            Core.Log.LogWarning("[Bloodcraft Tab] Failed to read existing tabs.");
            return;
        }

        Transform tabButtonsRoot = menu.transform.Find(TabButtonsPath);
        SimpleStunButton templateButton = FindTemplateButton(tabButtonsRoot);
        if (templateButton == null)
        {
            Core.Log.LogWarning("[Bloodcraft Tab] Failed to locate tab button template.");
            return;
        }

        bloodcraftTabIndex = tabs.Length;
        bloodcraftTabButton = CreateTabButton(templateButton, tabButtonsRoot);
        bloodcraftTab = CreateTabRoot(tabs[tabs.Length - 1]);

        if (bloodcraftTabButton == null || bloodcraftTab == null || entriesRoot == null || entryTemplate == null)
        {
            Core.Log.LogWarning("[Bloodcraft Tab] Failed to build Bloodcraft tab UI.");
            return;
        }

        menu.TabButtons = AppendTabButton(tabButtons, bloodcraftTabButton);
        menu.Tabs = AppendTab(tabs, bloodcraftTab);
        HookExistingTabButtons(tabButtons);
        ConfigureBloodcraftButton();

        initialized = true;
        Core.Log.LogInfo("[Bloodcraft Tab] Initialized Bloodcraft tab in Character menu.");
    }

    /// <summary>
    /// Updates the Bloodcraft tab content when the Character menu is active.
    /// </summary>
    public static void Update()
    {
        if (!initialized || inventorySubMenu == null || bloodcraftTab == null)
        {
            return;
        }

        int currentTabIndex = inventorySubMenu.CurrentTab;
        bool isActive = manualActive || currentTabIndex == bloodcraftTabIndex;

        if (manualActive && currentTabIndex != lastKnownTabIndex && currentTabIndex != bloodcraftTabIndex)
        {
            manualActive = false;
            isActive = false;
        }

        bloodcraftTab.gameObject.SetActive(isActive);
        UpdateButtonSelection(isActive);

        if (!isActive)
        {
            lastKnownTabIndex = currentTabIndex;
            return;
        }

        ApplyTabVisibility();
        UpdateEntries();
        lastKnownTabIndex = currentTabIndex;
    }

    /// <summary>
    /// Clears cached references when the client UI is destroyed.
    /// </summary>
    public static void Reset()
    {
        inventorySubMenu = null;
        bloodcraftTab = null;
        bloodcraftTabButton = null;
        entriesRoot = null;
        entryTemplate = null;
        entries.Clear();
        entryButtons.Clear();
        activeTab = TabType.Prestige;
        bloodcraftTabIndex = -1;
        lastKnownTabIndex = -1;
        initialized = false;
        manualActive = false;
    }

    /// <summary>
    /// Checks whether the provided menu instance belongs to the Character inventory menu.
    /// </summary>
    /// <param name="menu">The menu instance to inspect.</param>
    /// <returns>True when the menu matches the Character menu.</returns>
    static bool IsCharacterMenu(InventorySubMenu menu)
    {
        return menu.gameObject != null && menu.gameObject.name.Equals(CharacterMenuName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds the existing tab button used as a template for the Bloodcraft tab.
    /// </summary>
    /// <param name="tabButtonsRoot">The root transform containing tab buttons.</param>
    /// <returns>The template tab button or null if not found.</returns>
    static SimpleStunButton FindTemplateButton(Transform tabButtonsRoot)
    {
        if (tabButtonsRoot == null)
        {
            return null;
        }

        Transform templateTransform = tabButtonsRoot.Find(AttributesTabButtonName);
        return templateTransform != null ? templateTransform.GetComponent<SimpleStunButton>() : null;
    }

    /// <summary>
    /// Clones a tab button and updates its label for the Bloodcraft entry.
    /// </summary>
    /// <param name="templateButton">The existing button to clone.</param>
    /// <param name="parent">The parent transform to attach the new button.</param>
    /// <returns>The created tab button or null on failure.</returns>
    static SimpleStunButton CreateTabButton(SimpleStunButton templateButton, Transform parent)
    {
        if (templateButton == null || parent == null)
        {
            return null;
        }

        GameObject buttonObject = UnityEngine.Object.Instantiate(templateButton.gameObject, parent, false);
        buttonObject.name = BloodcraftTabButtonName;
        buttonObject.SetActive(true);
        buttonObject.transform.SetSiblingIndex(templateButton.transform.GetSiblingIndex() + 1);

        UpdateTabButtonLabel(buttonObject);

        return buttonObject.GetComponent<SimpleStunButton>();
    }

    /// <summary>
    /// Updates the tab button label text to Bloodcraft.
    /// </summary>
    /// <param name="buttonObject">The button object whose label is updated.</param>
    static void UpdateTabButtonLabel(GameObject buttonObject)
    {
        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "Bloodcraft";
        }
    }

    /// <summary>
    /// Creates the Bloodcraft tab root by cloning an existing tab and preparing its content container.
    /// </summary>
    /// <param name="templateTab">The tab to clone for layout and styling.</param>
    /// <returns>The new Bloodcraft tab root transform.</returns>
    static RectTransform CreateTabRoot(RectTransform templateTab)
    {
        if (templateTab == null)
        {
            return null;
        }

        RectTransform tabRoot = UnityEngine.Object.Instantiate(templateTab, templateTab.parent, false);
        tabRoot.name = BloodcraftTabName;
        tabRoot.gameObject.SetActive(false);

        TextMeshProUGUI referenceText = templateTab.GetComponentInChildren<TextMeshProUGUI>(true);
        Transform contentRoot = tabRoot.Find(TabContentPath) ?? tabRoot;
        ClearChildren(contentRoot);
        EnsureVerticalLayout(contentRoot);

        if (referenceText == null)
        {
            Core.Log.LogWarning("[Bloodcraft Tab] Failed to find reference text.");
            return tabRoot;
        }

        entriesRoot = contentRoot;
        entryTemplate = CreateEntryTemplate(contentRoot, referenceText);
        return tabRoot;
    }

    /// <summary>
    /// Removes all child objects under a parent transform.
    /// </summary>
    /// <param name="root">The parent transform to clear.</param>
    static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Ensures the target transform has a vertical layout with size fitting for entries.
    /// </summary>
    /// <param name="root">The transform to configure.</param>
    static void EnsureVerticalLayout(Transform root)
    {
        if (root == null)
        {
            return;
        }

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>() ?? root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 6f;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>() ?? root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Creates a reusable entry template for Bloodcraft tab rows.
    /// </summary>
    /// <param name="parent">The parent transform to attach the template.</param>
    /// <param name="reference">A reference text used to copy styling.</param>
    /// <returns>The entry template GameObject.</returns>
    static GameObject CreateEntryTemplate(Transform parent, TextMeshProUGUI reference)
    {
        GameObject templateObject = new("BloodcraftEntryTemplate");
        templateObject.transform.SetParent(parent, false);

        RectTransform rectTransform = templateObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);

        TextMeshProUGUI text = templateObject.AddComponent<TextMeshProUGUI>();
        CopyTextStyle(reference, text);
        text.text = string.Empty;
        text.raycastTarget = true;
        text.enableAutoSizing = false;
        text.enableWordWrapping = false;
        text.richText = true;

        LayoutElement referenceLayout = reference.GetComponent<LayoutElement>();
        if (referenceLayout != null)
        {
            LayoutElement layout = templateObject.AddComponent<LayoutElement>();
            layout.minHeight = referenceLayout.minHeight;
            layout.preferredHeight = referenceLayout.preferredHeight;
            layout.preferredWidth = referenceLayout.preferredWidth;
            layout.flexibleHeight = referenceLayout.flexibleHeight;
        }

        templateObject.SetActive(false);
        return templateObject;
    }

    /// <summary>
    /// Copies text styling settings from a source to a target TMP text component.
    /// </summary>
    /// <param name="source">The reference text component.</param>
    /// <param name="target">The text component to update.</param>
    static void CopyTextStyle(TextMeshProUGUI source, TextMeshProUGUI target)
    {
        target.font = source.font;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.color = source.color;
        target.alignment = source.alignment;
        target.margin = source.margin;
        target.overflowMode = source.overflowMode;
    }

    /// <summary>
    /// Appends a button to the tab button array.
    /// </summary>
    /// <param name="buttons">The existing tab button array.</param>
    /// <param name="newButton">The new button to append.</param>
    /// <returns>A resized array containing the new button.</returns>
    static Il2CppReferenceArray<SimpleStunButton> AppendTabButton(Il2CppReferenceArray<SimpleStunButton> buttons, SimpleStunButton newButton)
    {
        int length = buttons.Length;
        var expanded = new Il2CppReferenceArray<SimpleStunButton>(length + 1);

        for (int i = 0; i < length; i++)
        {
            expanded[i] = buttons[i];
        }

        expanded[length] = newButton;
        return expanded;
    }

    /// <summary>
    /// Appends a tab root to the tab array.
    /// </summary>
    /// <param name="tabs">The existing tab array.</param>
    /// <param name="newTab">The new tab to append.</param>
    /// <returns>A resized array containing the new tab.</returns>
    static Il2CppReferenceArray<RectTransform> AppendTab(Il2CppReferenceArray<RectTransform> tabs, RectTransform newTab)
    {
        int length = tabs.Length;
        var expanded = new Il2CppReferenceArray<RectTransform>(length + 1);

        for (int i = 0; i < length; i++)
        {
            expanded[i] = tabs[i];
        }

        expanded[length] = newTab;
        return expanded;
    }

    /// <summary>
    /// Hooks the existing tab buttons to clear manual Bloodcraft selection.
    /// </summary>
    /// <param name="tabButtons">The existing tab buttons.</param>
    static void HookExistingTabButtons(Il2CppReferenceArray<SimpleStunButton> tabButtons)
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            SimpleStunButton button = tabButtons[i];
            if (button == null)
            {
                continue;
            }

            button.onClick.AddListener((UnityAction)(() => manualActive = false));
        }
    }

    /// <summary>
    /// Binds click handling to the Bloodcraft tab button.
    /// </summary>
    static void ConfigureBloodcraftButton()
    {
        if (bloodcraftTabButton == null)
        {
            return;
        }

        bloodcraftTabButton.onClick.RemoveAllListeners();
        bloodcraftTabButton.onClick.AddListener((UnityAction)ActivateBloodcraftTab);
    }

    /// <summary>
    /// Activates the Bloodcraft tab, falling back to manual mode if needed.
    /// </summary>
    static void ActivateBloodcraftTab()
    {
        if (inventorySubMenu == null)
        {
            return;
        }

        manualActive = false;
        int previousTab = inventorySubMenu.CurrentTab;
        inventorySubMenu.CurrentTab = bloodcraftTabIndex;

        if (inventorySubMenu.CurrentTab != bloodcraftTabIndex)
        {
            manualActive = true;
            inventorySubMenu.CurrentTab = previousTab;
        }

        Update();
    }

    /// <summary>
    /// Updates the visual selection state of the Bloodcraft tab button.
    /// </summary>
    /// <param name="isActive">Whether the Bloodcraft tab is active.</param>
    static void UpdateButtonSelection(bool isActive)
    {
        if (bloodcraftTabButton == null)
        {
            return;
        }

        bloodcraftTabButton.ForceSelect = isActive;
        bloodcraftTabButton.ForceHighlight = isActive;
    }

    /// <summary>
    /// Ensures only the Bloodcraft tab content is visible when active.
    /// </summary>
    static void ApplyTabVisibility()
    {
        if (inventorySubMenu == null)
        {
            return;
        }

        Il2CppReferenceArray<RectTransform> tabs = inventorySubMenu.Tabs;
        if (tabs == null)
        {
            return;
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            RectTransform tab = tabs[i];
            if (tab == null)
            {
                continue;
            }

            tab.gameObject.SetActive(i == bloodcraftTabIndex);
        }
    }

    /// <summary>
    /// Updates the Bloodcraft entry list based on the active sub-tab.
    /// </summary>
    static void UpdateEntries()
    {
        if (entriesRoot == null || entryTemplate == null)
        {
            return;
        }

        List<BloodcraftEntry> entriesToApply = BuildEntries();
        EnsureEntries(entriesToApply.Count);

        for (int i = 0; i < entriesToApply.Count; i++)
        {
            BloodcraftEntry entry = entriesToApply[i];
            TextMeshProUGUI text = entries[i];
            text.text = entry.Text;
            text.fontStyle = entry.Style;
            text.color = Color.white;

            if (entry.Action != null)
            {
                ConfigureActionButton(entryButtons[i], entry.Action, entry.Enabled);
            }
            else
            {
                ConfigureCommandButton(entryButtons[i], entry.Command, entry.Enabled);
            }
        }
    }

    /// <summary>
    /// Builds the list of entries to render in the Bloodcraft tab.
    /// </summary>
    /// <returns>A list of entries for the current view.</returns>
    static List<BloodcraftEntry> BuildEntries()
    {
        List<BloodcraftEntry> list = [];

        list.Add(new BloodcraftEntry("Bloodcraft", FontStyles.Bold));
        list.Add(new BloodcraftEntry("Select a tab.", FontStyles.Normal));

        for (int i = 0; i < TabOrder.Count; i++)
        {
            TabType tab = TabOrder[i];
            TabType capturedTab = tab;
            string label = TabLabels.TryGetValue(tab, out string tabLabel) ? tabLabel : tab.ToString();
            FontStyles style = activeTab == tab ? FontStyles.Bold : FontStyles.Normal;

            list.Add(new BloodcraftEntry($"{i + 1} | {label}", style, action: () => activeTab = capturedTab, enabled: true));
        }

        list.Add(new BloodcraftEntry(string.Empty, FontStyles.Normal));

        switch (activeTab)
        {
            case TabType.Exoform:
                AppendExoFormEntries(list);
                break;
            case TabType.Battles:
                AppendFamiliarBattleEntries(list);
                break;
            default:
                AppendPrestigeEntries(list);
                break;
        }

        return list;
    }

    /// <summary>
    /// Appends prestige leaderboard entries to the list.
    /// </summary>
    /// <param name="list">The list to populate.</param>
    static void AppendPrestigeEntries(List<BloodcraftEntry> list)
    {
        list.Add(new BloodcraftEntry("Prestige Leaderboard", FontStyles.Bold));

        if (!_prestigeDataReady)
        {
            list.Add(new BloodcraftEntry("Awaiting prestige data...", FontStyles.Normal));
            return;
        }

        if (!_prestigeSystemEnabled)
        {
            list.Add(new BloodcraftEntry("Prestige system disabled.", FontStyles.Normal));
            return;
        }

        if (!_prestigeLeaderboardEnabled)
        {
            list.Add(new BloodcraftEntry("Prestige leaderboard disabled.", FontStyles.Normal));
            return;
        }

        if (_prestigeLeaderboardOrder.Count == 0)
        {
            list.Add(new BloodcraftEntry("No prestige data available.", FontStyles.Normal));
            return;
        }

        if (_prestigeLeaderboardIndex >= _prestigeLeaderboardOrder.Count)
        {
            _prestigeLeaderboardIndex = 0;
        }

        string typeKey = _prestigeLeaderboardOrder[_prestigeLeaderboardIndex];
        string displayType = SplitPascalCase(typeKey);
        _prestigeLeaderboards.TryGetValue(typeKey, out List<PrestigeLeaderboardEntry> leaderboard);
        leaderboard ??= [];

        list.Add(new BloodcraftEntry("Click type to cycle.", FontStyles.Normal));
        list.Add(new BloodcraftEntry($"Type: {displayType}", FontStyles.Bold, action: CyclePrestigeType, enabled: _prestigeLeaderboardOrder.Count > 1));

        if (leaderboard.Count == 0)
        {
            list.Add(new BloodcraftEntry("No prestige entries yet.", FontStyles.Normal));
            return;
        }

        for (int i = 0; i < leaderboard.Count; i++)
        {
            PrestigeLeaderboardEntry entry = leaderboard[i];
            FontStyles style = i == 0 ? FontStyles.Bold : FontStyles.Normal;
            list.Add(new BloodcraftEntry($"{i + 1} | {entry.Name}: {entry.Value}", style));
        }
    }

    /// <summary>
    /// Appends exoform and shapeshift entries to the list.
    /// </summary>
    /// <param name="list">The list to populate.</param>
    static void AppendExoFormEntries(List<BloodcraftEntry> list)
    {
        list.Add(new BloodcraftEntry("Exoforms", FontStyles.Bold));

        if (!_exoFormDataReady)
        {
            list.Add(new BloodcraftEntry("Awaiting exoform data...", FontStyles.Normal));
            return;
        }

        if (!_exoFormEnabled)
        {
            list.Add(new BloodcraftEntry("Exo prestiging disabled.", FontStyles.Normal));
            return;
        }

        string currentForm = string.IsNullOrWhiteSpace(_exoFormCurrentForm)
            ? "None"
            : SplitPascalCase(_exoFormCurrentForm);

        list.Add(new BloodcraftEntry($"Current: {currentForm}", FontStyles.Normal));

        bool canToggleTaunt = _exoFormPrestiges > 0;
        string chargeLine = _exoFormMaxDuration > 0f
            ? $"Charge: {_exoFormCharge:0.0}/{_exoFormMaxDuration:0.0}s"
            : "Charge: --";

        string tauntStatus = _exoFormTauntEnabled ? "<color=green>On</color>" : "<color=red>Off</color>";

        list.Add(new BloodcraftEntry($"Exo Prestiges: {_exoFormPrestiges}", FontStyles.Normal));
        list.Add(new BloodcraftEntry(chargeLine, FontStyles.Normal));
        list.Add(new BloodcraftEntry($"Taunt to Exoform: {tauntStatus}", FontStyles.Normal, command: ".prestige exoform", enabled: canToggleTaunt));
        list.Add(new BloodcraftEntry("Forms", FontStyles.Bold));

        for (int i = 0; i < _exoFormEntries.Count; i++)
        {
            ExoFormEntry form = _exoFormEntries[i];
            string formName = SplitPascalCase(form.FormName);
            string status = form.Unlocked ? "Unlocked" : "Locked";
            FontStyles style = form.FormName.Equals(_exoFormCurrentForm, StringComparison.OrdinalIgnoreCase)
                ? FontStyles.Bold
                : FontStyles.Normal;

            list.Add(new BloodcraftEntry($"{i + 1} | {formName} ({status})", style,
                command: $".prestige sf {form.FormName}", enabled: form.Unlocked));
        }

        ExoFormEntry activeForm = ResolveActiveExoForm();
        if (activeForm != null && activeForm.Abilities.Count > 0)
        {
            list.Add(new BloodcraftEntry("Abilities", FontStyles.Bold));

            foreach (ExoFormAbilityData ability in activeForm.Abilities)
            {
                string abilityName = ResolveAbilityName(ability.AbilityId);
                list.Add(new BloodcraftEntry($" - {abilityName} ({ability.Cooldown:0.0}s)", FontStyles.Normal));
            }
        }
    }

    /// <summary>
    /// Appends familiar battle group entries to the list.
    /// </summary>
    /// <param name="list">The list to populate.</param>
    static void AppendFamiliarBattleEntries(List<BloodcraftEntry> list)
    {
        list.Add(new BloodcraftEntry("Familiar Battles", FontStyles.Bold));

        if (!_familiarBattleDataReady)
        {
            list.Add(new BloodcraftEntry("Awaiting familiar battle data...", FontStyles.Normal));
            return;
        }

        if (!_familiarSystemEnabled)
        {
            list.Add(new BloodcraftEntry("Familiars are disabled.", FontStyles.Normal));
            return;
        }

        if (!_familiarBattlesEnabled)
        {
            list.Add(new BloodcraftEntry("Familiar battles disabled.", FontStyles.Normal));
            return;
        }

        string activeGroupName = string.IsNullOrWhiteSpace(_familiarActiveBattleGroup)
            ? "None"
            : _familiarActiveBattleGroup;

        list.Add(new BloodcraftEntry($"Active group: {activeGroupName}", FontStyles.Normal));

        if (_familiarBattleGroups.Count == 0)
        {
            list.Add(new BloodcraftEntry("No battle groups available.", FontStyles.Normal));
            return;
        }

        list.Add(new BloodcraftEntry("Groups", FontStyles.Bold));

        for (int i = 0; i < _familiarBattleGroups.Count; i++)
        {
            FamiliarBattleGroupData group = _familiarBattleGroups[i];
            bool isActive = group.Name.Equals(_familiarActiveBattleGroup, StringComparison.OrdinalIgnoreCase);
            FontStyles style = isActive ? FontStyles.Bold : FontStyles.Normal;
            string suffix = isActive ? " (Active)" : string.Empty;
            list.Add(new BloodcraftEntry($"{i + 1} | {group.Name}{suffix}", style,
                command: $".fam cbg {group.Name}", enabled: true));
        }

        FamiliarBattleGroupData activeGroup = FindBattleGroup(_familiarActiveBattleGroup) ?? _familiarBattleGroups[0];
        list.Add(new BloodcraftEntry("Slots", FontStyles.Bold));

        for (int i = 0; i < activeGroup.Slots.Count; i++)
        {
            FamiliarBattleSlotData slot = activeGroup.Slots[i];
            string slotText = FormatFamiliarSlot(slot, i + 1);
            list.Add(new BloodcraftEntry(slotText, FontStyles.Normal, command: $".fam sbg {i + 1}", enabled: true));
        }
    }

    /// <summary>
    /// Cycles the selected prestige leaderboard type.
    /// </summary>
    static void CyclePrestigeType()
    {
        if (_prestigeLeaderboardOrder.Count == 0)
        {
            return;
        }

        _prestigeLeaderboardIndex = (_prestigeLeaderboardIndex + 1) % _prestigeLeaderboardOrder.Count;
    }

    /// <summary>
    /// Resolves the active exoform entry.
    /// </summary>
    /// <returns>The active exoform entry or null if none is available.</returns>
    static ExoFormEntry ResolveActiveExoForm()
    {
        if (_exoFormEntries.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_exoFormCurrentForm))
        {
            ExoFormEntry match = _exoFormEntries.Find(entry =>
                entry.FormName.Equals(_exoFormCurrentForm, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }
        }

        return _exoFormEntries[0];
    }

    /// <summary>
    /// Resolves a display name for an ability identifier.
    /// </summary>
    /// <param name="abilityId">The ability prefab GUID hash.</param>
    /// <returns>The display-friendly ability name.</returns>
    static string ResolveAbilityName(int abilityId)
    {
        PrefabGUID abilityGuid = new(abilityId);
        string abilityName = abilityGuid.GetLocalizedName();
        if (string.IsNullOrEmpty(abilityName) || abilityName.Equals("LocalizationKey.Empty"))
        {
            abilityName = abilityGuid.GetPrefabName();
        }

        if (string.IsNullOrEmpty(abilityName))
        {
            return $"Ability {abilityId}";
        }

        Match match = AbilitySpellRegex.Match(abilityName);
        if (match.Success)
        {
            return match.Value.Replace('_', ' ');
        }

        return abilityName;
    }

    /// <summary>
    /// Finds a familiar battle group by name.
    /// </summary>
    /// <param name="groupName">The group name to match.</param>
    /// <returns>The battle group or null if not found.</returns>
    static FamiliarBattleGroupData FindBattleGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            return null;
        }

        return _familiarBattleGroups.Find(group =>
            group.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Formats a familiar battle slot display string.
    /// </summary>
    /// <param name="slot">The slot data.</param>
    /// <param name="slotIndex">The 1-based slot index.</param>
    /// <returns>A formatted slot string.</returns>
    static string FormatFamiliarSlot(FamiliarBattleSlotData slot, int slotIndex)
    {
        if (slot.Id == 0)
        {
            return $"Slot {slotIndex}: <color=grey>Empty</color>";
        }

        string name = string.IsNullOrWhiteSpace(slot.Name) ? $"Familiar {slot.Id}" : slot.Name;
        string prestige = slot.Prestige > 0 ? $" P{slot.Prestige}" : string.Empty;
        return $"Slot {slotIndex}: {name} (Lv {slot.Level}{prestige})";
    }

    /// <summary>
    /// Ensures the entry list has enough UI objects for the requested count.
    /// </summary>
    /// <param name="count">The number of entries required.</param>
    static void EnsureEntries(int count)
    {
        if (entriesRoot == null || entryTemplate == null)
        {
            return;
        }

        while (entries.Count < count)
        {
            int index = entries.Count;
            GameObject entryObject = UnityEngine.Object.Instantiate(entryTemplate, entriesRoot, false);
            entryObject.name = $"BloodcraftEntry_{index + 1}";
            entryObject.SetActive(true);

            TextMeshProUGUI text = entryObject.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                UnityEngine.Object.Destroy(entryObject);
                return;
            }

            SimpleStunButton button = entryObject.GetComponent<SimpleStunButton>() ?? entryObject.AddComponent<SimpleStunButton>();

            entries.Add(text);
            entryButtons.Add(button);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            bool isActive = i < count;
            entries[i].gameObject.SetActive(isActive);
        }
    }

    /// <summary>
    /// Configures a button to send a chat command when clicked.
    /// </summary>
    /// <param name="button">The button to configure.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="enabled">Whether the command is enabled.</param>
    static void ConfigureCommandButton(SimpleStunButton button, string command, bool enabled)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();

        if (!enabled || string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        button.onClick.AddListener((UnityAction)(() => Quips.SendCommand(command)));
    }

    /// <summary>
    /// Configures a button to execute a local action when clicked.
    /// </summary>
    /// <param name="button">The button to configure.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="enabled">Whether the action is enabled.</param>
    static void ConfigureActionButton(SimpleStunButton button, Action action, bool enabled)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();

        if (!enabled || action == null)
        {
            return;
        }

        button.onClick.AddListener((UnityAction)(() => action()));
    }

    /// <summary>
    /// Represents a single line entry in the Bloodcraft tab.
    /// </summary>
    readonly struct BloodcraftEntry
    {
        public string Text { get; }
        public string Command { get; }
        public Action Action { get; }
        public bool Enabled { get; }
        public FontStyles Style { get; }

        /// <summary>
        /// Initializes a new Bloodcraft entry.
        /// </summary>
        /// <param name="text">The display text for the entry.</param>
        /// <param name="style">The font style to apply.</param>
        /// <param name="command">An optional chat command to send when clicked.</param>
        /// <param name="action">An optional local action to invoke when clicked.</param>
        /// <param name="enabled">Whether the entry is clickable.</param>
        public BloodcraftEntry(string text, FontStyles style, string command = "", Action action = null, bool enabled = false)
        {
            Text = text;
            Command = command;
            Action = action;
            Enabled = enabled;
            Style = style;
        }
    }
}

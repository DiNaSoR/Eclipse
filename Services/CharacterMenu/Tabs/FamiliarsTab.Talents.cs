using Eclipse.Services.CharacterMenu.Shared;
using Eclipse.Utilities;
using ProjectM.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Eclipse.Services.CanvasService.DataHUD;
using static Eclipse.Services.DataService;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Partial class for FamiliarsTab containing Talents panel functionality.
/// Implements a Path of Exile-style talent tree for familiars.
/// </summary>
internal partial class FamiliarsTab
{
    #region Talent Constants

    private const float TalentNodeSize = 36f;
    private const float TalentKeystoneSize = 48f;
    private const float TalentNodeSpacing = 12f;
    private const float TalentConnectionWidth = 2f;
    private const float TalentTreePadding = 16f;

    private static readonly Color TalentNodeAllocatedColor = new(0.35f, 0.75f, 0.35f, 1f);
    private static readonly Color TalentNodeAvailableColor = new(0.7f, 0.6f, 0.2f, 1f);
    private static readonly Color TalentNodeLockedColor = new(0.3f, 0.3f, 0.35f, 0.7f);
    private static readonly Color TalentNodeKeystoneColor = new(0.8f, 0.2f, 0.2f, 1f);
    private static readonly Color TalentConnectionAllocatedColor = new(0.35f, 0.75f, 0.35f, 0.8f);
    private static readonly Color TalentConnectionLockedColor = new(0.4f, 0.4f, 0.45f, 0.5f);

    private static readonly string[] TalentNodeSpriteNames = ["IconBackground", "spell_icon"];
    private static readonly string[] TalentKeystoneSpriteNames = ["MobLevel_Skull", "IconBackground"];

    #endregion

    #region Talent UI Classes

    private class TalentNodeUI
    {
        public int TalentId { get; set; }
        public RectTransform Root { get; set; }
        public Image Background { get; set; }
        public Image Icon { get; set; }
        public TextMeshProUGUI Label { get; set; }
        public SimpleStunButton Button { get; set; }
        public bool IsAllocated { get; set; }
        public bool IsAvailable { get; set; }
    }

    #endregion

    #region Talent Panel Creation

    private void CreateFamiliarTalentsPanel(Transform parent, TextMeshProUGUI reference)
    {
        if (parent == null) return;

        RectTransform card = CreateFamiliarCard(parent, "FamiliarTalentsCard", stretchHeight: false);
        if (card == null) return;

        _ = CreateFamiliarSectionLabel(card, reference, "Talent Tree", FamiliarHeaderDefaultIconSpriteNames);

        // Talent points header
        _talentPointsText = CreateFamiliarText(card, reference, "Talent Points: 0 / 0",
            FamiliarMetaFontScale, FontStyles.Normal, TextAlignmentOptions.Left, FamiliarMetaColor);

        // Create the talent tree container with horizontal layout for 3 paths
        RectTransform treeContainer = CreateRectTransformObject("TalentTreeContainer", card);
        if (treeContainer != null)
        {
            treeContainer.anchorMin = new Vector2(0f, 1f);
            treeContainer.anchorMax = new Vector2(1f, 1f);
            treeContainer.pivot = new Vector2(0f, 1f);
            treeContainer.offsetMin = Vector2.zero;
            treeContainer.offsetMax = Vector2.zero;

            Image bg = treeContainer.gameObject.AddComponent<Image>();
            ApplySprite(bg, FamiliarCardSpriteNames);
            bg.color = new Color(0f, 0f, 0f, 0.25f);
            bg.raycastTarget = false;

            // Use HorizontalLayoutGroup for the 3 paths side by side
            HorizontalLayoutGroup hLayout = treeContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.UpperCenter;
            hLayout.spacing = 8f;
            hLayout.padding = CreatePadding(8, 8, 8, 8);
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = false;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;

            // Fixed height for the tree container
            LayoutElement treeLayout = treeContainer.gameObject.AddComponent<LayoutElement>();
            treeLayout.minHeight = 280f;
            treeLayout.preferredHeight = 280f;

            _talentTreeRoot = treeContainer;

            // Create the 3 paths
            CreateTalentPath(treeContainer, reference, "Speed", new[] {
                (1, "Speed I", false),
                (2, "Speed II", false),
                (3, "Swift Strike", false),
                (4, "Berserker", true)
            });

            CreateTalentPath(treeContainer, reference, "Power", new[] {
                (10, "Power I", false),
                (11, "Power II", false),
                (12, "Brutal Force", false),
                (13, "Enrage", true)
            });

            CreateTalentPath(treeContainer, reference, "Vitality", new[] {
                (20, "Vitality I", false),
                (21, "Vitality II", false),
                (22, "Fortitude", false),
                (23, "Juggernaut", true)
            });
        }

        // Reset talents button - create directly as an action row, not in a list
        RectTransform resetRow = CreateRectTransformObject("ResetTalentsRow", card);
        if (resetRow != null)
        {
            resetRow.anchorMin = new Vector2(0f, 1f);
            resetRow.anchorMax = new Vector2(1f, 1f);
            resetRow.pivot = new Vector2(0f, 1f);
            resetRow.offsetMin = Vector2.zero;
            resetRow.offsetMax = Vector2.zero;

            LayoutElement resetLayout = resetRow.gameObject.AddComponent<LayoutElement>();
            resetLayout.minHeight = 32f;
            resetLayout.preferredHeight = 32f;

            Image resetBg = resetRow.gameObject.AddComponent<Image>();
            ApplySprite(resetBg, FamiliarRowSpriteNames);
            resetBg.color = new Color(0.5f, 0.1f, 0.1f, 0.5f);
            resetBg.raycastTarget = true;

            HorizontalLayoutGroup resetHLayout = resetRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            resetHLayout.childAlignment = TextAnchor.MiddleCenter;
            resetHLayout.padding = CreatePadding(10, 10, 4, 4);
            resetHLayout.childForceExpandWidth = true;
            resetHLayout.childForceExpandHeight = false;
            resetHLayout.childControlWidth = true;
            resetHLayout.childControlHeight = true;

            TextMeshProUGUI resetText = CreateFamiliarText(resetRow, reference, "Reset Talents",
                FamiliarActionFontScale, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);

            SimpleStunButton resetButton = resetRow.gameObject.AddComponent<SimpleStunButton>();
            ConfigureCommandButton(resetButton, ".fam talent reset", true);
        }
    }

    private void CreateTalentPath(Transform parent, TextMeshProUGUI reference, string pathName,
        (int id, string name, bool isKeystone)[] talents)
    {
        RectTransform pathContainer = CreateRectTransformObject($"TalentPath_{pathName}", parent);
        if (pathContainer == null) return;

        pathContainer.anchorMin = new Vector2(0f, 1f);
        pathContainer.anchorMax = new Vector2(0f, 1f);
        pathContainer.pivot = new Vector2(0.5f, 1f);

        // Vertical layout for nodes in this path
        VerticalLayoutGroup vLayout = pathContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.spacing = 4f;
        vLayout.padding = CreatePadding(4, 4, 4, 4);
        vLayout.childForceExpandWidth = false;
        vLayout.childForceExpandHeight = false;
        vLayout.childControlWidth = false;
        vLayout.childControlHeight = false;

        LayoutElement pathLayout = pathContainer.gameObject.AddComponent<LayoutElement>();
        pathLayout.flexibleWidth = 1f;

        // Path label at top
        TextMeshProUGUI pathLabel = CreateFamiliarText(pathContainer, reference, pathName,
            FamiliarMetaFontScale * 0.9f, FontStyles.Bold, TextAlignmentOptions.Center, FamiliarMetaColor);
        if (pathLabel != null)
        {
            LayoutElement labelLayout = pathLabel.gameObject.AddComponent<LayoutElement>();
            labelLayout.minHeight = 18f;
            labelLayout.preferredHeight = 18f;
            labelLayout.preferredWidth = 80f;
        }

        // Create talent nodes
        foreach (var talent in talents)
        {
            TalentNodeUI nodeUI = CreateTalentNode(pathContainer, reference, talent.id, talent.name,
                Vector2.zero, talent.isKeystone ? TalentKeystoneSize : TalentNodeSize, talent.isKeystone);

            if (nodeUI != null)
            {
                _talentNodes.Add(nodeUI);
            }
        }
    }

    private TalentNodeUI CreateTalentNode(Transform parent, TextMeshProUGUI reference, 
        int talentId, string name, Vector2 position, float size, bool isKeystone)
    {
        // Create a container for the node + label to work with VerticalLayoutGroup
        RectTransform nodeContainer = CreateRectTransformObject($"TalentNodeContainer_{talentId}", parent);
        if (nodeContainer == null) return null;

        // Use layout element for proper sizing within layout groups
        LayoutElement containerLayout = nodeContainer.gameObject.AddComponent<LayoutElement>();
        containerLayout.minWidth = size + 24f;  // Extra width for label
        containerLayout.preferredWidth = size + 24f;
        containerLayout.minHeight = size + 18f;  // Extra height for label below
        containerLayout.preferredHeight = size + 18f;

        // Nested vertical layout for node icon + label
        VerticalLayoutGroup containerVLayout = nodeContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        containerVLayout.childAlignment = TextAnchor.UpperCenter;
        containerVLayout.spacing = 2f;
        containerVLayout.childForceExpandWidth = false;
        containerVLayout.childForceExpandHeight = false;
        containerVLayout.childControlWidth = false;
        containerVLayout.childControlHeight = false;

        // The actual node icon/button
        RectTransform nodeRect = CreateRectTransformObject($"TalentNode_{talentId}", nodeContainer);
        if (nodeRect == null) return null;

        LayoutElement nodeLayout = nodeRect.gameObject.AddComponent<LayoutElement>();
        nodeLayout.minWidth = size;
        nodeLayout.preferredWidth = size;
        nodeLayout.minHeight = size;
        nodeLayout.preferredHeight = size;

        // Background
        Image bg = nodeRect.gameObject.AddComponent<Image>();
        ApplySprite(bg, isKeystone ? TalentKeystoneSpriteNames : TalentNodeSpriteNames);
        bg.color = TalentNodeLockedColor;
        bg.raycastTarget = true;

        // Button
        SimpleStunButton button = nodeRect.gameObject.AddComponent<SimpleStunButton>();
        int capturedId = talentId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener((UnityAction)(() => OnTalentNodeClicked(capturedId)));

        // Label below the node
        TextMeshProUGUI label = CreateFamiliarText(nodeContainer, reference, name,
            FamiliarMetaFontScale * 0.7f, FontStyles.Normal, TextAlignmentOptions.Center, FamiliarMetaColor);
        if (label != null)
        {
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.minHeight = 14f;
            labelLayout.preferredHeight = 14f;
            labelLayout.preferredWidth = size + 20f;
        }

        return new TalentNodeUI
        {
            TalentId = talentId,
            Root = nodeRect,
            Background = bg,
            Label = label,
            Button = button,
            IsAllocated = false,
            IsAvailable = false
        };
    }

    #endregion

    #region Talent Panel Updates

    private void UpdateFamiliarTalentsPanel()
    {
        if (_talentTreeRoot == null) return;

        // Check if we have an active familiar
        string displayName = ResolveFamiliarDisplayName();
        bool hasFamiliar = !displayName.Equals("None", StringComparison.OrdinalIgnoreCase);

        if (!hasFamiliar)
        {
            if (_talentPointsText != null)
            {
                _talentPointsText.text = "No familiar bound - bind a familiar to allocate talents";
            }

            foreach (var node in _talentNodes)
            {
                if (node?.Background != null)
                {
                    node.Background.color = TalentNodeLockedColor;
                }
            }
            return;
        }

        // Calculate available talent points (1 per 10 levels + 2 per prestige)
        int availablePoints = _familiarLevel / 10 + (_familiarPrestige * 2);
        int spentPoints = CalculateSpentTalentPoints();
        int remainingPoints = availablePoints - spentPoints;

        if (_talentPointsText != null)
        {
            _talentPointsText.text = $"Talent Points: {remainingPoints} available ({spentPoints}/{availablePoints} spent) | {displayName} Lv.{_familiarLevel}";
        }

        // Update node visuals based on allocation state
        UpdateTalentNodeVisuals(remainingPoints);
    }

    private int CalculateSpentTalentPoints()
    {
        // This would query the allocated talents from server data
        // For now, return 0 as placeholder until data sync is implemented
        return 0;
    }

    private void UpdateTalentNodeVisuals(int remainingPoints)
    {
        // Get allocated talents from cache/data
        List<int> allocatedTalents = GetAllocatedTalents();

        foreach (var node in _talentNodes)
        {
            if (node?.Background == null) continue;

            bool isAllocated = allocatedTalents.Contains(node.TalentId);
            bool isAvailable = remainingPoints > 0 && CanAllocateTalent(node.TalentId, allocatedTalents);

            node.IsAllocated = isAllocated;
            node.IsAvailable = isAvailable;

            if (isAllocated)
            {
                node.Background.color = TalentNodeAllocatedColor;
            }
            else if (isAvailable)
            {
                node.Background.color = TalentNodeAvailableColor;
            }
            else
            {
                node.Background.color = TalentNodeLockedColor;
            }
        }
    }

    private List<int> GetAllocatedTalents()
    {
        // Placeholder - would be populated from server data
        return [];
    }

    private bool CanAllocateTalent(int talentId, List<int> allocatedTalents)
    {
        // Define prerequisites (matching server-side FamiliarTalentSystem)
        var prerequisites = new Dictionary<int, List<int>>
        {
            { 1, [] }, { 2, [1] }, { 3, [2] }, { 4, [3] },
            { 10, [] }, { 11, [10] }, { 12, [11] }, { 13, [12] },
            { 20, [] }, { 21, [20] }, { 22, [21] }, { 23, [22] },
        };

        if (!prerequisites.TryGetValue(talentId, out var prereqs))
            return false;

        if (allocatedTalents.Contains(talentId))
            return false;

        return prereqs.All(p => allocatedTalents.Contains(p));
    }

    private void OnTalentNodeClicked(int talentId)
    {
        // Send command to server to allocate talent
        string command = $".fam talent allocate {talentId}";
        Core.Log.LogInfo($"[FamiliarsTab] Talent node clicked: {talentId}, sending command: {command}");

        // Send command using the same pattern as other familiar actions
        if (!string.IsNullOrEmpty(command))
        {
            Quips.SendCommand(command);
        }
    }

    #endregion
}

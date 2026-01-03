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
using UnityEngine.EventSystems;
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

    private class TalentDef
    {
        public string Name;
        public string Description;
        public string Effect;
    }

    private TextMeshProUGUI _infoTitleText;
    private TextMeshProUGUI _infoDescText;
    private TextMeshProUGUI _infoEffectText;

    private readonly Dictionary<int, TalentDef> _talentDefs = new()
    {
        { 1, new() { Name = "Speed I", Description = "+5% Movement Speed" } },
        { 2, new() { Name = "Speed II", Description = "+10% Movement Speed" } },
        { 3, new() { Name = "Swift Strike", Description = "+25% Attack Speed" } },
        { 4, new() { Name = "Berserker", Description = "+50% Attack Speed, +30% Move Speed", Effect = "Effect: Dark Red Aura, -15% Defense" } },
        
        { 10, new() { Name = "Power I", Description = "+5% Physical & Spell Power" } },
        { 11, new() { Name = "Power II", Description = "+10% Physical & Spell Power" } },
        { 12, new() { Name = "Brutal Force", Description = "+25% All Damage" } },
        { 13, new() { Name = "Enrage", Description = "+100% Damage below 50% HP", Effect = "Effect: Enrage Aura, -25% Max Health" } },

        { 20, new() { Name = "Vitality I", Description = "+5% Max Health" } },
        { 21, new() { Name = "Vitality II", Description = "+10% Max Health" } },
        { 22, new() { Name = "Fortitude", Description = "+25% Damage Reduction" } },
        { 23, new() { Name = "Juggernaut", Description = "+100% Health, Immune to Stuns", Effect = "Effect: Iron Skin, -30% Speed" } }
    };

    #endregion

    #region Talent Panel Creation

    private void CreateFamiliarTalentsPanel(Transform parent, TextMeshProUGUI reference)
    {
        if (parent == null) return;

        RectTransform card = CreateFamiliarCard(parent, "FamiliarTalentsCard", stretchHeight: false);
        if (card == null) return;

        // Ensure the card has a VerticalLayoutGroup to stack Header, Tree, Info, and Footer
        VerticalLayoutGroup cardLayout = card.GetComponent<VerticalLayoutGroup>();
        if (cardLayout == null) cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        cardLayout.childAlignment = TextAnchor.UpperCenter;
        cardLayout.childControlHeight = true;
        cardLayout.childControlWidth = true;
        cardLayout.childForceExpandHeight = false;
        cardLayout.childForceExpandWidth = true;
        cardLayout.spacing = 8f;
        cardLayout.padding = CreatePadding(16, 16, 16, 16);

        _ = CreateFamiliarSectionLabel(card, reference, "Familiar Talent Tree", FamiliarHeaderDefaultIconSpriteNames);

        // Talent points header
        _talentPointsText = CreateFamiliarText(card, reference, "Talent Points: 0 / 0",
            FamiliarMetaFontScale, FontStyles.Normal, TextAlignmentOptions.Left, FamiliarMetaColor);

        // Create the talent tree container with horizontal layout for 3 paths
        RectTransform treeContainer = CreateRectTransformObject("TalentTreeContainer", card);
        if (treeContainer != null)
        {
            // Background for the tree area
            Image bg = treeContainer.gameObject.AddComponent<Image>();
            ApplySprite(bg, FamiliarCardSpriteNames);
            bg.color = new Color(0f, 0f, 0f, 0.3f);
            bg.raycastTarget = false;

            // Horizontal layout for the 3 paths (Speed, Power, Vitality)
            HorizontalLayoutGroup hLayout = treeContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.UpperCenter;
            hLayout.spacing = 20f; 
            hLayout.padding = CreatePadding(20, 20, 20, 20); 
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = true;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;

            // Height for the tree container
            LayoutElement treeLayout = treeContainer.gameObject.AddComponent<LayoutElement>();
            treeLayout.minHeight = 350f;     
            treeLayout.preferredHeight = 350f;
            treeLayout.flexibleHeight = 0f;

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
                (20, "Vitality II", false), // Corrected IDs from prev version
                (21, "Vitality II", false),
                (22, "Fortitude", false),
                (23, "Juggernaut", true)
            });
        }

        CreateSpacer(card, 5f);

        // --- Talent Info Panel ---
        RectTransform infoPanel = CreateRectTransformObject("TalentInfoPanel", card);
        LayoutElement infoLayout = infoPanel.gameObject.AddComponent<LayoutElement>();
        infoLayout.minHeight = 80f;
        infoLayout.preferredHeight = 80f;
        infoLayout.flexibleWidth = 1f;

        // Background for info
        Image infoBg = infoPanel.gameObject.AddComponent<Image>();
        ApplySprite(infoBg, FamiliarCardSpriteNames);
        infoBg.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

        VerticalLayoutGroup infoVLayout = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        infoVLayout.padding = CreatePadding(10, 10, 5, 5);
        infoVLayout.spacing = 2f;
        infoVLayout.childControlHeight = true;
        infoVLayout.childControlWidth = true;
        infoVLayout.childForceExpandHeight = false;

        _infoTitleText = CreateFamiliarText(infoPanel, reference, "Hover a Talent", FamiliarMetaFontScale, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        _infoDescText = CreateFamiliarText(infoPanel, reference, "See details here.", FamiliarMetaFontScale * 0.9f, FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.8f, 0.8f, 0.8f));
        _infoEffectText = CreateFamiliarText(infoPanel, reference, "", FamiliarMetaFontScale * 0.8f, FontStyles.Italic, TextAlignmentOptions.Left, new Color(0.6f, 0.6f, 1f));

        CreateSpacer(card, 5f);

        // Users might want the Reset button at the bottom
        RectTransform resetRow = CreateRectTransformObject("ResetTalentsRow", card);
        if (resetRow != null)
        {
            LayoutElement resetLayout = resetRow.gameObject.AddComponent<LayoutElement>();
            resetLayout.minHeight = 36f;
            resetLayout.preferredHeight = 36f;
            resetLayout.flexibleWidth = 1f;

            Image resetBg = resetRow.gameObject.AddComponent<Image>();
            ApplySprite(resetBg, FamiliarRowSpriteNames);
            resetBg.color = new Color(0.6f, 0.1f, 0.1f, 0.8f);
            resetBg.raycastTarget = true;

            HorizontalLayoutGroup resetHLayout = resetRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            resetHLayout.childAlignment = TextAnchor.MiddleCenter;
            resetHLayout.padding = CreatePadding(10, 10, 4, 4);
            resetHLayout.childControlWidth = true;
            resetHLayout.childControlHeight = true;

            CreateFamiliarText(resetRow, reference, "Reset Talents",
                FamiliarActionFontScale, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            SimpleStunButton resetButton = resetRow.gameObject.AddComponent<SimpleStunButton>();
            ConfigureCommandButton(resetButton, ".fam talent reset", true);
        }
    }

    private void CreateTalentPath(Transform parent, TextMeshProUGUI reference, string pathName,
        (int id, string name, bool isKeystone)[] talents)
    {
        RectTransform pathContainer = CreateRectTransformObject($"TalentPath_{pathName}", parent);
        if (pathContainer == null) return;

        // Vertical layout for nodes in this path
        VerticalLayoutGroup vLayout = pathContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.spacing = 0f; // Spacing handled by connection lines
        vLayout.padding = CreatePadding(0, 0, 10, 10);
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;

        // Path label at top
        TextMeshProUGUI pathLabel = CreateFamiliarText(pathContainer, reference, pathName,
            FamiliarMetaFontScale, FontStyles.Bold, TextAlignmentOptions.Center, new Color(0.9f, 0.8f, 0.6f));
        
        if (pathLabel != null)
        {
            LayoutElement labelLayout = pathLabel.gameObject.AddComponent<LayoutElement>();
            labelLayout.minHeight = 24f;
            labelLayout.preferredHeight = 24f;
            
            // Spacer after label
            CreateSpacer(pathContainer, 10f);
        }

        // Create talent nodes with connections
        for (int i = 0; i < talents.Length; i++)
        {
            var talent = talents[i];
            
            // Create connection line BEFORE the node (except the first one)
            if (i > 0)
            {
                CreateConnectionLine(pathContainer, 25f);
            }

            TalentNodeUI nodeUI = CreateTalentNode(pathContainer, reference, talent.id, talent.name,
                Vector2.zero, talent.isKeystone ? TalentKeystoneSize : TalentNodeSize, talent.isKeystone);

            if (nodeUI != null)
            {
                _talentNodes.Add(nodeUI);
            }
        }
    }

    private void CreateConnectionLine(Transform parent, float height)
    {
        RectTransform line = CreateRectTransformObject("ConnectionLine", parent);
        if (line == null) return;

        LayoutElement layout = line.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.minWidth = TalentConnectionWidth;
        layout.preferredWidth = TalentConnectionWidth;
        
        Image img = line.gameObject.AddComponent<Image>();
        // Use a simple white sprite (or pixel) that we can tint
        // Since we don't have a guaranteed "pixel" sprite, we use IconBackground and squash it
        ApplySprite(img, TalentNodeSpriteNames); 
        img.color = TalentConnectionLockedColor;
    }

    private void CreateSpacer(Transform parent, float height)
    {
        RectTransform spacer = CreateRectTransformObject("Spacer", parent);
        LayoutElement layout = spacer.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private TalentNodeUI CreateTalentNode(Transform parent, TextMeshProUGUI reference, 
        int talentId, string name, Vector2 position, float size, bool isKeystone)
    {
        // Container for Node + Label
        RectTransform nodeContainer = CreateRectTransformObject($"TalentNodeContainer_{talentId}", parent);
        if (nodeContainer == null) return null;

        VerticalLayoutGroup containerVLayout = nodeContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        containerVLayout.childAlignment = TextAnchor.UpperCenter;
        containerVLayout.spacing = 4f;
        containerVLayout.childControlWidth = true; // Changed to TRUE to prevent stretching if force expand is false
        containerVLayout.childControlHeight = true;
        containerVLayout.childForceExpandWidth = false;
        containerVLayout.childForceExpandHeight = false;

        // Node Icon/Button
        RectTransform nodeRect = CreateRectTransformObject($"TalentNode_{talentId}", nodeContainer);
        LayoutElement nodeLayout = nodeRect.gameObject.AddComponent<LayoutElement>();
        nodeLayout.minWidth = size;
        nodeLayout.preferredWidth = size;
        nodeLayout.minHeight = size;
        nodeLayout.preferredHeight = size;
        nodeRect.sizeDelta = new Vector2(size, size); // Explicitly size it

        Image bg = nodeRect.gameObject.AddComponent<Image>();
        ApplySprite(bg, isKeystone ? TalentKeystoneSpriteNames : TalentNodeSpriteNames);
        bg.color = TalentNodeLockedColor;
        bg.raycastTarget = true;

        SimpleStunButton button = nodeRect.gameObject.AddComponent<SimpleStunButton>();
        int capturedId = talentId;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener((UnityAction)(() => OnTalentNodeClicked(capturedId)));

        // ADD HOVER (PointerEnter/Exit)
        // Note: EventTrigger requires GraphicRaycaster which Canvas usually has.
        EventTrigger trigger = nodeRect.gameObject.AddComponent<EventTrigger>();
        
        // Enter
        EventTrigger.Entry entryEnter = new() { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((UnityAction<BaseEventData>)delegate { UpdateTalentInfoPanel(capturedId); });
        trigger.triggers.Add(entryEnter);

        // Exit
        EventTrigger.Entry entryExit = new() { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((UnityAction<BaseEventData>)delegate { ClearTalentInfoPanel(); });
        trigger.triggers.Add(entryExit);

        // Label
        TextMeshProUGUI label = CreateFamiliarText(nodeContainer, reference, name,
            FamiliarMetaFontScale * 0.75f, FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));
        
        if (label != null)
        {
            LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.minHeight = 16f;
            labelLayout.preferredHeight = 16f;
            labelLayout.preferredWidth = 100f; // Allow text to be wider than node
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

    private void UpdateTalentInfoPanel(int talentId)
    {
        if (_talentDefs.TryGetValue(talentId, out var def))
        {
            if (_infoTitleText != null) _infoTitleText.text = def.Name;
            if (_infoDescText != null) _infoDescText.text = def.Description;
            if (_infoEffectText != null) _infoEffectText.text = def.Effect ?? "";
        }
    }

    private void ClearTalentInfoPanel()
    {
        if (_infoTitleText != null) _infoTitleText.text = "Hover a Talent";
        if (_infoDescText != null) _infoDescText.text = "See details here.";
        if (_infoEffectText != null) _infoEffectText.text = "";
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
            _talentPointsText.text = $"<color=#FFD700>Talent Points: {remainingPoints}</color> <color=#888>({spentPoints}/{availablePoints} spent)</color> | {displayName} Lv.{_familiarLevel}";
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
                // Update icon to look 'lit up' if we had separate glows
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

using Eclipse.Services.CharacterMenu.Base;
using Eclipse.Services.CharacterMenu.Interfaces;
using Eclipse.Services.CharacterMenu.Shared;
using Eclipse.Services.HUD.Shared;
using Eclipse.Utilities;
using ProjectM.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static Eclipse.Services.DataService;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Character menu tab for displaying and managing weapon stat bonus selections.
/// Panel-based implementation extracted from the legacy CharacterMenuService.
/// </summary>
internal class StatBonusesTab : CharacterMenuTabBase, ICharacterMenuTabWithPanel
{
    #region Constants

    // Header layout (matches Docs/DesignMock/index.html: .stat-header)
    private const float HeaderMinHeight = 44f;
    private const float HeaderIconSize = 32f;
    private const float HeaderSpacing = 10f;
    private const int HeaderPaddingHorizontal = 12;
    private const int HeaderPaddingVertical = 6;

    private const float HeaderNameFontScale = 1.0f; // We'll set exact size relative to reference below
    private const float HeaderMetaFontScale = 0.75f;
    private const float HeaderLevelBarWidth = 60f;
    private const float HeaderLevelBarHeight = 4f;

    private const float ListSpacing = 4f;
    private const int ListPaddingVertical = 6;

    // Row layout (matches Docs/DesignMock/index.html: .stat-row)
    private const float RowHeight = 30f;
    private const float RowSpacing = 10f;
    private const int RowPaddingHorizontal = 10;
    private const int RowPaddingVertical = 7;
    private const float CheckBoxSize = 18f;
    private const float ValueWidth = 50f;

    #endregion

    #region Styles

    private static readonly Color HeaderFallbackBackgroundColor = new(0.1f, 0.1f, 0.12f, 0.95f);
    private static readonly Color HeaderBorderColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Color HeaderNameColor = new(0.95f, 0.84f, 0.7f, 1f);
    private static readonly Color HeaderMetaColor = new(1f, 1f, 1f, 0.6f);
    private static readonly Color DividerColor = new(1f, 1f, 1f, 0.7f);

    private static readonly Color RowBackgroundTint = new(9f / 255f, 9f / 255f, 12f / 255f, 0.6f);
    private static readonly Color RowBorderColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Color RowBorderHoverColor = new(1f, 1f, 1f, 0.2f);
    private static readonly Color RowSelectedTintA = new(128f / 255f, 12f / 255f, 16f / 255f, 0.4f);
    private static readonly Color RowSelectedTintB = new(88f / 255f, 28f / 255f, 18f / 255f, 0.25f);
    private static readonly Color RowSelectedBorderColor = new(128f / 255f, 12f / 255f, 16f / 255f, 0.6f);
    private static readonly Color RowSelectedBorderHoverColor = new(180f / 255f, 30f / 255f, 30f / 255f, 0.7f);

    private static readonly Color CheckBoxTextColor = new(79f / 255f, 209f / 255f, 84f / 255f, 1f);
    private static readonly Color CheckBoxBackgroundColor = new(0f, 0f, 0f, 0.4f);
    private static readonly Color CheckBoxBorderColor = new(1f, 1f, 1f, 0.15f);
    private static readonly Color CheckBoxSelectedBackgroundColor = new(79f / 255f, 209f / 255f, 84f / 255f, 0.15f);
    private static readonly Color CheckBoxSelectedBorderColor = new(79f / 255f, 209f / 255f, 84f / 255f, 0.4f);

    private static readonly Color StatNameColor = new(0.91f, 0.89f, 0.84f, 1f);
    private static readonly Color StatValueColor = new(79f / 255f, 209f / 255f, 84f / 255f, 1f);
    private static readonly Color StatNameUnselectedAlpha = new(1f, 1f, 1f, 0.7f);
    private static readonly Color StatValueUnselectedAlpha = new(1f, 1f, 1f, 0.6f);

    private static readonly Color LevelBarBackgroundColor = new(0f, 0f, 0f, 0.4f);
    private static readonly Color LevelBarFillA = new(0.831f, 0.639f, 0.310f, 1f); // #d4a34f
    private static readonly Color LevelBarFillB = new(0.945f, 0.820f, 0.478f, 1f); // #f1d17a

    private static readonly string[] HeaderSpriteNames = ["Act_BG", "TabGradient", "Window_Box_Background"];
    private static readonly string[] DividerSpriteNames = ["Divider_Horizontal", "Window_Divider_Horizontal_V_Red"];
    private static readonly string[] DefaultWeaponIconSpriteNames = ["Poneti_Icon_Sword_v2_48", "strength_level_icon"];
    private static readonly string[] RowSpriteNames = ["Window_Box_Background", "TabGradient", "SimpleBox_Normal"];
    private static readonly string[] LevelBarBackgroundSpriteNames = ["SimpleProgressBar_Empty_Default", "SimpleProgressBar_Mask", "Attribute_TierIndicator_Fixed"];
    private static readonly string[] LevelBarFillSpriteNames = ["SimpleProgressBar_Fill", "Attribute_TierIndicator_Fixed"];

    private static readonly Dictionary<string, string[]> WeaponTypeIconSpriteNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Sword", ["Poneti_Icon_Sword_v2_48", "Stunlock_Icon_BoneSword01"] },
        { "Axe", ["Poneti_Icon_Axe_v2_04", "Stunlock_Icon_BoneAxe01"] },
        { "Mace", ["Poneti_Icon_Hammer_30", "Stunlock_Icon_BoneMace01"] },
        { "Spear", ["Poneti_Icon_Spear_v2_01", "Poneti_Icon_Sword_v2_48"] },
        { "Crossbow", ["Poneti_Icon_Crossbow_v2_01", "Poneti_Icon_Bow_v2_01"] },
        { "GreatSword", ["Poneti_Icon_Greatsword_v2_01", "Poneti_Icon_Sword_v2_48"] },
        { "Slashers", ["Poneti_Icon_Dagger_v2_01", "Poneti_Icon_Sword_v2_48"] },
        { "Pistols", ["Poneti_Icon_Pistol_v2_01", "Poneti_Icon_Crossbow_v2_01"] },
        { "Reaper", ["Poneti_Icon_Scythe_v2_01", "Poneti_Icon_Axe_v2_04"] },
        { "Longbow", ["Poneti_Icon_Bow_v2_01", "Poneti_Icon_Crossbow_v2_01"] },
        { "Whip", ["Poneti_Icon_Whip_v2_01", "Poneti_Icon_Sword_v2_48"] }
    };

    #endregion

    #region Fields

    private Transform _panelRoot;
    private Transform _listRoot;
    private Image _weaponImage;
    private TextMeshProUGUI _headerNameText;
    private TextMeshProUGUI _headerSlotsText;
    private TextMeshProUGUI _headerLevelText;
    private Image _levelFillImage;
    private TextMeshProUGUI _levelPercentText;
    private TextMeshProUGUI _referenceText;
    private readonly List<StatBonusRow> _rows = [];

    #endregion

    #region Properties

    public override string TabId => "StatBonuses";
    public override string TabLabel => "Stat Bonuses";
    public override string SectionTitle => "Stat Bonuses";
    public override BloodcraftTab TabType => BloodcraftTab.StatBonuses;

    #endregion

    #region ICharacterMenuTabWithPanel

    public Transform CreatePanel(Transform parent, TextMeshProUGUI reference)
    {
        Reset();
        _referenceText = reference;

        RectTransform rectTransform = CreateRectTransformObject("BloodcraftStatBonuses", parent);
        if (rectTransform == null)
        {
            return null;
        }
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        EnsureVerticalLayout(rectTransform);

        CreateWeaponHeader(rectTransform, reference);
        _ = CreateDividerLine(rectTransform);
        _listRoot = CreateListRoot(rectTransform);

        TextMeshProUGUI hintText = UIFactory.CreateSectionSubHeader(rectTransform, reference,
            "Click a stat to toggle. Use .wep cst [Weapon] [StatIndex] in chat.");
        if (hintText != null)
        {
            hintText.alpha = 0.5f;
            hintText.alignment = TextAlignmentOptions.Center;
        }

        rectTransform.gameObject.SetActive(false);
        _panelRoot = rectTransform;
        return rectTransform;
    }

    public void UpdatePanel()
    {
        if (_panelRoot == null || _listRoot == null)
        {
            return;
        }

        if (_statBonusDataReady && _weaponStatBonusData != null)
        {
            WeaponStatBonusData data = _weaponStatBonusData;

            if (_weaponImage != null && !string.IsNullOrEmpty(data.WeaponType))
            {
                string[] iconSprites = GetWeaponTypeIconSprites(data.WeaponType);
                Sprite sprite = ResolveSprite(iconSprites);
                if (sprite == null)
                {
                    // No placeholder boxes if sprite can't resolve (lessons.md L-002)
                    _weaponImage.gameObject.SetActive(false);
                }
                else
                {
                    _weaponImage.sprite = sprite;
                    _weaponImage.gameObject.SetActive(true);
                    _weaponImage.type = Image.Type.Simple;
                    _weaponImage.preserveAspect = true;
                    _weaponImage.color = new Color(1f, 1f, 1f, 0.9f);
                }
            }

            UpdateHeader(data.WeaponType, data.SelectedStats.Count, data.MaxStatChoices, data.ExpertiseLevel, data.ExpertiseProgress);

            List<StatBonusEntry> entries = [];
            foreach (StatBonusDataEntry stat in data.AvailableStats)
            {
                entries.Add(new StatBonusEntry(stat.StatIndex, stat.StatName, stat.Value, stat.IsSelected));
            }

            EnsureRows(entries.Count);

            int rowCount = Math.Min(entries.Count, _rows.Count);
            Action<int> onClicked = (index) => Quips.SendCommand($".wep cst {data.WeaponType} {index}");

            for (int i = 0; i < rowCount; i++)
            {
                UpdateRow(_rows[i], entries[i], onClicked);
            }
        }
        else
        {
            UpdateHeader("Mock Sword", selectedCount: 0, maxChoices: 3, level: 67, progress01: 0.45f);

            List<StatBonusEntry> mockEntries =
            [
                new StatBonusEntry(1, "Max Health", 250f, false),
                new StatBonusEntry(2, "Movement Speed", 0.15f, false),
                new StatBonusEntry(3, "Primary Attack Speed", 0.12f, false),
                new StatBonusEntry(4, "Physical Life Leech", 0.05f, false),
                new StatBonusEntry(5, "Spell Life Leech", 0.05f, false),
                new StatBonusEntry(6, "Primary Life Leech", 0.05f, false),
                new StatBonusEntry(7, "Physical Power", 15f, false),
                new StatBonusEntry(8, "Spell Power", 15f, false),
                new StatBonusEntry(9, "Physical Crit Chance", 0.08f, false),
                new StatBonusEntry(10, "Physical Crit Damage", 0.25f, false),
                new StatBonusEntry(11, "Spell Crit Chance", 0.08f, false),
                new StatBonusEntry(12, "Spell Crit Damage", 0.25f, false)
            ];

            EnsureRows(mockEntries.Count);

            Action<int> onMockClicked = (statIndex) => Core.Log.LogInfo($"[Mock] Clicked stat index: {statIndex}");

            int rowCount = Math.Min(mockEntries.Count, _rows.Count);
            for (int i = 0; i < rowCount; i++)
            {
                UpdateRow(_rows[i], mockEntries[i], onMockClicked);
            }
        }
    }

    #endregion

    #region Lifecycle

    public override void Update()
    {
        UpdatePanel();
    }

    public override void Reset()
    {
        base.Reset();
        _panelRoot = null;
        _listRoot = null;
        _weaponImage = null;
        _headerNameText = null;
        _headerSlotsText = null;
        _headerLevelText = null;
        _levelFillImage = null;
        _levelPercentText = null;
        _referenceText = null;
        _rows.Clear();
    }

    #endregion

    #region Panel Construction

    private void CreateWeaponHeader(Transform parent, TextMeshProUGUI reference)
    {
        RectTransform rectTransform = CreateRectTransformObject("WeaponHeader", parent);
        if (rectTransform == null)
        {
            return;
        }
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        // Match DesignMock: background sprite + subtle border
        Image background = rectTransform.gameObject.AddComponent<Image>();
        ApplySprite(background, HeaderSpriteNames);
        background.color = background.sprite != null ? Color.white : HeaderFallbackBackgroundColor;
        background.raycastTarget = false;

        Outline outline = rectTransform.gameObject.AddComponent<Outline>();
        outline.effectColor = HeaderBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layout = rectTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = HeaderSpacing;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = CreatePadding(HeaderPaddingHorizontal, HeaderPaddingHorizontal, HeaderPaddingVertical, HeaderPaddingVertical);

        LayoutElement rowLayout = rectTransform.gameObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = HeaderMinHeight;
        rowLayout.preferredHeight = HeaderMinHeight;

        RectTransform iconRect = CreateRectTransformObject("WeaponIcon", rectTransform);
        if (iconRect != null)
        {
            iconRect.sizeDelta = new Vector2(HeaderIconSize, HeaderIconSize);
            Image iconImage = iconRect.gameObject.AddComponent<Image>();
            ApplySprite(iconImage, DefaultWeaponIconSpriteNames);
            if (iconImage.sprite == null)
            {
                // Hide icon if sprite can't be resolved (no placeholders)
                iconRect.gameObject.SetActive(false);
                _weaponImage = null;
            }
            else
            {
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
                iconImage.color = new Color(1f, 1f, 1f, 0.9f);
                iconImage.raycastTarget = false;
                _weaponImage = iconImage;

                LayoutElement iconLayout = iconRect.gameObject.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = HeaderIconSize;
                iconLayout.preferredHeight = HeaderIconSize;
            }
        }

        RectTransform infoRect = CreateRectTransformObject("WeaponInfo", rectTransform);
        if (infoRect != null)
        {
            VerticalLayoutGroup infoLayout = infoRect.gameObject.AddComponent<VerticalLayoutGroup>();
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            infoLayout.spacing = 2f;
            infoLayout.childForceExpandWidth = true;
            infoLayout.childForceExpandHeight = false;
            infoLayout.childControlHeight = true;
            infoLayout.childControlWidth = true;

            LayoutElement infoElement = infoRect.gameObject.AddComponent<LayoutElement>();
            infoElement.flexibleWidth = 1f;

            _headerNameText = UIFactory.CreateText(infoRect, reference, "No Weapon Equipped", reference.fontSize * HeaderNameFontScale, TextAlignmentOptions.Left);
            if (_headerNameText != null)
            {
                _headerNameText.fontSize = 16f;
                _headerNameText.color = HeaderNameColor;
            }

            // Meta row: "X / Y Bonuses" + "Lv.NN [bar] NN%"
            RectTransform metaRow = CreateRectTransformObject("HeaderMetaRow", infoRect);
            if (metaRow != null)
            {
                HorizontalLayoutGroup metaLayout = metaRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                metaLayout.childAlignment = TextAnchor.MiddleLeft;
                metaLayout.spacing = 12f;
                metaLayout.childForceExpandWidth = false;
                metaLayout.childForceExpandHeight = false;
                metaLayout.childControlWidth = true;
                metaLayout.childControlHeight = true;

                LayoutElement metaRowLayout = metaRow.gameObject.AddComponent<LayoutElement>();
                metaRowLayout.minHeight = 16f;
                metaRowLayout.preferredHeight = 16f;

                _headerSlotsText = CreateHeaderMetaText(metaRow, reference, "0 / 0 Bonuses");
                _headerLevelText = CreateHeaderMetaText(metaRow, reference, "Lv.0");

                _ = CreateLevelBar(metaRow, out _levelFillImage);
                _levelPercentText = CreateHeaderMetaText(metaRow, reference, "0%");
            }
        }
    }

    private Transform CreateListRoot(Transform parent)
    {
        RectTransform rectTransform = CreateRectTransformObject("StatBonusList", parent);
        if (rectTransform == null)
        {
            return null;
        }
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = rectTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = ListSpacing;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = CreatePadding(0, 0, ListPaddingVertical, ListPaddingVertical);

        ContentSizeFitter fitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return rectTransform;
    }

    private static RectTransform CreateDividerLine(Transform parent)
    {
        RectTransform rectTransform = CreateRectTransformObject("BloodcraftDivider", parent);
        if (rectTransform == null)
        {
            return null;
        }
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);

        Image image = rectTransform.gameObject.AddComponent<Image>();
        ApplySprite(image, DividerSpriteNames);
        image.color = DividerColor;
        image.raycastTarget = false;

        LayoutElement layout = rectTransform.gameObject.AddComponent<LayoutElement>();
        float height = image.sprite != null ? 8f : 2f;
        layout.preferredHeight = height;
        layout.minHeight = height;

        return rectTransform;
    }

    #endregion

    #region Rows

    private void EnsureRows(int count)
    {
        if (_listRoot == null)
        {
            return;
        }

        while (_rows.Count < count)
        {
            StatBonusRow row = CreateRow(_listRoot);
            if (row?.Root == null)
            {
                break;
            }

            _rows.Add(row);
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            bool isActive = i < count;
            _rows[i].Root.SetActive(isActive);
        }
    }

    private StatBonusRow CreateRow(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        RectTransform rectTransform = CreateRectTransformObject($"StatBonusRow_{_rows.Count + 1}", parent);
        if (rectTransform == null)
        {
            return null;
        }
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Image bgImage = rectTransform.gameObject.AddComponent<Image>();
        ApplySprite(bgImage, RowSpriteNames);
        bgImage.color = RowBackgroundTint;
        bgImage.raycastTarget = true;

        Outline outline = rectTransform.gameObject.AddComponent<Outline>();
        outline.effectColor = RowBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup layout = rectTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = RowSpacing;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = CreatePadding(RowPaddingHorizontal, RowPaddingHorizontal, RowPaddingVertical, RowPaddingVertical);

        LayoutElement rowLayout = rectTransform.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = RowHeight;
        rowLayout.minHeight = RowHeight;

        RectTransform checkRect = CreateRectTransformObject("CheckBox", rectTransform);
        TextMeshProUGUI checkText = null;
        if (checkRect != null)
        {
            checkRect.sizeDelta = new Vector2(CheckBoxSize, CheckBoxSize);
            Image checkBg = checkRect.gameObject.AddComponent<Image>();
            checkBg.color = CheckBoxBackgroundColor;
            checkBg.raycastTarget = false;

            Outline checkOutline = checkRect.gameObject.AddComponent<Outline>();
            checkOutline.effectColor = CheckBoxBorderColor;
            checkOutline.effectDistance = new Vector2(1f, -1f);

            checkText = CreateSimpleText(checkRect, "✓", 12f);
            if (checkText != null)
            {
                checkText.alignment = TextAlignmentOptions.Center;
                checkText.color = CheckBoxTextColor;
                checkText.text = string.Empty;
            }

            LayoutElement checkLayout = checkRect.gameObject.AddComponent<LayoutElement>();
            checkLayout.preferredWidth = CheckBoxSize;
            checkLayout.preferredHeight = CheckBoxSize;
        }

        TextMeshProUGUI nameText = CreateSimpleText(rectTransform, "Stat Name", 13f);
        if (nameText != null)
        {
            nameText.alignment = TextAlignmentOptions.Left;
            LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;
            nameText.color = StatNameColor;
        }

        TextMeshProUGUI valueText = CreateSimpleText(rectTransform, "+0%", 13f);
        if (valueText != null)
        {
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.color = StatValueColor;

            LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = ValueWidth;
        }

        SimpleStunButton button = rectTransform.gameObject.AddComponent<SimpleStunButton>();
        return new StatBonusRow(rectTransform.gameObject, bgImage, outline, checkText, checkRect, nameText, valueText, button);
    }

    private static void UpdateRow(StatBonusRow row, StatBonusEntry entry, Action<int> onClicked = null)
    {
        if (row == null)
        {
            return;
        }

        row.StatIndex = entry.StatIndex;
        row.IsSelected = entry.IsSelected;

        if (row.NameText != null)
        {
            row.NameText.text = entry.StatName;
            row.NameText.alpha = entry.IsSelected ? 1f : StatNameUnselectedAlpha.a;
        }

        if (row.ValueText != null)
        {
            row.ValueText.text = FormatStatValue(entry.StatIndex, entry.Value);
            row.ValueText.alpha = entry.IsSelected ? 1f : StatValueUnselectedAlpha.a;
        }

        if (row.CheckText != null)
        {
            row.CheckText.text = entry.IsSelected ? "✓" : string.Empty;
        }

        if (row.CheckBoxRoot != null)
        {
            Image checkBg = row.CheckBoxRoot.GetComponent<Image>();
            Outline checkBorder = row.CheckBoxRoot.GetComponent<Outline>();
            if (checkBg != null)
            {
                checkBg.color = entry.IsSelected ? CheckBoxSelectedBackgroundColor : CheckBoxBackgroundColor;
            }
            if (checkBorder != null)
            {
                checkBorder.effectColor = entry.IsSelected ? CheckBoxSelectedBorderColor : CheckBoxBorderColor;
            }
        }

        if (row.Background != null)
        {
            if (entry.IsSelected)
            {
                // Approximate CSS linear-gradient with tint + keep sprite
                row.Background.color = Color.Lerp(RowSelectedTintA, RowSelectedTintB, 0.5f);
            }
            else
            {
                row.Background.color = RowBackgroundTint;
            }
        }

        if (row.Border != null)
        {
            row.Border.effectColor = entry.IsSelected ? RowSelectedBorderColor : RowBorderColor;
        }

        if (row.Button != null)
        {
            row.Button.onClick.RemoveAllListeners();
            if (onClicked != null)
            {
                row.Button.onClick.AddListener((UnityAction)(() => onClicked(entry.StatIndex)));
            }
        }
    }

    private static string FormatStatValue(int statIndex, float value)
    {
        // Client indices are 1-based because DataService maps server enums (0-based) to client enums (0=None).
        // Weapon stats:
        // 1 MaxHealth (integer)
        // 2 MovementSpeed (decimal)
        // 3..6 attack/leech (percentage)
        // 7..8 power (integer)
        // 9..12 crits (percentage)
        bool isDecimal = statIndex == 2;
        bool isPercentage = statIndex is >= 3 and <= 6 || statIndex is >= 9 and <= 12;

        if (isPercentage)
        {
            return $"+{value * 100:0.#}%";
        }

        if (isDecimal)
        {
            return $"+{value:0.##}";
        }

        return $"+{value:0}";
    }

    #endregion

    #region Sprite Helpers

    private static string[] GetWeaponTypeIconSprites(string weaponType)
    {
        if (string.IsNullOrEmpty(weaponType))
        {
            return DefaultWeaponIconSpriteNames;
        }

        if (WeaponTypeIconSpriteNames.TryGetValue(weaponType, out string[] sprites))
        {
            return sprites;
        }

        return DefaultWeaponIconSpriteNames;
    }

    private static Sprite ResolveSprite(params string[] spriteNames)
    {
        if (spriteNames == null || spriteNames.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < spriteNames.Length; i++)
        {
            string name = spriteNames[i];
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (HudData.Sprites.TryGetValue(name, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }

    private static void ApplySprite(Image image, params string[] spriteNames)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = ResolveSprite(spriteNames);
        if (sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    #endregion

    #region Shared UI Helpers

    private static void EnsureVerticalLayout(Transform root, int paddingLeft = 0, int paddingRight = 0,
        int paddingTop = 0, int paddingBottom = 0, float spacing = 6f)
    {
        if (root == null || root.Equals(null))
        {
            return;
        }

        try
        {
            var layout = UIFactory.EnsureVerticalLayout(root, paddingLeft, paddingRight, paddingTop, paddingBottom);
            if (layout != null)
            {
                layout.spacing = spacing;
                layout.childControlHeight = true;
            }

            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        catch (Exception ex)
        {
            Core.Log.LogWarning($"[StatBonuses Tab] Failed to configure vertical layout: {ex.Message}");
        }
    }

    private static RectOffset CreatePadding(int left, int right, int top, int bottom)
        => UIFactory.CreatePadding(left, right, top, bottom);

    private static RectTransform CreateRectTransformObject(string name, Transform parent)
        => UIFactory.CreateRectTransformObject(name, parent);

    private TextMeshProUGUI CreateSimpleText(Transform parent, string text, float fontSize)
    {
        RectTransform rectTransform = CreateRectTransformObject("Text", parent);
        if (rectTransform == null)
        {
            return null;
        }

        TextMeshProUGUI tmpText = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        if (_referenceText != null)
        {
            UIFactory.CopyTextStyle(_referenceText, tmpText);
        }

        tmpText.text = text;
        tmpText.fontSize = fontSize;
        tmpText.color = Color.white;
        tmpText.raycastTarget = false;

        return tmpText;
    }

    private TextMeshProUGUI CreateHeaderMetaText(Transform parent, TextMeshProUGUI reference, string text)
    {
        TextMeshProUGUI tmp = UIFactory.CreateText(parent, reference, text, reference.fontSize * HeaderMetaFontScale, TextAlignmentOptions.Left);
        if (tmp != null)
        {
            tmp.fontSize = 12f;
            tmp.color = HeaderMetaColor;
            tmp.enableWordWrapping = false;
        }
        return tmp;
    }

    private RectTransform CreateLevelBar(Transform parent, out Image fill)
    {
        RectTransform rectTransform = CreateRectTransformObject("LevelBar", parent);
        if (rectTransform == null)
        {
            fill = null;
            return null;
        }

        rectTransform.sizeDelta = new Vector2(HeaderLevelBarWidth, HeaderLevelBarHeight);

        Image background = rectTransform.gameObject.AddComponent<Image>();
        ApplySprite(background, LevelBarBackgroundSpriteNames);
        background.color = LevelBarBackgroundColor;
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        LayoutElement layout = rectTransform.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = HeaderLevelBarWidth;
        layout.preferredWidth = HeaderLevelBarWidth;
        layout.minHeight = HeaderLevelBarHeight;
        layout.preferredHeight = HeaderLevelBarHeight;

        RectTransform fillRect = CreateRectTransformObject("Fill", rectTransform);
        if (fillRect == null)
        {
            fill = null;
            return rectTransform;
        }

        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);

        fill = fillRect.gameObject.AddComponent<Image>();
        ApplySprite(fill, LevelBarFillSpriteNames);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        fill.color = Color.Lerp(LevelBarFillA, LevelBarFillB, 0.5f);
        fill.raycastTarget = false;

        return rectTransform;
    }

    private void UpdateHeader(string weaponType, int selectedCount, int maxChoices, int level, float progress01)
    {
        if (_headerNameText != null)
        {
            _headerNameText.text = string.IsNullOrWhiteSpace(weaponType) ? "No Weapon Equipped" : weaponType;
        }

        if (_headerSlotsText != null)
        {
            _headerSlotsText.text = $"{selectedCount} / {maxChoices} Bonuses";
        }

        float clamped = Mathf.Clamp01(progress01);
        int pct = Mathf.RoundToInt(clamped * 100f);

        if (_headerLevelText != null)
        {
            _headerLevelText.text = $"Lv.{level}";
        }

        if (_levelFillImage != null)
        {
            _levelFillImage.fillAmount = clamped;
        }

        if (_levelPercentText != null)
        {
            _levelPercentText.text = $"{pct}%";
        }
    }

    #endregion

    #region Nested Types

    private sealed class StatBonusRow
    {
        public GameObject Root { get; }
        public Image Background { get; }
        public Outline Border { get; }
        public TextMeshProUGUI CheckText { get; }
        public RectTransform CheckBoxRoot { get; }
        public TextMeshProUGUI NameText { get; }
        public TextMeshProUGUI ValueText { get; }
        public SimpleStunButton Button { get; }
        public int StatIndex { get; set; }
        public bool IsSelected { get; set; }

        public StatBonusRow(
            GameObject root,
            Image background,
            Outline border,
            TextMeshProUGUI checkText,
            RectTransform checkBoxRoot,
            TextMeshProUGUI nameText,
            TextMeshProUGUI valueText,
            SimpleStunButton button)
        {
            Root = root;
            Background = background;
            Border = border;
            CheckText = checkText;
            CheckBoxRoot = checkBoxRoot;
            NameText = nameText;
            ValueText = valueText;
            Button = button;
            StatIndex = -1;
            IsSelected = false;
        }
    }

    private readonly struct StatBonusEntry
    {
        public int StatIndex { get; }
        public string StatName { get; }
        public float Value { get; }
        public bool IsSelected { get; }

        public StatBonusEntry(int statIndex, string statName, float value, bool isSelected)
        {
            StatIndex = statIndex;
            StatName = statName;
            Value = value;
            IsSelected = isSelected;
        }
    }

    #endregion
}

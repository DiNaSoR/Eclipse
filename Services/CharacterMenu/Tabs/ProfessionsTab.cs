using Eclipse.Services.CharacterMenu.Base;
using Eclipse.Services.CharacterMenu.Interfaces;
using Eclipse.Services.HUD.Shared;
using TMPro;
using UnityEngine;
using static Eclipse.Services.DataService;
using static Eclipse.Services.CanvasService.DataHUD;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Character menu tab for displaying profession progress.
/// Uses a custom panel layout with profession rows.
/// </summary>
internal class ProfessionsTab : CharacterMenuTabBase, ICharacterMenuTabWithPanel
{
    #region Fields

    private Transform _panelRoot;
    private Transform _listRoot;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _summaryText;

    #endregion

    #region Properties

    public override string TabId => "Professions";
    public override string TabLabel => "Professions";
    public override string SectionTitle => "Professions";
    public override BloodcraftTab TabType => BloodcraftTab.Professions;

    #endregion

    #region ICharacterMenuTabWithPanel

    public Transform CreatePanel(Transform parent, TextMeshProUGUI reference)
    {
        // Panel creation will be delegated to CharacterMenuService for now
        // This is a placeholder for when full extraction is complete
        return null;
    }

    public void UpdatePanel()
    {
        if (_panelRoot == null) return;

        if (!HudConfiguration.ProfessionBarsEnabled)
        {
            if (_statusText != null)
            {
                _statusText.text = "Profession UI disabled.";
                _statusText.gameObject.SetActive(true);
            }
            if (_listRoot != null)
            {
                _listRoot.gameObject.SetActive(false);
            }
            return;
        }

        if (_statusText != null)
        {
            _statusText.text = string.Empty;
            _statusText.gameObject.SetActive(false);
        }

        if (_listRoot != null)
        {
            _listRoot.gameObject.SetActive(true);
        }

        // Update summary text with total levels
        if (_summaryText != null)
        {
            _summaryText.text = BuildSummaryText();
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
        _statusText = null;
        _summaryText = null;
    }

    #endregion

    #region Private Methods

    private string BuildSummaryText()
    {
        int maxTotal = 8 * 100; // 8 professions * 100 max level

        // Sum up profession levels from individual DataHUD fields
        int totalLevels = _enchantingLevel
            + _alchemyLevel
            + _harvestingLevel
            + _blacksmithingLevel
            + _tailoringLevel
            + _woodcuttingLevel
            + _miningLevel
            + _fishingLevel;

        float percentage = (float)totalLevels / maxTotal * 100f;
        return $"Total: {totalLevels}/{maxTotal} ({percentage:F1}%)";
    }

    #endregion

    #region UI Element Setters

    /// <summary>
    /// Sets the UI element references for this tab.
    /// </summary>
    public void SetUIElements(
        Transform panelRoot,
        Transform listRoot,
        TextMeshProUGUI statusText,
        TextMeshProUGUI summaryText)
    {
        _panelRoot = panelRoot;
        _listRoot = listRoot;
        _statusText = statusText;
        _summaryText = summaryText;
    }

    #endregion
}

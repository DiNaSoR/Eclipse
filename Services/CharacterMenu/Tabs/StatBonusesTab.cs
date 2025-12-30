using Eclipse.Services.CharacterMenu.Base;
using Eclipse.Services.CharacterMenu.Interfaces;
using TMPro;
using UnityEngine;
using static Eclipse.Services.DataService;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Character menu tab for displaying and managing weapon stat bonus selections.
/// Uses a custom panel layout instead of simple entries.
/// </summary>
internal class StatBonusesTab : CharacterMenuTabBase, ICharacterMenuTabWithPanel
{
    #region Fields

    private Transform _panelRoot;
    private TextMeshProUGUI _weaponText;
    private TextMeshProUGUI _countText;

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
        // Panel creation will be delegated to CharacterMenuService for now
        // This is a placeholder for when full extraction is complete
        return null;
    }

    public void UpdatePanel()
    {
        if (_panelRoot == null) return;

        if (!_statBonusDataReady)
        {
            if (_weaponText != null)
            {
                _weaponText.text = "Awaiting stat bonus data...";
            }
            return;
        }

        if (_weaponStatBonusData == null)
        {
            if (_weaponText != null)
            {
                _weaponText.text = "No stat bonus data available.";
            }
            return;
        }

        // Update weapon type display
        if (_weaponText != null && !string.IsNullOrEmpty(_weaponStatBonusData.WeaponType))
        {
            _weaponText.text = _weaponStatBonusData.WeaponType;
        }

        // Update count display
        if (_countText != null)
        {
            int selected = _weaponStatBonusData.SelectedStats?.Count ?? 0;
            int max = _weaponStatBonusData.MaxStatChoices;
            _countText.text = $"{selected}/{max}";
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
        _weaponText = null;
        _countText = null;
    }

    #endregion

    #region UI Element Setters

    /// <summary>
    /// Sets the UI element references for this tab.
    /// </summary>
    public void SetUIElements(
        Transform panelRoot,
        TextMeshProUGUI weaponText,
        TextMeshProUGUI countText)
    {
        _panelRoot = panelRoot;
        _weaponText = weaponText;
        _countText = countText;
    }

    #endregion
}

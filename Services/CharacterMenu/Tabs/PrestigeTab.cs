using Eclipse.Services.CharacterMenu.Base;
using Eclipse.Services.CharacterMenu.Interfaces;
using Eclipse.Services.HUD.Shared;
using System.Collections.Generic;
using TMPro;
using static Eclipse.Services.DataService;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Character menu tab for displaying the Prestige leaderboard.
/// </summary>
internal class PrestigeTab : CharacterMenuTabBase, ICharacterMenuTabWithEntries
{
    #region Fields

    private int _leaderboardIndex = 0;

    #endregion

    #region Properties

    public override string TabId => "Prestige";
    public override string TabLabel => "Prestige";
    public override string SectionTitle => "Prestige Leaderboard";
    public override BloodcraftTab TabType => BloodcraftTab.Prestige;

    #endregion

    #region ICharacterMenuTabWithEntries

    public List<BloodcraftEntry> BuildEntries()
    {
        var list = new List<BloodcraftEntry>();

        if (!_prestigeDataReady)
        {
            list.Add(new BloodcraftEntry("Awaiting prestige data...", FontStyles.Normal));
            return list;
        }

        if (!_prestigeSystemEnabled)
        {
            list.Add(new BloodcraftEntry("Prestige system disabled.", FontStyles.Normal));
            return list;
        }

        if (!_prestigeLeaderboardEnabled)
        {
            list.Add(new BloodcraftEntry("Prestige leaderboard disabled.", FontStyles.Normal));
            return list;
        }

        var leaderboardOrder = _prestigeLeaderboardOrder;
        if (leaderboardOrder == null || leaderboardOrder.Count == 0)
        {
            list.Add(new BloodcraftEntry("No prestige data available.", FontStyles.Normal));
            return list;
        }

        if (_leaderboardIndex >= leaderboardOrder.Count)
        {
            _leaderboardIndex = 0;
        }

        string typeKey = leaderboardOrder[_leaderboardIndex];
        string displayType = HudUtilities.SplitPascalCase(typeKey);

        list.Add(new BloodcraftEntry("Click type to cycle.", FontStyles.Normal));
        list.Add(new BloodcraftEntry(
            $"Type: {displayType}",
            FontStyles.Normal,
            action: CyclePrestigeType,
            enabled: leaderboardOrder.Count > 1));

        if (!_prestigeLeaderboards.TryGetValue(typeKey, out var leaderboard) || leaderboard == null)
        {
            leaderboard = [];
        }

        if (leaderboard.Count == 0)
        {
            list.Add(new BloodcraftEntry("No prestige entries yet.", FontStyles.Normal));
        }
        else
        {
            for (int i = 0; i < leaderboard.Count; i++)
            {
                var entry = leaderboard[i];
                FontStyles style = i == 0 ? FontStyles.Bold : FontStyles.Normal;
                list.Add(new BloodcraftEntry($"{i + 1} | {entry.Name}: {entry.Value}", style));
            }
        }

        return list;
    }

    #endregion

    #region Lifecycle

    public override void Update()
    {
        // Update handled by orchestrator calling BuildEntries
    }

    public override void Reset()
    {
        base.Reset();
        _leaderboardIndex = 0;
    }

    #endregion

    #region Private Methods

    private void CyclePrestigeType()
    {
        var order = _prestigeLeaderboardOrder;
        if (order != null && order.Count > 0)
        {
            _leaderboardIndex = (_leaderboardIndex + 1) % order.Count;
        }
    }

    #endregion
}

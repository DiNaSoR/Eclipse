using Eclipse.Services.CharacterMenu.Base;
using Eclipse.Services.CharacterMenu.Interfaces;
using System.Collections.Generic;
using TMPro;
using static Eclipse.Services.CanvasService.DataHUD;
using static Eclipse.Services.DataService;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Character menu tab for managing Familiars.
/// Provides easy access to common familiar commands without typing in chat.
/// </summary>
internal class FamiliarsTab : CharacterMenuTabBase, ICharacterMenuTabWithEntries
{
    #region Properties

    public override string TabId => "Familiars";
    public override string TabLabel => "Familiars";
    public override string SectionTitle => "Familiar Management";
    public override BloodcraftTab TabType => BloodcraftTab.Familiars;

    #endregion

    #region ICharacterMenuTabWithEntries

    public List<BloodcraftEntry> BuildEntries()
    {
        var list = new List<BloodcraftEntry>();

        // Check if familiar system is enabled
        if (!_familiarSystemEnabled)
        {
            list.Add(new BloodcraftEntry("Familiars are disabled on this server.", FontStyles.Normal));
            return list;
        }

        // Active Familiar Section
        list.Add(new BloodcraftEntry("Active Familiar", FontStyles.Bold));

        string familiarDisplayName = !string.IsNullOrEmpty(_familiarName) && _familiarName != "Familiar" && _familiarName != "Frailed"
            ? _familiarName
            : "None";

        if (familiarDisplayName != "None")
        {
            string prestigeText = _familiarPrestige > 0 ? $" [P{_familiarPrestige}]" : "";
            list.Add(new BloodcraftEntry($"  {familiarDisplayName} Lv.{_familiarLevel}{prestigeText}", FontStyles.Normal));

            // Familiar stats if available
            if (_familiarStats != null && _familiarStats.Count >= 3)
            {
                bool hasStats = !string.IsNullOrEmpty(_familiarStats[0]) ||
                               !string.IsNullOrEmpty(_familiarStats[1]) ||
                               !string.IsNullOrEmpty(_familiarStats[2]);
                if (hasStats)
                {
                    string healthStat = !string.IsNullOrEmpty(_familiarStats[0]) ? $"HP:{_familiarStats[0]}" : "";
                    string physPower = !string.IsNullOrEmpty(_familiarStats[1]) ? $"PP:{_familiarStats[1]}" : "";
                    string spellPower = !string.IsNullOrEmpty(_familiarStats[2]) ? $"SP:{_familiarStats[2]}" : "";

                    var statParts = new List<string>();
                    if (!string.IsNullOrEmpty(healthStat)) statParts.Add(healthStat);
                    if (!string.IsNullOrEmpty(physPower)) statParts.Add(physPower);
                    if (!string.IsNullOrEmpty(spellPower)) statParts.Add(spellPower);

                    if (statParts.Count > 0)
                    {
                        list.Add(new BloodcraftEntry($"  {string.Join(" | ", statParts)}", FontStyles.Normal));
                    }
                }
            }
        }
        else
        {
            list.Add(new BloodcraftEntry("  No familiar bound", FontStyles.Italic));
        }

        // Quick Actions Section
        list.Add(new BloodcraftEntry("Quick Actions", FontStyles.Bold));
        list.Add(new BloodcraftEntry("  Call/Dismiss Familiar", FontStyles.Normal, command: ".fam t", enabled: true));
        list.Add(new BloodcraftEntry("  Toggle Combat Mode", FontStyles.Normal, command: ".fam c", enabled: true));
        list.Add(new BloodcraftEntry("  Unbind Familiar", FontStyles.Normal, command: ".fam ub", enabled: true));

        // Box Management Section
        list.Add(new BloodcraftEntry("Box Management", FontStyles.Bold));
        list.Add(new BloodcraftEntry("  List Boxes", FontStyles.Normal, command: ".fam boxes", enabled: true));
        list.Add(new BloodcraftEntry("  List Current Box", FontStyles.Normal, command: ".fam l", enabled: true));

        // Bind by number shortcuts (1-10)
        list.Add(new BloodcraftEntry("Bind Familiar (1-10)", FontStyles.Bold));
        for (int i = 1; i <= 10; i++)
        {
            list.Add(new BloodcraftEntry($"  Slot {i}", FontStyles.Normal, command: $".fam b {i}", enabled: true));
        }

        // Advanced Section
        list.Add(new BloodcraftEntry("Advanced", FontStyles.Bold));
        list.Add(new BloodcraftEntry("  Search Familiars", FontStyles.Normal, command: ".fam s", enabled: true));
        list.Add(new BloodcraftEntry("  View Overflow", FontStyles.Normal, command: ".fam of", enabled: true));
        list.Add(new BloodcraftEntry("  Toggle Emote Actions", FontStyles.Normal, command: ".fam e", enabled: true));
        list.Add(new BloodcraftEntry("  Show Emote Actions", FontStyles.Normal, command: ".fam actions", enabled: true));
        list.Add(new BloodcraftEntry("  Get Familiar Level", FontStyles.Normal, command: ".fam gl", enabled: true));
        list.Add(new BloodcraftEntry("  Prestige Familiar", FontStyles.Normal, command: ".fam pr", enabled: true));

        return list;
    }

    #endregion

    #region Lifecycle

    public override void Update()
    {
        // Update handled by orchestrator calling BuildEntries
    }

    #endregion
}

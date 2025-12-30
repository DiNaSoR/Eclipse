using Eclipse.Services.CharacterMenu.Base;
using Eclipse.Services.CharacterMenu.Interfaces;
using Eclipse.Services.HUD.Shared;
using System;
using System.Collections.Generic;
using TMPro;
using static Eclipse.Services.DataService;

namespace Eclipse.Services.CharacterMenu.Tabs;

/// <summary>
/// Character menu tab for displaying and managing Exoform/Shapeshift configurations.
/// </summary>
internal class ExoformTab : CharacterMenuTabBase, ICharacterMenuTabWithEntries
{
    #region Properties

    public override string TabId => "Exoform";
    public override string TabLabel => "Exoform";
    public override string SectionTitle => "Exoforms";
    public override BloodcraftTab TabType => BloodcraftTab.Exoform;

    #endregion

    #region ICharacterMenuTabWithEntries

    public List<BloodcraftEntry> BuildEntries()
    {
        var list = new List<BloodcraftEntry>();

        if (!_exoFormDataReady)
        {
            list.Add(new BloodcraftEntry("Awaiting exoform data...", FontStyles.Normal));
            return list;
        }

        if (!_exoFormEnabled)
        {
            list.Add(new BloodcraftEntry("Exo prestiging disabled.", FontStyles.Normal));
            return list;
        }

        string currentForm = string.IsNullOrWhiteSpace(_exoFormCurrentForm)
            ? "None"
            : HudUtilities.SplitPascalCase(_exoFormCurrentForm);

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

        if (_exoFormEntries == null || _exoFormEntries.Count == 0)
        {
            list.Add(new BloodcraftEntry("No forms available.", FontStyles.Normal));
            return list;
        }

        for (int i = 0; i < _exoFormEntries.Count; i++)
        {
            ExoFormEntry form = _exoFormEntries[i];
            string formName = HudUtilities.SplitPascalCase(form.FormName);
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

        return list;
    }

    #endregion

    #region Lifecycle

    public override void Update()
    {
        // Update handled by orchestrator calling BuildEntries
    }

    #endregion

    #region Private Methods

    private static ExoFormEntry ResolveActiveExoForm()
    {
        if (string.IsNullOrWhiteSpace(_exoFormCurrentForm) || _exoFormEntries == null)
            return null;

        foreach (var form in _exoFormEntries)
        {
            if (form.FormName.Equals(_exoFormCurrentForm, StringComparison.OrdinalIgnoreCase))
                return form;
        }

        return null;
    }

    private static string ResolveAbilityName(int abilityId)
    {
        // Try to get localized ability name, fallback to ID
        return $"Ability #{abilityId}";
    }

    #endregion
}

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using static CSFFCardDetailTooltip.Utils;

namespace CSFFCardDetailTooltip;

internal class Encounter
{
    public static TooltipText EncounterTooltip = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnHoverEnter")]
    public static void OnHoverEnter(TooltipProvider __instance)
    {
        if (!Plugin.Enabled) return;
        if (__instance is not EncounterOptionButton ecob) return;

        EncounterPopup popup = __instance.GetComponentInParent<EncounterPopup>();
        if (popup == null) return;
        //int actionIndex = __instance.Index;
        //if (actionIndex < 0 || actionIndex > popup.GeneralPlayerActions.Length - 1) return;
        List<string> texts = [];
        if (ecob.SubActions.Count == 1)
            texts.Add(FormatEncounterPlayerAction(ecob.SubActions[0], popup));

        string newContent = texts.Join(delimiter: "\n");

        if (!string.IsNullOrWhiteSpace(newContent))
        {
            var myTooltip = Traverse.Create(ecob).Field("MyTooltip").GetValue<TooltipText>();
            string orgContent = myTooltip == null ? "" : myTooltip.TooltipContent;
            EncounterTooltip.TooltipContent = orgContent + (string.IsNullOrEmpty(orgContent) ? "" : "\n") +
                                               "<size=70%>" + newContent + "</size>";
            EncounterTooltip.HoldText = myTooltip == null ? "" : myTooltip.HoldText;
            Tooltip.AddTooltip(EncounterTooltip);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EncounterPopup), "DisplayPlayerActions")]
    public static void OnEncounterDisplayPlayerActionsPatch(EncounterPopup __instance)
    {
        if (!Plugin.Enabled || !Plugin.AdditionalEncounterLogMessage) return;
        InGameEncounter encounter = __instance.CurrentEncounter;
        IEnumerable<string> actionTexts = encounter.EncounterModel.EnemyActions
            .Where(a => a is { DoesNotAttack: false }).Select(a => FormatEnemyHitResult(encounter, a, __instance, 1));

        if (actionTexts.Any() && !actionTexts.All(string.IsNullOrEmpty))
        {
            __instance.AddToLog(new EncounterLogMessage("If I am hit by an enemy, I might get hurt: (on average)"));
            __instance.AddToLog(new EncounterLogMessage(string.Join("\n", actionTexts)));
        }
        else
        {
            __instance.AddToLog(new EncounterLogMessage("I am confident it can't hurt me! (on average)"));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EncounterPopup), "GenerateEnemyWound")]
    public static void PostGenerateEnemyWoundPatch(EncounterPopup __instance)
    {
        if (!Plugin.Enabled || !Plugin.AdditionalEncounterLogMessage) return;
        static string SeverityText(WoundSeverity s)
        {
            return s switch
            {
                WoundSeverity.Minor => $"{LcStr("CSFFCardDetailTooltip.Encounter.DamageThisRound", "This Round's Damage")}: {LcStr("CSFFCardDetailTooltip.Encounter.Minor", "Minor")}",
                WoundSeverity.Medium => $"{LcStr("CSFFCardDetailTooltip.Encounter.DamageThisRound", "This Round's Damage")}: {LcStr("CSFFCardDetailTooltip.Encounter.Medium", "Medium")}",
                WoundSeverity.Serious => $"{LcStr("CSFFCardDetailTooltip.Encounter.DamageThisRound", "This Round's Damage")}: {LcStr("CSFFCardDetailTooltip.Encounter.Serious", "Serious")}",
                _ => ""
            };
        }

        EncounterPlayerDamageReport report = Traverse.Create(__instance).Field("CurrentRoundPlayerDamageReport").GetValue<EncounterPlayerDamageReport>();
        if (report.AttackSeverity > WoundSeverity.NoWound)
            __instance.AddToLog(new EncounterLogMessage(SeverityText(report.AttackSeverity)));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnHoverExit")]
    public static void EncounterOptionButtonOnHoverExitPatch(TooltipProvider __instance)
    {
        if (__instance is not EncounterOptionButton) return;
        Tooltip.RemoveTooltip(EncounterTooltip);
        Tooltip.Instance.TooltipContent.pageToDisplay = 1;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnDisable")]
    public static void EncounterOptionButtonOnDisablePatch(TooltipProvider __instance)
    {
        if (__instance is not EncounterOptionButton) return;
        Tooltip.RemoveTooltip(EncounterTooltip);
        Tooltip.Instance.TooltipContent.pageToDisplay = 1;
    }
}
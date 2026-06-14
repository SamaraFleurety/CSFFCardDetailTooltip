#if MELON_LOADER
using MelonLoader;
#else
using BepInEx;
#endif
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static CSFFCardDetailTooltip.Utils;


#if MELON_LOADER
[assembly: MelonInfo(typeof(CSFFCardDetailTooltip.Plugin), CSFFCardDetailTooltip.PluginInfo.PLUGIN_NAME, CSFFCardDetailTooltip.PluginInfo.PLUGIN_VERSION, "computerfan")]
[assembly: MelonGame("WinterSpring Games", "Card Survival - Tropical Island")]
[assembly: MelonGame("WinterSpringGames", "CardSurvivalTropicalIsland")]
[assembly: MelonGame("winterspringgames", "survivaljourney")]
[assembly: MelonGame("winterspringgames", "survivaljourneydemo")]
[assembly: HarmonyDontPatchAll]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: MelonPlatform((MelonPlatformAttribute.CompatiblePlatforms)3)] // 3 = Android
#endif

namespace CSFFCardDetailTooltip
{
#if MELON_LOADER
    public class Plugin : MelonMod
#else
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
#endif
    {
        public static TooltipText MyTooltip = new();
        public static bool Enabled;
        public static KeyCode HotKey;
        public static bool RecipesShowTargetDuration;
        public static bool HideImpossibleDropSet;
        public static KeyCode TooltipNextPageHotKey;
        public static KeyCode TooltipPreviousPageHotKey;
        public static bool AdditionalEncounterLogMessage;
        public static bool ForceInspectStatInfos;

        public static InGameCardBase lastDragHoverCard;
        public static string LastDragHoverCardOrgTooltipContent;

#if MELON_LOADER
        private MelonPreferences_Category GeneralPreferencesCategory;
        private MelonPreferences_Category TweakPreferencesCategory;
        private MelonPreferences_Entry<bool> EnabledEntry;
        private MelonPreferences_Entry<KeyCode> HotKeyEntry;
        private MelonPreferences_Entry<bool> RecipesShowTargetDurationEntry;
        private MelonPreferences_Entry<bool> HideImpossibleDropSetEntry;
        private MelonPreferences_Entry<bool> AdditionalEncounterLogMessageEntry;
        private MelonPreferences_Entry<bool> ForceInspectStatInfosEntry;
        public override void OnInitializeMelon()
        {
            GeneralPreferencesCategory = MelonPreferences.CreateCategory("General");
            TweakPreferencesCategory = MelonPreferences.CreateCategory("Tweak");
            GeneralPreferencesCategory.SetFilePath("UserData/CSFFCardDetailTooltip.cfg");
            TweakPreferencesCategory.SetFilePath("UserData/CSFFCardDetailTooltip.cfg");
            EnabledEntry =
 GeneralPreferencesCategory.CreateEntry(nameof(Enabled), true, "If true, will show the tool tips.");
            HotKeyEntry =
 GeneralPreferencesCategory.CreateEntry(nameof(HotKey), KeyCode.F2, "The key to enable and disable the tool tips");
            RecipesShowTargetDurationEntry =
 TweakPreferencesCategory.CreateEntry(nameof(RecipesShowTargetDuration), false, "If true, will show the target duration of recipes");
            HideImpossibleDropSetEntry =
 TweakPreferencesCategory.CreateEntry(nameof(HideImpossibleDropSet), true, "If true, will hide the impossible drop set");
            AdditionalEncounterLogMessageEntry = TweakPreferencesCategory.CreateEntry(
                nameof(AdditionalEncounterLogMessage), false,
                "If true, shows additional tips in the message log of combat encounter.");
            ForceInspectStatInfosEntry = GeneralPreferencesCategory.CreateEntry(nameof(ForceInspectStatInfosEntry),
                false, "If true, stats like Bacteria Fever are forced to be inspectable.");
            Enabled = EnabledEntry.Value; 
            HotKey = HotKeyEntry.Value;
            RecipesShowTargetDuration = RecipesShowTargetDurationEntry.Value;
            HideImpossibleDropSet = HideImpossibleDropSetEntry.Value;
            AdditionalEncounterLogMessage = AdditionalEncounterLogMessageEntry.Value;
            ForceInspectStatInfos = ForceInspectStatInfosEntry.Value;

            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Plugin));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Stat));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Action));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Locale));
            Locale.LoadLanguagePostfix();
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(TooltipMod));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(PrefabMod));
            HarmonyLib.Harmony.CreateAndPatchAll(typeof(Encounter));

            GeneralPreferencesCategory.SaveToFile();
            TweakPreferencesCategory.SaveToFile();

            LoggerInstance.Msg($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
#else
        public static ConfigEntry<bool> AdditionalEncounterLogMessageEntry;

        private void Awake()
        {
            Enabled = Config.Bind("General", nameof(Enabled), true, "If true, will show the tool tips.").Value;
            HotKey = Config.Bind("General", nameof(HotKey), KeyCode.F2, "The key to enable and disable the tool tips")
                .Value;
            RecipesShowTargetDuration = Config.Bind("Tweak", nameof(RecipesShowTargetDuration), false,
                "If true, cookers like traps will show exact cooking duration instead of a range.").Value;
            HideImpossibleDropSet = Config.Bind("Tweak", nameof(HideImpossibleDropSet), true,
                "If true, impossible drop sets will be hidden.").Value;
            TooltipNextPageHotKey = Config.Bind("Tooltip", nameof(TooltipNextPageHotKey), KeyCode.RightBracket,
                "The key to show next page of the tool tip.").Value;
            TooltipPreviousPageHotKey = Config.Bind("Tooltip", nameof(TooltipPreviousPageHotKey), KeyCode.LeftBracket,
                "The key to show previous page of the tool tip.").Value;
            AdditionalEncounterLogMessageEntry = Config.Bind("General", nameof(AdditionalEncounterLogMessage), false,
                "If true, shows additional tips in the message log of combat encounter.");
            AdditionalEncounterLogMessage = AdditionalEncounterLogMessageEntry.Value;
            ForceInspectStatInfos = Config.Bind("General", nameof(ForceInspectStatInfos), false, "If true, stats like Bacteria Fever are forced to be inspectable.").Value;

            // Plugin startup logic
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Stat));
            Harmony.CreateAndPatchAll(typeof(Action));
            Harmony.CreateAndPatchAll(typeof(Locale));
            Harmony.CreateAndPatchAll(typeof(TooltipMod));
            Harmony.CreateAndPatchAll(typeof(Encounter));

            Harmony.CreateAndPatchAll(typeof(Patch_HoverEnterCard));

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }
#endif

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "Update")]
        public static void GameMangerUpdatePatch()
        {
            if (Input.GetKeyDown(HotKey))
            {
                Enabled = !Enabled;
                TooltipMod.Fitter.verticalFit =
                    ~TooltipMod.Fitter.verticalFit & ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InGameCardBase), "OnHoverExit")]
        public static void InGameCardBaseOnHoverExitPatch(InGameCardBase __instance)
        {
            Tooltip.RemoveTooltip(MyTooltip);
            Tooltip.Instance.TooltipContent.pageToDisplay = 1;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InGameDraggableCard), "OnEndDrag")]
        public static void InGameDraggableCardOnEndDragPatch(InGameDraggableCard __instance)
        {
            lastDragHoverCard = null;
        }



        [HarmonyPrefix]
        [HarmonyPatch(typeof(TooltipProvider), "OnPointerEnter")]
        public static void EquipmentButtonUpdatePatch(TooltipProvider __instance)
        {
            if (__instance is not EquipmentButton || IsModCompatible("WikiMod", new Version(1, 0, 6))) return;

            if (!Enabled)
            {
                __instance.SetTooltip(LocalizedString.Equipment, null, null);
            }
            else
            {
                var InGamePlayerWeight = MBSingleton<GameManager>.Instance.InGamePlayerWeight;
                __instance.SetTooltip(__instance.Title,
                    FormatBasicEntry(
                        $"{InGamePlayerWeight.SimpleCurrentValue}/{InGamePlayerWeight.StatModel.MinMaxValue.y}",
                        "Weight"), null);
            }
        }
    }
}
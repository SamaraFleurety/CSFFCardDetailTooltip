using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CSFFCardDetailTooltip.Utils;
using static CSFFCardDetailTooltip.Plugin;
using UnityEngine;

namespace CSFFCardDetailTooltip
{
    [HarmonyPatch(typeof(InGameCardBase), "OnHoverEnter")]
    public static class Patch_HoverEnterCard
    {
        [HarmonyPrefix]
        public static void OnHoverEnterPatch(InGameCardBase __instance)
        {
            if (!Plugin.Enabled || __instance.IsPinned) return;
            CardData cardModel = __instance.CardModel;
            if (!cardModel) return;
            GraphicsManager graphicsM = GraphicsManager.Instance;
            GameManager gm = GameManager.Instance;
            List<string> baseSpoilageRate = [];
            List<string> baseUsageRate = [];
            List<string> baseFuelRate = [];
            List<string> baseConsumableRate = [];
            List<string> baseSpecial1Rate = [];
            List<string> baseSpecial2Rate = [];
            List<string> baseSpecial3Rate = [];
            List<string> baseSpecial4Rate = [];
            List<string> baseEvaporationRate = [];
            List<string> texts = [];

            if (GameManager.DraggedCard)
            {
                InGameDraggableCard droppedCard = GameManager.DraggedCard;
                if (!droppedCard || !droppedCard.CanBeDragged) return;
                if (lastDragHoverCard == __instance) return;

                if (lastDragHoverCard != null)
                {
                    var myTooltip = Traverse.Create(lastDragHoverCard).Field("MyTooltip").GetValue<TooltipText>();
                    TooltipText orgTooltip = myTooltip;
                    if (orgTooltip != null) orgTooltip.TooltipContent = LastDragHoverCardOrgTooltipContent;
                    lastDragHoverCard = null;
                }

                var possibleAction = Traverse.Create(__instance).Field("PossibleAction").GetValue<CardOnCardAction>();
                CardOnCardAction action = possibleAction;
                if (action == null) return;
                InGameCardBase currentCard =
                    action.CanGiveLiquid(droppedCard) &&
                    action.RequiredGivenLiquidContent.IsValid(droppedCard, _InactiveMeansEmpty: false)
                        ? __instance.ContainedLiquid
                        : __instance;
                if (action.ProducedCards != null)
                {
                    CollectionDropReport dropReport = gm.GetCollectionDropsReport(action, currentCard, droppedCard, InGameNPCOrPlayer.PlayerAgent, true);
                    texts.Add(Action.FormatCardDropList(dropReport, currentCard, action: action));
                }

                texts.Add(FormatCardOnCardAction(action, currentCard, droppedCard));
                if (texts.Count > 0)
                {
                    TooltipText orgTooltip = Traverse.Create(__instance).Field("MyTooltip").GetValue<TooltipText>();
                    LastDragHoverCardOrgTooltipContent = __instance.Content;
                    lastDragHoverCard = __instance;
                    orgTooltip.TooltipContent =
                        (string.IsNullOrEmpty(__instance.Content) ? "" : __instance.Content + "\n") + "<size=75%>" +
                        texts.Join(delimiter: "\n") + "</size>";
                }

                return;
            }

            if (cardModel.CardType == CardTypes.Location && __instance.IsCooking())
            {
                foreach (CookingCardStatus cookingstatus in __instance.CookingCards)
                {
                    if (cookingstatus == null || cookingstatus.Card == null) continue;
                    CookingRecipe recipe =
                        cardModel.GetRecipeForCard(cookingstatus.Card.CardModel, cookingstatus.Card, __instance);
                    if (recipe == null) continue;
                    if (!RecipesShowTargetDuration && recipe.MinDuration != recipe.MaxDuration)
                    {
                        texts.Add(FormatBasicEntry(
                            $"{cookingstatus.CookedDuration}/[{recipe.MinDuration}, {recipe.MaxDuration}]",
                            $"{recipe.ActionName}"));
                        texts.Add(Utils.FormatRate(1, cookingstatus.CookedDuration, recipe.MaxDuration));
                    }
                    else
                    {
                        texts.Add(FormatBasicEntry($"{cookingstatus.CookedDuration}/{cookingstatus.TargetDuration}",
                            $"{recipe.ActionName}"));
                        texts.Add(FormatRate(1, cookingstatus.CookedDuration, cookingstatus.TargetDuration));
                    }

                    if (recipe.DropsAsCollection != null && recipe.DropsAsCollection.Length != 0)
                    {
                        CardOnCardAction cardOnCardAction = recipe.GetResult(cookingstatus.Card);
                        CollectionDropReport dropReport =
                            gm.GetCollectionDropsReport(cardOnCardAction, __instance, null, InGameNPCOrPlayer.PlayerAgent, true);
                        texts.Add("<size=70%>" + Action.FormatCardDropList(dropReport, __instance, indent: 2) +
                                  "</size>");
                    }
                }
            }

            bool isShowWeightType = Array.IndexOf([CardTypes.Hand, CardTypes.Item, CardTypes.Location], cardModel.CardType) > -1;
            if (isShowWeightType && (__instance.CurrentWeight(false) != 0 || cardModel.WeightReductionWhenEquipped != 0 ||
                                     (__instance.CardsInInventory != null && __instance.CardsInInventory.Count > 0)))
            {
                texts.Add(FormatWeight(__instance.CurrentWeight(false)));


                if (cardModel.CardType == CardTypes.Blueprint)
                {
                    texts.Add(FormatTooltipEntry(cardModel.BlueprintResultWeight,
                        new LocalizedString
                        {
                            LocalizationKey = "CSFFCardDetailTooltip.BlueprintResultWeight",
                            DefaultText = "BlueprintResultWeight"
                        }, 2));
                }
                else
                {
                    texts.Add(FormatTooltipEntry(Traverse.Create(cardModel).Field("ObjectWeight").GetValue<float>(), cardModel.CardName.ToString(), 2));
                    if ((bool)graphicsM && graphicsM.CharacterWindow.HasCardEquipped(__instance))
                        texts.Add(FormatTooltipEntry(cardModel.WeightReductionWhenEquipped,
                            new LocalizedString
                            {
                                LocalizationKey = "CSFFCardDetailTooltip.EquippedReduction",
                                DefaultText = "Equipped Reduction"
                            }, 2));
                }

                if (!__instance.DontCountInventoryWeight &&
                    ((__instance.CardsInInventory != null && __instance.CardsInInventory.Count > 0) ||
                     (cardModel.CanContainLiquid && __instance.ContainedLiquid)))
                {
                    texts.Add(FormatTooltipEntry(__instance.InventoryWeight(),
                        new LocalizedString
                        {
                            LocalizationKey = "CSFFCardDetailTooltip.InventoryWeight",
                            DefaultText = "Inventory Weight"
                        }, 2));
                    if (__instance.ContainedLiquid)
                        texts.Add(FormatTooltipEntry(__instance.ContainedLiquid.CurrentWeight(false),
                            __instance.ContainedLiquid.CardModel.CardName.ToString(), 4));
                    if (__instance.CardsInInventory != null)
                    {
                        if (__instance.MaxWeightCapacity > 0)
                            texts.Add(FormatBasicEntry(
                                $"{__instance.InventoryWeight(true)}/{__instance.MaxWeightCapacity}",
                                new LocalizedString
                                { LocalizationKey = "CSFFCardDetailTooltip.Capacity", DefaultText = "Capacity" },
                                indent: 4));
                        for (int i = 0; i < __instance.CardsInInventory.Count; i++)
                            if (__instance.CardsInInventory.get_Item(i) != null &&
                                !__instance.CardsInInventory.get_Item(i).IsFree)
                                texts.Add(FormatTooltipEntry(__instance.CardsInInventory.get_Item(i).CurrentWeight,
                                    $"{__instance.CardsInInventory.get_Item(i).CardAmt}x {__instance.CardsInInventory.get_Item(i).MainCard.CardModel.CardName}",
                                    4));
                    }

                    if (cardModel.CardType == CardTypes.Blueprint)
                        texts.Add(FormatTooltipEntry(-cardModel.BlueprintResultWeight,
                            new LocalizedString
                            {
                                LocalizationKey = "CSFFCardDetailTooltip.WeightReduction",
                                DefaultText = "Weight Reduction"
                            }, 4));
                    else if (cardModel.ContentWeightReduction != 0)
                        texts.Add(FormatTooltipEntry(cardModel.ContentWeightReduction,
                            $"{cardModel.CardName} {new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Reduction", DefaultText = "Reduction" }}",
                            4));
                }
            }

            foreach (PassiveEffect effect in __instance.PassiveEffects.Values)
            {
                if (string.IsNullOrWhiteSpace(effect.EffectName)) continue;
                int multiplier = effect.EffectStacksWithRequiredCards ? effect.CurrentStack : 1;
                string entryValue = effect.EffectStacksWithRequiredCards
                    ? $"{effect.CurrentStack}x {effect.EffectName}"
                    : effect.EffectName;
                if ((bool)cardModel.SpoilageTime && (bool)effect.SpoilageRateModifier)
                    baseSpoilageRate.Add(FormatRateEntry(multiplier * effect.SpoilageRateModifier.FloatValue,
                        entryValue, effect.MultiplySpoilageRate));
                if ((bool)cardModel.UsageDurability && (bool)effect.UsageRateModifier)
                    baseUsageRate.Add(FormatRateEntry(multiplier * effect.UsageRateModifier.FloatValue, entryValue, effect.MultiplyUsageRate));
                if ((bool)cardModel.FuelCapacity && (bool)effect.FuelRateModifier)
                    baseFuelRate.Add(FormatRateEntry(multiplier * effect.FuelRateModifier.FloatValue, entryValue, effect.MultiplyFuelRate));
                if ((bool)cardModel.Progress && (bool)effect.ConsumableChargesModifier)
                    baseConsumableRate.Add(FormatRateEntry(multiplier * effect.ConsumableChargesModifier.FloatValue,
                        entryValue));
                if (__instance.IsLiquidContainer && __instance.ContainedLiquid && effect.LiquidRateModifier != 0)
                    baseEvaporationRate.Add(FormatRateEntry(multiplier * effect.LiquidRateModifier, entryValue));
                if ((bool)cardModel.SpecialDurability1 && (bool)effect.Special1RateModifier)
                    baseSpecial1Rate.Add(FormatRateEntry(multiplier * effect.Special1RateModifier.FloatValue,
                        entryValue, effect.MultiplySpecial1Rate));
                if ((bool)cardModel.SpecialDurability2 && (bool)effect.Special2RateModifier)
                    baseSpecial2Rate.Add(FormatRateEntry(multiplier * effect.Special2RateModifier.FloatValue,
                        entryValue, effect.MultiplySpecial2Rate));
                if ((bool)cardModel.SpecialDurability3 && (bool)effect.Special3RateModifier)
                    baseSpecial3Rate.Add(FormatRateEntry(multiplier * effect.Special3RateModifier.FloatValue,
                        entryValue, effect.MultiplySpecial3Rate));
                if ((bool)cardModel.SpecialDurability4 && (bool)effect.Special4RateModifier)
                    baseSpecial4Rate.Add(FormatRateEntry(multiplier * effect.Special4RateModifier.FloatValue,
                        entryValue, effect.MultiplySpecial4Rate));
            }

            if (__instance.IsLiquidContainer && __instance.ContainedLiquid)
                foreach (PassiveEffect effect in __instance.ContainedLiquid.PassiveEffects.Values)
                {
                    if (effect.SpoilageRateModifier != 0)
                        baseSpoilageRate.Add(FormatRateEntry(effect.SpoilageRateModifier, effect.EffectName, effect.MultiplySpoilageRate));
                    if (effect.LiquidRateModifier != 0)
                        baseEvaporationRate.Add(FormatRateEntry(effect.LiquidRateModifier, effect.EffectName));
                }

            CookingRecipe changeRecipe = GetRecipeForCard(__instance);
            CardStateChange? recipeStateChange = changeRecipe?.IngredientChanges;

            // Spoilage
            AddDurabilityBlock(texts, __instance, cardModel, cardModel.SpoilageTime,
                __instance.CurrentSpoilage, __instance.CurrentSpoilageRate,
                recipeStateChange?.SpoilageChange, "CSFFCardDetailTooltip.Spoilage", "Spoilage",
                baseSpoilageRate, DurabilitiesTypes.Spoilage, DurabilitiesTypes.Spoilage,
                graphicsM, changeRecipe);

            // Liquid spoilage
            if (__instance.ContainedLiquid?.CardModel?.SpoilageTime != null)
            {
                AddDurabilityBlock(texts, __instance.ContainedLiquid, __instance.ContainedLiquid.CardModel,
                    __instance.ContainedLiquid.CardModel.SpoilageTime,
                    __instance.ContainedLiquid.CurrentSpoilage, __instance.ContainedLiquid.CurrentSpoilageRate,
                    recipeStateChange?.SpoilageChange, "CSFFCardDetailTooltip.Spoilage", "Spoilage",
                    baseSpoilageRate, DurabilitiesTypes.Spoilage, DurabilitiesTypes.Spoilage,
                    graphicsM, changeRecipe);
            }

            // Usage
            AddDurabilityBlock(texts, __instance, cardModel, cardModel.UsageDurability,
                __instance.CurrentUsageDurability, __instance.CurrentUsageRate,
                recipeStateChange?.UsageChange, "CSFFCardDetailTooltip.Usage", "Usage",
                baseUsageRate, DurabilitiesTypes.Usage, DurabilitiesTypes.Usage,
                graphicsM, changeRecipe);

            // Fuel
            AddDurabilityBlock(texts, __instance, cardModel, cardModel.FuelCapacity,
                __instance.CurrentFuel, __instance.CurrentFuelRate,
                recipeStateChange?.FuelChange, "CSFFCardDetailTooltip.Fuel", "Fuel",
                baseFuelRate, DurabilitiesTypes.Fuel, DurabilitiesTypes.Fuel,
                graphicsM, changeRecipe);

            // Progress
            AddDurabilityBlock(texts, __instance, cardModel, cardModel.Progress,
                __instance.CurrentProgress, __instance.CurrentConsumableRate,
                recipeStateChange?.ChargesChange, "CSFFCardDetailTooltip.Progress", "Progress",
                baseConsumableRate, DurabilitiesTypes.Progress, DurabilitiesTypes.Progress,
                graphicsM, changeRecipe);

            // Liquid container
            if (__instance.IsLiquidContainer && __instance.ContainedLiquid)
            {
                texts.Add(FormatProgressAndRate(__instance.ContainedLiquid.CurrentLiquidQuantity,
                    cardModel.MaxLiquidCapacity, __instance.ContainedLiquidModel.CardName.ToString()
                    , recipeStateChange?.ModifyLiquid ?? false ? __instance.ContainedLiquid.CurrentEvaporationRate + (recipeStateChange?.LiquidQuantityChange.x ?? 0) : __instance.ContainedLiquid.CurrentEvaporationRate));
                if (cardModel.LiquidEvaporationRate != 0)
                    texts.Add(FormatRateEntry(cardModel.LiquidEvaporationRate,
                        new LocalizedString
                        { LocalizationKey = "CSFFCardDetailTooltip.Base", DefaultText = "Base" }));
                ;
                if (baseEvaporationRate.Count > 0)
                    texts.Add(baseEvaporationRate.Join(delimiter: "\n"));
                if (__instance.CurrentProducedLiquids != null)
                    for (int i = 0; i < __instance.CurrentProducedLiquids.Count; i++)
                        if (!__instance.CurrentProducedLiquids.get_Item(i).IsEmpty &&
                            !(__instance.CurrentProducedLiquids.get_Item(i).LiquidCard !=
                              __instance.ContainedLiquidModel))
                            texts.Add(FormatRateEntry(__instance.CurrentProducedLiquids.get_Item(i).Quantity.x,
                                $"{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Producing", DefaultText = "Producing" }} {__instance.CurrentProducedLiquids.get_Item(i).LiquidCard.CardName}"));
                if ((recipeStateChange?.ModifyLiquid ?? false) && (recipeStateChange?.LiquidQuantityChange.x ?? 0) != 0)
                    texts.Add(FormatRateEntry(recipeStateChange?.LiquidQuantityChange.x ?? 0,
                        $"{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Recipe", DefaultText = "Recipe" }} {changeRecipe.ActionName}"));
            }

            // Special durabilities
            AddDurabilityBlock(texts, __instance, cardModel, cardModel.SpecialDurability1,
                __instance.CurrentSpecial1, __instance.CurrentSpecial1Rate,
                recipeStateChange?.Special1Change, "CSFFCardDetailTooltip.Special1", "SpecialDurability1",
                baseSpecial1Rate, DurabilitiesTypes.Special1, DurabilitiesTypes.Special1,
                graphicsM, changeRecipe);

            AddDurabilityBlock(texts, __instance, cardModel, cardModel.SpecialDurability2,
                __instance.CurrentSpecial2, __instance.CurrentSpecial2Rate,
                recipeStateChange?.Special2Change, "CSFFCardDetailTooltip.Special2", "SpecialDurability2",
                baseSpecial2Rate, DurabilitiesTypes.Special2, DurabilitiesTypes.Special2,
                graphicsM, changeRecipe);

            AddDurabilityBlock(texts, __instance, cardModel, cardModel.SpecialDurability3,
                __instance.CurrentSpecial3, __instance.CurrentSpecial3Rate,
                recipeStateChange?.Special3Change, "CSFFCardDetailTooltip.Special3", "SpecialDurability3",
                baseSpecial3Rate, DurabilitiesTypes.Special3, DurabilitiesTypes.Special3,
                graphicsM, changeRecipe);

            AddDurabilityBlock(texts, __instance, cardModel, cardModel.SpecialDurability4,
                __instance.CurrentSpecial4, __instance.CurrentSpecial4Rate,
                recipeStateChange?.Special4Change, "CSFFCardDetailTooltip.Special4", "SpecialDurability4",
                baseSpecial4Rate, DurabilitiesTypes.Special4, DurabilitiesTypes.Special4,
                graphicsM, changeRecipe);

            if (cardModel.IsWeapon)
            {
                texts.Add(FormatWeaponStats(cardModel.BaseClashValue, cardModel.WeaponDamage, cardModel.WeaponReach));
            }

            if (texts.Count > 0)
            {
                MyTooltip.TooltipTitle = "";
                MyTooltip.TooltipContent = "<size=75%>" + texts.Join(delimiter: "\n") + "</size>";
                MyTooltip.HoldText = "";
                MyTooltip.Priority = -1;
                Tooltip.AddTooltip(MyTooltip);
            }

        }

        private static CookingRecipe GetRecipeForCard(InGameCardBase card)
        {
            CookingRecipe recipeForCard;
            if (card.ContainedLiquid != null)
                recipeForCard = card.CurrentContainer?.CardModel?.GetRecipeForCard(card.ContainedLiquid.CardModel,
                    card.ContainedLiquid, card.CurrentContainer);
            else
                recipeForCard =
                    card.CurrentContainer?.CardModel?.GetRecipeForCard(card.CardModel, card, card.CurrentContainer);
            if (recipeForCard != null &&
                (recipeForCard.IngredientChanges.ModType == CardModifications.DurabilityChanges ||
                 (card.ContainedLiquid && recipeForCard.IngredientChanges.ModifyLiquid))) return recipeForCard;
            return null;
        }

        private static void AddDurabilityBlock(
            List<string> texts,
            InGameCardBase instance,
            CardData cardModel,
            DurabilityStat durabilityStat,
            float currentValue,
            float currentRate,
            Vector2? recipeChange,
            string defaultKey,
            string defaultText,
            List<string> baseRates,
            DurabilitiesTypes cookingType,
            DurabilitiesTypes modifierType,
            GraphicsManager graphicsM,
            CookingRecipe changeRecipe)
        {
            if (durabilityStat == null || !durabilityStat.Show(instance.ContainedLiquid, currentValue, instance))
                return;

            float maxValue = durabilityStat.MaxValue == 0 ? durabilityStat.FloatValue : durabilityStat.MaxValue;

            texts.Add(FormatProgressAndRate(currentValue, maxValue,
                string.IsNullOrEmpty(durabilityStat.CardStatName)
                    ? new LocalizedString { LocalizationKey = defaultKey, DefaultText = defaultText }
                    : durabilityStat.CardStatName,
                currentRate + (recipeChange?.x ?? 0), instance, durabilityStat));

            if (durabilityStat.RatePerDaytimePoint != 0)
                texts.Add(FormatRateEntry(durabilityStat.RatePerDaytimePoint,
                    new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Base", DefaultText = "Base" }));

            if (baseRates.Count > 0)
                texts.Add(baseRates.Join(delimiter: "\n"));

            if (instance.IsCooking())
                texts.Add(FormatRateEntry(cardModel.CookingConditions.GetRate(cookingType),
                    new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Cooking", DefaultText = "Cooking" }));

            if (cardModel.LocalCounterEffects != null)
                for (int i = 0; i < cardModel.LocalCounterEffects.Length; i++)
                    if (cardModel.LocalCounterEffects[i].IsActive(instance))
                        texts.Add(FormatRateEntry(cardModel.LocalCounterEffects[i].GetModifier(modifierType),
                            cardModel.LocalCounterEffects[i].Counter.name));

            if (durabilityStat.ExtraRateWhenEquipped != 0 && graphicsM &&
                graphicsM.CharacterWindow.HasCardEquipped(instance))
                texts.Add(FormatRateEntry(durabilityStat.ExtraRateWhenEquipped,
                    new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Equipped", DefaultText = "Equipped" }));

            if ((recipeChange?.x ?? 0) != 0)
                texts.Add(FormatRateEntry(recipeChange?.x ?? 0,
                    $"{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Recipe", DefaultText = "Recipe" }} {changeRecipe.ActionName}"));
        }
    }
}
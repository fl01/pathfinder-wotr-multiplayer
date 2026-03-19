using System;
using HarmonyLib;
using Kingmaker.Armies;
using Kingmaker.Armies.State;
using Kingmaker.Blueprints.Root;
using Kingmaker.Kingdom.Armies;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.Core.Utils;
using UnityEngine;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyMercenariesManagerPatches
    {
        [HarmonyPatch(typeof(ArmyMercenariesManager), nameof(ArmyMercenariesManager.Recruit))]
        [HarmonyPrefix]
        public static void ArmyMercenariesManager_Recruit_Prefix(ArmyData target, MercenarySlot slot)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapUnitRecruitmentOrder = new NetworkGlobalMapUnitRecruitmentOrder
            {
                ArmyId = target.ArmyStateId,
                BlueprintId = slot.Recruits.Unit.AssetGuid.ToString(),
                Count = slot.Recruits.Count,
                Type = NetworkGlobalMapUnitRecruitmentType.Mercenary,
            };

            Main.Multiplayer.OnGlobalMapRecruitmentBuyUnits(globalMapUnitRecruitmentOrder);
        }

        /// <summary>
        /// Modified version uses deterministic Random instance
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="maxAllowedSlots"></param>
        /// <returns></returns>
        [HarmonyPatch(typeof(ArmyMercenariesManager), nameof(ArmyMercenariesManager.RollSlots))]
        [HarmonyPrefix]
        public static bool ArmyMercenariesManager_RollSlots_Prefix(ArmyMercenariesManager __instance, int maxAllowedSlots)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            try
            {
                var seededContext = Main.Multiplayer.GetSeededContext();
                var identifier = $"{nameof(ArmyMercenariesManager)}.{nameof(ArmyMercenariesManager.RollSlots)}:{nameof(ArmyMercenariesManager_RollSlots_Prefix)}:_{seededContext.Id}";
                Main.GetLogger<ArmyMercenariesManagerPatches>().LogInformation("Random for Roll Mercenaries slots has been initialized. Identifer={Identifier}", identifier);
                var random = Main.Multiplayer.ValueGenerator.GetRandom(IdentifierLifetime.Persistent, identifier);

                int slotNumber = 0;
                var list = __instance.Pool.ToTempList();
                while (slotNumber < maxAllowedSlots && list.Count > 0)
                {
                    var mercenaryPoolInfo = list.WeightedRandom(random);
                    ArmyRoot armyRoot = BlueprintRoot.Instance.ArmyRoot;
                    float count = armyRoot.MercenaryDefaultCountBonus + armyRoot.MercenaryDefaultCountFormula.Roll(random) / armyRoot.MercenaryDefaultCountDivider;
                    var baseGrowth = mercenaryPoolInfo.Unit.GetArmyData().MercenariesBaseGrowths;
                    var recruitCount = Mathf.RoundToInt(baseGrowth * count);
                    var mercenaryRecruits = new MercenaryRecruits(mercenaryPoolInfo.Unit, recruitCount);
                    var slot = new MercenarySlot(mercenaryRecruits, mercenaryRecruits.OneUnitPrice * mercenaryRecruits.Count);
                    __instance.CurrentSlots.Add(slot);
                    list.Remove(mercenaryPoolInfo);
                    slotNumber++;
                }
            }
            catch (Exception ex)
            {
                Main.GetLogger<ArmyMercenariesManagerPatches>().LogError(ex, "Failed to roll mercenaries slots");
                throw;
            }

            return false;
        }
    }
}

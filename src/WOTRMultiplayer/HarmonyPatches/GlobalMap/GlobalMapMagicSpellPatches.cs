using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Crusade.GlobalMagic;
using Kingmaker.Crusade.GlobalMagic.Executors;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.State.InputManager;
using Kingmaker.Globalmap.View;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MVVM._VM.ActionBar;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class GlobalMapMagicSpellPatches
    {
        [HarmonyPatch(typeof(ArmyTarget), nameof(ArmyTarget.OnSelected))]
        [HarmonyPrefix]
        public static void ArmyTarget_OnSelected_Prefix(IGMPoint pawn, BlueprintGlobalMagicSpell.GlobalMagicData context)
        {
            if (!Main.Multiplayer.IsActive || pawn is not GlobalMapArmyPawn armyPawn)
            {
                return;
            }

            var globalMapMagicSpell = CreateSpell(context.BlueprintSpell, [armyPawn.State], null);
            Main.Multiplayer.OnGlobalMapMagicSpellUsed(globalMapMagicSpell);
        }

        [HarmonyPatch(typeof(AllArmyTarget), nameof(AllArmyTarget.RunFor))]
        [HarmonyPrefix]
        public static void AllArmyTarget_RunFor_Prefix(AllArmyTarget __instance, BlueprintGlobalMagicSpell blueprint)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armies = Game.Instance.Player.AllGlobalMaps.SelectMany(x => x.Armies.Where(__instance.m_ArmyFilter.IsValidTarget)).ToList();
            var globalMapMagicSpell = CreateSpell(blueprint, armies, null);

            Main.Multiplayer.OnGlobalMapMagicSpellUsed(globalMapMagicSpell);
        }

        [HarmonyPatch(typeof(NonTarget), nameof(NonTarget.RunFor))]
        [HarmonyPrefix]
        public static void NonTarget_RunFor_Prefix(BlueprintGlobalMagicSpell blueprintSpell)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapMagicSpell = CreateSpell(blueprintSpell, [], null);
            Main.Multiplayer.OnGlobalMapMagicSpellUsed(globalMapMagicSpell);
        }

        [HarmonyPatch(typeof(PlayerSelectedNonTarget), nameof(PlayerSelectedNonTarget.RunFor))]
        [HarmonyPrefix]
        public static void PlayerSelectedNonTarget_RunFor_Prefix(BlueprintGlobalMagicSpell blueprintSpell)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var globalMapMagicSpell = CreateSpell(blueprintSpell, [], null);
            Main.Multiplayer.OnGlobalMapMagicSpellUsed(globalMapMagicSpell);
        }

        [HarmonyPatch(typeof(SelectedArmyAndPointTarget), nameof(SelectedArmyAndPointTarget.RunActions))]
        [HarmonyPrefix]
        public static void PlayerSelectedNonTarget_RunActions_Prefix(IGMPoint point, BlueprintGlobalMagicSpell.GlobalMagicData context)
        {
            if (!Main.Multiplayer.IsActive || point is not GlobalMapPointView globalMapPointView || Game.Instance.GlobalMapController.SelectedArmy == null)
            {
                return;
            }

            var globalMapMagicSpell = CreateSpell(context.BlueprintSpell, [Game.Instance.GlobalMapController.SelectedArmy], globalMapPointView.State);
            Main.Multiplayer.OnGlobalMapMagicSpellUsed(globalMapMagicSpell);
        }

        [HarmonyPatch(typeof(ActionBarGlobalMagicSpellSlotVM), nameof(ActionBarGlobalMagicSpellSlotVM.OnMainClick))]
        [HarmonyPrefix]
        public static bool ActionBarGlobalMagicSpellSlotVM_OnMainClick_Prefix()
        {
            if (!Main.Multiplayer.IsActive || Main.Multiplayer.CanControlGlobalMap())
            {
                return true;
            }

            var message = new LocalizedString { Key = WellKnownKeys.GameNotifications.GlobalMap.DisabledActionBar.Key };
            EventBus.RaiseEvent<IWarningNotificationUIHandler>(x => x.HandleWarning(message, false));
            return false;
        }

        private static NetworkGlobalMapMagicSpell CreateSpell(BlueprintGlobalMagicSpell spell, List<GlobalMapArmyState> armies, GlobalMapPointState pointState)
        {
            var globalMagicSpell = new NetworkGlobalMapMagicSpell
            {
                Id = spell.AssetGuid.ToString(),
                Name = spell.name,
                TargetArmies = [.. armies?.Select(x => x.Id)],
                Location = pointState == null ? null : new NetworkGlobalMapLocation { Id = pointState.Blueprint.AssetGuid.ToString(), Name = pointState.Name }
            };

            return globalMagicSpell;
        }
    }
}

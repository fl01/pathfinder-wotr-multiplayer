using HarmonyLib;
using Kingmaker.Armies;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.HarmonyPatches.GlobalMap
{
    [HarmonyPatch]
    public class ArmyLeaderInfoPatches
    {
        [HarmonyPatch(typeof(ArmyLeaderInfoVM), nameof(ArmyLeaderInfoVM.OnClick))]
        [HarmonyPrefix]
        public static void ArmyLeaderInfoVM_OnClick_Prefix(ArmyLeaderInfoVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var leader = CreateLeader(__instance.m_Leader);
            Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderAction(leader, NetworkGlobalMapArmyLeaderActionType.Main);
        }

        [HarmonyPatch(typeof(ArmyLeaderInfoVM), nameof(ArmyLeaderInfoVM.OnLevelUp))]
        [HarmonyPrefix]
        public static void ArmyLeaderInfoVM_OnLevelUp_Prefix(ArmyLeaderInfoVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var leader = CreateLeader(__instance.m_Leader);
            Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderAction(leader, NetworkGlobalMapArmyLeaderActionType.LevelUp);
        }

        [HarmonyPatch(typeof(ArmyLeaderInfoVM), nameof(ArmyLeaderInfoVM.OnLookAtLeaderPool))]
        [HarmonyPrefix]
        public static void ArmyLeaderInfoVM_OnLookAtLeaderPool_Prefix(ArmyLeaderInfoVM __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            var armyInfo = Main.UIAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
            var actionType = armyInfo?.m_MainArmyCartView?.m_LeaderInfoView?.ViewModel == __instance ? NetworkGlobalMapArmyLeaderActionType.MainLookAtPool : NetworkGlobalMapArmyLeaderActionType.MergeLookAtPool;
            var leader = CreateLeader(__instance.m_Leader);
            Main.Multiplayer.OnGlobalMapCrusadeArmyLeaderAction(leader, actionType);
        }

        private static NetworkGlobalMapArmyLeader CreateLeader(ArmyLeader armyLeader)
        {
            if (armyLeader == null)
            {
                return null;
            }

            var globalMapArmyLeader = new NetworkGlobalMapArmyLeader
            {
                Id = armyLeader.Guid,
                BlueprintId = armyLeader.Blueprint.AssetGuid.ToString(),
            };

            return globalMapArmyLeader;
        }
    }
}

using System.Linq;
using HarmonyLib;
using Kingmaker.TurnBasedMode;
using Kingmaker.UnitLogic.Commands;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Combat;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitAttackPatches
    {
        [HarmonyPatch(typeof(UnitAttack), nameof(UnitAttack.OnStart))]
        [HarmonyPostfix]
        public static void UnitAttack_OnStart_Postfix(UnitAttack __instance)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return;
            }

            OnUnitAttack(__instance);
        }

        private static void OnUnitAttack(UnitAttack command)
        {
            var path = PathVisualizer.Instance.CurrentPathForUnit(command.Executor.View);
            var networkPath = path?.vectorPath.Select(v => new NetworkVector3(v.x, v.y, v.z)).ToList();
            var networkAbility = new NetworkUnitAttack
            {
                ExecutorUnitId = command.Executor.UniqueId,
                TargetUnitId = command.TargetUnit?.UniqueId,
                IsFullAttack = command.IsAttackFull,
                VectorPath = networkPath
            };

            Main.Multiplayer.OnUnitAttack(networkAbility);
        }
    }
}

using System;
using HarmonyLib;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.Combat
{
    [HarmonyPatch]
    public class UnitCommandsPatches
    {
        [HarmonyPatch(typeof(UnitCommands), nameof(UnitCommands.Run), [typeof(UnitCommand)])]
        [HarmonyPrefix]
        public static bool UnitCommands_Run_Prefix(UnitCommand cmd)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            return cmd is not UnitAttack || cmd.CreatedByPlayer || cmd.Executor == null;
        }

        [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.Interrupt))]
        [HarmonyPrefix]
        public static void UnitAttack_OnStart_Prefix(UnitCommand __instance)
        {
            if (!Main.Multiplayer.IsActive || __instance is not UnitAttack attack || !attack.CreatedByPlayer)
            {
                return;
            }

            Main.GetLogger<UnitAttackPatches>().LogWarning("Interrupting attack command. AttackIndex={AttackIndex}, AttackCount={AttackCount}, StackTrace={StackTrace}", attack.m_AttackIndex, attack.m_AllAttacks.Count, Environment.StackTrace);
        }
    }
}

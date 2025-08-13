using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Area;
using Kingmaker.Blueprints.Root;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.MapObjectOvertip;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.View.MapObjects;

namespace WOTRMultiplayer.HarmonyPatches.MapObjects
{
    [HarmonyPatch]
    public class AreaTransitionPatches
    {
        [HarmonyPatch(typeof(AreaTransitionGroupCommand), nameof(AreaTransitionGroupCommand.ExecuteTransition))]
        [HarmonyPrefix]
        public static bool AreaTransitionGroupCommand_ExecuteTransition_Prefix(AreaTransitionGroupCommand __instance, AreaTransitionPart areaTransition)
        {
            if (!Main.Multiplayer.IsActive)
            {
                return true;
            }

            // transpiler seems to be harded to support in this case
            ExecuteTransitionEx(areaTransition);

            return false;
        }

        private static void ExecuteTransitionEx(AreaTransitionPart areaTransition)
        {
            if (AreaTransitionController.CanNotMove(areaTransition, false))
            {
                return;
            }
            UnitEntityData unitEntityData = (from u in Game.Instance.Player.PartyAndPets
                                             where CommonTranspilerReplacements.IsControlledByPlayers(u)
                                             where !u.IsPet
                                             select u).FirstOrDefault<UnitEntityData>();
            if (areaTransition.CheckRestrictions(unitEntityData))
            {
                ConditionAction conditionAction;
                if (areaTransition.Blueprint == null)
                {
                    conditionAction = null;
                }
                else
                {
                    conditionAction = areaTransition.Blueprint.Actions.FirstOrDefault(delegate (ConditionAction ca)
                    {
                        Condition condition = ca.Condition;
                        return condition == null || condition.Check(null);
                    });
                }
                ConditionAction conditionAction2 = conditionAction;
                if (conditionAction2 != null)
                {
                    conditionAction2.Actions.Run();
                    return;
                }
                BlueprintArea currentArea = Game.Instance.CurrentlyLoadedArea;
                BlueprintAreaEnterPoint targetEnterPoint = areaTransition.AreaEnterPoint;
                if (Game.Instance.State.LoadedAreaState.Encounter == null && targetEnterPoint.Area.IsGlobalMap)
                {
                    BlueprintGlobalMap globalMap = BlueprintRoot.Instance.GlobalMap.GetGlobalMap(targetEnterPoint);
                    if (globalMap != null)
                    {
                        Game.Instance.Player.GetGlobalMap(globalMap).Player.AreaReturnPoint = areaTransition.GetEnterPointToReturnTo();
                    }
                }
                EventBus.RaiseEvent<IPartyLeaveAreaHandler>(delegate (IPartyLeaveAreaHandler h)
                {
                    h.HandlePartyLeaveArea(currentArea, targetEnterPoint, areaTransition);
                }, true);
                Game.Instance.LoadArea(areaTransition.AreaEnterPoint, areaTransition.Settings.AutoSaveMode, null);
            }
        }
    }
}

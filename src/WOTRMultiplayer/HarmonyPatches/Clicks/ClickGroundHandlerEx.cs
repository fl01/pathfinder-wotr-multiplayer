using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Formations;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Owlcat.Runtime.Core.Utils;
using TurnBased.Controllers;
using UnityEngine;

namespace WOTRMultiplayer.HarmonyPatches.Clicks
{
    public static class ClickGroundHandlerEx
    {
        /// <summary>
        /// copy paste of ClickGroundHandler.MoveSelectedUnitsToPoint, but with some adjusments
        /// still requires a bit of refactoring after decompiler
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <param name="direction"></param>
        /// <param name="preview"></param>
        /// <param name="showTargetMarker"></param>
        /// <param name="formationSpaceFactor"></param>
        /// <param name="ignoreHold"></param>
        /// <param name="commandRunner"></param>
        public static void MoveSelectedUnitsToPoint(Vector3 worldPosition, Vector3 direction, bool preview = false, bool showTargetMarker = true, float formationSpaceFactor = 1f, bool ignoreHold = true, Action<UnitEntityData, ClickGroundHandler.CommandSettings> commandRunner = null)
        {
            if (!preview)
            {
                ClickGroundHandler.m_UnitWaitAgentList.Clear();
            }
            UnitEntityData unitEntityData = Game.Instance.SelectionCharacter.SingleSelectedUnit;
            if (!Game.Instance.IsControllerMouse)
            {
                unitEntityData = unitEntityData?.GetRider() ?? unitEntityData;
            }
            UnityEngine.Object @object;
            if (unitEntityData == null)
            {
                @object = null;
            }
            else
            {
                UnitEntityView unitEntityView = unitEntityData.View.Or(null);
                @object = unitEntityView?.AgentOverride;
            }
            bool flag = @object != null;
            UnitPartRider unitPartRider = unitEntityData?.RiderPart;
            bool flag2 = unitPartRider != null && unitPartRider && !unitEntityData.RiderPart.SaddledUnit.Descriptor.State.CanMove;
            bool flag3 = unitEntityData != null && !unitEntityData.Descriptor.State.CanMove || flag2;
            bool flag4;
            if (CombatController.IsInTurnBasedCombat())
            {
                TurnController currentTurn = Game.Instance.TurnBasedCombatController.CurrentTurn;
                flag4 = currentTurn != null && currentTurn.UnitCanGetUpOnCommand.Value;
            }
            else
            {
                flag4 = false;
            }
            bool flag5 = flag4;
            if (Game.Instance.IsControllerGamepad && unitEntityData != null && (!CommonTranspilerReplacements.IsControlledByPlayers(unitEntityData) || flag3 && !flag5))
            {
                return;
            }
            if (CombatController.IsInTurnBasedCombat() && flag3 && !flag5)
            {
                return;
            }
            IPartyFormation currentFormation = Game.Instance.Player.FormationManager.CurrentFormation;
            List<UnitEntityData> allUnits = [.. Game.Instance.SelectionCharacter.ActualGroup.Where(u => CommonTranspilerReplacements.IsControlledByPlayers(u) && u.SaddledPart == null)];
            float num = Mathf.Atan2(direction.x, direction.z) * 57.29578f;
            float? num2 = null;
            if (allUnits.Count > 1 && !Game.Instance.Player.IsInCombat)
            {
                num2 = allUnits.Min(u => u.ModifiedSpeedMps);
                float num3 = 30.Feet().Meters / 3f;
                float? num4 = num2;
                float num5 = num3;
                if (num4.GetValueOrDefault() < num5 & num4 != null)
                {
                    num2 = new float?(num3);
                }
            }
            if (allUnits.Count > 0)
            {
                int[] array = new int[allUnits.Count];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = i;
                }
                Array.Sort(array, (o1, o2) => (allUnits[o1].Position - worldPosition).sqrMagnitude.CompareTo((allUnits[o2].Position - worldPosition).sqrMagnitude));
                int num6 = 0;
                PartyFormationHelper.FillFormationPositions(worldPosition, FormationAnchor.Front, direction, allUnits, allUnits, currentFormation, formationSpaceFactor, false);
                UnitEntityData unitEntityData2 = currentFormation.Tank ?? Game.Instance.Player.PartyAndPets.FirstItem();
                bool flag6 = allUnits.Contains(unitEntityData2);
                float num7 = currentFormation.Length + 1f;
                float num8 = num2 == null ? unitEntityData2.ModifiedSpeedMps : Math.Min(num2.Value, unitEntityData2.ModifiedSpeedMps);
                for (int j = 0; j < allUnits.Count; j++)
                {
                    if ((!Game.Instance.IsControllerGamepad || !(allUnits[j] == unitEntityData) || !flag) && allUnits.HasItem(allUnits[j]))
                    {
                        UnitEntityData unitEntityData3 = allUnits[j];
                        Vector3 vector = PartyFormationHelper.ResultPositions[j];
                        if (preview)
                        {
                            ClickGroundHandler.ShowDestination(allUnits[j], vector, true);
                        }
                        else
                        {
                            commandRunner ??= new Action<UnitEntityData, ClickGroundHandler.CommandSettings>(ClickGroundHandler.RunCommand);
                            float? num9 = null;
                            if (Game.Instance.Player.FormationManager.GetPreserveFormation() && Game.Instance.Player.IsInCombat && flag6 && (unitEntityData2.Position - unitEntityData3.Position).sqrMagnitude <= num7)
                            {
                                num9 = new float?(num8);
                            }
                            if (CombatController.IsInTurnBasedCombat() || !allUnits[j].HoldState || ignoreHold)
                            {
                                Action<UnitEntityData, ClickGroundHandler.CommandSettings> action = commandRunner;
                                UnitEntityData unitEntityData4 = unitEntityData3;
                                ClickGroundHandler.CommandSettings commandSettings = default;
                                commandSettings.Destination = vector;
                                float? num4 = num9;
                                commandSettings.SpeedLimit = num4 != null ? num4 : num2;
                                commandSettings.ApplySpeedLimitInCombat = num9 != null;
                                commandSettings.Orientation = num;
                                commandSettings.Delay = array[num6] * 0.05f;
                                commandSettings.ShowTargetMarker = showTargetMarker;
                                commandSettings.MoveContiniously = flag;
                                action(unitEntityData4, commandSettings);
                            }
                        }
                        num6++;
                    }
                }
                float num10 = 0f;
                for (int k = 0; k < allUnits.Count; k++)
                {
                    if (allUnits.HasItem(allUnits[k]))
                    {
                        float magnitude = (worldPosition - PartyFormationHelper.ResultPositions[k]).To2D().magnitude;
                        if (magnitude > num10)
                        {
                            num10 = magnitude;
                        }
                    }
                }
                foreach (UnitEntityData unitEntityData5 in allUnits)
                {
                    if (!allUnits.HasItem(unitEntityData5) && (!Game.Instance.IsControllerGamepad || !(unitEntityData5 == unitEntityData) || !flag))
                    {
                        Vector3 vector2 = allUnits.Count == 1 ? worldPosition : GeometryUtils.ProjectToGround(worldPosition - direction.normalized * (num10 + 2f));
                        if (preview)
                        {
                            ClickGroundHandler.ShowDestination(unitEntityData5, vector2, true);
                        }

                        else
                        {
                            commandRunner ??= new Action<UnitEntityData, ClickGroundHandler.CommandSettings>(ClickGroundHandler.RunCommand);
                            Action<UnitEntityData, ClickGroundHandler.CommandSettings> action2 = commandRunner;
                            UnitEntityData unitEntityData6 = unitEntityData5;
                            ClickGroundHandler.CommandSettings commandSettings = new()
                            {
                                Destination = vector2,
                                SpeedLimit = num2,
                                Orientation = num,
                                Delay = 0f,
                                ShowTargetMarker = showTargetMarker,
                                MoveContiniously = flag
                            };
                            action2(unitEntityData6, commandSettings);
                        }
                    }
                }
                if (preview)
                {
                    ClickPointerManager clickPointerManager = Game.Instance.UI.ClickPointerManager;
                    clickPointerManager?.ShowPreviewArrow(worldPosition, direction);
                }
                else
                {
                    ClickPointerManager clickPointerManager2 = Game.Instance.UI.ClickPointerManager;
                    clickPointerManager2?.CancelPreview();
                }
                EventBus.RaiseEvent(delegate (IClickActionHandler h)
                {
                    h.OnMoveRequested(worldPosition);
                }, true);
            }
        }
    }
}

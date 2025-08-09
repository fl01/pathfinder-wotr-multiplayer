using System;
using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.MapObjects;

namespace WOTRMultiplayer.GameInteraction.Contexts
{
    public class RemoteExecutionContext : IDisposable
    {
        public List<UnitEntityData> SelectedUnits { get; set; }

        public PerceptionCheckRemoteContext PerceptionCheck { get; set; }

        public DropItemRemoteContext DropItem { get; set; }

        public EquipmentRemoteContext Equipment { get; set; }

        public HandEquipmentRemoteContext HandEquipment { get; set; }

        public void Dispose()
        {
            SelectedUnits = null;
            PerceptionCheck = null;
            DropItem = null;
            Equipment = null;
            HandEquipment = null;
        }

        public static RemoteExecutionContext CreateDropItem(string itemId, string unitId)
        {
            return new RemoteExecutionContext
            {
                DropItem = new DropItemRemoteContext
                {
                    ItemId = itemId,
                    UnitId = unitId
                }
            };
        }

        public static RemoteExecutionContext Create(NetworkEquipmentSlotPosition position)
        {
            return new RemoteExecutionContext
            {
                Equipment = new EquipmentRemoteContext
                {
                    Position = position
                }
            };
        }

        public static RemoteExecutionContext Create(NetworkActiveHandEquipmentSet set)
        {
            return new RemoteExecutionContext
            {
                HandEquipment = new HandEquipmentRemoteContext
                {
                    UnitId = set.UnitId,
                    Index = set.Index
                }
            };
        }

        public static RemoteExecutionContext Create(NetworkPerceptionCheck check)
        {
            return new RemoteExecutionContext
            {
                PerceptionCheck = new PerceptionCheckRemoteContext(check.UnitId, check.MapObject.Id)
            };
        }

        public static RemoteExecutionContext Create(List<UnitEntityData> selectedUnits)
        {
            return new RemoteExecutionContext { SelectedUnits = selectedUnits };
        }
    }
}

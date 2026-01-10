using System;
using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.Inspect;
using WOTRMultiplayer.Entities.Items;

namespace WOTRMultiplayer.Services.GameInteraction.Contexts
{
    public class RemoteExecutionContext : IDisposable
    {
        public List<UnitEntityData> SelectedUnits { get; set; }

        public PerceptionCheckContext PerceptionCheck { get; set; }

        public DropItemContext DropItem { get; set; }

        public UseInventoryItemContext UseInventoryItem { get; set; }

        public EquipmentContext Equipment { get; set; }

        public HandEquipmentContext HandEquipment { get; set; }

        public NetworkRandomEncounterContext RandomEncounter { get; set; }

        public OvertipInteractionContext Overtip { get; set; }

        public VendorItemTransferContext VendorItemTransfer { get; set; }

        public MapObjectLockpickContext Lockpick { get; set; }

        public PolymorphicItemContext PolymorphicItem { get; set; }

        public void Dispose()
        {
            SelectedUnits = null;
            PerceptionCheck = null;
            DropItem = null;
            UseInventoryItem = null;
            Equipment = null;
            HandEquipment = null;
            RandomEncounter = null;
            Overtip = null;
            VendorItemTransfer = null;
            Lockpick = null;
            PolymorphicItem = null;
        }

        public static RemoteExecutionContext CreateDropItem(string itemId, string unitId)
        {
            return new RemoteExecutionContext
            {
                DropItem = new DropItemContext
                {
                    ItemId = itemId,
                    UnitId = unitId
                }
            };
        }

        public static RemoteExecutionContext CreateUseInventoryItem(string itemId, string userUnitId)
        {
            return new RemoteExecutionContext
            {
                UseInventoryItem = new UseInventoryItemContext
                {
                    ItemId = itemId,
                    UserUnitId = userUnitId
                }
            };
        }

        public static RemoteExecutionContext Create(NetworkEquipmentSlotPosition position)
        {
            return new RemoteExecutionContext
            {
                Equipment = new EquipmentContext
                {
                    Position = position
                }
            };
        }

        public static RemoteExecutionContext Create(NetworkActiveHandEquipmentSet set)
        {
            return new RemoteExecutionContext
            {
                HandEquipment = new HandEquipmentContext
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
                PerceptionCheck = new PerceptionCheckContext(check.UnitId, check.MapObject.Id)
            };
        }

        public static RemoteExecutionContext Create(List<UnitEntityData> selectedUnits)
        {
            return new RemoteExecutionContext { SelectedUnits = selectedUnits };
        }

        public static RemoteExecutionContext Create(NetworkRandomEncounterContext encounterContext)
        {
            return new RemoteExecutionContext { RandomEncounter = encounterContext };
        }

        public static RemoteExecutionContext CreateVendorItemTransfer(string itemId)
        {
            return new RemoteExecutionContext
            {
                VendorItemTransfer = new VendorItemTransferContext
                {
                    ItemId = itemId
                }
            };
        }

        public static RemoteExecutionContext Create(NetworkPolymorphicItem polymorphicItem)
        {
            return new RemoteExecutionContext
            {
                PolymorphicItem = new PolymorphicItemContext(polymorphicItem.UnitId, polymorphicItem.Item.Name)
            };
        }
    }
}

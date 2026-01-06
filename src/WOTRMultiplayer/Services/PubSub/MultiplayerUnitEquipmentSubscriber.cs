using System;
using Kingmaker.ElementsSystem;
using Kingmaker.GameModes;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.PubSub;
using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.Services.PubSub
{
    public class MultiplayerUnitEquipmentSubscriber : MultiplayerSubscriberBase,
        IMultiplayerGlobalSubscriber,
        IUnitEquipmentHandler,
        IUnitActiveEquipmentSetHandler
    {
        private readonly IGameInteractionService _gameInteractionService;

        public MultiplayerUnitEquipmentSubscriber(
            ILogger<MultiplayerUnitEquipmentSubscriber> logger,
            IGameInteractionService gameInteractionService,
            IMultiplayerActorAccessor multiplayerActorAccessor)
            : base(logger, multiplayerActorAccessor)
        {
            _gameInteractionService = gameInteractionService;
        }

        public void HandleEquipmentSlotUpdated(ItemSlot slot, ItemEntity previousItem)
        {
            if (ActorAccessor.Current == null || _gameInteractionService.CurrentGameMode == GameModeType.None)
            {
                return;
            }

            var position = _gameInteractionService.GetEquipmentSlotPosition(slot);
            if (position == null || position.Index == -1)
            {
                return;
            }

            var swapContext = ContextData<ItemsCollection.SwapItems>.Current;
            var equipmentSwapContext = swapContext == null ? null : new NetworkEquipmentSwapContext
            {
                From = _gameInteractionService.GetEquipmentSlotPosition(swapContext.From),
                To = _gameInteractionService.GetEquipmentSlotPosition(swapContext.To)
            };

            var networkSlot = new NetworkEquipmentSlot
            {
                Item = slot.HasItem ? NetworkItem.FromItemEntity(slot.Item) : null,
                SwapContext = equipmentSwapContext,
                OwnerId = slot.Owner.Unit.UniqueId,
                Position = position
            };

            var equipmentContext = _gameInteractionService.RemoteContext?.Equipment;
            if (equipmentContext != null && equipmentContext.Position.Type == position.Type && equipmentContext.Position.Index == position.Index)
            {
                return;
            }

            ActorAccessor.Current.OnEquipmentSlotChanged(networkSlot);
        }

        public void HandleUnitChangeActiveEquipmentSet(UnitDescriptor unit)
        {
            if (ActorAccessor.Current == null)
            {
                return;
            }

            var networkActiveHandEquipmentSet = new NetworkActiveHandEquipmentSet
            {
                Index = unit.Body.CurrentHandEquipmentSetIndex,
                UnitId = unit.Unit.UniqueId,
            };

            var context = _gameInteractionService.RemoteContext?.HandEquipment;
            if (context != null
                && context.Index == networkActiveHandEquipmentSet.Index
                && string.Equals(context.UnitId, networkActiveHandEquipmentSet.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ActorAccessor.Current.OnChangeActiveHandEquipmentSet(networkActiveHandEquipmentSet);
        }
    }
}

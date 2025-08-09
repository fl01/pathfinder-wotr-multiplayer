using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Pubsub;
using WOTRMultiplayer.MP.Entities.Equipment;

namespace WOTRMultiplayer.PubSub
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
            if (ActorAccessor.Current == null)
            {
                return;
            }

            var position = _gameInteractionService.GetEquipmentSlotPosition(slot);
            if (position == null || position.Index == -1)
            {
                return;
            }

            var networkSlot = new NetworkEquipmentSlot
            {
                Item = slot.HasItem ? NetworkItem.FromItemEntity(slot.Item) : null,
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

            var set = new NetworkActiveHandEquipmentSet
            {
                Index = unit.Body.CurrentHandEquipmentSetIndex,
                UnitId = unit.Unit.UniqueId,
            };

            Main.Multiplayer.OnChangeActiveHandEquipmentSet(set);
        }
    }
}

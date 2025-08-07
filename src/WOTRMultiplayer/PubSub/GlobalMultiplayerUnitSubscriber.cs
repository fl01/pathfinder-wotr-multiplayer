using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.MP.Actors;
using WOTRMultiplayer.Abstractions.PubSub;
using WOTRMultiplayer.MP.Entities.Equipment;

namespace WOTRMultiplayer.PubSub
{
    public class GlobalMultiplayerUnitSubscriber : GlobalMultiplayerSubscriberBase,
        IGlobalMultiplayerUnitCommandSubscriber,
        IGlobalSubscriber,
        ISubscriber,
        IUnitEquipmentHandler,
        IUnitActiveEquipmentSetHandler
    {
        private readonly IGameInteractionService _gameInteractionService;

        public GlobalMultiplayerUnitSubscriber(
            ILogger<GlobalMultiplayerUnitSubscriber> logger,
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
                ItemId = slot.HasItem ? slot.Item.UniqueId : null,
                OwnerId = slot.Owner.Unit.UniqueId,
                Position = position
            };

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

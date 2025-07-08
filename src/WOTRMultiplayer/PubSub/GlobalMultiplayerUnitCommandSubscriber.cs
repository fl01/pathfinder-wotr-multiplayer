using Kingmaker;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.PubSub;

namespace WOTRMultiplayer.PubSub
{
    public class GlobalMultiplayerUnitCommandSubscriber : GlobalMultiplayerSubscriberBase,
        IGlobalMultiplayerUnitCommandSubscriber,
        IGlobalSubscriber,
        ISubscriber,
        IUnitCommandActHandler
    {
        public GlobalMultiplayerUnitCommandSubscriber(
            ILogger<GlobalMultiplayerUnitCommandSubscriber> logger,
            IMultiplayerHost multiplayerHost,
            IMultiplayerClient multiplayerClient)
            : base(logger, multiplayerHost, multiplayerClient)
        {
        }

        public void HandleUnitCommandDidAct(UnitCommand command)
        {
            var multiplayerParticipant = GetMultiplayerParticipant();
            if (multiplayerParticipant == null)
            {
                return;
            }

            if (!Game.Instance.Player.IsInCombat)
            {
                return;
            }

            // this event is not reliable for movement, e.g. doesn't fire when you click to move far away or move(not attack) to enemy in combat
            // so movement commands are handled by separate harmony patch
            if (command is UnitMoveTo)
            {
                return;
            }

            Logger.LogInformation("Unit did act. CommandType={commandType}, UnitId={unitId}, CharacterName={characterName}", command.GetType().Name, command.Executor?.UniqueId, command.Executor?.CharacterName);
        }
    }
}

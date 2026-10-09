using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyDialogPopupAccepted)]
    public class NotifyDialogPopupAccepted : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkDialogPopup Popup { get; set; }
    }
}

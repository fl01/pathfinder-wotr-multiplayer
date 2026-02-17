using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyDialogPopupAccepted)]
    public class NotifyDialogPopupAccepted
    {
        [ProtoMember(1)]
        public NetworkDialogPopup Popup { get; set; }
    }
}

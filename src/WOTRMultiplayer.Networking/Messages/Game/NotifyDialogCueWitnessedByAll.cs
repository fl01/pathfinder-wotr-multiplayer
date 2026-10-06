using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyDialogCueWitnessedByAll)]
    public class NotifyDialogCueWitnessedByAll
    {
        [ProtoMember(1)]
        [LogMe]
        public string CueName { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkDialog Dialog { get; set; }
    }
}

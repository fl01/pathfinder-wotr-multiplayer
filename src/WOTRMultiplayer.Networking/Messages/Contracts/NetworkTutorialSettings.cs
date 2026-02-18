using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTutorialSettings
    {
        [ProtoMember(1)]
        [LogMe]
        public bool ShowArmiesTutorial { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool ShowBasicTutorial { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool ShowContextTutorial { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool ShowControlsAdvancedTutorial { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public bool ShowControlsBasicTutorial { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public bool ShowCrusadeTutorial { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public bool ShowGameplayAdvancedTutorial { get; set; }

        [ProtoMember(8)]
        [LogMe]
        public bool ShowGameplayBasicTutorial { get; set; }

        [ProtoMember(9)]
        [LogMe]
        public bool ShowPathfinderRulesTutorial { get; set; }
    }
}

using CommandLine;
using WOTRMultiplayer.MP.Entities.Equipment;

namespace WOTRMultiplayer.Playground.Client
{
    /// <summary>
    /// verbs are listed in the same order
    /// </summary>
    public class CommandVerbs
    {
        [Verb("connect", HelpText = "connect to specified host, default is 127.0.0.1:1024")]
        public class ConnectCommandVerb
        {
            [Option('s', "server", Required = false, Default = "127.0.0.1:1024")]
            public string ServerAddress { get; set; }
        }

        [Verb("ready", HelpText = "triggers ready status change")]
        public class ReadyCommandVerb
        {
        }

        [Verb("loaded", HelpText = "send gameloaded to host")]
        public class ClientLoadedCommandVerb
        {
        }

        [Verb("dialog-witness-cue", HelpText = "witness cue")]
        public class DialogWitnessCueCommandVerb
        {
            [Option('c', "cue", Required = true, HelpText = "e.g. Cue_0001, Cue_0002 etc")]
            public string Cue { get; set; }

            [Option('d', "dialog-name", Required = false, Default = "MeetSeelahAnevia_Dialogue")]
            public string DialogName { get; set; }

            [Option('s', "system", Required = false, Default = false, HelpText = "Determines if continue(system) button should be disabled, doesn't matter in the playground env")]
            public bool HasSystemAnswer { get; set; }
        }

        [Verb("dialog-suggest-cue", HelpText = "suggest cue answer")]
        public class DialogSuggestCueCommandVerb
        {
            [Option('c', "cue", Required = true, HelpText = "e.g. Cue_0001, Cue_0002 etc")]
            public string Cue { get; set; }

            [Option('a', "answer", Required = true, HelpText = "e.g. Answer_0007 etc")]
            public string Answer { get; set; }

            [Option('d', "dialog-name", Required = false, Default = "MeetSeelahAnevia_Dialogue")]
            public string DialogName { get; set; }

            [Option('e', "exit", Required = false, Default = false, HelpText = "Doesn't matter for a client")]
            public bool IsExitAnswer { get; set; }

            [Option('u', "unit", Required = false, Default = false, HelpText = "Doesn't matter for a client")]
            public string ManualUnitSelectionId { get; set; }
        }

        [Verb("dialog-start", HelpText = "Send request to host to start dialog")]
        public class DialogStartCommandVerb
        {
            [Option('d', "dialog-name", Required = false, Default = "Vendor_Quartermaster_Dialogue")]
            public string DialogName { get; set; }

            [Option('t', "target", Required = false, HelpText = "Unit UniqueId")]
            public string TargetUnitId { get; set; }

            [Option('i', "initiator", Required = false, HelpText = "Unit UniqueId")]
            public string InitiatorUnitId { get; set; }

            [Option('m', "map-object", Required = false, HelpText = "Map object UniqueId")]
            public string MapObjectId { get; set; }

            [Option('s', "speaker", Required = false, HelpText = "some localization related stuff, not sure how to properly use yet")]
            public string SpeakerKey { get; set; }
        }

        [Verb("combat-started", HelpText = "Initialize combat")]
        public class CombatStartedCommandVerb
        {
        }


        [Verb("combat-round", HelpText = "Set combat round")]
        public class CombatRoundCommandVerb
        {
            [Option('r', "round", Required = true, Min = 1, Max = int.MaxValue, HelpText = "1 - int.Max")]
            public int Round { get; set; }
        }

        [Verb("combat-turn-started", HelpText = "Send turn initialization to host")]
        public class CombatTurnStartedCommandVerb
        {
            [Option('u', "unit", Required = true, HelpText = "Unit UniqueId")]
            public string UnitId { get; set; }

            [Option('s', "surprise", Required = false, Default = false, HelpText = "Doesn't matter in the playground env")]
            public bool IsSurpriseRound { get; set; }
        }

        [Verb("combat-turn-ended", HelpText = "Send turn ended if you are a turn owner (localplayer)")]
        public class CombatTurnEndedCommandVerb
        {
            [Option('u', "unit", Required = true, HelpText = "Unit UniqueId")]
            public string UnitId { get; set; }
        }


        [Verb("equipment", HelpText = "send equipment update")]
        public class EquipmentSlotUpdateCommandVerb
        {
            [Option('t', "slot-type", Required = false, Default = NetworkEquipmentSlotType.EquipmentSlotWrist, HelpText = "Look at NetworkEquipmentSlotType enum")]
            public NetworkEquipmentSlotType SlotType { get; set; }

            [Option('s', "slot-index", Required = false, Default = 0, HelpText = "Slot index")]
            public int SlotIndex { get; set; }

            [Option('i', "item", Required = false, Default = null, HelpText = "ItemId, null = remove item, not null = equip item, e.g. fd68e547-2f65-43ba-be0d-2e14b505372a")]
            public string ItemId { get; set; }

            [Option('u', "unit", Required = false, Default = "a950ad75-65cd-4dc1-96e9-444e291fed7e", HelpText = "UnitId")]
            public string UnitId { get; set; }
        }

        [Verb("equipment-hand", HelpText = "change active hand slot index")]
        public class EquipmentActiveHandSlotUpdateCommandVerb
        {
            [Option('s', "slot-index", Required = false, Default = 0, HelpText = "Hand slot index (0-3)")]
            public int SlotIndex { get; set; }

            [Option('u', "unit", Required = false, Default = "a950ad75-65cd-4dc1-96e9-444e291fed7e", HelpText = "UnitId")]
            public string UnitId { get; set; }
        }

        [Verb("exit", HelpText = "cya next time")]
        public class ExitCommandVerb
        {
        }
    }
}

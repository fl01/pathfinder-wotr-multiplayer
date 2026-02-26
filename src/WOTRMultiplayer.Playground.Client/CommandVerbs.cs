using CommandLine;
using Kingmaker.GameModes;
using WOTRMultiplayer.Entities.Equipment;

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

        [Verb("loaded", HelpText = "send AreaLoaded")]
        public class AreaLoadedCommandVerb
        {
        }

        [Verb("show-restview", HelpText = "RestPhase.ShowingResults means rest ended")]
        public class ShowRestViewCommandVerb
        {
            [Option('p', "phase", Default = Kingmaker.Controllers.Rest.RestPhase.ShowingResults)]
            public Kingmaker.Controllers.Rest.RestPhase Phase { get; set; }
        }

        [Verb("start-game-mode", HelpText = "send ClientGameModeStarted notification to host")]
        public class GameModeStartedCommandVerb
        {
            [Option('m', "mode", Default = GameModeType.Enum.Rest)]
            public GameModeType.Enum GameModeTypeId { get; set; }
        }

        [Verb("end-game-mode", HelpText = "send ClientGameModeEnded notification to host")]
        public class GameModeEndedCommandVerb
        {
            [Option('m', "mode", Default = GameModeType.Enum.Rest)]
            public GameModeType.Enum GameModeTypeId { get; set; }
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

        [Verb("leveling-witness", HelpText = "send equipment update")]
        public class LevelingWitnessCommandVerb
        {
            [Option('i', "index", Required = true, HelpText = "Screen index of leveling")]
            public int Index { get; set; }
        }

        [Verb("dump-locale", HelpText = "print test locale")]
        public class DumpLocaleCommandVerb
        {
        }

        [Verb("exit", HelpText = "cya next time")]
        public class ExitCommandVerb
        {
        }
    }
}

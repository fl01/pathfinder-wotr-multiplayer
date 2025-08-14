using System.Collections.Generic;
using TurnBased.Controllers;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkClick
    {
        public NetworkVector3 WorldPosition { get; set; }

        public string TargetUnitId { get; set; }

        public string MapObjectId { get; set; }

        public bool IsLootBagMapObject { get; set; }

        public int Button { get; set; }

        public bool MuteEvents { get; set; }

        public List<string> SelectedUnits { get; set; } = [];

        public List<NetworkVector3> VectorPath { get; set; } = [];

        public NetworkActionsState ActionsState { get; set; }

        public bool IsTurnBasedModeClick { get; set; }

        public TurnController.AttackMode? AttackMode { get; set; }
    }
}

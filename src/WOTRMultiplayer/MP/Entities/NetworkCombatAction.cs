namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkCombatAction
    {
        public string MovementActivityStatePredicted { get; set; }

        public string MovementActivityStateCurrent { get; set; }

        public string AttackActivityStatePredicted { get; set; }

        public string AttackActivityStateCurrent { get; set; }

        public string AbilityActivityStatePredicted { get; set; }

        public string AbilityActivityStateCurrent { get; set; }

        public bool LockType { get; set; }

        public bool HasMovePossibility { get; set; }

        public float? MaxMoveDistance { get; set; }

        public float? RemainingMoveDistance { get; set; }

        public float? PredictedMoveDistance { get; set; }

        public string Type { get; set; }
    }
}

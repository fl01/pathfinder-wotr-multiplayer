namespace WOTRMultiplayer.Networking.Messages
{
    /// <summary>
    /// protobuf ids must be unique, but no one cares about actual int value
    /// None value is a logic delimiter
    /// </summary>
    public static class MessageTypes
    {
        public enum Lobby
        {
            None = 100,
            GameServerConnectionSucceeded,
            ClientGameServerConnectionConfirmed,
            NotifyCharactersOwnerChanged,
            NotifyGameCharactersChanged,
            NotifyGameStageChanged,
            NotifyGameStarted,
            NotifyPlayerDisconnected,
            NotifyPlayersChanged,
            NotifySaveGameAssigned,
            PlayerSaveGameSyncChanged,
            PlayerReadyStatusChanged
        }

        public enum Game
        {
            None = 500,
            ClientAreaLoaded,
            ClientCharacterLevelingRequested,
            ClientCombatInitialized,
            ClientCombatTurnStarted,
            ClientCombatTurnSynchronized,
            ClientDialogStartRequested,
            ClientGameModeTypeEnded,
            ClientGameModeTypeStarted,
            ClientRestEnded,
            CueWitnessed,
            DialogCueAnswerSuggested,
            GamePauseChanged,
            NotifyAbilityUse,
            NotifyActiveHandEquipmentSetChanged,
            NotifyCampingStateChanged,
            NotifyCampingUnitsRoleChanged,
            NotifyCampingUseHealingSpellsChanged,
            NotifyCharacterLevelingStarted,
            NotifyCharacterMove,
            NotifyCombatInitialized,
            NotifyCombatTurnStarted,
            NotifyCombatTurnSynchronizationRequired,
            NotifyContainerLooted,
            NotifyDialogCueAnswerSelected,
            NotifyDialogCueAnswerSuggested,
            NotifyDialogStarted,
            NotifyDropItem,
            NotifyEquipmentSlotChanged,
            NotifyForcedPauseEnded,
            NotifyGroundClicked,
            NotifyInspectionKnowledgeCheckRolled,
            NotifyInvalidCombatTurnStarted,
            NotifyLevelingAbilityScoreDecreased,
            NotifyLevelingAbilityScoreIncreased,
            NotifyLevelingClassArchetypeSelected,
            NotifyLevelingClassSelected,
            NotifyLevelingCompleted,
            NotifyLevelingFeatureSelected,
            NotifyLevelingPhaseChanged,
            NotifyLevelingPhaseWitnessed,
            NotifyLevelingSkillPointDecreased,
            NotifyLevelingSkillPointIncreased,
            NotifyLevelingSpellChosen,
            NotifyLevelingSpellRemoved,
            NotifyLevelingTerminated,
            NotifyMapObjectClicked,
            NotifyOvertipInteracted,
            NotifyPartyLeaveArea,
            NotifyPerceptionCheckRolled,
            NotifyRestBanterInterrupted,
            NotifyRestStarted,
            NotifySpawnCampPlace,
            NotifySpellForgotten,
            NotifySpellMemorized,
            NotifyToggleActivatableAbility,
            NotifyUnitClicked,
            NotifyUnitJoinedMidCombat,
            NotifyVendorDealMade,
            NotifyVendorItemTransferred,
            NotifyVendorWindowClosed,
            PlayerCombatTurnEnded,
        }

        public enum Request
        {
            None = 88888,
            AIActionRequest,
            AIActionResponse,
            DiceRollValueRequest,
            DiceRollValueResponse,
            RandomEncounterContextRequest,
            RandomEncounterContextResponse,
        }
    }
}

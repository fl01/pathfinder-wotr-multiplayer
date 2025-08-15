using System;
using System.Collections.Generic;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;

namespace WOTRMultiplayer.Abstractions.MP.Actors
{
    public interface IMultiplayerActor
    {
        long GetLocalPlayerId();

        NetworkGameConnectivity GetGameConnectivity();

        List<NetworkPlayer> GetPlayers();

        List<NetworkPlayer> GetOtherPlayers();

        List<NetworkCharacterOwnership> GetCharacters();

        bool ReadyChanged();

        void MoveNonCombatCharacter(string unitId, NetworkVector3 destination, float delay, float orientation);

        bool IsInCombat { get; }

        bool IsActive { get; }

        bool IsInLobby { get; }

        void Dispose();

        Action<string> OnStartGame { get; set; }

        Action<NetworkGameConnectivity> OnConnected { get; set; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        int RestBanterSeed { get; }

        bool IsControlledByLocalPlayer(string unitId);

        bool IsControlledByPlayers(string unitId);

        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);

        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);

        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);

        void PartyChanged();

        void CombatStarted();

        void CombatEnded();

        bool CanInitializeCombat();

        bool CanContinueCombat();

        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);

        bool OnBeforeEndTurn(string unitId);

        void CombatRoundStarted(int round);

        void ForceLoadGame(string savePath, string gameId);

        bool IsDiceRollOwner(bool silent);

        TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string unitId)
            where TRollValue : RollValueBase;

        void OnClickUnit(NetworkClick click);

        void OnClickGround(NetworkClick click);

        void OnClickMapObject(NetworkClick click);

        void OnAbilityUse(NetworkAbility ability);

        void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse);

        void OnLootContainer(NetworkLootContainer container);

        void OnDropItem(NetworkDropItem dropItem);

        void OnEquipmentSlotChanged(NetworkEquipmentSlot networkSlot);

        void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set);

        void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip);

        void OnAreaScenesLoaded();

        bool CanUnitJoinCombat(string unitId);

        string GetMultiplayerOwnerName(string unitId);

        bool OnStartGameMode(GameModeType type);

        bool OnStopGameMode(GameModeType type);

        bool OnShowRestView(RestPhase phase);

        void OnInterrupRestBanterBark(NetworkRestBanter networkBanter);

        NetworkAIAction OnAfterAISelectedAction(NetworkAIAction action);

        void OnTransferVendorItem(NetworkVendorItemTransfer transfer);

        void OnMemorizeSpell(NetworkSpellSlot slot);

        void OnForgetSpell(NetworkSpellSlot slot);
    }
}

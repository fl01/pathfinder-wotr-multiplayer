using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Rolls;

namespace WOTRMultiplayer.Abstractions.MP
{
    public interface IMultiplayerParticipant
    {
        bool ReadyChanged();

        void MoveCharacter(string characterName, Vector3 destination, float delay, float orientation);

        bool IsActive { get; }

        bool IsInLobby { get; }

        void Dispose();

        Action<string> OnStartGame { get; set; }

        Action<EndPoint> OnConnected { get; set; }

        NetworkGame CurrentGame { get; }

        Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        bool CanControlCharacter(string unitId);

        void GameLoaded();
        void Pause();
        void Unpause();
        void OnAfterCueShow(string dialogName, string cueName, bool hasSystemAnswer);
        bool OnBeforeSelectDialogAnswer(string dialogName, string cueName, string answerName, bool isExitAnswer, string manualUnitSelectionId);
        bool StartDialog(string dialogName, string targetUnitId, string initiatorUnitId, string mapObjectId, string speakerKey);
        void PartyChanged();
        void CombatStarted();
        void CombatEnded();
        bool CanInitializeCombat();
        bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound);
        bool OnBeforeEndTurn(string unitId);
        void CombatRoundStarted(int round);
        int GetCombatRound();
        bool CanContinueCombat();
        void ForceLoadGame(string savePath);
        bool ShouldStoreRoll();
        NetworkDiceRoll RetrieveRoll(int networkDiceRollId, string initiatorId);
        void OnClickUnit(NetworkClick click);
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.MP
{
    public abstract class MultiplayerActorBase
    {
        public const int LocalHostPlayerId = -1;

        private readonly object _actionLock = new();

        protected NetworkGame Game { get; set; }

        protected ILogger Logger { get; private set; }

        protected IMapper Mapper { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

        protected IDiceRollStorage DiceRollStorage { get; private set; }

        protected IFileSystemService FileSystem { get; private set; }

        protected IMultiplayerSettingsProvider SettingsProvider { get; private set; }

        protected abstract bool IsHost { get; }

        protected object ActionLock => _actionLock;

        protected MultiplayerActorBase(
            ILogger logger,
            IMapper mapper,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            IGameInteractionService gameInteractionService,
            IDiceRollStorage diceRollStorage,
            IFileSystemService fileSystemService)
        {
            Logger = logger;
            Mapper = mapper;
            GameInteraction = gameInteractionService;
            DiceRollStorage = diceRollStorage;
            FileSystem = fileSystemService;
            SettingsProvider = multiplayerSettingsProvider;
        }

        public bool CanControlCharacter(string unitId)
        {
            if (Game == null)
            {
                return false;
            }

            var realCharacterId = GameInteraction.GetPetOwnerId(unitId) ?? unitId;

            var character = GetCharacterOwnership(realCharacterId);

            return character != null && character.Owner != null && character.Owner.Id == Game.LocalPlayerId;
        }

        public void CombatRoundStarted(int round)
        {
            Logger.LogInformation("Combat round started. Round={round}", round);
            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not started yet");
                return;
            }

            Game.Combat.Round = round;
        }

        public int GetCombatRound()
        {
            return Game.Combat?.Round ?? 0;
        }

        protected bool IsRolledByHost(bool silent)
        {
            var isNotInCombat = Game.Combat == null;
            var isCombatNotInitialized = !(Game.Combat?.IsInitialized ?? false);
            var isTurnNotInitialized = Game.Combat?.Turn == null;
            var isAI = Game.Combat?.Turn?.IsAI ?? false;
            var result = isNotInCombat // everything happens on host outside of combat
                || isCombatNotInitialized // combat initialization phase (initiative rolls)
                || isTurnNotInitialized // could happen when some new NPC joins midfight in midturns, e.g. Anevia in prologue
                || isAI; // clients are getting their AI rolls from host

            if (!silent)
            {
                Logger.LogInformation("IsRolledByHost calculation. Result={result}, IsNotInCombat={isNotInCombat}, IsCombatNotInitialized={isCombatNotInitialized}, IsTurnNotInitialized={isTurnNotInitialized}, IsAI={isAI}",
                    result, isNotInCombat, isCombatNotInitialized, isTurnNotInitialized, isAI);
            }

            return result;
        }

        protected bool IsRolledByLocalPlayer(bool silent)
        {
            var isNotAI = !(Game.Combat?.Turn?.IsAI ?? false);
            var isLocalPlayer = Game.Combat?.Turn?.IsLocalPlayer ?? false;
            var result = isNotAI  // clients are getting their AI rolls from host
                && isLocalPlayer; // other MP players are getting rolls from turn owner

            if (!silent)
            {
                Logger.LogInformation("IsRolledByLocalPlayer calculation. Result={result}, IsNotAI={isNotAI}, IsLocalPlayer={isLocalPlayer}",
                result, isNotAI, isLocalPlayer);
            }
            return result;
        }

        protected bool OnTurnEnd()
        {
            if (Game.Combat.Turn.IsAI || !Game.Combat.Turn.IsInProgress)
            {
                Logger.LogInformation("Turn end is allowed. Round={round}, UnitId={unitId}, IsAI={isAI}", Game.Combat.Round, Game.Combat.Turn.UnitId, Game.Combat.Turn.IsAI);
                Game.Combat.Turn = null;
                return true;
            }

            if (Game.Combat.Turn.IsLocalPlayer)
            {
                Logger.LogInformation("Ending local player turn. Round={round}, UnitId={unitId}", Game.Combat.Round, Game.Combat.Turn.UnitId);
                OnLocalPlayerTurnEnded();
            }

            Game.Combat.Turn.IsInProgress = false;
            return false;
        }

        protected bool OnTurnStart(string unitId, bool isActingInSurpriseRound)
        {
            if (Game.Combat.Turn != null && Game.Combat.Turn.IsInProgress)
            {
                return true;
            }

            Game.Combat.Turn = new NetworkCombatTurn
            {
                UnitId = unitId,
                IsInProgress = false,
                IsActingInSurpriseRound = isActingInSurpriseRound,
                IsLocalPlayer = CanControlCharacter(unitId),
                IsAI = GameInteraction.IsUnitAI(unitId)
            };

            Logger.LogInformation("OnTurnStart. UnitId={unitId}, IsLocalPlayer={isLocalPlayer}, IsAI={isAI}, IsActingInSurpriseRound={isActingInSurpriseRound}, IsInProgress={isInProgress}",
                             unitId, Game.Combat.Turn.IsLocalPlayer, Game.Combat.Turn.IsAI, Game.Combat.Turn.IsActingInSurpriseRound, Game.Combat.Turn.IsInProgress);

            OnLocalPlayerTurnStart();
            return false;
        }

        protected List<NetworkPlayer> GetMissingPlayers(string key, ConcurrentDictionary<string, HashSet<long>> playersReadinessTracker)
        {
            List<NetworkPlayer> notReadyPlayers = [.. Game.Players];
            if (playersReadinessTracker.TryGetValue(key, out var players))
            {
                notReadyPlayers.RemoveAll(p => players.Contains(p.Id));
            }

            return notReadyPlayers;
        }

        protected virtual void OnTurnStartConfirmed()
        {
        }

        protected abstract void OnLocalPlayerTurnStart();

        protected abstract void OnLocalPlayerTurnEnded();

        protected NetworkCharacterOwnership GetCharacterOwnership(string unitId)
        {
            return Game.Characters.FirstOrDefault(c => string.Equals(c.UnitId, unitId, StringComparison.OrdinalIgnoreCase));
        }

        protected string GetTurnReadinessKey(int round, string unitId)
        {
            return $"{round}-{unitId}";
        }

        protected enum PlayerReadinessType
        {
            TurnStart,
            TurnSynchronization
        }

        protected ConcurrentDictionary<string, HashSet<long>> GetReadinessTracker(PlayerReadinessType type)
        {
            var tracker = type switch
            {
                PlayerReadinessType.TurnStart => Game.Combat.PlayersTurnStartInitialization,
                PlayerReadinessType.TurnSynchronization => Game.Combat.PlayersTurnSynchronization,
                _ => null,
            };

            return tracker;
        }

        protected NetworkPlayer GetPlayer(long playerId)
        {
            return Game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        protected void EndLocalTurn()
        {
            Game.Combat.Turn.IsInProgress = false;
            GameInteraction.EndTurnBasedCombatTurn();
        }
    }
}

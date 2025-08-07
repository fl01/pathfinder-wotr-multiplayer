using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;

namespace WOTRMultiplayer.MP.Actors
{
    public abstract class MultiplayerActorBase
    {
        public const int LocalHostPlayerId = -1;

        private readonly object _actionLock = new();

        public bool IsInCombat => Game?.Combat != null;

        internal NetworkGame Game { get; set; }

        public Action<string> OnStartGame { get; set; }

        protected ILogger Logger { get; private set; }

        protected IMapper Mapper { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

        protected IDiceRollStorage DiceRollStorage { get; private set; }

        protected IFileSystemService FileSystem { get; private set; }

        protected IMultiplayerSettingsProvider SettingsProvider { get; private set; }

        private readonly IUniqueIdGenerator _uniqueIdGenerator;

        protected abstract bool IsHost { get; }

        protected object ActionLock => _actionLock;

        protected MultiplayerActorBase(
            ILogger logger,
            IMapper mapper,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            IGameInteractionService gameInteractionService,
            IDiceRollStorage diceRollStorage,
            IFileSystemService fileSystemService,
            IUniqueIdGenerator uniqueIdGenerator)
        {
            Logger = logger;
            Mapper = mapper;
            GameInteraction = gameInteractionService;
            DiceRollStorage = diceRollStorage;
            FileSystem = fileSystemService;
            SettingsProvider = multiplayerSettingsProvider;
            _uniqueIdGenerator = uniqueIdGenerator;
        }

        public long GetLocalPlayerId()
        {
            return Game?.LocalPlayerId ?? 0;
        }

        public NetworkGameConnectivity GetGameConnectivity()
        {
            return Game?.Connectivity;
        }

        public List<NetworkPlayer> GetPlayers()
        {

            return [.. Game?.Players ?? []];
        }

        public List<NetworkPlayer> GetOtherPlayers()
        {
            return [.. Game?.Players.Where(p => p.Id != Game.LocalPlayerId) ?? []];
        }

        public List<NetworkCharacterOwnership> GetCharacters()
        {
            return [.. Game?.Characters ?? []];
        }

        public bool IsControlledByLocalPlayer(string unitId)
        {
            try
            {
                var character = GetCharacterOwnership(unitId);

                return character?.Owner != null && character.Owner.Id == Game.LocalPlayerId;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to determine if character is controlled by local player");
                throw;
            }
        }

        public bool IsControlledByPlayers(string unitId)
        {
            try
            {
                var character = GetCharacterOwnership(unitId);

                return character?.Owner != null;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to determine if character is controlled by players");
                throw;
            }
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

        public void OnAbilityUse(NetworkAbility ability)
        {
            if (!ShouldNotifyAboutAbilityUse(ability.CasterId))
            {
                return;
            }

            Logger.LogInformation("Sending ability use. CasterId={unitId}, TargetId={targetId}, TargetPoint={targetPoint}, AbilityId={abilityId}, SpellbookId={spellbookId}, VectorPathCount={vectorPathCount}",
              ability.CasterId, ability.TargetId, ability.TargetPoint, ability.Id, ability.SpellbookId, ability.VectorPath?.Count);

            var message = new NotifyAbilityUse
            {
                Ability = Mapper.Map<Networking.Messages.NetworkAbility>(ability)
            };

            Send(message);
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse)
        {
            if (!ShouldNotifyAboutAbilityUse(activatableAbilityUse.CasterId))
            {
                return;
            }

            Logger.LogInformation("Toggle activatable ability. CasterId={unitId}, TargetId={targetId}, AbilityId={abilityId}, IsActive={isActive}", activatableAbilityUse.CasterId, activatableAbilityUse.TargetId, activatableAbilityUse.Id, activatableAbilityUse.IsActive);

            var message = new NotifyToggleActivatableAbility
            {
                Ability = Mapper.Map<Networking.Messages.NetworkActivatableAbility>(activatableAbilityUse)
            };

            Send(message);
        }

        public TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string unitId)
            where TRollValue : RollValueBase
        {
            Logger.LogInformation("Retrieving roll over network. RollId={rollId}, UnitId={unitId}", networkDiceRollId, unitId);

            var waitForRollTimeout = TimeSpan.FromSeconds(10);
            var request = new DiceRollValueRequest { RollId = networkDiceRollId, Timeout = waitForRollTimeout };
            // it's important to block current thread since we cannot proceed without response
            // yeah most likely it will cause the game to freeze in case of bad network
            var response = RetrieveRollAsync(request, unitId).Result;

            return ResponseToRollValue<TRollValue>(response);
        }


        public void OnClickUnit(NetworkClick click)
        {
            if (Game.Combat == null && !IsControlledByLocalPlayer(click.SelectedUnits)
                || Game.Combat != null && (!(Game.Combat.Turn?.IsLocalPlayer ?? false) || GameInteraction.CombatTurnHasBeenFinished()))
            {
                return;
            }

            Logger.LogInformation("Sending unit click. TargetUnitId={targetUnitId}, VectorPathCount={pathCount}", click.TargetUnitId, click.VectorPath.Count);

            var message = new NotifyUnitClicked
            {
                Click = Mapper.Map<Networking.Messages.NetworkClick>(click)
            };

            Send(message);
        }

        public void OnClickGround(NetworkClick click)
        {
            if (!(Game.Combat?.Turn?.IsLocalPlayer ?? false) || GameInteraction.CombatTurnHasBeenFinished())
            {
                return;
            }

            Logger.LogInformation("Sending ground click. WorldPosition={worldPosition}, VectorPathCount={pathCount}, SelectedUnits={selectedUnits}", click.WorldPosition, click.VectorPath.Count, string.Join(";", click.SelectedUnits));
            var message = new NotifyGroundClicked
            {
                Click = Mapper.Map<Networking.Messages.NetworkClick>(click)
            };

            Send(message);
        }

        public void OnClickMapObject(NetworkClick click)
        {
            if (!IsControlledByLocalPlayer(click.SelectedUnits))
            {
                return;
            }

            Logger.LogInformation("Sending map object click. WorldPosition={worldPosition}, VectorPathCount={pathCount}, SelectedUnits={selectedUnits}", click.WorldPosition, click.VectorPath.Count, string.Join(";", click.SelectedUnits));
            var message = new NotifyMapObjectClicked
            {
                Click = Mapper.Map<Networking.Messages.NetworkClick>(click)
            };

            Send(message);
        }

        public void OnLootContainer(NetworkLootContainer container)
        {
            Logger.LogInformation("Sending looted container info. ContainerId={containerId}, ContainerPosition={containerPosition}, ItemsCount={itemsCount}, Items={itemsIds}", container.Id, container.Position, container.Items.Count, container.Items.Select(i => i.UniqueId));
            var message = new NotifyContainerLooted
            {
                Container = Mapper.Map<Networking.Messages.NetworkLootContainer>(container)
            };

            Send(message);
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            Logger.LogInformation("Sending drop item. OwnerId={ownerId}, ItemId={itemId}, ItemName={itemName}", dropItem.OwnerEntityId, dropItem.Item.UniqueId, dropItem.Item.Name);
            var message = new NotifyDropItem
            {
                Drop = Mapper.Map<Networking.Messages.NetworkDropItem>(dropItem)
            };

            Send(message);
        }


        public void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            if (!IsControlledByPlayers(set.UnitId))
            {
                return;
            }

            Logger.LogInformation("Sending changed active hand equipment set. UnitId={unitId}, SetIndex={setIndex}", set.UnitId, set.Index);
            var message = new NotifyActiveHandEquipmentSetChanged
            {
                Set = Mapper.Map<Networking.Messages.NetworkActiveHandEquipmentSet>(set)
            };

            Send(message);
        }

        public void OnEquipmentSlotChanged(NetworkEquipmentSlot equipmentSlot)
        {
            if (!IsControlledByPlayers(equipmentSlot.OwnerId))
            {
                return;
            }

            Logger.LogWarning("Sending changed equipment slot. SlotType={slotType}, SlotIndex={slotIndex}, ItemId={itemId}, OwnerId={ownerId}", equipmentSlot.Position.Type, equipmentSlot.Position.Index, equipmentSlot.Item?.UniqueId, equipmentSlot.OwnerId);
            var message = new NotifyEquipmentSlotChanged
            {
                Slot = Mapper.Map<Networking.Messages.NetworkEquipmentSlot>(equipmentSlot)
            };

            Send(message);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            Logger.LogWarning("Sending overtip interaction. MapObjectId={mapObjectId}, Units={units}", networkOvertip.MapObject.Id, networkOvertip.Units);
            var message = new NotifyOvertipInteracted
            {
                Overtip = Mapper.Map<Networking.Messages.NetworkOvertip>(networkOvertip)
            };

            Send(message);
        }

        public void OnAreaScenesLoaded()
        {
            SoftReset();

            PartyChanged();
        }

        /// <summary>
        /// Reloads current party characters and tries to merge ownership
        /// </summary>
        public void PartyChanged()
        {
            Logger.LogInformation("Updating current characters & merging ownership");

            // could be synced from host, but state is the same anyway
            var partyCharacters = GameInteraction.GetPartyPlayers();
            if (partyCharacters.Count == 0)
            {
                return;
            }

            var oldCharacters = Game.Characters.ToList();
            Game.Characters = [.. partyCharacters];
            var defaultOwner = GetPlayer(LocalHostPlayerId);
            foreach (var character in Game.Characters)
            {
                var existingOwnershipConfiguration = oldCharacters.FirstOrDefault(old =>
                    old.Name == character.Name || old.Name.Contains(character.Name));
                if (existingOwnershipConfiguration?.Owner != null)
                {
                    character.Owner = existingOwnershipConfiguration.Owner;
                    Logger.LogInformation("Character ownership has been preserved. UnitId={unitId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
                    continue;
                }

                character.Owner = defaultOwner;
                Logger.LogInformation("Character ownership has been assigned to default player (host). UnitId={unitId}, CharacterName={characterName}, Owner={ownerId}", character.UnitId, character.Name, character.Owner.Id);
            }
        }

        public void ForceLoadGame(string savePath, string gameId)
        {
            if (!string.IsNullOrEmpty(Game.SaveFilePath))
            {
                return;
            }

            Game.SaveFilePath = savePath;
            Game.Id = gameId;

            if (IsHost)
            {
                foreach (var player in Game.Players)
                {
                    player.IsLoading = true;
                }
            }

            ResetGameIdGenerator();

            Logger.LogInformation("Notifying other players to force load save game. GameId={gameId}, Path={savePath}", Game.Id, savePath);
            var message = new NotifySaveGameAssigned
            {
                GameId = Game.Id,
                Content = FileSystem.GetFile(savePath),
                IsForceLoad = true
            };

            Send(message);
        }

        public void CombatStarted()
        {
            Logger.LogInformation("Combat started");
            if (Game.Combat != null)
            {
                Logger.LogWarning("Previous combat has not been disposed correctly");
            }

            Game.Combat = new NetworkCombat();
        }

        public void CombatEnded()
        {
            Logger.LogInformation("Combat ended");
            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started correctly");
            }

            Game.Combat = null;
        }

        public bool CanUnitJoinCombat(string unitId)
        {
            try
            {
                if (Game.Combat == null)
                {
                    return true;
                }

                var isSummoned = GameInteraction.IsSummoned(unitId);
                if (isSummoned)
                {
                    return true;
                }

                if (Game.Combat.ConfirmedMidCombatUnits.Contains(unitId))
                {
                    Logger.LogInformation("Unit has been allowed to join mid combat. UnitId={unitId}", unitId);
                    return true;
                }

                var localPlayerId = GetLocalPlayerId();
                var isFirstJoinEvent = !IsPlayerReady(PlayerTurnReadinessType.UnitJoinedMidCombat, localPlayerId, unitId);
                if (isFirstJoinEvent)
                {
                    Logger.LogInformation("Sending {messageType}. UnitId={unitId}", nameof(NotifyUnitJoinedMidCombat), unitId);
                    var message = new NotifyUnitJoinedMidCombat { UnitId = unitId, PlayerId = localPlayerId };
                    Send(message);
                }

                AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, localPlayerId, unitId);

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to process unit join combat request. UnitId={unitId}", unitId);
                throw;
            }
        }

        public string GetMultiplayerOwnerName(string unitId)
        {
            var owner = GetCharacterOwnership(unitId);
            return owner?.Owner?.Name;
        }

        protected abstract Task<DiceRollValueResponse> RetrieveRollAsync(DiceRollValueRequest rollRequest, string unitId);

        protected abstract void OnLocalPlayerTurnStart();

        protected abstract void OnLocalPlayerTurnEnded();

        protected abstract void Send(object message);

        protected abstract void Send(long playerId, object message);

        protected void ShowPlayerDisconnectedMessage(NetworkPlayer networkPlayer)
        {
            if (networkPlayer == null || Game.Stage != NetworkGameStage.Playing)
            {
                return;
            }

            GameInteraction.ShowModalMessage($"Player {networkPlayer.Name} has left the game");
        }

        protected NetworkPlayer CleanupPlayer(long playerId)
        {
            var existingPlayer = GetPlayer(playerId);
            if (existingPlayer == null)
            {
                Logger.LogWarning("Nothing to cleanup since player doesn't exist. PlayerId={playerId}", playerId);
                return null;
            }

            Game.Players.Remove(existingPlayer);

            foreach (var characterOwnership in Game.Characters)
            {
                if (characterOwnership.Owner == existingPlayer)
                {
                    characterOwnership.Owner = GetPlayer(LocalHostPlayerId);
                }
            }

            return existingPlayer;
        }

        protected HashSet<long> AddPlayerReadyStatus(PlayerTurnReadinessType playerReadinessType, long playerId, string unitId)
        {
            return AddPlayerReadyStatus(playerReadinessType, playerId, null, unitId);
        }

        protected HashSet<long> AddPlayerReadyStatus(PlayerTurnReadinessType playerReadinessType, long playerId, int? round, string unitId)
        {
            try
            {
                lock (ActionLock)
                {
                    var tracker = GetPlayerTurnReadinessTracker(playerReadinessType);
                    if (tracker == null)
                    {
                        Logger.LogError("Unable to find readiness tracker for provided type. Type={type}, PlayerId={playerId}, Round={round}, UnitId={unitId}", playerReadinessType, playerId, round, unitId);
                        return null;
                    }

                    var key = GetPlayerReadinessKey(round, unitId);

                    var isFirstAdd = !tracker.TryGetValue(key, out var readyPlayers) || !readyPlayers.Contains(playerId);

                    var players = tracker.AddOrUpdate(key,
                         key => new HashSet<long>(collection: [playerId]),
                         (key, existing) =>
                         {
                             existing.Add(playerId);
                             return existing;
                         });

                    if (isFirstAdd)
                    {
                        Logger.LogInformation("Player ready status has been confirmed. Type={readinessTypeName}, Key={key}, PlayersCount={playersCount}, KeysCount={keysCount}", playerReadinessType, key, tracker[key].Count, tracker.Keys.Count);
                    }

                    return players;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to confirm player readiness. PlayerId={playerId}, Round={round}, UnitId={unitId}", playerId, round, unitId);
                throw;
            }
        }

        protected enum PlayerTurnReadinessType
        {
            Start,
            UnitSynchronization,
            UnitJoinedMidCombat
        }

        protected void PrepareCombat()
        {
            // looks dumb af, but seems like combat could start before all initiatives are rolled
            // so let's make sure combat is 100% prepared before allowing to proceed
            const int combatPreparationFramesDelay = 0;
            if (Game.Combat.CombatPreparedFrames < combatPreparationFramesDelay)
            {
                Game.Combat.CombatPreparedFrames++;
            }

            if (!Game.Combat.IsCombatPrepared && Game.Combat.CombatPreparedFrames == combatPreparationFramesDelay)
            {
                Game.Combat.IsCombatPrepared = true;
            }
        }

        protected void InvokeOnStartGame()
        {
            ResetGameIdGenerator();
            OnStartGame?.Invoke(Game.SaveFilePath);
        }

        protected void ForceLoadGame()
        {
            Logger.LogInformation("Force loading save game. SavePath={savePath}", Game.SaveFilePath);
            Game.Id = GameInteraction.QuickLoadGame(Game.SaveFilePath);
            ResetGameIdGenerator();
        }

        protected void ResetGameIdGenerator()
        {
            Logger.LogInformation("Resetting id counters. GameId={gameId}", Game.Id);
            _uniqueIdGenerator.Reset(Game.Id);
        }

        protected void SoftReset()
        {
            Logger.LogInformation("Doing soft reset");
            Game.Dialog = null;
            Game.SaveFilePath = null;
            Game.Combat = null;
            DiceRollStorage.Reset();
        }

        protected string StoreSaveFile(byte[] content)
        {
            var baseUnityPath = GameInteraction.GetSaveGamePath();
            var multiplayerPath = Regex.Replace(baseUnityPath, "(((\\\\|\\/)+)(Saved Games)((\\\\|\\/)+))$", "/Saved Multiplayer Games/");
            var savePath = Path.Combine(multiplayerPath, "latest save.zks");
            Logger.LogInformation("Save game path changed. Path={path}", savePath);
            if (!FileSystem.WriteFile(savePath, content))
            {
                return null;
            }

            return savePath;
        }

        protected bool ShouldNotifyAboutAbilityUse(string sourceUnitId)
        {
            if (Game.Combat == null)
            {
                return IsControlledByLocalPlayer(sourceUnitId);
            }

            // not sure what falls under this category
            // midfight joins shouldn't have any actions?
            if (Game.Combat.Turn == null)
            {
                Logger.LogWarning("Midfight action. UnitId={unitID}", sourceUnitId);
                return IsHost;
            }

            return Game.Combat.Turn.IsLocalPlayer && !GameInteraction.CombatTurnHasBeenFinished();
        }

        protected TRollValue ResponseToRollValue<TRollValue>(DiceRollValueResponse rollResponse)
               where TRollValue : RollValueBase
        {
            if (rollResponse?.RollValue == null)
            {
                Logger.LogError("Retrieved roll is null. RollId={rollId}", rollResponse?.RollId);
                return null;
            }

            if (rollResponse.RollValue.Result > 0)
            {
                var intValue = Mapper.Map<NetworkIntRollValue>(rollResponse.RollValue);
                return intValue as TRollValue;
            }

            if (rollResponse.RollValue.DamageValues.Count > 0)
            {
                var damageValues = Mapper.Map<NetworkDamageListRollValue>(rollResponse.RollValue);
                return damageValues as TRollValue;
            }

            Logger.LogError("Unknown roll type response. RollId={rollId}", rollResponse.RollId);
            return null;
        }

        protected async Task SendLocalRollAsync(long playerId, DiceRollValueRequest request)
        {
            try
            {
                var roll = await DiceRollStorage.GetAsync<RollValueBase>(request.RollId, playerId, request.Timeout);
                var response = new DiceRollValueResponse
                {
                    RollId = request.RollId,
                    RollValue = Mapper.Map<Networking.Messages.NetworkRollValue>(roll)
                };

                Logger.LogInformation("Sending roll value response. RollId={rollId}, RollType={rollType}, Result={result}, DamageValuesCount={damageValuesCount} RollHistoryCount={rollHistoryCount}",
                    response.RollId, roll?.GetType().Name, response.RollValue?.Result, response.RollValue?.DamageValues.Count, response.RollValue?.RollHistory.Count);

                Send(playerId, response);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to send local roll");
                throw;
            }
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
                UpdateConfirmedMidCombatUnits();

                return true;
            }

            Game.Combat.Turn = new NetworkCombatTurn
            {
                UnitId = unitId,
                IsInProgress = false,
                IsActingInSurpriseRound = isActingInSurpriseRound,
                IsLocalPlayer = IsControlledByLocalPlayer(unitId),
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

        protected bool IsPlayerReady(PlayerTurnReadinessType playerTurnReadinessType, long playerId, int? round, string unitId)
        {
            var tracker = GetPlayerTurnReadinessTracker(playerTurnReadinessType);
            var key = GetPlayerReadinessKey(round, unitId);
            var missingPlayers = GetMissingPlayers(key, tracker);
            return !missingPlayers.Any(p => p.Id == playerId);
        }

        protected bool IsPlayerReady(PlayerTurnReadinessType playerTurnReadinessType, long playerId, string unitId)
        {
            return IsPlayerReady(playerTurnReadinessType, playerId, null, unitId);
        }

        protected virtual void OnTurnStartConfirmed()
        {
        }

        protected NetworkCharacterOwnership GetCharacterOwnership(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            var realCharacterId = GameInteraction.GetPetOwnerId(unitId) ?? unitId;

            return Game.Characters.FirstOrDefault(c => string.Equals(c.UnitId, realCharacterId, StringComparison.OrdinalIgnoreCase));
        }

        protected string GetTurnReadinessKey(int round, string unitId)
        {
            return $"{round}-{unitId}";
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

        private bool IsControlledByLocalPlayer(List<string> units)
        {
            return IsControlledByLocalPlayer(units?.FirstOrDefault());
        }

        private string GetPlayerReadinessKey(int? round, string unitId)
        {
            return round.HasValue ? $"{round}-{unitId}" : unitId;
        }

        private ConcurrentDictionary<string, HashSet<long>> GetPlayerTurnReadinessTracker(PlayerTurnReadinessType type)
        {
            var tracker = type switch
            {
                PlayerTurnReadinessType.Start => Game.Combat.PlayersTurnStartInitialization,
                PlayerTurnReadinessType.UnitSynchronization => Game.Combat.PlayersTurnSynchronization,
                PlayerTurnReadinessType.UnitJoinedMidCombat => Game.Combat.MidCombatUnitJoins,
                _ => null,
            };

            return tracker;
        }

        private void UpdateConfirmedMidCombatUnits()
        {
            lock (ActionLock)
            {
                var midCombat = GetPlayerTurnReadinessTracker(PlayerTurnReadinessType.UnitJoinedMidCombat);
                foreach (var kv in midCombat)
                {
                    if (kv.Value.Count >= Game.Players.Count)
                    {
                        Game.Combat.ConfirmedMidCombatUnits.Add(kv.Key);
                    }
                }
            }
        }
    }
}

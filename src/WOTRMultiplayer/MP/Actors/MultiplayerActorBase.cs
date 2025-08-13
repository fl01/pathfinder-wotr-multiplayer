using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.GameModes;
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
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP.Actors
{
    public abstract class MultiplayerActorBase
    {
        public const int LocalHostPlayerId = -1;

        private readonly object _actionLock = new();

        public bool IsInCombat => Game?.Combat != null;

        public int RestBanterSeed => Game.RestBanterSeed;

        internal NetworkGame Game { get; set; }

        public Action<string> OnStartGame { get; set; }

        protected ILogger Logger { get; private set; }

        protected IMapper Mapper { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

        protected IDiceRollStorage DiceRollStorage { get; private set; }

        protected IFileSystemService FileSystem { get; private set; }

        protected IMultiplayerSettingsProvider SettingsProvider { get; private set; }

        private readonly IValueGenerator _valueGenerator;

        protected abstract bool IsHost { get; }

        protected object ActionLock => _actionLock;

        protected MultiplayerActorBase(
            ILogger logger,
            IMapper mapper,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
            IGameInteractionService gameInteractionService,
            IDiceRollStorage diceRollStorage,
            IFileSystemService fileSystemService,
            IValueGenerator valueGenerator)
        {
            Logger = logger;
            Mapper = mapper;
            GameInteraction = gameInteractionService;
            DiceRollStorage = diceRollStorage;
            FileSystem = fileSystemService;
            SettingsProvider = multiplayerSettingsProvider;
            _valueGenerator = valueGenerator;
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
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(ability)
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
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkActivatableAbility>(activatableAbilityUse)
            };

            Send(message);
        }

        public TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string unitId)
            where TRollValue : RollValueBase
        {
            Logger.LogInformation("Retrieving roll over network. RollId={rollId}, UnitId={unitId}", networkDiceRollId, unitId);

            var waitForRollTimeout = TimeSpan.FromSeconds(10);
            var request = new DiceRollValueRequest { RollId = networkDiceRollId, Timeout = waitForRollTimeout, UnitId = unitId };
            // it's important to block current thread since we cannot proceed without response
            // yeah most likely it will cause the game to freeze in case of bad network
            var response = RetrieveRoll(request);

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
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
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
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
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
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
            };

            Send(message);
        }

        public void OnLootContainer(NetworkLootContainer container)
        {
            Logger.LogInformation("Sending looted container info. ContainerId={containerId}, ContainerPosition={containerPosition}, ItemsCount={itemsCount}, Items={itemsIds}", container.Id, container.Position, container.Items.Count, container.Items.Select(i => i.UniqueId));
            var message = new NotifyContainerLooted
            {
                Container = Mapper.Map<Networking.Messages.Contracts.NetworkLootContainer>(container)
            };

            Send(message);
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            Logger.LogInformation("Sending drop item. OwnerId={ownerId}, ItemId={itemId}, ItemName={itemName}", dropItem.OwnerEntityId, dropItem.Item.UniqueId, dropItem.Item.Name);
            var message = new NotifyDropItem
            {
                Drop = Mapper.Map<Networking.Messages.Contracts.NetworkDropItem>(dropItem)
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
                Set = Mapper.Map<Networking.Messages.Contracts.NetworkActiveHandEquipmentSet>(set)
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
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkEquipmentSlot>(equipmentSlot)
            };

            Send(message);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            Logger.LogInformation("Sending overtip interaction. MapObjectId={mapObjectId}, Units={units}", networkOvertip.MapObject.Id, networkOvertip.Units);
            var message = new NotifyOvertipInteracted
            {
                Overtip = Mapper.Map<Networking.Messages.Contracts.NetworkOvertip>(networkOvertip)
            };

            Send(message);
        }

        public virtual void OnAreaScenesLoaded()
        {
            Logger.LogInformation("Area loaded");

            SoftReset();

            PartyChanged();

            lock (ActionLock)
            {
                EnsureForcePaused(UIStringConsts.GameNotifications.ForcedPauseReasons.AreaLoading);
                var localPlayerId = GetLocalPlayerId();
                Game.ForcedPause.ReadyPlayers.Add(localPlayerId);
                GameInteraction.Pause(true);
            }
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

        public bool OnStartGameMode(GameModeType type)
        {
            if (!IsGameModeAllowedToRun(type))
            {
                return false;
            }

            var canRun = OnStartGameModeInternal(type);
            return canRun;
        }

        public bool OnStopGameMode(GameModeType type)
        {
            Logger.LogInformation("Trying to stop GameModeType. Mode={mode}", type.Name);

            if (type == GameModeType.Pause && Game.ForcedPause != null)
            {
                GameInteraction.ShowWarningNotification(Game.ForcedPause.Reason);
                return false;
            }

            var canRun = OnStopGameModeInternal(type);
            return canRun;
        }

        public void OnInterrupRestBanterBark(NetworkRestBanter networkBanter)
        {
            var message = new NotifyRestBanterInterrupted
            {
                Banter = Mapper.Map<Networking.Messages.Contracts.NetworkRestBanter>(networkBanter),
            };

            Logger.LogInformation("Sending {messageType}. SpeakerUnitId={speakerUnitId}, Key={key}", nameof(NotifyRestBanterInterrupted), message.Banter.SpeakerUnitId, message.Banter.Key);
            Send(message);
        }

        protected abstract bool OnStartGameModeInternal(GameModeType type);

        protected abstract bool OnStopGameModeInternal(GameModeType type);

        protected void EnsureForcePaused(string reason, TimeSpan? removalDelay)
        {
            if (Game.ForcedPause == null)
            {
                Game.ForcedPause = new NetworkForcedPause
                {
                    Reason = reason,
                    RemovalDelay = removalDelay
                };
                Logger.LogInformation("Forced pause has been initialized. Delay={delay}", removalDelay);
            }
        }

        protected void EnsureForcePaused(string reason)
        {
            EnsureForcePaused(reason, SettingsProvider.Settings.ForcedPauseDefaultTerminationDelay);
        }

        protected bool RegisterGameMode(GameModeType type, long playerId)
        {
            var isNew = true;
            var registeredPlayers = Game.PlayersInGameMode.AddOrUpdate(type,
                 k => new HashSet<long>([playerId]),
                 (k, existing) =>
                 {
                     // side effects are fine
                     isNew = existing.Add(playerId);
                     return existing;
                 });

            return isNew;
        }

        protected bool UnregisterGameMode(GameModeType type, long playerId)
        {
            if (Game.PlayersInGameMode.TryGetValue(type, out var players))
            {
                return players.Remove(playerId);
            }

            return false;
        }

        protected abstract DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest);

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
            try
            {
                lock (ActionLock)
                {
                    var tracker = GetPlayerTurnReadinessTracker(playerReadinessType);
                    if (tracker == null)
                    {
                        Logger.LogError("Unable to find readiness tracker for provided type. Type={type}, PlayerId={playerId}, UnitId={unitId}", playerReadinessType, playerId, unitId);
                        return null;
                    }

                    var isFirstAdd = !tracker.TryGetValue(unitId, out var readyPlayers) || !readyPlayers.Contains(playerId);

                    var players = tracker.AddOrUpdate(unitId,
                         key => new HashSet<long>(collection: [playerId]),
                         (key, existing) =>
                         {
                             existing.Add(playerId);
                             return existing;
                         });

                    if (isFirstAdd)
                    {
                        Logger.LogInformation("Player ready status has been confirmed. Type={readinessTypeName}, Key={key}, PlayersCount={playersCount}, KeysCount={keysCount}", playerReadinessType, unitId, tracker[unitId].Count, tracker.Keys.Count);
                    }

                    return players;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to confirm player readiness. PlayerId={playerId}, UnitId={unitId}", playerId, unitId);
                throw;
            }
        }

        protected enum PlayerTurnReadinessType
        {
            Start,
            UnitSynchronization,
            UnitJoinedMidCombat
        }

        protected void InvokeOnStartGame()
        {
            ResetGameIdGenerator();
            OnStartGame?.Invoke(Game.SaveFilePath);
            Game.Stage = NetworkGameStage.Loading;
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
            _valueGenerator.Reset(Game.Id);
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
            if (rollResponse == null)
            {
                return null;
            }

            if (rollResponse.RollValue == null)
            {
                Logger.LogError("Specified roll is missing at remote player. RollId={rollId}", rollResponse?.RollId);
                return null;
            }

            if (rollResponse.RollValue.DamageValues.Count > 0)
            {
                var damageValues = Mapper.Map<NetworkDamageListRollValue>(rollResponse.RollValue);
                return damageValues as TRollValue;
            }

            if (rollResponse.RollValue.NamedIntValues.Count > 0)
            {
                var namedIntValues = Mapper.Map<NetworkNamedIntRollValue>(rollResponse.RollValue);
                return namedIntValues as TRollValue;
            }

            var intValue = Mapper.Map<NetworkIntRollValue>(rollResponse.RollValue);
            return intValue as TRollValue;
        }

        protected async Task SendLocalRollAsync(long playerId, DiceRollValueRequest request)
        {
            try
            {
                var roll = await DiceRollStorage.GetAsync<RollValueBase>(request.RollId, playerId, request.Timeout);
                var response = new DiceRollValueResponse
                {
                    RollId = request.RollId,
                    UnitId = request.UnitId,
                    RollValue = Mapper.Map<Networking.Messages.Contracts.NetworkRollValue>(roll)
                };

                Logger.LogInformation("Sending roll value response. RollId={rollId}, RollType={rollType}, Result={result}, DamageValuesCount={damageValuesCount}, RollHistoryCount={rollHistoryCount}",
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
            silent = true;
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
            silent = true;
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
                Game.Combat.AIActions.Clear();
                Logger.LogInformation("Turn start is allowed. UnitId={unitId}, IsActingInSurpiseRound={isActingInSurpriseRound}, TurnUnitId={turnUnitId}", unitId, isActingInSurpriseRound, Game.Combat.Turn.UnitId);
                return true;
            }

            Game.Combat.Turn = new NetworkCombatTurn
            {
                UnitId = unitId,
                IsInProgress = false,
                IsActingInSurpriseRound = isActingInSurpriseRound,
                IsLocalPlayer = IsControlledByLocalPlayer(unitId),
                IsAI = GameInteraction.IsUnitAI(unitId),
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

        protected bool IsPlayerReady(PlayerTurnReadinessType playerTurnReadinessType, long playerId, string unitId)
        {
            var tracker = GetPlayerTurnReadinessTracker(playerTurnReadinessType);
            var missingPlayers = GetMissingPlayers(unitId, tracker);
            return !missingPlayers.Any(p => p.Id == playerId);
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

        protected NetworkPlayer GetPlayer(long playerId)
        {
            return Game.Players.FirstOrDefault(p => p.Id == playerId);
        }

        protected void EndLocalTurn()
        {
            Game.Combat.Turn.IsInProgress = false;
            GameInteraction.EndTurnBasedCombatTurn();
        }

        protected bool IsGameModeAllowedToRun(GameModeType type)
        {
            return type != GameModeType.EscMode && type != GameModeType.FullScreenUi;
        }

        private bool IsControlledByLocalPlayer(List<string> units)
        {
            return IsControlledByLocalPlayer(units?.FirstOrDefault());
        }

        private ConcurrentDictionary<string, HashSet<long>> GetPlayerTurnReadinessTracker(PlayerTurnReadinessType type)
        {
            var tracker = type switch
            {
                PlayerTurnReadinessType.Start => Game.Combat.PlayersNextTurnInitialization,
                PlayerTurnReadinessType.UnitSynchronization => Game.Combat.PlayersNextTurnSynchronization,
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

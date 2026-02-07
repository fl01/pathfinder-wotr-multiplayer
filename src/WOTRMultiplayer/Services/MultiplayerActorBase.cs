using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Movement;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.Spells;
using WOTRMultiplayer.Entities.Vendor;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.Services.Random;

namespace WOTRMultiplayer.Services
{
    public abstract class MultiplayerActorBase
    {
        private readonly object _actionLock = new();

        public bool IsInCombat => Game?.Combat != null;

        public int SessionSeed => Game.SessionSeed;

        public int? LoadedSaveSeed => Game.LoadedSaveSeed;

        public int? CombatSeed => Game.Combat?.Seed;

        public int? CrusadeArmyCombatAreaSeed => Game.ArmyCombat?.AreaSeed;

        public int? CrusadeArmyCombatSeed => Game.ArmyCombat?.Seed;

        public Action<NetworkLobbyStage, List<NetworkPlayer>> OnPlayersChanged { get; set; }

        public Action<string, List<NetworkCharacter>> OnCharactersChanged { get; set; }

        public Action<bool> OnNewGameSequenceStarted { get; set; }

        internal NetworkGame Game { get; set; }

        protected ILogger Logger { get; private set; }

        protected IMapper Mapper { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

        protected ILevelingInteractionService LevelingInteraction { get; private set; }

        protected IPlayerNotificationService PlayerNotification { get; private set; }

        protected IDialogInteractionService DialogInteraction { get; private set; }

        protected IGlobalMapInteractionService GlobalMapInteraction { get; private set; }

        protected IPingInteractionService PingInteraction { get; private set; }

        protected ICombatInteractionService CombatInteraction { get; private set; }

        protected IDiceRollStorage DiceRollStorage { get; private set; }

        protected IFileSystemService FileSystem { get; private set; }

        protected IMultiplayerSettingsService SettingsService { get; private set; }

        private readonly IValueGenerator _valueGenerator;
        private readonly INetworkReceiver _networkReceiver;

        protected abstract bool HasControlOverUI { get; }

        // TODO: revise usages since it's not needed in many cases
        protected object ActionLock => _actionLock;

        protected MultiplayerActorBase(
            ILogger logger,
            IMapper mapper,
            IMultiplayerSettingsService multiplayerSettingsService,
            IGameInteractionService gameInteractionService,
            ILevelingInteractionService levelingInteractionService,
            IPlayerNotificationService playerNotificationService,
            IDialogInteractionService dialogInteractionService,
            IGlobalMapInteractionService globalMapInteractionService,
            IPingInteractionService pingInteractionService,
            ICombatInteractionService combatInteractionService,
            IDiceRollStorage diceRollStorage,
            IFileSystemService fileSystemService,
            IValueGenerator valueGenerator,
            INetworkReceiver networkReceiver)
        {
            Logger = logger;
            Mapper = mapper;
            GameInteraction = gameInteractionService;
            LevelingInteraction = levelingInteractionService;
            PlayerNotification = playerNotificationService;
            DialogInteraction = dialogInteractionService;
            GlobalMapInteraction = globalMapInteractionService;
            PingInteraction = pingInteractionService;
            CombatInteraction = combatInteractionService;
            DiceRollStorage = diceRollStorage;
            FileSystem = fileSystemService;
            SettingsService = multiplayerSettingsService;
            _valueGenerator = valueGenerator;
            _networkReceiver = networkReceiver;
        }

        public NetworkGameConnectivity GetGameConnectivity()
        {
            return Game?.Connectivity;
        }

        public List<NetworkPlayer> GetPlayers()
        {
            lock (ActionLock)
            {
                return [.. Game.Players ?? []];
            }
        }

        public List<NetworkPlayer> GetOtherPlayers()
        {
            lock (ActionLock)
            {
                return [.. Game.Players.Where(p => p.Id != Game.LocalPlayerId) ?? []];
            }
        }

        public List<NetworkCharacter> GetCharacters()
        {
            return [.. Game.Characters ?? []];
        }

        public bool IsControlledByLocalPlayer(string unitId)
        {
            try
            {
                var character = GetPartyCharacter(unitId);

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
                var character = GetPartyCharacter(unitId);

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
            Logger.LogInformation("Combat round started. Round={Round}", round);
            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not started yet");
                return;
            }

            Game.Combat.Round = round;
        }

        public void OnAbilityUse(NetworkAbility ability)
        {
            if (!IsCasterControlledByLocalPlayer(ability.CasterId))
            {
                return;
            }

            var message = new NotifyAbilityUsed
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(ability)
            };
            Logger.LogInformation("Sending {MessageType}. CasterId={CasterId}, TargetUnitId={TargetUnitId}, TargetPoint={TargetPoint}, AbilityId={AbilityId}, SpellbookId={SpellbookId}, VectorPathCount={VectorPathCount}, MovementLimit={MovementLimit}",
                nameof(NotifyAbilityUsed), message.Ability.CasterId, message.Ability.Target.UnitId, message.Ability.Target.Point, message.Ability.Id, message.Ability.SpellbookId, message.Ability.VectorPath?.Count, message.Ability.MovementLimit);

            Send(message);
        }

        public void OnUnitAttackCommandStarted(NetworkUnitAttack networkUnitAttack)
        {
            // this one is only used in combat
            // regular Unit Click handler is used to initiate the combat
            if (Game.Combat == null)
            {
                return;
            }

            var isLocal = IsControlledByLocalPlayer(networkUnitAttack.ExecutorUnitId);
            if (!isLocal || (Game.Combat.Turn?.IsAI ?? false))
            {
                return;
            }

            var message = new NotifyUnitAttacked
            {
                Attack = Mapper.Map<Networking.Messages.Contracts.NetworkUnitAttack>(networkUnitAttack)
            };
            Logger.LogInformation("Sending {MessageType}. ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}", nameof(NotifyUnitAttacked), message.Attack.ExecutorUnitId, message.Attack.TargetUnitId, message.Attack.IsFullAttack);

            Send(message);
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse)
        {
            if (!IsCasterControlledByLocalPlayer(activatableAbilityUse.CasterId))
            {
                return;
            }

            var message = new NotifyToggleActivatableAbility
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkActivatableAbility>(activatableAbilityUse)
            };
            Logger.LogInformation("Sending {MessageType}. CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}, IsActive={IsActive}",
                nameof(NotifyToggleActivatableAbility), message.Ability.CasterId, message.Ability.TargetId, message.Ability.Id, message.Ability.IsActive);

            Send(message);
        }

        public TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string ruleName, string unitId)
            where TRollValue : RollValueBase
        {
            Logger.LogInformation("Retrieving roll over network. RollId={RollId}, UnitId={UnitId}, RuleName={RuleName}", networkDiceRollId, unitId, ruleName);

            var waitForRollTimeout = SettingsService.GetSettings().RemoteRollRetrievalTimeout;
            var request = new DiceRollValueRequest { RollId = networkDiceRollId, Timeout = waitForRollTimeout, UnitId = unitId, PlayerId = Game.LocalPlayerId, RuleName = ruleName, CombatTurnUnitId = Game.Combat?.Turn?.UnitId };
            // it's important to block current thread since we cannot proceed without response
            // yeah most likely it will cause the game to freeze in case of bad network
            var response = RetrieveRoll(request);

            return ResponseToRollValue<TRollValue>(response);
        }


        public void OnClickUnit(NetworkClick click)
        {
            if (Game.Combat == null && (IsControlledByPlayers(click.TargetUnitId) || !IsControlledByLocalPlayer(click.SelectedUnits))
                || Game.Combat != null && (!(Game.Combat.Turn?.IsLocalPlayer ?? false) || CombatInteraction.IsCombatTurnFinished()))
            {
                return;
            }

            var message = new NotifyUnitClicked
            {
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
            };
            Logger.LogInformation("Sending {MessageType}. TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyUnitClicked), message.Click.TargetUnitId, message.Click.SelectedUnits);

            Send(message);
        }

        public void OnClickGround(NetworkClick click)
        {
            if (!(Game.Combat?.Turn?.IsLocalPlayer ?? false) || CombatInteraction.IsCombatTurnFinished())
            {
                return;
            }

            var message = new NotifyGroundClicked
            {
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
            };
            Logger.LogInformation("Sending {MessageType}. WorldPosition={WorldPosition}, SelectedUnits={SelectedUnits}, MovementLimit={MovementLimit}", nameof(NotifyGroundClicked), click.WorldPosition, click.SelectedUnits, click.MovementLimit);

            Send(message);
        }

        public void OnClickMapObject(NetworkClick click)
        {
            if (!IsControlledByLocalPlayer(click.SelectedUnits))
            {
                return;
            }

            Logger.LogInformation("Sending map object click. WorldPosition={WorldPosition}, SelectedUnits={SelectedUnits}", click.WorldPosition, click.SelectedUnits);
            var message = new NotifyMapObjectClicked
            {
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
            };

            Send(message);
        }


        public void OnTransferInventoryItem(NetworkItemsTransfer transferItem)
        {
            var message = new NotifyInventoryItemTransferred
            {
                TransferItem = Mapper.Map<Networking.Messages.Contracts.NetworkItemsTransfer>(transferItem)
            };

            Logger.LogInformation("Sending {MessageType}. Items={Items}, Source={Source}, SourceType={SourceType}, Destination={Destination}, DestinationType={DestinationType}", nameof(NotifyInventoryItemTransferred), message.TransferItem.Items.Select(x => x.UniqueId), message.TransferItem.Source.Id, message.TransferItem.Source.Type, message.TransferItem.Destination?.Id, message.TransferItem.Destination?.Type);
            Send(message);
        }

        public void OnSkinLootContainer(NetworkLootableEntity networkLootableEntity)
        {
            var message = new NotifyLootableEntitySkinned
            {
                Entity = Mapper.Map<Networking.Messages.Contracts.NetworkLootableEntity>(networkLootableEntity)
            };
            Logger.LogInformation("Sending {MessageType}. Id={ContainerId}, Position={Position}, Type={Type}", networkLootableEntity.Id, networkLootableEntity.Position, networkLootableEntity.Type);

            Send(message);
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            var message = new NotifyDropItem
            {
                Drop = Mapper.Map<Networking.Messages.Contracts.NetworkDropItem>(dropItem)
            };
            Logger.LogInformation("Sending {MessageType}. OwnerId={OwnerId}, ItemId={ItemId}, ItemName={ItemName}", nameof(NotifyDropItem), message.Drop.OwnerEntityId, message.Drop.Item.UniqueId, message.Drop.Item.Name);

            Send(message);
        }

        public void OnUseInventoryItem(NetworkUseInventoryItem useInventoryItem)
        {
            // you can't use items from inventory in combat
            // but using them from action bar is triggering this method as well
            if (Game.Combat != null)
            {
                return;
            }

            var message = new NotifyInventoryItemUsed
            {
                UseItem = Mapper.Map<Networking.Messages.Contracts.NetworkUseInventoryItem>(useInventoryItem)
            };
            Logger.LogInformation("Sending {MessageType}. UserUnitId={UserUnitId}, TargetUnitId={TargetUnitId}, ItemId={ItemId}, ItemName={ItemName}", nameof(NetworkUseInventoryItem), message.UseItem.UserUnitId, message.UseItem.Target?.UnitId, message.UseItem.Item.UniqueId, message.UseItem.Item.Name);

            Send(message);
        }

        public void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            if (!IsControlledByPlayers(set.UnitId))
            {
                return;
            }

            Logger.LogInformation("Sending changed active hand equipment set. UnitId={UnitId}, SetIndex={SetIndex}", set.UnitId, set.Index);
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

            var message = new NotifyEquipmentSlotChanged
            {
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkEquipmentSlot>(equipmentSlot)
            };
            Logger.LogInformation("Sending changed equipment slot. SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}, OwnerId={OwnerId}", message.Slot.Position.Type, message.Slot.Position.Index, message.Slot.Item?.UniqueId, message.Slot.OwnerId);

            Send(message);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            var message = new NotifyOvertipInteracted
            {
                Overtip = Mapper.Map<Networking.Messages.Contracts.NetworkOvertip>(networkOvertip)
            };
            Logger.LogInformation("Sending overtip interaction. MapObjectId={MapObjectId}, Units={Units}", message.Overtip.MapObject.Id, message.Overtip.Units);

            Send(message);
        }

        public void OnAreaScenesLoaded()
        {
            var currentChapter = GameInteraction.GetCurrentChapter();
            var currentArea = GameInteraction.GetCurrentAreaName();
            Logger.LogInformation("Area scenes loaded. Chapter={Chapter}, AreaName={AreaName}", currentChapter, currentArea);

            SetLobbyStage(NetworkLobbyStage.Playing);

            SoftReset();

            UpdateCharactersOwnership();

            lock (ActionLock)
            {
                EnsureForcePaused(NetworkForcedPauseReason.AreaLoading);
                Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);
                GameInteraction.SetPause(true);
            }

            if (IsOutOfSupportedArea(currentChapter, currentArea))
            {
                PlayerNotification.ShowModalMessage(WellKnownKeys.SysMessages.OutOfSupportedAreas.Key);
            }
        }

        /// <summary>
        /// Reloads current party characters and tries to merge ownership
        /// </summary>
        public void UpdateCharactersOwnership()
        {
            Logger.LogInformation("Updating current characters & merging ownership");

            try
            {
                // could be synced from host, but state is the same anyway
                var partyCharacters = GameInteraction.GetPartyPlayers();
                if (partyCharacters.Count == 0)
                {
                    Logger.LogWarning("Can't update ownership due to empty party characters list");
                    return;
                }

                List<NetworkCharacter> oldCharacters = [.. Game.Characters];
                Game.Characters = [.. partyCharacters];
                var defaultOwner = GetHost();
                foreach (var character in Game.Characters)
                {
                    NetworkPlayer historicOwner = null;
                    if (Game.CharactersOwnershipHistory.TryGetValue(character.UnitId, out var playerId))
                    {
                        historicOwner = GetPlayer(playerId);
                    }

                    var owner = historicOwner
                        ?? oldCharacters.FirstOrDefault(old => old.Name == character.Name || old.Name.Contains(character.Name))?.Owner // this one is possible only on initial multiplayer game load when we don't have history yet due to missing UnitIds
                        ?? defaultOwner;

                    character.Owner = owner;
                    UpdateCharacterOwnershipHistory(character);
                    Logger.LogInformation("Character owner has been assigned. UnitId={UnitId}, CharacterName={CharacterName}, Owner={Owner}", character.UnitId, character.Name, character.Owner.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to update characters ownership");
                throw;
            }
        }

        public void OnGameLoaded()
        {
            Logger.LogInformation("OnGameLoaded");
            // Tutorial settings are save dependant, so it must be overriden if save was created without a mod
            var settings = new NetworkGameSettings { Tutorial = new NetworkTutorialSettings() };
            GameInteraction.ApplyGameSettings(settings);
        }

        public void OnPing(NetworkPing ping)
        {
            var message = new NotifyPingedByPlayer
            {
                PlayerId = Game.LocalPlayerId,
                Ping = Mapper.Map<Networking.Messages.Contracts.NetworkPing>(ping)
            };
            Logger.LogInformation("Sending {MessageType}. Type={Type}, WorldPosition={WorldPosition}, TargetUnitId={TargetUnitId}, MapObjectId={MapObjectId}, MapObjectPosition={MapObjectPosition}", nameof(NotifyPingedByPlayer), ping.Type, ping.WorldPosition, ping.UnitId, ping.MapObject?.Id, ping.MapObject?.Position);
            Send(message);

            PingInteraction.Create(null, ping);
        }


        public void OnCutsceneSkip()
        {
            var localPlayer = GetPlayer(Game.LocalPlayerId);
            var message = new NotifyCutsceneSkipped { PlayerId = localPlayer.Id };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyCutsceneSkipped), message.PlayerId);
            Send(message);

            PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Cutscenes.Skipped.Key, args: localPlayer.Name);
        }

        public void ForceLoadGame(string gameId, string savePath)
        {
            if (Game.Stage != NetworkLobbyStage.Playing && Game.Stage != NetworkLobbyStage.Lobby)
            {
                return;
            }

            UpdateSaveInfo(gameId, savePath);

            Game.ForcedPause = null;
            ResetGameIdGenerator();
            Game.LoadedSaveSeed = CreateRandomSeed();

            var content = FileSystem.GetRawFileContent(savePath);
            var message = new NotifyGameForceLoaded
            {
                GameId = Game.Id,
                Content = content,
                Seed = Game.LoadedSaveSeed,
            };
            Logger.LogInformation("Sending {MessageType}. GameId={GameId}, SavePath={SavePath}, ContentSize={ContentSize}, LoadedSaveSeed={LoadedSaveSeed}", nameof(NotifyGameForceLoaded), message.GameId, Game.StartUp.SavePath, message.Content.Length, message.Seed);

            Send(message);
        }

        public virtual void CombatStarted()
        {
            Logger.LogInformation("Combat started");
            if (Game.Combat != null)
            {
                Logger.LogWarning("Previous combat has not been disposed correctly");
            }

            Game.Combat = new NetworkCombat();
            Game.LastCombatTurn = null;

            var unitsInCombat = CombatInteraction.GetCombatState();
            var message = new NotifyCombatStarted
            {
                PlayerId = Game.LocalPlayerId,
                State = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(unitsInCombat),
            };
            Logger.LogInformation("Sending {MessageType}. UnitsCount={UnitsCount}", nameof(NotifyCombatStarted), message.State.Units.Count);
            Send(message);
        }

        public void CombatEnded()
        {
            Logger.LogInformation("Combat ended");
            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started correctly");
            }

            SaveLastCombatTurn();

            Game.Combat = null;
            _valueGenerator.ResetSeedGenerators(SeedLifetime.Combat);
        }
        public void OnHandleDelayCombatTurn(string unitId, string targetUnitId)
        {
            if (!IsControlledByLocalPlayer(unitId))
            {
                return;
            }

            var message = new NotifyCombatTurnDelayed
            {
                UnitId = unitId,
                TargetUnitId = targetUnitId
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={Round}, TargetUnitId={TargetUnitId}", nameof(NotifyCombatTurnDelayed), message.UnitId, message.TargetUnitId);

            Send(message);
        }

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            if (Game.Combat.Turn != null && Game.Combat.Turn.IsInProgress)
            {
                if (!string.Equals(Game.Combat.Turn.UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Invalid unit turn start detected. ExpectedUnitId={ExpectedUnitId}, ActualUnitId={ActualUnitId}", Game.Combat.Turn.UnitId, unitId);
                    InitializeNewTurn(unitId, actingInSurpriseRound);
                    return false;
                }

                UpdateConfirmedMidCombatUnits();
                Game.Combat.AIActions.Clear();
                Logger.LogInformation("Turn start is allowed. UnitId={UnitId}, IsActingInSurpiseRound={IsActingInSurpiseRound}, TurnUnitId={TurnUnitId}", unitId, actingInSurpriseRound, Game.Combat.Turn.UnitId);
                return true;
            }

            InitializeNewTurn(unitId, actingInSurpriseRound);

            return false;
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            if (Game.Combat.Turn.IsAI || !Game.Combat.Turn.IsInProgress)
            {
                Logger.LogInformation("Turn end is allowed. Round={Round}, TurnUnitId={TurnUnitId}, IsAI={IsAI}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn.UnitId, Game.Combat.Turn.IsAI, unitId);
                ResetCombatTurn();
                return true;
            }

            if (Game.Combat.Turn.IsLocalPlayer)
            {
                Logger.LogInformation("Ending local player turn. Round={Round}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn.UnitId);
                var message = new NotifyPlayerCombatTurnEnded { UnitId = Game.Combat.Turn.UnitId, PlayerId = Game.LocalPlayerId };
                Send(message);
            }

            Game.Combat.Turn.IsInProgress = false;
            return false;
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
                    Logger.LogInformation("Unit has been allowed to join mid combat. UnitId={UnitId}", unitId);
                    return true;
                }

                var isFirstJoinEvent = !IsPlayerReady(PlayerTurnReadinessType.UnitJoinedMidCombat, Game.LocalPlayerId, unitId);
                if (isFirstJoinEvent)
                {
                    Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}", nameof(NotifyUnitJoinedMidCombat), unitId);
                    var message = new NotifyUnitJoinedMidCombat { UnitId = unitId, PlayerId = Game.LocalPlayerId };
                    Send(message);
                }

                AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, Game.LocalPlayerId, unitId);

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to process unit join combat request. UnitId={UnitId}", unitId);
                throw;
            }
        }

        public string GetCharacterOwnerName(string unitId)
        {
            var character = GetPartyCharacter(unitId);
            return character?.Owner?.Name;
        }

        public void OnStartGameMode(GameModeType type)
        {
            var isFirstTime = RegisterGameMode(type, Game.LocalPlayerId);
            if (!isFirstTime)
            {
                return;
            }

            if (type == GameModeType.Rest)
            {
                UpdateRestUIState();
            }

            // pause has been initiated by someone else
            if (type == GameModeType.Pause && Game.ForcedPause != null)
            {
                lock (ActionLock)
                {
                    Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);
                }
            }

            var message = new NotifyGameModeTypeStarted { PlayerId = Game.LocalPlayerId, Type = type.Name };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Type={Type}", nameof(NotifyGameModeTypeStarted), message.PlayerId, message.Type);
            Send(message);
        }

        public void OnStopGameMode(GameModeType type)
        {
            var isFirstTime = UnregisterGameMode(type, Game.LocalPlayerId);
            if (!isFirstTime)
            {
                return;
            }

            if (type == GameModeType.Rest)
            {
                lock (ActionLock)
                {
                    OnLocalRestGameModeEnded();
                }
            }
            else if (type == GameModeType.Dialog)
            {
                Game.Dialog = null;
            }

            var message = new NotifyGameModeTypeEnded { PlayerId = Game.LocalPlayerId, Type = type.Name };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Type={Type}", nameof(NotifyGameModeTypeEnded), message.PlayerId, message.Type);
            Send(message);
        }

        public void OnCapitalModeRest()
        {
            var message = new NotifyCapitalModeRestInitiated();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyCapitalModeRestInitiated));
            Send(message);
        }

        public virtual void OnStartRest()
        {
            Game.Rest = new NetworkRest();
        }

        public void OnStartRestSleepPhase()
        {
            lock (ActionLock)
            {
                Game.Rest.SleepPhase++;
                Logger.LogInformation("Rest sleep phase has been updated. SleepPhase={SleepPhase}", Game.Rest.SleepPhase);
            }
        }

        public void OnShowRestView(RestPhase phase)
        {
            if (phase != RestPhase.ShowingResults)
            {
                return;
            }

            lock (ActionLock)
            {
                AddPlayerToTracker(Game.Rest.PlayersFinishedRest, Game.LocalPlayerId);
                UpdateRestResultsUIState();
            }

            var message = new NotifyRestEnded { PlayerId = Game.LocalPlayerId };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyRestEnded), message.PlayerId);
            Send(message);
        }

        public void OnInterrupRestBanterBark(NetworkRestBanter networkBanter)
        {
            var message = new NotifyRestBanterInterrupted
            {
                Banter = Mapper.Map<Networking.Messages.Contracts.NetworkRestBanter>(networkBanter),
            };

            Logger.LogInformation("Sending {MessageType}. SpeakerUnitId={SpeakerUnitId}, Key={Key}", nameof(NotifyRestBanterInterrupted), message.Banter.SpeakerUnitId, message.Banter.Key);
            Send(message);
        }

        public void OnTransferVendorItem(NetworkVendorItemTransfer transfer)
        {
            var message = new NotifyVendorItemTransferred
            {
                ItemTransfer = Mapper.Map<Networking.Messages.Contracts.NetworkVendorItemTransfer>(transfer)
            };
            Logger.LogInformation("Sending {MessageType}. ItemId={ItemId}, Count={Count}, Action={Action}, ActionTarget={ActionTarget}", nameof(NetworkVendorItemTransfer), message.ItemTransfer.Item.UniqueId, message.ItemTransfer.Count, message.ItemTransfer.ItemAction, message.ItemTransfer.ItemActionTarget);
            Send(message);
        }
        public void OnMemorizeSpell(NetworkSpellSlot slot)
        {
            var message = new NotifySpellMemorized
            {
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkSpellSlot>(slot)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, SpellbookId={SpellbookId}, SpellId={SpellId}, SpellLevel={SpellLevel}, SpellName={SpellName}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellMemorized), slot.UnitId, slot.SpellbookId, slot.SpellId, slot.SpellLevel, slot.SpellName, slot.Index, slot.Type);

            Send(message);
        }

        public void OnForgetSpell(NetworkSpellSlot slot)
        {
            var message = new NotifySpellForgotten
            {
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkSpellSlot>(slot)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellForgotten), slot.UnitId, slot.SpellbookId, slot.Index, slot.Type);

            Send(message);
        }

        /// <summary>
        /// Unlike OnRequestLevelingUI, this method is used when game is enforcing opening leveling ui
        /// State should be the same across players since it's not a result of their action
        /// Also, we shouldn't deny opening it as it contains few 'OnStop' actions
        /// E.g. dialog -> mythic path selection at the end of act 2 -> dialog
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="networkLevelingType"></param>
        public void OnForceLevelingUI(string unitId, NetworkLevelingType networkLevelingType)
        {
            InitiateLeveling(unitId, networkLevelingType);
            Logger.LogInformation("Leveling UI has been forced by the game. UnitId={UnitId}, Type={Type}", Game.Leveling.UnitId, Game.Leveling.Type);
        }

        public void OnLevelingMythicClassSelected(string mythicClassId)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingMythicClassSelected
            {
                MythicClassId = mythicClassId
            };
            Logger.LogInformation("Sending {MessageType}. MythicClassId={MythicClassId}", nameof(NotifyLevelingMythicClassSelected), mythicClassId);
            Send(message);
        }

        public void OnLevelingClassSelected(NetworkLevelingClass levelingClass)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingClassSelected
            {
                Class = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingClass>(levelingClass)
            };
            Logger.LogInformation("Sending {MessageType}. Id={Id}, Name={Name}", nameof(NotifyLevelingClassSelected), message.Class.Id, message.Class.Name);
            Send(message);
        }

        public void OnLevelingClassArchetypeSelected(NetworkLevelingArchetype archetype)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingClassArchetypeSelected
            {
                Archetype = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingArchetype>(archetype)
            };
            Logger.LogInformation("Sending {MessageType}. Id={Id}, Name={Name}", nameof(NotifyLevelingClassArchetypeSelected), message.Archetype?.Id, message.Archetype?.Name);
            Send(message);
        }

        public bool CanMakeLevelingDecisions()
        {
            if (Game.Leveling == null)
            {
                return false;
            }

            var characterControl = Game.Leveling.Type switch
            {
                // we don't have character id yet, so we rely on 'fake' character
                NetworkLevelingType.NewGameSequence => Game.Characters.FirstOrDefault()?.Owner?.Id == Game.LocalPlayerId,
                NetworkLevelingType.Mercenary => HasControlOverUI,
                // either this character has been controlled by someone previously (and that player is still in lobby) or fallback to default
                _ => WasControlledByCurrentPlayer(Game.Leveling.UnitId),
            };

            return characterControl && Game.Leveling.PlayerReadiness.Count >= GetSyncedPlayersCount();
        }

        public bool CanMakeNewGameSequenceDecisions()
        {
            return HasControlOverUI;
        }

        public void OnLevelingWitnessPhase(NetworkLevelingPhase phase)
        {
            if (Game.Leveling.PhaseIndex != phase.Index && CanMakeLevelingDecisions())
            {
                var phaseChangedMessage = new NotifyLevelingPhaseChanged
                {
                    Phase = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingPhase>(phase)
                };
                Send(phaseChangedMessage);
                Game.Leveling.PhaseIndex = phase.Index;
                ResetPlayersTracker(Game.Leveling.PlayerReadiness);
                Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingPhaseChanged), phaseChangedMessage.Phase.Index);
            }

            WitnessLevelingPhase(Game.LocalPlayerId);
            var message = new NotifyLevelingPhaseWitnessed
            {
                PlayerId = Game.LocalPlayerId,
                Phase = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingPhase>(phase)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Index={Index}", nameof(NotifyLevelingPhaseWitnessed), message.PlayerId, message.Phase.Index);
            Send(message);
        }

        public string GetNewGameSequenceId()
        {
            return Game.Id;
        }

        public void OnNewGameSequenceWitnessPhase(NetworkNewGameSequencePhase phase)
        {
            if (Game.StartUp.PhaseType != phase.Type && CanMakeNewGameSequenceDecisions())
            {
                var phaseChangedMessage = new NotifyNewGameSequencePhaseChanged
                {
                    Phase = Mapper.Map<Networking.Messages.Contracts.NetworkNewGameSequencePhase>(phase)
                };
                Send(phaseChangedMessage);
                Game.StartUp.PhaseType = phase.Type;
                ResetPlayersTracker(Game.StartUp.ReadyPlayers);
                Logger.LogInformation("Sending {MessageType}. Type={Type}", nameof(NotifyNewGameSequencePhaseChanged), phaseChangedMessage.Phase.Type);
            }

            WitnessNewGameSequencePhase(Game.LocalPlayerId, phase.Type);
            var message = new NotifyNewGameSequenceWitnessed
            {
                PlayerId = Game.LocalPlayerId,
                Phase = Mapper.Map<Networking.Messages.Contracts.NetworkNewGameSequencePhase>(phase)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Type={Type}", nameof(NotifyNewGameSequenceWitnessed), message.PlayerId, message.Phase.Type);
            Send(message);
        }

        public void OnLevelingIncreaseSkillPoint(NetworkLevelingSkillPoint skill)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingSkillPointIncreased
            {
                Skill = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingSkillPoint>(skill)
            };
            Logger.LogInformation("Sending {MessageType}. StatType={StatType}", nameof(NotifyLevelingSkillPointIncreased), message.Skill.StatType);
            Send(message);
        }

        public void OnLevelingDecreaseSkillPoint(NetworkLevelingSkillPoint skill)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingSkillPointDecreased
            {
                Skill = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingSkillPoint>(skill)
            };
            Logger.LogInformation("Sending {MessageType}. StatType={StatType}", nameof(NotifyLevelingSkillPointDecreased), message.Skill.StatType);
            Send(message);
        }

        public void OnLevelingIncreaseAbilityScore(NetworkLevelingAbilityScore abilityScore)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingAbilityScoreIncreased
            {
                AbilityScore = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingAbilityScore>(abilityScore)
            };
            Logger.LogInformation("Sending {MessageType}. StatType={StatType}", nameof(NotifyLevelingAbilityScoreIncreased), message.AbilityScore.StatType);
            Send(message);
        }

        public void OnLevelingDecreaseAbilityScore(NetworkLevelingAbilityScore abilityScore)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingAbilityScoreDecreased
            {
                AbilityScore = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingAbilityScore>(abilityScore)
            };
            Logger.LogInformation("Sending {MessageType}. StatType={StatType}", nameof(NotifyLevelingAbilityScoreDecreased), message.AbilityScore.StatType);
            Send(message);
        }

        public void OnLevelingPortraitSelected(NetworkLevelingPortrait levelingPortrait)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingPortraitSelected
            {
                Portrait = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingPortrait>(levelingPortrait)
            };
            Logger.LogInformation("Sending {MessageType}. Name={Name}, CustomId={CustomId}, Category={Category}", nameof(NotifyLevelingPortraitSelected), message.Portrait.Name, message.Portrait.CustomId, message.Portrait.Category);
            Send(message);
        }

        public void OnLevelingVoiceSelected(NetworkLevelingVoice levelingVoice)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingVoiceSelected
            {
                Voice = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingVoice>(levelingVoice)
            };
            Logger.LogInformation("Sending {MessageType}. Id={Id}, GenderId={GenderId}", nameof(NotifyLevelingVoiceSelected), message.Voice.Id, message.Voice.GenderId);
            Send(message);
        }

        public void OnLevelingRaceSelected(string raceId)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingRaceSelected
            {
                RaceId = raceId
            };
            Logger.LogInformation("Sending {MessageType}. RaceId={RaceId}", nameof(NotifyLevelingRaceSelected), message.RaceId);
            Send(message);
        }

        public void OnLevelingGenderSelected(string genderId)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingGenderSelected
            {
                GenderId = genderId
            };
            Logger.LogInformation("Sending {MessageType}. GenderId={GenderId}", nameof(NotifyLevelingGenderSelected), message.GenderId);
            Send(message);
        }

        public void OnLevelingAlignmentSelected(string alignmentId)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingAlignmentSelected
            {
                AlignmentId = alignmentId
            };
            Logger.LogInformation("Sending {MessageType}. AlignmentId={AlignmentId}", nameof(NotifyLevelingAlignmentSelected), message.AlignmentId);
            Send(message);
        }

        public void OnLevelingNameChanged(string name)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingNameChanged
            {
                Name = name
            };
            Logger.LogInformation("Sending {MessageType}. Name={Name}", nameof(NotifyLevelingNameChanged), message.Name);
            Send(message);
        }

        public void OnLevelingRacialAbilityScoreBonusChanged(NetworkLevelingSequenceDirection direction)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingRacialAbilityScoreBonusChanged
            {
                Direction = direction.ToString()
            };
            Logger.LogInformation("Sending {MessageType}. Direction={Direction}", nameof(NotifyLevelingRacialAbilityScoreBonusChanged), message.Direction);
            Send(message);
        }

        public void OnLevelingBirthMonthChanged(NetworkLevelingSequenceDirection direction)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingBirthMonthChanged
            {
                Direction = direction.ToString()
            };
            Logger.LogInformation("Sending {MessageType}. Direction={Direction}", nameof(NotifyLevelingBirthMonthChanged), message.Direction);
            Send(message);
        }

        public void OnLevelingBirthDayChanged(NetworkLevelingSequenceDirection direction)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingBirthDayChanged
            {
                Direction = direction.ToString()
            };
            Logger.LogInformation("Sending {MessageType}. Direction={Direction}", nameof(NotifyLevelingBirthDayChanged), message.Direction);
            Send(message);
        }

        public void OnLevelingBodyTypeAppearanceChanged(int index)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingBodyTypeAppearanceChanged
            {
                Index = index
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingBodyTypeAppearanceChanged), message.Index);
            Send(message);
        }

        public void OnLevelingFaceAppearanceChanged(int index)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingFaceAppearanceChanged
            {
                Index = index
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingFaceAppearanceChanged), message.Index);
            Send(message);
        }

        public void OnLevelingScarAppearanceChanged(int index)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingScarAppearanceChanged
            {
                Index = index
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingScarAppearanceChanged), message.Index);
            Send(message);
        }

        public void OnLevelingHairStyleAppearanceChanged(int index)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingHairStyleAppearanceChanged
            {
                Index = index
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingHairStyleAppearanceChanged), message.Index);
            Send(message);
        }

        public void OnLevelingHornsAppearanceChanged(int index)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingHornsAppearanceChanged
            {
                Index = index
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingHornsAppearanceChanged), message.Index);
            Send(message);
        }

        public void OnLevelingWarpaintAppearanceChanged(NetworkLevelingWarpaint levelingWarpaint)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingWarpaintAppearanceChanged
            {
                Warpaint = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingWarpaint>(levelingWarpaint)
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}, PageNumber={PageNumber}", nameof(NotifyLevelingWarpaintAppearanceChanged), message.Warpaint.Index, message.Warpaint.PageNumber);
            Send(message);
        }

        public void OnLevelingTattooAppearanceChanged(NetworkLevelingTattoo levelingTattoo)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingTattooAppearanceChanged
            {
                Tattoo = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingTattoo>(levelingTattoo)
            };
            Logger.LogInformation("Sending {MessageType}. Index={Index}, PageNumber={PageNumber}", nameof(NotifyLevelingTattooAppearanceChanged), message.Tattoo.Index, message.Tattoo.PageNumber);
            Send(message);
        }

        public void OnLevelingBodyColorAppearanceChanged(string textureName)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingBodyColorAppearanceChanged
            {
                TextureName = textureName
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}", nameof(NotifyLevelingBodyColorAppearanceChanged), message.TextureName);
            Send(message);
        }

        public void OnLevelingEyesColorAppearanceChanged(string textureName)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingEyesColorAppearanceChanged
            {
                TextureName = textureName
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}", nameof(NotifyLevelingEyesColorAppearanceChanged), message.TextureName);
            Send(message);
        }

        public void OnLevelingHairColorAppearanceChanged(string textureName)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingHairColorAppearanceChanged
            {
                TextureName = textureName
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}", nameof(NotifyLevelingHairColorAppearanceChanged), message.TextureName);
            Send(message);
        }

        public void OnLevelingHornsColorAppearanceChanged(string textureName)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingHornsColorAppearanceChanged
            {
                TextureName = textureName
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}", nameof(NotifyLevelingHornsColorAppearanceChanged), message.TextureName);
            Send(message);
        }

        public void OnLevelingWarpaintColorAppearanceChanged(NetworkLevelingWarpaint levelingWarpaint)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingWarpaintColorAppearanceChanged
            {
                Warpaint = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingWarpaint>(levelingWarpaint)
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}, PageNumber={PageNumber}", nameof(NotifyLevelingWarpaintColorAppearanceChanged), message.Warpaint.TextureName, message.Warpaint.PageNumber);
            Send(message);
        }

        public void OnLevelingTattooColorAppearanceChanged(NetworkLevelingTattoo levelingTattoo)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingTattooColorAppearanceChanged
            {
                Tattoo = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingTattoo>(levelingTattoo)
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}, PageNumber={PageNumber}", nameof(NotifyLevelingTattooColorAppearanceChanged), message.Tattoo.TextureName, message.Tattoo.PageNumber);
            Send(message);
        }

        public void OnLevelingPrimaryOutfitColorAppearanceChanged(string textureName)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingPrimaryOutfitColorAppearanceChanged
            {
                TextureName = textureName
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}", nameof(NotifyLevelingPrimaryOutfitColorAppearanceChanged), message.TextureName);
            Send(message);
        }

        public void OnLevelingSecondaryOutfitColorAppearanceChanged(string textureName)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingSecondaryOutfitColorAppearanceChanged
            {
                TextureName = textureName
            };
            Logger.LogInformation("Sending {MessageType}. TextureName={TextureName}", nameof(NotifyLevelingSecondaryOutfitColorAppearanceChanged), message.TextureName);
            Send(message);
        }

        public void OnLevelingRespecCompleted()
        {
            ResetPlayersTracker(Game.PlayersInRespecWindow);
            var message = new NotifyLevelingRespecCompleted();
            Logger.LogInformation("Sending {MessageType}", nameof(NotifyLevelingRespecCompleted));
            Send(message);
        }

        public void OnLevelingRespecWindowShown(string unitId)
        {
            AddPlayerToTracker(Game.PlayersInRespecWindow, Game.LocalPlayerId);

            UpdateLevelingRespecUIState(unitId);

            var message = new NotifyLevelingRespecWindowShown
            {
                PlayerId = Game.LocalPlayerId,
                UnitId = unitId
            };

            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyLevelingRespecWindowShown), message.PlayerId, message.UnitId);
            Send(message);
        }

        public void OnLevelingRespecLevelUp()
        {
            RemovePlayerFromTracker(Game.PlayersInRespecWindow, Game.LocalPlayerId);

            var message = new NotifyLevelingRespecLevelUp
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingRespecLevelUp), message.PlayerId);
            Send(message);
        }

        public void OnLevelingRespecMythicLevelUp()
        {
            RemovePlayerFromTracker(Game.PlayersInRespecWindow, Game.LocalPlayerId);

            var message = new NotifyLevelingRespecMythicLevelUp
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingRespecMythicLevelUp), message.PlayerId);
            Send(message);
        }

        public void OnLevelingFeatureSelected(NetworkLevelingFeature feature)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingFeatureSelected
            {
                Feature = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingFeature>(feature)
            };
            Logger.LogInformation("Sending {MessageType}. FeatureName={FeatureName}, Id={Id}", nameof(NotifyLevelingFeatureSelected), message.Feature.Name, message.Feature.Id);
            Send(message);
        }

        public void OnLevelingSpellRemoved(NetworkLevelingSpell spell)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingSpellRemoved
            {
                Spell = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingSpell>(spell)
            };
            Logger.LogInformation("Sending {MessageType}. SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellRemoved), message.Spell.Name, message.Spell.Id);
            Send(message);
        }

        public void OnLevelingSpellChosen(NetworkLevelingSpell spell)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingSpellChosen
            {
                Spell = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingSpell>(spell)
            };
            Logger.LogInformation("Sending {MessageType}. SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellChosen), message.Spell.Name, message.Spell.Id);
            Send(message);
        }

        public void OnLevelingTerminated()
        {
            Logger.LogInformation("Leveling has been terminated. UnitId={unitId}, Type={Type}", Game.Leveling.UnitId, Game.Leveling.Type);

            if (CanMakeLevelingDecisions())
            {
                var message = new NotifyLevelingTerminated();
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLevelingTerminated));
                Send(message);
            }

            if (Game.Leveling.Type == NetworkLevelingType.NewGameSequence)
            {
                ResetPlayersTracker(Game.StartUp.ReadyPlayers);
            }

            var characterName = GameInteraction.GetUnitCharacterName(Game.Leveling.UnitId);
            var messageKey = Game.Leveling.Type switch
            {
                NetworkLevelingType.MythicLeveling => WellKnownKeys.GameNotifications.Leveling.MythicLeveling.Terminated.Key,
                NetworkLevelingType.Mercenary => WellKnownKeys.GameNotifications.Leveling.Mercenary.Terminated.Key,
                NetworkLevelingType.NewGameSequence => null,
                NetworkLevelingType.Leveling or _ => WellKnownKeys.GameNotifications.Leveling.Terminated.Key
            };

            if (!string.IsNullOrEmpty(messageKey))
            {
                PlayerNotification.ShowWarningNotification(messageKey, args: characterName);
            }

            Game.Leveling = null;
        }

        public void OnLevelingCompleted()
        {
            Logger.LogInformation("Leveling has been completed. UnitId={UnitId}, Type={Type}", Game.Leveling.UnitId, Game.Leveling.Type);

            if (CanMakeLevelingDecisions())
            {
                var message = new NotifyLevelingCompleted();
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLevelingCompleted));
                Send(message);
            }

            var characterName = GameInteraction.GetUnitCharacterName(Game.Leveling.UnitId);
            var messageKey = Game.Leveling.Type switch
            {
                NetworkLevelingType.MythicLeveling => WellKnownKeys.GameNotifications.Leveling.MythicLeveling.Completed.Key,
                NetworkLevelingType.Mercenary => WellKnownKeys.GameNotifications.Leveling.Mercenary.Completed.Key,
                NetworkLevelingType.NewGameSequence => null,
                NetworkLevelingType.Leveling or _ => WellKnownKeys.GameNotifications.Leveling.Completed.Key,
            };

            if (!string.IsNullOrEmpty(messageKey))
            {
                PlayerNotification.AddCombatText(messageKey, characterName);
            }

            Game.Leveling = null;
        }

        public void OnCharacterSelectionWindowShown()
        {
            AddPlayerToTracker(Game.PlayersInCharacterSelectionWindow, Game.LocalPlayerId);

            UpdateCharacterSelectionUIState();

            var message = new NotifyCharacterSelectionWindowShown
            {
                PlayerId = Game.LocalPlayerId
            };

            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyCharacterSelectionWindowShown), message.PlayerId);
            Send(message);
        }

        public void MoveNonCombatCharacter(NetworkCharacterMove move)
        {
            if (Game.Combat != null)
            {
                return;
            }

            var message = new NotifyCharacterMove
            {
                Move = Mapper.Map<Networking.Messages.Contracts.NetworkCharacterMove>(move)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, Destination={Destination}, Delay={Delay}, Orientation={Orientation}", nameof(NotifyCharacterMove), message.Move.UnitId, message.Move.Destination, message.Move.Delay, message.Move.Orientation);

            Send(message);
        }

        public void OnMoveActionBarSlot(NetworkActionBarSlot sourceActionBarSlot, NetworkActionBarSlot targetActionBarSlot)
        {
            if (!IsControlledByLocalPlayer(sourceActionBarSlot.UnitId))
            {
                return;
            }

            var message = new NotifyActionBarSlotMoved
            {
                SourceActionBarSlot = Mapper.Map<Networking.Messages.Contracts.NetworkActionBarSlot>(sourceActionBarSlot),
                TargetActionBarSlot = Mapper.Map<Networking.Messages.Contracts.NetworkActionBarSlot>(targetActionBarSlot),
            };

            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, SourceSlotIndex={SourceSlotIndex}, SourceSlotAbilityId={SourceSlotAbilityId}, SourceSlotActivatableAbilityId={SourceSlotActivatableAbilityId}, SourceSlotItemId={SourceSlotItemId}, TargetSlotIndex={TargetSlotIndex}, TargetSlotAbilityId={TargetSlotAbilityId}, TargetSlotActivatableAbilityId={TargetSlotActivatableAbilityId}, TargetSlotItemId={TargetSlotItemId}",
                nameof(NotifyActionBarSlotMoved), sourceActionBarSlot.UnitId, sourceActionBarSlot.Index, sourceActionBarSlot.Ability?.Id, sourceActionBarSlot.ActivatableAbility?.Id, sourceActionBarSlot.Item?.UniqueId, targetActionBarSlot.Index, targetActionBarSlot.Ability?.Id, targetActionBarSlot.ActivatableAbility?.Id, targetActionBarSlot.Item?.UniqueId);

            Send(message);
        }

        public void OnClearActionBarSlot(NetworkActionBarSlot actionBarSlot)
        {
            if (!IsControlledByLocalPlayer(actionBarSlot.UnitId))
            {
                return;
            }

            var message = new NotifyActionBarSlotCleared
            {
                ActionBarSlot = Mapper.Map<Networking.Messages.Contracts.NetworkActionBarSlot>(actionBarSlot)
            };
            Logger.LogInformation("Sending {MessageType}. SlotIndex={SlotIndex}, UnitId={UnitId}", nameof(NetworkActionBarSlot), message.ActionBarSlot.Index, message.ActionBarSlot.UnitId);

            Send(message);
        }

        public void OnLockpickInteraction(NetworkLockpickInteraction lockpickInteraction)
        {
            var message = new NotifyMapObjectLockpicked
            {
                LockpickInteraction = Mapper.Map<Networking.Messages.Contracts.NetworkLockpickInteraction>(lockpickInteraction)
            };
            Logger.LogInformation("Sending {MessageType}. MapObjectId={MapObjectId}, MapObjectPosition={MapObjectPosition}, Units={Units}", nameof(NotifyMapObjectLockpicked), message.LockpickInteraction.MapObject.Id, message.LockpickInteraction.MapObject.Position, message.LockpickInteraction.Units);
            Send(message);
        }

        public void OnSetUnitStealthEnabled(string unitId, bool isEnabled, bool isForced)
        {
            var message = new NotifyUnitStealthChanged
            {
                UnitId = unitId,
                IsEnabled = isEnabled,
                IsForced = isForced
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, IsEnabled={IsEnabled}, IsForced={IsForced}", nameof(NotifyUnitStealthChanged), message.UnitId, message.IsEnabled, message.IsForced);
            Send(message);
        }

        public void OnGlobalMapMessageBoxShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapLocationMessage, Game.LocalPlayerId);

            var message = new NotifyGlobalMapLocationMessageShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapLocationMessageShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapLocationMessageUIState();
        }

        public void OnShowGroupChangerUI()
        {
            AddPlayerToTracker(Game.PlayersInGroupChanger, Game.LocalPlayerId);

            var message = new NotifyGroupChangerOpened
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGroupChangerOpened), message.PlayerId);
            Send(message);

            UpdateGroupManagerUIState();
        }

        public void OnSkipTimeOpened()
        {
            AddPlayerToTracker(Game.PlayersInSkipTime, Game.LocalPlayerId);

            var message = new NotifySkipTimeOpened
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifySkipTimeOpened), message.PlayerId);
            Send(message);

            UpdateSkipTimeUIState();
        }

        public void OnDialogPopupShown(NetworkDialogPopup networkDialogPopup)
        {
            AddPlayerToTracker(Game.PlayersInDialogPopup, Game.LocalPlayerId);

            var message = new NotifyDialogPopupShown
            {
                PlayerId = Game.LocalPlayerId,
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkDialogPopup>(networkDialogPopup)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", nameof(NotifyDialogPopupShown), message.PlayerId, message.Popup.AreaName, message.Popup.DialogName, message.Popup.CueName);
            Send(message);

            UpdateDialogPopupState();
        }

        public void OnGlobalMapCommonPopupShown(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCommonPopup, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCommonPopupShown
            {
                PlayerId = Game.LocalPlayerId,
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapCommonPopup>(globalMapCommonPopup)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupShown), message.PlayerId, message.Popup.Type, message.Popup.Location?.Id, message.Popup.Location?.Name);
            Send(message);

            UpdateGlobalMapCommonPopupUIState(globalMapCommonPopup);
        }

        public void OnGlobalMapEncounterMessageShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapEncounterMessage, Game.LocalPlayerId);

            var message = new NotifyGlobalMapEncounterMessageShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapEncounterMessageShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapEncounterMessageUIState();
        }

        public void OnGlobalMapDisposed()
        {
            ResetGlobalMapCounters();
        }

        public void OnGlobalMapTravelerModeChanged(NetworkGlobalMapTravelerMode travelerMode)
        {
            RegisterGlobalMapMode(Game.LocalPlayerId, travelerMode);

            var message = new NotifyGlobalMapTravelerModeChanged
            {
                PlayerId = Game.LocalPlayerId,
                TravelerMode = travelerMode.ToString(),
                MustBeEnforced = HasControlOverUI
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, TravelerMode={TravelerMode}, MustBeEnforced={MustBeEnforced}", nameof(NotifyGlobalMapTravelerModeChanged), message.PlayerId, message.TravelerMode, message.MustBeEnforced);
            Send(message);

            UpdateGlobalMapUIState();
        }

        public void OnUnitDeath(string unitId)
        {
            if (Game.Combat?.Turn == null)
            {
                return;
            }

            var message = new NotifyCombatUnitKilled
            {
                PlayerId = Game.LocalPlayerId,
                UnitId = unitId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyCombatUnitKilled), message.PlayerId, message.UnitId);
            Send(message);
        }

        public void OnTrapDisarmRolled(NetworkTrapDisarm trapDisarm)
        {
            var message = new NotifyTrapDisarmRolled
            {
                TrapDisarm = Mapper.Map<Networking.Messages.Contracts.NetworkTrapDisarm>(trapDisarm)
            };
            Logger.LogInformation("Sending {MessageType}. TrapId={TrapId}, Position={Position}, Roll={Roll}, IsSuccess={IsSuccess}, UnitId={UnitId}", nameof(NotifyTrapDisarmRolled), message.TrapDisarm.MapObject.Id, message.TrapDisarm.MapObject.Position, message.TrapDisarm.Roll, message.TrapDisarm.IsSuccess, message.TrapDisarm.UnitId);
            Send(message);
        }

        public void OnUnitAutoUseAbilityChanged(string unitId, NetworkAbility networkAbility)
        {
            if (!IsControlledByLocalPlayer(unitId))
            {
                return;
            }

            var message = new NotifyUnitAutoUseAbilityChanged
            {
                UnitId = unitId,
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(networkAbility)
            };
            Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", nameof(NotifyUnitAutoUseAbilityChanged), message.UnitId, message.Ability?.Id, message.Ability?.Name);
            Send(message);
        }

        public void OnZoneLootCollectorButtonsUpdated()
        {
            UpdateZoneLootUIState();
        }

        public void OnZoneLootShown()
        {
            AddPlayerToTracker(Game.PlayersInZoneLoot, Game.LocalPlayerId);

            var message = new NotifyZoneLootShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootShown), message.PlayerId);
            Send(message);

            UpdateZoneLootUIState();
        }

        public void OnZoneLootClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInZoneLoot, Game.LocalPlayerId);

            var message = new NotifyZoneLootClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootClosed), message.PlayerId);
            Send(message);
        }

        public bool ReadyChanged()
        {
            var player = Game.Players.First(p => p.Id == Game.LocalPlayerId);
            return ReadyChanged(player, !player.IsReady);
        }

        public void OnCrusadeArmyBattleResultsShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults, Game.LocalPlayerId);

            var message = new NotifyCrusadeArmyBattleResultsShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyCrusadeArmyBattleResultsShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyBattleResultsUIState();
        }

        public void OnGlobalMapCombatResultsShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCombatResults, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCombatResultsShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCombatResultsShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapCombatResultsUIState();
        }

        public void OnGlobalMapCrusadeArmyInfoShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfo, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyInfoShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyInfoShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyInfoUIState();
        }

        public void OnGlobalMapCrusadeArmyMergeCartClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyMergeCartClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyMergeCartClosed), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyInfoUIStateAfterMerge();
        }

        public void OnGlobalMapCrusadeArmyInfoMergeShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyMergeCartShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyMergeCartShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyInfoUIStateOnMerge();
        }

        public void OnGlobalMapCrusadeArmySetLeaderShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmySetLeaderShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmySetLeaderShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyInfoUIStateOnSetLeader();
        }

        public void OnGlobalMapCrusadeArmySetLeaderClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmySetLeaderClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmySetLeaderClosed), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader();
        }

        public void OnGlobalMapCrusadeArmyBuyLeaderShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyBuyLeaderShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyBuyLeaderShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyBuyLeaderUIState();
        }

        public bool TogglePause(bool isPaused)
        {
            lock (ActionLock)
            {
                if (isPaused)
                {
                    var canContinue = OnToggleOffPause(out var showReason);
                    if (canContinue)
                    {
                        return true;
                    }

                    if (showReason)
                    {
                        ShowForcedPauseReason();
                    }
                    return false;
                }

                if (Game.ForcedPause == null)
                {
                    EnsureForcePaused(NetworkForcedPauseReason.Manual, removalDelay: null);
                    Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);

                    var pauseStarted = new NotifyGamePauseStarted
                    {
                        PlayerId = Game.LocalPlayerId,
                        Pause = Mapper.Map<Networking.Messages.Contracts.NetworkForcedPause>(Game.ForcedPause)
                    };
                    Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Reason={Reason}, Delay={Delay}", nameof(NotifyGamePauseStarted), pauseStarted.PlayerId, pauseStarted.Pause.Reason, pauseStarted.Pause.RemovalDelay);
                    Send(pauseStarted);
                    return true;
                }

                return false;
            }
        }

        public void OnGlobalMapCrusadeArmyBuyLeaderClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyBuyLeaderClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyBuyLeaderClosed), message.PlayerId);
            Send(message);

            UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader();
        }

        public void OnGlobalMapRecruitmentShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapRecruitment, Game.LocalPlayerId);

            var message = new NotifyGlobalMapRecruitmentShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapRecruitmentShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapRecruitmentUIState();
        }

        public void OnGlobalMapRecruitmentClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapRecruitment, Game.LocalPlayerId);

            var message = new NotifyGlobalMapRecruitmentClosed
            {
                PlayerId = Game.LocalPlayerId
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapRecruitmentClosed), message.PlayerId);
            Send(message);
        }

        public void OnGlobalMapRecruitmentSlotsRerolled()
        {
            UpdateGlobalMapRecruitmentUIState();
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingShown()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingShown();
            Send(message);

            UpdateGlobalMapCrusadeArmyLeaderLevelingUIState();
        }

        public void OnTacticalCombatEnded()
        {
            Game.ArmyCombat = null;
            Logger.LogInformation("Crusade army combat has ended");
            _valueGenerator.ResetSeedGenerators(SeedLifetime.Combat);
        }

        public bool OnBeforeTacticalCombatTurnStart(int turnNumber)
        {
            var playerId = Game.LocalPlayerId;
            if (AddPlayerCrusadeArmyCombatTurnInitialization(turnNumber, playerId))
            {
                if (turnNumber == 0)
                {
                    Game.ArmyCombat.Turn = new NetworkArmyCombatTurn { Number = turnNumber };
                }

                var message = new NotifyTacticalCombatTurnInitialized
                {
                    PlayerId = playerId,
                    TurnNumber = turnNumber
                };
                Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, TurnNumber={TurnNumber}", nameof(NotifyTacticalCombatTurnInitialized), message.PlayerId, message.TurnNumber);
                Send(message);
            }

            var canContinue = IsCrusadeArmyCombatTurnInitialized();
            return canContinue;
        }

        public void OnCrusadeArmyCombatTurnStarted(NetworkArmyCombatTurn armyCombatTurn)
        {
            Game.ArmyCombat.Turn = armyCombatTurn;
            Logger.LogInformation("New Crusade Army combat turn started. TurnNumber={TurnNumber}, UnitId={UnitId}, IsAI={IsAI}", Game.ArmyCombat.Turn.Number, Game.ArmyCombat.Turn.UnitId, Game.ArmyCombat.Turn.IsAI);
        }

        protected abstract DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest);

        protected abstract void OnLocalPlayerTurnStart();

        protected abstract void Send(object message);

        protected abstract void Send(long playerId, object message);

        protected abstract bool OnToggleOffPause(out bool showReason);

        protected void ShowForcedPauseReason()
        {
            var pause = Game.ForcedPause;
            if (pause == null)
            {
                return;
            }

            var messageKey = pause.IsLifting ? WellKnownKeys.GameNotifications.ForcedPause.IsLifting.Key : pause.Reason switch
            {
                NetworkForcedPauseReason.Manual => WellKnownKeys.GameNotifications.ForcedPause.ManualPause.Key,
                NetworkForcedPauseReason.AreaLoading => WellKnownKeys.GameNotifications.ForcedPause.AreaLoading.Key,
                NetworkForcedPauseReason.RestEncounterLoading => WellKnownKeys.GameNotifications.ForcedPause.RestRandomEncounterLoading.Key,
                NetworkForcedPauseReason.TrapDetected => WellKnownKeys.GameNotifications.ForcedPause.TrapDetected.Key,
                _ => null
            };

            if (!string.IsNullOrEmpty(messageKey))
            {
                PlayerNotification.ShowWarningNotification(messageKey);
            }
        }

        protected List<NetworkAIAction> GetAIActions()
        {
            var settings = SettingsService.GetSettings();
            if (Game.Combat != null && settings.SyncAICombatActions)
            {
                return Game.Combat.AIActions;
            }

            if (Game.ArmyCombat != null && settings.SyncAICombatActions)
            {
                return Game.ArmyCombat.AIActions;
            }

            return null;
        }

        protected bool AddPlayerCrusadeArmyCombatTurnInitialization(int turnNumber, long playerId)
        {
            var isFirstAdd = true;
            Game.ArmyCombat.PlayersNextTurnInitialization.AddOrUpdate(turnNumber, [playerId], (key, existing) =>
            {
                isFirstAdd = existing.Add(playerId);
                return existing;
            });

            return isFirstAdd;
        }

        protected bool IsCrusadeArmyCombatTurnInitialized()
        {
            if (Game.ArmyCombat?.Turn == null || !Game.ArmyCombat.IsInitialized || !Game.ArmyCombat.PlayersNextTurnInitialization.TryGetValue(Game.ArmyCombat.Turn.Number, out var readyPlayers))
            {
                return false;
            }

            var isReady = readyPlayers.Count >= GetSyncedPlayersCount();
            return isReady;
        }

        protected virtual void OnLocalCrusadeArmyCombatTurnInitialized(int turnNumber, long playerId)
        {

        }

        protected virtual void OnLocalRestGameModeEnded()
        {
            if (Game.ForcedPause != null)
            {
                GameInteraction.SetPause(true);
            }
        }

        protected virtual void OnRemoteRestGameModeEnded(long playerId)
        {
            UpdateRestUIState();
        }

        protected virtual void OnRemoteRestGameModeStarted()
        {
            UpdateRestUIState();
        }

        protected virtual void OnRemotePauseGameModeStarted(long playerId)
        {
            lock (ActionLock)
            {
                Game.ForcedPause?.ReadyPlayers.Add(playerId);
            }
        }

        protected void RegisterGlobalMapMode(long playerId, NetworkGlobalMapTravelerMode travelerMode)
        {
            Game.PlayersInGlobalMapMode.AddOrUpdate(playerId, travelerMode, (key, existing) => travelerMode);
        }

        protected void InitiateLeveling(string unitId, NetworkLevelingType levelingType)
        {
            if (Game.Leveling != null)
            {
                Logger.LogWarning("Previous leveling has not been finished. UnitId={UnitId}, Type={Type}", Game.Leveling.UnitId, Game.Leveling.Type);
            }

            Game.Leveling = new NetworkLeveling(unitId, levelingType);
        }

        protected void UpdateGroupManagerUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGroupChanger.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateGroupChangerUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateDialogPopupState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInDialogPopup.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                DialogInteraction.UpdateDialogPopupUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateSkipTimeUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInSkipTime.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateSkipTimeUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapLocationMessageUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapLocationMessage.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateEnterMessageBoxUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCommonPopupUIState(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCommonPopup.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateCommonPopupUI(globalMapCommonPopup, canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCombatResultsUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCombatResults.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateCombatResultsUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyInfoUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCrusadeArmyInfo.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateCrusadeArmyInfoUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyInfoUIStateOnMerge()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCrusadeArmyInfoMerge.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateCrusadeArmyInfoUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyInfoUIStateOnSetLeader()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCrusadeArmySetLeader.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateSharedCrusadeManagementUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapRecruitmentUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapRecruitment.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateRecruitmentUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyLeaderLevelingUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateLeaderLevelingUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyBuyLeaderUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCrusadeArmyBuyLeader.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateBuyLeaderUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyInfoUIStateAfterMerge()
        {
            lock (ActionLock)
            {
                var totalPlayers = GetSyncedPlayersCount();
                var readyPlayers = totalPlayers - Game.PlayersInGlobalMapCrusadeArmyInfoMerge.Count;
                var canUse = HasControlOverUI && Game.PlayersInGlobalMapCrusadeArmyInfoMerge.Count == 0;
                GlobalMapInteraction.UpdateCrusadeArmyInfoUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyInfoUIStateAfterBuyLeader()
        {
            lock (ActionLock)
            {
                var totalPlayers = GetSyncedPlayersCount();
                var readyPlayers = totalPlayers - Game.PlayersInGlobalMapCrusadeArmyBuyLeader.Count;
                var canUse = HasControlOverUI && Game.PlayersInGlobalMapCrusadeArmyBuyLeader.Count == 0;
                GlobalMapInteraction.UpdateSharedCrusadeManagementUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyInfoUIStateAfterSetLeader()
        {
            lock (ActionLock)
            {
                var totalPlayers = GetSyncedPlayersCount();
                var readyPlayers = totalPlayers - Game.PlayersInGlobalMapCrusadeArmySetLeader.Count;
                var canUse = HasControlOverUI && Game.PlayersInGlobalMapCrusadeArmySetLeader.Count == 0;
                GlobalMapInteraction.UpdateSharedCrusadeManagementUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapCrusadeArmyBattleResultsUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapCrusadeArmyBattleResults.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateCrusadeArmyBattleResultsUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapEncounterMessageUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapEncounterMessage.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateEncounterMessageUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateZoneLootUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInZoneLoot.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateZoneLootUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateLevelingRespecUIState(string unitId)
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInRespecWindow.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = WasControlledByCurrentPlayer(unitId) && readyPlayers >= totalPlayers;
                LevelingInteraction.UpdateLevelingRespecUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateCharacterSelectionUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInCharacterSelectionWindow.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateCharacterSelectionUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateRestUIState()
        {
            lock (ActionLock)
            {
                Game.PlayersInGameMode.TryGetValue(GameModeType.Rest, out var restReadyPlayers);
                var readyPlayersCount = (restReadyPlayers ?? []).Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayersCount >= totalPlayers;
                GameInteraction.UpdateRestUI(canUse, readyPlayersCount, totalPlayers);
            }
        }

        protected void UpdateRestResultsUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.Rest.PlayersFinishedRest.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateRestUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected int? GetPlayersCountWithSyncedGlobalMapMode()
        {
            lock (ActionLock)
            {
                if (!Game.PlayersInGlobalMapMode.TryGetValue(Game.LocalPlayerId, out var localPlayerMode))
                {
                    Logger.LogWarning("Global map mode for local player is not set");
                    return null;
                }

                var readyPlayers = Game.PlayersInGlobalMapMode.Count(x => x.Value == localPlayerMode);
                return readyPlayers;
            }
        }

        protected void UpdateGlobalMapUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = GetPlayersCountWithSyncedGlobalMapMode();
                if (!readyPlayers.HasValue)
                {
                    Logger.LogWarning("Unable to update global map ui state due to invalid ready players count");
                    return;
                }

                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers.Value >= totalPlayers;
                GlobalMapInteraction.UpdateUIState(canUse, readyPlayers.Value, totalPlayers);
            }
        }

        protected void ResetPlayersTracker(HashSet<long> tracker)
        {
            lock (ActionLock)
            {
                tracker.Clear();
            }
        }

        protected void AddPlayerToTracker(HashSet<long> tracker, long playerId)
        {
            lock (ActionLock)
            {
                tracker.Add(playerId);
            }
        }

        protected void RemovePlayerFromTracker(HashSet<long> tracker, long playerId)
        {
            lock (ActionLock)
            {
                tracker.Remove(playerId);
            }
        }

        protected void SetLobbyStage(NetworkLobbyStage lobbyStage)
        {
            Game.Stage = lobbyStage;
            Logger.LogInformation("Lobby stage has been changed. Stage={Stage}", lobbyStage);
        }

        protected void EnsureForcePaused(NetworkForcedPauseReason reason, TimeSpan? removalDelay)
        {
            if (Game.ForcedPause == null)
            {
                Game.ForcedPause = new NetworkForcedPause
                {
                    Reason = reason,
                    RemovalDelay = removalDelay,
                };
                Logger.LogInformation("Forced pause has been initialized. Delay={Delay}", removalDelay);
            }
        }

        protected void EnsureForcePaused(NetworkForcedPauseReason reason)
        {
            EnsureForcePaused(reason, SettingsService.GetSettings().ForcedPauseDefaultTerminationDelay);
        }

        protected void WitnessLevelingPhase(long playerId)
        {
            lock (ActionLock)
            {
                Game.Leveling.PlayerReadiness.Add(playerId);

                var isEnabled = CanMakeLevelingDecisions();
                LevelingInteraction.UpdateLevelingPhaseControls(isEnabled);
            }
        }

        protected void WitnessNewGameSequencePhase(long playerId, NetworkNewGameSequencePhaseType newGameSequencePhaseType)
        {
            lock (ActionLock)
            {
                Game.StartUp.ReadyPlayers.Add(playerId);

                var isEnabled = HasControlOverUI && Game.StartUp.ReadyPlayers.Count >= GetSyncedPlayersCount();
                GameInteraction.UpdateNewGameSequencePhaseControls(isEnabled, newGameSequencePhaseType);
            }
        }

        protected bool RegisterGameMode(GameModeType type, long playerId)
        {
            var isNew = true;
            Game.PlayersInGameMode.AddOrUpdate(type,
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

        protected void ShowPlayerConnectedMessage(NetworkPlayer networkPlayer)
        {
            if (Game.Stage != NetworkLobbyStage.Playing)
            {
                return;
            }

            PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Session.PlayerJoined.Key, args: networkPlayer.Name);
        }

        protected void ShowPlayerDisconnectedMessage(NetworkPlayer networkPlayer)
        {
            if (networkPlayer == null || Game.Stage != NetworkLobbyStage.Playing)
            {
                return;
            }

            PlayerNotification.ShowModalMessage(WellKnownKeys.GameNotifications.Session.PlayerLeft.Key, networkPlayer.Name);
        }

        protected NetworkPlayer CleanupPlayer(long playerId)
        {
            var existingPlayer = GetPlayer(playerId);
            if (existingPlayer == null)
            {
                return null;
            }

            CleanupPlayer(existingPlayer);

            return existingPlayer;
        }

        protected void CleanupPlayer(NetworkPlayer player)
        {
            Game.Players.Remove(player);

            var defaultOwner = GetHost();
            foreach (var characterOwnership in Game.Characters)
            {
                if (characterOwnership.Owner != player)
                {
                    continue;
                }

                characterOwnership.Owner = defaultOwner;
            }
        }

        protected void ResetCharacterOwnership()
        {
            var host = GetHost();

            foreach (var character in Game.Characters)
            {
                character.Owner = host;
            }

            Game.CharactersOwnershipHistory.Clear();
        }

        protected void UpdateRespecWindowStateOnPlayerLeave(long playerId)
        {
            RemovePlayerFromTracker(Game.PlayersInRespecWindow, playerId);

            var respecUnitId = LevelingInteraction.GetCurrentRespecWindowUnitId();
            UpdateLevelingRespecUIState(respecUnitId);
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
                        Logger.LogError("Unable to find readiness tracker for provided type. ReadinessType={ReadinessTypeName}, PlayerId={PlayerId}, UnitId={UnitId}", playerReadinessType, playerId, unitId);
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
                        Logger.LogInformation("Player ready status has been confirmed. ReadinessType={ReadinessTypeName}, Key={Key}, PlayersCount={PlayersCount}, KeysCount={KeysCount}", playerReadinessType, unitId, tracker[unitId].Count, tracker.Keys.Count);
                    }

                    return players;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to confirm player readiness. PlayerId={PlayerId}, UnitId={UnitId}", playerId, unitId);
                throw;
            }
        }

        protected enum PlayerTurnReadinessType
        {
            Start,
            UnitSynchronization,
            UnitJoinedMidCombat
        }

        protected void LoadSavedGame()
        {
            ResetGameIdGenerator();
            Game.ForcedPause = null;
            Game.Dialog = null;

            // it's important to use different loading method for players who joined mid-game
            if (Game.Stage == NetworkLobbyStage.Playing)
            {
                Game.Id = GameInteraction.QuickLoadGame(Game.StartUp.SavePath);
            }
            else
            {
                var localPlayer = GetPlayer(Game.LocalPlayerId);
                ReadyChanged(localPlayer, true);

                var status = NetworkLobbySyncStatus.Succeed;
                UpdateLobbySyncStatus(localPlayer, status);
                var message = new NotifyLobbySyncStatusChanged { PlayerId = localPlayer.Id, Status = status.ToString() };
                Send(message);

                Game.Id = GameInteraction.LoadGameFromMainMenu(Game.StartUp.SavePath);
            }

            SetLobbyStage(NetworkLobbyStage.Loading);
        }

        protected NetworkPlayer GetHost()
        {
            lock (ActionLock)
            {
                return Game.Players.First(p => p.IsHost);
            }
        }

        protected void ResetGameIdGenerator()
        {
            Logger.LogInformation("Resetting id counters. GameId={GameId}", Game.Id);
            _valueGenerator.ResetUniqueIdCounters(Game.Id);
        }

        protected void SoftReset()
        {
            Logger.LogInformation("Doing soft reset");
            Game.StartUp = null;
            Game.Combat = null;
            Game.LastCombatTurn = null;
            Game.ArmyCombat = null;
            Game.Leveling = null;
            DiceRollStorage.Reset();
            _valueGenerator.ResetSeedGenerators(SeedLifetime.Area, SeedLifetime.Combat);

            ResetPlayersTracker(Game.PlayersInGroupChanger);
            ResetPlayersTracker(Game.PlayersInSkipTime);
            ResetPlayersTracker(Game.PlayersInGlobalMapLocationMessage);
            ResetPlayersTracker(Game.PlayersInGlobalMapCommonPopup);
            ResetPlayersTracker(Game.PlayersInGlobalMapEncounterMessage);
            ResetPlayersTracker(Game.PlayersInZoneLoot);
            ResetPlayersTracker(Game.PlayersInDialogPopup);
            ResetPlayersTracker(Game.PlayersInCharacterSelectionWindow);
            ResetPlayersTracker(Game.PlayersInRespecWindow);

            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfo);
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge);
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader);
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader);
            ResetPlayersTracker(Game.PlayersInGlobalMapRecruitment);
            ResetPlayersTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling);
        }

        protected string StoreSaveGameContent(byte[] content)
        {
            if (content == null)
            {
                return null;
            }

            var baseUnityPath = GameInteraction.GetSaveGamePath();
            var multiplayerPath = Regex.Replace(baseUnityPath, "((\\\\|\\/)+(Saved Games))$", "/Saved Multiplayer Games/");
            var savePath = Path.Combine(multiplayerPath, "latest joined game.zks");
            if (!FileSystem.WriteFile(savePath, content))
            {
                PlayerNotification.ShowModalMessage(WellKnownKeys.SysMessages.FailedToStoreSave.Key, multiplayerPath);
                return null;
            }

            return savePath;
        }

        protected bool IsCasterControlledByLocalPlayer(string sourceUnitId)
        {
            if (Game.Combat == null)
            {
                return IsControlledByLocalPlayer(sourceUnitId);
            }

            // not sure what falls under this category
            // midfight joins shouldn't have any actions?
            if (Game.Combat.Turn == null)
            {
                Logger.LogWarning("Midfight action. UnitId={UnitId}", sourceUnitId);
                return HasControlOverUI;
            }

            return Game.Combat.Turn.IsLocalPlayer && !CombatInteraction.IsCombatTurnFinished();
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
                Logger.LogError("Specified roll is missing at remote player. RollId={RollId}", rollResponse?.RollId);
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
                var roll = await DiceRollStorage.GetAsync<RollValueBase>(request.RollId, request.PlayerId, request.Timeout);
                var response = new DiceRollValueResponse
                {
                    RollId = request.RollId,
                    UnitId = request.UnitId,
                    RollValue = Mapper.Map<Networking.Messages.Contracts.NetworkRollValue>(roll),
                    PlayerId = request.PlayerId
                };

                Logger.LogInformation("Sending roll value response. RollId={RollId}, RollType={RollType}, Result={Result}, DamageValuesCount={DamageValuesCount}, RollHistoryCount={RollHistoryCount}, PlayerId={PlayerId}",
                    response.RollId, roll?.GetType().Name, response.RollValue?.Result, response.RollValue?.DamageValues.Count, response.RollValue?.RollHistory.Count, response.PlayerId);

                Send(playerId, response);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to send local roll");
                throw;
            }
        }

        protected bool IsRolledByHost()
        {
            var isNotInCombat = Game.Combat == null;
            var isCombatNotInitialized = !(Game.Combat?.IsInitialized ?? false);
            var isTurnNotInitialized = Game.Combat?.Turn == null;
            var isAI = Game.Combat?.Turn?.IsAI ?? false;
            var result = isNotInCombat // everything happens on host outside of combat
                || isCombatNotInitialized // combat initialization phase (initiative rolls)
                || isTurnNotInitialized // could happen when some new NPC joins midfight in midturns, e.g. Anevia in prologue
                || isAI; // clients are getting their AI rolls from host

            return result;
        }

        protected bool IsRolledByLocalPlayer()
        {
            var isNotAI = !(Game.Combat?.Turn?.IsAI ?? false);
            var isLocalPlayer = Game.Combat?.Turn?.IsLocalPlayer ?? false;
            var result = isNotAI  // clients are getting their AI rolls from host
                && isLocalPlayer; // other MP players are getting rolls from turn owner

            return result;
        }

        protected List<NetworkPlayer> GetMissingPlayers(string key, ConcurrentDictionary<string, HashSet<long>> playersReadinessTracker)
        {
            List<NetworkPlayer> notReadyPlayers = GetPlayers();
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

        protected NetworkCharacter GetPartyCharacter(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            var realCharacterId = GameInteraction.GetPetOwnerId(unitId) ?? unitId;

            return Game.Characters.FirstOrDefault(c => string.Equals(c.UnitId, realCharacterId, StringComparison.OrdinalIgnoreCase));
        }

        protected NetworkCharacter FindCharacter(NetworkCharacter character)
        {
            var actualCharacter = Game.Characters.FirstOrDefault(x => !string.IsNullOrEmpty(x.UnitId) && string.Equals(x.UnitId, character.UnitId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name, character.Name, StringComparison.OrdinalIgnoreCase));

            return actualCharacter;
        }

        protected NetworkPlayer GetPlayer(long playerId)
        {
            lock (ActionLock)
            {
                return Game?.Players.FirstOrDefault(p => p.Id == playerId);
            }
        }

        protected void EndLocalTurn()
        {
            Game.Combat.Turn.IsInProgress = false;
            CombatInteraction.EndTurnBasedCombatTurn();
        }

        protected bool IsGameModeAllowedToRun(GameModeType type)
        {
            return type != GameModeType.EscMode && type != GameModeType.FullScreenUi;
        }

        protected virtual void OnAfterNetworkMessageHandled(long playerId, object message)
        {
        }

        protected void UpdateInGameCharacterOwnershipChange(NetworkCharacter networkCharacter)
        {
            if (Game.Stage != NetworkLobbyStage.Playing)
            {
                return;
            }

            GameInteraction.ReselectSelectedCharacters();
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Session.CharacterOwnerChanged.Key, networkCharacter.Owner.Name, networkCharacter.Name);
        }

        protected void UpdateLobbySyncStatus(long playerId, NetworkLobbySyncStatus syncStatus)
        {
            var player = GetPlayer(playerId);
            if (player == null)
            {
                Logger.LogWarning("Unable to update lobby sync status for missing player. PlayerId={PlayerId}", playerId);
                return;
            }

            UpdateLobbySyncStatus(player, syncStatus);
        }

        protected void UpdateLobbySyncStatus(NetworkPlayer player, NetworkLobbySyncStatus syncStatus)
        {
            player.LobbySyncStatus = syncStatus;
            InvokeOnPlayersChanged();
        }

        protected void UpdateReadyStatus(long playerId, bool isReady)
        {
            lock (ActionLock)
            {
                var existingPlayer = GetPlayer(playerId);
                if (existingPlayer == null)
                {
                    Logger.LogWarning("Unable to update ready status for missing player. PlayerId={PlayerId}", playerId);
                    return;
                }

                existingPlayer.IsReady = isReady;

                InvokeOnPlayersChanged();
            }
        }

        protected void InvokeOnPlayersChanged()
        {
            var players = GetPlayers();
            OnPlayersChanged?.Invoke(Game.Stage, players);
        }

        protected void UpdateCharacterOwnershipHistory(NetworkCharacter ownership)
        {
            if (Game.CharactersOwnershipHistory.TryGetValue(ownership.UnitId, out var playerId) && playerId == ownership.Owner.Id)
            {
                return;
            }

            Game.CharactersOwnershipHistory[ownership.UnitId] = ownership.Owner.Id;
            Logger.LogInformation("Character ownership hisory has been updated. CharacterUnitId={CharacterUnitId}, PlayerId={PlayerId}", ownership.UnitId, ownership.Owner.Id);
        }

        protected int CreateRandomSeed()
        {
            var seed = new System.Random().Next(int.MinValue, int.MaxValue);
            return seed;
        }

        protected int GetSyncedPlayersCount()
        {
            lock (ActionLock)
            {
                return Game.Players.Count(x => x.LobbySyncStatus == NetworkLobbySyncStatus.Succeed);
            }
        }

        protected NetworkContentState GetInstalledContent()
        {
            var content = GameInteraction.GetInstalledContent();
            return content;
        }

        protected void ResetCombatTurn()
        {
            lock (ActionLock)
            {
                SaveLastCombatTurn();
                if (Game.Combat != null)
                {
                    Game.Combat.Turn = null;
                }
            }
        }

        protected void RefreshUIOnPlayerDisconnect(long playerId)
        {
            UpdateRestUIState();

            if (Game.Rest != null)
            {
                RemovePlayerFromTracker(Game.Rest.PlayersFinishedRest, playerId);
                UpdateRestResultsUIState();
            }

            RemovePlayerFromTracker(Game.PlayersInSkipTime, playerId);
            UpdateSkipTimeUIState();

            RemovePlayerFromTracker(Game.PlayersInGroupChanger, playerId);
            UpdateGroupManagerUIState();

            RemovePlayerFromTracker(Game.PlayersInZoneLoot, playerId);
            UpdateZoneLootUIState();

            UpdateRespecWindowStateOnPlayerLeave(playerId);

            UpdateCharacterSelectionUIState();

            Game.PlayersInGlobalMapMode.TryRemove(playerId, out _);
            UpdateGlobalMapUIState();

            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults, playerId);
            UpdateGlobalMapCrusadeArmyBattleResultsUIState();

            RemovePlayerFromTracker(Game.PlayersInGlobalMapCommonPopup, playerId);

            RemovePlayerFromTracker(Game.PlayersInGlobalMapCombatResults, playerId);
            UpdateGlobalMapCombatResultsUIState();
        }

        protected async Task WaitWhileTrue(Func<bool> condition, string warningMessage)
        {
            var delay = TimeSpan.FromMilliseconds(10);
            if (condition())
            {
                Logger.LogWarning(warningMessage);
                while (condition())
                {
                    await Task.Delay(delay);
                }
            }
        }

        protected void UpdateSaveInfo(string gameId, byte[] content)
        {
            var savePath = StoreSaveGameContent(content);
            UpdateSaveInfo(gameId, savePath);
        }

        protected void UpdateSaveInfo(string gameId, string savePath)
        {
            Game.StartUp = new NetworkGameStartUp(savePath);
            Game.Id = gameId;

            Logger.LogInformation("Save info has been updated. GameId={GameId}, SavePath={SavePath}, IsNewGameSequence={IsNewGameSequence}", Game.Id, Game.StartUp.SavePath, Game.StartUp.IsNewGameSequence);
        }

        protected void ResetGlobalMapCounters()
        {
            Game.PlayersInGlobalMapMode.Clear();
            Logger.LogInformation("Global map counters have been reset");
        }

        protected void StartNewGameSequence()
        {
            Logger.LogInformation("Starting new game sequence");
            SetLobbyStage(NetworkLobbyStage.NewGameSequence);
            OnNewGameSequenceStarted?.Invoke(true);

            var mainCharacterId = Game.Characters.First().UnitId;
            GameInteraction.StartNewGameSequence(
                mainCharacterId,
                onBack: () =>
                {
                    Logger.LogInformation("New game sequence has been cancelled");
                    SetLobbyStage(NetworkLobbyStage.Lobby);
                    foreach (var player in Game.Players)
                    {
                        player.IsReady = false;
                        UpdateLobbySyncStatus(player, NetworkLobbySyncStatus.None);
                    }

                    if (CanMakeNewGameSequenceDecisions())
                    {
                        var message = new NotifyNewGameSequenceTerminated();
                        Logger.LogInformation("Sending {MessageType}", nameof(NotifyNewGameSequenceTerminated));
                        Send(message);
                    }

                    ResetPlayersTracker(Game.StartUp.ReadyPlayers);
                    OnNewGameSequenceStarted?.Invoke(false);
                },
                onStart: () =>
                {
                    ResetPlayersTracker(Game.StartUp.ReadyPlayers);

                    OnForceLevelingUI(mainCharacterId, NetworkLevelingType.NewGameSequence);
                    Logger.LogInformation("New game sequence has been finished");

                    if (CanMakeNewGameSequenceDecisions())
                    {
                        var message = new NotifyNewGameSequenceLevelingStarted();
                        Logger.LogInformation("Sending {MessageType}", nameof(NotifyNewGameSequenceLevelingStarted));
                        Send(message);
                    }
                },
                onCharacterCreated: (character) =>
                {
                    var fakeCharacter = Game.Characters.First();
                    fakeCharacter.Name = character.Name;
                    fakeCharacter.Portrait = character.Portrait;
                });
        }

        protected virtual void SetupNetworkMessageHandlers()
        {
            _networkReceiver
                // lobby
                .On<NotifyGameForceLoaded>(OnNotifyGameForceLoaded)
                .On<NotifyPlayerReadyStatusChanged>(OnNotifyPlayerReadyStatusChanged)

                // combat
                .On<NotifyCombatStarted>(OnNotifyCombatStarted)

                // leveling
                .On<NotifyLevelingClassArchetypeSelected>(OnNotifyLevelingClassArchetypeSelected)
                .On<NotifyLevelingMythicClassSelected>(OnNotifyLevelingMythicClassSelected)
                .On<NotifyLevelingClassSelected>(OnNotifyLevelingClassSelected)
                .On<NotifyLevelingPhaseWitnessed>(OnNotifyLevelingPhaseWitnessed)
                .On<NotifyLevelingPhaseChanged>(OnNotifyLevelingPhaseChanged)
                .On<NotifyLevelingSkillPointIncreased>(OnNotifyLevelingSkillPointIncreased)
                .On<NotifyLevelingSkillPointDecreased>(OnNotifyLevelingSkillPointDecreased)
                .On<NotifyLevelingAbilityScoreIncreased>(OnNotifyLevelingAbilityScoreIncreased)
                .On<NotifyLevelingAbilityScoreDecreased>(OnNotifyLevelingAbilityScoreDecreased)
                .On<NotifyLevelingPortraitSelected>(OnNotifyLevelingPortraitSelected)
                .On<NotifyLevelingVoiceSelected>(OnNotifyLevelingVoiceSelected)
                .On<NotifyLevelingRaceSelected>(OnNotifyLevelingRaceSelected)
                .On<NotifyLevelingGenderSelected>(OnNotifyLevelingGenderSelected)
                .On<NotifyLevelingAlignmentSelected>(OnNotifyLevelingAlignmentSelected)
                .On<NotifyLevelingNameChanged>(OnNotifyLevelingNameChanged)
                .On<NotifyLevelingBodyTypeAppearanceChanged>(OnNotifyLevelingBodyTypeAppearanceChanged)
                .On<NotifyLevelingBodyColorAppearanceChanged>(OnNotifyLevelingBodyColorAppearanceChanged)
                .On<NotifyLevelingEyesColorAppearanceChanged>(OnNotifyLevelingEyesColorAppearanceChanged)
                .On<NotifyLevelingFaceAppearanceChanged>(OnNotifyLevelingFaceAppearanceChanged)
                .On<NotifyLevelingHairColorAppearanceChanged>(OnNotifyLevelingHairColorAppearanceChanged)
                .On<NotifyLevelingHairStyleAppearanceChanged>(OnNotifyLevelingHairStyleAppearanceChanged)
                .On<NotifyLevelingHornsAppearanceChanged>(OnNotifyLevelingHornsAppearanceChanged)
                .On<NotifyLevelingHornsColorAppearanceChanged>(OnNotifyLevelingHornsColorAppearanceChanged)
                .On<NotifyLevelingPrimaryOutfitColorAppearanceChanged>(OnNotifyLevelingPrimaryOutfitColorAppearanceChanged)
                .On<NotifyLevelingSecondaryOutfitColorAppearanceChanged>(OnNotifyLevelingSecondaryOutfitColorAppearanceChanged)
                .On<NotifyLevelingScarAppearanceChanged>(OnNotifyLevelingScarAppearanceChanged)
                .On<NotifyLevelingTattooAppearanceChanged>(OnNotifyLevelingTattooAppearanceChanged)
                .On<NotifyLevelingTattooColorAppearanceChanged>(OnNotifyLevelingTattooColorAppearanceChanged)
                .On<NotifyLevelingWarpaintAppearanceChanged>(OnNotifyLevelingWarpaintAppearanceChanged)
                .On<NotifyLevelingWarpaintColorAppearanceChanged>(OnNotifyLevelingWarpaintColorAppearanceChanged)
                .On<NotifyLevelingRacialAbilityScoreBonusChanged>(OnNotifyLevelingRacialAbilityScoreBonusChanged)
                .On<NotifyLevelingBirthMonthChanged>(OnNotifyLevelingBirthMonthChanged)
                .On<NotifyLevelingBirthDayChanged>(OnNotifyLevelingBirthDayChanged)
                .On<NotifyLevelingFeatureSelected>(OnNotifyLevelingFeatureSelected)
                .On<NotifyLevelingSpellChosen>(OnNotifyLevelingSpellChosen)
                .On<NotifyLevelingSpellRemoved>(OnNotifyLevelingSpellRemoved)
                .On<NotifyLevelingCompleted>(OnNotifyLevelingCompleted)
                .On<NotifyLevelingTerminated>(OnNotifyLevelingTerminated)
                // respec selector & window
                .On<NotifyLevelingRespecCompleted>(OnNotifyLevelingRespecCompleted)
                .On<NotifyLevelingRespecWindowShown>(OnNotifyLevelingRespecWindowShown)
                .On<NotifyLevelingRespecLevelUp>(OnNotifyLevelingRespecLevelUp)
                .On<NotifyLevelingRespecMythicLevelUp>(OnNotifyLevelingRespecMythicLevelUp)

                // new game sequence
                .On<NotifyNewGameSequenceWitnessed>(OnNotifyNewGameSequenceWitnessed)

                // character selection window
                .On<NotifyCharacterSelectionWindowShown>(OnNotifyCharacterSelectionWindowShown)

                // spellbook management
                .On<NotifySpellMemorized>(OnNotifySpellMemorized)
                .On<NotifySpellForgotten>(OnNotifySpellForgotten)

                // vendor interaction
                .On<NotifyVendorItemTransferred>(OnNotifyVendorItemTransferred)

                // rest
                .On<NotifyRestBanterInterrupted>(OnNotifyRestBanterInterrupted)
                .On<NotifyRestEnded>(OnNotifyRestEnded)
                .On<NotifyCapitalModeRestInitiated>(OnNotifyCapitalModeRestInitiated)

                // combat
                .On<NotifyUnitJoinedMidCombat>(OnNotifyUnitJoinedMidCombat)
                .On<NotifyPlayerCombatTurnEnded>(OnNotifyPlayerCombatTurnEnded)
                .On<NotifyUnitAttacked>(OnNotifyUnitAttacked)
                .On<NotifyCombatTurnDelayed>(OnNotifyCombatTurnDelayed)
                .On<NotifyCombatUnitKilled>(OnNotifyCombatUnitKilled)

                // global map & crusade combat
                .On<NotifyGlobalMapLocationMessageShown>(OnNotifyGlobalMapLocationMessageShown)
                .On<NotifyGlobalMapCommonPopupShown>(OnNotifyGlobalMapCommonPopupShown)
                .On<NotifyGlobalMapEncounterMessageShown>(OnNotifyGlobalMapEncounterMessageShown)
                .On<NotifyGlobalMapCombatResultsShown>(OnNotifyGlobalMapCombatResultsShown)
                .On<NotifyTacticalCombatTurnInitialized>(OnNotifyTacticalCombatTurnInitialized)
                .On<NotifyCrusadeArmyBattleResultsShown>(OnNotifyCrusadeArmyBattleResultsShown)
                .On<NotifyGlobalMapCrusadeArmyMergeCartShown>(OnNotifyGlobalMapCrusadeArmyInfoMergeShown)
                .On<NotifyGlobalMapCrusadeArmySetLeaderShown>(OnNotifyGlobalMapCrusadeArmySetLeaderShown)
                .On<NotifyGlobalMapCrusadeArmyBuyLeaderShown>(OnNotifyGlobalMapCrusadeArmyBuyLeaderShown)
                .On<NotifyGlobalMapCrusadeArmyLeaderLevelingShown>(OnNotifyGlobalMapCrusadeArmyLeaderLevelingShown)

                // mapobjects
                .On<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)
                .On<NotifyTrapDisarmRolled>(OnNotifyTrapDisarmRolled)

                // items&inventory
                .On<NotifyLootableEntitySkinned>(OnNotifyContainerSkinned)
                .On<NotifyDropItem>(OnNotifyDropItem)
                .On<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .On<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)
                .On<NotifyZoneLootShown>(OnNotifyZoneLootShown)
                .On<NotifyZoneLootClosed>(OnNotifyZoneLootClosed)
                .On<NotifyInventoryItemTransferred>(OnNotifyInventoryItemTransferred)
                .On<NotifyInventoryItemUsed>(OnNotifyInventoryItemUsed)

                // lockpick
                .On<NotifyMapObjectLockpicked>(OnNotifyMapObjectLockpicked)

                // abilities
                .On<NotifyUnitAutoUseAbilityChanged>(OnNotifyUnitAutoUseAbilityChanged)
                .On<NotifyAbilityUsed>(OnNotifyAbilityUsed)
                .On<NotifyToggleActivatableAbility>(OnNotifyToggleActivatableAbility)

                // clicks
                .On<NotifyUnitClicked>(OnNotifyUnitClicked)
                .On<NotifyGroundClicked>(OnNotifyGroundClicked)
                .On<NotifyMapObjectClicked>(OnNotifyMapObjectClicked)

                // movement
                .On<NotifyCharacterMove>(OnNotifyCharacterMove)

                // pausing
                .On<NotifyGamePauseStarted>(OnNotifyGamePauseStarted)

                // action bar
                .On<NotifyActionBarSlotCleared>(OnNotifyActionBarSlotCleared)
                .On<NotifyActionBarSlotMoved>(OnNotifyActionBarSlotMoved)

                // stealth
                .On<NotifyUnitStealthChanged>(OnNotifyUnitStealthChanged)

                // skip time
                .On<NotifySkipTimeOpened>(OnNotifySkipTimeOpened)

                // group management
                .On<NotifyGroupChangerOpened>(OnNotifyGroupChangerOpened)

                // dialogs
                .On<NotifyDialogPopupShown>(OnNotifyDialogPopupShown)

                // game modes
                .On<NotifyGameModeTypeStarted>(OnNotifyGameModeTypeStarted)
                .On<NotifyGameModeTypeEnded>(OnNotifyGameModeTypeEnded)

                // ping
                .On<NotifyPingedByPlayer>(OnNotifyPingedByPlayer)

                // cutscenes
                .On<NotifyCutsceneSkipped>(OnNotifyCutsceneSkipped)
                ;
        }

        private void OnNotifyUnitAutoUseAbilityChanged(long receivedFrom, NotifyUnitAutoUseAbilityChanged message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, AbilityId={AbilityId}, AbilityName={AbilityName}", nameof(NotifyUnitAutoUseAbilityChanged), receivedFrom, message.UnitId, message.Ability?.Id, message.Ability?.Name);

            var ability = Mapper.Map<NetworkAbility>(message.Ability);
            GameInteraction.SetUnitAutoUseAbility(message.UnitId, ability);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGamePauseStarted(long receivedFrom, NotifyGamePauseStarted message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Reason={Reason}, RemovalDelay={RemovalDelay}", nameof(NotifyGamePauseStarted), receivedFrom, message.PlayerId, message.Pause.Reason, message.Pause.RemovalDelay);

            var pause = Mapper.Map<NetworkForcedPause>(message.Pause);
            EnsureForcePaused(pause.Reason, pause.RemovalDelay);
            GameInteraction.SetPause(true);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private async void OnNotifyTrapDisarmRolled(long receivedFrom, NotifyTrapDisarmRolled message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TrapId={TrapId}, Position={Position}, Roll={Roll}, IsSuccess={IsSuccess}, UnitId={UnitId}",
                nameof(NotifyTrapDisarmRolled), receivedFrom, message.TrapDisarm.MapObject.Id, message.TrapDisarm.MapObject.Position, message.TrapDisarm.Roll, message.TrapDisarm.IsSuccess, message.TrapDisarm.UnitId);

            OnAfterNetworkMessageHandled(receivedFrom, message);

            await WaitWhileTrue(() => GameInteraction.IsUnitBusy(message.TrapDisarm.UnitId), "Waiting for unit to finish actions before applying trap disarm roll");

            var trapDisarm = Mapper.Map<NetworkTrapDisarm>(message.TrapDisarm);
            GameInteraction.ApplyTrapDisarm(trapDisarm);
        }

        private async void OnNotifyCombatUnitKilled(long receivedFrom, NotifyCombatUnitKilled combatUnitKilled)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyCombatUnitKilled), receivedFrom, combatUnitKilled.PlayerId, combatUnitKilled.UnitId);

            OnAfterNetworkMessageHandled(receivedFrom, combatUnitKilled);

            if (Game.Combat == null)
            {
                return;
            }

            await WaitWhileTrue(CombatInteraction.IsRiderActive, "Waiting for active commands to finish before checking if unit should be killed");

            var player = GetPlayer(combatUnitKilled.PlayerId);
            if (player == null)
            {
                Logger.LogWarning("Received unit killed event from a missing player. PlayerId={PlayerId}", combatUnitKilled.PlayerId);
                return;
            }

            CombatInteraction.KillUnit(player, combatUnitKilled.UnitId);
        }

        private async void OnNotifyCombatStarted(long receivedFrom, NotifyCombatStarted combatStarted)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, UnitsCount={UnitsCount}", nameof(NotifyCombatStarted), receivedFrom, combatStarted.PlayerId, combatStarted.State.Units.Count);

            OnAfterNetworkMessageHandled(receivedFrom, combatStarted);

            if (Game.Combat != null)
            {
                return;
            }

            var player = GetPlayer(combatStarted.PlayerId);
            if (player == null)
            {
                Logger.LogWarning("Combat has been started by missing player. PlayerId={PlayerId}", combatStarted.PlayerId);
                return;
            }

            var settings = SettingsService.GetSettings();
            var delay = TimeSpan.FromSeconds(settings.EnforcedCombatStartDelay);
            await Task.Delay(delay);

            var combatState = Mapper.Map<NetworkCombatState>(combatStarted.State);
            var hasBeenForcedToStart = await CombatInteraction.StartCombatAsync(combatState);
            if (hasBeenForcedToStart)
            {
                GameInteraction.SetPause(false);
                PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Combat.ForcedToStart.Key, args: player.Name);
            }
        }

        private void OnNotifyCapitalModeRestInitiated(long receivedFrom, NotifyCapitalModeRestInitiated message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyCapitalModeRestInitiated), receivedFrom);

            GameInteraction.InitiateRest();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingShown(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingShown message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyLeaderLevelingShown), receivedFrom, message.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling, message.PlayerId);

            UpdateGlobalMapCrusadeArmyLeaderLevelingUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderShown(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderShown globalMapCrusadeArmyBuyLeaderShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyMergeCartShown), receivedFrom, globalMapCrusadeArmyBuyLeaderShown.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, globalMapCrusadeArmyBuyLeaderShown.PlayerId);

            UpdateGlobalMapCrusadeArmyBuyLeaderUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyBuyLeaderShown);
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderShown(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderShown globalMapCrusadeArmyInfoSetLeaderShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyMergeCartShown), receivedFrom, globalMapCrusadeArmyInfoSetLeaderShown.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, globalMapCrusadeArmyInfoSetLeaderShown.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateOnSetLeader();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyInfoSetLeaderShown);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoMergeShown(long receivedFrom, NotifyGlobalMapCrusadeArmyMergeCartShown globalMapCrusadeArmyInfoMergeShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCrusadeArmyMergeCartShown), receivedFrom, globalMapCrusadeArmyInfoMergeShown.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, globalMapCrusadeArmyInfoMergeShown.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateOnMerge();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyInfoMergeShown);
        }

        private void OnNotifyGlobalMapCombatResultsShown(long receivedFrom, NotifyGlobalMapCombatResultsShown globalMapCombatResultsShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapCombatResultsShown), receivedFrom, globalMapCombatResultsShown.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCombatResults, globalMapCombatResultsShown.PlayerId);

            UpdateGlobalMapCombatResultsUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCombatResultsShown);
        }

        private void OnNotifyCrusadeArmyBattleResultsShown(long receivedFrom, NotifyCrusadeArmyBattleResultsShown crusadeArmyBattleResultsShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyCrusadeArmyBattleResultsShown), receivedFrom, crusadeArmyBattleResultsShown.PlayerId);
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults, crusadeArmyBattleResultsShown.PlayerId);

            UpdateGlobalMapCrusadeArmyBattleResultsUIState();

            OnAfterNetworkMessageHandled(receivedFrom, crusadeArmyBattleResultsShown);
        }

        private async void OnNotifyTacticalCombatTurnInitialized(long receivedFrom, NotifyTacticalCombatTurnInitialized tacticalCombatTurnInitialized)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, TurnNumber={TurnNumber}", nameof(NotifyTacticalCombatTurnInitialized), receivedFrom, tacticalCombatTurnInitialized.PlayerId, tacticalCombatTurnInitialized.TurnNumber);

            await WaitWhileTrue(() => Game.ArmyCombat == null, "Crusade army combat has not been started yet");

            AddPlayerCrusadeArmyCombatTurnInitialization(tacticalCombatTurnInitialized.TurnNumber, tacticalCombatTurnInitialized.PlayerId);

            OnAfterNetworkMessageHandled(receivedFrom, tacticalCombatTurnInitialized);
        }

        private void OnNotifyPlayerReadyStatusChanged(long receivedFrom, NotifyPlayerReadyStatusChanged readyStatusChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, IsReady={IsReady}", nameof(NotifyPlayerReadyStatusChanged), receivedFrom, readyStatusChanged.PlayerId, readyStatusChanged.IsReady);
            UpdateReadyStatus(readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

            OnAfterNetworkMessageHandled(receivedFrom, readyStatusChanged);
        }

        private void OnNotifyCutsceneSkipped(long playerId, NotifyCutsceneSkipped cutsceneSkipped)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyCutsceneSkipped), cutsceneSkipped.PlayerId);

            var player = GetPlayer(cutsceneSkipped.PlayerId);
            if (player != null)
            {
                GameInteraction.SkipCutscene(player.Name);
            }

            OnAfterNetworkMessageHandled(playerId, cutsceneSkipped);
        }

        private void OnNotifyPingedByPlayer(long receivedFrom, NotifyPingedByPlayer pingedAt)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Type={Type}, WorldPosition={WorldPosition}, TargetUnitId={TargetUnitId}, MapObjectId={MapObjectId}, MapObjectPosition={MapObjectPosition}",
                nameof(NotifyPingedByPlayer), receivedFrom, pingedAt.PlayerId, pingedAt.Ping.Type, pingedAt.Ping.WorldPosition, pingedAt.Ping.UnitId, pingedAt.Ping.MapObject?.Id, pingedAt.Ping.MapObject?.Position);

            var ping = Mapper.Map<NetworkPing>(pingedAt.Ping);

            var player = GetPlayer(pingedAt.PlayerId);
            if (player == null)
            {
                return;
            }

            PingInteraction.Create(player, ping);

            OnAfterNetworkMessageHandled(receivedFrom, pingedAt);
        }

        private void OnNotifyRestEnded(long receivedFrom, NotifyRestEnded restEnded)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyRestEnded), receivedFrom, restEnded.PlayerId);
            AddPlayerToTracker(Game.Rest.PlayersFinishedRest, restEnded.PlayerId);

            UpdateRestResultsUIState();

            OnAfterNetworkMessageHandled(receivedFrom, restEnded);
        }

        private void OnNotifyGameModeTypeEnded(long receivedFrom, NotifyGameModeTypeEnded gameModeTypeEnded)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Type={Type}", nameof(NotifyGameModeTypeEnded), receivedFrom, gameModeTypeEnded.PlayerId, gameModeTypeEnded.Type);
            var gameMode = GameModeType.All.FirstOrDefault(g => string.Equals(g.Name, gameModeTypeEnded.Type, StringComparison.OrdinalIgnoreCase));
            UnregisterGameMode(gameMode, gameModeTypeEnded.PlayerId);
            if (gameMode == GameModeType.Rest)

            {
                OnRemoteRestGameModeEnded(gameModeTypeEnded.PlayerId);
            }

            OnAfterNetworkMessageHandled(receivedFrom, gameModeTypeEnded);
        }

        private void OnNotifyGameModeTypeStarted(long receivedFrom, NotifyGameModeTypeStarted gameModeTypeStarted)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Type={Type}", nameof(NotifyGameModeTypeStarted), receivedFrom, gameModeTypeStarted.PlayerId, gameModeTypeStarted.Type);
            var gameMode = GameModeType.All.FirstOrDefault(g => string.Equals(g.Name, gameModeTypeStarted.Type, StringComparison.OrdinalIgnoreCase));
            RegisterGameMode(gameMode, gameModeTypeStarted.PlayerId);

            if (gameMode == GameModeType.Rest)
            {
                OnRemoteRestGameModeStarted();
            }
            else if (gameMode == GameModeType.Pause)
            {
                OnRemotePauseGameModeStarted(gameModeTypeStarted.PlayerId);
            }

            OnAfterNetworkMessageHandled(receivedFrom, gameModeTypeStarted);
        }

        private void OnNotifyGameForceLoaded(long receivedFrom, NotifyGameForceLoaded gameForceLoaded)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, GameId={GameId}, ContentSize={ContentSize}, LoadedSaveSeed={LoadedSaveSeed}", nameof(NotifyGameForceLoaded), receivedFrom, gameForceLoaded.GameId, gameForceLoaded.Content.Length, gameForceLoaded.Seed);

            UpdateSaveInfo(gameForceLoaded.GameId, gameForceLoaded.Content);

            Game.LoadedSaveSeed = gameForceLoaded.Seed;

            LoadSavedGame();

            OnAfterNetworkMessageHandled(receivedFrom, gameForceLoaded);
        }

        private void OnNotifyNewGameSequenceWitnessed(long receivedFrom, NotifyNewGameSequenceWitnessed newGameSequenceWitnessed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyNewGameSequenceWitnessed), newGameSequenceWitnessed.PlayerId, newGameSequenceWitnessed.Phase.Type);

            var phase = Mapper.Map<NetworkNewGameSequencePhase>(newGameSequenceWitnessed.Phase);
            WitnessNewGameSequencePhase(newGameSequenceWitnessed.PlayerId, phase.Type);

            OnAfterNetworkMessageHandled(receivedFrom, newGameSequenceWitnessed);
        }

        private void OnNotifyCharacterSelectionWindowShown(long receivedFrom, NotifyCharacterSelectionWindowShown characterSelectionWindowShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyCharacterSelectionWindowShown), receivedFrom, characterSelectionWindowShown.PlayerId);
            AddPlayerToTracker(Game.PlayersInCharacterSelectionWindow, characterSelectionWindowShown.PlayerId);

            UpdateCharacterSelectionUIState();

            OnAfterNetworkMessageHandled(receivedFrom, characterSelectionWindowShown);
        }

        private void OnNotifyLevelingRespecMythicLevelUp(long receivedFrom, NotifyLevelingRespecMythicLevelUp levelingRespecMythicLevelUp)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyLevelingRespecMythicLevelUp), receivedFrom, levelingRespecMythicLevelUp.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInRespecWindow, levelingRespecMythicLevelUp.PlayerId);
            LevelingInteraction.InitiateLevelingRespecMythicLevelUp();

            OnAfterNetworkMessageHandled(receivedFrom, levelingRespecMythicLevelUp);
        }

        private void OnNotifyLevelingRespecLevelUp(long receivedFrom, NotifyLevelingRespecLevelUp levelingRespecLevelUp)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyLevelingRespecLevelUp), receivedFrom, levelingRespecLevelUp.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInRespecWindow, levelingRespecLevelUp.PlayerId);
            LevelingInteraction.InitiateLevelingRespecLevelUp();

            OnAfterNetworkMessageHandled(receivedFrom, levelingRespecLevelUp);
        }

        private void OnNotifyLevelingRespecWindowShown(long receivedFrom, NotifyLevelingRespecWindowShown levelingRespecWindowShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyLevelingRespecWindowShown), receivedFrom, levelingRespecWindowShown.PlayerId, levelingRespecWindowShown.UnitId);

            AddPlayerToTracker(Game.PlayersInRespecWindow, levelingRespecWindowShown.PlayerId);

            UpdateLevelingRespecUIState(levelingRespecWindowShown.UnitId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingRespecWindowShown);
        }

        private void OnNotifyLevelingRespecCompleted(long receivedFrom, NotifyLevelingRespecCompleted levelingRespecCompleted)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(NotifyLevelingRespecCompleted), receivedFrom);

            ResetPlayersTracker(Game.PlayersInRespecWindow);

            LevelingInteraction.CompleteLevelingRespec();

            OnAfterNetworkMessageHandled(receivedFrom, levelingRespecCompleted);
        }

        private void OnNotifyLevelingWarpaintColorAppearanceChanged(long receivedFrom, NotifyLevelingWarpaintColorAppearanceChanged levelingWarpaintColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}, PageNumber={PageNumber}", nameof(NotifyLevelingWarpaintColorAppearanceChanged), receivedFrom, levelingWarpaintColorAppearanceChanged.Warpaint.TextureName, levelingWarpaintColorAppearanceChanged.Warpaint.PageNumber);

            var levelingWarpaint = Mapper.Map<NetworkLevelingWarpaint>(levelingWarpaintColorAppearanceChanged.Warpaint);
            LevelingInteraction.SelectLevelingWarpaintColorAppearance(levelingWarpaint);

            OnAfterNetworkMessageHandled(receivedFrom, levelingWarpaintColorAppearanceChanged);
        }

        private void OnNotifyLevelingWarpaintAppearanceChanged(long receivedFrom, NotifyLevelingWarpaintAppearanceChanged levelingWarpaintAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}, PageNumber={PageNumber}", nameof(NotifyLevelingWarpaintAppearanceChanged), receivedFrom, levelingWarpaintAppearanceChanged.Warpaint.Index, levelingWarpaintAppearanceChanged.Warpaint.PageNumber);

            var levelingWarpaint = Mapper.Map<NetworkLevelingWarpaint>(levelingWarpaintAppearanceChanged.Warpaint);
            LevelingInteraction.SelectLevelingWarpaintAppearance(levelingWarpaint);

            OnAfterNetworkMessageHandled(receivedFrom, levelingWarpaintAppearanceChanged);
        }

        private void OnNotifyLevelingTattooColorAppearanceChanged(long receivedFrom, NotifyLevelingTattooColorAppearanceChanged levelingTattooColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={Index}, PageNumber={PageNumber}", nameof(NotifyLevelingTattooColorAppearanceChanged), receivedFrom, levelingTattooColorAppearanceChanged.Tattoo.TextureName, levelingTattooColorAppearanceChanged.Tattoo.PageNumber);

            var levelingTattoo = Mapper.Map<NetworkLevelingTattoo>(levelingTattooColorAppearanceChanged.Tattoo);
            LevelingInteraction.SelectLevelingTattooColorAppearance(levelingTattoo);

            OnAfterNetworkMessageHandled(receivedFrom, levelingTattooColorAppearanceChanged);
        }

        private void OnNotifyLevelingTattooAppearanceChanged(long receivedFrom, NotifyLevelingTattooAppearanceChanged levelingTattooAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}, PageNumber={PageNumber}", nameof(NotifyLevelingTattooAppearanceChanged), receivedFrom, levelingTattooAppearanceChanged.Tattoo.Index, levelingTattooAppearanceChanged.Tattoo.PageNumber);

            var levelingTattoo = Mapper.Map<NetworkLevelingTattoo>(levelingTattooAppearanceChanged.Tattoo);
            LevelingInteraction.SelectLevelingTattooAppearance(levelingTattoo);

            OnAfterNetworkMessageHandled(receivedFrom, levelingTattooAppearanceChanged);
        }

        private void OnNotifyLevelingScarAppearanceChanged(long receivedFrom, NotifyLevelingScarAppearanceChanged levelingScarAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}", nameof(NotifyLevelingScarAppearanceChanged), receivedFrom, levelingScarAppearanceChanged.Index);

            LevelingInteraction.SelectLevelingScarAppearance(levelingScarAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingScarAppearanceChanged);
        }

        private void OnNotifyLevelingSecondaryOutfitColorAppearanceChanged(long receivedFrom, NotifyLevelingSecondaryOutfitColorAppearanceChanged levelingSecondaryOutfitColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}", nameof(NotifyLevelingSecondaryOutfitColorAppearanceChanged), receivedFrom, levelingSecondaryOutfitColorAppearanceChanged.TextureName);

            LevelingInteraction.SelectLevelingSecondaryOutfitColorAppearance(levelingSecondaryOutfitColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingSecondaryOutfitColorAppearanceChanged);
        }

        private void OnNotifyLevelingPrimaryOutfitColorAppearanceChanged(long receivedFrom, NotifyLevelingPrimaryOutfitColorAppearanceChanged levelingPrimaryOutfitColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}", nameof(NotifyLevelingPrimaryOutfitColorAppearanceChanged), receivedFrom, levelingPrimaryOutfitColorAppearanceChanged.TextureName);

            LevelingInteraction.SelectLevelingPrimaryOutfitColorAppearance(levelingPrimaryOutfitColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPrimaryOutfitColorAppearanceChanged);
        }

        private void OnNotifyLevelingHornsColorAppearanceChanged(long receivedFrom, NotifyLevelingHornsColorAppearanceChanged levelingHornsColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}", nameof(NotifyLevelingHornsColorAppearanceChanged), receivedFrom, levelingHornsColorAppearanceChanged.TextureName);

            LevelingInteraction.SelectLevelingHornsColorAppearance(levelingHornsColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHornsColorAppearanceChanged);
        }

        private void OnNotifyLevelingHornsAppearanceChanged(long receivedFrom, NotifyLevelingHornsAppearanceChanged levelingHornsAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}", nameof(NotifyLevelingHornsAppearanceChanged), receivedFrom, levelingHornsAppearanceChanged.Index);

            LevelingInteraction.SelectLevelingHornsAppearance(levelingHornsAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHornsAppearanceChanged);
        }

        private void OnNotifyLevelingHairStyleAppearanceChanged(long receivedFrom, NotifyLevelingHairStyleAppearanceChanged levelingHairStyleAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}", nameof(NotifyLevelingHairStyleAppearanceChanged), receivedFrom, levelingHairStyleAppearanceChanged.Index);

            LevelingInteraction.SelectLevelingHairStyleAppearance(levelingHairStyleAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHairStyleAppearanceChanged);
        }

        private void OnNotifyLevelingHairColorAppearanceChanged(long receivedFrom, NotifyLevelingHairColorAppearanceChanged levelingHairColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}", nameof(NotifyLevelingHairColorAppearanceChanged), receivedFrom, levelingHairColorAppearanceChanged.TextureName);

            LevelingInteraction.SelectLevelingHairColorAppearance(levelingHairColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHairColorAppearanceChanged);
        }

        private void OnNotifyLevelingFaceAppearanceChanged(long receivedFrom, NotifyLevelingFaceAppearanceChanged levelingFaceAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}", nameof(NotifyLevelingFaceAppearanceChanged), receivedFrom, levelingFaceAppearanceChanged.Index);

            LevelingInteraction.SelectLevelingFaceAppearance(levelingFaceAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingFaceAppearanceChanged);
        }

        private void OnNotifyLevelingEyesColorAppearanceChanged(long receivedFrom, NotifyLevelingEyesColorAppearanceChanged levelingEyesColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}", nameof(NotifyLevelingEyesColorAppearanceChanged), receivedFrom, levelingEyesColorAppearanceChanged.TextureName);

            LevelingInteraction.SelectLevelingEyesColorAppearance(levelingEyesColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingEyesColorAppearanceChanged);
        }

        private void OnNotifyLevelingBodyColorAppearanceChanged(long receivedFrom, NotifyLevelingBodyColorAppearanceChanged levelingBodyColorAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TextureName={TextureName}", nameof(NotifyLevelingBodyColorAppearanceChanged), receivedFrom, levelingBodyColorAppearanceChanged.TextureName);

            LevelingInteraction.SelectLevelingBodyColorAppearance(levelingBodyColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBodyColorAppearanceChanged);
        }

        private void OnNotifyLevelingBodyTypeAppearanceChanged(long receivedFrom, NotifyLevelingBodyTypeAppearanceChanged levelingBodyTypeAppearanceChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}", nameof(NotifyLevelingBodyTypeAppearanceChanged), receivedFrom, levelingBodyTypeAppearanceChanged.Index);

            LevelingInteraction.SelectLevelingBodyTypeAppearance(levelingBodyTypeAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBodyTypeAppearanceChanged);
        }

        private void OnNotifyDialogPopupShown(long receivedFrom, NotifyDialogPopupShown dialogPopupShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", nameof(NotifyDialogPopupShown), receivedFrom, dialogPopupShown.PlayerId, dialogPopupShown.Popup.AreaName, dialogPopupShown.Popup.DialogName, dialogPopupShown.Popup.CueName);
            AddPlayerToTracker(Game.PlayersInDialogPopup, dialogPopupShown.PlayerId);

            UpdateDialogPopupState();

            OnAfterNetworkMessageHandled(receivedFrom, dialogPopupShown);
        }

        private void OnNotifyInventoryItemTransferred(long receivedFrom, NotifyInventoryItemTransferred itemTransferred)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Items={Items}, Source={Source}, Destination={Destination}", nameof(NotifyInventoryItemTransferred), receivedFrom, itemTransferred.TransferItem.Items.Select(x => x.UniqueId), itemTransferred.TransferItem.Source.Id, itemTransferred.TransferItem.Destination?.Id);

            var transferItem = Mapper.Map<NetworkItemsTransfer>(itemTransferred.TransferItem);
            GameInteraction.TransferInventoryItems(transferItem);

            OnAfterNetworkMessageHandled(receivedFrom, itemTransferred);
        }

        private void OnNotifyZoneLootClosed(long receivedFrom, NotifyZoneLootClosed zoneLootClosed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyZoneLootClosed), receivedFrom, zoneLootClosed.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInZoneLoot, zoneLootClosed.PlayerId);
            UpdateZoneLootUIState();

            OnAfterNetworkMessageHandled(receivedFrom, zoneLootClosed);
        }

        private void OnNotifyZoneLootShown(long receivedFrom, NotifyZoneLootShown zoneLootShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyZoneLootShown), receivedFrom, zoneLootShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInZoneLoot, zoneLootShown.PlayerId);
            UpdateZoneLootUIState();

            OnAfterNetworkMessageHandled(receivedFrom, zoneLootShown);
        }

        private void OnNotifyGlobalMapEncounterMessageShown(long receivedFrom, NotifyGlobalMapEncounterMessageShown globalMapEncounterMessageShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapEncounterMessageShown), receivedFrom, globalMapEncounterMessageShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapEncounterMessage, globalMapEncounterMessageShown.PlayerId);
            UpdateGlobalMapEncounterMessageUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapEncounterMessageShown);
        }

        private void OnNotifyGlobalMapCommonPopupShown(long receivedFrom, NotifyGlobalMapCommonPopupShown globalMapCommonPopupShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, Type={Type}, LocationId={LocationId}, LocationName={LocationName}", nameof(NotifyGlobalMapCommonPopupShown), receivedFrom, globalMapCommonPopupShown.PlayerId, globalMapCommonPopupShown.Popup.Type, globalMapCommonPopupShown.Popup.Location?.Id.Length, globalMapCommonPopupShown.Popup.Location?.Name);

            AddPlayerToTracker(Game.PlayersInGlobalMapCommonPopup, globalMapCommonPopupShown.PlayerId);
            var popup = Mapper.Map<NetworkGlobalMapCommonPopup>(globalMapCommonPopupShown.Popup);
            UpdateGlobalMapCommonPopupUIState(popup);

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCommonPopupShown);
        }

        private void OnNotifyGroupChangerOpened(long receivedFrom, NotifyGroupChangerOpened groupChangerVisible)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGroupChangerOpened), receivedFrom, groupChangerVisible.PlayerId);

            AddPlayerToTracker(Game.PlayersInGroupChanger, groupChangerVisible.PlayerId);
            UpdateGroupManagerUIState();

            OnAfterNetworkMessageHandled(receivedFrom, groupChangerVisible);
        }

        private void OnNotifyGlobalMapLocationMessageShown(long receivedFrom, NotifyGlobalMapLocationMessageShown globalMapLocationMessageShown)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyGlobalMapLocationMessageShown), receivedFrom, globalMapLocationMessageShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapLocationMessage, globalMapLocationMessageShown.PlayerId);
            UpdateGlobalMapLocationMessageUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapLocationMessageShown);
        }

        private void OnNotifySkipTimeOpened(long receivedFrom, NotifySkipTimeOpened skipTimeOpened)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifySkipTimeOpened), receivedFrom, skipTimeOpened.PlayerId);

            AddPlayerToTracker(Game.PlayersInSkipTime, skipTimeOpened.PlayerId);
            GameInteraction.OpenSkipTimeUI();

            UpdateSkipTimeUIState();

            OnAfterNetworkMessageHandled(receivedFrom, skipTimeOpened);
        }

        private void OnNotifyUnitStealthChanged(long receivedFrom, NotifyUnitStealthChanged unitStealthChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={Round}, IsEnabled={IsEnabled}, IsForced={IsForced}", nameof(NotifyUnitStealthChanged), receivedFrom, unitStealthChanged.UnitId, unitStealthChanged.IsEnabled, unitStealthChanged.IsForced);

            GameInteraction.ChangeUnitStealth(unitStealthChanged.UnitId, unitStealthChanged.IsEnabled, unitStealthChanged.IsForced);

            OnAfterNetworkMessageHandled(receivedFrom, unitStealthChanged);
        }

        private void OnNotifyCombatTurnDelayed(long receivedFrom, NotifyCombatTurnDelayed combatTurnDelayed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={Round}, TargetUnitId={TargetUnitId}", nameof(NotifyCombatTurnDelayed), receivedFrom, combatTurnDelayed.UnitId, combatTurnDelayed.TargetUnitId);

            Game.Combat.Turn.IsInProgress = false;
            CombatInteraction.DelayCombatTurn(combatTurnDelayed.UnitId, combatTurnDelayed.TargetUnitId);

            OnAfterNetworkMessageHandled(receivedFrom, combatTurnDelayed);
        }

        private void OnNotifyMapObjectLockpicked(long receivedFrom, NotifyMapObjectLockpicked mapObjectLockpicked)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, MapObjectId={MapObjectId}, MapObjectPosition={MapObjectPosition}, Units={Units}", nameof(NotifyMapObjectLockpicked), receivedFrom, mapObjectLockpicked.LockpickInteraction.MapObject.Id, mapObjectLockpicked.LockpickInteraction.MapObject.Position, mapObjectLockpicked.LockpickInteraction.Units);
            var lockpickInteraction = Mapper.Map<NetworkLockpickInteraction>(mapObjectLockpicked.LockpickInteraction);

            GameInteraction.LockpickMapObject(lockpickInteraction);

            OnAfterNetworkMessageHandled(receivedFrom, mapObjectLockpicked);
        }

        private void OnNotifyActionBarSlotMoved(long receivedFrom, NotifyActionBarSlotMoved actionBarSlotMoved)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, SourceSlotIndex={SourceSlotIndex}, SourceSlotAbilityId={SourceSlotAbilityId}, SourceSlotActivatableAbilityId={SourceSlotActivatableAbilityId}, SourceSlotItemId={SourceSlotItemId}, TargetSlotIndex={TargetSlotIndex}, TargetSlotAbilityId={TargetSlotAbilityId}, TargetSlotActivatableAbilityId={TargetSlotActivatableAbilityId}, TargetSlotItemId={TargetSlotItemId}",
                nameof(NotifyActionBarSlotMoved), receivedFrom, actionBarSlotMoved.SourceActionBarSlot.UnitId, actionBarSlotMoved.SourceActionBarSlot.Index, actionBarSlotMoved.SourceActionBarSlot.Ability?.Id, actionBarSlotMoved.SourceActionBarSlot.ActivatableAbility?.Id, actionBarSlotMoved.SourceActionBarSlot.Item?.UniqueId, actionBarSlotMoved.TargetActionBarSlot.Index, actionBarSlotMoved.TargetActionBarSlot.Ability?.Id, actionBarSlotMoved.TargetActionBarSlot.ActivatableAbility?.Id, actionBarSlotMoved.TargetActionBarSlot.Item?.UniqueId);

            var sourceActionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotMoved.SourceActionBarSlot);
            var targetActionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotMoved.TargetActionBarSlot);

            GameInteraction.MoveActionBarSlots(sourceActionBarSlot, targetActionBarSlot);

            OnAfterNetworkMessageHandled(receivedFrom, actionBarSlotMoved);
        }

        private void OnNotifyActionBarSlotCleared(long receivedFrom, NotifyActionBarSlotCleared actionBarSlotCleared)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, SlotIndex={SlotIndex}", nameof(NotifyActionBarSlotCleared), receivedFrom, actionBarSlotCleared.ActionBarSlot.UnitId, actionBarSlotCleared.ActionBarSlot.Index);

            var actionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotCleared.ActionBarSlot);

            GameInteraction.ClearActionBarSlot(actionBarSlot);

            OnAfterNetworkMessageHandled(receivedFrom, actionBarSlotCleared);
        }

        private void OnNotifyCharacterMove(long receivedFrom, NotifyCharacterMove characterMove)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, Destination={Destination}, Delay={Delay}, Orientation={Orientation}", nameof(NotifyCharacterMove), receivedFrom, characterMove.Move.UnitId, characterMove.Move.Destination, characterMove.Move.Delay, characterMove.Move.Orientation);

            var move = Mapper.Map<NetworkCharacterMove>(characterMove.Move);
            GameInteraction.MoveNonCombatCharacter(move);

            OnAfterNetworkMessageHandled(receivedFrom, characterMove);
        }

        private void OnNotifyGroundClicked(long receivedFrom, NotifyGroundClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, SelectedUnits={SelectedUnits}, WorldPosition={WorldPosition}, MovementLimit={MovementLimit}", nameof(NotifyGroundClicked), receivedFrom, clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition, clicked.Click.MovementLimit);
            if (Game.Combat == null)
            {
                Logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored out of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);

            OnAfterNetworkMessageHandled(receivedFrom, clicked);
        }

        private void OnNotifyUnitClicked(long receivedFrom, NotifyUnitClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyUnitClicked), receivedFrom, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            // Combat Unit clicks are usually followed up with UnitAttack command
            // UnitAttack commands are synced separately as we can enforce specific rules like fullattack
            // so this must be skiped to avoid command duplication
            var canGetUp = CombatInteraction.CanRiderGetUp();
            if (Game.Combat != null && !canGetUp)
            {
                Logger.LogInformation("Ignoring {MessageType} in combat", nameof(NotifyUnitClicked));
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickUnit(click);

            OnAfterNetworkMessageHandled(receivedFrom, clicked);
        }

        private void OnNotifyMapObjectClicked(long receivedFrom, NotifyMapObjectClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyMapObjectClicked), receivedFrom, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);

            OnAfterNetworkMessageHandled(receivedFrom, clicked);
        }

        private void OnNotifyToggleActivatableAbility(long receivedFrom, NotifyToggleActivatableAbility activatableAbility)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, CasterId={CasterId}, AbilityId={AbilityId}, IsActive={IsActive}", nameof(NotifyToggleActivatableAbility), receivedFrom, activatableAbility.Ability.CasterId, activatableAbility.Ability.Id, activatableAbility.Ability.IsActive);

            var ability = Mapper.Map<NetworkActivatableAbility>(activatableAbility.Ability);
            GameInteraction.ToggleActivatableAbility(ability);

            OnAfterNetworkMessageHandled(receivedFrom, activatableAbility);
        }

        private void OnNotifyAbilityUsed(long receivedFrom, NotifyAbilityUsed abilityUse)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, CasterId={CasterId}, AbilityId={AbilityId}, TargetUnitId={TargetUnitId}, TargetPoint={TargetPoint}, MovementLimit={MovementLimit}", nameof(NotifyAbilityUsed), receivedFrom, abilityUse.Ability.CasterId, abilityUse.Ability.Id, abilityUse.Ability.Target.UnitId, abilityUse.Ability.Target.Point, abilityUse.Ability.MovementLimit);

            var ability = Mapper.Map<NetworkAbility>(abilityUse.Ability);
            CombatInteraction.UseAbility(ability);

            OnAfterNetworkMessageHandled(receivedFrom, abilityUse);
        }

        private void OnNotifyUnitAttacked(long receivedFrom, NotifyUnitAttacked unitAttacked)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}, IsSingleAttack={IsSingleAttack}, MovementLimit={MovementLimit}",
                nameof(NotifyUnitAttacked), receivedFrom, unitAttacked.Attack.ExecutorUnitId, unitAttacked.Attack.TargetUnitId, unitAttacked.Attack.IsFullAttack, unitAttacked.Attack.IsSingleAttack, unitAttacked.Attack.MovementLimit);

            var attack = Mapper.Map<NetworkUnitAttack>(unitAttacked.Attack);
            CombatInteraction.AttackUnit(attack);

            OnAfterNetworkMessageHandled(receivedFrom, unitAttacked);
        }

        private async void OnNotifyPlayerCombatTurnEnded(long receivedFrom, NotifyPlayerCombatTurnEnded combatTurnEnded)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyPlayerCombatTurnEnded), receivedFrom, combatTurnEnded.PlayerId, combatTurnEnded.UnitId);

            await WaitWhileTrue(CombatInteraction.IsRiderActive, "Waiting for all combat commands to finish before ending turn");

            EndLocalTurn();

            OnAfterNetworkMessageHandled(receivedFrom, combatTurnEnded);
        }

        private void OnNotifyActiveHandEquipmentSetChanged(long receivedFrom, NotifyActiveHandEquipmentSetChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, SetIndex={SetIndex}", nameof(NotifyEquipmentSlotChanged), receivedFrom, changed.Set.UnitId, changed.Set.Index);
            var set = Mapper.Map<NetworkActiveHandEquipmentSet>(changed.Set);
            GameInteraction.SetActiveHandEquipmentSet(set);

            OnAfterNetworkMessageHandled(receivedFrom, changed);
        }

        private void OnNotifyEquipmentSlotChanged(long receivedFrom, NotifyEquipmentSlotChanged slotChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}, OwnerId={OwnerId}", nameof(NotifyEquipmentSlotChanged), receivedFrom, slotChanged.Slot.Position.Type, slotChanged.Slot.Position.Index, slotChanged.Slot.Item?.UniqueId, slotChanged.Slot.OwnerId);
            var slot = Mapper.Map<NetworkEquipmentSlot>(slotChanged.Slot);
            GameInteraction.UpdateEquipmentSlot(slot);

            OnAfterNetworkMessageHandled(receivedFrom, slotChanged);
        }

        private void OnNotifyDropItem(long receivedFrom, NotifyDropItem item)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, OwnerId={OwnerId}, ItemId={ItemId}, ItemName={ItemName}", nameof(NotifyDropItem), receivedFrom, item.Drop.OwnerEntityId, item.Drop.Item.UniqueId, item.Drop.Item.Name);

            var dropItem = Mapper.Map<NetworkDropItem>(item.Drop);
            GameInteraction.DropItem(dropItem);

            OnAfterNetworkMessageHandled(receivedFrom, item);
        }

        private void OnNotifyInventoryItemUsed(long receivedFrom, NotifyInventoryItemUsed inventoryItemUsed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UserUnitId={UserUnitId}, TargetUnitId={TargetUnitId}, ItemId={ItemId}, ItemName={ItemName}", nameof(NetworkUseInventoryItem), receivedFrom, inventoryItemUsed.UseItem.UserUnitId, inventoryItemUsed.UseItem.Target?.UnitId, inventoryItemUsed.UseItem.Item.UniqueId, inventoryItemUsed.UseItem.Item.Name);

            var useItem = Mapper.Map<NetworkUseInventoryItem>(inventoryItemUsed.UseItem);
            GameInteraction.UseInventoryItem(useItem);

            OnAfterNetworkMessageHandled(receivedFrom, inventoryItemUsed);
        }

        private void OnNotifyContainerSkinned(long receivedFrom, NotifyLootableEntitySkinned notifyLootableEntitySkinned)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Id={Id}, Position={Position}, Type={Type}", nameof(NotifyLootableEntitySkinned), receivedFrom, notifyLootableEntitySkinned.Entity.Id, notifyLootableEntitySkinned.Entity.Position, notifyLootableEntitySkinned.Entity.Type);
            var container = Mapper.Map<NetworkLootableEntity>(notifyLootableEntitySkinned.Entity);
            GameInteraction.SkinLootContainer(container);

            OnAfterNetworkMessageHandled(receivedFrom, notifyLootableEntitySkinned);
        }

        private void OnNotifyOvertipInteracted(long receivedFrom, NotifyOvertipInteracted interacted)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, MapObjectId={MapObjectId}, UnitsCount={UnitsCount}", nameof(NotifyOvertipInteracted), receivedFrom, interacted.Overtip.MapObject.Id, interacted.Overtip.Units);
            var overtip = Mapper.Map<NetworkOvertip>(interacted.Overtip);
            GameInteraction.InteractWithOvertip(overtip);

            OnAfterNetworkMessageHandled(receivedFrom, interacted);
        }

        private void OnNotifyUnitJoinedMidCombat(long receivedFrom, NotifyUnitJoinedMidCombat combat)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyUnitJoinedMidCombat), receivedFrom, combat.PlayerId, combat.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, combat.PlayerId, combat.UnitId);

            OnAfterNetworkMessageHandled(receivedFrom, combat);
        }

        private void OnNotifyRestBanterInterrupted(long receivedFrom, NotifyRestBanterInterrupted interrupted)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, SpeakerUnitId={SpeakerUnitId}, Key={Key}", nameof(NotifyRestBanterInterrupted), receivedFrom, interrupted.Banter.SpeakerUnitId, interrupted.Banter.Key);
            var banter = Mapper.Map<NetworkRestBanter>(interrupted.Banter);
            GameInteraction.TryInterruptRestBanter(banter);

            OnAfterNetworkMessageHandled(receivedFrom, interrupted);
        }

        private void OnNotifyVendorItemTransferred(long receivedFrom, NotifyVendorItemTransferred message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, ItemId={ItemId}, Count={Count}, Action={Action}, ActionTarget={ActionTarget}", nameof(NotifyVendorItemTransferred), receivedFrom, message.ItemTransfer.Item.UniqueId, message.ItemTransfer.Count, message.ItemTransfer.ItemAction, message.ItemTransfer.ItemActionTarget);

            var transfer = Mapper.Map<NetworkVendorItemTransfer>(message.ItemTransfer);
            GameInteraction.TransferVendorItem(transfer);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifySpellForgotten(long receivedFrom, NotifySpellForgotten spellForgotten)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellForgotten), receivedFrom, spellForgotten.Slot.UnitId, spellForgotten.Slot.SpellbookId, spellForgotten.Slot.Index, spellForgotten.Slot.Type);

            var slot = Mapper.Map<NetworkSpellSlot>(spellForgotten.Slot);

            GameInteraction.ForgetSpell(slot);

            OnAfterNetworkMessageHandled(receivedFrom, spellForgotten);
        }

        private void OnNotifySpellMemorized(long receivedFrom, NotifySpellMemorized memorized)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, UnitId={UnitId}, SpellbookId={SpellbookId}, SpellId={SpellId}, SpellLevel={SpellLevel}, SpellName={SpellName}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellMemorized), receivedFrom, memorized.Slot.UnitId, memorized.Slot.SpellbookId, memorized.Slot.SpellId, memorized.Slot.SpellLevel, memorized.Slot.SpellName, memorized.Slot.Index, memorized.Slot.Type);

            var slot = Mapper.Map<NetworkSpellSlot>(memorized.Slot);

            GameInteraction.MemorizeSpell(slot);

            OnAfterNetworkMessageHandled(receivedFrom, memorized);
        }

        private void OnNotifyLevelingPortraitSelected(long receivedFrom, NotifyLevelingPortraitSelected levelingPortraitSelected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Name={Name}, CustomId={CustomId}, Category={Category}", nameof(NotifyLevelingPortraitSelected), receivedFrom, levelingPortraitSelected.Portrait.Name, levelingPortraitSelected.Portrait.CustomId, levelingPortraitSelected.Portrait.Category);

            var levelingPortrait = Mapper.Map<NetworkLevelingPortrait>(levelingPortraitSelected.Portrait);
            LevelingInteraction.SelectLevelingPortrait(levelingPortrait);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPortraitSelected);
        }

        private void OnNotifyLevelingVoiceSelected(long receivedFrom, NotifyLevelingVoiceSelected levelingVoiceSelected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Id={Id}, GenderId={GenderId}", nameof(NotifyLevelingVoiceSelected), receivedFrom, levelingVoiceSelected.Voice.Id, levelingVoiceSelected.Voice.GenderId);

            var levelingVoice = Mapper.Map<NetworkLevelingVoice>(levelingVoiceSelected.Voice);
            LevelingInteraction.SelectLevelingVoice(levelingVoice);

            OnAfterNetworkMessageHandled(receivedFrom, levelingVoiceSelected);
        }

        private void OnNotifyLevelingAlignmentSelected(long receivedFrom, NotifyLevelingAlignmentSelected levelingAlignmentSelected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, AlignmentId={AlignmentId}", nameof(NotifyLevelingGenderSelected), receivedFrom, levelingAlignmentSelected.AlignmentId);

            LevelingInteraction.SelectLevelingAlignment(levelingAlignmentSelected.AlignmentId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingAlignmentSelected);
        }

        private void OnNotifyLevelingNameChanged(long receivedFrom, NotifyLevelingNameChanged levelingNameChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Name={Name}", nameof(NotifyLevelingGenderSelected), receivedFrom, levelingNameChanged.Name);

            LevelingInteraction.SetLevelingName(levelingNameChanged.Name);

            OnAfterNetworkMessageHandled(receivedFrom, levelingNameChanged);
        }

        private void OnNotifyLevelingGenderSelected(long receivedFrom, NotifyLevelingGenderSelected levelingGenderSelected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, GenderId={GenderId}", nameof(NotifyLevelingGenderSelected), receivedFrom, levelingGenderSelected.GenderId);

            LevelingInteraction.SelectLevelingGender(levelingGenderSelected.GenderId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingGenderSelected);
        }

        private void OnNotifyLevelingRaceSelected(long receivedFrom, NotifyLevelingRaceSelected levelingRaceSelected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, RaceId={RaceId}", nameof(NotifyLevelingRaceSelected), receivedFrom, levelingRaceSelected.RaceId);

            LevelingInteraction.SelectLevelingRace(levelingRaceSelected.RaceId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingRaceSelected);
        }

        private void OnNotifyLevelingRacialAbilityScoreBonusChanged(long receivedFrom, NotifyLevelingRacialAbilityScoreBonusChanged racialAbilityScoreBonusChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Direction={Direction}", nameof(NotifyLevelingRaceSelected), receivedFrom, racialAbilityScoreBonusChanged.Direction);

            var direction = Mapper.Map<NetworkLevelingSequenceDirection>(racialAbilityScoreBonusChanged.Direction);

            LevelingInteraction.ChangeLevelingRacialAbilityScoreBonus(direction);

            OnAfterNetworkMessageHandled(receivedFrom, racialAbilityScoreBonusChanged);
        }

        private void OnNotifyLevelingBirthDayChanged(long receivedFrom, NotifyLevelingBirthDayChanged levelingBirthDayChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Direction={Direction}", nameof(NotifyLevelingBirthDayChanged), receivedFrom, levelingBirthDayChanged.Direction);

            var direction = Mapper.Map<NetworkLevelingSequenceDirection>(levelingBirthDayChanged.Direction);

            LevelingInteraction.ChangeLevelingBirthDay(direction);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBirthDayChanged);
        }

        private void OnNotifyLevelingBirthMonthChanged(long receivedFrom, NotifyLevelingBirthMonthChanged levelingBirthMonthChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Direction={Direction}", nameof(NotifyLevelingBirthMonthChanged), receivedFrom, levelingBirthMonthChanged.Direction);

            var direction = Mapper.Map<NetworkLevelingSequenceDirection>(levelingBirthMonthChanged.Direction);

            LevelingInteraction.ChangeLevelingBirthMonth(direction);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBirthMonthChanged);
        }

        private void OnNotifyLevelingAbilityScoreDecreased(long receivedFrom, NotifyLevelingAbilityScoreDecreased levelingAbilityScoreDecreased)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, StatType={StatType}", nameof(NotifyLevelingAbilityScoreDecreased), receivedFrom, levelingAbilityScoreDecreased.AbilityScore.StatType);

            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(levelingAbilityScoreDecreased.AbilityScore);
            LevelingInteraction.DecreaseLevelingAbilityScore(abilityScore);

            OnAfterNetworkMessageHandled(receivedFrom, levelingAbilityScoreDecreased);
        }

        private void OnNotifyLevelingAbilityScoreIncreased(long receivedFrom, NotifyLevelingAbilityScoreIncreased levelingAbilityScoreIncreased)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, StatType={StatType}", nameof(NotifyLevelingAbilityScoreIncreased), receivedFrom, levelingAbilityScoreIncreased.AbilityScore.StatType);

            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(levelingAbilityScoreIncreased.AbilityScore);
            LevelingInteraction.IncreaseLevelingAbilityScore(abilityScore);

            OnAfterNetworkMessageHandled(receivedFrom, levelingAbilityScoreIncreased);
        }

        private void OnNotifyLevelingCompleted(long receivedFrom, NotifyLevelingCompleted completed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(NotifyLevelingCompleted), receivedFrom);
            LevelingInteraction.CompleteLeveling();

            OnAfterNetworkMessageHandled(receivedFrom, completed);
        }

        private void OnNotifyLevelingTerminated(long receivedFrom, NotifyLevelingTerminated terminated)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}", nameof(NotifyLevelingTerminated), receivedFrom);
            LevelingInteraction.TerminateLeveling();

            OnAfterNetworkMessageHandled(receivedFrom, terminated);
        }

        private void OnNotifyLevelingSpellRemoved(long receivedFrom, NotifyLevelingSpellRemoved removed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellRemoved), receivedFrom, removed.Spell.Name, removed.Spell.Id);
            var spell = Mapper.Map<NetworkLevelingSpell>(removed.Spell);
            LevelingInteraction.RemoveLevelingSpell(spell);

            OnAfterNetworkMessageHandled(receivedFrom, removed);
        }

        private void OnNotifyLevelingSpellChosen(long receivedFrom, NotifyLevelingSpellChosen chosen)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellChosen), receivedFrom, chosen.Spell.Name, chosen.Spell.Id);
            var spell = Mapper.Map<NetworkLevelingSpell>(chosen.Spell);
            LevelingInteraction.SelectLevelingSpell(spell);

            OnAfterNetworkMessageHandled(receivedFrom, chosen);
        }

        private void OnNotifyLevelingFeatureSelected(long receivedFrom, NotifyLevelingFeatureSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, FeatureName={FeatureName}, FeatureId={FeatureId}", nameof(NotifyLevelingFeatureSelected), receivedFrom, selected.Feature.Name, selected.Feature.Id);
            var feature = Mapper.Map<NetworkLevelingFeature>(selected.Feature);
            LevelingInteraction.SelectLevelingFeature(feature);

            OnAfterNetworkMessageHandled(receivedFrom, selected);
        }

        private void OnNotifyLevelingSkillPointDecreased(long receivedFrom, NotifyLevelingSkillPointDecreased decreased)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, StatType={StatType}", nameof(NotifyLevelingSkillPointDecreased), receivedFrom, decreased.Skill.StatType);
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(decreased.Skill);
            LevelingInteraction.DecreaseLevelingSkillPoint(skillPoint);
            OnAfterNetworkMessageHandled(receivedFrom, decreased);
        }

        private void OnNotifyLevelingSkillPointIncreased(long receivedFrom, NotifyLevelingSkillPointIncreased increased)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, StatType={StatType}", nameof(NotifyLevelingSkillPointIncreased), receivedFrom, increased.Skill.StatType);
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(increased.Skill);
            LevelingInteraction.IncreaseLevelingSkillPoint(skillPoint);

            OnAfterNetworkMessageHandled(receivedFrom, increased);
        }

        private void OnNotifyLevelingPhaseChanged(long receivedFrom, NotifyLevelingPhaseChanged levelingPhaseChanged)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Index={Index}", nameof(NotifyLevelingPhaseChanged), receivedFrom, levelingPhaseChanged.Phase.Index);
            var phase = Mapper.Map<NetworkLevelingPhase>(levelingPhaseChanged.Phase);
            ResetPlayersTracker(Game.Leveling.PlayerReadiness);
            LevelingInteraction.SwitchLevelingPhase(phase);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPhaseChanged);
        }

        private async void OnNotifyLevelingPhaseWitnessed(long receivedFrom, NotifyLevelingPhaseWitnessed levelingPhaseWitnessed)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, PlayerId={PlayerId}", nameof(NotifyLevelingPhaseWitnessed), receivedFrom, levelingPhaseWitnessed.PlayerId);

            // leveling is always created at the host first and later on on clients as a part of leveling confirmation
            // but not in case when game is forcing leveling ui to open for everyone at the same time => means there is some racing there
            await WaitWhileTrue(() => Game.Leveling == null, "Received leveling witness notification, but leveling has not been started yet.");

            WitnessLevelingPhase(levelingPhaseWitnessed.PlayerId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPhaseWitnessed);
        }

        private void OnNotifyLevelingClassArchetypeSelected(long receivedFrom, NotifyLevelingClassArchetypeSelected message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, Id={Id}, Name={Name}", nameof(NotifyLevelingClassArchetypeSelected), receivedFrom, message.Archetype?.Id, message.Archetype?.Name);

            var archetype = Mapper.Map<NetworkLevelingArchetype>(message.Archetype);
            LevelingInteraction.SelectLevelingClassArchetype(archetype);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }


        private void OnNotifyLevelingMythicClassSelected(long receivedFrom, NotifyLevelingMythicClassSelected mythicClassSelected)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, MythicClassId={MythicClassId}", nameof(NotifyLevelingMythicClassSelected), receivedFrom, mythicClassSelected.MythicClassId);
            LevelingInteraction.SelectMythicLevelingClass(mythicClassSelected.MythicClassId);

            OnAfterNetworkMessageHandled(receivedFrom, mythicClassSelected);
        }

        private void OnNotifyLevelingClassSelected(long receivedFrom, NotifyLevelingClassSelected message)
        {
            Logger.LogInformation("Received {MessageType}. ReceivedFrom={ReceivedFrom}, ClassId={ClassId}", nameof(NotifyLevelingClassSelected), receivedFrom, message.Class.Id, message.Class.Name);

            var levelingClass = Mapper.Map<NetworkLevelingClass>(message.Class);
            LevelingInteraction.SelectLevelingClass(levelingClass);

            OnAfterNetworkMessageHandled(receivedFrom, message);
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
                    if (kv.Value.Count >= GetSyncedPlayersCount())
                    {
                        Game.Combat.ConfirmedMidCombatUnits.Add(kv.Key);
                    }
                }
            }
        }

        private void InitializeNewTurn(string unitId, bool actingInSurpriseRound)
        {
            lock (ActionLock)
            {
                Game.Combat.Turn = new NetworkCombatTurn
                {
                    UnitId = unitId,
                    IsInProgress = false,
                    IsActingInSurpriseRound = actingInSurpriseRound,
                    IsLocalPlayer = IsControlledByLocalPlayer(unitId),
                    IsAI = !GameInteraction.IsUnitInParty(unitId),
                };
            }

            Logger.LogInformation("OnTurnStart. UnitId={UnitId}, IsLocalPlayer={IsLocalPlayer}, IsAI={IsAI}, IsActingInSurpriseRound={IsActingInSurpriseRound}, IsInProgress={IsInProgress}",
                unitId, Game.Combat.Turn.IsLocalPlayer, Game.Combat.Turn.IsAI, Game.Combat.Turn.IsActingInSurpriseRound, Game.Combat.Turn.IsInProgress);

            OnLocalPlayerTurnStart();
        }

        private bool WasControlledByCurrentPlayer(string unitId)
        {
            // could be horse leveling
            var realCharacterUnitId = GameInteraction.GetPetOwnerId(unitId) ?? unitId;

            if (string.IsNullOrEmpty(realCharacterUnitId)
                || !Game.CharactersOwnershipHistory.TryGetValue(realCharacterUnitId, out var playerId)
                || GetPlayer(playerId) == null)
            {
                return HasControlOverUI;
            }

            var canControl = playerId == Game.LocalPlayerId;
            return canControl;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private bool IsOutOfSupportedArea(int currentChapter, string currentArea)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var isOutOfSupport = currentChapter switch
            {
                <= 2 => false,
                _ => true,
            };

            return isOutOfSupport;
        }

        private bool ReadyChanged(NetworkPlayer networkPlayer, bool isReady)
        {
            lock (ActionLock)
            {
                networkPlayer.IsReady = isReady;

                InvokeOnPlayersChanged();

                var readyChanged = new NotifyPlayerReadyStatusChanged { PlayerId = networkPlayer.Id, IsReady = networkPlayer.IsReady };
                Send(readyChanged);

                return networkPlayer.IsReady;
            }
        }

        private void SaveLastCombatTurn()
        {
            if (Game.Combat?.Turn != null)
            {
                Game.LastCombatTurn = Game.Combat.Turn;
            }
        }
    }
}

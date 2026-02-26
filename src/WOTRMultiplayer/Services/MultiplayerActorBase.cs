using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Kingmaker.Controllers.Rest;
using Kingmaker.GameModes;
using Kingmaker.UI.Kingdom;
using Kingmaker.Utility;
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.GameInteraction.CombatLog;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.ActionBar;
using WOTRMultiplayer.Entities.Area;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Combat.Crusades;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Dialogs;
using WOTRMultiplayer.Entities.Equipment;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.GlobalMap.Kingdom;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Leveling;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.NewGame;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Entities.Rest;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Entities.SpellbookManagement;
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

        public NetworkArea CurrentArea => Game.CurrentArea;

        public bool IsInCombat => Game?.Combat != null;

        public int SessionSeed => Game.SessionSeed;

        public int? LoadedSaveSeed => Game.LoadedSaveSeed;

        public int? AreaSeed => Game.CurrentArea?.Seed;

        public int? CombatSeed => Game.Combat?.Seed;

        public int? CombatTurnSeed => Game.Combat?.Turn?.Seed;

        public int? LastCombatTurnSeed => Game.LastCombatTurn?.Seed;

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

        protected IValueGenerator ValueGenerator { get; private set; }

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
            ValueGenerator = valueGenerator;
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
                var isControlled = character?.Owner != null && character.Owner.Id == Game.LocalPlayerId;

                if (!isControlled && GameInteraction.IsCapitalPartyMode)
                {
                    isControlled = WasControlledByCurrentPlayer(unitId);
                }

                return isControlled;
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

        public void OnAbilityUse(NetworkAbilityUse abilityUse)
        {
            if (!ShouldNotifyAboutAbility(abilityUse.InitiatorUnitId))
            {
                return;
            }

            var message = new NotifyAbilityUsed
            {
                AbilityUse = Mapper.Map<Networking.Messages.Contracts.NetworkAbilityUse>(abilityUse)
            };
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

            var isLocal = IsControlledByLocalPlayer(networkUnitAttack.InitiatorUnitId);
            if (!isLocal || (Game.Combat.Turn?.IsAI ?? false))
            {
                return;
            }

            var message = new NotifyUnitAttacked
            {
                Attack = Mapper.Map<Networking.Messages.Contracts.NetworkUnitAttack>(networkUnitAttack)
            };
            Send(message);
        }

        public void OnToggleActivatableAbility(NetworkActivatableAbility activatableAbilityUse)
        {
            if (!ShouldNotifyAboutAbility(activatableAbilityUse.CasterId))
            {
                return;
            }

            var message = new NotifyToggleActivatableAbility
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkActivatableAbility>(activatableAbilityUse)
            };
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
            if (Game.Combat == null && (IsControlledByPlayers(click.TargetUnitId) || !IsControlledByLocalPlayer(click.SelectedUnits.FirstOrDefault()))
                || Game.Combat != null && (!(Game.Combat.Turn?.IsLocalPlayer ?? false) || CombatInteraction.IsCombatTurnFinished()))
            {
                return;
            }

            var message = new NotifyUnitClicked
            {
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
            };
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
            Send(message);
        }

        public void OnClickMapObject(NetworkClick click)
        {
            if (!IsControlledByLocalPlayer(click.SelectedUnits.FirstOrDefault()))
            {
                return;
            }

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
            Send(message);
        }

        public void OnSkinLootContainer(NetworkLootableEntity networkLootableEntity)
        {
            var message = new NotifyLootableEntitySkinned
            {
                Entity = Mapper.Map<Networking.Messages.Contracts.NetworkLootableEntity>(networkLootableEntity)
            };
            Send(message);
        }

        public void OnDropItem(NetworkDropItem dropItem)
        {
            var message = new NotifyDropItem
            {
                Drop = Mapper.Map<Networking.Messages.Contracts.NetworkDropItem>(dropItem)
            };
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
            Send(message);
        }

        public void OnChangeActiveHandEquipmentSet(NetworkActiveHandEquipmentSet set)
        {
            if (!IsControlledByPlayers(set.UnitId) && !GameInteraction.IsUnitInParty(set.UnitId))
            {
                return;
            }

            var message = new NotifyActiveHandEquipmentSetChanged
            {
                Set = Mapper.Map<Networking.Messages.Contracts.NetworkActiveHandEquipmentSet>(set)
            };
            Send(message);
        }

        public void OnEquipmentSlotChanged(NetworkEquipmentSlot equipmentSlot)
        {
            if (!IsControlledByPlayers(equipmentSlot.OwnerId) && !GameInteraction.IsUnitInParty(equipmentSlot.OwnerId))
            {
                return;
            }

            var message = new NotifyEquipmentSlotChanged
            {
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkEquipmentSlot>(equipmentSlot)
            };
            Send(message);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            var message = new NotifyOvertipInteracted
            {
                Overtip = Mapper.Map<Networking.Messages.Contracts.NetworkOvertip>(networkOvertip)
            };
            Send(message);
        }

        public virtual void OnAreaLoaded()
        {
            Logger.LogInformation("OnAreaLoaded");
            Game.ForcedPause?.ReadyPlayers.Add(Game.LocalPlayerId);

            if (Game.CurrentArea.IsGlobalMap)
            {
                if (!Game.PlayersInGlobalMapMode.TryGetValue(Game.LocalPlayerId, out var mode))
                {
                    mode = NetworkGlobalMapTravelerMode.Player;
                }

                OnGlobalMapTravelerModeChanged(mode);
            }
        }

        public void OnAreaLoadingComplete()
        {
            Game.CurrentArea = GameInteraction.GetCurrentArea();
            Logger.LogInformation("Area scenes loaded. Chapter={Chapter}, AreaName={AreaName}", Game.CurrentArea.Chapter, Game.CurrentArea.Name);

            SetLobbyStage(NetworkLobbyStage.Playing);

            SoftReset();

            UpdateCharactersOwnership();

            lock (ActionLock)
            {
                EnsureForcePaused(NetworkForcedPauseReason.AreaLoading);
                GameInteraction.SetPause(true);
            }

            if (IsOutOfSupportedArea())
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
            // Tutorial settings are save dependant, so it must be overridden if save was created without a mod
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
            Send(message);

            PingInteraction.Create(null, ping);
        }


        public void OnCutsceneSkip()
        {
            var localPlayer = GetPlayer(Game.LocalPlayerId);
            var message = new NotifyCutsceneSkipped { PlayerId = localPlayer.Id };
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
            Send(message);
        }

        public void CombatStarted()
        {
            Logger.LogInformation("Combat started");
            if (Game.Combat != null)
            {
                Logger.LogWarning("Previous combat has not been disposed correctly");
            }

            Game.Combat = new NetworkCombat { StartedAt = DateTime.UtcNow };
            Game.LastCombatTurn = null;

            var combatState = CombatInteraction.GetCombatState();
            var message = new NotifyCombatStarted
            {
                PlayerId = Game.LocalPlayerId,
                State = Mapper.Map<Networking.Messages.Contracts.NetworkCombatState>(combatState),
            };
            Send(message);
        }

        public void CombatEnded()
        {
            try
            {
                Logger.LogInformation("Combat ended");
                if (Game.Combat == null)
                {
                    Logger.LogWarning("Combat has not been started correctly");
                }

                SaveLastCombatTurn();

                Game.Combat = null;
                ValueGenerator.ResetSeededGenerators(IdentifierLifetime.Combat, IdentifierLifetime.CombatTurn);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error ending combat");
                throw;
            }
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
            Send(message);
        }

        public bool OnBeforeTurnStart(string unitId, bool actingInSurpriseRound)
        {
            try
            {
                Logger.LogInformation("Trying to start turn. TurnInitialized={TurnInitialized}, UnitId={UnitId}, ActingInSurpriseRound={ActingInSurpriseRound}", Game.Combat.Turn != null, unitId, actingInSurpriseRound);
                if (Game.Combat.Turn == null)
                {
                    InitializeNewTurn(unitId, actingInSurpriseRound);
                    return false;
                }

                switch (Game.Combat.Turn.Stage)
                {
                    case NetworkCombatTurnStage.Playing:
                        if (!string.Equals(Game.Combat.Turn.UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                        {
                            Logger.LogWarning("Invalid unit turn start detected. ExpectedUnitId={ExpectedUnitId}, ActualUnitId={ActualUnitId}", Game.Combat.Turn.UnitId, unitId);
                            InitializeNewTurn(unitId, actingInSurpriseRound);
                            return false;
                        }

                        UpdateConfirmedMidCombatUnits();
                        PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.Turn.Started.Key, CombatTextSeverity.Common, new UnitEntityLog(unitId));
                        Logger.LogInformation("Turn start is allowed. UnitId={UnitId}, IsActingInSurpiseRound={IsActingInSurpiseRound}, TurnUnitId={TurnUnitId}, TurnSeed={TurnSeed}", unitId, actingInSurpriseRound, Game.Combat.Turn.UnitId, Game.Combat.Turn.Seed);
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to start turn");
                throw;
            }
        }

        public bool OnBeforeTurnEnd(string unitId)
        {
            try
            {
                switch (Game.Combat.Turn.Stage)
                {
                    case NetworkCombatTurnStage.Playing:
                        var message = new NotifyCombatLocalTurnEnded
                        {
                            UnitId = Game.Combat.Turn.UnitId,
                            PlayerId = Game.LocalPlayerId
                        };
                        Send(message);
                        SetCombatTurnStage(NetworkCombatTurnStage.Ending);
                        OnLocalPlayerTurnEnd();
                        return false;
                    case NetworkCombatTurnStage.Ended:
                        Logger.LogInformation("Turn end is allowed. Round={Round}, TurnUnitId={TurnUnitId}, IsAI={IsAI}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn.UnitId, Game.Combat.Turn.IsAI, unitId);
                        Game.Combat.TriggeredAreaEffects.Clear();
                        ResetCombatTurn();
                        PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.Turn.Ended.Key, CombatTextSeverity.Common, new UnitEntityLog(unitId));
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unabel to process turn end");
                throw;
            }
        }

        public bool CanUnitJoinCombat(string unitId)
        {
            try
            {
                if (Game.Combat == null || Game.Combat.Stage == NetworkCombatStage.Preparing)
                {
                    Logger.LogInformation("Unit is allowed to join combat at current stage. UnitId={UnitId}, Stage={Stage}", unitId, Game.Combat?.Stage);
                    return true;
                }

                var isSummoned = GameInteraction.IsSummoned(unitId);
                if (isSummoned)
                {
                    Logger.LogInformation("Summoned unit is allowed to join combat. UnitId={UnitId}", unitId);
                    return true;
                }

                if (Game.Combat.ConfirmedMidCombatUnits.Contains(unitId))
                {
                    if (Game.Combat.UntargetableUnits.Remove(unitId))
                    {
                        CombatInteraction.MakeUnitTargetable(unitId, isTargetable: true);
                    }

                    Logger.LogWarning("Unit has been allowed to join mid combat. UnitId={UnitId}", unitId);
                    return true;
                }

                var isFirstJoinEvent = !ConfirmReadiness(PlayerTurnReadinessType.UnitJoinedMidCombat, Game.LocalPlayerId, unitId);
                if (isFirstJoinEvent)
                {
                    var message = new NotifyUnitJoinedMidCombat { UnitId = unitId, PlayerId = Game.LocalPlayerId };
                    Send(message);
                }

                AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, Game.LocalPlayerId, unitId);
                if (Game.Combat.UntargetableUnits.Add(unitId))
                {
                    CombatInteraction.MakeUnitTargetable(unitId, isTargetable: false);
                }

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
                Game.DialogState = null;
            }

            var message = new NotifyGameModeTypeEnded { PlayerId = Game.LocalPlayerId, Type = type.Name };
            Send(message);
        }

        public void OnCapitalModeRest()
        {
            var message = new NotifyCapitalModeRestInitiated();
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
            Send(message);
        }

        public void OnInterrupRestBanterBark(NetworkRestBanter networkBanter)
        {
            var message = new NotifyRestBanterInterrupted
            {
                Banter = Mapper.Map<Networking.Messages.Contracts.NetworkRestBanter>(networkBanter),
            };

            Send(message);
        }

        public void OnTransferVendorItem(NetworkVendorItemTransfer transfer)
        {
            var message = new NotifyVendorItemTransferred
            {
                ItemTransfer = Mapper.Map<Networking.Messages.Contracts.NetworkVendorItemTransfer>(transfer)
            };
            Send(message);
        }

        public void OnMemorizeSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility)
        {
            var message = new NotifySpellMemorized
            {
                UnitId = unitId,
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkSpellSlot>(networkSpellSlot),
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(networkAbility),
            };
            Send(message);
        }

        public void OnForgetSpell(string unitId, NetworkSpellSlot networkSpellSlot, NetworkAbility networkAbility)
        {
            var message = new NotifySpellForgotten
            {
                UnitId = unitId,
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkSpellSlot>(networkSpellSlot),
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(networkAbility),
            };
            Send(message);
        }

        public void OnRemoveCustomSpell(string unitId, NetworkAbility ability)
        {
            if (!IsControlledByLocalPlayer(unitId))
            {
                return;
            }

            var message = new NotifyCustomSpellRemoved
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(ability),
                UnitId = unitId
            };
            Send(message);
        }

        public void OnSpellbookMetamagicSpellCreated(NetworkMetamagicSpell metamagicSpell)
        {
            var message = new NotifyMetamagicSpellCreated
            {
                MetamagicSpell = Mapper.Map<Networking.Messages.Contracts.NetworkMetamagicSpell>(metamagicSpell)
            };
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
            }

            WitnessLevelingPhase(Game.LocalPlayerId);
            var message = new NotifyLevelingPhaseWitnessed
            {
                PlayerId = Game.LocalPlayerId,
                Phase = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingPhase>(phase)
            };
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
            }

            WitnessNewGameSequencePhase(Game.LocalPlayerId, phase.Type);
            var message = new NotifyNewGameSequenceWitnessed
            {
                PlayerId = Game.LocalPlayerId,
                Phase = Mapper.Map<Networking.Messages.Contracts.NetworkNewGameSequencePhase>(phase)
            };
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
            Send(message);
        }

        public void OnLevelingRespecCompleted()
        {
            ResetPlayersTracker(Game.PlayersInRespecWindow);
            var message = new NotifyLevelingRespecCompleted();
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
            Send(message);
        }

        public void OnLevelingRespecLevelUp()
        {
            RemovePlayerFromTracker(Game.PlayersInRespecWindow, Game.LocalPlayerId);

            var message = new NotifyLevelingRespecLevelUp
            {
                PlayerId = Game.LocalPlayerId
            };
            Send(message);
        }

        public void OnLevelingRespecMythicLevelUp()
        {
            RemovePlayerFromTracker(Game.PlayersInRespecWindow, Game.LocalPlayerId);

            var message = new NotifyLevelingRespecMythicLevelUp
            {
                PlayerId = Game.LocalPlayerId
            };
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
            Send(message);
        }

        public void OnLevelingTerminated()
        {
            Logger.LogInformation("Leveling has been terminated. UnitId={unitId}, Type={Type}", Game.Leveling.UnitId, Game.Leveling.Type);

            if (CanMakeLevelingDecisions())
            {
                var message = new NotifyLevelingTerminated();
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
                Send(message);
            }

            var messageKey = Game.Leveling.Type switch
            {
                NetworkLevelingType.MythicLeveling => WellKnownKeys.GameNotifications.Leveling.MythicLeveling.Completed.Key,
                NetworkLevelingType.Mercenary => WellKnownKeys.GameNotifications.Leveling.Mercenary.Completed.Key,
                NetworkLevelingType.NewGameSequence => null,
                NetworkLevelingType.Leveling or _ => WellKnownKeys.GameNotifications.Leveling.Completed.Key,
            };

            if (!string.IsNullOrEmpty(messageKey))
            {
                PlayerNotification.AddCombatText(messageKey, CombatTextSeverity.Common, new UnitEntityLog(Game.Leveling.UnitId));
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
            Send(message);
        }

        public void OnLockpickInteraction(NetworkLockpickInteraction lockpickInteraction)
        {
            var message = new NotifyMapObjectLockpicked
            {
                LockpickInteraction = Mapper.Map<Networking.Messages.Contracts.NetworkLockpickInteraction>(lockpickInteraction)
            };
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
            Send(message);
        }

        public void OnGlobalMapMessageBoxShown(bool shouldManuallyTriggerMessage)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapLocationMessage, Game.LocalPlayerId);

            var message = new NotifyGlobalMapLocationMessageShown
            {
                PlayerId = Game.LocalPlayerId,
                ShouldManuallyTriggerMessage = shouldManuallyTriggerMessage
            };
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

            if (Game.CurrentArea == null || !Game.CurrentArea.IsGlobalMap)
            {
                UpdateGlobalMapUIState();
                return;
            }

            var message = new NotifyGlobalMapTravelerModeChanged
            {
                PlayerId = Game.LocalPlayerId,
                TravelerMode = travelerMode.ToString(),
                MustBeEnforced = HasControlOverUI
            };
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
            Send(message);
        }

        public void OnTrapDisarmRolled(NetworkTrapDisarm trapDisarm)
        {
            var message = new NotifyTrapDisarmRolled
            {
                TrapDisarm = Mapper.Map<Networking.Messages.Contracts.NetworkTrapDisarm>(trapDisarm)
            };
            Send(message);
        }

        public void OnTrapActivation(string unitId, NetworkMapObject trapObject)
        {
            var message = new NotifyTrapActivated
            {
                UnitId = unitId,
                Trap = Mapper.Map<Networking.Messages.Contracts.NetworkMapObject>(trapObject)
            };
            Send(message);
        }

        public void OnUnitAutoUseAbilityChanged(NetworkAutoUseAbility networkAutoUseAbility)
        {
            if (!IsControlledByLocalPlayer(networkAutoUseAbility.UnitId))
            {
                return;
            }

            var message = new NotifyUnitAutoUseAbilityChanged
            {
                AutoUse = Mapper.Map<Networking.Messages.Contracts.NetworkAutoUseAbility>(networkAutoUseAbility)
            };
            Send(message);
        }

        public void OnUnitMoveTo(NetworkUnitMoveTo unitMoveTo)
        {
            if (!IsControlledByLocalPlayer(unitMoveTo.InitiatorUnitId))
            {
                return;
            }

            var message = new NotifyUnitMovedTo
            {
                Movement = Mapper.Map<Networking.Messages.Contracts.NetworkUnitMoveTo>(unitMoveTo)
            };
            Send(message);
        }

        public bool CanLeaveCombat()
        {
            var canLeave = Game.Combat == null || Game.Combat.UntargetableUnits.Count == 0 || Game.Combat.UntargetableUnits.All(GameInteraction.IsDeadOrMissing);
            return canLeave;
        }

        public void OnItemDescriptionRead(NetworkItem networkItem)
        {
            var message = new NotifyItemDescriptionRead
            {
                Item = Mapper.Map<Networking.Messages.Contracts.NetworkItem>(networkItem)
            };
            Send(message);
        }

        public void OnCopyInventoryItem(NetworkItemCopy itemCopy)
        {
            var message = new NotifyInventoryItemCopied
            {
                Copy = Mapper.Map<Networking.Messages.Contracts.NetworkItemCopy>(itemCopy)
            };
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
            Send(message);

            UpdateGlobalMapCrusadeArmyBuyLeaderUIState();
        }

        public void ForceUnpause()
        {
            lock (ActionLock)
            {
                Game.ForcedPause = null;
                GameInteraction.SetPause(false);
            }
        }

        public bool TogglePause(bool isPaused)
        {
            lock (ActionLock)
            {
                // unpaused => paused
                if (!isPaused)
                {
                    if (Game.ForcedPause == null)
                    {
                        EnsureForcePaused(NetworkForcedPauseReason.Manual, removalDelay: null);
                        Game.ForcedPause.ReadyPlayers.Add(Game.LocalPlayerId);

                        var pauseStarted = new NotifyGamePauseStarted
                        {
                            PlayerId = Game.LocalPlayerId,
                            Pause = Mapper.Map<Networking.Messages.Contracts.NetworkForcedPause>(Game.ForcedPause)
                        };
                        Send(pauseStarted);
                    }

                    return true;
                }

                // paused => unpaused
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
        }

        public void OnGlobalMapCrusadeArmyBuyLeaderClosed()
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, Game.LocalPlayerId);

            var message = new NotifyGlobalMapCrusadeArmyBuyLeaderClosed
            {
                PlayerId = Game.LocalPlayerId
            };
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
            Send(message);
        }

        public void OnGlobalMapRecruitmentSlotsRerolled()
        {
            UpdateGlobalMapRecruitmentUIState();
        }

        public void OnGlobalMapCrusadeArmyLeaderLevelingStarted(NetworkGlobalMapArmy globalMapArmy)
        {
            var message = new NotifyGlobalMapCrusadeArmyLeaderLevelingStarted
            {
                Army = Mapper.Map<Networking.Messages.Contracts.NetworkGlobalMapArmy>(globalMapArmy)
            };
            Send(message);
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
            ValueGenerator.ResetSeededGenerators(IdentifierLifetime.Combat, IdentifierLifetime.CombatTurn);
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

        public void OnEnterKingdom(NetworkKingdomEntryPoint kingdomEntryPoint)
        {
            var message = new NotifyKingdomEntered
            {
                EntryPoint = Mapper.Map<Networking.Messages.Contracts.NetworkKingdomEntryPoint>(kingdomEntryPoint)
            };
            Send(message);
        }

        public void OnExitKingdom()
        {
            var message = new NotifyKingdomExited();
            Send(message);
        }

        public void OnKingdomLoaded()
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapKingdom, Game.LocalPlayerId);
            var message = new NotifyKingdomLoaded
            {
                PlayerId = Game.LocalPlayerId
            };
            Send(message);
            UpdateGlobalMapKingdomUIState();
        }

        public void OnKingdomUnloaded()
        {
            var message = new NotifyKingdomUnloaded
            {
                PlayerId = Game.LocalPlayerId
            };
            Send(message);
            ResetPlayersTracker(Game.PlayersInGlobalMapKingdom);
            ResetPlayersTracker(Game.PlayersInSettlement);
            Game.PlayersInKingdomNavigationType.Clear();
        }

        public void OnKingdomNavigationChanged(KingdomNavigationType kingdomNavigationType)
        {
            var message = new NotifyKingdomNavigationChanged
            {
                Type = kingdomNavigationType.ToString(),
                PlayerId = Game.LocalPlayerId
            };
            Send(message);
        }

        public void OnKingdomSettlementLoaded()
        {
            AddPlayerToTracker(Game.PlayersInSettlement, Game.LocalPlayerId);
            var message = new NotifyKingdomSettlementLoaded
            {
                PlayerId = Game.LocalPlayerId
            };
            Send(message);
            UpdateSettlementUIState();
        }

        public void OnTransitionMapShown()
        {
            AddPlayerToTracker(Game.PlayersInTransitionMap, Game.LocalPlayerId);
            var message = new NotifyTransitionMapShown
            {
                PlayerId = Game.LocalPlayerId
            };
            Send(message);
            UpdateTransitionMapUIState();
        }

        protected abstract DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest);

        protected abstract void OnLocalPlayerTurnStart();

        protected abstract void Send(object message);

        protected abstract void Send(long playerId, object message);

        protected abstract bool OnToggleOffPause(out bool showReason);

        protected virtual void OnLocalPlayerTurnEnd()
        {
        }

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
            if (Game.ArmyCombat?.Turn == null
                || !Game.ArmyCombat.IsInitialized
                || !Game.ArmyCombat.PlayersNextTurnInitialization.TryGetValue(Game.ArmyCombat.Turn.Number, out var readyPlayers))
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
                GlobalMapInteraction.UpdateLocationMessageBoxUI(canUse, readyPlayers, totalPlayers);
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

        protected void UpdateGlobalMapKingdomUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapKingdom.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateKingdomUIState(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateSettlementUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInSettlement.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GlobalMapInteraction.UpdateSettlementUIState(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateTransitionMapUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInTransitionMap.Count;
                var totalPlayers = GetSyncedPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateTransitionMapUIState(canUse, readyPlayers, totalPlayers);
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
                Logger.LogInformation("Forced pause has been initialized. Reason={Reason}, RemovalDelay={RemovalDelay}", reason, removalDelay);
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
            Game.DialogState = null;

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
            ValueGenerator.ResetUniqueIdCounters(Game.Id);
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
            ValueGenerator.ResetSeededGenerators(IdentifierLifetime.Area, IdentifierLifetime.Combat, IdentifierLifetime.CombatTurn);

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

        protected void SetCombatStage(NetworkCombatStage combatStage)
        {
            var current = Game.Combat.Stage;
            Game.Combat.Stage = combatStage;
            Logger.LogInformation("Combat stage has been changed. From={From}, To={To}", current, combatStage);
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Combat.StageChanged.Key, CombatTextSeverity.Debug, current, combatStage);
        }

        protected void SetCombatTurnStage(NetworkCombatTurnStage combatTurnStage)
        {
            var current = Game.Combat.Turn.Stage;
            Game.Combat.Turn.Stage = combatTurnStage;
            Logger.LogInformation("Combat turn stage has been changed. From={From}, To={To}", current, combatTurnStage);
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

        protected async Task<bool> FixCombatUnitDiscrepancyAsync(NetworkCombatUnitDiscrepancy unitDiscrepancy)
        {
            var isFixed = await TryFixCombatUnitDiscrepancyAsync(unitDiscrepancy).ConfigureAwait(false);
            if (isFixed)
            {
                Game.Combat.PlayersCombatPreparation.TryRemove(Game.LocalPlayerId, out _);
            }

            return isFixed;
        }

        protected bool ShouldNotifyAboutAbility(string sourceUnitId)
        {
            if (Game.ArmyCombat?.Turn != null)
            {
                return HasControlOverUI && !Game.ArmyCombat.Turn.IsAI;
            }

            if (Game.Combat != null && Game.Combat.Turn == null)
            {
                Logger.LogWarning("Midfight action. UnitId={UnitId}", sourceUnitId);
                return false;
            }

            var shouldNotify = IsControlledByLocalPlayer(sourceUnitId);
            return shouldNotify;
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
            var players = GetPlayers();
            if (!playersReadinessTracker.TryGetValue(key, out var readyPlayers))
            {
                return players;
            }

            var notReady = players.Where(x => !readyPlayers.Contains(x.Id)).ToList();
            return notReady;
        }

        protected bool ConfirmReadiness(PlayerTurnReadinessType playerTurnReadinessType, long playerId, string unitId)
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

        protected void SetAreaSeed(int seed)
        {
            Game.CurrentArea.Seed = seed;
            Logger.LogInformation("Area seed has been set. Seed={Seed}", Game.CurrentArea.Seed);
        }

        protected NetworkPlayer GetPlayer(long playerId)
        {
            lock (ActionLock)
            {
                return Game?.Players.FirstOrDefault(p => p.Id == playerId);
            }
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
            PlayerNotification.AddCombatText(WellKnownKeys.GameNotifications.Session.CharacterOwnerChanged.Key, CombatTextSeverity.Common, networkCharacter.Owner.Name, new UnitEntityLog(networkCharacter.UnitId));

            if (Game.Combat?.Turn != null)
            {
                lock (ActionLock)
                {
                    if (Game.Combat?.Turn != null)
                    {
                        Game.Combat.Turn.IsLocalPlayer = IsControlledByLocalPlayer(Game.Combat.Turn.UnitId);
                    }
                }
            }
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
            var seed = ValueGenerator.GetRandom(IdentifierLifetime.Area, Guid.NewGuid().ToString()).Next(int.MinValue, int.MaxValue);
            return seed;
        }

        protected int GetSyncedPlayersCount()
        {
            return GetSyncedPlayers().Count;
        }

        protected List<NetworkPlayer> GetSyncedPlayers()
        {
            lock (ActionLock)
            {
                return [.. Game.Players.Where(x => x.LobbySyncStatus == NetworkLobbySyncStatus.Succeed)];
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

        protected async Task<bool> WaitWhileTrue(Func<bool> condition, string warningMessage, TimeSpan? awaiterTimeout = null)
        {
            if (condition())
            {
                Logger.LogWarning(warningMessage);
                using var timeout = new CancellationTokenSource(awaiterTimeout ?? TimeSpan.FromSeconds(30));
                while (condition())
                {
                    await Task.Delay(10);

                    if (timeout.IsCancellationRequested)
                    {
                        Logger.LogWarning($"Awaiter failed due to timeout. WarningText={warningMessage}");
                        return false;
                    }
                }
            }

            return true;
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
                .On<NotifyMetamagicSpellCreated>(OnNotifyMetamagicSpellCreated)
                .On<NotifyCustomSpellRemoved>(OnNotifyCustomSpellRemoved)

                // vendor interaction
                .On<NotifyVendorItemTransferred>(OnNotifyVendorItemTransferred)

                // rest
                .On<NotifyRestBanterInterrupted>(OnNotifyRestBanterInterrupted)
                .On<NotifyRestEnded>(OnNotifyRestEnded)
                .On<NotifyCapitalModeRestInitiated>(OnNotifyCapitalModeRestInitiated)

                // combat
                .On<NotifyUnitJoinedMidCombat>(OnNotifyUnitJoinedMidCombat)
                .On<NotifyUnitAttacked>(OnNotifyUnitAttacked)
                .On<NotifyCombatTurnDelayed>(OnNotifyCombatTurnDelayed)
                .On<NotifyCombatUnitKilled>(OnNotifyCombatUnitKilled)

                // global map & crusade combat
                .On<NotifyGlobalMapLocationMessageShown>(OnNotifyGlobalMapLocationMessageShown)
                .On<NotifyGlobalMapEncounterMessageShown>(OnNotifyGlobalMapEncounterMessageShown)
                .On<NotifyGlobalMapCombatResultsShown>(OnNotifyGlobalMapCombatResultsShown)
                .On<NotifyTacticalCombatTurnInitialized>(OnNotifyTacticalCombatTurnInitialized)
                .On<NotifyCrusadeArmyBattleResultsShown>(OnNotifyCrusadeArmyBattleResultsShown)
                .On<NotifyGlobalMapCrusadeArmyMergeCartShown>(OnNotifyGlobalMapCrusadeArmyInfoMergeShown)
                .On<NotifyGlobalMapCrusadeArmySetLeaderShown>(OnNotifyGlobalMapCrusadeArmySetLeaderShown)
                .On<NotifyGlobalMapCrusadeArmyBuyLeaderShown>(OnNotifyGlobalMapCrusadeArmyBuyLeaderShown)
                .On<NotifyGlobalMapCrusadeArmyLeaderLevelingShown>(OnNotifyGlobalMapCrusadeArmyLeaderLevelingShown)
                .On<NotifyGlobalMapCrusadeArmyLeaderLevelingStarted>(OnNotifyGlobalMapCrusadeArmyLeaderLevelingStarted)
                .On<NotifyKingdomEntered>(OnNotifyKingdomEntered)
                .On<NotifyKingdomExited>(OnNotifyKingdomExited)
                .On<NotifyKingdomLoaded>(OnNotifyKingdomLoaded)
                .On<NotifyKingdomUnloaded>(OnNotifyKingdomUnloaded)

                // kingdom
                .On<NotifyKingdomSettlementLoaded>(OnNotifyKingdomSettlementLoaded)

                // mapobjects
                .On<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)
                .On<NotifyTrapDisarmRolled>(OnNotifyTrapDisarmRolled)
                .On<NotifyTransitionMapShown>(OnNotifyTransitionMapShown)

                // items&inventory
                .On<NotifyLootableEntitySkinned>(OnNotifyContainerSkinned)
                .On<NotifyDropItem>(OnNotifyDropItem)
                .On<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .On<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)
                .On<NotifyZoneLootShown>(OnNotifyZoneLootShown)
                .On<NotifyZoneLootClosed>(OnNotifyZoneLootClosed)
                .On<NotifyInventoryItemTransferred>(OnNotifyInventoryItemTransferred)
                .On<NotifyInventoryItemUsed>(OnNotifyInventoryItemUsed)
                .On<NotifyInventoryItemCopied>(OnNotifyInventoryItemCopied)
                .On<NotifyItemDescriptionRead>(OnNotifyItemDescriptionRead)

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

                // movement
                .On<NotifyUnitMovedTo>(OnNotifyUnitMovedTo)

                // map objects
                .On<NotifyTrapActivated>(OnNotifyTrapActivated)
                ;
        }

        private void OnNotifyTrapActivated(long receivedFrom, NotifyTrapActivated message)
        {
            var trapObject = Mapper.Map<NetworkMapObject>(message.Trap);
            GameInteraction.ActivateTrap(message.UnitId, trapObject);
        }

        private void OnNotifyCustomSpellRemoved(long receivedFrom, NotifyCustomSpellRemoved message)
        {
            var ability = Mapper.Map<NetworkAbility>(message.Ability);
            GameInteraction.RemoveCustomSpell(message.UnitId, ability);
        }

        private void OnNotifyMetamagicSpellCreated(long receivedFrom, NotifyMetamagicSpellCreated message)
        {
            var metamagicSpell = Mapper.Map<NetworkMetamagicSpell>(message.MetamagicSpell);
            GameInteraction.CreateMetamagicSpell(metamagicSpell);
        }

        private void OnNotifyTransitionMapShown(long receivedFrom, NotifyTransitionMapShown message)
        {
            AddPlayerToTracker(Game.PlayersInTransitionMap, message.PlayerId);
            UpdateTransitionMapUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyKingdomSettlementLoaded(long receivedFrom, NotifyKingdomSettlementLoaded message)
        {
            AddPlayerToTracker(Game.PlayersInSettlement, message.PlayerId);
            UpdateSettlementUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyKingdomUnloaded(long receivedFrom, NotifyKingdomUnloaded message)
        {
            RemovePlayerFromTracker(Game.PlayersInGlobalMapKingdom, message.PlayerId);
            UpdateGlobalMapUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyKingdomLoaded(long receivedFrom, NotifyKingdomLoaded message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapKingdom, message.PlayerId);
            UpdateGlobalMapKingdomUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyKingdomExited(long receivedFrom, NotifyKingdomExited message)
        {
            ResetPlayersTracker(Game.PlayersInGlobalMapKingdom);
            ResetGlobalMapCounters();

            GlobalMapInteraction.ExitKingdom();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyKingdomEntered(long receivedFrom, NotifyKingdomEntered message)
        {
            var entryPoint = Mapper.Map<NetworkKingdomEntryPoint>(message.EntryPoint);
            ResetGlobalMapCounters();

            GlobalMapInteraction.EnterKingdom(entryPoint);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyUnitMovedTo(long receivedFrom, NotifyUnitMovedTo message)
        {
            var unitMoveTo = Mapper.Map<NetworkUnitMoveTo>(message.Movement);

            CombatInteraction.MoveUnit(unitMoveTo);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyItemDescriptionRead(long receivedFrom, NotifyItemDescriptionRead message)
        {
            var item = Mapper.Map<NetworkItem>(message.Item);
            GameInteraction.ReadItem(item);
        }

        private void OnNotifyInventoryItemCopied(long receivedFrom, NotifyInventoryItemCopied message)
        {
            var itemCopy = Mapper.Map<NetworkItemCopy>(message.Copy);
            GameInteraction.CopyInventoryItem(itemCopy);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyUnitAutoUseAbilityChanged(long receivedFrom, NotifyUnitAutoUseAbilityChanged message)
        {
            var autoUseAbility = Mapper.Map<NetworkAutoUseAbility>(message.AutoUse);
            GameInteraction.SetUnitAutoUseAbility(autoUseAbility);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGamePauseStarted(long receivedFrom, NotifyGamePauseStarted message)
        {
            var pause = Mapper.Map<NetworkForcedPause>(message.Pause);
            lock (ActionLock)
            {
                EnsureForcePaused(pause.Reason, pause.RemovalDelay);
            }
            GameInteraction.SetPause(true);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private async void OnNotifyTrapDisarmRolled(long receivedFrom, NotifyTrapDisarmRolled message)
        {
            OnAfterNetworkMessageHandled(receivedFrom, message);

            await WaitWhileTrue(() => GameInteraction.IsUnitBusy(message.TrapDisarm.UnitId), "Waiting for unit to finish actions before applying trap disarm roll");

            var trapDisarm = Mapper.Map<NetworkTrapDisarm>(message.TrapDisarm);
            GameInteraction.ApplyTrapDisarm(trapDisarm);
        }

        private async void OnNotifyCombatUnitKilled(long receivedFrom, NotifyCombatUnitKilled combatUnitKilled)
        {
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
            var delay = Task.Delay(TimeSpan.FromSeconds(settings.EnforcedCombatStartDelay));
            var startedLocally = WaitWhileTrue(() => Game.Combat == null, $"Waiting for combat to start or forcing it after {settings.EnforcedCombatStartDelay}");
            await Task.WhenAny(delay, startedLocally);

            var combatState = Mapper.Map<NetworkCombatState>(combatStarted.State);
            if (Game.Combat == null)
            {
                var hasBeenForcedToStart = await CombatInteraction.StartCombatAsync(combatState);
                if (hasBeenForcedToStart)
                {
                    GameInteraction.SetPause(false);
                    PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Combat.ForcedToStart.Key, args: player.Name);
                }

                Logger.LogWarning("Combat has been started by another player. Forced={Forced}", hasBeenForcedToStart);
            }
        }

        private void OnNotifyCapitalModeRestInitiated(long receivedFrom, NotifyCapitalModeRestInitiated message)
        {
            GameInteraction.InitiateRest();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }


        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingStarted(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingStarted message)
        {
            var army = Mapper.Map<NetworkGlobalMapArmy>(message.Army);
            GlobalMapInteraction.StartCrusadeArmyLeaderLeveling(army);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyLeaderLevelingShown(long receivedFrom, NotifyGlobalMapCrusadeArmyLeaderLevelingShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyLeaderLeveling, message.PlayerId);

            UpdateGlobalMapCrusadeArmyLeaderLevelingUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyGlobalMapCrusadeArmyBuyLeaderShown(long receivedFrom, NotifyGlobalMapCrusadeArmyBuyLeaderShown globalMapCrusadeArmyBuyLeaderShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyBuyLeader, globalMapCrusadeArmyBuyLeaderShown.PlayerId);

            UpdateGlobalMapCrusadeArmyBuyLeaderUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyBuyLeaderShown);
        }

        private void OnNotifyGlobalMapCrusadeArmySetLeaderShown(long receivedFrom, NotifyGlobalMapCrusadeArmySetLeaderShown globalMapCrusadeArmyInfoSetLeaderShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmySetLeader, globalMapCrusadeArmyInfoSetLeaderShown.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateOnSetLeader();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyInfoSetLeaderShown);
        }

        private void OnNotifyGlobalMapCrusadeArmyInfoMergeShown(long receivedFrom, NotifyGlobalMapCrusadeArmyMergeCartShown globalMapCrusadeArmyInfoMergeShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyInfoMerge, globalMapCrusadeArmyInfoMergeShown.PlayerId);
            UpdateGlobalMapCrusadeArmyInfoUIStateOnMerge();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCrusadeArmyInfoMergeShown);
        }

        private void OnNotifyGlobalMapCombatResultsShown(long receivedFrom, NotifyGlobalMapCombatResultsShown globalMapCombatResultsShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCombatResults, globalMapCombatResultsShown.PlayerId);

            UpdateGlobalMapCombatResultsUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapCombatResultsShown);
        }

        private void OnNotifyCrusadeArmyBattleResultsShown(long receivedFrom, NotifyCrusadeArmyBattleResultsShown crusadeArmyBattleResultsShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapCrusadeArmyBattleResults, crusadeArmyBattleResultsShown.PlayerId);

            UpdateGlobalMapCrusadeArmyBattleResultsUIState();

            OnAfterNetworkMessageHandled(receivedFrom, crusadeArmyBattleResultsShown);
        }

        private async void OnNotifyTacticalCombatTurnInitialized(long receivedFrom, NotifyTacticalCombatTurnInitialized message)
        {
            await WaitWhileTrue(() => Game.ArmyCombat == null || Game.ArmyCombat.Seed == 0 || !CombatInteraction.IsInCrusadeTacticalCombat(), "Crusade army combat has not been started yet");

            AddPlayerCrusadeArmyCombatTurnInitialization(message.TurnNumber, message.PlayerId);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyPlayerReadyStatusChanged(long receivedFrom, NotifyPlayerReadyStatusChanged readyStatusChanged)
        {
            UpdateReadyStatus(readyStatusChanged.PlayerId, readyStatusChanged.IsReady);

            OnAfterNetworkMessageHandled(receivedFrom, readyStatusChanged);
        }

        private async void OnNotifyCutsceneSkipped(long playerId, NotifyCutsceneSkipped cutsceneSkipped)
        {
            var isCutscene = await WaitWhileTrue(() => GameInteraction.CurrentGameMode != GameModeType.Cutscene, "Waiting for cutscene to be played before applying skip");
            if (!isCutscene)
            {
                return;
            }

            var player = GetPlayer(cutsceneSkipped.PlayerId);
            if (player != null)
            {
                GameInteraction.SkipCutscene(player.Name);
            }

            OnAfterNetworkMessageHandled(playerId, cutsceneSkipped);
        }

        private void OnNotifyPingedByPlayer(long receivedFrom, NotifyPingedByPlayer pingedAt)
        {
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
            AddPlayerToTracker(Game.Rest.PlayersFinishedRest, restEnded.PlayerId);

            UpdateRestResultsUIState();

            OnAfterNetworkMessageHandled(receivedFrom, restEnded);
        }

        private void OnNotifyGameModeTypeEnded(long receivedFrom, NotifyGameModeTypeEnded gameModeTypeEnded)
        {
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
            UpdateSaveInfo(gameForceLoaded.GameId, gameForceLoaded.Content);

            Game.LoadedSaveSeed = gameForceLoaded.Seed;

            LoadSavedGame();

            OnAfterNetworkMessageHandled(receivedFrom, gameForceLoaded);
        }

        private void OnNotifyNewGameSequenceWitnessed(long receivedFrom, NotifyNewGameSequenceWitnessed newGameSequenceWitnessed)
        {
            var phase = Mapper.Map<NetworkNewGameSequencePhase>(newGameSequenceWitnessed.Phase);
            WitnessNewGameSequencePhase(newGameSequenceWitnessed.PlayerId, phase.Type);

            OnAfterNetworkMessageHandled(receivedFrom, newGameSequenceWitnessed);
        }

        private void OnNotifyCharacterSelectionWindowShown(long receivedFrom, NotifyCharacterSelectionWindowShown characterSelectionWindowShown)
        {
            AddPlayerToTracker(Game.PlayersInCharacterSelectionWindow, characterSelectionWindowShown.PlayerId);

            UpdateCharacterSelectionUIState();

            OnAfterNetworkMessageHandled(receivedFrom, characterSelectionWindowShown);
        }

        private void OnNotifyLevelingRespecMythicLevelUp(long receivedFrom, NotifyLevelingRespecMythicLevelUp levelingRespecMythicLevelUp)
        {
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
            AddPlayerToTracker(Game.PlayersInRespecWindow, levelingRespecWindowShown.PlayerId);

            UpdateLevelingRespecUIState(levelingRespecWindowShown.UnitId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingRespecWindowShown);
        }

        private void OnNotifyLevelingRespecCompleted(long receivedFrom, NotifyLevelingRespecCompleted levelingRespecCompleted)
        {
            ResetPlayersTracker(Game.PlayersInRespecWindow);

            LevelingInteraction.CompleteLevelingRespec();

            OnAfterNetworkMessageHandled(receivedFrom, levelingRespecCompleted);
        }

        private void OnNotifyLevelingWarpaintColorAppearanceChanged(long receivedFrom, NotifyLevelingWarpaintColorAppearanceChanged levelingWarpaintColorAppearanceChanged)
        {
            var levelingWarpaint = Mapper.Map<NetworkLevelingWarpaint>(levelingWarpaintColorAppearanceChanged.Warpaint);
            LevelingInteraction.SelectLevelingWarpaintColorAppearance(levelingWarpaint);

            OnAfterNetworkMessageHandled(receivedFrom, levelingWarpaintColorAppearanceChanged);
        }

        private void OnNotifyLevelingWarpaintAppearanceChanged(long receivedFrom, NotifyLevelingWarpaintAppearanceChanged levelingWarpaintAppearanceChanged)
        {
            var levelingWarpaint = Mapper.Map<NetworkLevelingWarpaint>(levelingWarpaintAppearanceChanged.Warpaint);
            LevelingInteraction.SelectLevelingWarpaintAppearance(levelingWarpaint);

            OnAfterNetworkMessageHandled(receivedFrom, levelingWarpaintAppearanceChanged);
        }

        private void OnNotifyLevelingTattooColorAppearanceChanged(long receivedFrom, NotifyLevelingTattooColorAppearanceChanged levelingTattooColorAppearanceChanged)
        {
            var levelingTattoo = Mapper.Map<NetworkLevelingTattoo>(levelingTattooColorAppearanceChanged.Tattoo);
            LevelingInteraction.SelectLevelingTattooColorAppearance(levelingTattoo);

            OnAfterNetworkMessageHandled(receivedFrom, levelingTattooColorAppearanceChanged);
        }

        private void OnNotifyLevelingTattooAppearanceChanged(long receivedFrom, NotifyLevelingTattooAppearanceChanged levelingTattooAppearanceChanged)
        {
            var levelingTattoo = Mapper.Map<NetworkLevelingTattoo>(levelingTattooAppearanceChanged.Tattoo);
            LevelingInteraction.SelectLevelingTattooAppearance(levelingTattoo);

            OnAfterNetworkMessageHandled(receivedFrom, levelingTattooAppearanceChanged);
        }

        private void OnNotifyLevelingScarAppearanceChanged(long receivedFrom, NotifyLevelingScarAppearanceChanged levelingScarAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingScarAppearance(levelingScarAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingScarAppearanceChanged);
        }

        private void OnNotifyLevelingSecondaryOutfitColorAppearanceChanged(long receivedFrom, NotifyLevelingSecondaryOutfitColorAppearanceChanged levelingSecondaryOutfitColorAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingSecondaryOutfitColorAppearance(levelingSecondaryOutfitColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingSecondaryOutfitColorAppearanceChanged);
        }

        private void OnNotifyLevelingPrimaryOutfitColorAppearanceChanged(long receivedFrom, NotifyLevelingPrimaryOutfitColorAppearanceChanged levelingPrimaryOutfitColorAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingPrimaryOutfitColorAppearance(levelingPrimaryOutfitColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPrimaryOutfitColorAppearanceChanged);
        }

        private void OnNotifyLevelingHornsColorAppearanceChanged(long receivedFrom, NotifyLevelingHornsColorAppearanceChanged levelingHornsColorAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingHornsColorAppearance(levelingHornsColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHornsColorAppearanceChanged);
        }

        private void OnNotifyLevelingHornsAppearanceChanged(long receivedFrom, NotifyLevelingHornsAppearanceChanged levelingHornsAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingHornsAppearance(levelingHornsAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHornsAppearanceChanged);
        }

        private void OnNotifyLevelingHairStyleAppearanceChanged(long receivedFrom, NotifyLevelingHairStyleAppearanceChanged levelingHairStyleAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingHairStyleAppearance(levelingHairStyleAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHairStyleAppearanceChanged);
        }

        private void OnNotifyLevelingHairColorAppearanceChanged(long receivedFrom, NotifyLevelingHairColorAppearanceChanged levelingHairColorAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingHairColorAppearance(levelingHairColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingHairColorAppearanceChanged);
        }

        private void OnNotifyLevelingFaceAppearanceChanged(long receivedFrom, NotifyLevelingFaceAppearanceChanged levelingFaceAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingFaceAppearance(levelingFaceAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingFaceAppearanceChanged);
        }

        private void OnNotifyLevelingEyesColorAppearanceChanged(long receivedFrom, NotifyLevelingEyesColorAppearanceChanged levelingEyesColorAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingEyesColorAppearance(levelingEyesColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingEyesColorAppearanceChanged);
        }

        private void OnNotifyLevelingBodyColorAppearanceChanged(long receivedFrom, NotifyLevelingBodyColorAppearanceChanged levelingBodyColorAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingBodyColorAppearance(levelingBodyColorAppearanceChanged.TextureName);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBodyColorAppearanceChanged);
        }

        private void OnNotifyLevelingBodyTypeAppearanceChanged(long receivedFrom, NotifyLevelingBodyTypeAppearanceChanged levelingBodyTypeAppearanceChanged)
        {
            LevelingInteraction.SelectLevelingBodyTypeAppearance(levelingBodyTypeAppearanceChanged.Index);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBodyTypeAppearanceChanged);
        }

        private void OnNotifyDialogPopupShown(long receivedFrom, NotifyDialogPopupShown dialogPopupShown)
        {
            AddPlayerToTracker(Game.PlayersInDialogPopup, dialogPopupShown.PlayerId);

            UpdateDialogPopupState();

            OnAfterNetworkMessageHandled(receivedFrom, dialogPopupShown);
        }

        private void OnNotifyInventoryItemTransferred(long receivedFrom, NotifyInventoryItemTransferred itemTransferred)
        {
            var transferItem = Mapper.Map<NetworkItemsTransfer>(itemTransferred.TransferItem);
            GameInteraction.TransferInventoryItems(transferItem);

            OnAfterNetworkMessageHandled(receivedFrom, itemTransferred);
        }

        private void OnNotifyZoneLootClosed(long receivedFrom, NotifyZoneLootClosed zoneLootClosed)
        {
            RemovePlayerFromTracker(Game.PlayersInZoneLoot, zoneLootClosed.PlayerId);
            UpdateZoneLootUIState();

            OnAfterNetworkMessageHandled(receivedFrom, zoneLootClosed);
        }

        private void OnNotifyZoneLootShown(long receivedFrom, NotifyZoneLootShown zoneLootShown)
        {
            AddPlayerToTracker(Game.PlayersInZoneLoot, zoneLootShown.PlayerId);
            UpdateZoneLootUIState();

            OnAfterNetworkMessageHandled(receivedFrom, zoneLootShown);
        }

        private void OnNotifyGlobalMapEncounterMessageShown(long receivedFrom, NotifyGlobalMapEncounterMessageShown globalMapEncounterMessageShown)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapEncounterMessage, globalMapEncounterMessageShown.PlayerId);
            UpdateGlobalMapEncounterMessageUIState();

            OnAfterNetworkMessageHandled(receivedFrom, globalMapEncounterMessageShown);
        }

        private void OnNotifyGroupChangerOpened(long receivedFrom, NotifyGroupChangerOpened groupChangerVisible)
        {
            AddPlayerToTracker(Game.PlayersInGroupChanger, groupChangerVisible.PlayerId);
            UpdateGroupManagerUIState();

            OnAfterNetworkMessageHandled(receivedFrom, groupChangerVisible);
        }

        private void OnNotifyGlobalMapLocationMessageShown(long receivedFrom, NotifyGlobalMapLocationMessageShown message)
        {
            AddPlayerToTracker(Game.PlayersInGlobalMapLocationMessage, message.PlayerId);
            if (message.ShouldManuallyTriggerMessage)
            {
                GlobalMapInteraction.ShowCurrentEnterCurrentLocationMessage();
            }

            UpdateGlobalMapLocationMessageUIState();

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifySkipTimeOpened(long receivedFrom, NotifySkipTimeOpened skipTimeOpened)
        {
            AddPlayerToTracker(Game.PlayersInSkipTime, skipTimeOpened.PlayerId);
            GameInteraction.OpenSkipTimeUI();

            UpdateSkipTimeUIState();

            OnAfterNetworkMessageHandled(receivedFrom, skipTimeOpened);
        }

        private void OnNotifyUnitStealthChanged(long receivedFrom, NotifyUnitStealthChanged unitStealthChanged)
        {
            GameInteraction.ChangeUnitStealth(unitStealthChanged.UnitId, unitStealthChanged.IsEnabled, unitStealthChanged.IsForced);

            OnAfterNetworkMessageHandled(receivedFrom, unitStealthChanged);
        }

        private void OnNotifyCombatTurnDelayed(long receivedFrom, NotifyCombatTurnDelayed combatTurnDelayed)
        {
            CombatInteraction.DelayCombatTurn(combatTurnDelayed.UnitId, combatTurnDelayed.TargetUnitId);

            OnAfterNetworkMessageHandled(receivedFrom, combatTurnDelayed);
        }

        private void OnNotifyMapObjectLockpicked(long receivedFrom, NotifyMapObjectLockpicked mapObjectLockpicked)
        {
            var lockpickInteraction = Mapper.Map<NetworkLockpickInteraction>(mapObjectLockpicked.LockpickInteraction);

            GameInteraction.LockpickMapObject(lockpickInteraction);

            OnAfterNetworkMessageHandled(receivedFrom, mapObjectLockpicked);
        }

        private void OnNotifyActionBarSlotMoved(long receivedFrom, NotifyActionBarSlotMoved actionBarSlotMoved)
        {
            var sourceActionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotMoved.SourceActionBarSlot);
            var targetActionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotMoved.TargetActionBarSlot);

            GameInteraction.MoveActionBarSlots(sourceActionBarSlot, targetActionBarSlot);

            OnAfterNetworkMessageHandled(receivedFrom, actionBarSlotMoved);
        }

        private void OnNotifyActionBarSlotCleared(long receivedFrom, NotifyActionBarSlotCleared actionBarSlotCleared)
        {
            var actionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotCleared.ActionBarSlot);

            GameInteraction.ClearActionBarSlot(actionBarSlot);

            OnAfterNetworkMessageHandled(receivedFrom, actionBarSlotCleared);
        }

        private void OnNotifyGroundClicked(long receivedFrom, NotifyGroundClicked clicked)
        {
            if (Game.Combat == null)
            {
                Logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored outside of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);

            OnAfterNetworkMessageHandled(receivedFrom, clicked);
        }

        private void OnNotifyUnitClicked(long receivedFrom, NotifyUnitClicked clicked)
        {
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
            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);

            OnAfterNetworkMessageHandled(receivedFrom, clicked);
        }

        private void OnNotifyToggleActivatableAbility(long receivedFrom, NotifyToggleActivatableAbility activatableAbility)
        {
            var ability = Mapper.Map<NetworkActivatableAbility>(activatableAbility.Ability);
            GameInteraction.ToggleActivatableAbility(ability);

            OnAfterNetworkMessageHandled(receivedFrom, activatableAbility);
        }

        private void OnNotifyAbilityUsed(long receivedFrom, NotifyAbilityUsed message)
        {
            var ability = Mapper.Map<NetworkAbilityUse>(message.AbilityUse);
            CombatInteraction.UseAbility(ability);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyUnitAttacked(long receivedFrom, NotifyUnitAttacked unitAttacked)
        {
            var attack = Mapper.Map<NetworkUnitAttack>(unitAttacked.Attack);
            CombatInteraction.AttackUnit(attack);

            OnAfterNetworkMessageHandled(receivedFrom, unitAttacked);
        }

        private void OnNotifyActiveHandEquipmentSetChanged(long receivedFrom, NotifyActiveHandEquipmentSetChanged changed)
        {
            var set = Mapper.Map<NetworkActiveHandEquipmentSet>(changed.Set);
            GameInteraction.SetActiveHandEquipmentSet(set);

            OnAfterNetworkMessageHandled(receivedFrom, changed);
        }

        private void OnNotifyEquipmentSlotChanged(long receivedFrom, NotifyEquipmentSlotChanged slotChanged)
        {
            var slot = Mapper.Map<NetworkEquipmentSlot>(slotChanged.Slot);
            GameInteraction.UpdateEquipmentSlot(slot);

            OnAfterNetworkMessageHandled(receivedFrom, slotChanged);
        }

        private void OnNotifyDropItem(long receivedFrom, NotifyDropItem item)
        {
            var dropItem = Mapper.Map<NetworkDropItem>(item.Drop);
            GameInteraction.DropItem(dropItem);

            OnAfterNetworkMessageHandled(receivedFrom, item);
        }

        private void OnNotifyInventoryItemUsed(long receivedFrom, NotifyInventoryItemUsed inventoryItemUsed)
        {
            var useItem = Mapper.Map<NetworkUseInventoryItem>(inventoryItemUsed.UseItem);
            GameInteraction.UseInventoryItem(useItem);

            OnAfterNetworkMessageHandled(receivedFrom, inventoryItemUsed);
        }

        private void OnNotifyContainerSkinned(long receivedFrom, NotifyLootableEntitySkinned notifyLootableEntitySkinned)
        {
            var container = Mapper.Map<NetworkLootableEntity>(notifyLootableEntitySkinned.Entity);
            GameInteraction.SkinLootContainer(container);

            OnAfterNetworkMessageHandled(receivedFrom, notifyLootableEntitySkinned);
        }

        private void OnNotifyOvertipInteracted(long receivedFrom, NotifyOvertipInteracted interacted)
        {
            var overtip = Mapper.Map<NetworkOvertip>(interacted.Overtip);
            GameInteraction.InteractWithOvertip(overtip);

            OnAfterNetworkMessageHandled(receivedFrom, interacted);
        }

        private void OnNotifyUnitJoinedMidCombat(long receivedFrom, NotifyUnitJoinedMidCombat combat)
        {
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, combat.PlayerId, combat.UnitId);

            OnAfterNetworkMessageHandled(receivedFrom, combat);
        }

        private void OnNotifyRestBanterInterrupted(long receivedFrom, NotifyRestBanterInterrupted interrupted)
        {
            var banter = Mapper.Map<NetworkRestBanter>(interrupted.Banter);
            GameInteraction.TryInterruptRestBanter(banter);

            OnAfterNetworkMessageHandled(receivedFrom, interrupted);
        }

        private void OnNotifyVendorItemTransferred(long receivedFrom, NotifyVendorItemTransferred message)
        {
            var transfer = Mapper.Map<NetworkVendorItemTransfer>(message.ItemTransfer);
            GameInteraction.TransferVendorItem(transfer);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifySpellForgotten(long receivedFrom, NotifySpellForgotten message)
        {
            var slot = Mapper.Map<NetworkSpellSlot>(message.Slot);
            var ability = Mapper.Map<NetworkAbility>(message.Ability);

            GameInteraction.ForgetSpell(message.UnitId, slot, ability);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifySpellMemorized(long receivedFrom, NotifySpellMemorized message)
        {
            var slot = Mapper.Map<NetworkSpellSlot>(message.Slot);
            var ability = Mapper.Map<NetworkAbility>(message.Ability);

            GameInteraction.MemorizeSpell(message.UnitId, slot, ability);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }

        private void OnNotifyLevelingPortraitSelected(long receivedFrom, NotifyLevelingPortraitSelected levelingPortraitSelected)
        {
            var levelingPortrait = Mapper.Map<NetworkLevelingPortrait>(levelingPortraitSelected.Portrait);
            LevelingInteraction.SelectLevelingPortrait(levelingPortrait);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPortraitSelected);
        }

        private void OnNotifyLevelingVoiceSelected(long receivedFrom, NotifyLevelingVoiceSelected levelingVoiceSelected)
        {
            var levelingVoice = Mapper.Map<NetworkLevelingVoice>(levelingVoiceSelected.Voice);
            LevelingInteraction.SelectLevelingVoice(levelingVoice);

            OnAfterNetworkMessageHandled(receivedFrom, levelingVoiceSelected);
        }

        private void OnNotifyLevelingAlignmentSelected(long receivedFrom, NotifyLevelingAlignmentSelected levelingAlignmentSelected)
        {
            LevelingInteraction.SelectLevelingAlignment(levelingAlignmentSelected.AlignmentId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingAlignmentSelected);
        }

        private void OnNotifyLevelingNameChanged(long receivedFrom, NotifyLevelingNameChanged levelingNameChanged)
        {
            LevelingInteraction.SetLevelingName(levelingNameChanged.Name);

            OnAfterNetworkMessageHandled(receivedFrom, levelingNameChanged);
        }

        private void OnNotifyLevelingGenderSelected(long receivedFrom, NotifyLevelingGenderSelected levelingGenderSelected)
        {
            LevelingInteraction.SelectLevelingGender(levelingGenderSelected.GenderId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingGenderSelected);
        }

        private void OnNotifyLevelingRaceSelected(long receivedFrom, NotifyLevelingRaceSelected levelingRaceSelected)
        {
            LevelingInteraction.SelectLevelingRace(levelingRaceSelected.RaceId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingRaceSelected);
        }

        private void OnNotifyLevelingRacialAbilityScoreBonusChanged(long receivedFrom, NotifyLevelingRacialAbilityScoreBonusChanged racialAbilityScoreBonusChanged)
        {
            var direction = Mapper.Map<NetworkLevelingSequenceDirection>(racialAbilityScoreBonusChanged.Direction);

            LevelingInteraction.ChangeLevelingRacialAbilityScoreBonus(direction);

            OnAfterNetworkMessageHandled(receivedFrom, racialAbilityScoreBonusChanged);
        }

        private void OnNotifyLevelingBirthDayChanged(long receivedFrom, NotifyLevelingBirthDayChanged levelingBirthDayChanged)
        {
            var direction = Mapper.Map<NetworkLevelingSequenceDirection>(levelingBirthDayChanged.Direction);

            LevelingInteraction.ChangeLevelingBirthDay(direction);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBirthDayChanged);
        }

        private void OnNotifyLevelingBirthMonthChanged(long receivedFrom, NotifyLevelingBirthMonthChanged levelingBirthMonthChanged)
        {
            var direction = Mapper.Map<NetworkLevelingSequenceDirection>(levelingBirthMonthChanged.Direction);

            LevelingInteraction.ChangeLevelingBirthMonth(direction);

            OnAfterNetworkMessageHandled(receivedFrom, levelingBirthMonthChanged);
        }

        private void OnNotifyLevelingAbilityScoreDecreased(long receivedFrom, NotifyLevelingAbilityScoreDecreased levelingAbilityScoreDecreased)
        {
            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(levelingAbilityScoreDecreased.AbilityScore);
            LevelingInteraction.DecreaseLevelingAbilityScore(abilityScore);

            OnAfterNetworkMessageHandled(receivedFrom, levelingAbilityScoreDecreased);
        }

        private void OnNotifyLevelingAbilityScoreIncreased(long receivedFrom, NotifyLevelingAbilityScoreIncreased levelingAbilityScoreIncreased)
        {
            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(levelingAbilityScoreIncreased.AbilityScore);
            LevelingInteraction.IncreaseLevelingAbilityScore(abilityScore);

            OnAfterNetworkMessageHandled(receivedFrom, levelingAbilityScoreIncreased);
        }

        private void OnNotifyLevelingCompleted(long receivedFrom, NotifyLevelingCompleted completed)
        {
            LevelingInteraction.CompleteLeveling();

            OnAfterNetworkMessageHandled(receivedFrom, completed);
        }

        private void OnNotifyLevelingTerminated(long receivedFrom, NotifyLevelingTerminated terminated)
        {
            LevelingInteraction.TerminateLeveling();

            OnAfterNetworkMessageHandled(receivedFrom, terminated);
        }

        private void OnNotifyLevelingSpellRemoved(long receivedFrom, NotifyLevelingSpellRemoved removed)
        {
            var spell = Mapper.Map<NetworkLevelingSpell>(removed.Spell);
            LevelingInteraction.RemoveLevelingSpell(spell);

            OnAfterNetworkMessageHandled(receivedFrom, removed);
        }

        private void OnNotifyLevelingSpellChosen(long receivedFrom, NotifyLevelingSpellChosen chosen)
        {
            var spell = Mapper.Map<NetworkLevelingSpell>(chosen.Spell);
            LevelingInteraction.SelectLevelingSpell(spell);

            OnAfterNetworkMessageHandled(receivedFrom, chosen);
        }

        private void OnNotifyLevelingFeatureSelected(long receivedFrom, NotifyLevelingFeatureSelected selected)
        {
            var feature = Mapper.Map<NetworkLevelingFeature>(selected.Feature);
            LevelingInteraction.SelectLevelingFeature(feature);

            OnAfterNetworkMessageHandled(receivedFrom, selected);
        }

        private void OnNotifyLevelingSkillPointDecreased(long receivedFrom, NotifyLevelingSkillPointDecreased decreased)
        {
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(decreased.Skill);
            LevelingInteraction.DecreaseLevelingSkillPoint(skillPoint);
            OnAfterNetworkMessageHandled(receivedFrom, decreased);
        }

        private void OnNotifyLevelingSkillPointIncreased(long receivedFrom, NotifyLevelingSkillPointIncreased increased)
        {
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(increased.Skill);
            LevelingInteraction.IncreaseLevelingSkillPoint(skillPoint);

            OnAfterNetworkMessageHandled(receivedFrom, increased);
        }

        private void OnNotifyLevelingPhaseChanged(long receivedFrom, NotifyLevelingPhaseChanged levelingPhaseChanged)
        {
            var phase = Mapper.Map<NetworkLevelingPhase>(levelingPhaseChanged.Phase);
            ResetPlayersTracker(Game.Leveling.PlayerReadiness);
            LevelingInteraction.SwitchLevelingPhase(phase);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPhaseChanged);
        }

        private async void OnNotifyLevelingPhaseWitnessed(long receivedFrom, NotifyLevelingPhaseWitnessed levelingPhaseWitnessed)
        {
            // leveling is always created at the host first and later on on clients as a part of leveling confirmation
            // but not in case when game is forcing leveling ui to open for everyone at the same time => means there is some racing there
            await WaitWhileTrue(() => Game.Leveling == null, "Received leveling witness notification, but leveling has not been started yet.");

            WitnessLevelingPhase(levelingPhaseWitnessed.PlayerId);

            OnAfterNetworkMessageHandled(receivedFrom, levelingPhaseWitnessed);
        }

        private void OnNotifyLevelingClassArchetypeSelected(long receivedFrom, NotifyLevelingClassArchetypeSelected message)
        {
            var archetype = Mapper.Map<NetworkLevelingArchetype>(message.Archetype);
            LevelingInteraction.SelectLevelingClassArchetype(archetype);

            OnAfterNetworkMessageHandled(receivedFrom, message);
        }


        private void OnNotifyLevelingMythicClassSelected(long receivedFrom, NotifyLevelingMythicClassSelected mythicClassSelected)
        {
            LevelingInteraction.SelectMythicLevelingClass(mythicClassSelected.MythicClassId);

            OnAfterNetworkMessageHandled(receivedFrom, mythicClassSelected);
        }

        private void OnNotifyLevelingClassSelected(long receivedFrom, NotifyLevelingClassSelected message)
        {
            var levelingClass = Mapper.Map<NetworkLevelingClass>(message.Class);
            LevelingInteraction.SelectLevelingClass(levelingClass);

            OnAfterNetworkMessageHandled(receivedFrom, message);
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

        private async Task<bool> TryFixCombatUnitDiscrepancyAsync(NetworkCombatUnitDiscrepancy combatUnitDiscrepancy)
        {
            var localPlayerDiscrepancy = combatUnitDiscrepancy.Units.Where(x => x.Key != Game.LocalPlayerId).SelectMany(x => x.Value).ToList();
            if (localPlayerDiscrepancy.Count == 0)
            {
                Logger.LogInformation("Local player doesn't require any combat unit discrepancy fixes");
                return true;
            }

            Logger.LogInformation("Discrepant units will be added to combat. Units={Units}", localPlayerDiscrepancy.Select(x => x.Id));
            var result = await CombatInteraction.EnsureUnitsInCombatAsync(localPlayerDiscrepancy).ConfigureAwait(false);
            if (!result)
            {
                Logger.LogError("Combat unit discrepancy has not been fixed");
                PlayerNotification.ShowWarningNotification(WellKnownKeys.GameNotifications.Combat.DesyncedCombatUnits.Key);
            }

            return result;
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
                    Stage = NetworkCombatTurnStage.Initialization,
                    IsActingInSurpriseRound = actingInSurpriseRound,
                    IsLocalPlayer = IsControlledByLocalPlayer(unitId),
                    IsAI = !GameInteraction.IsUnitInParty(unitId),
                };
            }

            Logger.LogInformation("Turn has been initialized. UnitId={UnitId}, IsLocalPlayer={IsLocalPlayer}, IsAI={IsAI}, IsActingInSurpriseRound={IsActingInSurpriseRound}, Stage={Stage}",
                unitId, Game.Combat.Turn.IsLocalPlayer, Game.Combat.Turn.IsAI, Game.Combat.Turn.IsActingInSurpriseRound, Game.Combat.Turn.Stage);

            OnLocalPlayerTurnStart();
        }

        private bool WasControlledByCurrentPlayer(string unitId)
        {
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

        private bool IsOutOfSupportedArea()
        {
            var isOutOfSupport = Game.CurrentArea.Chapter switch
            {
                <= 3 => false,
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

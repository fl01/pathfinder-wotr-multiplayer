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
using UniRx;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Content;
using WOTRMultiplayer.MP.Entities.Dialogs;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;

namespace WOTRMultiplayer.MP.Actors
{
    public abstract class MultiplayerActorBase
    {
        private readonly object _actionLock = new();

        public bool IsInCombat => Game?.Combat != null;

        public int SessionSeed => Game.SessionSeed;

        public int? CombatSeed => Game.Combat?.Seed;

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        internal NetworkGame Game { get; set; }

        protected ILogger Logger { get; private set; }

        protected IMapper Mapper { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

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
            IDiceRollStorage diceRollStorage,
            IFileSystemService fileSystemService,
            IValueGenerator valueGenerator,
            INetworkReceiver networkReceiver)
        {
            Logger = logger;
            Mapper = mapper;
            GameInteraction = gameInteractionService;
            DiceRollStorage = diceRollStorage;
            FileSystem = fileSystemService;
            SettingsService = multiplayerSettingsService;
            _valueGenerator = valueGenerator;
            _networkReceiver = networkReceiver;
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
            if (!ShouldNotifyAboutAbilityUse(ability.CasterId))
            {
                return;
            }

            Logger.LogInformation("Sending ability use. CasterId={CasterId}, TargetId={TargetId}, TargetPoint={TargetPoint}, AbilityId={AbilityId}, SpellbookId={SpellbookId}, VectorPathCount={VectorPathCount}",
              ability.CasterId, ability.TargetId, ability.TargetPoint, ability.Id, ability.SpellbookId, ability.VectorPath?.Count);

            var message = new NotifyAbilityUsed
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkAbility>(ability)
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

            var isLocal = IsControlledByLocalPlayer(networkUnitAttack.ExecutorUnitId);
            if (!isLocal || (Game.Combat.Turn?.IsAI ?? false))
            {
                Logger.LogInformation("Skipping UnitAttack notification as executor is not controlled by local player. UnitId={UnitId}", networkUnitAttack.ExecutorUnitId);
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
            if (!ShouldNotifyAboutAbilityUse(activatableAbilityUse.CasterId))
            {
                return;
            }

            var message = new NotifyToggleActivatableAbility
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkActivatableAbility>(activatableAbilityUse)
            };
            Logger.LogInformation("Sending {MessageType}. CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}, IsActive={IsActive}", nameof(NotifyToggleActivatableAbility), message.Ability.CasterId, message.Ability.TargetId, message.Ability.Id, message.Ability.IsActive);

            Send(message);
        }

        public TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string unitId)
            where TRollValue : RollValueBase
        {
            Logger.LogInformation("Retrieving roll over network. RollId={RollId}, UnitId={UnitId}", networkDiceRollId, unitId);

            var waitForRollTimeout = SettingsService.GetSettings().RemoteRollRetrievalTimeout;
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

            Logger.LogInformation("Sending {MessageType}. TargetUnitId={TargetUnitId}", nameof(NotifyUnitClicked), click.TargetUnitId);

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

            Logger.LogInformation("Sending ground click. WorldPosition={WorldPosition}, SelectedUnits={SelectedUnits}", click.WorldPosition, click.SelectedUnits);
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
            Logger.LogInformation("Sending drop item. OwnerId={OwnerId}, ItemId={ItemId}, ItemName={ItemName}", dropItem.OwnerEntityId, dropItem.Item.UniqueId, dropItem.Item.Name);
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

            Logger.LogWarning("Sending changed equipment slot. SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}, OwnerId={OwnerId}", equipmentSlot.Position.Type, equipmentSlot.Position.Index, equipmentSlot.Item?.UniqueId, equipmentSlot.OwnerId);
            var message = new NotifyEquipmentSlotChanged
            {
                Slot = Mapper.Map<Networking.Messages.Contracts.NetworkEquipmentSlot>(equipmentSlot)
            };

            Send(message);
        }

        public void OnInteractWithMapObjectOvertip(NetworkOvertip networkOvertip)
        {
            Logger.LogInformation("Sending overtip interaction. MapObjectId={MapObjectId}, Units={Units}", networkOvertip.MapObject.Id, networkOvertip.Units);
            var message = new NotifyOvertipInteracted
            {
                Overtip = Mapper.Map<Networking.Messages.Contracts.NetworkOvertip>(networkOvertip)
            };

            Send(message);
        }

        public void OnAreaScenesLoaded()
        {
            Logger.LogInformation("Area loaded");

            Game.Stage = NetworkGameStage.Playing;

            SoftReset();

            UpdateCharactersOwnership();

            lock (ActionLock)
            {
                EnsureForcePaused(WellKnownKeys.GameNotifications.ForcedPause.AreaLoading.Key);
                var localPlayerId = GetLocalPlayerId();
                Game.ForcedPause.ReadyPlayers.Add(localPlayerId);
                GameInteraction.SetPause(true);
            }
        }

        /// <summary>
        /// Reloads current party characters and tries to merge ownership
        /// </summary>
        public void UpdateCharactersOwnership()
        {
            Logger.LogInformation("Updating current characters & merging ownership");

            // could be synced from host, but state is the same anyway
            var partyCharacters = GameInteraction.GetPartyPlayers();
            if (partyCharacters.Count == 0)
            {
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

        public void ForceLoadGame(string savePath, string gameId)
        {
            if (!string.IsNullOrEmpty(Game.SaveFilePath))
            {
                return;
            }

            Game.SaveFilePath = savePath;
            Game.Id = gameId;

            Game.ForcedPause = null;
            ResetGameIdGenerator();

            Logger.LogInformation("Notifying other players to force load save game. GameId={GameId}, Path={Path}", Game.Id, savePath);
            var message = new NotifyLobbySaveGameChanged
            {
                GameId = Game.Id,
                Content = FileSystem.GetRawFileContent(savePath),
                IsForceLoad = true
            };

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
        }

        public void CombatEnded()
        {
            Logger.LogInformation("Combat ended");
            if (Game.Combat == null)
            {
                Logger.LogWarning("Combat has not been started correctly");
            }

            Game.Combat = null;
            _valueGenerator.ResetSeedGenerators(Random.SeedLifetime.Combat);
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
                    Logger.LogError("Invalid unit turn start detected. ExpectedUnitId={ExpectedUnitId}, ActualUnitId={ActualUnitId}", Game.Combat.Turn.UnitId, unitId);
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
                var message = new NotifyPlayerCombatTurnEnded { UnitId = Game.Combat.Turn.UnitId };
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

                var localPlayerId = GetLocalPlayerId();
                var isFirstJoinEvent = !IsPlayerReady(PlayerTurnReadinessType.UnitJoinedMidCombat, localPlayerId, unitId);
                if (isFirstJoinEvent)
                {
                    Logger.LogInformation("Sending {MessageType}. UnitId={UnitId}", nameof(NotifyUnitJoinedMidCombat), unitId);
                    var message = new NotifyUnitJoinedMidCombat { UnitId = unitId, PlayerId = localPlayerId };
                    Send(message);
                }

                AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, localPlayerId, unitId);

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to process unit join combat request. UnitId={UnitId}", unitId);
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

        public void OnLevelingClassSelected(string classId)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingClassSelected
            {
                ClassId = classId
            };
            Logger.LogInformation("Sending {MessageType}. ClassId={ClassId}", nameof(NotifyLevelingClassSelected), classId);
            Send(message);
        }

        public void OnLevelingClassArchetypeSelected(string archetypeId)
        {
            if (!CanMakeLevelingDecisions())
            {
                return;
            }

            var message = new NotifyLevelingClassArchetypeSelected
            {
                ArchetypeId = archetypeId
            };
            Logger.LogInformation("Sending {MessageType}. ArchetypeId={ArchetypeId}", nameof(NotifyLevelingClassArchetypeSelected), archetypeId);
            Send(message);
        }

        public bool CanMakeLevelingDecisions()
        {
            return Game.Leveling != null && IsControlledByLocalPlayer(Game.Leveling.UnitId) && Game.Leveling.PlayerReadiness.Count >= GetPlayersCount();
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
                Game.Leveling.PlayerReadiness.Clear();
                Logger.LogInformation("Sending {MessageType}. Index={Index}", nameof(NotifyLevelingPhaseChanged), phaseChangedMessage.Phase.Index);
            }

            var localPlayer = GetLocalPlayerId();
            WitnessLevelingPhase(localPlayer);
            var message = new NotifyLevelingPhaseWitnessed
            {
                PlayerId = localPlayer,
                Phase = Mapper.Map<Networking.Messages.Contracts.NetworkLevelingPhase>(phase)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, Index={Index}", nameof(NotifyLevelingPhaseWitnessed), message.PlayerId, message.Phase.Index);
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
            Logger.LogInformation("Sending {MessageType}. FeatureName={FeatureName}, Id={Id}", nameof(NetworkLevelingFeature), message.Feature.Name, message.Feature.Id);
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
            Logger.LogInformation("Leveling has been terminated. UnitId={unitId}", Game.Leveling.UnitId);

            if (CanMakeLevelingDecisions())
            {
                var message = new NotifyLevelingTerminated();
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLevelingTerminated));
                Send(message);
            }

            var character = GetCharacterOwnership(Game.Leveling.UnitId);
            var messageKey = Game.Leveling.Type switch
            {
                NetworkLevelingType.MythicLeveling => WellKnownKeys.GameNotifications.Leveling.MythicLeveling.Terminated.Key,
                NetworkLevelingType.Leveling or _ => WellKnownKeys.GameNotifications.Leveling.Terminated.Key
            };
            GameInteraction.ShowWarningNotification(messageKey, character?.Name);
            Game.Leveling = null;
        }

        public void OnLevelingCompleted()
        {
            Logger.LogInformation("Leveling has been completed. UnitId={UnitId}", Game.Leveling.UnitId);

            if (CanMakeLevelingDecisions())
            {
                var message = new NotifyLevelingCompleted();
                Logger.LogInformation("Sending {MessageType}", nameof(NotifyLevelingCompleted));
                Send(message);
            }

            var character = GetCharacterOwnership(Game.Leveling.UnitId);
            var messageKey = Game.Leveling.Type switch
            {
                NetworkLevelingType.MythicLeveling => WellKnownKeys.GameNotifications.Leveling.MythicLeveling.Completed.Key,
                NetworkLevelingType.Leveling or _ => WellKnownKeys.GameNotifications.Leveling.Completed.Key
            };
            GameInteraction.AddCombatText(messageKey, character?.Name);
            Game.Leveling = null;
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

        public void OnGlobalMapMessageBoxClosed()
        {
            var localPlayer = GetLocalPlayerId();
            RemovePlayerFromTracker(Game.PlayersInGlobalMapLocationMessage, localPlayer);

            var message = new NotifyGlobalMapMessageBoxClosed
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapMessageBoxClosed), message.PlayerId);
            Send(message);

            UpdateGlobalMapMessageBoxUIState();
        }

        public void OnGlobalMapMessageBoxShown()
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInGlobalMapLocationMessage, localPlayer);

            var message = new NotifyGlobalMapMessageBoxShown
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapMessageBoxShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapMessageBoxUIState();
        }

        public void OnShowGroupChangerUI()
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInGroupChanger, localPlayer);

            var message = new NotifyGroupChangerOpened
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGroupChangerOpened), message.PlayerId);
            Send(message);

            UpdateGroupManagerUIState();
        }

        public void OnSkipTimeOpened()
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInSkipTime, localPlayer);

            var message = new NotifySkipTimeOpened
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifySkipTimeOpened), message.PlayerId);
            Send(message);

            UpdateSkipTimeUIState();
        }

        public void OnDialogPopupShown(NetworkDialogPopup networkDialogPopup)
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInDialogPopup, localPlayer);

            var message = new NotifyDialogPopupShown
            {
                PlayerId = localPlayer,
                Popup = Mapper.Map<Networking.Messages.Contracts.NetworkDialogPopup>(networkDialogPopup)
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}, AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", nameof(NotifyDialogPopupShown), message.PlayerId, message.Popup.AreaName, message.Popup.DialogName, message.Popup.CueName);
            Send(message);

            UpdateDialogPopupState();
        }

        public void OnGlobalMapIngredientCollectionShown()
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInGlobalMapIngredientCollection, localPlayer);

            var message = new NotifyGlobalMapIngredientCollectionShown
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapIngredientCollectionShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapIngredientCollectionUIState();
        }

        public void OnGlobalMapIngredientCollectionClosed()
        {
            var localPlayer = GetLocalPlayerId();
            RemovePlayerFromTracker(Game.PlayersInGlobalMapIngredientCollection, localPlayer);

            var message = new NotifyGlobalMapIngredientCollectionClosed
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapIngredientCollectionClosed), message.PlayerId);
            Send(message);

            UpdateGlobalMapIngredientCollectionUIState();
        }

        public void OnGlobalMapEncounterMessageShown()
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInGlobalMapEncounterMessage, localPlayer);

            var message = new NotifyGlobalMapEncounterMessageShown
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapEncounterMessageShown), message.PlayerId);
            Send(message);

            UpdateGlobalMapEncounterMessageUIState();
        }

        public void OnZoneLootCollectorButtonsUpdated()
        {
            UpdateZoneLootUIState();
        }

        public void OnZoneLootShown()
        {
            var localPlayer = GetLocalPlayerId();
            AddPlayerToTracker(Game.PlayersInZoneLoot, localPlayer);

            var message = new NotifyZoneLootShown
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootShown), message.PlayerId);
            Send(message);

            UpdateZoneLootUIState();
        }

        public void OnZoneLootClosed()
        {
            var localPlayer = GetLocalPlayerId();
            RemovePlayerFromTracker(Game.PlayersInZoneLoot, localPlayer);

            var message = new NotifyZoneLootClosed
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootClosed), message.PlayerId);
            Send(message);
        }

        public void OnZoneLootCompleted()
        {
            var localPlayer = GetLocalPlayerId();
            var message = new NotifyZoneLootCompleted
            {
                PlayerId = localPlayer
            };
            Logger.LogInformation("Sending {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootCompleted), message.PlayerId);
            Send(message);
        }

        public bool ReadyChanged()
        {
            lock (ActionLock)
            {
                var player = Game.Players.First(p => p.Id == Game.LocalPlayerId);
                player.IsReady = !player.IsReady;

                OnPlayersChanged?.Invoke(Game.Players);

                var readyChanged = new PlayerReadyStatusChanged { PlayerId = player.Id, IsReady = player.IsReady };
                Send(readyChanged);

                return player.IsReady;
            }
        }

        protected abstract bool OnStartGameModeInternal(GameModeType type);

        protected abstract DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest);

        protected abstract void OnLocalPlayerTurnStart();

        protected abstract void Send(object message);

        protected abstract void Send(long playerId, object message);

        protected void UpdateGroupManagerUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGroupChanger.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateGroupChangerUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateDialogPopupState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInDialogPopup.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateDialogPopupUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateSkipTimeUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInSkipTime.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateSkipTimeUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapMessageBoxUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapLocationMessage.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateGlobalMapMessageBoxUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapIngredientCollectionUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapIngredientCollection.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateGlobalMapIngredientCollectionUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateGlobalMapEncounterMessageUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInGlobalMapEncounterMessage.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateGlobalMapEncounterMessageUI(canUse, readyPlayers, totalPlayers);
            }
        }

        protected void UpdateZoneLootUIState()
        {
            lock (ActionLock)
            {
                var readyPlayers = Game.PlayersInZoneLoot.Count;
                var totalPlayers = GetPlayersCount();
                var canUse = HasControlOverUI && readyPlayers >= totalPlayers;
                GameInteraction.UpdateZoneLootUI(canUse, readyPlayers, totalPlayers);
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

        protected void EnsureForcePaused(string reason, TimeSpan? removalDelay)
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

        protected void EnsureForcePaused(string reason)
        {
            EnsureForcePaused(reason, SettingsService.GetSettings().ForcedPauseDefaultTerminationDelay);
        }

        protected void WitnessLevelingPhase(long playerId)
        {
            lock (ActionLock)
            {
                Game.Leveling.PlayerReadiness.Add(playerId);

                var isEnabled = CanMakeLevelingDecisions();
                GameInteraction.UpdateLevelingPhaseControls(isEnabled);
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
            if (Game.Stage != NetworkGameStage.Playing)
            {
                return;
            }

            GameInteraction.ShowWarningNotification(WellKnownKeys.GameNotifications.Session.PlayerJoined.Key, networkPlayer.Name);
        }

        protected void ShowPlayerDisconnectedMessage(NetworkPlayer networkPlayer)
        {
            if (networkPlayer == null || Game.Stage != NetworkGameStage.Playing)
            {
                return;
            }

            GameInteraction.ShowModalMessage(WellKnownKeys.GameNotifications.Session.PlayerLeft.Key, networkPlayer.Name);
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

        protected void LoadSaveGame()
        {
            ResetGameIdGenerator();
            Game.ForcedPause = null;
            Game.Dialog = null;

            // We need to use different save load method if someone joined mid game
            // I assume game just need to load more resources or whatever if you are not in the game already
            if (Game.Stage == NetworkGameStage.Playing)
            {
                Game.Id = GameInteraction.QuickLoadGame(Game.SaveFilePath);
            }
            else
            {
                Game.Id = GameInteraction.LoadGameFromMainMenu(Game.SaveFilePath);
            }
        }

        protected NetworkPlayer GetHost()
        {
            lock (ActionLock)
            {
                return Game.Players.First(p => p.IsHost);
            }
        }

        protected void ForceLoadGame()
        {
            Logger.LogInformation("Force loading save game. Stage={Stage}, SavePath={SavePath}", Game.Stage, Game.SaveFilePath);

            LoadSaveGame();
        }

        protected void ResetGameIdGenerator()
        {
            Logger.LogInformation("Resetting id counters. GameId={GameId}", Game.Id);
            _valueGenerator.ResetUniqueIdCounters(Game.Id);
        }

        protected void SoftReset()
        {
            Logger.LogInformation("Doing soft reset");
            Game.SaveFilePath = null;
            Game.Combat = null;
            Game.Leveling = null;
            DiceRollStorage.Reset();
            _valueGenerator.ResetSeedGenerators(Random.SeedLifetime.Area, Random.SeedLifetime.Combat);
        }

        protected string StoreSaveFile(byte[] content)
        {
            var baseUnityPath = GameInteraction.GetSaveGamePath();
            var multiplayerPath = Regex.Replace(baseUnityPath, "(((\\\\|\\/)+)(Saved Games)((\\\\|\\/)+))$", "/Saved Multiplayer Games/");
            var savePath = Path.Combine(multiplayerPath, "latest save.zks");
            Logger.LogInformation("Save game path changed. Path={Path}", savePath);
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
                Logger.LogWarning("Midfight action. UnitId={UnitId}", sourceUnitId);
                return HasControlOverUI;
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
                var roll = await DiceRollStorage.GetAsync<RollValueBase>(request.RollId, playerId, request.Timeout);
                var response = new DiceRollValueResponse
                {
                    RollId = request.RollId,
                    UnitId = request.UnitId,
                    RollValue = Mapper.Map<Networking.Messages.Contracts.NetworkRollValue>(roll)
                };

                Logger.LogInformation("Sending roll value response. RollId={RollId}, RollType={RollType}, Result={Result}, DamageValuesCount={DamageValuesCount}, RollHistoryCount={RollHistoryCount}",
                    response.RollId, roll?.GetType().Name, response.RollValue?.Result, response.RollValue?.DamageValues.Count, response.RollValue?.RollHistory.Count);

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

        protected NetworkCharacter GetCharacterOwnership(string unitId)
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
            lock (ActionLock)
            {
                return Game.Players.FirstOrDefault(p => p.Id == playerId);
            }
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

        protected virtual void OnAfterNetworkMessageHandled(long playerId, object message)
        {
        }

        protected void UpdatePlayerSaveGameSyncStatus(long playerId, NetworkPlayerSaveGameSyncStatus status)
        {
            var player = GetPlayer(playerId);
            if (player == null)
            {
                Logger.LogWarning("Unable to update save game sync status for missing player. PlayerId={PlayerId}", playerId);
                return;
            }

            UpdatePlayerSaveGameSyncStatus(player, status);
        }

        protected void UpdatePlayerSaveGameSyncStatus(NetworkPlayer player, NetworkPlayerSaveGameSyncStatus status)
        {
            player.SaveGameSyncStatus = status;
            OnPlayersChanged?.Invoke(GetPlayers());
        }

        protected void UpdatePlayerReadyStatus(long playerId, bool isReady)
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

                OnPlayersChanged?.Invoke(GetPlayers());
            }
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

        protected int GetPlayersCount()
        {
            lock (ActionLock)
            {
                return Game.Players.Count;
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
                if (Game.Combat != null)
                {
                    Game.Combat.Turn = null;
                }
            }
        }

        protected virtual void SetupNetworkMessageHandlers()
        {
            _networkReceiver
                // leveling
                .On<NotifyLevelingClassArchetypeSelected>(OnNotifyLevelingClassArchetypeSelected)
                .On<NotifyLevelingClassSelected>(OnNotifyLevelingClassSelected)
                .On<NotifyLevelingPhaseWitnessed>(OnNotifyLevelingPhaseWitnessed)
                .On<NotifyLevelingPhaseChanged>(OnNotifyLevelingPhaseChanged)
                .On<NotifyLevelingSkillPointIncreased>(OnNotifyLevelingSkillPointIncreased)
                .On<NotifyLevelingSkillPointDecreased>(OnNotifyLevelingSkillPointDecreased)
                .On<NotifyLevelingAbilityScoreIncreased>(OnNotifyLevelingAbilityScoreIncreased)
                .On<NotifyLevelingAbilityScoreDecreased>(OnNotifyLevelingAbilityScoreDecreased)
                .On<NotifyLevelingFeatureSelected>(OnNotifyLevelingFeatureSelected)
                .On<NotifyLevelingSpellChosen>(OnNotifyLevelingSpellChosen)
                .On<NotifyLevelingSpellRemoved>(OnNotifyLevelingSpellRemoved)
                .On<NotifyLevelingCompleted>(OnNotifyLevelingCompleted)
                .On<NotifyLevelingTerminated>(OnNotifyLevelingTerminated)

                // spellbook management
                .On<NotifySpellMemorized>(OnNotifySpellMemorized)
                .On<NotifySpellForgotten>(OnNotifySpellForgotten)

                // vendor interaction
                .On<NotifyVendorItemTransferred>(OnNotifyVendorItemTransferred)

                // rest
                .On<NotifyRestBanterInterrupted>(OnNotifyRestBanterInterrupted)

                // combat
                .On<NotifyUnitJoinedMidCombat>(OnNotifyUnitJoinedMidCombat)
                .On<NotifyPlayerCombatTurnEnded>(OnNotifyPlayerCombatTurnEnded)
                .On<NotifyUnitAttacked>(OnNotifyUnitAttacked)
                .On<NotifyCombatTurnDelayed>(OnNotifyCombatTurnDelayed)

                // overtips
                .On<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)

                // items&inventory
                .On<NotifyLootableEntitySkinned>(OnNotifyContainerSkinned)
                .On<NotifyDropItem>(OnNotifyDropItem)
                .On<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .On<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)
                .On<NotifyZoneLootShown>(OnNotifyZoneLootShown)
                .On<NotifyZoneLootClosed>(OnNotifyZoneLootClosed)
                .On<NotifyInventoryItemTransferred>(OnNotifyInventoryItemTransferred)

                // lockpick
                .On<NotifyMapObjectLockpicked>(OnNotifyMapObjectLockpicked)

                // abilities
                .On<NotifyAbilityUsed>(OnNotifyAbilityUsed)
                .On<NotifyToggleActivatableAbility>(OnNotifyToggleActivatableAbility)

                // clicks
                .On<NotifyUnitClicked>(OnNotifyUnitClicked)
                .On<NotifyGroundClicked>(OnNotifyGroundClicked)
                .On<NotifyMapObjectClicked>(OnNotifyMapObjectClicked)

                // movement
                .On<NotifyCharacterMove>(OnNotifyCharacterMove)

                // action bar
                .On<NotifyActionBarSlotCleared>(OnNotifyActionBarSlotCleared)
                .On<NotifyActionBarSlotMoved>(OnNotifyActionBarSlotMoved)

                // stealth
                .On<NotifyUnitStealthChanged>(OnNotifyUnitStealthChanged)

                // skip time
                .On<NotifySkipTimeOpened>(OnNotifySkipTimeOpened)

                // global map
                .On<NotifyGlobalMapMessageBoxShown>(OnNotifyGlobalMapMessageBoxShown)
                .On<NotifyGlobalMapMessageBoxClosed>(OnNotifyGlobalMapMessageBoxClosed)
                .On<NotifyGlobalMapIngredientCollectionShown>(OnNotifyGlobalMapIngredientCollectionShown)
                .On<NotifyGlobalMapIngredientCollectionClosed>(OnNotifyGlobalMapIngredientCollectionClosed)
                .On<NotifyGlobalMapEncounterMessageShown>(OnNotifyGlobalMapEncounterMessageShown)

                // group management
                .On<NotifyGroupChangerOpened>(OnNotifyGroupChangerOpened)

                // dialogs
                .On<NotifyDialogPopupShown>(OnNotifyDialogPopupShown)
                ;
        }

        private void OnNotifyDialogPopupShown(long playerId, NotifyDialogPopupShown dialogPopupShown)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, AreaName={AreaName}, DialogName={DialogName}, CueName={CueName}", nameof(NotifyDialogPopupShown), playerId, dialogPopupShown.Popup.AreaName, dialogPopupShown.Popup.DialogName, dialogPopupShown.Popup.CueName);
            AddPlayerToTracker(Game.PlayersInDialogPopup, dialogPopupShown.PlayerId);

            UpdateDialogPopupState();
        }

        private void OnNotifyInventoryItemTransferred(long playerId, NotifyInventoryItemTransferred itemTransferred)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Items={Items}, Source={Source}, Destination={Destination}", nameof(NotifyInventoryItemTransferred), playerId, itemTransferred.TransferItem.Items.Select(x => x.UniqueId), itemTransferred.TransferItem.Source.Id, itemTransferred.TransferItem.Destination?.Id);

            var transferItem = Mapper.Map<NetworkItemsTransfer>(itemTransferred.TransferItem);
            GameInteraction.TransferInventoryItems(transferItem);

            OnAfterNetworkMessageHandled(playerId, itemTransferred);
        }

        private void OnNotifyZoneLootClosed(long playerId, NotifyZoneLootClosed zoneLootClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootClosed), zoneLootClosed.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInZoneLoot, zoneLootClosed.PlayerId);
            UpdateZoneLootUIState();

            OnAfterNetworkMessageHandled(playerId, zoneLootClosed);
        }

        private void OnNotifyZoneLootShown(long playerId, NotifyZoneLootShown zoneLootShown)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyZoneLootShown), zoneLootShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInZoneLoot, zoneLootShown.PlayerId);
            UpdateZoneLootUIState();

            OnAfterNetworkMessageHandled(playerId, zoneLootShown);
        }

        private void OnNotifyGlobalMapEncounterMessageShown(long playerId, NotifyGlobalMapEncounterMessageShown globalMapEncounterMessageShown)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapEncounterMessageShown), globalMapEncounterMessageShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapEncounterMessage, globalMapEncounterMessageShown.PlayerId);
            UpdateGlobalMapEncounterMessageUIState();

            OnAfterNetworkMessageHandled(playerId, globalMapEncounterMessageShown);
        }

        private void OnNotifyGlobalMapIngredientCollectionClosed(long playerId, NotifyGlobalMapIngredientCollectionClosed globalMapIngredientCollectionClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapIngredientCollectionClosed), globalMapIngredientCollectionClosed.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInGlobalMapIngredientCollection, globalMapIngredientCollectionClosed.PlayerId);
            UpdateGlobalMapIngredientCollectionUIState();

            OnAfterNetworkMessageHandled(playerId, globalMapIngredientCollectionClosed);
        }

        private void OnNotifyGlobalMapIngredientCollectionShown(long playerId, NotifyGlobalMapIngredientCollectionShown globalMapIngredientCollectionShown)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapIngredientCollectionShown), globalMapIngredientCollectionShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapIngredientCollection, globalMapIngredientCollectionShown.PlayerId);
            UpdateGlobalMapIngredientCollectionUIState();

            OnAfterNetworkMessageHandled(playerId, globalMapIngredientCollectionShown);
        }

        private void OnNotifyGroupChangerOpened(long playerId, NotifyGroupChangerOpened groupChangerVisible)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGroupChangerOpened), groupChangerVisible.PlayerId);

            AddPlayerToTracker(Game.PlayersInGroupChanger, groupChangerVisible.PlayerId);
            UpdateGroupManagerUIState();

            OnAfterNetworkMessageHandled(playerId, groupChangerVisible);
        }

        private void OnNotifyGlobalMapMessageBoxClosed(long playerId, NotifyGlobalMapMessageBoxClosed globalMapMessageBoxClosed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapMessageBoxClosed), globalMapMessageBoxClosed.PlayerId);

            RemovePlayerFromTracker(Game.PlayersInGlobalMapLocationMessage, globalMapMessageBoxClosed.PlayerId);
            UpdateGlobalMapMessageBoxUIState();

            OnAfterNetworkMessageHandled(playerId, globalMapMessageBoxClosed);
        }

        private void OnNotifyGlobalMapMessageBoxShown(long playerId, NotifyGlobalMapMessageBoxShown globalMapMessageBoxShown)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyGlobalMapMessageBoxClosed), globalMapMessageBoxShown.PlayerId);

            AddPlayerToTracker(Game.PlayersInGlobalMapLocationMessage, globalMapMessageBoxShown.PlayerId);
            UpdateGlobalMapMessageBoxUIState();

            OnAfterNetworkMessageHandled(playerId, globalMapMessageBoxShown);
        }

        private void OnNotifySkipTimeOpened(long playerId, NotifySkipTimeOpened skipTimeOpened)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifySkipTimeOpened), skipTimeOpened.PlayerId);

            AddPlayerToTracker(Game.PlayersInSkipTime, skipTimeOpened.PlayerId);
            GameInteraction.OpenSkipTimeUI();

            UpdateSkipTimeUIState();

            OnAfterNetworkMessageHandled(playerId, skipTimeOpened);
        }

        private void OnNotifyUnitStealthChanged(long playerId, NotifyUnitStealthChanged unitStealthChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={Round}, IsEnabled={IsEnabled}, IsForced={IsForced}", nameof(NotifyUnitStealthChanged), playerId, unitStealthChanged.UnitId, unitStealthChanged.IsEnabled, unitStealthChanged.IsForced);

            GameInteraction.ChangeUnitStealth(unitStealthChanged.UnitId, unitStealthChanged.IsEnabled, unitStealthChanged.IsForced);

            OnAfterNetworkMessageHandled(playerId, unitStealthChanged);
        }

        private void OnNotifyCombatTurnDelayed(long playerId, NotifyCombatTurnDelayed combatTurnDelayed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={Round}, TargetUnitId={TargetUnitId}", nameof(NotifyCombatTurnDelayed), playerId, combatTurnDelayed.UnitId, combatTurnDelayed.TargetUnitId);

            Game.Combat.Turn.IsInProgress = false;
            GameInteraction.DelayCombatTurn(combatTurnDelayed.UnitId, combatTurnDelayed.TargetUnitId);

            OnAfterNetworkMessageHandled(playerId, combatTurnDelayed);
        }

        private void OnNotifyMapObjectLockpicked(long playerId, NotifyMapObjectLockpicked mapObjectLockpicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, MapObjectId={MapObjectId}, MapObjectPosition={MapObjectPosition}, Units={Units}", nameof(NotifyMapObjectLockpicked), playerId, mapObjectLockpicked.LockpickInteraction.MapObject.Id, mapObjectLockpicked.LockpickInteraction.MapObject.Position, mapObjectLockpicked.LockpickInteraction.Units);
            var lockpickInteraction = Mapper.Map<NetworkLockpickInteraction>(mapObjectLockpicked.LockpickInteraction);

            GameInteraction.LockpickMapObject(lockpickInteraction);

            OnAfterNetworkMessageHandled(playerId, mapObjectLockpicked);
        }

        private void OnNotifyActionBarSlotMoved(long playerId, NotifyActionBarSlotMoved actionBarSlotMoved)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SourceSlotIndex={SourceSlotIndex}, SourceSlotAbilityId={SourceSlotAbilityId}, SourceSlotActivatableAbilityId={SourceSlotActivatableAbilityId}, SourceSlotItemId={SourceSlotItemId}, TargetSlotIndex={TargetSlotIndex}, TargetSlotAbilityId={TargetSlotAbilityId}, TargetSlotActivatableAbilityId={TargetSlotActivatableAbilityId}, TargetSlotItemId={TargetSlotItemId}",
                nameof(NotifyActionBarSlotMoved), playerId, actionBarSlotMoved.TargetActionBarSlot.UnitId, actionBarSlotMoved.SourceActionBarSlot.Index, actionBarSlotMoved.SourceActionBarSlot.Ability?.Id, actionBarSlotMoved.SourceActionBarSlot.ActivatableAbility?.Id, actionBarSlotMoved.SourceActionBarSlot.Item?.UniqueId, actionBarSlotMoved.TargetActionBarSlot.Index, actionBarSlotMoved.TargetActionBarSlot.Ability?.Id, actionBarSlotMoved.TargetActionBarSlot.ActivatableAbility?.Id, actionBarSlotMoved.TargetActionBarSlot.Item?.UniqueId);

            var sourceActionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotMoved.SourceActionBarSlot);
            var targetActionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotMoved.TargetActionBarSlot);

            GameInteraction.MoveActionBarSlots(sourceActionBarSlot, targetActionBarSlot);

            OnAfterNetworkMessageHandled(playerId, actionBarSlotMoved);
        }

        private void OnNotifyActionBarSlotCleared(long playerId, NotifyActionBarSlotCleared actionBarSlotCleared)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SlotIndex={SlotIndex}", nameof(NotifyActionBarSlotCleared), playerId, actionBarSlotCleared.ActionBarSlot.UnitId, actionBarSlotCleared.ActionBarSlot.Index);

            var actionBarSlot = Mapper.Map<NetworkActionBarSlot>(actionBarSlotCleared.ActionBarSlot);

            GameInteraction.ClearActionBarSlot(actionBarSlot);

            OnAfterNetworkMessageHandled(playerId, actionBarSlotCleared);
        }

        private void OnNotifyCharacterMove(long playerId, NotifyCharacterMove characterMove)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, Destination={Destination}, Delay={Delay}, Orientation={Orientation}", nameof(NotifyCharacterMove), playerId, characterMove.Move.UnitId, characterMove.Move.Destination, characterMove.Move.Delay, characterMove.Move.Orientation);

            var move = Mapper.Map<NetworkCharacterMove>(characterMove.Move);
            GameInteraction.MoveNonCombatCharacter(move);

            OnAfterNetworkMessageHandled(playerId, characterMove);
        }

        private void OnNotifyGroundClicked(long playerId, NotifyGroundClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SelectedUnits={SelectedUnits}, WorldPosition={WorldPosition}", nameof(NotifyGroundClicked), playerId, clicked.Click.SelectedUnits.Count, clicked.Click.WorldPosition);
            if (Game.Combat == null)
            {
                Logger.LogWarning($"{nameof(NotifyGroundClicked)} is ignored out of combat");
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickGroundInCombat(click);

            OnAfterNetworkMessageHandled(playerId, clicked);
        }

        private void OnNotifyUnitClicked(long playerId, NotifyUnitClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyUnitClicked), playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var canGetUp = GameInteraction.CanRiderGetUp();
            if (Game.Combat != null && !canGetUp)
            {
                Logger.LogInformation("Ignoring {MessageType} in combat", nameof(NotifyUnitClicked));
                return;
            }

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickUnit(click);

            OnAfterNetworkMessageHandled(playerId, clicked);
        }

        private void OnNotifyMapObjectClicked(long playerId, NotifyMapObjectClicked clicked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, TargetUnitId={TargetUnitId}, SelectedUnits={SelectedUnits}", nameof(NotifyMapObjectClicked), playerId, clicked.Click.TargetUnitId, clicked.Click.SelectedUnits.Count);

            var click = Mapper.Map<NetworkClick>(clicked.Click);
            GameInteraction.ClickMapObject(click);

            OnAfterNetworkMessageHandled(playerId, clicked);
        }

        private void OnNotifyToggleActivatableAbility(long playerId, NotifyToggleActivatableAbility activatableAbility)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, AbilityId={AbilityId}, IsActive={IsActive}", nameof(NotifyToggleActivatableAbility), playerId, activatableAbility.Ability.Id, activatableAbility.Ability.IsActive);

            var ability = Mapper.Map<NetworkActivatableAbility>(activatableAbility.Ability);
            GameInteraction.ToggleActivatableAbility(ability);

            OnAfterNetworkMessageHandled(playerId, activatableAbility);
        }

        private void OnNotifyAbilityUsed(long playerId, NotifyAbilityUsed abilityUse)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, CasterId={CasterId}, AbilityId={AbilityId}", nameof(NotifyAbilityUsed), playerId, abilityUse.Ability.CasterId, abilityUse.Ability.Id);

            var ability = Mapper.Map<NetworkAbility>(abilityUse.Ability);
            GameInteraction.UseAbility(ability);

            OnAfterNetworkMessageHandled(playerId, abilityUse);
        }

        private void OnNotifyUnitAttacked(long playerId, NotifyUnitAttacked unitAttacked)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ExecutorUnitId={ExecutorUnitId}, TargetUnitId={TargetUnitId}, IsFullAttack={IsFullAttack}", nameof(NotifyUnitAttacked), playerId, unitAttacked.Attack.ExecutorUnitId, unitAttacked.Attack.TargetUnitId, unitAttacked.Attack.IsFullAttack);

            var attack = Mapper.Map<NetworkUnitAttack>(unitAttacked.Attack);
            GameInteraction.AttackUnit(attack);

            OnAfterNetworkMessageHandled(playerId, unitAttacked);
        }

        private void OnNotifyPlayerCombatTurnEnded(long playerId, NotifyPlayerCombatTurnEnded ended)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyPlayerCombatTurnEnded), playerId, ended.UnitId);

            OnAfterNetworkMessageHandled(playerId, ended);

            if (!string.Equals(Game.Combat.Turn?.UnitId, ended.UnitId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Player ended invalid turn. PlayerId={PlayerId}, PlayerUnitId={PlayerUnitId}, LocalUnitId={LocalUnitId}", playerId, ended.UnitId, Game.Combat.Turn?.UnitId);
                return;
            }

            EndLocalTurn();
        }

        private void OnNotifyActiveHandEquipmentSetChanged(long playerId, NotifyActiveHandEquipmentSetChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SetIndex={SetIndex}", nameof(NotifyEquipmentSlotChanged), playerId, changed.Set.UnitId, changed.Set.Index);
            var set = Mapper.Map<NetworkActiveHandEquipmentSet>(changed.Set);
            GameInteraction.SetActiveHandEquipmentSet(set);

            OnAfterNetworkMessageHandled(playerId, changed);
        }

        private void OnNotifyEquipmentSlotChanged(long playerId, NotifyEquipmentSlotChanged slotChanged)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SlotType={SlotType}, SlotIndex={SlotIndex}, ItemId={ItemId}, OwnerId={OwnerId}", nameof(NotifyEquipmentSlotChanged), playerId, slotChanged.Slot.Position.Type, slotChanged.Slot.Position.Index, slotChanged.Slot.Item?.UniqueId, slotChanged.Slot.OwnerId);
            var slot = Mapper.Map<NetworkEquipmentSlot>(slotChanged.Slot);
            GameInteraction.UpdateEquipmentSlot(slot);

            OnAfterNetworkMessageHandled(playerId, slotChanged);
        }

        private void OnNotifyDropItem(long playerId, NotifyDropItem item)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, OwnerId={OwnerId}, ItemId={ItemId}, ItemName={ItemName}", nameof(NotifyDropItem), playerId, item.Drop.OwnerEntityId, item.Drop.Item.UniqueId, item.Drop.Item.Name);

            var dropItem = Mapper.Map<NetworkDropItem>(item.Drop);
            GameInteraction.DropItem(dropItem);

            OnAfterNetworkMessageHandled(playerId, item);
        }

        private void OnNotifyContainerSkinned(long playerId, NotifyLootableEntitySkinned notifyLootableEntitySkinned)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Id={Id}, Position={Position}, Type={Type}", nameof(NotifyLootableEntitySkinned), playerId, notifyLootableEntitySkinned.Entity.Id, notifyLootableEntitySkinned.Entity.Position, notifyLootableEntitySkinned.Entity.Type);
            var container = Mapper.Map<NetworkLootableEntity>(notifyLootableEntitySkinned.Entity);
            GameInteraction.SkinLootContainer(container);
        }

        private void OnNotifyOvertipInteracted(long playerId, NotifyOvertipInteracted interacted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, MapObjectId={MapObjectId}, UnitsCount={UnitsCount}", nameof(NotifyOvertipInteracted), playerId, interacted.Overtip.MapObject.Id, interacted.Overtip.Units);
            var overtip = Mapper.Map<NetworkOvertip>(interacted.Overtip);
            GameInteraction.InteractWithOvertip(overtip);

            OnAfterNetworkMessageHandled(playerId, interacted);
        }

        private void OnNotifyUnitJoinedMidCombat(long playerId, NotifyUnitJoinedMidCombat combat)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}", nameof(NotifyUnitJoinedMidCombat), playerId, combat.UnitId);
            AddPlayerReadyStatus(PlayerTurnReadinessType.UnitJoinedMidCombat, combat.PlayerId, combat.UnitId);

            OnAfterNetworkMessageHandled(playerId, combat);
        }

        private void OnNotifyRestBanterInterrupted(long playerId, NotifyRestBanterInterrupted interrupted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SpeakerUnitId={SpeakerUnitId}, Key={Key}", nameof(NotifyRestBanterInterrupted), playerId, interrupted.Banter.SpeakerUnitId, interrupted.Banter.Key);
            var banter = Mapper.Map<NetworkRestBanter>(interrupted.Banter);
            GameInteraction.TryInterruptRestBanter(banter);

            OnAfterNetworkMessageHandled(playerId, interrupted);
        }

        private void OnNotifyVendorItemTransferred(long playerId, NotifyVendorItemTransferred message)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ItemId={ItemId}, Count={Count}, Action={Action}, ActionTarget={ActionTarget}", nameof(NotifyVendorItemTransferred), playerId, message.ItemTransfer.Item.UniqueId, message.ItemTransfer.Count, message.ItemTransfer.ItemAction, message.ItemTransfer.ItemActionTarget);

            var transfer = Mapper.Map<NetworkVendorItemTransfer>(message.ItemTransfer);
            GameInteraction.TransferVendorItem(transfer);

            OnAfterNetworkMessageHandled(playerId, message);
        }

        private void OnNotifySpellForgotten(long playerId, NotifySpellForgotten spellForgotten)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SpellbookId={SpellbookId}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellForgotten), playerId, spellForgotten.Slot.UnitId, spellForgotten.Slot.SpellbookId, spellForgotten.Slot.Index, spellForgotten.Slot.Type);

            var slot = Mapper.Map<NetworkSpellSlot>(spellForgotten.Slot);

            GameInteraction.ForgetSpell(slot);

            OnAfterNetworkMessageHandled(playerId, spellForgotten);
        }

        private void OnNotifySpellMemorized(long playerId, NotifySpellMemorized memorized)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, UnitId={UnitId}, SpellbookId={SpellbookId}, SpellId={SpellId}, SpellLevel={SpellLevel}, SpellName={SpellName}, SpellSlotIndex={SpellSlotIndex}, SpellSlotType={SpellSlotType}",
                nameof(NotifySpellMemorized), playerId, memorized.Slot.UnitId, memorized.Slot.SpellbookId, memorized.Slot.SpellId, memorized.Slot.SpellLevel, memorized.Slot.SpellName, memorized.Slot.Index, memorized.Slot.Type);

            var slot = Mapper.Map<NetworkSpellSlot>(memorized.Slot);

            GameInteraction.MemorizeSpell(slot);

            OnAfterNetworkMessageHandled(playerId, memorized);
        }

        private void OnNotifyLevelingAbilityScoreDecreased(long playerId, NotifyLevelingAbilityScoreDecreased decreased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingAbilityScoreDecreased), playerId, decreased.AbilityScore.StatType);

            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(decreased.AbilityScore);
            GameInteraction.DecreaseLevelingAbilityScore(abilityScore);

            OnAfterNetworkMessageHandled(playerId, decreased);
        }

        private void OnNotifyLevelingAbilityScoreIncreased(long playerId, NotifyLevelingAbilityScoreIncreased increased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingAbilityScoreIncreased), playerId, increased.AbilityScore.StatType);

            var abilityScore = Mapper.Map<NetworkLevelingAbilityScore>(increased.AbilityScore);
            GameInteraction.IncreaseLevelingAbilityScore(abilityScore);

            OnAfterNetworkMessageHandled(playerId, increased);
        }

        private void OnNotifyLevelingCompleted(long playerId, NotifyLevelingCompleted completed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingCompleted), playerId);
            GameInteraction.CompleteLeveling();

            OnAfterNetworkMessageHandled(playerId, completed);
        }

        private void OnNotifyLevelingTerminated(long playerId, NotifyLevelingTerminated terminated)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingTerminated), playerId);
            GameInteraction.TerminateLeveling();

            OnAfterNetworkMessageHandled(playerId, terminated);
        }

        private void OnNotifyLevelingSpellRemoved(long playerId, NotifyLevelingSpellRemoved removed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellRemoved), playerId, removed.Spell.Name, removed.Spell.Id);
            var spell = Mapper.Map<NetworkLevelingSpell>(removed.Spell);
            GameInteraction.RemoveLevelingSpell(spell);

            OnAfterNetworkMessageHandled(playerId, removed);
        }

        private void OnNotifyLevelingSpellChosen(long playerId, NotifyLevelingSpellChosen chosen)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, SpellName={SpellName}, SpellId={SpellId}", nameof(NotifyLevelingSpellChosen), playerId, chosen.Spell.Name, chosen.Spell.Id);
            var spell = Mapper.Map<NetworkLevelingSpell>(chosen.Spell);
            GameInteraction.SelectLevelingSpell(spell);

            OnAfterNetworkMessageHandled(playerId, chosen);
        }

        private void OnNotifyLevelingFeatureSelected(long playerId, NotifyLevelingFeatureSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, FeatureName={FeatureName}, FeatureId={FeatureId}", nameof(NotifyLevelingFeatureSelected), playerId, selected.Feature.Name, selected.Feature.Id);
            var feature = Mapper.Map<NetworkLevelingFeature>(selected.Feature);
            GameInteraction.SelectLevelingFeature(feature);

            OnAfterNetworkMessageHandled(playerId, selected);
        }

        private void OnNotifyLevelingSkillPointDecreased(long playerId, NotifyLevelingSkillPointDecreased decreased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingSkillPointDecreased), playerId, decreased.Skill.StatType);
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(decreased.Skill);
            GameInteraction.DecreaseLevelingSkillPoint(skillPoint);
            OnAfterNetworkMessageHandled(playerId, decreased);
        }

        private void OnNotifyLevelingSkillPointIncreased(long playerId, NotifyLevelingSkillPointIncreased increased)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, StatType={StatType}", nameof(NotifyLevelingSkillPointIncreased), playerId, increased.Skill.StatType);
            var skillPoint = Mapper.Map<NetworkLevelingSkillPoint>(increased.Skill);
            GameInteraction.IncreaseLevelingSkillPoint(skillPoint);

            OnAfterNetworkMessageHandled(playerId, increased);
        }

        private void OnNotifyLevelingPhaseChanged(long playerId, NotifyLevelingPhaseChanged changed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, Index={Index}", nameof(NotifyLevelingPhaseChanged), playerId, changed.Phase.Index);
            var phase = Mapper.Map<NetworkLevelingPhase>(changed.Phase);
            Game.Leveling.PlayerReadiness.Clear();
            GameInteraction.SwitchLevelingPhase(phase);

            OnAfterNetworkMessageHandled(playerId, changed);
        }

        private void OnNotifyLevelingPhaseWitnessed(long playerId, NotifyLevelingPhaseWitnessed witnessed)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}", nameof(NotifyLevelingPhaseWitnessed), playerId);
            WitnessLevelingPhase(playerId);

            OnAfterNetworkMessageHandled(playerId, witnessed);
        }

        private void OnNotifyLevelingClassArchetypeSelected(long playerId, NotifyLevelingClassArchetypeSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ArchetypeId={ArchetypeId}", nameof(NotifyLevelingClassArchetypeSelected), playerId, selected.ArchetypeId);
            GameInteraction.SelectLevelingClassArchetype(selected.ArchetypeId);

            OnAfterNetworkMessageHandled(playerId, selected);
        }

        private void OnNotifyLevelingClassSelected(long playerId, NotifyLevelingClassSelected selected)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ClassId={ClassId}", nameof(NotifyCharacterLevelingStarted), playerId, selected.ClassId);
            GameInteraction.SelectLevelingClass(selected.ClassId);

            OnAfterNetworkMessageHandled(playerId, selected);
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
                    IsAI = GameInteraction.IsUnitAI(unitId),
                };
            }

            Logger.LogWarning("OnTurnStart. UnitId={UnitId}, IsLocalPlayer={IsLocalPlayer}, IsAI={IsAI}, IsActingInSurpriseRound={IsActingInSurpriseRound}, IsInProgress={IsInProgress}",
                unitId, Game.Combat.Turn.IsLocalPlayer, Game.Combat.Turn.IsAI, Game.Combat.Turn.IsActingInSurpriseRound, Game.Combat.Turn.IsInProgress);

            OnLocalPlayerTurnStart();
        }
    }
}

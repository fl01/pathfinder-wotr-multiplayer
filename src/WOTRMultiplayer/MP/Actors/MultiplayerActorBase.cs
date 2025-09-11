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
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Abilities;
using WOTRMultiplayer.MP.Entities.ActionBar;
using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;
using WOTRMultiplayer.MP.Entities.Leveling;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.MP.Entities.MapObjects;
using WOTRMultiplayer.MP.Entities.Movement;
using WOTRMultiplayer.MP.Entities.Rest;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.MP.Entities.Spells;
using WOTRMultiplayer.MP.Entities.Vendor;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.UI;

namespace WOTRMultiplayer.MP.Actors
{
    public abstract class MultiplayerActorBase
    {
        private readonly object _actionLock = new();

        public bool IsInCombat => Game?.Combat != null;

        public int RestBanterSeed => Game.RestBanterSeed;

        public Action<List<NetworkPlayer>> OnPlayersChanged { get; set; }

        internal NetworkGame Game { get; set; }

        protected ILogger Logger { get; private set; }

        protected IMapper Mapper { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

        protected IDiceRollStorage DiceRollStorage { get; private set; }

        protected IFileSystemService FileSystem { get; private set; }

        protected IMultiplayerSettingsProvider SettingsProvider { get; private set; }

        private readonly IValueGenerator _valueGenerator;
        private readonly INetworkReceiver _networkReceiver;

        protected abstract bool IsHost { get; }

        protected object ActionLock => _actionLock;

        protected MultiplayerActorBase(
            ILogger logger,
            IMapper mapper,
            IMultiplayerSettingsProvider multiplayerSettingsProvider,
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
            SettingsProvider = multiplayerSettingsProvider;
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

            Logger.LogInformation("Toggle activatable ability. CasterId={CasterId}, TargetId={TargetId}, AbilityId={AbilityId}, IsActive={IsActive}", activatableAbilityUse.CasterId, activatableAbilityUse.TargetId, activatableAbilityUse.Id, activatableAbilityUse.IsActive);

            var message = new NotifyToggleActivatableAbility
            {
                Ability = Mapper.Map<Networking.Messages.Contracts.NetworkActivatableAbility>(activatableAbilityUse)
            };

            Send(message);
        }

        public TRollValue RetrieveRoll<TRollValue>(int networkDiceRollId, string unitId)
            where TRollValue : RollValueBase
        {
            Logger.LogInformation("Retrieving roll over network. RollId={RollId}, UnitId={UnitId}", networkDiceRollId, unitId);

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

            Logger.LogInformation("Sending unit click. TargetUnitId={TargetUnitId}, VectorPathCount={VectorPathCount}", click.TargetUnitId, click.VectorPath.Count);

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

            Logger.LogInformation("Sending ground click. WorldPosition={WorldPosition}, VectorPathCount={VectorPathCount}, SelectedUnits={SelectedUnits}", click.WorldPosition, click.VectorPath.Count, string.Join(";", click.SelectedUnits));
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

            Logger.LogInformation("Sending map object click. WorldPosition={WorldPosition}, VectorPathCount={VectorPathCount}, SelectedUnits={SelectedUnits}", click.WorldPosition, click.VectorPath.Count, string.Join(";", click.SelectedUnits));
            var message = new NotifyMapObjectClicked
            {
                Click = Mapper.Map<Networking.Messages.Contracts.NetworkClick>(click)
            };

            Send(message);
        }

        public void OnLootContainer(NetworkLootContainer container)
        {
            Logger.LogInformation("Sending looted container info. ContainerId={ContainerId}, ContainerPosition={ContainerPosition}, ItemsCount={ItemsCount}, Items={Items}", container.Id, container.Position, container.Items.Count, container.Items.Select(i => i.UniqueId));
            var message = new NotifyContainerLooted
            {
                Container = Mapper.Map<Networking.Messages.Contracts.NetworkLootContainer>(container)
            };

            Send(message);
        }

        public void OnSkinLootContainer(NetworkLootContainer container)
        {
            Logger.LogInformation("Sending skin container info. ContainerId={ContainerId}, ContainerPosition={ContainerPosition}", container.Id, container.Position);
            var message = new NotifyContainerSkinned
            {
                Container = Mapper.Map<Networking.Messages.Contracts.NetworkLootContainer>(container)
            };

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

        public virtual void OnAreaScenesLoaded()
        {
            Logger.LogInformation("Area loaded");

            Game.Stage = NetworkGameStage.Playing;

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
            var defaultOwner = GetPlayer(NetworkingConsts.HostPlayerId);
            foreach (var character in Game.Characters)
            {
                var existingOwnershipConfiguration = oldCharacters.FirstOrDefault(old =>
                    old.Name == character.Name || old.Name.Contains(character.Name));

                if (existingOwnershipConfiguration?.Owner != null)
                {
                    character.Owner = existingOwnershipConfiguration.Owner;
                    Logger.LogInformation("Character ownership has been preserved. UnitId={UnitId}, CharacterName={CharacterName}, Owner={Owner}", character.UnitId, character.Name, character.Owner.Id);
                    continue;
                }

                character.Owner = defaultOwner;
                Logger.LogInformation("Character ownership has been assigned to default player (host). UnitId={UnitId}, CharacterName={CharacterName}, Owner={Owner}", character.UnitId, character.Name, character.Owner.Id);
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

        public bool OnBeforeStartTurn(string unitId, bool actingInSurpriseRound)
        {
            if (Game.Combat.Turn != null && Game.Combat.Turn.IsInProgress)
            {
                if (!string.Equals(Game.Combat.Turn.UnitId, unitId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogError("Invalid unit turn start detected. ExpectedUnitId={ExpectedUnitId}, ActualUnitId={ActualUnitId}", Game.Combat.Turn.UnitId, unitId);
                }

                UpdateConfirmedMidCombatUnits();
                Game.Combat.AIActions.Clear();
                Logger.LogInformation("Turn start is allowed. UnitId={UnitId}, IsActingInSurpiseRound={IsActingInSurpiseRound}, TurnUnitId={TurnUnitId}", unitId, actingInSurpriseRound, Game.Combat.Turn.UnitId);
                return true;
            }

            Game.Combat.Turn = new NetworkCombatTurn
            {
                UnitId = unitId,
                IsInProgress = false,
                IsActingInSurpriseRound = actingInSurpriseRound,
                IsLocalPlayer = IsControlledByLocalPlayer(unitId),
                IsAI = GameInteraction.IsUnitAI(unitId),
            };

            Logger.LogWarning("OnTurnStart. UnitId={UnitId}, IsLocalPlayer={IsLocalPlayer}, IsAI={IsAI}, IsActingInSurpriseRound={IsActingInSurpriseRound}, IsInProgress={IsInProgress}",
                unitId, Game.Combat.Turn.IsLocalPlayer, Game.Combat.Turn.IsAI, Game.Combat.Turn.IsActingInSurpriseRound, Game.Combat.Turn.IsInProgress);

            OnLocalPlayerTurnStart();

            return false;
        }

        public bool OnBeforeEndTurn(string unitId)
        {
            if (Game.Combat.Turn.IsAI || !Game.Combat.Turn.IsInProgress)
            {
                Logger.LogInformation("Turn end is allowed. Round={Round}, TurnUnitId={TurnUnitId}, IsAI={IsAI}, UnitId={UnitId}", Game.Combat.Round, Game.Combat.Turn.UnitId, Game.Combat.Turn.IsAI, unitId);
                Game.Combat.Turn = null;
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
            return Game.Leveling != null && IsControlledByLocalPlayer(Game.Leveling.UnitId) && Game.Leveling.PlayerReadiness.Count >= Game.Players.Count;
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
            GameInteraction.ShowWarningNotification(string.Format(UIStringConsts.GameNotifications.LevelingTerminated, character?.Name));
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
            GameInteraction.AddCombatText(string.Format(UIStringConsts.GameNotifications.LevelingCompleted, character?.Name));
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

        protected abstract bool OnStartGameModeInternal(GameModeType type);

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
            EnsureForcePaused(reason, SettingsProvider.Settings.ForcedPauseDefaultTerminationDelay);
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

        protected abstract DiceRollValueResponse RetrieveRoll(DiceRollValueRequest rollRequest);

        protected abstract void OnLocalPlayerTurnStart();

        protected abstract void Send(object message);

        protected abstract void Send(long playerId, object message);

        protected void ShowPlayerDisconnectedMessage(NetworkPlayer networkPlayer)
        {
            if (networkPlayer == null || Game.Stage != NetworkGameStage.Playing)
            {
                return;
            }

            GameInteraction.ShowModalMessage(string.Format(UIStringConsts.GameNotifications.PlayerLeft, networkPlayer.Name));
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

            foreach (var characterOwnership in Game.Characters)
            {
                if (characterOwnership.Owner == player)
                {
                    characterOwnership.Owner = GetPlayer(NetworkingConsts.HostPlayerId);
                }
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
            return Game.Players.First(p => p.Id == NetworkingConsts.HostPlayerId);
        }

        protected void ForceLoadGame()
        {
            Logger.LogInformation("Force loading save game. Stage={Stage}, SavePath={SavePath}", Game.Stage, Game.SaveFilePath);

            LoadSaveGame();
        }

        protected void ResetGameIdGenerator()
        {
            Logger.LogInformation("Resetting id counters. GameId={GameId}", Game.Id);
            _valueGenerator.Reset(Game.Id);
        }

        protected void SoftReset()
        {
            Logger.LogInformation("Doing soft reset");
            Game.SaveFilePath = null;
            Game.Combat = null;
            Game.Leveling = null;
            DiceRollStorage.Reset();
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
            OnPlayersChanged?.Invoke(Game.Players);
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

                OnPlayersChanged?.Invoke(Game.Players);
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
                // overtips
                .On<NotifyOvertipInteracted>(OnNotifyOvertipInteracted)
                // items&inventory
                .On<NotifyContainerLooted>(OnNotifyContainerLooted)
                .On<NotifyContainerSkinned>(OnNotifyContainerSkinned)
                .On<NotifyDropItem>(OnNotifyDropItem)
                .On<NotifyEquipmentSlotChanged>(OnNotifyEquipmentSlotChanged)
                .On<NotifyActiveHandEquipmentSetChanged>(OnNotifyActiveHandEquipmentSetChanged)
                // lockpick
                .On<NotifyMapObjectLockpicked>(OnNotifyMapObjectLockpicked)
                // abilities
                .On<NotifyAbilityUse>(OnNotifyAbilityUsed)
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
                ;
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

        private void OnNotifyAbilityUsed(long playerId, NotifyAbilityUse abilityUse)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, AbilityId={AbilityId}", nameof(NotifyAbilityUse), playerId, abilityUse.Ability.Id);

            var ability = Mapper.Map<NetworkAbility>(abilityUse.Ability);
            GameInteraction.UseAbility(ability);

            OnAfterNetworkMessageHandled(playerId, abilityUse);
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

        private void OnNotifyContainerLooted(long playerId, NotifyContainerLooted containerLooted)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ContainerId={ContainerId}, ContainerPosition={ContainerPosition}, ItemsCount={ItemsCount}, Items={Items}",
               nameof(NotifyContainerLooted), playerId, containerLooted.Container.Id, containerLooted.Container.Position, containerLooted.Container.Items.Count, containerLooted.Container.Items.Select(i => i.UniqueId));

            var container = Mapper.Map<NetworkLootContainer>(containerLooted.Container);
            GameInteraction.CollectLootContainer(container);

            OnAfterNetworkMessageHandled(playerId, containerLooted);
        }

        private void OnNotifyContainerSkinned(long playerId, NotifyContainerSkinned containerSkinned)
        {
            Logger.LogInformation("Received {MessageType}. PlayerId={PlayerId}, ContainerId={ContainerId}, ContainerPosition={ContainerPosition}", nameof(NotifyContainerSkinned), playerId, containerSkinned.Container.Id, containerSkinned.Container.Position);
            var container = Mapper.Map<NetworkLootContainer>(containerSkinned.Container);
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
    }
}

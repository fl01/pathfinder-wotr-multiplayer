using System;
using System.Linq;
using Kingmaker;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Rest;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.Globalmap.View;
using Kingmaker.Kingdom.Armies;
using Kingmaker.RandomEncounters;
using Kingmaker.RandomEncounters.Settings;
using Kingmaker.UI.MVVM._PCView.Crusade.Armies;
using Kingmaker.UI.MVVM._PCView.Crusade.ArmyInfo;
using Kingmaker.UI.MVVM._VM.Crusade.ArmyInfo;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities.GlobalMap;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class GlobalMapInteractionService : IGlobalMapInteractionService
    {
        private readonly ILogger<GlobalMapInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IUIAccessor _uiAccessor;
        private readonly IUISyncCountersService _uiSyncCountersService;

        public GlobalMapInteractionService(
            ILogger<GlobalMapInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IGameStateLookupService gameStateLookupService,
            IUIAccessor uiAccessor,
            IUISyncCountersService uiSyncCountersService)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _gameStateLookupService = gameStateLookupService;
            _uiAccessor = uiAccessor;
            _uiSyncCountersService = uiSyncCountersService;
        }

        public void OpenRestMenu()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.RestView != null)
                {
                    return;
                }

                var isOk = RestHelper.TryStartRest();
                if (!isOk)
                {
                    _logger.LogError("Unable to start global map rest");
                    return;
                }

                _logger.LogInformation("Opened global map rest menu");
            });
        }

        public void StartTravel(NetworkGlobalMapTravel travel)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = _gameStateLookupService.GetGlobalMapPoint(travel.Destination);
                if (point == null)
                {
                    _logger.LogError("Unable to find global map point. PointId={PointId}, PointName={PointName}", travel.Destination.Id, travel.Destination.Name);
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                modalMessage.ViewModel?.OnDeclinePressed();
                var messageBoxView = _uiAccessor.GlobalMapPCView.m_GlobalMapEnterMessagePCView;
                messageBoxView.ViewModel?.Close();

                var traveler = Game.Instance.GlobalMapController.SelectedTraveler;
                UpdateTraveler(traveler, travel.Traveler);

                GlobalMapTravelData globalMapTravelData = travel.Type switch
                {
                    NetworkGlobalMapPathType.Direction => GlobalMapView.Instance.State.PathManager.CalculatePathByDirection(traveler, point.Blueprint),
                    NetworkGlobalMapPathType.Exact => GlobalMapView.Instance.State.PathManager.CalculateTravelerPathToLocation(traveler, point.Blueprint),
                    _ => null
                };

                if (globalMapTravelData == null)
                {
                    _logger.LogError("Failed to calculate travel data. PathType={PathType}, Destination={DestinationId}, DestinationName={DestinationName}", travel.Type, point.Blueprint.AssetGuid.ToString(), point.name);
                    return;
                }

                traveler.StartTravel(globalMapTravelData, travel.FromClick);
                _logger.LogInformation("Global map traveler has been started. Type={Type}, FromClick={FromClick}, Destination={DestinationId}, DestinationName={DestinationName}", travel.Type, travel.FromClick, point.Blueprint.AssetGuid.ToString(), point.name);
            });
        }

        public void DeclineCommonPopup()
        {
            _mainThreadAccessor.Post(() =>
            {
                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel == null)
                {
                    _logger.LogWarning("Global map common popup is missing");
                    return;
                }

                modalMessage.m_DeclineButton.OnLeftClick.Invoke();
                _logger.LogInformation("Global map common popup has been closed");
            });
        }

        public bool IsAtLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var targetPoint = _gameStateLookupService.GetGlobalMapPoint(globalMapLocation);
            return targetPoint != null && GlobalMapView.Instance.State.Player.Location == targetPoint.Blueprint;
        }

        public void ContinueTravel(NetworkGlobalMapTraveler travaler)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateTraveler(travaler);
                GlobalMapUI.Instance.OnContinue();
            });
        }

        public void StopTravel(NetworkGlobalMapTraveler travaler)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateTraveler(travaler);

                GlobalMapUI.Instance.OnStop();
            });
        }

        public void UpdateEnterMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    return;
                }

                var messageBoxView = _uiAccessor.GlobalMapPCView.m_GlobalMapEnterMessagePCView;
                if (messageBoxView?.ViewModel == null)
                {
                    return;
                }

                messageBoxView.m_AcceptButton.Interactable = !messageBoxView.ViewModel.IsCurrentLocation || isInteractable;
                messageBoxView.m_DeclineButton.Interactable = !messageBoxView.ViewModel.IsCurrentLocation || isInteractable;

                var buttonText = messageBoxView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                if (messageBoxView.ViewModel.IsCurrentLocation)
                {
                    _uiSyncCountersService.UpdateButtonTextCounter(messageBoxView.m_AcceptText, readyPlayersCount, totalPlayersCount);
                    _uiSyncCountersService.UpdateButtonTextCounter(messageBoxView.m_DeclineText, readyPlayersCount, totalPlayersCount);
                }
                else
                {
                    _uiSyncCountersService.RemoveButtonTextCounter(messageBoxView.m_AcceptText);
                    _uiSyncCountersService.RemoveButtonTextCounter(messageBoxView.m_DeclineText);
                }

                _logger.LogInformation("Global Map Message box buttons have been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateCommonPopupUI(NetworkGlobalMapCommonPopup globalMapCommonPopup, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView?.ViewModel == null)
                {
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                modalMessage.m_AcceptButton.Interactable = isInteractable;
                modalMessage.m_DeclineButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AcceptText, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_DeclineText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Global Map Common Popup buttons have been updated. Type={Type}, IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", globalMapCommonPopup?.Type, isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView?.ViewModel == null)
                {
                    return;
                }

                var modalMessage = _uiAccessor.GlobalMapPCView.m_GlobalMapRandomEncounterPCView;
                modalMessage.m_AvoidButton.Interactable = isInteractable;
                modalMessage.m_ContinueButton.Interactable = isInteractable;
                modalMessage.m_EnterButton.Interactable = isInteractable;

                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AvoidLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_ContinueLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_EnterLabel, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Encounter Message has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateCrusadeArmyBattleResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TacticalCombatResultsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update crusade army battle results due to missing view");
                    return;
                }

                view.m_CloseButton.Interactable = isInteractable;
                view.m_StartManualCombatButton.Interactable = isInteractable;

                _uiSyncCountersService.UpdateButtonTextCounter(view.m_CloseButtonLabel, readyPlayersCount, totalPlayersCount);
                _uiSyncCountersService.UpdateButtonTextCounter(view.m_StartManualCombatButtonLabel, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CloseCrusadeArmyBattleResults()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TacticalCombatResultsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army battle results due to missing view");
                    return;
                }

                view.ViewModel.Close();
                _logger.LogInformation("BattleResults window has been closed");
            });
        }

        public void StartCrusadeArmyBattleResultsManualCombat()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.TacticalCombatResultsPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to start crusade army battle results manual combat due to missing view");
                    return;
                }

                view.ViewModel.StartManualCombat();
                _logger.LogInformation("Manual combat has been started via BattleResults");
            });
        }

        public void CloseCombatResults()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_CombatResultPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close combat results UI due to missing view");
                    return;
                }

                view.ViewModel.Close();
                _logger.LogInformation("Combat results window has been closed");
            });
        }

        public void UpdateCombatResultsUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_CombatResultPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update combat results UI due to missing view");
                    return;
                }

                view.m_CloseButton.Interactable = isInteractable;
                var closeButtonText = view.m_CloseButton.GetComponentInChildren<TextMeshProUGUI>();
                _uiSyncCountersService.UpdateButtonTextCounter(closeButtonText, readyPlayersCount, totalPlayersCount);
                _logger.LogInformation("Combat results UI state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateUIState(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var globalMapView = _uiAccessor.GlobalMapPCView;
                if (globalMapView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update UI due to missing GlobalMapView");
                    return;
                }

                var toolbar = globalMapView.m_GlobalMapToolbarPCView;
                toolbar.m_ArmyModeCharacterButton.Interactable = isInteractable;
                toolbar.m_ArmyModeCrusadeButton.Interactable = isInteractable;
                toolbar.m_SkipDay.Interactable = isInteractable;
                toolbar.m_AddArmyCrusadeButton.Interactable = isInteractable;
                toolbar.m_AddArmyCrusadePlusButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(toolbar.m_SkipDayLabel, readyPlayersCount, totalPlayersCount);

                // bottom left list
                var armies = globalMapView.m_ArmiesPCView.m_ArmiesContainer?.gameObject.GetComponentsInChildren<GlobalMapCrusadeArmyPCView>() ?? [];
                foreach (var army in armies)
                {
                    army.m_SelectButton.Interactable = isInteractable;
                    army.m_SettingsButton.Interactable = isInteractable;
                }

                var armyHud = globalMapView.m_ArmyInfoHUDPCView;
                if (armyHud?.ViewModel != null)
                {
                    armyHud.m_InfoButton.Interactable = isInteractable;
                }

                var menuView = globalMapView.m_GlobalMapMenuPCView;
                if (menuView?.ViewModel != null)
                {
                    menuView.m_KingdomButton.Interactable = isInteractable;
                    menuView.m_RecruitButton.Interactable = isInteractable;
                }

                var markersView = globalMapView.m_GlobalMapArmyPointerMarkerPCView;
                if (markersView?.m_Items != null)
                {
                    foreach (var marker in markersView.m_Items.Values)
                    {
                        marker.m_Button.Interactable = isInteractable;
                    }
                }

                _logger.LogInformation("UI state has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void AcceptCommonPopup(NetworkGlobalMapCommonPopup globalMapCommonPopup)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage?.ViewModel != null)
                {
                    modalMessage.m_AcceptButton.OnLeftClick?.Invoke();
                    _logger.LogInformation("Global map common popup has been accepted");
                    return;
                }
            });
        }

        public void CloseLocationMessageBox()
        {
            _mainThreadAccessor.Post(() =>
            {
                var messageBoxView = _uiAccessor.GlobalMapPCView.m_GlobalMapEnterMessagePCView;
                if (messageBoxView?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close missing global map message box");
                    return;
                }

                messageBoxView.ViewModel.Close();
                _logger.LogInformation("Global map message box has been closed");
            });
        }

        public void EnterLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = _gameStateLookupService.GetGlobalMapPoint(globalMapLocation);
                if (point == null || GlobalMapView.Instance.State.Player.Location != point)
                {
                    _logger.LogError("Unable to enter desynced global map location. ExpectedLocationId={ExpectedLocationId}, ExpectedLocationName={ExpectedLocationName}, ActualLocationId={ActualLocationId}, ActualLocationName={ActualLocationName}", globalMapLocation.Id, globalMapLocation.Name, point?.Blueprint.AssetGuid.ToString(), point?.Blueprint.name);
                    return;
                }

                _uiAccessor.GlobalMapPCView?.m_GlobalMapEnterMessagePCView?.ViewModel?.Close();

                GlobalMapView.Instance.EnterLocation();
                _logger.LogInformation("Global map location has been entered. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
            });
        }

        public void AvoidEncounter()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    _logger.LogWarning("Unable to avoid global map encounter when global map view is not available");
                    return;
                }

                _uiAccessor.GlobalMapPCView.m_GlobalMapRandomEncounterPCView.ViewModel.Avoid();
                _logger.LogInformation("Global map encounter has been avoided");
            });
        }

        public void AcceptEncounter()
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    _logger.LogWarning("Unable to avoid global map encounter when global map view is not available");
                    return;
                }

                _uiAccessor.GlobalMapPCView.m_GlobalMapRandomEncounterPCView.ViewModel.Accept();
                _logger.LogInformation("Global map encounter has been accepted");
            });
        }

        public void RollEncounter(NetworkGlobalMapEncounter globalMapEncounter)
        {
            _mainThreadAccessor.Post(() =>
            {
                try
                {
                    var encounter = BlueprintRoot.Instance.RE.Encounters.FirstOrDefault(e => string.Equals(e.AssetGuid.ToString(), globalMapEncounter.BlueprintId));
                    if (encounter == null)
                    {
                        _logger.LogError("Unable to find global map encounter. BlueprintId={BlueprintId}", globalMapEncounter.BlueprintId);
                        return;
                    }

                    var position = new Vector3(globalMapEncounter.Position.X, globalMapEncounter.Position.Y, globalMapEncounter.Position.Z);
                    var combatRandomEncounterData = new CombatRandomEncounterData(encounter, position)
                    {
                        IsTraderRE = globalMapEncounter.IsTrader,
                        AvoidanceCheckResult = (RandomEncounterAvoidanceCheckResult)Enum.Parse(typeof(RandomEncounterAvoidanceCheckResult), globalMapEncounter.AvoidanceResult, true)
                    };
                    var component = encounter.AreaEntrance.Area.GetComponent<CombatRandomEncounterAreaSettings>();
                    combatRandomEncounterData.RandomCombat = CombatRandomEncountersGenerator.GenerateRandomEncounter(globalMapEncounter.Seed, GlobalMapView.Instance.CurrentZone, component, null);
                    RandomEncountersController.State.Player.StartEncounter(combatRandomEncounterData);
                    Game.Instance.RandomEncountersController.m_LastStartedEncounter = RandomEncountersController.State.Player.CurrentEncounterData;
                    Game.Instance.Player.REManager.OnGlobalMapEncounterStarted(encounter, false);
                    _logger.LogInformation("Global map encounter has been rolled. BlueprintId={BlueprintId}, Seed={Seed}, AvoidanceResult={AvoidanceResult}", globalMapEncounter.BlueprintId, globalMapEncounter.Seed, globalMapEncounter.AvoidanceResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while rolling global map encounter. BlueprintId={BlueprintId}, Seed={Seed}, AvoidanceResult={AvoidanceResult}", globalMapEncounter.BlueprintId, globalMapEncounter.Seed, globalMapEncounter.AvoidanceResult);
                    throw;
                }
            });
        }

        public void SkipDay()
        {
            _mainThreadAccessor.Post(() =>
            {
                var toolbarView = _uiAccessor.GlobalMapPCView?.m_GlobalMapToolbarPCView;
                if (toolbarView?.ViewModel == null)
                {
                    _logger.LogError("Unable to skip day due to missing global map toolbar view");
                    return;
                }

                toolbarView.ViewModel.SkipDay();
                _logger.LogInformation("Global map day has been skipped");
            });
        }

        public void SetSelectedArmy(NetworkGlobalMapArmy globalMapArmy)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (globalMapArmy == null)
                {
                    Game.Instance.GlobalMapController.SetSelectedArmy(null);
                    return;
                }

                var armyState = _gameStateLookupService.GetGlobalMapArmy(globalMapArmy.Id);
                if (armyState == null)
                {
                    _logger.LogError("Unable to find army. ArmyId={ArmyId}", globalMapArmy.Id);
                    return;
                }

                Game.Instance.GlobalMapController.SetSelectedArmy(armyState);
                _logger.LogInformation("Selected army has been updated. ArmyId={ArmyId}", armyState.Id);
            });
        }

        public void ChangeArmyMode(NetworkGlobalMapTravelerMode travelerMode)
        {
            _mainThreadAccessor.Post(() =>
            {
                var viewModel = _uiAccessor.GlobalMapPCView?.m_GlobalMapToolbarPCView?.ViewModel;
                if (viewModel == null)
                {
                    _logger.LogWarning("Unable to change army mode due to missing view model. TravelerMode={TravelerMode}", travelerMode);
                    return;
                }

                viewModel.ArmyMode.Value = travelerMode == NetworkGlobalMapTravelerMode.Army;
                _logger.LogInformation("Army mode has been changed. IsArmyMode={IsArmyMode}, TravelerMode={TravelerMode}", viewModel.ArmyMode.Value, travelerMode);
            });
        }

        public void SetAutoCrusadeCombat(bool isEnabled)
        {
            _mainThreadAccessor.Post(() =>
            {
                var settingsView = _uiAccessor.GlobalMapPCView?.m_GlobalMapToolbarPCView?.m_SettingsView;
                // settings are not opened -> update UI settings directly
                if (settingsView?.ViewModel == null)
                {
                    Game.Instance.Player.UISettings.AutoTacticalCombat = isEnabled;
                    _logger.LogInformation("Auto crusade combat setting has been updated directly. IsEnabled={IsEnabled}", isEnabled);
                    return;
                }

                // update via view to make it visible for current player
                settingsView.m_AutoTacticalCombat.ViewModel.IsOn.Value = isEnabled;
                _logger.LogInformation("Auto crusade combat setting has been updated via view. IsEnabled={IsEnabled}", isEnabled);
            });
        }

        public void RunSplitRequestForCrusadeArmySquad(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(sourceSquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to run split request for crusade army squad due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position);
                    return;
                }

                var (targetView, targetSquadVM) = GetArmyInfoSquadVM(targetSquadSlot);
                if (targetSquadVM == null)
                {
                    _logger.LogWarning("Unable to run split request for crusade army squad due to missing target squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", targetSquadSlot.ArmyId, targetSquadSlot.Position);
                    return;
                }

                var errros = sourceView.ViewModel.m_State.Data.MergeSquads(sourceSquadVM.SquadPosition, targetSquadVM.SquadPosition, count);
                sourceView.ViewModel.SquadErrorsTrigger.Execute(errros);
                _logger.LogInformation("Split request for crusade army squad completed. SourceArmyId={SourceArmyId}, SourceSquadPosition={SourceSquadPosition}, TargetArmyId={TargetArmyId}, TargetSquadPosition={TargetSquadPosition}, Count={Count}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position, count);
            });
        }

        public void SwitchCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(sourceSquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to switch crusade army squads due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position);
                    return;
                }

                var (targetView, targetSquadVM) = GetArmyInfoSquadVM(targetSquadSlot);
                if (targetSquadVM == null)
                {
                    _logger.LogWarning("Unable to switch crusade army squads due to missing target squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", targetSquadSlot.ArmyId, targetSquadSlot.Position);
                    return;
                }

                sourceView.ViewModel.SwitchSquads(sourceSquadVM, targetSquadVM);
                _logger.LogInformation("Crusade army squads have been switched. SourceArmyId={SourceArmyId}, SourceSquadPosition={SourceSquadPosition}, TargetArmyId={TargetArmyId}, TargetSquadPosition={TargetSquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position);
            });
        }

        public void MergeCrusadeArmySquads(NetworkGlobalMapArmySquadSlot sourceSquadSlot, NetworkGlobalMapArmySquadSlot targetSquadSlot, int count)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(sourceSquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to merge crusade army squads due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position);
                    return;
                }

                var (targetView, targetSquadVM) = GetArmyInfoSquadVM(targetSquadSlot);
                if (targetSquadVM == null)
                {
                    _logger.LogWarning("Unable to merge crusade army squads due to missing target squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", targetSquadSlot.ArmyId, targetSquadSlot.Position);
                    return;
                }

                sourceView.ViewModel.MergeSquads(sourceSquadVM, targetSquadVM, count);
                _logger.LogInformation("Crusade army squads have been merged. SourceArmyId={SourceArmyId}, SourceSquadPosition={SourceSquadPosition}, TargetArmyId={TargetArmyId}, TargetSquadPosition={TargetSquadPosition}, Count={Count}", sourceSquadSlot.ArmyId, sourceSquadSlot.Position, targetSquadSlot.ArmyId, targetSquadSlot.Position, count);
            });
        }

        public void SplitCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot, int count)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(globalMapArmySquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to split crusade army squads due to missing squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                    return;
                }

                sourceSquadVM.Army.Split(sourceSquadVM.Squad, count);
                _logger.LogInformation("Crusade army squad has been split. ArmyId={ArmyId}, SquadPosition={SquadPosition}, Count={Count}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position, count);
            });
        }

        public void MergeInOneCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(globalMapArmySquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to merge in one crusade army squads due to missing squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                    return;
                }

                sourceSquadVM.Army.MergeInOne(sourceSquadVM.Squad);
                _logger.LogInformation("Crusade army squad has been merged in one. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
            });
        }

        public void DismissCrusadeArmySquad(NetworkGlobalMapArmySquadSlot globalMapArmySquadSlot)
        {
            _mainThreadAccessor.Post(() =>
            {
                var (sourceView, sourceSquadVM) = GetArmyInfoSquadVM(globalMapArmySquadSlot);
                if (sourceSquadVM == null)
                {
                    _logger.LogWarning("Unable to dismiss crusade army squads due to missing source squad vm. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
                    return;
                }

                ArmyDismissManager.Instance.DismissSquad(sourceSquadVM.Army, sourceSquadVM.Squad);
                _logger.LogInformation("Crusade army squad has been dismissed. ArmyId={ArmyId}, SquadPosition={SquadPosition}", globalMapArmySquadSlot.ArmyId, globalMapArmySquadSlot.Position);
            });
        }

        public void DismissCrusadeArmy(NetworkGlobalMapArmy globalMapArmy)
        {
            _mainThreadAccessor.Post(() =>
            {
                var army = _gameStateLookupService.GetGlobalMapArmy(globalMapArmy.Id);
                if (army == null)
                {
                    _logger.LogWarning("Unable to dismiss missing crusade army. ArmyId={ArmyId}", globalMapArmy.Id);
                    return;
                }

                ArmyDismissManager.Instance.DismissArmy(army.Data);
                _logger.LogInformation("Crusade army has been dismissed. ArmyId={ArmyId}", army.Id);
            });
        }

        public void UpdateCrusadeArmyInfoUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to update crusade army info due to missing view");
                    return;
                }

                view.m_NextMergeArmy.Interactable = view.ViewModel.HaveNextArmyMerge.Value && isInteractable;
                view.m_PrevMergeArmy.Interactable = view.ViewModel.HavePrevArmyMerge.Value && isInteractable;
                view.m_CreateArmyButton.Interactable = isInteractable;
                view.m_RecruitArmyButton.Interactable = isInteractable;
                view.m_MoveSquadsToMainButton.Interactable = isInteractable;
                view.m_MoveSquadsToSecondButton.Interactable = isInteractable;

                var setLeaderView = view.m_SetLeaderView.m_LeaderInfoView;
                UpdateLeaderInfoUIState((ArmyLeaderInfoPCView)setLeaderView, isInteractable, readyPlayersCount, totalPlayersCount);

                var mainCartView = view.m_MainArmyCartView;
                UpdateArmyCartView(mainCartView, isInteractable, readyPlayersCount, totalPlayersCount);

                var mergeCartView = view.m_MergeArmyCartView;
                UpdateArmyCartView(mergeCartView, isInteractable, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Crusade army info ui has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        private void UpdateArmyCartView(ArmyInfoArmyCartView armyInfoArmyCartView, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            if (armyInfoArmyCartView?.ViewModel == null)
            {
                return;
            }

            var view = (ArmyInfoArmyCartPCView)armyInfoArmyCartView;
            if (armyInfoArmyCartView?.ViewModel != null)
            {
                view.m_CloseButton.Interactable = isInteractable;
                view.m_DismissButton.Interactable = isInteractable;

                UpdateLeaderInfoUIState((ArmyLeaderInfoPCView)view.m_LeaderInfoView, isInteractable, readyPlayersCount, totalPlayersCount);
            }
        }

        public void CloseCrusadeArmyInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army info due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army info close handler has been executed");
            });
        }

        public void CloseCrusadeArmyMainInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView?.m_MainArmyCartView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army main info due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army main info close handler has been executed");
            });
        }

        public void CloseCrusadeArmyMergeInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView?.m_MergeArmyCartView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to close crusade army merge info due to missing view");
                    return;
                }

                view.ViewModel.OnClose();
                _logger.LogInformation("Crusade army info close handler has been executed");
            });
        }

        public void OpenCrusadeArmyInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView.m_ArmyInfoHUDPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to open crusade army info due to missing view");
                    return;
                }

                if (_uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView?.ViewModel != null)
                {
                    _logger.LogWarning("Army info is already opened");
                    return;
                }

                view.ViewModel.OnInfoRequest();
                _logger.LogInformation("Crusade army info has been opened");
            });
        }

        public void MoveCrusadeArmySquadsToMainArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to move crusade army squads to main army due to missing view");
                    return;
                }

                view.ViewModel.MoveSquadsToMainArmy();
                _logger.LogInformation("Crusade army squads have been moved to main army");
            });
        }

        public void MoveCrusadeArmySquadsToSecondArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to move crusade army squads to second army due to missing view");
                    return;
                }

                view.ViewModel.MoveSquadsToSecondArmy();
                _logger.LogInformation("Crusade army squads have been moved to second army");
            });
        }

        public void SelectPrevCrusadeArmyInfoMergeArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select prev crusade army merge due to missing view");
                    return;
                }

                view.ViewModel.PrevMergeArmy();
                _logger.LogInformation("Prev army for crusade army merge has been selected");
            });
        }

        public void SelectNextCrusadeArmyInfoMergeArmy()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to select next crusade army merge due to missing view");
                    return;
                }

                view.ViewModel.NextMergeArmy();
                _logger.LogInformation("Next army for crusade army merge has been selected");
            });
        }

        public void RunMainLeaderAction()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView.m_MainArmyCartView?.m_LeaderInfoView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to run main leader action due to missing view");
                    return;
                }

                view.ViewModel.OnClick();
                _logger.LogInformation("Main leader action has been executed");
            });
        }

        public void RunMergeLeaderAction()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView.m_MergeArmyCartView?.m_LeaderInfoView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to run merge leader action due to missing view");
                    return;
                }

                view.ViewModel.OnClick();
                _logger.LogInformation("Merge leader action has been executed");
            });
        }

        public void LevelUpMergeLeader()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView.m_MergeArmyCartView?.m_LeaderInfoView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to levelup merge leader due to missing view");
                    return;
                }

                view.ViewModel.OnLevelUp();
                _logger.LogInformation("Merge leader has started leveling process");
            });
        }

        public void LevelUpMainLeader()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView.m_MainArmyCartView?.m_LeaderInfoView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to levelup main leader due to missing view");
                    return;
                }

                view.ViewModel.OnLevelUp();
                _logger.LogInformation("Main leader has started leveling process");
            });
        }

        public void LookAtPoolForMainLeader()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView.m_MainArmyCartView?.m_LeaderInfoView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to look at pool for main leader due to missing view");
                    return;
                }

                view.ViewModel.OnLookAtLeaderPool();
                _logger.LogInformation("Main leader pool has been opened");
            });
        }

        public void LookAtPoolForMergeLeader()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView.m_MainArmyCartView?.m_LeaderInfoView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to look at pool for merge leader due to missing view");
                    return;
                }

                view.ViewModel.OnLookAtLeaderPool();
                _logger.LogInformation("Merge leader pool has been opened");
            });
        }

        public void OpenCrusadeArmiesMergeScreen()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to open merge screen due to missing global map view");
                    return;
                }

                view.ViewModel.GlobalMapArmyOvertipsVM.MergeArmies();
                _logger.LogInformation("Merge armies screen has been opened");
            });
        }

        public void CreateArmyAtCrusadeArmyInfo()
        {
            _mainThreadAccessor.Post(() =>
            {
                var view = _uiAccessor.GlobalMapPCView?.m_ArmyInfoPCView;
                if (view?.ViewModel == null)
                {
                    _logger.LogWarning("Unable to create army at crusade info due to missing view");
                    return;
                }

                // view call to play sound
                view.CreateArmy();
                _logger.LogInformation("Crusade army has been created at crusade info screen");
            });
        }

        private void UpdateLeaderInfoUIState(ArmyLeaderInfoPCView view, bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            if (view?.ViewModel == null)
            {
                return;
            }

            view.m_LevelUpButton.Interactable = isInteractable;
            view.m_Button.Interactable = isInteractable;
            _uiSyncCountersService.UpdateButtonTextCounter(view.m_ButtonText, readyPlayersCount, totalPlayersCount);

            view.m_EmptyButton.Interactable = isInteractable;
            _uiSyncCountersService.UpdateButtonTextCounter(view.m_EmptyButtonText, readyPlayersCount, totalPlayersCount);
        }

        private (ArmySquadsPCView view, ArmyInfoSquadVM squadVM)? GetArmyInfoSquadVM(ArmySquadsPCView armySquadsView, NetworkGlobalMapArmySquadSlot mapArmySquadSlot)
        {
            if (armySquadsView?.ViewModel == null)
            {
                return null;
            }

            var position = new Vector2Int(mapArmySquadSlot.Position.X, mapArmySquadSlot.Position.Y);
            var squadView = armySquadsView?.m_Squads.FirstOrDefault(v => v.ViewModel != null && string.Equals(v.ViewModel.Army.ArmyStateId, mapArmySquadSlot.ArmyId, StringComparison.OrdinalIgnoreCase) && v.ViewModel.SquadPosition == position);
            if (squadView?.ViewModel == null)
            {
                return null;
            }

            return (armySquadsView, squadView.ViewModel);
        }

        private (ArmySquadsPCView view, ArmyInfoSquadVM) GetArmyInfoSquadVM(NetworkGlobalMapArmySquadSlot mapArmySquadSlot)
        {
            var globalMapView = _uiAccessor.GlobalMapPCView;

            var squad = GetArmyInfoSquadVM((ArmySquadsPCView)globalMapView.m_ArmyInfoPCView?.m_MainArmyCartView?.m_SquadsView, mapArmySquadSlot)
                ?? GetArmyInfoSquadVM((ArmySquadsPCView)globalMapView.m_ArmyInfoPCView?.m_MergeArmyCartView?.m_SquadsView, mapArmySquadSlot)
                ?? GetArmyInfoSquadVM((ArmySquadsPCView)globalMapView.m_ArmyInfoHUDPCView?.m_SquadView, mapArmySquadSlot);

            if (squad == null)
            {
                return (null, null);
            }

            return squad.Value;
        }

        private void UpdateTraveler(NetworkGlobalMapTraveler traveler)
        {
            var selectedTraveler = Game.Instance.GlobalMapController.SelectedTraveler;
            UpdateTraveler(selectedTraveler, traveler);
        }

        private void UpdateTraveler(IGlobalMapTraveler globalMapTraveler, NetworkGlobalMapTraveler traveler)
        {
            if (globalMapTraveler == null || traveler == null)
            {
                return;
            }

            if (globalMapTraveler is GlobalMapArmyState armyState && traveler.MovementPoints.HasValue && armyState.MovementPoints != traveler.MovementPoints.Value)
            {
                _logger.LogInformation("Updated army movement points. ArmyId={ArmyId}, OldValue={OldValue}, NewValue={NewValue}", armyState.Id, armyState.MovementPoints, traveler.MovementPoints.Value);
                armyState.MovementPoints = traveler.MovementPoints.Value;
            }

            if (globalMapTraveler.Position.EdgePosition != traveler.Position.EdgePosition)
            {
                _logger.LogInformation("Updated traveler edge position. OldValue={OldValue}, NewValue={NewValue}", globalMapTraveler.Position.EdgePosition, traveler.Position.EdgePosition);
                globalMapTraveler.Position.EdgePosition = traveler.Position.EdgePosition;
            }
        }
    }
}

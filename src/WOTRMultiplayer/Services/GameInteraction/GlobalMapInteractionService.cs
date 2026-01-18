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
using Kingmaker.RandomEncounters;
using Kingmaker.RandomEncounters.Settings;
using Kingmaker.UI.MVVM._PCView.Crusade.Armies;
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

        public void ContinueTravel(NetworkGlobalMapState globalMapState)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateGlobalMapState(globalMapState);

                GlobalMapUI.Instance.OnContinue();
            });
        }

        public void StopTravel(NetworkGlobalMapState globalMapState)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateGlobalMapState(globalMapState);

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

                _logger.LogInformation("Global Map Common Popup buttons have been updated. Type={Type}, IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", globalMapCommonPopup.Type, isInteractable, readyPlayersCount, totalPlayersCount);
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
                _logger.LogInformation("AutoBattleResults window has been closed");
            });
        }

        public void StartCrusadeArmyAutoBattleResultsManualCombat()
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
                _logger.LogInformation("Manual combat has been started via AutoBattleResults");
            });
        }

        public void CloseCombatBattleResults()
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

        public void SetSelectedArmy(string armyId)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (string.IsNullOrEmpty(armyId))
                {
                    Game.Instance.GlobalMapController.SetSelectedArmy(null);
                    return;
                }

                var army = _gameStateLookupService.GetGlobalMapArmy(armyId);
                if (army == null)
                {
                    _logger.LogError("Unable to find army. ArmyId={ArmyId}", armyId);
                    return;
                }

                Game.Instance.GlobalMapController.SetSelectedArmy(army);
                _logger.LogInformation("Selected army has been updated. ArmyId={ArmyId}", armyId);
            });
        }

        public void ChangeArmyMode(NetworkGlobalMapTravelerMode travelerMode)
        {
            _mainThreadAccessor.Post(() =>
            {
                var viewModel = _uiAccessor.GlobalMapPCView?.m_GlobalMapToolbarPCView?.ViewModel;
                if (viewModel == null)
                {
                    _logger.LogError("Unable to change army mode due to missing view model. TravelerMode={TravelerMode}", travelerMode);
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

        private void UpdateGlobalMapState(NetworkGlobalMapState globalMapState)
        {
            // not sure if player position is unavailable while army is selected (act2+), need to check later
            if (globalMapState.Player?.Position != null)
            {
                GlobalMapView.Instance.State.Player.TravelData.EdgePosition = globalMapState.Player.Position.Edge;
            }
        }
    }
}

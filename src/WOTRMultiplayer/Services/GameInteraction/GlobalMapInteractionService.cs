using System;
using System.Linq;
using Kingmaker;
using Kingmaker.Assets.Controllers.GlobalMap;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Rest;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Globalmap.View;
using Kingmaker.PubSubSystem;
using Kingmaker.RandomEncounters;
using Kingmaker.RandomEncounters.Settings;
using Kingmaker.UI.Common;
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
        private readonly IUIAccessor _uiAccessor;
        private readonly IUISyncCountersService _uiSyncCountersService;

        public GlobalMapInteractionService(
            ILogger<GlobalMapInteractionService> logger,
            IMainThreadAccessor mainThreadAccessor,
            IUIAccessor uiAccessor,
            IUISyncCountersService uiSyncCountersService)
        {
            _logger = logger;
            _mainThreadAccessor = mainThreadAccessor;
            _uiAccessor = uiAccessor;
            _uiSyncCountersService = uiSyncCountersService;
        }

        public void OpenGlobalMapRestMenu()
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

        public void StartGlobalMapTravel(NetworkGlobalMapLocation destination)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = GetGlobalMapPoint(destination.Id);
                if (point == null)
                {
                    _logger.LogError("Unable to find global map point. PointId={PointId}, PointName={PointName}", destination.Id, destination.Name);
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                modalMessage.ViewModel?.OnDeclinePressed();
                var messageBoxView = _uiAccessor.GlobalMapPCView.m_GlobalMapEnterMessagePCView;
                messageBoxView.ViewModel?.Close();

                var traveler = Game.Instance.GlobalMapController.SelectedTraveler;
                var globalMapTravelData = GlobalMapView.Instance.State.PathManager.CalculateTravelerPathToLocation(traveler, point.Blueprint);
                traveler.StartTravel(globalMapTravelData, true);
                _logger.LogInformation("Global map traveler has been started. Destination={DestinationId}, DestinationName={DestinationName}", point.Blueprint.AssetGuid.ToString(), point.name);
            });
        }

        public bool IsAtGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            var targetPoint = GetGlobalMapPoint(globalMapLocation.Id);
            return targetPoint != null && GlobalMapView.Instance.State.Player.Location == targetPoint.Blueprint;
        }

        public void ContinueGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateGlobalMapState(globalMapState);

                GlobalMapUI.Instance.OnContinue();
            });
        }

        public void StopGlobalMapTravel(NetworkGlobalMapState globalMapState)
        {
            _mainThreadAccessor.Post(() =>
            {
                UpdateGlobalMapState(globalMapState);

                GlobalMapUI.Instance.OnStop();
            });
        }

        public void UpdateGlobalMapMessageBoxUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
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

                var buttonText = messageBoxView.m_AcceptButton.GetComponentInChildren<TextMeshProUGUI>();
                if (messageBoxView.ViewModel.IsCurrentLocation)
                {
                    _uiSyncCountersService.UpdateButtonTextCounter(buttonText, readyPlayersCount, totalPlayersCount);
                }
                else
                {
                    _uiSyncCountersService.RemoveButtonTextCounter(buttonText);
                }

                _logger.LogInformation("Global Map Message box accept button has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateGlobalMapIngredientCollectionUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView?.ViewModel == null)
                {
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                modalMessage.m_AcceptButton.Interactable = isInteractable;
                _uiSyncCountersService.UpdateButtonTextCounter(modalMessage.m_AcceptText, readyPlayersCount, totalPlayersCount);

                _logger.LogInformation("Global Map Ingredient Collection Accept button has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void UpdateGlobalMapEncounterMessageUI(bool isInteractable, int readyPlayersCount, int totalPlayersCount)
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

                _logger.LogInformation("Global Map Encounter Message has been updated. IsInteractable={IsInteractable}, ReadyPlayers={ReadyPlayers}, TotalPlayers={TotalPlayers}", isInteractable, readyPlayersCount, totalPlayersCount);
            });
        }

        public void CollectGlobalMapIngredients(NetworkGlobalMapLocation globalMapLocation)
        {
            _mainThreadAccessor.Post(() =>
            {
                if (_uiAccessor.GlobalMapPCView == null)
                {
                    return;
                }

                var modalMessage = _uiAccessor.CommonPCView?.m_MessageModalPCView;
                if (modalMessage != null)
                {
                    modalMessage.m_AcceptButton.OnLeftClick?.Invoke();
                    _logger.LogInformation("Global map ingredients have been collected via Accept button");
                    return;
                }

                // no message box means client closed his message box right before host clicked accept
                // autocollecting items since we are at the same place anyway
                var point = GetGlobalMapPoint(globalMapLocation.Id);
                if (point == null)
                {
                    _logger.LogError("Unable to autocollect global map ingredients due to missing point. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
                    return;
                }

                var craftRoot = BlueprintRoot.Instance.CraftRoot.CollectRoot;
                var collected = craftRoot.CollectIngredient(point.Blueprint);
                var warningMessage = collected.Count > 0 ? craftRoot.SuccessCollect : craftRoot.FailCollected;
                UIUtility.SendWarning(warningMessage, addLog: false);
                EventBus.RaiseEvent<ILogMessageUIHandler>(x => x.HandleLogMessage(collected.Count > 0 ? $"{craftRoot.SuccessCollect}:\n{BlueprintGlobalMapPoint.IngredientToString(collected)}" : (string)craftRoot.FailCollected));
                point.State.IngredientWasCollected = true;
                point.State.SetVisited();

                _logger.LogInformation("Global map ingredients have been collected via auto collection. LocationId={LocationId}, LocationName={LocationName}", globalMapLocation.Id, globalMapLocation.Name);
            });
        }

        public void EnterGlobalMapLocation(NetworkGlobalMapLocation globalMapLocation)
        {
            _mainThreadAccessor.Post(() =>
            {
                var point = GetGlobalMapPoint(globalMapLocation.Id);
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

        public void AvoidGlobalMapEncounter()
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

        public void AcceptGlobalMapEncounter()
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

        public void RollGlobalMapEncounter(NetworkGlobalMapEncounter globalMapEncounter)
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

        private void UpdateGlobalMapState(NetworkGlobalMapState globalMapState)
        {
            // not sure if player position is unavailable while army is selected (act2+), need to check later
            if (globalMapState.Player?.Position != null)
            {
                GlobalMapView.Instance.State.Player.TravelData.EdgePosition = globalMapState.Player.Position.Edge;
            }
        }

        private GlobalMapPointView GetGlobalMapPoint(string pointId)
        {
            var point = GlobalMapView.Instance.Points.FirstOrDefault(p => string.Equals(p.Blueprint.AssetGuid.ToString(), pointId, StringComparison.OrdinalIgnoreCase));
            return point;
        }
    }
}

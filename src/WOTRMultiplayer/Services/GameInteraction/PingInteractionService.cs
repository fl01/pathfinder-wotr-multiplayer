using System;
using System.Linq;
using Kingmaker;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Blueprints;
using Kingmaker.Armies.TacticalCombat.Grid;
using Kingmaker.Controllers.Clicks;
using Kingmaker.Globalmap.View;
using Kingmaker.UI;
using Kingmaker.UI.Kingdom;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.PointMarker;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.MapObjects;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Utility;
using Owlcat.Runtime.Visual.RenderPipeline.RendererFeatures.Highlighting;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.GlobalMap;
using WOTRMultiplayer.Entities.MapObjects;
using WOTRMultiplayer.Entities.Ping;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI;
using WOTRMultiplayer.UnityBehaviours.Ping;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class PingInteractionService : IPingInteractionService
    {
        public const string MarkerObjectName = "PingPointMarker";

        private readonly ILogger<PingInteractionService> _logger;
        private readonly IMainThreadAccessor _mainThreadAccessor;
        private readonly IUIAccessor _uiAccessor;
        private readonly IGameStateLookupService _gameStateLookupService;
        private readonly IResourceProvider _resourceProvider;

        public PingInteractionService(
            ILogger<PingInteractionService> logger,
            IGameStateLookupService gameStateLookupService,
            IResourceProvider resourceProvider,
            IMainThreadAccessor mainThreadAccessor,
            IUIAccessor uiAccessor)
        {
            _logger = logger;
            _gameStateLookupService = gameStateLookupService;
            _resourceProvider = resourceProvider;
            _mainThreadAccessor = mainThreadAccessor;
            _uiAccessor = uiAccessor;
        }

        public NetworkPing Get()
        {
            if (PointerController.InGui)
            {
                var guiPing = GetPingedGuiElement();
                return guiPing;
            }

            var pointer = PointerController.PointerPosition;
            Game.Instance.DefaultPointerController.SelectClickObject(pointer, out var gameObject, out var worldPosition, out _);
            if (worldPosition == Vector3.zero && gameObject == null)
            {
                return null;
            }

            if (RootUIContext.Instance.IsGlobalMap)
            {
                var guiPing = GetGlobalMapPing(gameObject);
                return guiPing;
            }
            else if (TacticalCombatHelper.IsActive && Game.Instance.CurrentlyLoadedArea is BlueprintTacticalCombatArea)
            {
                var unit = TacticalCombatGridHelper.TryGetUnitUnderCursor();
                if (unit != null)
                {
                    gameObject = unit.View.gameObject;
                }
            }

            var point = worldPosition.ToNetworkVector3();
            var unitId = gameObject?.GetComponent<UnitEntityView>()?.Data?.UniqueId;
            var mapObjectData = gameObject?.GetComponent<MapObjectView>()?.Data;
            var ping = new NetworkPing
            {
                WorldPosition = point,
                UnitId = unitId,
                MapObject = Main.Mapper.Map<NetworkMapObject>(mapObjectData)
            };

            if (ping.MapObject != null)
            {
                ping.Type = NetworkPingType.MapObject;
            }
            else if (!string.IsNullOrEmpty(ping.UnitId))
            {
                ping.Type = NetworkPingType.Unit;
            }
            else
            {
                ping.Type = NetworkPingType.WorldPosition;
            }

            return ping;
        }

        public void Create(NetworkPlayer player, NetworkPing ping)
        {
            _mainThreadAccessor.Post(() =>
            {
                switch (ping.Type)
                {
                    case NetworkPingType.WorldPosition:
                        var position = ping.WorldPosition.ToUnityVector3();
                        CreateWorldPositionPing(player, position);
                        break;
                    case NetworkPingType.Unit:
                        CreateUnitPing(player, ping);
                        break;
                    case NetworkPingType.MapObject:
                        CreateMapObjectPing(player, ping);
                        break;
                    case NetworkPingType.GlobalMapLocation:
                        CreateGlobalMapLocationPing(ping);
                        break;
                    case NetworkPingType.GlobalMapArmyPawn:
                        CreateGlobalMapArmyPawnPing(ping);
                        break;
                    case NetworkPingType.GlobalMapKingdomSettlement:
                        CreateGlobalMapKingdomSettlementPing(ping);
                        break;
                    default:
                        return;
                }
            });
        }

        private void CreateUnitPing(NetworkPlayer player, NetworkPing ping)
        {
            var unit = _gameStateLookupService.GetUnitEntity(ping.UnitId);
            if (unit == null)
            {
                _logger.LogWarning("Unable to enable ping missing unit. UnitId={UnitId}", ping.UnitId);
                return;
            }

            CreateWorldPositionPing(player, unit.Position);
            var unitGameObject = unit.View.gameObject;
            var isEnemyUnit = unit.IsPlayersEnemy || TacticalCombatHelper.IsDemon(unit);
            CreateWorldEntityPing(isEnemyUnit, unitGameObject);
        }

        private void CreateMapObjectPing(NetworkPlayer player, NetworkPing ping)
        {
            var mapObject = _gameStateLookupService.GetMapObject(ping.MapObject.Id) ?? _gameStateLookupService.GetNeareastLootBagMapObject(ping.WorldPosition, 20.Feet().Meters);
            if (mapObject == null)
            {
                _logger.LogWarning("Unable to enable ping missing map object. MapObjectId={MapObjectId}", ping.UnitId);
                return;
            }

            CreateWorldPositionPing(player, mapObject.Position);
            var mapGameObject = mapObject.View.gameObject;
            CreateWorldEntityPing(false, mapGameObject);
        }

        private void CreateGlobalMapLocationPing(NetworkPing ping)
        {
            var point = _gameStateLookupService.GetGlobalMapPoint(ping.GlobalMapLocation);
            if (point == null)
            {
                _logger.LogWarning("Unable to enable ping missing global map point. LocationId={LocationId}", ping.GlobalMapLocation.Id);
                return;
            }

            var highlighters = point.GetComponentsInChildren<Highlighter>().Select(x => x.gameObject).ToList();
            if (point.GetComponent<Highlighter>() != null)
            {
                highlighters.Add(point.gameObject);
            }

            if (highlighters.Count == 0)
            {
                _logger.LogWarning("Unable to enable ping global map point due to missing child with highlighter component. LocationId={LocationId}", ping.GlobalMapLocation.Id);
                return;
            }

            foreach (var highlighter in highlighters)
            {
                CreateWorldEntityPing(false, highlighter);
            }

            PlayPingSound(point.gameObject);
        }

        private void CreateGlobalMapArmyPawnPing(NetworkPing ping)
        {
            var armyPawn = _gameStateLookupService.GetGlobalMapArmyPawn(ping.GlobalMapArmy);
            if (armyPawn == null)
            {
                _logger.LogWarning("Unable to enable ping missing global map army pawn. PawnId={PawnId}", ping.GlobalMapArmy.Id);
                return;
            }

            if (!armyPawn.State.IsRevealed)
            {
                return;
            }

            var isEnemyArmy = armyPawn.State?.Data?.Faction != Kingmaker.Armies.ArmyFaction.Crusaders;
            CreateWorldEntityPing(isEnemyArmy, armyPawn.gameObject);
            PlayPingSound(armyPawn.gameObject);
        }

        private void CreateGlobalMapKingdomSettlementPing(NetworkPing ping)
        {
            var point = _gameStateLookupService.GetGlobalMapPoint(ping.GlobalMapLocation);
            if (point == null)
            {
                _logger.LogWarning("Unable to enable ping global map settlement due to missing parent point. LocationId={LocationId}", ping.GlobalMapLocation.Id);
                return;
            }

            var settlement = point.GetComponentInChildren<KingdomUISettlementMarker>();
            if (settlement == null || !string.Equals(settlement.Settlement.UniqueId, ping.GlobalMapKingdomSettlement.Id, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unable to enable ping global map settlement due to missing valid settlement component. SettlementId={SettlementId}", ping.GlobalMapKingdomSettlement.Id);
                return;
            }

            CreateWorldEntityPing(false, settlement.gameObject);
            PlayPingSound(settlement.gameObject);
        }

        private void CreateWorldEntityPing(bool isEnemy, GameObject gameObject)
        {
            var existing = gameObject.GetComponent<WorldEntityHighlighterBehaviour>();
            if (existing != null)
            {
                existing.RefreshDuration();
                return;
            }

            var pingHighlighter = gameObject.AddComponent<WorldEntityHighlighterBehaviour>();
            pingHighlighter.Begin(isEnemy, TimeSpan.FromSeconds(2));
        }

        private void CreateWorldPositionPing(NetworkPlayer player, Vector3 position)
        {
            if (ClickPointerManager.Instance == null)
            {
                return;
            }

            // this is a placeholder that needs to be replaced with something good
            var pingObject = UnityEngine.Object.Instantiate(ClickPointerManager.Instance.PointerPrefab.gameObject);
            var decayingBehaviour = pingObject.AddComponent<WorldPositionPingBehaviour>();
            var markerDuration = TimeSpan.FromSeconds(2);
            Vector3? scale = TacticalCombatHelper.IsActive ? new Vector3(1f, 3f, 1f) : null;
            decayingBehaviour.Begin(markerDuration, position, scale);
            PlayPingSound(pingObject);

            // no need to create marker if there is no responsible player for it (local player)
            if (player == null || TacticalCombatHelper.IsActive)
            {
                return;
            }

            var outsideOfCameraDuration = TimeSpan.FromMilliseconds(markerDuration.TotalMilliseconds * 2);
            CreateOutsideOfCameraMarker(position, outsideOfCameraDuration);
        }

        private void CreateOutsideOfCameraMarker(Vector3 position, TimeSpan duration)
        {
            var marker = PointMarkerController.Instance.transform.Find(MarkerObjectName)?.GetComponent<PointMarker>();
            if (marker == null)
            {
                marker = WidgetFactory.GetWidget(PointMarkerController.Instance.MarkerPrefab, true, false);
                marker.gameObject.name = MarkerObjectName;
                var markerSprite = _resourceProvider.GetSprite(WellKnownResourceBundles.UI, "UI_QuestNotification_IconNew");
                marker.gameObject
                    .AddComponent<WorldPositionPingOutsideOfCameraBehaviour>()
                    .WithPortrait(markerSprite);
            }

            marker.gameObject
                .GetComponent<WorldPositionPingOutsideOfCameraBehaviour>()
                .Begin(duration, position);
        }

        private void PlayPingSound(GameObject pingObject)
        {
            UISoundController.Instance.Play(UISoundType.GlobalMapLocationsSelect, pingObject);
        }

        private NetworkPing GetPingedGuiElement()
        {
            return null;
        }

        private NetworkPing GetGlobalMapPing(GameObject gameObject)
        {
            var pointView = gameObject.GetComponent<GlobalMapPointView>();
            if (pointView != null)
            {
                var globalMapLocationPing = new NetworkPing
                {
                    GlobalMapLocation = new NetworkGlobalMapLocation
                    {
                        Id = pointView.Blueprint.AssetGuid.ToString(),
                        Name = pointView.Blueprint.name
                    },
                    Type = NetworkPingType.GlobalMapLocation
                };
                return globalMapLocationPing;
            }

            var pawnView = gameObject.GetComponent<GlobalMapArmyPawn>();
            if (pawnView != null)
            {
                var globalMapArmyPing = new NetworkPing
                {
                    GlobalMapArmy = new NetworkGlobalMapArmy
                    {
                        Id = pawnView.Id,
                    },
                    Type = NetworkPingType.GlobalMapArmyPawn
                };
                return globalMapArmyPing;
            }

            var settlementMarker = gameObject.GetComponent<KingdomUISettlementMarker>();
            if (settlementMarker != null)
            {
                var parentPoint = settlementMarker.gameObject.transform.parent.GetComponent<GlobalMapPointView>();
                if (parentPoint != null)
                {
                    var globalMapSettlementPing = new NetworkPing
                    {
                        GlobalMapKingdomSettlement = new NetworkGlobalMapKingdomSettlement
                        {
                            Id = settlementMarker.Settlement.UniqueId,
                        },
                        GlobalMapLocation = new NetworkGlobalMapLocation
                        {
                            Id = parentPoint.Blueprint.AssetGuid.ToString(),
                            Name = parentPoint.Blueprint.name
                        },
                        Type = NetworkPingType.GlobalMapKingdomSettlement
                    };
                    return globalMapSettlementPing;
                }
            }

            return null;
        }
    }
}

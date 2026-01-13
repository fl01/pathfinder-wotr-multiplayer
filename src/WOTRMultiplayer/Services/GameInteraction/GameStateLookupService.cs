using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.View.MapObjects;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class GameStateLookupService : IGameStateLookupService
    {
        public MapObjectEntityData GetMapObject(string uniqueId)
        {
            return Game.Instance.State.MapObjects.All.FirstOrDefault(o => string.Equals(o.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        public UnitEntityData GetUnitEntity(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                return null;
            }

            return Game.Instance.State.Units.All.FirstOrDefault(u => string.Equals(u.UniqueId, uniqueId, StringComparison.OrdinalIgnoreCase));
        }

        public List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position)
        {
            var targetPoint = new Vector3(position.X, position.Y, position.Z);
            var orderedContainers = Game.Instance.State.MapObjects.All
                .Where(o => o.Interactions.Any(i => i is InteractionLootPart))
                .OrderBy(o => (o.Position - targetPoint).magnitude)
                .ToList();

            return orderedContainers;
        }
    }
}

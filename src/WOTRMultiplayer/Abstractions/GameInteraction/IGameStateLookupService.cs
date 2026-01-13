using System.Collections.Generic;
using Kingmaker.EntitySystem.Entities;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Abstractions.GameInteraction
{
    public interface IGameStateLookupService
    {
        UnitEntityData GetUnitEntity(string uniqueId);

        MapObjectEntityData GetMapObject(string uniqueId);

        List<MapObjectEntityData> GetNeareastLootableMapObjects(NetworkVector3 position);
    }
}

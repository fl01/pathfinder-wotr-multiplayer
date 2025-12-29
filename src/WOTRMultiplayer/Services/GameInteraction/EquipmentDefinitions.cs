using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Items.Slots;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.Services.GameInteraction
{
    public class EquipmentDefinitions : IEquipmentDefinitions
    {
        private readonly Dictionary<NetworkEquipmentSlotType, Type> _networkTypeToType = new()
        {
            { NetworkEquipmentSlotType.HandSlot, typeof(HandSlot) },
            { NetworkEquipmentSlotType.UsableSlot, typeof(UsableSlot) },
            { NetworkEquipmentSlotType.ArmorSlot, typeof(ArmorSlot) },
            { NetworkEquipmentSlotType.EquipmentSlotShirt, typeof(EquipmentSlot<BlueprintItemEquipmentShirt>) },
            { NetworkEquipmentSlotType.EquipmentSlotBelt, typeof(EquipmentSlot<BlueprintItemEquipmentBelt>) },
            { NetworkEquipmentSlotType.EquipmentSlotHead, typeof(EquipmentSlot<BlueprintItemEquipmentHead>) },
            { NetworkEquipmentSlotType.EquipmentSlotGlasses, typeof(EquipmentSlot<BlueprintItemEquipmentGlasses>) },
            { NetworkEquipmentSlotType.EquipmentSlotFeet, typeof(EquipmentSlot<BlueprintItemEquipmentFeet>) },
            { NetworkEquipmentSlotType.EquipmentSlotGloves, typeof(EquipmentSlot<BlueprintItemEquipmentGloves>) },
            { NetworkEquipmentSlotType.EquipmentSlotNeck, typeof(EquipmentSlot<BlueprintItemEquipmentNeck>) },
            { NetworkEquipmentSlotType.EquipmentSlotRing, typeof(EquipmentSlot<BlueprintItemEquipmentRing>) },
            { NetworkEquipmentSlotType.EquipmentSlotWrist, typeof(EquipmentSlot<BlueprintItemEquipmentWrist>) },
            { NetworkEquipmentSlotType.EquipmentSlotShoulders, typeof(EquipmentSlot<BlueprintItemEquipmentShoulders>) },
        };

        private readonly Dictionary<Type, NetworkEquipmentSlotType> _typeToNetworkType;


        public EquipmentDefinitions()
        {
            _typeToNetworkType = _networkTypeToType.ToDictionary(x => x.Value, x => x.Key);
        }

        public Type GetSlotType(NetworkEquipmentSlotType slotType)
        {
            _networkTypeToType.TryGetValue(slotType, out var type);
            return type;
        }

        public NetworkEquipmentSlotType? GetSlotType(Type type)
        {
            _typeToNetworkType.TryGetValue(type, out var slotType);
            return slotType;
        }
    }
}

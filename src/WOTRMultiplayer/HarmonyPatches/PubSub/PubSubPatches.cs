using HarmonyLib;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Microsoft.Extensions.Logging;

namespace WOTRMultiplayer.HarmonyPatches.PubSub
{
    [HarmonyPatch]
    public class PubSubPatches
    {
        //[HarmonyPatch(typeof(SubscribersList<object>), nameof(SubscribersList<object>.AddSubscriber))]
        //[HarmonyPrefix]
        //public static bool SubscribersList_AddSubscriber_Prefix(SubscribersList<object> __instance, object subscriber)
        //{
        //    var logger = Main.GetLogger<SubscriberListPatches>();

        //    logger.LogInformation("GenericType={genericType}, SubscriberType={subscriberType}", __instance.GetType().Name, subscriber?.GetType().Name);

        //    return false;
        //}

        //[HarmonyPatch(typeof(SubscriptionManager<IGlobalSubscriber>), nameof(SubscriptionManager<IGlobalSubscriber>.Subscribe))]
        //[HarmonyPrefix]
        //public static void SubscriptionManager_Subscribe_Prefix(SubscriptionManager<IGlobalSubscriber> __instance, object subscriber, ISubscriptionProxy proxy)
        //{
        //    //var logger = Main.GetLogger<PubSubPatches>();

        //    //logger.LogInformation("GenericType={genericType}, SubscriberType={subscriberType}", __instance.GetType().GenericTypeArguments?.FirstOrDefault(), subscriber?.GetType().Name);

        //    //if (subscriber is INetworkSubscriber)
        //    //{
        //    //    logger.LogInformation("Network sub");
        //    //}

        //    //return true;
        //}

        [HarmonyPatch(typeof(EventBus), nameof(EventBus.Subscribe), typeof(IUnitSubscriber), typeof(ISubscriptionProxy))]
        [HarmonyPrefix]
        public static void EventBus_Subscribe_Prefix(IUnitSubscriber subscriber, ISubscriptionProxy proxy)
        {
            //var logger = Main.GetLogger<EventBus>();
            //var unit = subscriber?.GetSubscribingUnit() ?? proxy?.GetSubscribingUnit();
            //if (unit != null)
            //{
            //    logger.LogInformation("Subscribe. SubscriberType={subscriberType} CharacterName={charactrerName}", (subscriber?.GetType() ?? proxy?.GetSubscriber()?.GetType()).Name, unit.CharacterName);
            //}
        }

        [HarmonyPatch(typeof(ClickGroundHandler), nameof(ClickGroundHandler.RunCommand))]
        [HarmonyPrefix]
        public static void ClickGroundHandler_RunCommand_Prefix(UnitEntityData unit, ClickGroundHandler.CommandSettings settings)
        {
            var logger = Main.GetLogger<ClickGroundHandler>();
            logger.LogInformation("Move command. CharacterName={characterName} Destination={destination}", unit.CharacterName, settings.Destination);
            Main.Multiplayer.MoveCharacter(unit, settings);
        }
    }
}

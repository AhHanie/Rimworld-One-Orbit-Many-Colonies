using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace One_Orbit_Many_Colonies.Patches
{
    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.ColonyThingsWillingToBuy))]
    internal static class TradeShip_ColonyThingsWillingToBuy
    {
        private static bool Prefix(TradeShip __instance, Pawn playerNegotiator, ref IEnumerable<Thing> __result)
        {
            Map map = playerNegotiator?.Map;
            if (map == null || map == __instance.Map)
            {
                return true;
            }
            __result = Enumerate(map, __instance);
            return false;
        }

        private static IEnumerable<Thing> Enumerate(Map map, TradeShip ship)
        {
            foreach (Thing item in TradeUtility.AllLaunchableThingsForTrade(map, ship))
            {
                yield return item;
            }
            foreach (Pawn item2 in TradeUtility.AllSellableColonyPawns(map, checkAcceptableTemperatureOfAnimals: false))
            {
                yield return item2;
            }
        }
    }

    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer))]
    internal static class TradeShip_GiveSoldThingToPlayer
    {
        private static readonly AccessTools.FieldRef<TradeShip, List<Pawn>> SoldPrisoners = AccessTools.FieldRefAccess<TradeShip, List<Pawn>>("soldPrisoners");

        private static bool Prefix(TradeShip __instance, Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            Map map = playerNegotiator?.Map;
            if (map == null || map == __instance.Map)
            {
                return true;
            }
            Thing thing = toGive.SplitOff(countToGive);
            thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, __instance);
            if (thing is Pawn pawn)
            {
                SoldPrisoners(__instance).Remove(pawn);
            }
            TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(map), map, thing);
            return false;
        }
    }

    [HarmonyPatch(typeof(TradeShip), nameof(TradeShip.TryOpenComms))]
    internal static class TradeShip_TryOpenComms
    {
        private static bool Prefix(TradeShip __instance, Pawn negotiator)
        {
            if (__instance.Map != null && SharedTradeShips.IsEligiblePlayerMap(negotiator?.Map))
            {
                return true;
            }
            if (!__instance.Departed)
            {
                Messages.Message("OneOrbitManyColonies.TradeUnavailable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            }
            return false;
        }
    }
}

using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace One_Orbit_Many_Colonies.Patches
{
    [HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetCommTargets))]
    internal static class Building_CommsConsole_GetCommTargets
    {
        private static void Postfix(Pawn myPawn, ref IEnumerable<ICommunicable> __result)
        {
            Map map = myPawn.Map;
            if (!SharedTradeShips.IsEligiblePlayerMap(map))
            {
                return;
            }
            List<TradeShip> ships = SharedTradeShips.AvailableFor(map);
            List<ICommunicable> targets = new List<ICommunicable>(ships.Count + 8);
            for (int i = 0; i < ships.Count; i++)
            {
                targets.Add(ships[i]);
            }
            foreach (ICommunicable target in __result)
            {
                if (!(target is TradeShip))
                {
                    targets.Add(target);
                }
            }
            __result = targets;
        }
    }

    [HarmonyPatch(typeof(Building_CommsConsole), "AnnounceTradeShips")]
    internal static class Building_CommsConsole_AnnounceTradeShips
    {
        private static void Postfix(Building_CommsConsole __instance)
        {
            Map map = __instance.Map;
            if (map == null || !__instance.CanUseCommsNow || !SharedTradeShips.IsEligiblePlayerMap(map))
            {
                return;
            }
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                List<PassingShip> ships = maps[i].passingShipManager.passingShips;
                for (int j = 0; j < ships.Count; j++)
                {
                    if (ships[j] is TradeShip ship && !ship.WasAnnounced && !ship.Departed)
                    {
                        SharedTradeShips.SendArrivalLetter(ship, map);
                    }
                }
            }
        }
    }
}

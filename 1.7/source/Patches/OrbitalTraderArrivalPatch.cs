using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace One_Orbit_Many_Colonies.Patches
{
    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    internal static class IncidentWorker_OrbitalTraderArrival_TryExecuteWorker
    {
        private static void Postfix(bool __result, IncidentParms parms)
        {
            if (!__result)
            {
                return;
            }
            Map map = (Map)parms.target;
            List<PassingShip> ships = map.passingShipManager.passingShips;
            if (ships.Count == 0 || !(ships[ships.Count - 1] is TradeShip ship) || ship.WasAnnounced)
            {
                return;
            }
            if (SharedTradeShips.HasPoweredCommsOnAnyEligibleMap())
            {
                SharedTradeShips.SendArrivalLetter(ship, map);
            }
        }
    }
}

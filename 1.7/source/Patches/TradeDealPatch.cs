using HarmonyLib;
using RimWorld;
using Verse;

namespace One_Orbit_Many_Colonies.Patches
{
    [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
    internal static class TradeDeal_TryExecute
    {
        private static bool Prefix(out bool actuallyTraded, ref bool __result)
        {
            actuallyTraded = false;
            if (TradeSession.giftMode || !(TradeSession.trader is TradeShip ship))
            {
                return true;
            }
            if (ship.Map != null && !ship.Departed && SharedTradeShips.IsEligiblePlayerMap(TradeSession.playerNegotiator?.Map))
            {
                return true;
            }
            Messages.Message("OneOrbitManyColonies.TradeUnavailable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            Find.WindowStack.WindowOfType<Dialog_Trade>()?.Close();
            __result = false;
            return false;
        }
    }
}

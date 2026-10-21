using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using Verse;

namespace One_Orbit_Many_Colonies.Patches
{
    [HarmonyPatch(typeof(PassingShip), nameof(PassingShip.CommFloatMenuOption))]
    internal static class PassingShip_CommFloatMenuOption
    {
        private static bool Prefix(PassingShip __instance, Building_CommsConsole console, Pawn negotiator, ref FloatMenuOption __result)
        {
            if (!(__instance is TradeShip ship) || console.Map == ship.Map)
            {
                return true;
            }
            string label = "CallOnRadio".Translate(ship.GetCallLabel());
            Action action = null;
            AcceptanceReport canCommunicate = negotiator.CanTradeWith(ship.Faction, ship.TraderKind);
            if (!canCommunicate.Accepted)
            {
                string reason = canCommunicate.Reason;
                if (!reason.NullOrEmpty())
                {
                    action = delegate
                    {
                        Messages.Message(reason, console, MessageTypeDefOf.RejectInput, historical: false);
                    };
                }
            }
            else
            {
                action = delegate
                {
                    if (!Building_OrbitalTradeBeacon.AllPowered(console.Map).Any())
                    {
                        Messages.Message("MessageNeedBeaconToTradeWithShip".Translate(), console, MessageTypeDefOf.RejectInput, historical: false);
                    }
                    else
                    {
                        console.GiveUseCommsJob(negotiator, ship);
                    }
                };
            }
            __result = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action, MenuOptionPriority.InitiateSocial), negotiator, console);
            return false;
        }
    }

    [HarmonyPatch(typeof(PassingShip), nameof(PassingShip.Depart))]
    internal static class PassingShip_Depart
    {
        private static void Prefix(PassingShip __instance)
        {
            if (!(__instance is TradeShip))
            {
                return;
            }
            Map map = __instance.Map;
            if (map == null || map.listerBuildings.ColonistsHaveBuilding(SharedTradeShips.IsCommsConsole))
            {
                return;
            }
            if (SharedTradeShips.HasCommsOnAnyEligibleMap())
            {
                Messages.Message("MessageShipHasLeftCommsRange".Translate(__instance.FullTitle), MessageTypeDefOf.SituationResolved);
            }
        }
    }
}

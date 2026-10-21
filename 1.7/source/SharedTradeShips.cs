using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace One_Orbit_Many_Colonies
{
    internal static class SharedTradeShips
    {
        internal static readonly Func<Thing, bool> IsCommsConsole = b => b.def.IsCommsConsole;

        internal static bool IsEligiblePlayerMap(Map map)
        {
            if (map == null || !Find.Maps.Contains(map))
            {
                return false;
            }
            return IsEligibleLoadedMap(map);
        }

        private static bool IsEligibleLoadedMap(Map map)
        {
            return map.ParentFaction == Faction.OfPlayer || map.mapPawns.FreeColonistsSpawnedCount > 0 || map.IsPlayerHome;
        }

        internal static List<TradeShip> AvailableFor(Map callingMap)
        {
            List<TradeShip> result = new List<TradeShip>();
            if (!IsEligiblePlayerMap(callingMap))
            {
                return result;
            }
            Collect(callingMap, result);
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i] != callingMap)
                {
                    Collect(maps[i], result);
                }
            }
            return result;
        }

        private static void Collect(Map map, List<TradeShip> result)
        {
            List<PassingShip> ships = map.passingShipManager.passingShips;
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] is TradeShip ship && !ship.Departed)
                {
                    if (result.Contains(ship))
                    {
                        Logger.Error("Trade ship " + ship.FullTitle + " is owned by more than one map.");
                    }
                    else
                    {
                        result.Add(ship);
                    }
                }
            }
        }

        internal static bool HasPoweredCommsOnAnyEligibleMap()
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (!IsEligibleLoadedMap(map))
                {
                    continue;
                }
                List<Building> buildings = map.listerBuildings.allBuildingsColonist;
                for (int j = 0; j < buildings.Count; j++)
                {
                    Building building = buildings[j];
                    if (!building.def.IsCommsConsole)
                    {
                        continue;
                    }
                    CompPowerTrader power = building.GetComp<CompPowerTrader>();
                    if (power == null || power.PowerOn)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static bool HasCommsOnAnyEligibleMap()
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (IsEligibleLoadedMap(map) && map.listerBuildings.ColonistsHaveBuilding(IsCommsConsole))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void SendArrivalLetter(TradeShip ship, Map targetMap)
        {
            TaggedString text = "TraderArrival".Translate(ship.name, ship.def.label, (ship.Faction == null) ? "TraderArrivalNoFaction".Translate() : "TraderArrivalFromFaction".Translate(ship.Faction.Named("FACTION")));
            IncidentParms parms = new IncidentParms
            {
                target = targetMap,
                traderKind = ship.TraderKind
            };
            IncidentWorker.SendIncidentLetter(ship.def.LabelCap, text, LetterDefOf.PositiveEvent, parms, LookTargets.Invalid, null);
            ship.WasAnnounced = true;
        }

        internal static void RehomeShips(Map removing)
        {
            if (removing == null || !Find.Maps.Contains(removing))
            {
                return;
            }
            List<PassingShip> ships = removing.passingShipManager.passingShips;
            List<TradeShip> owned = null;
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] is TradeShip ship)
                {
                    if (owned == null)
                    {
                        owned = new List<TradeShip>();
                    }
                    owned.Add(ship);
                }
            }
            if (owned == null)
            {
                return;
            }
            Map target = null;
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (maps[i] != removing && IsEligibleLoadedMap(maps[i]))
                {
                    target = maps[i];
                    break;
                }
            }
            for (int i = 0; i < owned.Count; i++)
            {
                TradeShip ship = owned[i];
                if (target == null)
                {
                    ship.Depart();
                    continue;
                }
                removing.passingShipManager.RemoveShip(ship);
                target.passingShipManager.AddShip(ship);
                if (ship.Map != target || !target.passingShipManager.passingShips.Contains(ship) || ships.Contains(ship))
                {
                    Logger.Error("Failed to move trade ship " + ship.FullTitle + " to " + target + ".");
                }
            }
        }
    }
}

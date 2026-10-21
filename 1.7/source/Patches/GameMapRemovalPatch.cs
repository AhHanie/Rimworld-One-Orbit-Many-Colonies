using HarmonyLib;
using Verse;

namespace One_Orbit_Many_Colonies.Patches
{
    [HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
    internal static class Game_DeinitAndRemoveMap
    {
        private static void Prefix(Map map)
        {
            SharedTradeShips.RehomeShips(map);
        }
    }
}

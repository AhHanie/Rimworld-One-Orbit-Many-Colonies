using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace One_Orbit_Many_Colonies
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "OneOrbitManyColonies.LoadingLabel", doAsynchronously: true, null);
        }

        private void Init()
        {
            GetSettings<ModSettings>();
            new Harmony("sk.oneorbit").PatchAll();
        }

        public override string SettingsCategory()
        {
            return "OneOrbitManyColonies.SettingsTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ModSettingsWindow.Draw(inRect);
            base.DoSettingsWindowContents(inRect);
        }
    }
}

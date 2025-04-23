using HarmonyLib;
using TaleWorlds.MountAndBlade;


namespace BattleSizeResized
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Harmony _harmony = new("BattleSizeResized");
            _harmony.PatchAll();
        }
    }
}
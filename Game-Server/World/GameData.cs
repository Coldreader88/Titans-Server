using System.Diagnostics;
using Common.Characters;
using SmartEngine.Core;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// Reads every client data table (DB/Templates) when the game server starts, so a missing or broken file
    /// shows as a warning in the console right away instead of the first time a player needs it. Each table
    /// still loads itself on first use if this was skipped.
    /// </summary>
    public static class GameData
    {
        public static void LoadAll()
        {
            var watch = Stopwatch.StartNew();
            IdLinks.EnsureLoaded();
            ItemTemplates.EnsureLoaded();
            VehicleTemplates.EnsureLoaded();
            EngineTemplates.EnsureLoaded();
            VehicleEquipment.EnsureLoaded();
            Improvements.EnsureLoaded();
            Production.EnsureLoaded();
            ClothesColours.EnsureLoaded();
            Shops.EnsureLoaded();
            Facilities.EnsureLoaded();
            Mining.EnsureLoaded();
            Quests.All.ToString();
            QuestGivers.EnsureLoaded();
            Occupation.EnsureLoaded();
            Logger.ShowInfo(string.Format("Loaded the client data tables in {0:0.0} s.", watch.Elapsed.TotalSeconds));
        }
    }
}

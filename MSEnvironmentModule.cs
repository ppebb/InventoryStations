using MagicStorage;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InventoryStations {
    [JITWhenModsEnabled("MagicStorage")]
    [ExtendsFromMod("MagicStorage")]
    public class InventoryStationsModule : EnvironmentModule {
        public override void ModifyCraftingZones(EnvironmentSandbox sandbox, ref CraftingInformation information) {
            Player player = sandbox.player;
            int[] adjTiles = ModContent.GetInstance<InventoryStationsTileCheck>().AdjTiles(1);

            foreach (int id in adjTiles) {
                information.adjTiles[id] = true;

                if (TileID.Sets.CountsAsWaterSource[id])
                    information.water = true;
                if (TileID.Sets.CountsAsLavaSource[id])
                    information.lava = true;
                if (TileID.Sets.CountsAsHoneySource[id])
                    information.honey = true;
            }

            if (player.ZoneSnow)
                information.snow = true;

            if (player.adjWater)
                information.water = true;

            if (player.adjLava)
                information.lava = true;

            if (player.adjHoney)
                information.honey = true;

            if (player.alchemyTable)
                information.alchemyTable = true;

            if (player.ZoneGraveyard)
                information.graveyard = true;
        }
    }
}

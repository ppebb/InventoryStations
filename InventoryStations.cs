using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace InventoryStations {
	public class InventoryStations : Mod {

	}

	public class ISGlobalTile : GlobalTile {
        public override int[] AdjTiles(int type) {
			Player player = Main.LocalPlayer;
			List<int> stations = new List<int>();
			ISConfig isConfig = ModContent.GetInstance<ISConfig>();

				LoopThroughInventory(player.inventory, stations); // Inventory

			if (isConfig.Piggy)
				LoopThroughInventory(player.bank.item, stations); // Piggy Bank

			if (isConfig.Safe)
				LoopThroughInventory(player.bank2.item, stations); // Safe

			if (isConfig.Forge)
				LoopThroughInventory(player.bank3.item, stations); // Forge

			if (isConfig.Chest && player.chest > -1)
				LoopThroughInventory(Main.chest[player.chest].item, stations); // Currently Open Chest

			return stations.ToArray();
		}

		private void LoopThroughInventory(Item[] inventory, List<int> stations) {
			for (int i = 0; i < inventory.Length; i++) {
				Item item = inventory[i];
				if (item.IsAir || item.createTile < TileID.Dirt)
					continue;

				stations.Add(item.createTile);

				ModTile mTile = ModContent.GetModTile(item.createTile);
				if (mTile != null) {
					for (int j = 0; j < mTile.adjTiles.Length; j++) {
						stations.Add(mTile.adjTiles[j]);
					}
				}
			}
		}
    }

	public class ISConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[DefaultValue(true)]
		public bool Piggy;

		[DefaultValue(true)]
		public bool Safe;

		[DefaultValue(true)]
		public bool Forge;

		[DefaultValue(true)]
		public bool Chest;
	}
}
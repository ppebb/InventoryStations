using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace InventoryStations {
	public class InventoryStations : Mod {
		internal static Mod MagicStorage;
		internal static bool msLoaded => ModLoader.TryGetMod("MagicStorage", out MagicStorage);
		private static Type msCraftingGUI;
		private static MethodInfo msAnalyzeIngredients;
		private FieldInfo msAdjTiles;
		private FieldInfo msZoneSnow;
		private FieldInfo msAdjWater;
		private FieldInfo msAdjLava;
		private FieldInfo msAdjHoney;
		private FieldInfo msAlchemyTable;
		private static int[] adjTiles;

		public override void Load() {
			if (msLoaded) {
				msCraftingGUI = MagicStorage.GetType().Assembly.GetType("MagicStorage.CraftingGUI");
				msAnalyzeIngredients = MagicStorage.GetType().Assembly.GetType("MagicStorage.CraftingGUI").GetMethod("AnalyzeIngredients", BindingFlags.NonPublic | BindingFlags.Static);
				msAdjTiles = msCraftingGUI.GetField("adjTiles", BindingFlags.NonPublic | BindingFlags.Static);
				msZoneSnow = msCraftingGUI.GetField("zoneSnow", BindingFlags.NonPublic | BindingFlags.Static);
				msAdjWater = msCraftingGUI.GetField("adjWater", BindingFlags.NonPublic | BindingFlags.Static);
				msAdjLava = msCraftingGUI.GetField("adjLava", BindingFlags.NonPublic | BindingFlags.Static);
				msAdjHoney = msCraftingGUI.GetField("adjHoney", BindingFlags.NonPublic | BindingFlags.Static);
				msAlchemyTable = msCraftingGUI.GetField("alchemyTable", BindingFlags.NonPublic | BindingFlags.Static);
				OnAnalyzeIngredients += InventoryStations_ModifyOnAnalyzeIngredients;
			}
        }

		// This could probably be a detour but maybe I'll actually need local variables soon!
        private void InventoryStations_ModifyOnAnalyzeIngredients(ILContext il) {
			ILCursor c = new ILCursor(il);

			if (!c.TryGotoNext(MoveType.Before,
				i => i.MatchRet())) {
				Logger.Fatal("Could not locate the end of MagicStorage.CraftingGUI.AnalyzeIngredients");
            }

			c.Emit(OpCodes.Ldsfld, msAdjTiles);
			c.Emit(OpCodes.Ldsfld, msZoneSnow);
			c.Emit(OpCodes.Ldsfld, msAdjWater);
			c.Emit(OpCodes.Ldsfld, msAdjLava);
			c.Emit(OpCodes.Ldsfld, msAdjHoney);
			c.Emit(OpCodes.Ldsfld, msAlchemyTable);

			c.EmitDelegate<Action<bool[], bool, bool, bool, bool, bool>>((eAdjTiles, eZoneSnow, eAdjWater, eAdjLava, eAdjHoney, eAlchemyTable) => {
				Player player = Main.LocalPlayer;
				adjTiles = ModContent.GetInstance<ISGlobalTile>().AdjTiles(1);
				for (int i = 0; i < adjTiles.Length; i++) {
					eAdjTiles[adjTiles[i]] = true;

					if (TileID.Sets.CountsAsWaterSource[adjTiles[i]])
						eAdjWater = true;
					if (TileID.Sets.CountsAsLavaSource[adjTiles[i]])
						eAdjLava = true;
					if (TileID.Sets.CountsAsHoneySource[adjTiles[i]])
						eAdjHoney = true;
				}


				if (player.ZoneSnow)
					eZoneSnow = true;

				if (player.adjWater)
					eAdjWater = true;

				if (player.adjLava)
					eAdjLava = true;

				if (player.adjHoney)
					eAdjHoney = true;

				if (player.alchemyTable)
					eAlchemyTable = true;
			});

			//WriteIL(il);
        }

		private void WriteIL(ILContext il) {
			using (StreamWriter file = new StreamWriter(@"C:\Users\ppeb\Documents\debugil.txt", true)) {
				foreach (Instruction instruction in il.Instrs) {
					if (instruction.Operand is ILLabel iLLabel) {
						file.WriteLine("IL_" + instruction.Offset.ToString("x4") + ":" + iLLabel + " label to" + "IL_" + iLLabel.Target.Offset.ToString("x4") + " " + iLLabel.Target.Operand);
                    }
					else {
						try {
							file.WriteLine(instruction);
                        }
						catch (Exception) {
							file.WriteLine("bad code : " + instruction.Operand + " " + instruction.OpCode);
                        }
                    }
                }
            }
        }

        private delegate void OrigAnalyzeIngredients();
		private delegate void HookAnalyzeIngredients();

		private static event ILContext.Manipulator OnAnalyzeIngredients {
			add => HookEndpointManager.Modify(msAnalyzeIngredients, value);
			remove => HookEndpointManager.Unmodify(msAnalyzeIngredients, value);
		}
	}

	public class ISGlobalTile : GlobalTile {
        public override int[] AdjTiles(int type) {
			Player player = Main.LocalPlayer;
			List<int> stations = new List<int>();
			ISConfig isConfig = ModContent.GetInstance<ISConfig>();

				LoopThroughInventory(player.inventory, stations, player); // Inventory

			if (isConfig.Piggy)
				LoopThroughInventory(player.bank.item, stations, player); // Piggy Bank

			if (isConfig.Safe)
				LoopThroughInventory(player.bank2.item, stations, player); // Safe

			if (isConfig.Forge)
				LoopThroughInventory(player.bank3.item, stations, player); // Forge

			if (isConfig.Void)
				LoopThroughInventory(player.bank4.item, stations, player); // Void Bag

			if (isConfig.Chest && player.chest > -1)
				LoopThroughInventory(Main.chest[player.chest].item, stations, player); // Currently Open Chest

			return stations.ToArray();
		}

		private static void LoopThroughInventory(Item[] inventory, List<int> stations, Player player) {
			for (int i = 0; i < inventory.Length; i++) {
				Item item = inventory[i];

				if (item.stack == 0)
					continue;

				switch (item.type) {
					case ItemID.WaterBucket:
					case ItemID.BottomlessBucket:
						player.adjWater = true;
						continue;
					case ItemID.LavaBucket:
					case ItemID.BottomlessLavaBucket:
						player.adjLava = true;
						continue;
					case ItemID.HoneyBucket:
						player.adjHoney = true;
						continue;
				}

				if (InventoryStations.msLoaded) {
					if (item.type == InventoryStations.MagicStorage.Find<ModItem>("SnowBiomeEmulator").Type) {
						player.ZoneSnow = true;
                    }
                }

				if (item.IsAir || item.createTile <= TileID.Dirt)
					continue;


				stations.Add(item.createTile);

				switch (item.createTile) {
					case TileID.GlassKiln:
					case TileID.Hellforge:
						stations.Add(TileID.Furnaces);
						continue;
					case TileID.AdamantiteForge:
						stations.Add(TileID.Furnaces);
						stations.Add(TileID.Hellforge);
						continue;
					case TileID.MythrilAnvil:
						stations.Add(TileID.Anvils);
						continue;
					case TileID.BewitchingTable:
					case TileID.Tables2:
						stations.Add(TileID.Tables);
						continue;
					case TileID.AlchemyTable:
						stations.Add(TileID.Bottles);
						stations.Add(TileID.Tables);
						player.alchemyTable = true;
						continue;
				}

				ModTile mTile = ModContent.GetModTile(item.createTile);
				if (mTile != null) {
					for (int j = 0; j < mTile.AdjTiles.Length; j++) {
						stations.Add(mTile.AdjTiles[j]);
					}
				}

				/*if (item.createTile == TileID.Tombstones) {
					stations.Add(TileID.Tombstones);
					player.ZoneGraveyard = true;
                }*/

				if (TileID.Sets.CountsAsWaterSource[item.createTile])
					player.adjWater = true;
				if (TileID.Sets.CountsAsLavaSource[item.createTile])
					player.adjLava = true;
				if (TileID.Sets.CountsAsHoneySource[item.createTile])
					player.adjHoney = true;
			}
		}
    }

	public class ISConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[DefaultValue(true)]
		[Label("Disable stations in piggy banks counting toward crafting.")]
		public bool Piggy;

		[DefaultValue(true)]
		[Label("Disable stations in safes counting toward crafting.")]
		public bool Safe;

		[DefaultValue(true)]
		[Label("Disable stations in the forge counting toward crafting.")]
		public bool Forge;

		[DefaultValue(true)]
		[Label("Disable stations in void bags counting toward crafting.")]
		public bool Void;

		[DefaultValue(true)]
		[Label("Disable stations in the currently opened chest counting toward crafting.")]
		public bool Chest;
	}
}
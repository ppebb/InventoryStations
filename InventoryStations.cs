using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private FieldInfo msGraveyard;
        private static int[] adjTiles;

        public override void Load() {
            if (!msLoaded)
                return;

            msCraftingGUI = MagicStorage.GetType().Assembly.GetType("MagicStorage.CraftingGUI");
            msAnalyzeIngredients = msCraftingGUI.GetMethod("AnalyzeIngredients", BindingFlags.NonPublic | BindingFlags.Static);
            msAdjTiles = msCraftingGUI.GetField("adjTiles", BindingFlags.NonPublic | BindingFlags.Static);
            msZoneSnow = msCraftingGUI.GetField("zoneSnow", BindingFlags.NonPublic | BindingFlags.Static);
            msAdjWater = msCraftingGUI.GetField("adjWater", BindingFlags.NonPublic | BindingFlags.Static);
            msAdjLava = msCraftingGUI.GetField("adjLava", BindingFlags.NonPublic | BindingFlags.Static);
            msAdjHoney = msCraftingGUI.GetField("adjHoney", BindingFlags.NonPublic | BindingFlags.Static);
            msAlchemyTable = msCraftingGUI.GetField("alchemyTable", BindingFlags.NonPublic | BindingFlags.Static);
            msGraveyard = msCraftingGUI.GetField("graveyard", BindingFlags.NonPublic | BindingFlags.Static);
            OnAnalyzeIngredients += InventoryStations_ModifyOnAnalyzeIngredients;
        }

        public override void Unload() {
            MagicStorage = null;
            msCraftingGUI = null;
            msAnalyzeIngredients = null;
        }

        private void InventoryStations_ModifyOnAnalyzeIngredients(ILContext il) {
            ILCursor c = new(il);

            if (!c.TryGotoNext(MoveType.Before,
                i => i.MatchRet())) {
                Logger.Fatal("Could not locate the end of MagicStorage.CraftingGUI.AnalyzeIngedients");
            }

            c.Emit(OpCodes.Ldsfld, msAdjTiles);
            c.Emit(OpCodes.Ldsfld, msZoneSnow);
            c.Emit(OpCodes.Ldsfld, msAdjWater);
            c.Emit(OpCodes.Ldsfld, msAdjLava);
            c.Emit(OpCodes.Ldsfld, msAdjHoney);
            c.Emit(OpCodes.Ldsfld, msAlchemyTable);
            c.Emit(OpCodes.Ldsfld, msGraveyard);

            c.EmitDelegate<Action<bool[], bool, bool, bool, bool, bool, bool>>((eAdjTiles, eZoneSnow, eAdjWater, eAdjLava, eAdjHoney, eAlchemyTable, eGraveyard) => {
                Player player = Main.LocalPlayer;
                adjTiles = ModContent.GetInstance<InventoryStationsTileCheck>().AdjTiles(1);

                foreach (int id in adjTiles) {
                    eAdjTiles[id] = true;

                    if (TileID.Sets.CountsAsWaterSource[id])
                        eAdjWater = true;
                    if (TileID.Sets.CountsAsLavaSource[id])
                        eAdjLava = true;
                    if (TileID.Sets.CountsAsHoneySource[id])
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

                if (player.ZoneGraveyard)
                    eGraveyard = true;
            });
        }

        private delegate void OrigAnalyzeIngredients();
        private delegate void HookAnalyzeIngredients();

        private static event ILContext.Manipulator OnAnalyzeIngredients {
            add => HookEndpointManager.Modify(msAnalyzeIngredients, value);
            remove => HookEndpointManager.Unmodify(msAnalyzeIngredients, value);
        }
    }

    public class InventoryStationsTileCheck : GlobalTile {
        public override int[] AdjTiles(int type) {
            Player player = Main.LocalPlayer;
            List<int> stations = new List<int>();
            ISConfig config = ModContent.GetInstance<ISConfig>();

            LoopThroughInventory(player.inventory, stations, player);

            if (config.Piggy)
                LoopThroughInventory(player.bank.item, stations, player);

            if (config.Safe)
                LoopThroughInventory(player.bank2.item, stations, player); // Safe

            if (config.Forge)
                LoopThroughInventory(player.bank3.item, stations, player); // Forge

            if (config.Void)
                LoopThroughInventory(player.bank4.item, stations, player); // Void Bag

            if (config.Chest && player.chest > -1)
                LoopThroughInventory(Main.chest[player.chest].item, stations, player); // Currently Open Chest

            return stations.ToArray();
        }

        private static void LoopThroughInventory(Item[] inventory, List<int> stations, Player player) {
            foreach (Item item in inventory) {
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
                    if (item.type == InventoryStations.MagicStorage.Find<ModItem>("SnowBiomeEmulator").Type)
                        player.ZoneSnow = true;
                    else if (item.type == InventoryStations.MagicStorage.Find<ModItem>("BiomeGlobe").Type) {
                        player.ZoneSnow = true;
                        player.ZoneGraveyard = true;
                        player.adjWater = true;
                        player.adjLava = true;
                        player.adjHoney = true;

                        stations.Add(TileID.Campfire);
                        stations.Add(TileID.DemonAltar);
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
                    foreach (int j in mTile.AdjTiles)
                        stations.Add(j);
                }

                if (item.createTile == TileID.Tombstones) {
                    player.ZoneGraveyard = true;
                    stations.Add(TileID.Tombstones);
                }

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

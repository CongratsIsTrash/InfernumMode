﻿using InfernumMode.Systems;
using Terraria;
using Terraria.ModLoader;

namespace InfernumMode.Biomes
{
    public class ProfanedTempleBiome : ModBiome
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.Environment;

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Sounds/Music/ProfanedTemple");

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Profaned Temple");
        }

        public override bool IsBiomeActive(Player player)
        {
            return !player.ZoneDungeon && ((InfernumBiomeTileCounterSystem.ProfanedTile > 350 && player.ZoneUnderworldHeight) || player.Infernum().InProfanedArena);
        }
    }
}
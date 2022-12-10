using CalamityMod.NPCs;
using CalamityMod.NPCs.ExoMechs;
using CalamityMod.NPCs.GreatSandShark;
using InfernumMode.BehaviorOverrides.BossAIs.GreatSandShark;
using InfernumMode.BossRush;
using InfernumMode.Projectiles.Wayfinder;
using InfernumMode.Subworlds;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.Systems
{
    public class WorldUpdatingSystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            CalamityMod.CalamityMod.sharkKillCount = 0;

            BossRushChanges.HandleTeleports();
            if (!NPC.AnyNPCs(ModContent.NPCType<Draedon>()))
                CalamityGlobalNPC.draedon = -1;

            bool inColosseum = SubworldSystem.IsActive<LostColosseum>();
            if (!inColosseum)
            {
                // Mark the vassal as not having been spawned.
                LostColosseum.HasBereftVassalAppeared = false;

                // Register the great sand shark and bereft vassal as dead in the bestiary if they were successfully defeated in the subworld.
                if (LostColosseum.HasBereftVassalBeenDefeated)
                {
                    LostColosseum.HasBereftVassalBeenDefeated = false;

                    NPC fakeNPC = new();
                    fakeNPC.SetDefaults(ModContent.NPCType<BereftVassal>());
                    for (int i = 0; i < 100; i++)
                        Main.BestiaryTracker.Kills.RegisterKill(fakeNPC);

                    fakeNPC.SetDefaults(ModContent.NPCType<GreatSandShark>());
                    for (int i = 0; i < 100; i++)
                        Main.BestiaryTracker.Kills.RegisterKill(fakeNPC);
                }
            }

            // Manage the sandstorm and sunset in the Colosseum if inside of it.
            else
            {
                LostColosseum.ManageSandstorm();
                LostColosseum.UpdateSunset();
                CalamityMod.CalamityMod.StopRain();

                // Get rid of clouds.
                for (int i = 0; i < Main.maxClouds; i++)
                    Main.cloud[i].active = false;
            }

            if (!LostColosseum.HasBereftVassalAppeared && inColosseum && !Main.LocalPlayer.dead)
            {
                int x = Main.maxTilesX * 8 + 6240;
                int y = Main.maxTilesY * 8 - 500;
                NPC.NewNPC(new EntitySource_WorldEvent(), x, y, ModContent.NPCType<BereftVassal>(), 1);
                LostColosseum.HasBereftVassalAppeared = true;
            }

            // Create a wayfinder gate projectile if one doesn't exist yet.
            if (WorldSaveSystem.WayfinderGateLocation != Vector2.Zero)
            {              
                bool gateExists = false;
                int wayfinderGateID = ModContent.ProjectileType<WayfinderGate>();
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile projectile = Main.projectile[i];

                    if (projectile.type == wayfinderGateID && projectile.active)
                    {
                        gateExists = true;
                        break;
                    }
                }

                if (!gateExists && Main.netMode is not NetmodeID.MultiplayerClient)
                    Projectile.NewProjectileDirect(Entity.GetSource_None(), WorldSaveSystem.WayfinderGateLocation, Vector2.Zero, wayfinderGateID, 0, 0, Main.myPlayer);
            }
        }
    }
}
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.AstrumAureus;
using CalamityMod.NPCs.Crabulon;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.NPCs.DevourerofGods;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.Perforator;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Projectiles.DraedonsArsenal;
using CalamityMod.Projectiles.Magic;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Projectiles.Rogue;
using InfernumMode.BehaviorOverrides.BossAIs.SlimeGod;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

using static Terraria.ModLoader.ModContent;

namespace InfernumMode.Balancing
{
    public static class BalancingChangesManager
    {
        internal static List<IBalancingRule[]> UniversalBalancingChanges = null;
        internal static List<NPCBalancingChange> NPCSpecificBalancingChanges = null;

        internal static void Load()
        {
            int corrosiveSpineCloud1 = ProjectileType<Corrocloud1>();
            int corrosiveSpineCloud2 = ProjectileType<Corrocloud2>();
            int corrosiveSpineCloud3 = ProjectileType<Corrocloud3>();

            UniversalBalancingChanges = new List<IBalancingRule[]>()
            {
                Do(new ProjectileResistBalancingRule(0.8f, ProjectileID.Flare, ProjectileID.BlueFlare)),
                Do(new StealthStrikeBalancingRule(0.65f, ProjectileType<AshenStalagmiteProj>())),
                Do(new ProjectileResistBalancingRule(0.55f, ProjectileType<SporeBomb>(), ProjectileType<LeafArrow>(), ProjectileType<IcicleArrowProj>())),
                Do(new ProjectileResistBalancingRule(0.25f, corrosiveSpineCloud1, corrosiveSpineCloud2, corrosiveSpineCloud3)),
            };

            var desertScourgeProjResists1 = new ProjectileResistBalancingRule(0.45f, ProjectileID.JestersArrow, ProjectileID.UnholyArrow, ProjectileID.WaterBolt);
            var desertScourgeProjResists2 = new ProjectileResistBalancingRule(0.75f, ProjectileID.Flare, ProjectileID.BlueFlare);

            var eowIsSplitRequirement = new NPCSpecificRequirementBalancingRule(n => n.type == NPCID.EaterofWorldsBody && n.realLife >= 0 && Main.npc[n.realLife].ai[2] >= 1f);

            int inkCloud1 = ProjectileType<InkCloud>();
            int inkCloud2 = ProjectileType<InkCloud2>();
            int inkCloud3 = ProjectileType<InkCloud3>();

            NPCSpecificBalancingChanges = new List<NPCBalancingChange>()
            {
                // Desert Scourge.
                new NPCBalancingChange(NPCType<DesertScourgeHead>(), Do(desertScourgeProjResists1, desertScourgeProjResists2, new PierceResistBalancingRule(0.425f))),
                new NPCBalancingChange(NPCType<DesertScourgeBody>(), Do(desertScourgeProjResists1, desertScourgeProjResists2, new PierceResistBalancingRule(0.425f))),
                new NPCBalancingChange(NPCType<DesertScourgeTail>(), Do(desertScourgeProjResists1, desertScourgeProjResists2, new PierceResistBalancingRule(0.425f))),

                // King Slime.
                new NPCBalancingChange(NPCID.KingSlime, new PierceResistBalancingRule(0.67f)),

                // Crabulon.
                new NPCBalancingChange(NPCType<CrabulonIdle>(), Do(new ProjectileResistBalancingRule(0.785f, ProjectileType<SeafoamBubble>()))),

                // Eater of Worlds.
                new NPCBalancingChange(NPCID.EaterofWorldsBody, Do(eowIsSplitRequirement, new PierceResistBalancingRule(0.45f))),
                new NPCBalancingChange(NPCID.EaterofWorldsBody, Do(new StealthStrikeBalancingRule(0.75f, ProjectileType<ScourgeoftheDesertProj>()))),
                new NPCBalancingChange(NPCID.EaterofWorldsBody, Do(new PierceResistBalancingRule(0.4f))),

                // Brain of Cthulhu.
                new NPCBalancingChange(NPCID.BrainofCthulhu, Do(new ProjectileResistBalancingRule(0.75f, ProjectileType<SeafoamBubble>()))),

                // Perforators.
                new NPCBalancingChange(NPCType<PerforatorBodySmall>(), Do(new PierceResistBalancingRule(0.4f), new ProjectileResistBalancingRule(0.3f, ProjectileType<InfernalKrisCinder>()))),
                new NPCBalancingChange(NPCType<PerforatorBodyMedium>(), Do(new PierceResistBalancingRule(0.4f))),
                new NPCBalancingChange(NPCType<PerforatorBodyLarge>(), Do(new PierceResistBalancingRule(0.4f), new ProjectileResistBalancingRule(0.3f, ProjectileType<InfernalKrisCinder>()))),

                // King Slime.
                new NPCBalancingChange(NPCType<SlimeSpawnCorrupt2>(), Do(new ProjectileResistBalancingRule(0.6f, inkCloud1, inkCloud2, inkCloud3), new PierceResistBalancingRule(0.5f))),
                new NPCBalancingChange(NPCType<SlimeSpawnCrimson3>(), Do(new ProjectileResistBalancingRule(0.6f, inkCloud1, inkCloud2, inkCloud3), new PierceResistBalancingRule(0.5f))),

                // Wall of Flehsh.
                new NPCBalancingChange(NPCID.WallofFleshEye, Do(new PierceResistBalancingRule(0.785f), new ProjectileResistBalancingRule(0.625f, ProjectileType<TrackingDiskLaser>()))),
                new NPCBalancingChange(NPCID.WallofFleshEye, Do(new PierceResistBalancingRule(0.785f), new ProjectileResistBalancingRule(0.625f, ProjectileType<TrackingDiskLaser>()))),

                // Aquatic Scourge.
                new NPCBalancingChange(NPCType<AquaticScourgeBody>(), Do(new PierceResistBalancingRule(0.45f))),
                new NPCBalancingChange(NPCType<AquaticScourgeBodyAlt>(), Do(new PierceResistBalancingRule(0.45f))),

                // Astrum Aureus.
                new NPCBalancingChange(NPCType<AstrumAureus>(), Do(new PierceResistBalancingRule(0.67f))),

                // Cultist.
                new NPCBalancingChange(NPCID.CultistDragonBody1, Do(new PierceResistBalancingRule(0.24f))),
                new NPCBalancingChange(NPCID.CultistDragonBody2, Do(new PierceResistBalancingRule(0.24f))),
                new NPCBalancingChange(NPCID.CultistDragonBody3, Do(new PierceResistBalancingRule(0.24f))),
                new NPCBalancingChange(NPCID.CultistDragonBody4, Do(new PierceResistBalancingRule(0.24f))),

                // The Devourer of Gods.
                new NPCBalancingChange(NPCType<DevourerofGodsBody>(), Do(new ProjectileResistBalancingRule(0.45f, ProjectileID.MoonlordBullet, ProjectileID.MoonlordArrow, ProjectileID.MoonlordArrowTrail))),

                // Exo Mechs.
                new NPCBalancingChange(NPCType<Artemis>(), Do(new ProjectileResistBalancingRule(1.25f, ProjectileType<Galaxia2>()))),
                new NPCBalancingChange(NPCType<Apollo>(), Do(new ProjectileResistBalancingRule(1.25f, ProjectileType<Galaxia2>()))),

                // Supreme Calamitas.
                new NPCBalancingChange(NPCType<SupremeCalamitas>(), Do(new ProjectileResistBalancingRule(0.55f, ProjectileType<InfernadoFriendly>()))),
            };
        }

        internal static void Unload()
        {
            UniversalBalancingChanges = null;
            NPCSpecificBalancingChanges = null;
        }

        public static void ApplyFromProjectile(NPC npc, ref int damage, Projectile proj)
        {
            NPCHitContext hitContext = NPCHitContext.ConstructFromProjectile(proj);
            foreach (IBalancingRule[] balancingRules in UniversalBalancingChanges)
            {
                foreach (IBalancingRule balancingRule in balancingRules)
                {
                    if (balancingRule.AppliesTo(npc, hitContext))
                        balancingRule.ApplyBalancingChange(npc, ref damage);
                }
            }

            foreach (NPCBalancingChange balanceChange in NPCSpecificBalancingChanges)
            {
                if (npc.type != balanceChange.NPCType)
                    continue;

                foreach (IBalancingRule balancingRule in balanceChange.BalancingRules)
                {
                    if (balancingRule.AppliesTo(npc, hitContext))
                        balancingRule.ApplyBalancingChange(npc, ref damage);
                }
            }
        }

        // This function simply concatenates a bunch of balancing rules into an array.
        // It looks a lot nicer than constantly typing "new IItemTweak[]".
        internal static IBalancingRule[] Do(params IBalancingRule[] r) => r;
    }
}
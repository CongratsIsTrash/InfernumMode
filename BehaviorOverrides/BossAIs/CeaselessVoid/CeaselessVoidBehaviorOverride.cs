﻿using CalamityMod;
using CalamityMod.NPCs;
using CalamityMod.World;
using InfernumMode.OverridingSystem;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using CeaselessVoidBoss = CalamityMod.NPCs.CeaselessVoid.CeaselessVoid;

namespace InfernumMode.BehaviorOverrides.BossAIs.CeaselessVoid
{
    public class CeaselessVoidBehaviorOverride : NPCBehaviorOverride
    {
        public override int NPCOverrideType => ModContent.NPCType<CeaselessVoidBoss>();

        public override NPCOverrideContext ContentToOverride => NPCOverrideContext.NPCAI | NPCOverrideContext.NPCSetDefaults;

        #region Enumerations
        public enum CeaselessVoidAttackType
        {
            ReleaseRealityTearPortals,
            DarkMagicCharge,
            DarkEnergySummon,
        }
		#endregion

		#region Set Defaults
		public override void SetDefaults(NPC npc)
        {
            npc.npcSlots = 36f;
            npc.width = 100;
            npc.height = 100;
            npc.defense = 0;
            npc.lifeMax = 416000;
            Mod calamityModMusic = ModLoader.GetMod("CalamityModMusic");
            if (calamityModMusic != null)
                npc.modNPC.music = calamityModMusic.GetSoundSlot(SoundType.Music, "Sounds/Music/ScourgeofTheUniverse");
            else
                npc.modNPC.music = MusicID.Boss3;
            if (CalamityWorld.DoGSecondStageCountdown <= 0)
            {
                npc.value = Item.buyPrice(0, 35, 0, 0);
                if (calamityModMusic != null)
                    npc.modNPC.music = calamityModMusic.GetSoundSlot(SoundType.Music, "Sounds/Music/Void");
                else
                    npc.modNPC.music = MusicID.Boss3;
            }
            npc.aiStyle = -1;
            npc.modNPC.aiType = -1;
            npc.knockBackResist = 0f;
            for (int k = 0; k < npc.buffImmune.Length; k++)
                npc.buffImmune[k] = true;

            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.boss = true;
            npc.DeathSound = SoundID.NPCDeath14;
        }
        #endregion Set Defaults

        #region AI

        public const float Phase2LifeRatio = 0.65f;

		public override bool PreAI(NPC npc)
        {
            // Reset DR.
            npc.Calamity().DR = 0.2f;

            // Do targeting.
            npc.TargetClosest();
            Player target = Main.player[npc.target];

            CalamityGlobalNPC.voidBoss = npc.whoAmI;

            if (!target.active || target.dead || !npc.WithinRange(target.Center, 7200f))
            {
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitY * 18f, 0.08f);
                if (!npc.WithinRange(target.Center, 1450f))
                {
                    npc.life = 0;
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            float lifeRatio = npc.life / (float)npc.lifeMax;
            ref float attackType = ref npc.ai[0];
            ref float attackTimer = ref npc.ai[1];

            // Reset things.
            npc.damage = npc.defDamage;
            npc.dontTakeDamage = target.Center.Y < Main.worldSurface * 16f;

            switch ((CeaselessVoidAttackType)(int)attackType)
            {
                case CeaselessVoidAttackType.ReleaseRealityTearPortals:
                    DoBehavior_ReleaseTearPortals(npc, target, lifeRatio, ref attackTimer);
                    break;
                case CeaselessVoidAttackType.DarkMagicCharge:
                    DoBehavior_DarkMagicCharge(npc, target, lifeRatio, ref attackTimer);
                    break;
                case CeaselessVoidAttackType.DarkEnergySummon:
                    DoBehavior_DarkEnergySummon(npc, target, lifeRatio, ref attackTimer);
                    break;
            }

            attackTimer++;
            return false;
        }

        public static void DoBehavior_ReleaseTearPortals(NPC npc, Player target, float lifeRatio, ref float attackTimer)
        {
            float hoverSpeed = 21f;

            if (lifeRatio < Phase2LifeRatio)
                hoverSpeed += 4.5f;

            Vector2 hoverDestination = target.Center + Vector2.UnitX * (target.Center.X < npc.Center.X).ToDirectionInt() * 450f;

            // Fly to the side of the target.
            if (!npc.WithinRange(hoverDestination, 150f) || npc.WithinRange(target.Center, 200f))
			{
                Vector2 idealVelocity = npc.SafeDirectionTo(hoverDestination) * hoverSpeed;
                npc.SimpleFlyMovement(idealVelocity, hoverSpeed / 22f);
			}

            // Create rifts around the void.
            if (attackTimer % 20f == 19f && attackTimer < 300f)
			{
                Main.PlaySound(SoundID.Item8, npc.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient)
				{
                    Vector2 portalSpawnPosition = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height) * 0.6f;
                    Utilities.NewProjectileBetter(portalSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EnergyPortalBeam>(), 0, 0f);
				}
			}

            if (attackTimer > 375f)
                SelectNewAttack(npc);
        }

        public static void DoBehavior_DarkMagicCharge(NPC npc, Player target, float lifeRatio, ref float attackTimer)
		{
            int chargeTime = 35;
            int chargeCount = 3;
            float chargeSpeed = MathHelper.Lerp(23f, 29f, 1f - lifeRatio);

            ref float attackState = ref npc.Infernum().ExtraAI[0];
            ref float chargeCounter = ref npc.Infernum().ExtraAI[1];

            switch ((int)attackState)
			{
                // Hover into position for the charge.
                case 0:
                    Vector2 hoverDestination = target.Center + new Vector2((target.Center.X < npc.Center.X).ToDirectionInt() * 420f, -300f);
                    npc.SimpleFlyMovement(npc.SafeDirectionTo(hoverDestination) * 25f, 0.8f);
                    npc.Center = npc.Center.MoveTowards(hoverDestination, 10f);

                    if (Main.netMode != NetmodeID.MultiplayerClient && npc.WithinRange(hoverDestination, 50f))
					{
                        npc.velocity = npc.SafeDirectionTo(target.Center, -Vector2.UnitY) * chargeSpeed;

                        for (int i = 0; i < 4; i++)
						{
                            Vector2 portalSpawnPosition = npc.Center + (MathHelper.TwoPi * i / 4f).ToRotationVector2() * 135f;
                            Utilities.NewProjectileBetter(portalSpawnPosition, Vector2.Zero, ModContent.ProjectileType<EnergyPortalBeam>(), 0, 0f);
                        }

                        attackTimer = 0f;
                        attackState = 1f;
                    }
                    break;
                // Do the charge.
                case 1:
                    if (attackTimer > chargeTime)
                        npc.velocity *= 0.93f;
                    if (attackTimer > chargeTime + 25f)
                    {
                        attackTimer = 0f;
                        attackState = 0f;

                        if (chargeCounter < chargeCount)
                            chargeCounter++;
                        else
                            SelectNewAttack(npc);
                        npc.netUpdate = true;
                    }
                    break;
			}
		}

        public static void DoBehavior_DarkEnergySummon(NPC npc, Player target, float lifeRatio, ref float attackTimer)
		{

		}

        public static void SelectNewAttack(NPC npc)
        {
            List<CeaselessVoidAttackType> possibleAttacks = new List<CeaselessVoidAttackType>
            {
                CeaselessVoidAttackType.ReleaseRealityTearPortals,
                CeaselessVoidAttackType.DarkMagicCharge
            };

            if (possibleAttacks.Count > 1)
                possibleAttacks.Remove((CeaselessVoidAttackType)(int)npc.ai[0]);

            for (int i = 0; i < 5; i++)
                npc.Infernum().ExtraAI[i] = 0f;

            npc.ai[0] = (int)Main.rand.Next(possibleAttacks);
            npc.ai[1] = 0f;
            npc.netUpdate = true;
        }
        #endregion AI
    }
}

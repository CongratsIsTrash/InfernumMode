using CalamityMod.NPCs;
using CalamityMod.NPCs.SupremeCalamitas;
using InfernumMode.OverridingSystem;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.BehaviorOverrides.BossAIs.SupremeCalamitas
{
    public class SupremeCatastropheBehaviorOverride : NPCBehaviorOverride
    {
        public enum SupremeCatastropheAttackState
        {
            SliceTarget,
            FlameBlasts,
            SinusoidalDarkMagicFlames
        }

        public override int NPCOverrideType => ModContent.NPCType<SupremeCatastrophe>();

        public override NPCOverrideContext ContentToOverride => NPCOverrideContext.NPCAI;

        public override bool PreAI(NPC npc)
        {
            // Disappear if Supreme Calamitas is not present.
            if (CalamityGlobalNPC.SCal == -1)
            {
                npc.active = false;
                return false;
            }

            npc.target = Main.npc[CalamityGlobalNPC.SCal].target;
            npc.defDamage = 600;
            Player target = Main.player[npc.target];
            ref float attackState = ref npc.ai[0];
            ref float attackTimer = ref npc.ai[1];

            CalamityGlobalNPC.SCalCatastrophe = npc.whoAmI;

            if (attackTimer <= 1f && Main.npc.IndexInRange(CalamityGlobalNPC.SCalCataclysm))
                Main.npc[CalamityGlobalNPC.SCalCataclysm].ai[1] = attackTimer;

            bool alone = !NPC.AnyNPCs(ModContent.NPCType<SupremeCataclysm>());
            switch ((SupremeCatastropheAttackState)attackState)
            {
                case SupremeCatastropheAttackState.SliceTarget:
                    DoBehavior_SliceTarget(npc, target, alone, ref attackTimer);
                    break;
                case SupremeCatastropheAttackState.FlameBlasts:
                    DoBehavior_FlameBlasts(npc, target, alone, ref attackTimer);
                    break;
                case SupremeCatastropheAttackState.SinusoidalDarkMagicFlames:
                    DoBehavior_SinusoidalDarkMagicFlames(npc, target, alone, ref attackTimer);
                    break;
            }
            attackTimer++;
            return false;
        }

        public static void DoBehavior_SliceTarget(NPC npc, Player target, bool alone, ref float attackTimer)
        {
            float predictivenessFactor = 0f;
            float chargeSpeed = 36f;
            int chargeTime = 35;
            ref float attackSubstate = ref npc.ai[2];
            ref float chargeCounter = ref npc.ai[3];

            int hoverDirection = (npc.type == ModContent.NPCType<SupremeCataclysm>()).ToDirectionInt() * (chargeCounter % 2f == 0f).ToDirectionInt();
            if (alone)
            {
                predictivenessFactor = 12f;
                chargeTime -= 8;
                chargeSpeed += 5f;
                hoverDirection = (target.Center.X < npc.Center.X).ToDirectionInt();
            }

            switch ((int)attackSubstate)
            {
                // Hover into position for a brief moment.
                case 0:
                    npc.damage = 0;
                    npc.Opacity = MathHelper.Lerp(npc.Opacity, 0.45f, 0.08f);
                    Vector2 hoverDestination = target.Center - Vector2.UnitY * 200f;
                    hoverDestination.X += hoverDirection * 480f;
                    npc.rotation = npc.AngleTo(target.Center) - MathHelper.PiOver2;

                    // After a sufficient amount of time has passed or if close to the destination, grind to a halt.
                    if (npc.WithinRange(hoverDestination, 100f) || attackTimer >= 75f)
                    {
                        npc.velocity = npc.velocity.MoveTowards(Vector2.Zero, 2.5f) * 0.85f;
                        if (attackTimer < 75f)
                            attackTimer += alone ? 8f : 4f;
                    }
                    else
                    {
                        npc.Center = npc.Center.MoveTowards(hoverDestination, 8f);
                        npc.SimpleFlyMovement(npc.SafeDirectionTo(hoverDestination) * chargeSpeed * 0.85f, 1.8f);
                    }

                    // Charge at the target after slowing down.
                    if (attackTimer >= 85f)
                    {
                        Main.PlaySound(SoundID.DD2_WyvernDiveDown, npc.Center);
                        npc.velocity = npc.SafeDirectionTo(target.Center + target.velocity * predictivenessFactor, -Vector2.UnitY) * chargeSpeed;
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;
                        attackSubstate = 1f;
                        attackTimer = 0f;

                        if (Main.netMode != NetmodeID.MultiplayerClient && alone)
                        {
                            int projectileType = ModContent.ProjectileType<DarkMagicBurst>();
                            int damage = 560;
                            Vector2 burstVelocity = npc.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2) * 8f;
                            Utilities.NewProjectileBetter(npc.Center + burstVelocity * 2f, burstVelocity, projectileType, damage, 0f);
                            Utilities.NewProjectileBetter(npc.Center - burstVelocity * 2f, -burstVelocity, projectileType, damage, 0f);
                        }

                        npc.netUpdate = true;
                    }
                    break;

                // Charge.
                case 1:
                    npc.damage = npc.defDamage;
                    npc.Opacity = MathHelper.Lerp(npc.Opacity, 1f, 0.08f);

                    // Look at the player again after a bit of time charging.
                    if (attackTimer >= chargeTime)
                    {
                        npc.velocity *= 0.9f;
                        npc.rotation = npc.rotation.AngleTowards(npc.AngleTo(target.Center) - MathHelper.PiOver2, 0.33f);
                    }
                    if (attackTimer >= chargeTime + 8f)
                    {
                        attackTimer = 0f;
                        attackSubstate = 0f;
                        if (chargeCounter <= 3f)
                            chargeCounter++;
                        else
                        {
                            npc.ai[0] = Main.rand.Next(1, 3);
                            chargeCounter = 0f;
                        }
                        npc.netUpdate = true;
                    }
                    break;
            }
        }

        public static void DoBehavior_FlameBlasts(NPC npc, Player target, bool alone, ref float attackTimer)
        {
            int hoverTime = 100;
            int burstsPerBurst = 2;
            int postBurstMoveDelay = 10;
            int burstRedirectTime = 35;
            float burstSpeed = 15f;
            float burstAngularVariance = 0.33f;
            int attackCycleCount = 3;
            int hoverDirection = (npc.type == ModContent.NPCType<SupremeCataclysm>()).ToDirectionInt();

            if (alone)
            {
                hoverTime -= 50;
                burstsPerBurst += 2;
                burstRedirectTime -= 10;
                burstSpeed -= 1.5f;
                attackCycleCount++;
                burstAngularVariance *= 2f;
                hoverDirection = (target.Center.X < npc.Center.X).ToDirectionInt();
            }

            float burstAdjustedTime = attackTimer - hoverTime;
            ref float attackCycleCounter = ref npc.ai[2];

            npc.damage = 0;
            npc.Opacity = MathHelper.Lerp(npc.Opacity, 0.45f, 0.08f);

            if (attackTimer < hoverTime)
            {
                // Slow down right before firing.
                if (attackTimer > hoverTime * 0.6f)
                    npc.velocity *= 0.9f;

                // Otherwise, do typical hover behavior, towards the left of the target.
                else
                {
                    Vector2 hoverDestination = target.Center + Vector2.UnitX * hoverDirection * 470f;
                    float distanceFromDestination = npc.Distance(hoverDestination);
                    npc.velocity = npc.SafeDirectionTo(hoverDestination) * MathHelper.Min(distanceFromDestination, 29f);
                }
            }
            else
            {
                // Release brimstone flame bursts at the target on the first frame of targetting.
                // Movement is completely halted at this point until a delay has elapsed.
                if (burstAdjustedTime == 1f)
                {
                    // Play a firing sound.
                    Main.PlaySound(SoundID.DD2_FlameburstTowerShot, npc.Center);

                    // And shoot the projectile serverside.
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int projectileType = ModContent.ProjectileType<DarkMagicBurst>();
                        int damage = 560;

                        for (int i = 0; i < burstsPerBurst; i++)
                        {
                            float shootAngularOffset = MathHelper.Lerp(-burstAngularVariance, burstAngularVariance, i / (burstsPerBurst - 1f));
                            Vector2 shootVelocity = npc.SafeDirectionTo(target.Center, -Vector2.UnitY).RotatedBy(shootAngularOffset) * burstSpeed;
                            Utilities.NewProjectileBetter(npc.Center + shootVelocity * 2f, shootVelocity, projectileType, damage, 0f);
                        }

                        npc.velocity = Vector2.Zero;
                        npc.netUpdate = true;
                    }
                }

                // After the burst and the subsequent move delay is over, begin hovering again in anticipation of the next burst.
                // This movement ceases right before the next burst.
                if (burstAdjustedTime > postBurstMoveDelay)
                {
                    if (burstAdjustedTime < postBurstMoveDelay + burstRedirectTime * 0.7f)
                    {
                        Vector2 hoverDestination = target.Center + Vector2.UnitX * hoverDirection * 470f;
                        float distanceFromDestination = npc.Distance(hoverDestination);
                        npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(hoverDestination) * MathHelper.Min(distanceFromDestination, 29f), 0.1f);
                    }
                    else
                        npc.velocity *= 0.9f;
                }
            }

            // Go to the next attack or do another slash.
            if (burstAdjustedTime >= postBurstMoveDelay + burstRedirectTime)
            {
                attackTimer = 0f;
                if (attackCycleCounter <= attackCycleCount)
                    attackCycleCounter++;
                else
                {
                    attackCycleCounter = 0f;
                    npc.ai[0] = 2;
                }
                npc.netUpdate = true;
            }

            npc.rotation = npc.AngleTo(target.Center) - MathHelper.PiOver2;
        }

        public static void DoBehavior_SinusoidalDarkMagicFlames(NPC npc, Player target, bool alone, ref float attackTimer)
        {
            int shootRate = 25;
            int hoverDirection = (npc.type == ModContent.NPCType<SupremeCataclysm>()).ToDirectionInt();
            if (alone)
            {
                shootRate -= 8;
                hoverDirection = (target.Center.X < npc.Center.X).ToDirectionInt();
            }
            Vector2 hoverDestination = target.Center;
            hoverDestination.X += hoverDirection * 475f;
            hoverDestination.Y += (npc.type == ModContent.NPCType<SupremeCatastrophe>()).ToDirectionInt() * (float)Math.Sin(attackTimer / 42f) * 300f;
            float distanceFromDestination = npc.Distance(hoverDestination);
            Vector2 closeMoveVelocity = npc.SafeDirectionTo(hoverDestination) * MathHelper.Min(distanceFromDestination, 29f);
            npc.velocity = Vector2.Lerp(closeMoveVelocity, (hoverDestination - npc.Center) * 0.02f, Utils.InverseLerp(360f, 1080f, distanceFromDestination, true));

            npc.damage = 0;
            npc.Opacity = MathHelper.Lerp(npc.Opacity, 0.45f, 0.08f);

            if (attackTimer % shootRate == shootRate - 1f)
            {
                Main.PlaySound(SoundID.Item73, npc.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 shootVelocity = Vector2.UnitX * (target.Center.X > npc.Center.X).ToDirectionInt() * 16f;
                    Utilities.NewProjectileBetter(npc.Center, shootVelocity, ModContent.ProjectileType<WavyDarkMagicSkull>(), 550, 0f);
                }
            }

            npc.rotation = npc.AngleTo(target.Center) - MathHelper.PiOver2;

            if (attackTimer >= 440f || alone)
            {
                npc.ai[0] = 0;
                attackTimer = 0f;
                npc.netUpdate = true;
            }
        }
    }
}

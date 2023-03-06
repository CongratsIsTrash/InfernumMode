using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Events;
using CalamityMod.NPCs.AcidRain;
using CalamityMod.NPCs.AquaticScourge;
using CalamityMod.NPCs.DesertScourge;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Boss;
using CalamityMod.World;
using InfernumMode.Assets.Sounds;
using InfernumMode.Common;
using InfernumMode.Common.Graphics;
using InfernumMode.Common.Graphics.Particles;
using InfernumMode.Content.WorldGeneration;
using InfernumMode.Core.OverridingSystem;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace InfernumMode.Content.BehaviorOverrides.BossAIs.AquaticScourge
{
    public class AquaticScourgeHeadBehaviorOverride : NPCBehaviorOverride
    {
        public enum AquaticScourgeAttackType
        {
            SpawnAnimation,
            BubbleSpin,
            RadiationPulse,
            WallHitCharges,
            GasBreath,
            EnterSecondPhase,
            PerpendicularSpikeBarrage,
            EnterFinalPhase,
            AcidRain,
            SulphurousTyphoon
        }

        public override int NPCOverrideType => ModContent.NPCType<AquaticScourgeHead>();

        #region AI
        public override float[] PhaseLifeRatioThresholds => new float[]
        {
            Phase2LifeRatio,
            Phase3LifeRatio
        };

        public const float Phase2LifeRatio = 0.67f;

        public const float Phase3LifeRatio = 0.25f;

        public const int AcidVerticalLineIndex = 7;

        public const int CurrentPhaseIndex = 8;

        public const int AttackCycleIndex = 9;

        public static float PoisonChargeUpSpeedFactor => 0.333f;

        public static float PoisonChargeUpSpeedFactorFinalPhase => 10f;

        public static float PoisonFadeOutSpeedFactor => 2.5f;

        public static AquaticScourgeAttackType[] Phase1AttackCycle => new AquaticScourgeAttackType[]
        {
            AquaticScourgeAttackType.BubbleSpin,
            AquaticScourgeAttackType.RadiationPulse,
            AquaticScourgeAttackType.WallHitCharges,
            AquaticScourgeAttackType.GasBreath,
            AquaticScourgeAttackType.WallHitCharges,
        };

        public static AquaticScourgeAttackType[] Phase2AttackCycle => new AquaticScourgeAttackType[]
        {
            AquaticScourgeAttackType.PerpendicularSpikeBarrage,
            AquaticScourgeAttackType.RadiationPulse,
            AquaticScourgeAttackType.GasBreath,
            AquaticScourgeAttackType.PerpendicularSpikeBarrage,
            AquaticScourgeAttackType.WallHitCharges,
        };

        public static AquaticScourgeAttackType[] Phase3AttackCycle => new AquaticScourgeAttackType[]
        {
            AquaticScourgeAttackType.AcidRain,
            AquaticScourgeAttackType.SulphurousTyphoon,
            AquaticScourgeAttackType.GasBreath,
        };

        public override bool PreAI(NPC npc)
        {
            // Select a new target if an old one was lost.
            npc.TargetClosestIfTargetIsInvalid();

            // Decide music.
            npc.ModNPC.Music = (InfernumMode.CalamityMod as CalamityMod.CalamityMod).GetMusicFromMusicMod("AquaticScourge") ?? MusicID.Boss1;

            // Fade in.
            npc.alpha = Utils.Clamp(npc.alpha - 20, 0, 255);

            ref float generalTimer = ref npc.ai[1];
            ref float attackType = ref npc.ai[2];
            ref float attackTimer = ref npc.ai[3];
            ref float attackDelay = ref npc.Infernum().ExtraAI[5];
            ref float initializedFlag = ref npc.Infernum().ExtraAI[6];
            ref float acidVerticalLine = ref npc.Infernum().ExtraAI[AcidVerticalLineIndex];

            if (Main.netMode != NetmodeID.MultiplayerClient && initializedFlag == 0f)
            {
                CreateSegments(npc, 35, ModContent.NPCType<AquaticScourgeBody>(), ModContent.NPCType<AquaticScourgeTail>());
                initializedFlag = 1f;
                npc.netUpdate = true;
            }

            // Determine hostility.
            CalamityMod.CalamityMod.bossKillTimes.TryGetValue(npc.type, out int revKillTime);
            npc.Calamity().KillTime = revKillTime;
            npc.damage = npc.defDamage;
            npc.boss = true;
            npc.chaseable = true;
            npc.dontTakeDamage = false;
            npc.Calamity().newAI[0] = 1f;

            // If there still was no valid target, swim away.
            if (npc.target < 0 || npc.target >= 255 || Main.player[npc.target].dead || !Main.player[npc.target].active)
            {
                DoBehavior_Despawn(npc);
                return false;
            }

            Player target = Main.player[npc.target];
            float lifeRatio = npc.life / (float)npc.lifeMax;
            bool phase2 = lifeRatio < Phase2LifeRatio;
            bool phase3 = lifeRatio < Phase3LifeRatio;
            bool enraged = !target.IsUnderwater() && !phase3;

            // Disable obnoxious water mechanics so that the player can fight the boss without interruption.
            if (!target.Calamity().ZoneAbyss)
            {
                target.breath = target.breathMax;
                target.ignoreWater = true;
                target.wingTime = target.wingTimeMax;
            }

            // Stop despawning.
            npc.timeLeft = 7200;

            // Be enraged.
            npc.Calamity().CurrentlyEnraged = enraged;
            npc.dontTakeDamage = enraged;

            switch ((AquaticScourgeAttackType)attackType)
            {
                case AquaticScourgeAttackType.SpawnAnimation:
                    DoBehavior_SpawnAnimation(npc, target, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.BubbleSpin:
                    DoBehavior_BubbleSpin(npc, target, phase2, enraged, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.RadiationPulse:
                    DoBehavior_RadiationPulse(npc, target, phase2, enraged, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.GasBreath:
                    DoBehavior_GasBreath(npc, target, phase2, phase3, enraged, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.WallHitCharges:
                    DoBehavior_WallHitCharges(npc, target, phase2, enraged, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.EnterSecondPhase:
                    DoBehavior_EnterSecondPhase(npc, target, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.PerpendicularSpikeBarrage:
                    DoBehavior_PerpendicularSpikeBarrage(npc, target, enraged, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.EnterFinalPhase:
                    DoBehavior_EnterFinalPhase(npc, target, ref attackTimer, ref acidVerticalLine);
                    break;
                case AquaticScourgeAttackType.AcidRain:
                    DoBehavior_AcidRain(npc, target, ref attackTimer);
                    break;
                case AquaticScourgeAttackType.SulphurousTyphoon:
                    DoBehavior_SulphurousTyphoon(npc, target, ref attackTimer);
                    break;
            }

            // Release acid mist based on the vertical line.
            if (acidVerticalLine != 0f)
            {
                if (Main.rand.NextBool(3))
                {
                    float skullSpawnPositionY = acidVerticalLine + Main.rand.NextFloat(250f);
                    if (target.Center.Y >= acidVerticalLine)
                        skullSpawnPositionY = target.Center.Y + Main.rand.NextFloat() * 200f;
                    Vector2 skullSpawnPosition = new(target.Center.X + Main.rand.NextFloatDirection() * 800f, skullSpawnPositionY);

                    Tile t = CalamityUtils.ParanoidTileRetrieval((int)(skullSpawnPosition.X / 16f), (int)(skullSpawnPosition.Y / 16f));
                    if (!t.HasTile && t.LiquidAmount > 0)
                    {
                        DesertProwlerSkullParticle skull = new(skullSpawnPosition, -Vector2.UnitY * 2f, new(70, 204, 80), Color.Purple, Main.rand.NextFloat(0.3f, 0.64f), 350f);
                        GeneralParticleHandler.SpawnParticle(skull);
                    }
                }

                for (int i = 0; i < 36; i++)
                {
                    Vector2 acidVelocity = -Vector2.UnitY.RotatedByRandom(0.36f) * Main.rand.NextFloat(1f, 3.5f);

                    // Determine where the acid mist should spawn.
                    // If the player is below the acid line, just draw it near them to sell the illusion that the water is incredibly acidic.
                    float acidSpawnPositionY = acidVerticalLine + Main.rand.NextFloat(200f);
                    if (target.Center.Y >= acidVerticalLine)
                        acidSpawnPositionY = target.Center.Y + Main.rand.NextFloat(-200f, 500f);
                    Vector2 acidSpawnPosition = new(target.Center.X + Main.rand.NextFloatDirection() * 1100f, acidSpawnPositionY);
                    Color acidColor = Color.Lerp(Color.LightSeaGreen, Color.LightGoldenrodYellow, Main.rand.NextFloat(0.6f));

                    Tile t = CalamityUtils.ParanoidTileRetrieval((int)(acidSpawnPosition.X / 16f), (int)(acidSpawnPosition.Y / 16f));
                    if (t.HasTile || t.LiquidAmount <= 0)
                        continue;

                    CloudParticle acidFoam = new(acidSpawnPosition, acidVelocity, acidColor, Color.White, 20, 0.3f)
                    {
                        Rotation = Main.rand.NextFloat(0.5f),
                    };
                    GeneralParticleHandler.SpawnParticle(acidFoam);
                }
            }

            // Update the acid hiss sound every frame.
            UpdateAcidHissSound(npc);

            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            attackTimer++;
            generalTimer++;

            return false;
        }
        #endregion AI

        #region Specific Behaviors

        public static void DoBehavior_Despawn(NPC npc)
        {
            npc.velocity.X *= 0.985f;
            if (npc.velocity.Y < 24f)
                npc.velocity.Y += 0.32f;

            if (npc.timeLeft > 210)
                npc.timeLeft = 210;

            Player closestTarget = Main.player[Player.FindClosest(npc.Center, 1, 1)];
            if (!npc.WithinRange(closestTarget.Center, 3200f))
                npc.active = false;
        }

        public static void DoBehavior_SpawnAnimation(NPC npc, Player target, ref float attackTimer)
        {
            int waterBubbleTime = 150;
            int acidFizzleTime = 150;
            int emergeTime = 96;
            int acidWarningDelay = 30;

            // Handle acoustic and visual triggers.
            Projectile bubbleProj = null;
            List<Projectile> goodBubbles = Utilities.AllProjectilesByID(ModContent.ProjectileType<WaterClearingBubble>()).ToList();
            if (goodBubbles.Any())
                bubbleProj = goodBubbles.First();

            if (attackTimer < waterBubbleTime)
            {
                // Spawn the bubbles and inform the player of their usage.
                if (attackTimer == acidWarningDelay)
                {
                    SoundEngine.PlaySound(CalamityMod.NPCs.Leviathan.Leviathan.EmergeSound);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 bubbleSpawnPosition = target.Center + Vector2.UnitY * 300f;
                        Utilities.NewProjectileBetter(bubbleSpawnPosition, -Vector2.UnitY * 2f, ModContent.ProjectileType<WaterClearingBubble>(), 0, 0f, -1, 0f, waterBubbleTime + acidFizzleTime);
                    }
                }

                float cameraInterpolant = Utils.GetLerpValue(acidWarningDelay, acidWarningDelay + 10f, attackTimer, true) * Utils.GetLerpValue(waterBubbleTime - 1f, waterBubbleTime - 10f, attackTimer, true);
                if (bubbleProj is not null)
                {
                    target.Infernum_Camera().ScreenFocusInterpolant = cameraInterpolant;
                    target.Infernum_Camera().ScreenFocusPosition = bubbleProj.Center;
                }
            }

            // Emit bubbles around the player.
            if (Main.netMode != NetmodeID.Server)
            {
                float bubbleSpawnRate = Utils.Remap(attackTimer, 0f, acidWarningDelay + 45f, 0.02f, 0.3f);
                for (int i = 0; i < 3; i++)
                {
                    if (Main.rand.NextFloat() > bubbleSpawnRate)
                        continue;

                    Vector2 bubbleSpawnPosition = target.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(200f, 675f);
                    if (Main.rand.NextBool(3))
                        bubbleSpawnPosition = target.Center + new Vector2(Main.rand.NextFloatDirection() * 540f, 650f);

                    // Don't spawn negative particles inside of the bubble.
                    if (bubbleProj is not null && bubbleSpawnPosition.WithinRange(bubbleProj.Center, bubbleProj.scale * 300f))
                        continue;

                    int goreID = 421;
                    if (Main.rand.NextBool(4))
                        goreID = 422;
                    if (Main.rand.NextBool(27) && attackTimer >= acidWarningDelay)
                        goreID = 423;
                    if (Main.rand.NextBool(3))
                        goreID -= 10;

                    Gore bubble = Gore.NewGorePerfect(npc.GetSource_FromThis(), bubbleSpawnPosition, -Vector2.UnitY * Main.rand.NextFloat(0.6f, 3f), goreID);
                    bubble.type = goreID;
                    bubble.timeLeft = 50;
                    bubble.scale = 0.4f;

                    for (int j = 0; j < 3; j++)
                    {
                        Vector2 acidVelocity = -Vector2.UnitY.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f);
                        acidVelocity.X += Abyss.AtLeftSideOfWorld.ToDirectionInt() * -4f;
                        Dust acid = Dust.NewDustPerfect(bubbleSpawnPosition + Main.rand.NextVector2Circular(80f, 80f), 256, acidVelocity);
                        acid.scale = 1.25f;
                        acid.fadeIn = 0.87f;
                        acid.noGravity = true;
                    }
                }
            }

            // Create acid around the player.
            if (attackTimer >= waterBubbleTime)
            {
                Color acidColor = Color.Lerp(Color.LightSeaGreen, Color.LightGoldenrodYellow, Main.rand.NextFloat(0.6f));

                for (int i = 0; i < 2; i++)
                {
                    // Don't spawn negative particles inside of the bubble.
                    Vector2 acidSpawnPosition = target.Center + new Vector2(Main.rand.NextFloatDirection() * 600f, 500f);
                    if (bubbleProj is not null && acidSpawnPosition.WithinRange(bubbleProj.Center, bubbleProj.scale * 300f))
                        continue;

                    MediumMistParticle acidFoam = new(acidSpawnPosition, -Vector2.UnitY.RotatedByRandom(0.67f) * Main.rand.NextFloat(4f, 20f), acidColor, Color.White, 0.5f, 255f, 0.02f);
                    GeneralParticleHandler.SpawnParticle(acidFoam);
                }
            }

            // Rise upward from below and attack.
            if (attackTimer == waterBubbleTime + acidFizzleTime)
            {
                SoundEngine.PlaySound(InfernumSoundRegistry.AquaticScourgeAppearSound);
                SoundEngine.PlaySound(Mauler.RoarSound);
                npc.velocity = npc.SafeDirectionTo(target.Center) * 9f;
                npc.netUpdate = true;
            }

            if (attackTimer == waterBubbleTime + acidFizzleTime + 34f)
            {
                // Release acid mist.
                for (int i = 0; i < 100; i++)
                {
                    Vector2 acidVelocity = -Vector2.UnitY.RotatedByRandom(0.87f) * Main.rand.NextFloat(8f, 14f);
                    acidVelocity.Y -= Main.rand.NextFloat(10f, 17f);
                    Vector2 acidSpawnPosition = target.Center + new Vector2(Main.rand.NextFloatDirection() * 500f, 500f);
                    Color acidColor = Color.Lerp(Color.LightSeaGreen, Color.LightGoldenrodYellow, Main.rand.NextFloat(0.6f));

                    MediumMistParticle acidMistFoam = new(acidSpawnPosition, acidVelocity, acidColor, Color.White, 2f, 255f, 0.009f);
                    GeneralParticleHandler.SpawnParticle(acidMistFoam);
                }
            }

            if (attackTimer >= waterBubbleTime + acidFizzleTime + emergeTime)
            {
                Utilities.DeleteAllProjectiles(false, ModContent.ProjectileType<FallingAcid>(), ModContent.ProjectileType<AcidBubble>(), ModContent.ProjectileType<SulphurousRockRubble>());
                SelectNextAttack(npc);
                return;
            }

            // Don't use the Calamity HP bar.
            npc.Calamity().ShouldCloseHPBar = true;

            // Stay below the player at first.
            if (attackTimer < waterBubbleTime + acidFizzleTime)
            {
                npc.damage = 0;
                npc.dontTakeDamage = true;
                npc.Center = target.Center + Vector2.UnitY * 1300f;
                npc.velocity = Vector2.UnitY * -3f;

                int bodyID = ModContent.NPCType<AquaticScourgeBody>();
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC n = Main.npc[i];
                    if (!n.active || n.realLife != npc.whoAmI || n.type != bodyID)
                        continue;

                    n.Center = npc.Center;
                }
            }
            else
                npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(target.Center) * npc.velocity.Length(), 0.09f).SafeNormalize(Vector2.UnitY) * npc.velocity.Length() * 1.011f;
        }

        public static void DoBehavior_BubbleSpin(NPC npc, Player target, bool phase2, bool enraged, ref float attackTimer)
        {
            int redirectTime = 75;
            int spinTime = 270;
            int bubbleReleaseRate = 22;
            int chargeRedirectTime = 16;
            int chargeTime = 56;
            float spinSpeed = 23f;
            float chargeSpeed = 30.5f;
            float bubbleShootSpeed = 13f;
            float spinArc = MathHelper.Pi / spinTime * 3f;

            if (phase2)
            {
                bubbleReleaseRate -= 6;
                chargeSpeed += 4f;
                bubbleShootSpeed += 2f;
            }

            if (enraged)
            {
                bubbleReleaseRate -= 9;
                chargeSpeed += 13f;
                bubbleShootSpeed += 7f;
            }

            bool charging = attackTimer >= redirectTime + spinTime + chargeRedirectTime;
            bool doneCharging = attackTimer >= redirectTime + spinTime + chargeRedirectTime + chargeTime;
            ref float bubbleReleaseCount = ref npc.Infernum().ExtraAI[0];

            // Don't do damage if not charging.
            if (!charging)
                npc.damage = 0;

            // Approach the target before spinning.
            if (attackTimer < redirectTime)
            {
                float flySpeed = Utils.Remap(attackTimer, 16f, redirectTime - 8f, 11f, spinSpeed);
                npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(target.Center) * flySpeed, 0.1f);

                if (npc.WithinRange(target.Center, 300f))
                {
                    attackTimer = redirectTime;
                    npc.netUpdate = true;
                }

                return;
            }

            // Spin in place.
            if (attackTimer < redirectTime + spinTime)
            {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(spinArc) * spinSpeed;
                if (!npc.WithinRange(target.Center, 600f))
                    npc.Center = npc.Center.MoveTowards(target.Center, 8f);
            }

            // Release bubbles at the player.
            if (attackTimer % bubbleReleaseRate == bubbleReleaseRate - 1f && attackTimer < redirectTime + spinTime)
            {
                SoundEngine.PlaySound(SoundID.Item95, npc.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int bubbleDamage = 135;
                    int bubbleID = ModContent.ProjectileType<AcidBubble>();
                    Vector2 bubbleShootVelocity = npc.SafeDirectionTo(target.Center) * bubbleShootSpeed;
                    if (bubbleReleaseCount % 8f == 3f)
                    {
                        bubbleDamage = 0;
                        bubbleID = ModContent.ProjectileType<WaterClearingBubble>();
                        bubbleShootVelocity *= 0.35f;
                    }

                    Utilities.NewProjectileBetter(npc.Center, bubbleShootVelocity, bubbleID, bubbleDamage, 0f);
                    bubbleReleaseCount++;
                    npc.netUpdate = true;
                }
            }

            // Redirect for a charge towards the target.
            if (attackTimer >= redirectTime + spinTime && !charging)
            {
                // Roar and pop all bubbles before the redirecting begins.
                if (attackTimer == redirectTime + spinTime)
                {
                    SoundEngine.PlaySound(DesertScourgeHead.RoarSound, target.Center);
                    PopAllBubbles();
                }

                Vector2 chargeVelocity = npc.SafeDirectionTo(target.Center) * chargeSpeed;
                npc.velocity = Vector2.Lerp(npc.velocity, chargeVelocity, 0.08f);

                if (attackTimer == redirectTime + spinTime + chargeRedirectTime - 1f)
                {
                    SoundEngine.PlaySound(InfernumSoundRegistry.AquaticScourgeChargeSound, target.Center);
                    npc.velocity = chargeVelocity;
                    npc.netUpdate = true;
                }
            }
            
            // Emit acid mist while charging.
            if (Main.netMode != NetmodeID.MultiplayerClient && charging && attackTimer % 2f == 0f)
            {
                Vector2 gasVelocity = npc.velocity.SafeNormalize(Main.rand.NextVector2Unit()).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(2f, 6f);
                Utilities.NewProjectileBetter(npc.Center, -gasVelocity.RotatedByRandom(0.3f), ModContent.ProjectileType<SulphuricGas>(), 135, 0f);
                Utilities.NewProjectileBetter(npc.Center, gasVelocity.RotatedByRandom(0.3f), ModContent.ProjectileType<SulphuricGas>(), 135, 0f);
            }

            if (doneCharging)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_RadiationPulse(NPC npc, Player target, bool phase2, bool enraged, ref float attackTimer)
        {
            int shootDelay = 90;
            int pulseReleaseRate = 120;
            int acidReleaseRate = 60;
            int shootTime = 480;
            int goodBubbleReleaseRate = 180;
            int acidShootCount = 4;
            float pulseMaxRadius = 425f;

            if (phase2)
            {
                acidReleaseRate -= 5;
                pulseMaxRadius += 56f;
            }

            if (enraged)
            {
                acidReleaseRate -= 25;
                pulseMaxRadius += 184f;
            }

            // Slowly move towards the target.
            Vector2 idealVelocity = npc.SafeDirectionTo(target.Center) * 5f;
            if (npc.WithinRange(target.Center, 200f))
                npc.velocity = (npc.velocity * 1.01f).ClampMagnitude(0f, idealVelocity.Length() * 1.5f);
            else
                npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, 0.125f);

            if (attackTimer < shootDelay)
                return;

            // Release radiation pulses.
            if (attackTimer % pulseReleaseRate == pulseReleaseRate - 1f)
            {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse, target.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Utilities.NewProjectileBetter(npc.Center, Vector2.Zero, ModContent.ProjectileType<RadiationPulse>(), 0, 0f, -1, 0f, pulseMaxRadius);
            }

            // Release acid.
            if (attackTimer % acidReleaseRate == acidReleaseRate - 1f)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < acidShootCount; i++)
                    {
                        float acidInterpolant = i / (float)(acidShootCount - 1f);
                        float angularVelocity = MathHelper.Lerp(0.016f, -0.016f, acidInterpolant);
                        Vector2 acidShootVelocity = npc.SafeDirectionTo(target.Center).RotatedBy(MathHelper.Lerp(-1.09f, 1.09f, acidInterpolant)) * 5f;
                        Utilities.NewProjectileBetter(npc.Center + acidShootVelocity * 5f, acidShootVelocity, ModContent.ProjectileType<AcceleratingArcingAcid>(), 135, 0f, -1, 0f, angularVelocity);
                    }
                }
            }

            // Release safe bubbles from below occasionally.
            if (attackTimer % goodBubbleReleaseRate == goodBubbleReleaseRate - 1f)
            {
                SoundEngine.PlaySound(SoundID.Item95, npc.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Utilities.NewProjectileBetter(target.Center + Vector2.UnitY * 450f, -Vector2.UnitY * 5f, ModContent.ProjectileType<WaterClearingBubble>(), 0, 0f);
            }

            if (attackTimer >= shootDelay + shootTime)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_WallHitCharges(NPC npc, Player target, bool phase2, bool enraged, ref float attackTimer)
        {
            int chargeCount = 5;
            int chargeDelay = 30;
            int minChargeTime = 36;
            int maxChargeTime = 59;
            int stunTime = 60;
            int rubbleCount = 9;
            bool insideBlocks = Collision.SolidCollision(npc.TopLeft, npc.width, npc.height);
            float chargeSpeed = 25f;
            float rubbleArc = 1.17f;
            float rubbleShootSpeed = 5f;
            float bubbleSpacing = 445f;
            float bubbleAreaCoverage = 1500f;

            if (phase2)
            {
                chargeDelay -= 4;
                stunTime -= 15;
                chargeSpeed += 2.75f;
                rubbleArc += 0.09f;
                bubbleSpacing -= 35f;
            }
            if (enraged)
            {
                chargeDelay -= 14;
                stunTime = 16;
                chargeSpeed = 41f;
                rubbleArc += 0.09f;
                rubbleCount += 5;
            }

            ref float chargeCounter = ref npc.Infernum().ExtraAI[0];
            ref float performingCharge = ref npc.Infernum().ExtraAI[1];
            ref float stunTimer = ref npc.Infernum().ExtraAI[2];
            ref float dontInteractWithBlocksYet = ref npc.Infernum().ExtraAI[3];

            if (chargeCounter <= 0f)
                chargeDelay += 60;

            // Attempt to move towards the target before charging.
            if (attackTimer <= chargeDelay)
            {
                float slowdownInterpolant = (float)Math.Pow(attackTimer / chargeDelay, 2D);
                Vector2 idealVelocity = npc.SafeDirectionTo(target.Center) * slowdownInterpolant * chargeSpeed;
                npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, 0.08f);
            }

            // Do the charge.
            if (attackTimer == chargeDelay)
            {
                SoundEngine.PlaySound(InfernumSoundRegistry.AquaticScourgeChargeSound, target.Center);
                npc.velocity = npc.SafeDirectionTo(target.Center) * chargeSpeed;
                npc.netUpdate = true;

                performingCharge = 1f;
                dontInteractWithBlocksYet = insideBlocks.ToInt();
            }

            // Handle post-stun behaviors.
            if (stunTimer >= 1f)
            {
                stunTimer--;
                if (stunTimer <= 0f)
                {
                    attackTimer = 0f;
                    dontInteractWithBlocksYet = 0f;
                    chargeCounter++;
                    if (chargeCounter >= chargeCount)
                        SelectNextAttack(npc);

                    // Release a single clean bubble on the third charge.
                    if (Main.netMode != NetmodeID.MultiplayerClient && chargeCounter == 3f)
                        Utilities.NewProjectileBetter(target.Center + Vector2.UnitY * 650f, -Vector2.UnitY * 5f, ModContent.ProjectileType<WaterClearingBubble>(), 0, 0f);

                    npc.netUpdate = true;
                }
            }

            if (performingCharge == 1f)
            {
                // If the scourge started in blocks when charging but has now left them, allow it to rebound.
                if (dontInteractWithBlocksYet == 1f && !insideBlocks)
                {
                    dontInteractWithBlocksYet = 0f;
                    npc.netUpdate = true;
                }

                // Perform rebound effects when tiles are hit. This takes a small amount of time before it can happen, so that charges aren't immediate.
                if (attackTimer >= minChargeTime && dontInteractWithBlocksYet == 0f && insideBlocks && npc.WithinRange(target.Center, 1200f))
                {
                    performingCharge = 0f;
                    stunTimer = stunTime;

                    // Create tile hit dust effects.
                    Collision.HitTiles(npc.TopLeft, -npc.velocity, npc.width, npc.height);

                    // Create rubble that aims backwards and some bubbles from below.
                    if (Main.rand.NextBool(25))
                        SoundEngine.PlaySound(InfernumSoundRegistry.SkeletronHeadBonkSound, target.Center);
                    else
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact, target.Center);

                    // Create some silly cartoon anger particles to give a bit of charm.
                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 angerParticleSpawnPosition = npc.Center + (MathHelper.TwoPi * i / 3f).ToRotationVector2() * 48f + Main.rand.NextVector2Circular(12f, 12f);
                        int angerParticleLifetime = Main.rand.Next(54, 67);
                        float angerParticleScale = Main.rand.NextFloat(0.36f, 0.58f);
                        CartoonAngerParticle angy = new(angerParticleSpawnPosition, Color.Red, Color.DarkRed, angerParticleLifetime, Main.rand.NextFloat(MathHelper.TwoPi), angerParticleScale);
                        GeneralParticleHandler.SpawnParticle(angy);
                    }

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        for (int i = 0; i < rubbleCount; i++)
                        {
                            float rubbleOffsetAngle = MathHelper.Lerp(-rubbleArc, rubbleArc, i / (float)(rubbleCount - 1f)) + Main.rand.NextFloatDirection() * 0.05f;
                            Vector2 rubbleVelocity = npc.SafeDirectionTo(target.Center).RotatedBy(rubbleOffsetAngle) * rubbleShootSpeed;
                            Utilities.NewProjectileBetter(npc.Center + rubbleVelocity * 3f, rubbleVelocity, ModContent.ProjectileType<SulphurousRockRubble>(), 135, 0f);
                        }
                        
                        for (float dx = -bubbleAreaCoverage; dx < bubbleAreaCoverage; dx += bubbleSpacing)
                        {
                            float bubbleSpeed = Main.rand.NextFloat(7f, 9f);
                            float verticalOffset = Math.Max(target.velocity.Y * 30f, 0f) + 600f;
                            Utilities.NewProjectileBetter(target.Center + new Vector2(dx, verticalOffset), -Vector2.UnitY * bubbleSpeed, ModContent.ProjectileType<AcidBubble>(), 135, 0f);
                        }
                    }

                    npc.velocity = npc.velocity.RotatedByRandom(0.32f) * -0.1f;
                    npc.netUpdate = true;
                }

                if (attackTimer >= chargeDelay + maxChargeTime)
                {
                    performingCharge = 0f;
                    stunTimer = 2f;
                    npc.velocity *= 0.5f;
                    npc.netUpdate = true;
                }

                // Emit acid mist while charging and not inside of tiles.
                if (Main.netMode != NetmodeID.MultiplayerClient && !insideBlocks && attackTimer % 5f == 0f)
                {
                    Vector2 gasVelocity = npc.velocity.SafeNormalize(Main.rand.NextVector2Unit()).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.7f, 3f);
                    Utilities.NewProjectileBetter(npc.Center, -gasVelocity.RotatedByRandom(0.25f), ModContent.ProjectileType<SulphuricGas>(), 135, 0f);
                    Utilities.NewProjectileBetter(npc.Center, gasVelocity.RotatedByRandom(0.25f), ModContent.ProjectileType<SulphuricGas>(), 135, 0f);
                }
            }

            // Skip to the next attack if the scourge is so far away that it won't be able to accomplish anything.
            if (!npc.WithinRange(target.Center, 2200f))
                SelectNextAttack(npc);
        }

        public static void DoBehavior_EnterSecondPhase(NPC npc, Player target, ref float attackTimer)
        {
            int roarDelay = 60;
            int chargeDelay = 60;
            Vector2 hoverDestination = target.Center + new Vector2((target.Center.X < npc.Center.X).ToDirectionInt() * 400f, -180f);
            ref float hasReachedDestination = ref npc.Infernum().ExtraAI[0];

            // Disable damage.
            npc.damage = 0;
            npc.dontTakeDamage = true;

            // Attempt to hover to the top left/right of the target at first.
            if (hasReachedDestination == 0f)
            {
                npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(hoverDestination) * 32f, 0.084f);
                if (npc.WithinRange(hoverDestination, 96f))
                {
                    hasReachedDestination = 1f;
                    npc.netUpdate = true;
                }

                // Don't let the attack timer increment.
                attackTimer = -1f;

                return;
            }

            // Slow down and look at the target threateningly before attacking.
            npc.velocity = Vector2.Lerp(npc.velocity, npc.SafeDirectionTo(target.Center) * 3f, 0.071f);

            // Roar after a short delay.
            if (attackTimer == roarDelay)
            {
                SoundEngine.PlaySound(Mauler.RoarSound);
                SoundEngine.PlaySound(InfernumSoundRegistry.SizzleSound);
                Main.LocalPlayer.Infernum_Camera().CurrentScreenShakePower = 8f;
                ScreenEffectSystem.SetBlurEffect(npc.Center, 2f, 45);
                ScreenEffectSystem.SetFlashEffect(npc.Center, 3f, 45);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Utilities.NewProjectileBetter(npc.Center, Vector2.Zero, ModContent.ProjectileType<RadiationPulse>(), 0, 0f, -1, 0f, 1200f);
            }

            // Disable the water poison effects.
            target.Calamity().SulphWaterPoisoningLevel = 0f;

            if (attackTimer >= roarDelay + chargeDelay)
            {
                npc.velocity = npc.SafeDirectionTo(target.Center) * 50f;
                SelectNextAttack(npc);
            }
        }

        public static void DoBehavior_PerpendicularSpikeBarrage(NPC npc, Player target, bool enraged, ref float attackTimer)
        {
            int slowdownTime = 30;
            int attackTime = 60;
            int shootCount = 2;
            float selfHurtHPRatio = 0.0075f;

            if (enraged)
            {
                shootCount = 1;
                selfHurtHPRatio = 0f;
            }

            ref float hasReachedPlayer = ref npc.Infernum().ExtraAI[0];
            ref float shootCounter = ref npc.Infernum().ExtraAI[1];
            ref float shudderOffset = ref npc.Infernum().ExtraAI[2];
            ref float segmentShudderIndex = ref npc.Infernum().ExtraAI[3];

            if (shootCounter <= 0f)
                slowdownTime += 48;

            // Approach the player.
            if (hasReachedPlayer == 0f)
            {
                Vector2 hoverDestination = target.Center - Vector2.UnitY * 250f;
                Vector2 idealVelocity = npc.SafeDirectionTo(hoverDestination) * (attackTimer * 0.05f + 20f);
                npc.damage = 0;
                npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, 0.075f);

                if (npc.WithinRange(target.Center, 600f) && !npc.WithinRange(target.Center, 250f))
                {
                    hasReachedPlayer = 1f;
                    attackTimer = 0f;
                    npc.velocity = npc.velocity.ClampMagnitude(4f, 30f);
                    npc.netUpdate = true;
                }

                return;
            }

            // Slow down before releasing spikes from the body segments.
            if (attackTimer <= slowdownTime)
            {
                npc.velocity *= 0.96f;
                shudderOffset = 0f;
                segmentShudderIndex = 0f;
            }

            if (attackTimer == slowdownTime)
            {
                if (selfHurtHPRatio > 0f)
                    npc.StrikeNPCNoInteraction((int)(npc.lifeMax * selfHurtHPRatio), 0f, 0);

                SoundEngine.PlaySound(Mauler.RoarSound with { Pitch = 0.2f }, target.Center);
                SoundEngine.PlaySound(InfernumSoundRegistry.AquaticScourgeGoreSound, target.Center);
                ReleaseSpikesFromSegments(npc, shootCount, (int)shootCounter);

                if (shootCounter >= shootCount - 1f)
                {
                    int bodyID = ModContent.NPCType<AquaticScourgeBody>();
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC n = Main.npc[i];
                        if (!n.active || n.type != bodyID || n.realLife != npc.whoAmI)
                            continue;

                        n.Infernum().ExtraAI[0] = 0f;
                        n.netUpdate = true;
                    }
                }

                shudderOffset = 20f;
                npc.netUpdate = true;
            }

            // Shudder if necessary.
            if (shudderOffset > 0f)
            {
                segmentShudderIndex = Utils.GetLerpValue(slowdownTime, slowdownTime + attackTime - 10f, attackTimer, true) * 35f;
                shudderOffset -= 0.15f;

                if (segmentShudderIndex <= 3f)
                    npc.Center += npc.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloatDirection() * shudderOffset;
            }

            if (attackTimer >= slowdownTime + attackTime)
            {
                attackTimer = 0f;
                hasReachedPlayer = 0f;
                shootCounter++;
                if (shootCounter >= shootCount)
                    SelectNextAttack(npc);

                npc.netUpdate = true;
            }
        }

        public static void DoBehavior_GasBreath(NPC npc, Player target, bool phase2, bool phase3, bool enraged, ref float attackTimer)
        {
            int attackTime = 480;
            int rubbleReleaseRate = 90;
            float idealRotation = npc.AngleTo(target.Center);
            float turnAngularVelocity = BossRushEvent.BossRushActive ? 0.0384f : 0.027f;
            float movementSpeed = MathHelper.Lerp(14.5f, 38f, Utils.GetLerpValue(640f, 3000f, npc.Distance(target.Center), true));

            if (phase2)
            {
                attackTime += 60;
                rubbleReleaseRate -= 30;
                turnAngularVelocity += 0.0048f;
                movementSpeed += 2.4f;
            }
            if (phase3)
            {
                rubbleReleaseRate -= 8;
                turnAngularVelocity += 0.0098f;
                movementSpeed += 5.6f;
            }
            if (enraged)
            {
                rubbleReleaseRate = int.MaxValue;
                movementSpeed = 36f;
                turnAngularVelocity = 0.1f;
            }

            ref float hasGottenNearPlayer = ref npc.Infernum().ExtraAI[0];

            // Fly more aggressively if the target is close to the safety bubble.
            Projectile closestBubble = null;
            List<Projectile> goodBubbles = Utilities.AllProjectilesByID(ModContent.ProjectileType<WaterClearingBubble>()).ToList();
            if (goodBubbles.Any())
            {
                closestBubble = goodBubbles.OrderBy(b => b.DistanceSQ(target.Center)).First();
                float closenessInterpolant = Utils.GetLerpValue(400f, 180f, closestBubble.Distance(target.Center), true);
                movementSpeed += closenessInterpolant * 9f;
                turnAngularVelocity *= MathHelper.Lerp(1f, 1.35f, closenessInterpolant);
            }

            // Create a good bubble above the target on the first frame.
            // If it spawns inside of blocks, move it up.
            if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer == 1f && !phase3)
            {
                Vector2 bubbleSpawnPosition = target.Center - Vector2.UnitY * 540f;
                while (Collision.SolidCollision(bubbleSpawnPosition - Vector2.One * 125f, 250, 250))
                    bubbleSpawnPosition.Y += 16f;

                Utilities.NewProjectileBetter(bubbleSpawnPosition, Vector2.Zero, ModContent.ProjectileType<WaterClearingBubble>(), 0, 0f, -1, 0f, attackTime - 25f);
            }

            // Fly towards the target.
            if (!npc.WithinRange(target.Center, 240f))
            {
                float newSpeed = MathHelper.Lerp(npc.velocity.Length(), movementSpeed, turnAngularVelocity * 3.2f);
                npc.velocity = npc.velocity.RotateTowards(idealRotation, turnAngularVelocity, true);
                npc.velocity = Vector2.Lerp(npc.velocity * newSpeed, idealRotation.ToRotationVector2() * newSpeed, 0.03f);

                // Vomit a bunch of gas if the scourge was close to the target previously but isn't anymore.
                if (hasGottenNearPlayer == 1f)
                {
                    SoundEngine.PlaySound(InfernumSoundRegistry.DesertScourgeShortRoar, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item66, npc.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        for (int i = 0; i < 25; i++)
                        {
                            Vector2 gasVelocity = npc.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.43f) * Main.rand.NextFloat(5f, 40f) + npc.velocity * 0.6f;
                            Utilities.NewProjectileBetter(npc.Center + gasVelocity * 4f, gasVelocity, ModContent.ProjectileType<SulphuricGasDebuff>(), 0, 0f);
                        }
                    }

                    hasGottenNearPlayer = 0f;
                    npc.netUpdate = true;
                }
            }
            else
            {
                if (npc.WithinRange(target.Center, 200f))
                    hasGottenNearPlayer = 1f;
                npc.velocity *= 1.02f;
            }

            // Release rubble from the ceiling.
            if (attackTimer % rubbleReleaseRate == rubbleReleaseRate - 1f)
            {
                if (!phase3)
                    SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack, target.Center);
                for (int i = 0; i < 5; i++)
                {
                    int projID = ModContent.ProjectileType<SulphurousRockRubble>();
                    Vector2 rubbleSpawnPosition = Utilities.GetGroundPositionFrom(target.Center + new Vector2(Main.rand.NextFloatDirection() * 800f, -30f), new Searches.Up(50));
                    if (MathHelper.Distance(rubbleSpawnPosition.Y, target.Center.Y) < 150f || phase3)
                        rubbleSpawnPosition.Y = target.Center.Y - 980f + Main.rand.NextFloatDirection() * 40f;
                    if (phase3)
                        projID = ModContent.ProjectileType<FallingAcid>();

                    if (Main.netMode != NetmodeID.MultiplayerClient && !target.WithinRange(rubbleSpawnPosition, 300f))
                        Utilities.NewProjectileBetter(rubbleSpawnPosition, Vector2.UnitY * 11f, projID, 135, 0f, -1, 0f, phase3 ? 1f :0f);
                }
            }

            if (attackTimer >= attackTime)
                SelectNextAttack(npc);
        }

        public static void DoBehavior_EnterFinalPhase(NPC npc, Player target, ref float attackTimer, ref float acidVerticalLine)
        {
            ref float rumbleSoundSlot = ref npc.localAI[2];

            // Intiialize the acid vertical line.
            // It will try to spawn a set distance below the player for the sake of fair time in being able to get out of the water, but if they're so
            // low that they're in the abyss, a limit is imposed so that the water doesn't take an eternity to rise to the surface.
            if (acidVerticalLine == 0f)
            {
                acidVerticalLine = MathHelper.Min(target.Bottom.Y + 2000f, CustomAbyss.AbyssTop * 16f + 900f);
                Utilities.DisplayText("A deluge of acid is quickly rising from below!", Color.GreenYellow);

                SoundEngine.PlaySound(Mauler.RoarSound);
                SoundEngine.PlaySound(InfernumSoundRegistry.SizzleSound);
                Main.LocalPlayer.Infernum_Camera().CurrentScreenShakePower = 10f;
                ScreenEffectSystem.SetBlurEffect(npc.Center, 2f, 45);
                ScreenEffectSystem.SetFlashEffect(npc.Center, 3f, 45);

                npc.netUpdate = true;
            }

            float lineTop = SulphurousSea.YStart * 16f + 20f;
            bool soundIsValid = SoundEngine.TryGetActiveSound(SlotId.FromFloat(rumbleSoundSlot), out ActiveSound rumbleSound);
            bool needsToPlayRumble = !soundIsValid || rumbleSoundSlot == 0f;
            float rumbleVolume = Utils.GetLerpValue(acidVerticalLine - 2900f, acidVerticalLine - 1650f, target.Center.Y, true) * Utils.GetLerpValue(lineTop + 300f, lineTop + 800f, acidVerticalLine, true) * 1.8f + 0.001f;
            if (needsToPlayRumble && acidVerticalLine >= lineTop + 300f)
                rumbleSoundSlot = SoundEngine.PlaySound(LeviathanSpawner.RumbleSound, target.Center).ToFloat();
            if (rumbleSound is not null)
                rumbleSound.Volume = rumbleVolume;

            // Make the acid rise upward.
            if (acidVerticalLine >= lineTop)
                acidVerticalLine -= Utils.Remap(acidVerticalLine, (float)lineTop + 500f, (float)lineTop, 10.5f, 4f);
            else
            {
                acidVerticalLine = lineTop;
                rumbleSound?.Stop();
                SelectNextAttack(npc);
            }

            // Make the scourge move upward slowly, not taking or doing damage.
            npc.damage = 0;
            npc.dontTakeDamage = true;
            if (npc.Center.Y < acidVerticalLine)
            {
                float flySpeed = acidVerticalLine >= lineTop - 150f ? 8f : 17f;
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitY * -flySpeed, 0.04f);
            }
            else
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitY * -25f, 0.05f);
            npc.velocity.X = (float)Math.Sin(MathHelper.TwoPi * attackTimer / 90f) * 12f;

            // Create very strong rain.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                CalamityUtils.StartRain(true, true);
                Main.cloudBGActive = 1f;
                Main.numCloudsTemp = 150;
                Main.numClouds = Main.numCloudsTemp;
                Main.windSpeedCurrent = 0.84f;
                Main.windSpeedTarget = Main.windSpeedCurrent;
                Main.maxRaining = 0.85f;
            }
        }

        public static void DoBehavior_AcidRain(NPC npc, Player target, ref float attackTimer)
        {
            int lungeCount = 2;
            int vomitShootRate = 15;
            int vomitBurstCount = 3;
            int acidPerVomitBurst = 20;
            int bubbleReleaseRate = 45;
            float upwardLungeDistance = 450f;
            ref float lungeCounter = ref npc.Infernum().ExtraAI[0];
            ref float attackSubstate = ref npc.Infernum().ExtraAI[1];

            // Fall into the ground if the first charge started off with the scourge far in the air.
            if (lungeCounter <= 0f && attackTimer == 1f && npc.Center.Y < target.Center.Y - upwardLungeDistance - 500f)
            {
                attackSubstate = 2f;
                attackTimer = 0f;
                npc.netUpdate = true;
            }

            // Release acid bubbles below the target.
            if (Main.netMode != NetmodeID.MultiplayerClient && attackTimer % bubbleReleaseRate == bubbleReleaseRate - 1f)
            {
                Vector2 bubbleSpawnPosition = target.Bottom + Vector2.UnitY * 660f;
                if (WorldUtils.Find((target.Bottom + Vector2.UnitY * 300f).ToTileCoordinates(), Searches.Chain(new Searches.Down(40), new CustomTileConditions.IsWaterOrSolid()), out Point result))
                    bubbleSpawnPosition = result.ToWorldCoordinates();
                Utilities.NewProjectileBetter(bubbleSpawnPosition + Vector2.UnitY * 136f, -Vector2.UnitY * 6f, ModContent.ProjectileType<AcidBubble>(), 140, 0f);
            }

            // Roar at the start of the first charge.
            if (lungeCounter <= 0f && attackTimer == 1f && attackSubstate == 0f)
                SoundEngine.PlaySound(Mauler.RoarSound, target.Center);

            switch ((int)attackSubstate)
            {
                // Rise upward until sufficiently above the target.
                case 0:
                    float verticalSpeedAdditive = attackTimer * 0.05f;
                    bool readyToReleaseAcid = npc.Center.Y < target.Center.Y - upwardLungeDistance;

                    // Accelerate upward if almost above the target.
                    if (npc.Center.Y < target.Center.Y + 200f)
                    {
                        Vector2 idealVelocity = Vector2.UnitY * -(verticalSpeedAdditive + 25f);
                        npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, 0.025f);
                    }

                    // If below the target, move upward while attempting to meet their horizontal position.
                    else
                    {
                        Vector2 idealVelocity = new(npc.SafeDirectionTo(target.Center).X * 27f, -verticalSpeedAdditive - 24f);
                        if (MathHelper.Distance(target.Center.X, npc.Center.X) >= 600f)
                            idealVelocity.X *= 2f;

                        npc.velocity = Vector2.Lerp(npc.velocity, idealVelocity, 0.11f).MoveTowards(idealVelocity, 0.8f);
                    }

                    if (readyToReleaseAcid)
                    {
                        attackSubstate = 1f;
                        attackTimer = 0f;
                        npc.velocity.Y *= 0.36f;
                        npc.netUpdate = true;
                    }

                    break;

                // Vomit bursts of acid into the air.
                case 1:
                    // Disable damage.
                    npc.damage = 0;

                    // Gain horizontal momentum in anticipation of the upcoming fall.
                    npc.velocity.X = MathHelper.Lerp(npc.velocity.X, Math.Sign(npc.velocity.X) * 7.5f, 0.064f);

                    // Release the vomit bursts.
                    if (attackTimer % vomitShootRate == 0f)
                    {
                        SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center);

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int vomitShootCounter = (int)(attackTimer / vomitShootRate);
                            float verticalVomitShootSpeed = MathHelper.Lerp(-10f, -15f, vomitShootCounter / (float)(vomitBurstCount - 1f));
                            for (int i = 0; i < acidPerVomitBurst; i++)
                            {
                                Vector2 acidVelocity = new Vector2(MathHelper.Lerp(-28f, 28f, i / (float)(acidPerVomitBurst - 1f)), verticalVomitShootSpeed) + Main.rand.NextVector2Circular(0.3f, 0.3f);
                                Utilities.NewProjectileBetter(npc.Center + acidVelocity, acidVelocity, ModContent.ProjectileType<FallingAcid>(), 140, 0f);
                            }
                        }
                    }

                    if (attackTimer >= vomitShootRate * vomitBurstCount)
                    {
                        attackTimer = 0f;
                        attackSubstate = 2f;
                        npc.velocity.Y += 3f;
                        npc.netUpdate = true;
                    }

                    break;

                // Fall into the ground in anticipation of the next rise. The scourge does not do damage during this subphase.
                case 2:
                    // Disable damage.
                    npc.damage = 0;

                    npc.velocity.X *= 0.99f;
                    npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y + 0.5f, -32f, 25f);
                    if (npc.Center.Y >= target.Center.Y + 1450f)
                    {
                        attackTimer = 0f;
                        attackSubstate = 0f;
                        lungeCounter++;
                        if (lungeCounter >= lungeCount)
                        {
                            Utilities.DeleteAllProjectiles(false, ModContent.ProjectileType<FallingAcid>());
                            SelectNextAttack(npc);
                        }

                        npc.velocity.Y *= 0.5f;
                        npc.netUpdate = true;
                    }
                    break;
            }
        }

        public static void DoBehavior_SulphurousTyphoon(NPC npc, Player target, ref float attackTimer)
        {
            int bubbleShootRate = 30;

            // Don't do contact damage.
            npc.damage = 0;

            // Create the tornado on the first frame.
            if (attackTimer <= 1f)
            {
                SoundEngine.PlaySound(CalamityMod.NPCs.Leviathan.Leviathan.EmergeSound);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    float tornadoMoveDirection = (target.Center.X < 3000f).ToDirectionInt();
                    Vector2 tornadoSpawnPosition = target.Center + Vector2.UnitY * 500f;
                    Utilities.NewProjectileBetter(tornadoSpawnPosition, Vector2.UnitY * -3f, ModContent.ProjectileType<SulphuricTornado>(), 250, 0f, -1, 0f, tornadoMoveDirection);
                }
                return;
            }

            // Circle around the tornado.
            List<Projectile> tornadoes = Utilities.AllProjectilesByID(ModContent.ProjectileType<SulphuricTornado>()).ToList();
            if (!tornadoes.Any())
            {
                SelectNextAttack(npc);
                return;
            }

            // Circle around the tornado.
            Projectile tornado = tornadoes.First();
            Vector2 hoverPosition = tornado.Center + (MathHelper.TwoPi * attackTimer / 120f).ToRotationVector2() * new Vector2(708f, -330f);
            hoverPosition.Y += (float)Math.Sin(MathHelper.TwoPi * attackTimer / 120f) * 15f;

            npc.velocity = npc.SafeDirectionTo(hoverPosition, (npc.rotation - MathHelper.PiOver2).ToRotationVector2()) * MathHelper.Clamp(npc.Distance(hoverPosition), 0.4f, 50f);
            npc.Center = npc.Center.MoveTowards(hoverPosition, 2f);

            // Periodically vomit bubbles at the target.
            if (attackTimer % bubbleShootRate == bubbleShootRate - 1f && npc.WithinRange(hoverPosition, 120f))
            {
                SoundEngine.PlaySound(SoundID.Item95, npc.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int bubbleDamage = 145;
                    int bubbleID = ModContent.ProjectileType<AcidBubble>();
                    Vector2 bubbleShootVelocity = npc.SafeDirectionTo(target.Center) * 16f;
                    Utilities.NewProjectileBetter(npc.Center, bubbleShootVelocity, bubbleID, bubbleDamage, 0f);
                    npc.netUpdate = true;
                }
            }
        }

        #endregion Specific Behaviors

        #region AI Utility Methods
        public static void CreateSegments(NPC npc, int wormLength, int bodyType, int tailType)
        {
            int previousIndex = npc.whoAmI;
            for (int i = 0; i < wormLength; i++)
            {
                int nextIndex;
                if (i < wormLength - 1)
                    nextIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, bodyType, npc.whoAmI + 1);
                else
                    nextIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, tailType, npc.whoAmI + 1);

                Main.npc[nextIndex].realLife = npc.whoAmI;
                Main.npc[nextIndex].ai[2] = npc.whoAmI;
                Main.npc[nextIndex].ai[1] = previousIndex;
                Main.npc[nextIndex].ai[3] = i;
                Main.npc[previousIndex].ai[0] = nextIndex;

                // Force sync the new segment into existence.
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, nextIndex, 0f, 0f, 0f, 0);

                previousIndex = nextIndex;
            }
        }

        public static void SelectNextAttack(NPC npc)
        {
            npc.Opacity = 1f;

            float lifeRatio = npc.life / (float)npc.lifeMax;
            bool phase2 = lifeRatio < Phase2LifeRatio;
            bool phase3 = lifeRatio < Phase3LifeRatio;
            AquaticScourgeAttackType nextAttack = AquaticScourgeAttackType.GasBreath;
            ref float currentPhase = ref npc.Infernum().ExtraAI[CurrentPhaseIndex];
            ref float attackCycleIndex = ref npc.Infernum().ExtraAI[AttackCycleIndex];

            attackCycleIndex++;
            if (currentPhase == 0f)
                nextAttack = Phase1AttackCycle[(int)(attackCycleIndex % Phase1AttackCycle.Length)];
            if (currentPhase == 1f)
                nextAttack = Phase2AttackCycle[(int)(attackCycleIndex % Phase2AttackCycle.Length)];
            if (currentPhase == 2f)
                nextAttack = Phase3AttackCycle[(int)(attackCycleIndex % Phase3AttackCycle.Length)];

            // Increment the phase values.
            if (phase2 && currentPhase <= 0f)
            {
                currentPhase = 1f;
                nextAttack = AquaticScourgeAttackType.EnterSecondPhase;
                attackCycleIndex = -1f;
            }
            if (phase3 && currentPhase <= 1f)
            {
                currentPhase = 2f;
                nextAttack = AquaticScourgeAttackType.EnterFinalPhase;
                attackCycleIndex = -1f;
            }

            // Get a new target.
            npc.TargetClosest();

            npc.ai[2] = (int)nextAttack;
            npc.ai[3] = 0f;

            // Set an 2 second delay up after the attack.
            npc.Infernum().ExtraAI[5] = 120f;
            for (int i = 0; i < 5; i++)
                npc.Infernum().ExtraAI[i] = 0f;
            npc.noTileCollide = true;
            npc.netUpdate = true;
        }

        public static void ReleaseSpikesFromSegments(NPC npc, int segmentInterval, int segmentIndex)
        {
            int bodyID = ModContent.NPCType<AquaticScourgeBody>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (!n.active || n.type != bodyID || n.realLife != npc.whoAmI || n.ai[3] % segmentInterval != segmentIndex)
                    continue;

                foreach (Vector2 spikePosition in AquaticScourgeBodyBehaviorOverride.GetSpikePositions(n))
                {
                    Vector2 spikeVelocity = (spikePosition - n.Center).SafeNormalize(Vector2.UnitY) * 4f;
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Utilities.NewProjectileBetter(spikePosition, spikeVelocity, ModContent.ProjectileType<AquaticScourgeBodySpike>(), 140, 0f, -1, 120f);

                    // Release blood from the segment.
                    for (int j = 0; j < 3; j++)
                    {
                        int bloodLifetime = Main.rand.Next(22, 36);
                        float bloodScale = Main.rand.NextFloat(0.6f, 0.8f);
                        Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat());
                        bloodColor = Color.Lerp(bloodColor, new Color(51, 22, 94), Main.rand.NextFloat(0.65f));

                        if (Main.rand.NextBool(20))
                            bloodScale *= 2f;

                        Vector2 bloodVelocity = spikeVelocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.81f) * Main.rand.NextFloat(11f, 23f);
                        bloodVelocity.Y -= 12f;
                        BloodParticle blood = new(spikePosition, bloodVelocity, bloodLifetime, bloodScale, bloodColor);
                        GeneralParticleHandler.SpawnParticle(blood);
                    }
                    for (int j = 0; j < 2; j++)
                    {
                        float bloodScale = Main.rand.NextFloat(0.2f, 0.33f);
                        Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat(0.5f, 1f));
                        Vector2 bloodVelocity = spikeVelocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(9f, 14.5f);
                        BloodParticle2 blood = new(spikePosition, bloodVelocity, 20, bloodScale, bloodColor);
                        GeneralParticleHandler.SpawnParticle(blood);
                    }
                }

                CloudParticle bloodCloud = new(n.Center, Main.rand.NextVector2Circular(12f, 12f), Color.Red, Color.DarkRed, 270, Main.rand.NextFloat(1.9f, 2.12f));
                GeneralParticleHandler.SpawnParticle(bloodCloud);

                n.Infernum().ExtraAI[0] = 0.001f;
                n.netUpdate = true;
            }
        }

        public static void ApplySulphuricPoisoningBoostToPlayersInArea(Vector2 center, float radius, float boostFactor)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead)
                    continue;

                if (CalamityUtils.CircularHitboxCollision(center, radius, p.Hitbox))
                {
                    float increment = boostFactor / CalamityPlayer.SulphSeaWaterSafetyTime;
                    if (p.Calamity().sulphurskin)
                        increment *= 0.5f;
                    if (p.Calamity().sulfurSet)
                        increment *= 0.5f;
                    p.Calamity().SulphWaterPoisoningLevel += increment;
                }
            }
        }

        public static void PopAllBubbles()
        {
            List<int> bubbles = new()
            {
                ModContent.ProjectileType<AcidBubble>(),
                ModContent.ProjectileType<WaterClearingBubble>()
            };

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || !bubbles.Contains(p.type))
                    continue;
                
                for (int j = 0; j < 45; j++)
                {
                    Dust bubble = Dust.NewDustPerfect(p.Center + Main.rand.NextVector2Circular(32f, 32f), 256);
                    bubble.velocity = (MathHelper.TwoPi * i / 45f + Main.rand.NextFloatDirection() * 0.1f).ToRotationVector2() * Main.rand.NextFloat(1f, 16f);
                    bubble.scale = Main.rand.NextFloat(1f, 1.5f);
                    bubble.noGravity = true;
                }
                p.timeLeft = Main.rand.Next(10, 20);
            }
        }

        public static void UpdateAcidHissSound(NPC npc)
        {
            Player target = Main.player[npc.target];
            float acidVerticalLine = npc.Infernum().ExtraAI[AcidVerticalLineIndex];
            ref float hissSound = ref npc.localAI[0];
            if (acidVerticalLine <= 0f)
                return;

            // Determine the volume of the acid sound.
            float volumeInterpolant = Utils.GetLerpValue(500f, 60f, MathHelper.Distance(target.Center.Y, acidVerticalLine), true);
            if (target.Center.Y >= acidVerticalLine)
                volumeInterpolant = 1f;

            // Initialize the sound if necessary.
            bool shouldStopSound = volumeInterpolant <= 0f || npc.life <= 0 || !npc.active;
            if (hissSound == 0f && !shouldStopSound)
                hissSound = SoundEngine.PlaySound(InfernumSoundRegistry.AquaticScourgeAcidHissLoopSound, npc.Center).ToFloat();

            // Update the sound's position.
            if (SoundEngine.TryGetActiveSound(SlotId.FromFloat(hissSound), out var t) && t.IsPlaying)
            {
                t.Position = target.Center;
                t.Volume = volumeInterpolant;
                if (shouldStopSound)
                    t.Stop();
            }
            else
                hissSound = 0f;
        }

        #endregion AI Utility Methods
    }
}

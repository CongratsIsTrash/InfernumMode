﻿using CalamityMod;
using CalamityMod.NPCs;
using CalamityMod.NPCs.ExoMechs.Artemis;
using InfernumMode.OverridingSystem;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

using static InfernumMode.BehaviorOverrides.BossAIs.Draedon.ApolloBehaviorOverride;

namespace InfernumMode.BehaviorOverrides.BossAIs.Draedon
{
	public class ArtemisBehaviorOverride : NPCBehaviorOverride
	{
		public override int NPCOverrideType => ModContent.NPCType<Artemis>();

		public override NPCOverrideContext ContentToOverride => NPCOverrideContext.NPCAI | NPCOverrideContext.NPCFindFrame | NPCOverrideContext.NPCPreDraw;

		#region AI
		public override bool PreAI(NPC npc)
		{
			// Despawn if Apollo is not present.
			if (!Main.npc.IndexInRange(npc.realLife) || !Main.npc[npc.realLife].active)
			{
				npc.active = false;
				return false;
			}

			// Define the life ratio.
			float lifeRatio = npc.life / (float)npc.lifeMax;

			// Define the whoAmI variable.
			CalamityGlobalNPC.draedonExoMechTwinGreen = npc.whoAmI;

			// Define attack variables.
			ref float attackState = ref npc.ai[0];
			ref float attackTimer = ref npc.ai[1];
			ref float hoverSide = ref npc.ai[2];
			ref float phaseTransitionAnimationTime = ref npc.ai[3];
			ref float frame = ref npc.localAI[0];
			ref float hasDoneInitializations = ref npc.localAI[1];
			ref float hasSummonedComplementMech = ref npc.Infernum().ExtraAI[7];
			ref float complementMechIndex = ref npc.Infernum().ExtraAI[10];
			ref float wasNotInitialSummon = ref npc.Infernum().ExtraAI[11];
			ref float finalMechIndex = ref npc.Infernum().ExtraAI[12];
			NPC finalMech = ExoMechManagement.FindFinalMech();
			NPC apollo = Main.npc[npc.realLife];

			if (Main.netMode != NetmodeID.MultiplayerClient && hasDoneInitializations == 0f)
			{
				complementMechIndex = -1f;
				finalMechIndex = -1f;
				hasDoneInitializations = 1f;
				npc.netUpdate = true;
			}

			// Reset things and use variables from Apollo.
			npc.damage = 0;
			npc.defDamage = 640;
			npc.dontTakeDamage = apollo.dontTakeDamage;
			npc.target = apollo.target;
			npc.life = apollo.life;
			npc.lifeMax = apollo.lifeMax;

			TwinsAttackType apolloAttackType = (TwinsAttackType)(int)apollo.ai[0];
			if (apolloAttackType != TwinsAttackType.SpecialAttack_LaserRayScarletBursts && apolloAttackType != TwinsAttackType.SpecialAttack_PlasmaCharges)
			{
				attackState = (int)apollo.ai[0];
				attackTimer = apollo.ai[1];
			}
			hoverSide = -apollo.ai[2];
			phaseTransitionAnimationTime = apollo.ai[3];
			npc.Infernum().ExtraAI[7] = apollo.Infernum().ExtraAI[7];
			npc.Infernum().ExtraAI[10] = apollo.Infernum().ExtraAI[10];
			npc.Infernum().ExtraAI[11] = apollo.Infernum().ExtraAI[11];
			npc.Infernum().ExtraAI[12] = apollo.Infernum().ExtraAI[12];
			npc.Calamity().newAI[0] = (int)Artemis.Phase.Charge;

			// Become invincible and disappear if the final mech is present.
			npc.Calamity().newAI[1] = 0f;
			if (finalMech != null && finalMech != apollo)
			{
				npc.Opacity = MathHelper.Clamp(npc.Opacity - 0.08f, 0f, 1f);
				attackTimer = 0f;
				attackState = (int)TwinsAttackType.VanillaShots;
				npc.Calamity().newAI[1] = (int)Artemis.SecondaryPhase.PassiveAndImmune;
				npc.ModNPC<Artemis>().ChargeFlash = 0f;
				npc.Calamity().ShouldCloseHPBar = true;
				npc.dontTakeDamage = true;
			}
			else
				npc.Opacity = MathHelper.Clamp(npc.Opacity + 0.08f, 0f, 1f);

			// Get a target.
			Player target = Main.player[npc.target];

			// Despawn if the target is gone.
			if (!target.active || target.dead)
			{
				npc.TargetClosest(false);
				target = Main.player[npc.target];
				if (!target.active || target.dead)
					npc.active = false;
			}

			// Handle the second phase transition.
			if (phaseTransitionAnimationTime < Phase2TransitionTime && lifeRatio < ExoMechManagement.Phase2LifeRatio)
			{
				npc.dontTakeDamage = true;
				npc.ModNPC<Artemis>().ChargeFlash = 0f;
				DoBehavior_DoPhaseTransition(npc, target, ref frame, hoverSide, phaseTransitionAnimationTime);
				return false;
			}

			switch ((TwinsAttackType)(int)attackState)
			{
				case TwinsAttackType.VanillaShots:
					DoBehavior_ReleasePredictiveLasers(npc, target, hoverSide, ref frame, ref attackTimer);
					break;
				case TwinsAttackType.FireCharge:
					DoBehavior_FireCharge(npc, target, hoverSide, ref frame, ref attackTimer);
					break;
				case TwinsAttackType.SpecialAttack_PlasmaCharges:
					DoBehavior_PlasmaCharges(npc, target, hoverSide, ref frame, ref attackTimer);
					break;
				case TwinsAttackType.SpecialAttack_LaserRayScarletBursts:
					DoBehavior_LaserRayScarletBursts(npc, target, ref frame, ref attackTimer);
					break;
				case TwinsAttackType.SpecialAttack_GatlingLaserAndPlasmaFlames:
					DoBehavior_GatlingLaserAndPlasmaFlames(npc, target, hoverSide, ref frame, ref attackTimer);
					break;
			}

			return false;
		}

		public static void DoBehavior_ReleasePredictiveLasers(NPC npc, Player target, float hoverSide, ref float frame, ref float attackTimer)
		{
			float shootRate = 60f;
			float laserShootSpeed = 10f;
			float predictivenessFactor = 18.5f;
			Vector2 aimDestination = target.Center + target.velocity * predictivenessFactor;
			Vector2 aimDirection = npc.SafeDirectionTo(aimDestination);

			if (ExoMechManagement.CurrentTwinsPhase >= 2)
				shootRate -= 12f;
			if (ExoMechManagement.CurrentTwinsPhase == 3)
				shootRate -= 8f;
			if (ExoMechManagement.CurrentTwinsPhase >= 5)
			{
				laserShootSpeed *= 0.8f;
				shootRate -= 20f;
			}

			ref float hoverOffsetX = ref npc.Infernum().ExtraAI[0];
			ref float hoverOffsetY = ref npc.Infernum().ExtraAI[1];
			ref float shootCounter = ref npc.Infernum().ExtraAI[2];

			Vector2 hoverDestination = target.Center + Vector2.UnitX * hoverSide * 780f;
			hoverDestination.X += hoverOffsetX;
			hoverDestination.Y += hoverOffsetY;

			// Determine rotation.
			npc.rotation = aimDirection.ToRotation() + MathHelper.PiOver2;

			// Move to the appropriate side of the target.
			AresBodyBehaviorOverride.DoHoverMovement(npc, hoverDestination, 30f, 84f);

			// Fire a plasma burst and select a new offset.
			if (attackTimer >= shootRate)
			{
				Main.PlaySound(InfernumMode.CalamityMod.GetLegacySoundSlot(SoundType.Item, "Sounds/Item/LaserCannon"), npc.Center);

				if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					int laser = Utilities.NewProjectileBetter(npc.Center + aimDirection * 70f, aimDirection * laserShootSpeed, ModContent.ProjectileType<ArtemisLaser>(), 550, 0f);
					if (Main.projectile.IndexInRange(laser))
					{
						Main.projectile[laser].ModProjectile<ArtemisLaser>().InitialDestination = aimDestination + aimDirection * 1000f;
						Main.projectile[laser].ai[1] = npc.whoAmI;
						Main.projectile[laser].netUpdate = true;
					}
				}

				hoverOffsetX = Main.rand.NextFloat(-50f, 50f);
				hoverOffsetY = Main.rand.NextFloat(-250f, 250f);
				attackTimer = 0f;
				shootCounter++;
				npc.netUpdate = true;
			}

			// Calculate frames.
			frame = (int)Math.Round(MathHelper.Lerp(20f, 29f, attackTimer / shootRate));
			if (ExoMechManagement.CurrentTwinsPhase >= 2)
				frame += 60f;
		}

		#endregion AI

		#region Frames and Drawcode
		public override void FindFrame(NPC npc, int frameHeight)
		{
			int frameX = (int)npc.localAI[0] / 9;
			int frameY = (int)npc.localAI[0] % 9;
			npc.frame = new Rectangle(npc.width * frameX, npc.height * frameY, npc.width, npc.height);
		}


		public static float FlameTrailWidthFunction(NPC npc, float completionRatio) => MathHelper.SmoothStep(21f, 8f, completionRatio) * npc.ModNPC<Artemis>().ChargeFlash;

		public static float FlameTrailWidthFunctionBig(NPC npc, float completionRatio) => MathHelper.SmoothStep(34f, 12f, completionRatio) * npc.ModNPC<Artemis>().ChargeFlash;

		public static float RibbonTrailWidthFunction(float completionRatio)
		{
			float baseWidth = Utils.InverseLerp(1f, 0.54f, completionRatio, true) * 5f;
			float endTipWidth = CalamityUtils.Convert01To010(Utils.InverseLerp(0.96f, 0.89f, completionRatio, true)) * 2.4f;
			return baseWidth + endTipWidth;
		}

		public static Color FlameTrailColorFunction(NPC npc, float completionRatio)
		{
			float trailOpacity = Utils.InverseLerp(0.8f, 0.27f, completionRatio, true) * Utils.InverseLerp(0f, 0.067f, completionRatio, true);
			Color startingColor = Color.Lerp(Color.White, Color.Cyan, 0.27f);
			Color middleColor = Color.Lerp(Color.Orange, Color.Yellow, 0.31f);
			Color endColor = Color.OrangeRed;
			return CalamityUtils.MulticolorLerp(completionRatio, startingColor, middleColor, endColor) * npc.ModNPC<Artemis>().ChargeFlash * trailOpacity;
		}

		public static Color FlameTrailColorFunctionBig(NPC npc, float completionRatio)
		{
			float trailOpacity = Utils.InverseLerp(0.8f, 0.27f, completionRatio, true) * Utils.InverseLerp(0f, 0.067f, completionRatio, true) * 0.56f;
			Color startingColor = Color.Lerp(Color.White, Color.Cyan, 0.25f);
			Color middleColor = Color.Lerp(Color.Blue, Color.White, 0.35f);
			Color endColor = Color.Lerp(Color.DarkBlue, Color.White, 0.47f);
			Color color = CalamityUtils.MulticolorLerp(completionRatio, startingColor, middleColor, endColor) * npc.ModNPC<Artemis>().ChargeFlash * trailOpacity;
			color.A = 0;
			return color;
		}

		public static Color RibbonTrailColorFunction(NPC npc, float completionRatio)
		{
			Color startingColor = new Color(34, 40, 48);
			Color endColor = new Color(219, 82, 28);
			return Color.Lerp(startingColor, endColor, (float)Math.Pow(completionRatio, 1.5D)) * npc.Opacity;
		}

		public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Color lightColor)
		{
			// Declare the trail drawers if they have yet to be defined.
			if (npc.ModNPC<Artemis>().ChargeFlameTrail is null)
				npc.ModNPC<Artemis>().ChargeFlameTrail = new PrimitiveTrail(c => FlameTrailWidthFunction(npc, c), c => FlameTrailColorFunction(npc, c), null, GameShaders.Misc["CalamityMod:ImpFlameTrail"]);

			if (npc.ModNPC<Artemis>().ChargeFlameTrailBig is null)
				npc.ModNPC<Artemis>().ChargeFlameTrailBig = new PrimitiveTrail(c => FlameTrailWidthFunctionBig(npc, c), c => FlameTrailColorFunctionBig(npc, c), null, GameShaders.Misc["CalamityMod:ImpFlameTrail"]);

			if (npc.ModNPC<Artemis>().RibbonTrail is null)
				npc.ModNPC<Artemis>().RibbonTrail = new PrimitiveTrail(RibbonTrailWidthFunction, c => RibbonTrailColorFunction(npc, c));

			// Prepare the flame trail shader with its map texture.
			GameShaders.Misc["CalamityMod:ImpFlameTrail"].SetShaderTexture(ModContent.GetTexture("CalamityMod/ExtraTextures/ScarletDevilStreak"));

			int numAfterimages = npc.ModNPC<Artemis>().ChargeFlash > 0f ? 0 : 5;
			Texture2D texture = Main.npcTexture[npc.type];
			Rectangle frame = npc.frame;
			Vector2 origin = npc.Size * 0.5f;
			Vector2 center = npc.Center - Main.screenPosition;
			Color afterimageBaseColor = Color.White;

			// Draws a single instance of a regular, non-glowmask based Artemis.
			// This is created to allow easy duplication of them when drawing the charge.
			void drawInstance(Vector2 drawOffset, Color baseColor)
			{
				if (CalamityConfig.Instance.Afterimages)
				{
					for (int i = 1; i < numAfterimages; i += 2)
					{
						Color afterimageColor = npc.GetAlpha(Color.Lerp(baseColor, afterimageBaseColor, 0.5f)) * ((numAfterimages - i) / 15f);
						Vector2 afterimageCenter = npc.oldPos[i] + frame.Size() * 0.5f - Main.screenPosition;
						spriteBatch.Draw(texture, afterimageCenter, npc.frame, afterimageColor, npc.oldRot[i], origin, npc.scale, SpriteEffects.None, 0f);
					}
				}

				spriteBatch.Draw(texture, center + drawOffset, frame, npc.GetAlpha(baseColor), npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
			}

			// Draw ribbons near the main thruster
			for (int direction = -1; direction <= 1; direction += 2)
			{
				Vector2 ribbonOffset = -Vector2.UnitY.RotatedBy(npc.rotation) * 14f;
				ribbonOffset += Vector2.UnitX.RotatedBy(npc.rotation) * direction * 26f;

				float currentSegmentRotation = npc.rotation;
				List<Vector2> ribbonDrawPositions = new List<Vector2>();
				for (int i = 0; i < 12; i++)
				{
					float ribbonCompletionRatio = i / 12f;
					float wrappedAngularOffset = MathHelper.WrapAngle(npc.oldRot[i + 1] - currentSegmentRotation) * 0.3f;
					float segmentRotationOffset = MathHelper.Clamp(wrappedAngularOffset, -0.12f, 0.12f);

					// Add a sinusoidal offset that goes based on time and completion ratio to create a waving-flag-like effect.
					// This is dampened for the first few points to prevent weird offsets. It is also dampened by high velocity.
					float sinusoidalRotationOffset = (float)Math.Sin(ribbonCompletionRatio * 2.22f + Main.GlobalTime * 3.4f) * 1.36f;
					float sinusoidalRotationOffsetFactor = Utils.InverseLerp(0f, 0.37f, ribbonCompletionRatio, true) * direction * 24f;
					sinusoidalRotationOffsetFactor *= Utils.InverseLerp(24f, 16f, npc.velocity.Length(), true);

					Vector2 sinusoidalOffset = Vector2.UnitY.RotatedBy(npc.rotation + sinusoidalRotationOffset) * sinusoidalRotationOffsetFactor;
					Vector2 ribbonSegmentOffset = Vector2.UnitY.RotatedBy(currentSegmentRotation) * ribbonCompletionRatio * 540f + sinusoidalOffset;
					ribbonDrawPositions.Add(npc.Center + ribbonSegmentOffset + ribbonOffset);

					currentSegmentRotation += segmentRotationOffset;
				}
				npc.ModNPC<Artemis>().RibbonTrail.Draw(ribbonDrawPositions, -Main.screenPosition, 66);
			}

			int instanceCount = (int)MathHelper.Lerp(1f, 15f, npc.ModNPC<Artemis>().ChargeFlash);
			Color baseInstanceColor = Color.Lerp(lightColor, Color.White, npc.ModNPC<Artemis>().ChargeFlash);
			baseInstanceColor.A = (byte)(int)(255f - npc.ModNPC<Artemis>().ChargeFlash * 255f);

			spriteBatch.EnterShaderRegion();

			drawInstance(Vector2.Zero, baseInstanceColor);
			if (instanceCount > 1)
			{
				baseInstanceColor *= 0.04f;
				float backAfterimageOffset = MathHelper.SmoothStep(0f, 2f, npc.ModNPC<Artemis>().ChargeFlash);
				for (int i = 0; i < instanceCount; i++)
				{
					Vector2 drawOffset = (MathHelper.TwoPi * i / instanceCount + Main.GlobalTime * 0.8f).ToRotationVector2() * backAfterimageOffset;
					drawInstance(drawOffset, baseInstanceColor);
				}
			}

			texture = ModContent.GetTexture("CalamityMod/NPCs/ExoMechs/Artemis/ArtemisGlow");
			if (CalamityConfig.Instance.Afterimages)
			{
				for (int i = 1; i < numAfterimages; i += 2)
				{
					Color afterimageColor = npc.GetAlpha(Color.Lerp(lightColor, afterimageBaseColor, 0.5f)) * ((numAfterimages - i) / 15f);
					Vector2 afterimageCenter = npc.oldPos[i] + frame.Size() * 0.5f - Main.screenPosition;
					spriteBatch.Draw(texture, afterimageCenter, npc.frame, afterimageColor, npc.oldRot[i], origin, npc.scale, SpriteEffects.None, 0f);
				}
			}

			spriteBatch.Draw(texture, center, frame, Color.White * npc.Opacity, npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

			spriteBatch.ExitShaderRegion();

			// Draw a flame trail on the thrusters if needed. This happens during charges.
			if (npc.ModNPC<Artemis>().ChargeFlash > 0f)
			{
				for (int direction = -1; direction <= 1; direction++)
				{
					Vector2 baseDrawOffset = new Vector2(0f, direction == 0f ? 18f : 60f).RotatedBy(npc.rotation);
					baseDrawOffset += new Vector2(direction * 64f, 0f).RotatedBy(npc.rotation);

					float backFlameLength = direction == 0f ? 700f : 190f;
					Vector2 drawStart = npc.Center + baseDrawOffset;
					Vector2 drawEnd = drawStart - (npc.rotation - MathHelper.PiOver2).ToRotationVector2() * npc.ModNPC<Artemis>().ChargeFlash * backFlameLength;
					Vector2[] drawPositions = new Vector2[]
					{
						drawStart,
						drawEnd
					};

					if (direction == 0)
					{
						for (int i = 0; i < 4; i++)
						{
							Vector2 drawOffset = (MathHelper.TwoPi * i / 4f).ToRotationVector2() * 8f;
							npc.ModNPC<Artemis>().ChargeFlameTrailBig.Draw(drawPositions, drawOffset - Main.screenPosition, 70);
						}
					}
					else
						npc.ModNPC<Artemis>().ChargeFlameTrail.Draw(drawPositions, -Main.screenPosition, 70);
				}
			}

			return false;
		}
		#endregion Frames and Drawcode
	}
}
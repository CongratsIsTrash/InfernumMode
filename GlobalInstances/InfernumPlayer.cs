using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.NPCs.Polterghast;
using CalamityMod.World;
using InfernumMode.Achievements;
using InfernumMode.Biomes;
using InfernumMode.Dusts;
using InfernumMode.Projectiles;
using InfernumMode.Sounds;
using InfernumMode.Subworlds;
using InfernumMode.Systems;
using InfernumMode.Tiles;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using SubworldLibrary;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace InfernumMode.GlobalInstances
{
    [LegacyName("PoDPlayer")]
    public class InfernumPlayer : ModPlayer
    {
        public int MadnessTime;
        public bool RedElectrified = false;
        public bool ShadowflameInferno = false;
        public bool DarkFlames = false;
        public bool Madness = false;
        public float CurrentScreenShakePower;
        public float MusicMuffleFactor;
        public float ShimmerSoundVolumeInterpolant;
        public SlotId ShimmerSoundID;

        public int ProvidenceRoomShatterTimer;

        public bool ProfanedTempleAnimationHasPlayed;

        public bool CreateALotOfHolyCinders;

        public bool HatGirl;

        public bool HatGirlShouldGiveAdvice;

        public float MadnessInterpolant => MathHelper.Clamp(MadnessTime / 600f, 0f, 1f);

        public bool InProfanedArena
        {
            get
            {
                Rectangle arena = WorldSaveSystem.ProvidenceArena;
                arena.X *= 16;
                arena.Y *= 16;
                arena.Width *= 16;
                arena.Height *= 16;

                return Player.Hitbox.Intersects(arena) && !WeakReferenceSupport.InAnySubworld();
            }
        }
        public bool InProfanedArenaAntiCheeseZone
        {
            get
            {
                Rectangle arena = WorldSaveSystem.ProvidenceArena;
                arena.X *= 16;
                arena.Y *= 16;
                arena.Width *= 16;
                arena.Height *= 16;
                arena.Inflate(1080, 1080);

                return Player.Hitbox.Intersects(arena) && !WeakReferenceSupport.InAnySubworld();
            }
        }

        public Vector2 ScreenFocusPosition;
        public float ScreenFocusInterpolant = 0f;
        public int ScreenFocusHoldInPlaceTime;

        internal Point? CornerOne = null;
        internal Point? CornerTwo = null;

        public bool ProfanedLavaFountain
        {
            get;
            set;
        }

        // Property with a getter that dynamically assembles the corners to produce a meaningful Rectangle.
        internal Rectangle? SelectedProvidenceArena
        {
            get
            {
                if (!CornerOne.HasValue || !CornerTwo.HasValue)
                    return null;

                Point c1 = CornerOne.GetValueOrDefault();
                Point c2 = CornerTwo.GetValueOrDefault();

                // It is possible the player dragged the corners in any direction, so use Abs and Min to find the true upper left corner.
                int startingX = Math.Min(c1.X, c2.X);
                int width = Math.Abs(c1.X - c2.X);
                int startingY = Math.Min(c1.Y, c2.Y);
                int height = Math.Abs(c1.Y - c2.Y);
                return new Rectangle(startingX, startingY, width, height);
            }
        }

        public bool ZoneProfaned => Player.InModBiome(ModContent.GetInstance<ProfanedTempleBiome>()) && !WeakReferenceSupport.InAnySubworld();

        #region Nurse Cheese Death
        public override bool ModifyNurseHeal(NPC nurse, ref int health, ref bool removeDebuffs, ref string chatText)
        {
            if (InfernumMode.CanUseCustomAIs && CalamityPlayer.areThereAnyDamnBosses)
            {
                chatText = "I cannot help you. Good luck.";
                return false;
            }
            return true;
        }
        #endregion Nurse Cheese Death
        #region Reset Effects
        public override void ResetEffects()
        {
            RedElectrified = false;
            ShadowflameInferno = false;
            DarkFlames = false;
            Madness = false;
            HatGirl = false;

            if (ScreenFocusHoldInPlaceTime > 0)
                ScreenFocusHoldInPlaceTime--;
            else
                ScreenFocusInterpolant = MathHelper.Clamp(ScreenFocusInterpolant - 0.2f, 0f, 1f);
            MusicMuffleFactor = 0f;

            // Disable block placement and destruction in the profaned arena.
            if (InProfanedArenaAntiCheeseZone)
            {
                Player.AddBuff(BuffID.NoBuilding, 10);
                Player.noBuilding = true;
            }
        }
        #endregion
        #region Update Dead
        public override void UpdateDead()
        {
            RedElectrified = false;
            ShadowflameInferno = false;
            DarkFlames = false;
            Madness = false;
            MadnessTime = 0;

            // THIS IS A TEST EFFECT. REMOVE IT LATER.
            LostColosseum.HasBereftVassalAppeared = false;

            if (WorldSaveSystem.InfernumMode)
                Player.respawnTimer = Utils.Clamp(Player.respawnTimer - 1, 0, 3600);
        }
        #endregion
        #region Update
        public override void PreUpdate()
        {
            ProfanedLavaFountain = false;
            int profanedFountainID = ModContent.TileType<ProfanedFountainTile>();
            for (int dx = -75; dx < 75; dx++)
            {
                for (int dy = -75; dy < 75; dy++)
                {
                    int x = (int)(Player.Center.X / 16f + dx);
                    int y = (int)(Player.Center.Y / 16f + dy);
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == profanedFountainID && Main.tile[x, y].TileFrameX < 36)
                    {
                        ProfanedLavaFountain = true;
                        goto LeaveLoop;
                    }
                }
            }
            LeaveLoop:

            if (Main.netMode == NetmodeID.Server)
                return;

            // Handle shimmer sound looping when near the Providence door.
            if (SoundEngine.TryGetActiveSound(ShimmerSoundID, out var sound))
            {
                float idealVolume = Main.soundVolume * ShimmerSoundVolumeInterpolant;
                if (sound.Sound.Volume != idealVolume)
                    sound.Sound.Volume = idealVolume;

                if (WorldSaveSystem.HasProvidenceDoorShattered || ShimmerSoundVolumeInterpolant <= 0f)
                    sound.Stop();
                if (ShimmerSoundVolumeInterpolant > 0f)
                    sound.Resume();
            }
            else
            {
                if (ShimmerSoundVolumeInterpolant > 0f && !WorldSaveSystem.HasProvidenceDoorShattered)
                    ShimmerSoundID = SoundEngine.PlaySound(InfernumSoundRegistry.ProvidenceDoorShimmerSoundLoop with { Volume = 0.0001f });
            }
            ShimmerSoundVolumeInterpolant = MathHelper.Clamp(ShimmerSoundVolumeInterpolant - 0.02f, 0f, 1f);
        }

        public override void PostUpdate()
        {
            // Keep the player out of the providence arena if the door is around.
            if (WorldSaveSystem.ProvidenceDoorXPosition != 0 && !WorldSaveSystem.HasProvidenceDoorShattered && Player.Bottom.Y >= (Main.maxTilesY - 220f) * 16f)
            {
                bool passedDoor = false;
                float doorX = WorldSaveSystem.ProvidenceDoorXPosition;
                while (Player.Right.X >= doorX || (passedDoor && Collision.SolidCollision(Player.TopLeft, Player.width, Player.height)))
                {
                    Player.velocity.X = 0f;
                    Player.position.X -= 0.1f;
                    passedDoor = true;
                }
            }

            if (CalamityPlayer.areThereAnyDamnBosses && Player.Calamity().momentumCapacitorBoost > 1.8f)
                Player.Calamity().momentumCapacitorBoost = 1.8f;

            if (Main.myPlayer != Player.whoAmI || !ZoneProfaned || !Player.ZoneUnderworldHeight)
                return;

            bool createALotOfHolyCinders = CreateALotOfHolyCinders;
            float cinderSpawnInterpolant = CalamityPlayer.areThereAnyDamnBosses ? 0.9f : 0.1f;
            int cinderSpawnRate = (int)MathHelper.Lerp(6f, 2f, cinderSpawnInterpolant);
            float cinderFlySpeed = MathHelper.Lerp(6f, 12f, cinderSpawnInterpolant);
            if (createALotOfHolyCinders)
            {
                cinderSpawnRate = 1;
                cinderFlySpeed = 13.25f;
                CreateALotOfHolyCinders = false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (!Main.rand.NextBool(cinderSpawnRate) || Main.gfxQuality < 0.35f)
                    continue;

                Vector2 cinderSpawnOffset = new(Main.rand.NextFloatDirection() * 1550f, 650f);
                Vector2 cinderVelocity = -Vector2.UnitY.RotatedBy(Main.rand.NextFloat(0.23f, 0.98f)) * Main.rand.NextFloat(0.6f, 1.2f) * cinderFlySpeed;
                if (Main.rand.NextBool())
                {
                    cinderSpawnOffset = cinderSpawnOffset.RotatedBy(-MathHelper.PiOver2) * new Vector2(0.9f, 1f);
                    cinderVelocity = cinderVelocity.RotatedBy(-MathHelper.PiOver2) * new Vector2(1.8f, -1f);
                }

                if (Main.rand.NextBool(createALotOfHolyCinders ? 2 : 6))
                    cinderVelocity.X *= -1f;

                Utilities.NewProjectileBetter(Player.Center + cinderSpawnOffset, cinderVelocity, ModContent.ProjectileType<ProfanedTempleCinder>(), 0, 0f);
            }
        }
        #endregion Update
        #region Pre Kill
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore, ref PlayerDeathReason damageSource)
        {
            if (damage == 10.0 && hitDirection == 0 && damageSource.SourceOtherIndex == 8)
            {
                if (RedElectrified)
                    damageSource = PlayerDeathReason.ByCustomReason($"{Player.name} could not withstand the red lightning.");
                if (DarkFlames)
                    damageSource = PlayerDeathReason.ByCustomReason($"{Player.name} was incinerated by ungodly fire.");
                if (Madness)
                    damageSource = PlayerDeathReason.ByCustomReason($"{Player.name} went mad.");
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genGore, ref damageSource);
        }
        #endregion
        #region Kill
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            HatGirlTipsManager.PotentialTipToUse = HatGirlTipsManager.SelectTip();
            if (CalamityPlayer.areThereAnyDamnBosses)
                AchievementPlayer.ExtraUpdateAchievements(Player, new UpdateContext(-1, -1, SpecificUpdateContexts.PlayerDeath));
        }
        #endregion Kill
        #region Life Regen
        public override void UpdateLifeRegen()
        {
            void causeLifeRegenLoss(int regenLoss)
            {
                if (Player.lifeRegen > 0)
                    Player.lifeRegen = 0;
                Player.lifeRegenTime = 0;
                Player.lifeRegen -= regenLoss;
            }
            if (RedElectrified)
                causeLifeRegenLoss(Player.controlLeft || Player.controlRight ? 64 : 16);

            if (ShadowflameInferno)
                causeLifeRegenLoss(23);
            if (DarkFlames)
            {
                causeLifeRegenLoss(30);
                Player.statDefense -= 8;
            }
            if (Madness)
                causeLifeRegenLoss(NPC.AnyNPCs(ModContent.NPCType<Polterghast>()) ? 800 : 50);
            MadnessTime = Utils.Clamp(MadnessTime + (Madness ? 1 : -8), 0, 660);
        }
        #endregion
        #region Screen Shaking
        public override void ModifyScreenPosition()
        {
            if (ScreenFocusInterpolant > 0f && InfernumConfig.Instance.BossIntroductionAnimationsAreAllowed)
            {
                Vector2 idealScreenPosition = ScreenFocusPosition - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, idealScreenPosition, ScreenFocusInterpolant);
            }

            if (CurrentScreenShakePower > 0f)
                CurrentScreenShakePower = Utils.Clamp(CurrentScreenShakePower - 0.2f, 0f, 15f);
            else
                return;

            if (!CalamityConfig.Instance.Screenshake)
                return;

            Main.screenPosition += Main.rand.NextVector2CircularEdge(CurrentScreenShakePower, CurrentScreenShakePower);
        }
        #endregion
        #region Saving and Loading
        public override void SaveData(TagCompound tag)/* tModPorter Suggestion: Edit tag parameter instead of returning new TagCompound */
        {
            tag["ProfanedTempleAnimationHasPlayed"] = ProfanedTempleAnimationHasPlayed;
        }

        public override void LoadData(TagCompound tag)
        {
            ProfanedTempleAnimationHasPlayed = tag.GetBool("ProfanedTempleAnimationHasPlayed");            
        }
        #endregion Saving and Loading
        #region Misc Effects
        public override void PostUpdateMiscEffects()
        {
            if (Player.mount.Active && Player.mount.Type == MountID.Slime && NPC.AnyNPCs(InfernumMode.CalamityMod.Find<ModNPC>("DesertScourgeHead").Type) && InfernumMode.CanUseCustomAIs)
            {
                Player.mount.Dismount(Player);
            }

            // Ensure that Death+Revengeance Mode is always active while Infernum is active.
            if (WorldSaveSystem.InfernumMode && !CalamityWorld.revenge)
                CalamityWorld.revenge = true;
            if (WorldSaveSystem.InfernumMode && !CalamityWorld.death)
                CalamityWorld.death = true;

            // I said FUCK OFF.
            bool stupidDifficultyIsActive = Main.masterMode || Main.getGoodWorld || InfernumMode.EmodeIsActive;
            if (WorldSaveSystem.InfernumMode && stupidDifficultyIsActive)
            {
                Utilities.DisplayText("Infernum is not allowed in Master Mode, For the Worthy, or Eternity Mode.", Color.Red);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetcodeHandler.SyncInfernumActivity(Main.myPlayer);
                WorldSaveSystem.InfernumMode = false;
            }

            if (!stupidDifficultyIsActive && SubworldSystem.IsActive<LostColosseum>())
                WorldSaveSystem.InfernumMode = true;

            if (ShadowflameInferno)
            {
                for (int i = 0; i < 2; i++)
                {
                    Dust shadowflame = Dust.NewDustDirect(Player.position, Player.width, Player.height, 28);
                    shadowflame.velocity = Player.velocity.SafeNormalize(Vector2.UnitX * Player.direction);
                    shadowflame.velocity = shadowflame.velocity.RotatedByRandom(0.4f) * -Main.rand.NextFloat(2.5f, 5.4f);
                    shadowflame.scale = Main.rand.NextFloat(0.95f, 1.3f);
                    shadowflame.noGravity = true;
                }
            }

            if (DarkFlames)
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust shadowflame = Dust.NewDustDirect(Player.position, Player.width, Player.height, ModContent.DustType<RavagerMagicDust>());
                    shadowflame.velocity = Player.velocity.SafeNormalize(Vector2.UnitX * Player.direction);
                    shadowflame.velocity = shadowflame.velocity.RotatedByRandom(0.4f) * -Main.rand.NextFloat(2.5f, 5.4f);
                    shadowflame.velocity += Main.rand.NextVector2Circular(3f, 3f);
                    shadowflame.scale = Main.rand.NextFloat(0.95f, 1.25f);
                    shadowflame.noGravity = true;
                }
            }
        }
        #endregion
    }
}
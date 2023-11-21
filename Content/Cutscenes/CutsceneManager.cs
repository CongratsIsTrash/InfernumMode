﻿using System.Collections.Generic;
using InfernumMode.Core.GlobalInstances.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace InfernumMode.Content.Cutscenes
{
    public class CutsceneManager : ModSystem
    {
        internal static Queue<Cutscene> CutscenesQueue = new();

        internal static Cutscene ActiveCutscene
        {
            get;
            private set;
        }

        public static void QueueCutscene(Cutscene cutscene)
        {
            if (Main.netMode != NetmodeID.Server)
                CutscenesQueue.Enqueue(cutscene);
        }

        public static bool IsCutsceneActive(Cutscene cutscene)
        {
            if (ActiveCutscene == null)
                return false;

            return ActiveCutscene.Name == cutscene.Name;
        }

        public override void Load()
        {
            Main.OnPostDraw += PostDraw;
        }

        public override void PostUpdateEverything()
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            if (ActiveCutscene == null)
            {
                if (CutscenesQueue.TryDequeue(out Cutscene cutscene))
                {
                    ActiveCutscene = cutscene;
                    ActiveCutscene.Timer = 0;
                    ActiveCutscene.IsActive = true;
                    if (ActiveCutscene.GetBlockCondition.HasValue)
                        BlockerSystem.Start(ActiveCutscene.GetBlockCondition.Value);
                }
            }
            
            if (ActiveCutscene != null)
            {
                if (ActiveCutscene.Timer >= ActiveCutscene.CutsceneLength)
                {
                    ActiveCutscene.Timer = 0;
                    ActiveCutscene.IsActive = false;
                    ActiveCutscene = null;
                }
                else
                {
                    ActiveCutscene.Update();
                    ActiveCutscene.Timer++;
                }
            }
        }

        public override void ModifyScreenPosition() => ActiveCutscene?.ModifyScreenPosition();

        public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform) => ActiveCutscene?.ModifyTransformMatrix(ref Transform);

        internal static void DrawToWorld() => ActiveCutscene?.DrawToWorld(Main.spriteBatch);

        internal static RenderTarget2D DrawWorld(RenderTarget2D screen)
        {
            if (ActiveCutscene == null)
                return screen;

            return ActiveCutscene?.DrawWorld(Main.spriteBatch, screen);
        }

        private void PostDraw(GameTime obj)
        {
            ActiveCutscene?.PostDraw(Main.spriteBatch);
        }
    }
}

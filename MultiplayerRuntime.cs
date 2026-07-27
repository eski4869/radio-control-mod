using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.GameManager.MultiEnding;
using JumpKing.Level;
using JumpKing.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RadioControlMod
{
    internal static class MultiplayerRuntime
    {
        private const float Player2LaneOffset = 240f;
        private static PlayerEntity _player2;
        private static bool _levelStarted;
        private static bool _raceComplete;

        public static bool IsActive
        {
            get
            {
                return ModEntry.IsMultiplayerEnabled && _levelStarted && !_raceComplete &&
                    _player2 != null && _player2.IsAlive;
            }
        }

        public static PlayerEntity Player2
        {
            get { return IsActive ? _player2 : null; }
        }

        public static void OnLevelStart()
        {
            _levelStarted = true;
            _raceComplete = false;

            if (ModEntry.IsMultiplayerEnabled)
            {
                StartPlayer2();
            }
        }

        public static void OnLevelEnd()
        {
            _levelStarted = false;
            StopPlayer2();
        }

        public static void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                StopPlayer2();
                return;
            }

            _raceComplete = false;
            if (_levelStarted)
            {
                StartPlayer2();
            }
        }

        public static bool IsPlayer2(PlayerEntity player)
        {
            return player != null && ReferenceEquals(player, _player2);
        }

        public static bool IsPlayer2(InputComponent input)
        {
            return input != null && input.gameObject != null &&
                ReferenceEquals(input.gameObject, _player2);
        }

        public static void FinishRace()
        {
            _raceComplete = true;
            StopPlayer2();
        }

        private static void StartPlayer2()
        {
            if (_player2 != null && _player2.IsAlive)
            {
                return;
            }

            if (EntityManager.instance == null)
            {
                return;
            }

            PlayerEntity player1 = EntityManager.instance.Find<PlayerEntity>();
            if (player1 == null)
            {
                return;
            }

            BodyComp player1Body = player1.GetComponent<BodyComp>();
            if (player1Body == null)
            {
                return;
            }

            try
            {
                PlayerEntity player2 = new PlayerEntity();
                _player2 = player2;

                BodyComp player2Body = player2.GetComponent<BodyComp>();
                if (player2Body != null)
                {
                    player2Body.Position = new Vector2(
                        player1Body.Position.X + Player2LaneOffset,
                        player1Body.Position.Y
                    );
                    player2Body.Velocity = Vector2.Zero;
                }

                Component[] components = player2.GetComponents();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i].GetType().FullName == "JumpKing.Player.CameraFollowComp")
                    {
                        components[i].Enabled = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _player2 = null;
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl multiplayer start failed: " + ex.Message
                );
            }
        }

        private static void StopPlayer2()
        {
            RadioVirtualInput.Clear(PlayerTargets.Player2);
            MultiplayerSplitRenderer.Release();

            if (_player2 != null && _player2.IsAlive)
            {
                _player2.Destroy();
            }

            _player2 = null;
        }
    }

    internal static class Player2InputStatePatch
    {
        public static bool Prefix(
            InputComponent __instance,
            MethodBase __originalMethod,
            ref InputComponent.State __result
        )
        {
            if (!MultiplayerRuntime.IsPlayer2(__instance))
            {
                return true;
            }

            bool pressed = __originalMethod.Name == "GetPressedState";
            __result = RadioVirtualInput.GetPlayer2State(pressed);
            return false;
        }
    }

    internal static class Player2SaveUpdatePatch
    {
        public static bool Prefix(PlayerEntity __instance)
        {
            return !MultiplayerRuntime.IsPlayer2(__instance);
        }
    }

    internal static class MultiplayerEndingPatch
    {
        public static void Postfix(
            ref bool __result,
            ref IEnding __0,
            List<IEnding> ___m_endings
        )
        {
            if (!MultiplayerRuntime.IsActive)
            {
                return;
            }

            if (__result)
            {
                MultiplayerRuntime.FinishRace();
                return;
            }

            PlayerEntity player2 = MultiplayerRuntime.Player2;
            if (player2 == null || ___m_endings == null)
            {
                return;
            }

            for (int i = 0; i < ___m_endings.Count; i++)
            {
                IEnding ending = ___m_endings[i];
                if (!ending.CheckWin(player2))
                {
                    continue;
                }

                PlayerEntity player1 = EntityManager.instance.Find<PlayerEntity>();
                BodyComp player1Body = player1 == null ? null : player1.GetComponent<BodyComp>();
                BodyComp player2Body = player2.GetComponent<BodyComp>();

                if (player1Body == null || player2Body == null)
                {
                    return;
                }

                player1Body.Position = player2Body.Position;
                player1Body.Velocity = player2Body.Velocity;

                if (ending.CheckWin(player1))
                {
                    __0 = ending;
                    __result = true;
                    MultiplayerRuntime.FinishRace();
                }

                return;
            }
        }
    }

    internal static class MultiplayerDrawPatch
    {
        public static bool Prefix(JumpGame __instance)
        {
            return MultiplayerSplitRenderer.PrefixDraw(__instance);
        }
    }

    internal static class MultiplayerSplitRenderer
    {
        private const int Width = 480;
        private const int Height = 360;
        private const int HalfWidth = Width / 2;
        private static readonly FieldInfo CameraScreenField = AccessTools.Field(
            typeof(Camera),
            "_current_screen"
        );

        private static RenderTarget2D _player1Target;
        private static RenderTarget2D _player2Target;
        private static bool _drawingPass;

        public static bool PrefixDraw(JumpGame game)
        {
            if (_drawingPass || !MultiplayerRuntime.IsActive)
            {
                return true;
            }

            Game1 host = Game1.instance;
            PlayerEntity player1 = EntityManager.instance.Find<PlayerEntity>();
            PlayerEntity player2 = MultiplayerRuntime.Player2;
            if (host == null || player1 == null || player2 == null || CameraScreenField == null)
            {
                return true;
            }

            GraphicsDevice graphics = host.GraphicsDevice;
            EnsureTargets(graphics);

            RenderTargetBinding[] previousTargets = graphics.GetRenderTargets();
            int previousScreen = Camera.CurrentScreen;
            Vector2 previousOffset = Camera.Offset;

            host.EndBatch();

            try
            {
                DrawView(game, host, graphics, _player1Target, GetScreen(player1));
                DrawView(game, host, graphics, _player2Target, GetScreen(player2));
            }
            finally
            {
                _drawingPass = false;
                RestoreTargets(graphics, previousTargets);
                CameraScreenField.SetValue(null, previousScreen);
                Camera.Offset = previousOffset;
                host.StartBatch();
            }

            Game1.spriteBatch.Draw(
                _player1Target,
                new Rectangle(0, 0, HalfWidth, Height),
                new Rectangle(0, 0, HalfWidth, Height),
                Color.White
            );
            Game1.spriteBatch.Draw(
                _player2Target,
                new Rectangle(HalfWidth, 0, HalfWidth, Height),
                new Rectangle(HalfWidth, 0, HalfWidth, Height),
                Color.White
            );

            return false;
        }

        public static void Release()
        {
            DisposeTarget(ref _player1Target);
            DisposeTarget(ref _player2Target);
        }

        private static void DrawView(
            JumpGame game,
            Game1 host,
            GraphicsDevice graphics,
            RenderTarget2D target,
            int screen
        )
        {
            graphics.SetRenderTarget(target);
            graphics.Clear(Color.Black);
            CameraScreenField.SetValue(null, screen);
            host.StartBatch();
            _drawingPass = true;

            try
            {
                game.Draw();
            }
            finally
            {
                _drawingPass = false;
                host.EndBatch();
            }
        }

        private static int GetScreen(PlayerEntity player)
        {
            BodyComp body = player.GetComponent<BodyComp>();
            if (body == null)
            {
                return Camera.CurrentScreen;
            }

            int screen = (int)Math.Floor(body.GetHitbox().Center.Y / (double)Height);
            int maxScreen = LevelManager.Instance == null ? screen :
                Math.Max(0, LevelManager.TotalScreens - 1);
            return Math.Max(0, Math.Min(screen, maxScreen));
        }

        private static void EnsureTargets(GraphicsDevice graphics)
        {
            if (_player1Target == null || _player1Target.IsDisposed)
            {
                _player1Target = new RenderTarget2D(graphics, Width, Height);
            }

            if (_player2Target == null || _player2Target.IsDisposed)
            {
                _player2Target = new RenderTarget2D(graphics, Width, Height);
            }
        }

        private static void RestoreTargets(
            GraphicsDevice graphics,
            RenderTargetBinding[] previousTargets
        )
        {
            if (previousTargets == null || previousTargets.Length == 0)
            {
                graphics.SetRenderTarget(null);
            }
            else
            {
                graphics.SetRenderTargets(previousTargets);
            }
        }

        private static void DisposeTarget(ref RenderTarget2D target)
        {
            if (target != null)
            {
                target.Dispose();
                target = null;
            }
        }
    }
}

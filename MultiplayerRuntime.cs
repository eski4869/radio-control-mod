using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.API;
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
        private static bool _blockBehavioursSynchronized;
        private static readonly FieldInfo BlockBehaviourLookupField = AccessTools.Field(
            typeof(BodyComp),
            "m_blockBehaviourLookup"
        );

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

        public static void SynchronizeBlockBehaviours()
        {
            if (_blockBehavioursSynchronized || !IsActive ||
                BlockBehaviourLookupField == null || EntityManager.instance == null)
            {
                return;
            }

            PlayerEntity player1 = EntityManager.instance.Find<PlayerEntity>();
            PlayerEntity player2 = Player2;
            BodyComp player1Body = player1 == null ? null : player1.GetComponent<BodyComp>();
            BodyComp player2Body = player2 == null ? null : player2.GetComponent<BodyComp>();
            if (player1Body == null || player2Body == null)
            {
                return;
            }

            IDictionary sourceLookup = BlockBehaviourLookupField.GetValue(player1Body) as IDictionary;
            IDictionary targetLookup = BlockBehaviourLookupField.GetValue(player2Body) as IDictionary;
            if (sourceLookup == null || targetLookup == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in sourceLookup)
            {
                Type blockType = entry.Key as Type;
                IBlockBehaviour sourceBehaviour = entry.Value as IBlockBehaviour;
                if (blockType == null || sourceBehaviour == null ||
                    targetLookup.Contains(blockType))
                {
                    continue;
                }

                IBlockBehaviour player2Behaviour = CreateBlockBehaviour(
                    sourceBehaviour,
                    player2,
                    player2Body
                );
                if (player2Behaviour == null)
                {
                    JumpKing.Program.crashLog.AddErrorMessage(
                        "RadioControl multiplayer cannot construct block behaviour: " +
                        sourceBehaviour.GetType().FullName
                    );
                    continue;
                }

                player2Body.RegisterBlockBehaviour(blockType, player2Behaviour);
            }

            _blockBehavioursSynchronized = true;
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
                _blockBehavioursSynchronized = false;

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
            _blockBehavioursSynchronized = false;
        }

        private static IBlockBehaviour CreateBlockBehaviour(
            IBlockBehaviour sourceBehaviour,
            PlayerEntity player,
            BodyComp body
        )
        {
            Type behaviourType = sourceBehaviour.GetType();
            ConstructorInfo[] constructors = behaviourType.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            for (int i = 0; i < constructors.Length; i++)
            {
                ParameterInfo[] parameters = constructors[i].GetParameters();
                object[] arguments = new object[parameters.Length];
                bool supported = true;

                for (int j = 0; j < parameters.Length; j++)
                {
                    object argument = ResolveBehaviourArgument(
                        parameters[j],
                        sourceBehaviour,
                        player,
                        body
                    );
                    if (argument == UnsupportedArgument.Value)
                    {
                        supported = false;
                        break;
                    }

                    arguments[j] = argument;
                }

                if (!supported)
                {
                    continue;
                }

                try
                {
                    return constructors[i].Invoke(arguments) as IBlockBehaviour;
                }
                catch
                {
                }
            }

            return null;
        }

        private static object ResolveBehaviourArgument(
            ParameterInfo parameter,
            IBlockBehaviour sourceBehaviour,
            PlayerEntity player,
            BodyComp body
        )
        {
            Type parameterType = parameter.ParameterType;

            if (parameterType.IsInstanceOfType(player))
            {
                return player;
            }

            if (parameterType.IsInstanceOfType(body))
            {
                return body;
            }

            InputComponent input = player.GetComponent<InputComponent>();
            if (input != null && parameterType.IsInstanceOfType(input))
            {
                return input;
            }

            if (LevelManager.Instance != null &&
                parameterType.IsInstanceOfType(LevelManager.Instance))
            {
                return LevelManager.Instance;
            }

            object configuredValue;
            if (TryReadConfiguredValue(sourceBehaviour, parameter, out configuredValue))
            {
                return configuredValue;
            }

            return UnsupportedArgument.Value;
        }

        private static bool TryReadConfiguredValue(
            object source,
            ParameterInfo parameter,
            out object value
        )
        {
            value = null;
            Type sourceType = source.GetType();
            Type parameterType = parameter.ParameterType;
            string parameterName = NormalizeMemberName(parameter.Name);
            PropertyInfo[] properties = sourceType.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 ||
                    !parameterType.IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                if (NormalizeMemberName(property.Name) == parameterName)
                {
                    value = property.GetValue(source, null);
                    return true;
                }
            }

            PropertyInfo singleProperty = null;
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.CanRead && property.GetIndexParameters().Length == 0 &&
                    parameterType.IsAssignableFrom(property.PropertyType))
                {
                    if (singleProperty != null)
                    {
                        singleProperty = null;
                        break;
                    }

                    singleProperty = property;
                }
            }

            if (singleProperty != null)
            {
                value = singleProperty.GetValue(source, null);
                return true;
            }

            FieldInfo[] fields = sourceType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (parameterType.IsAssignableFrom(field.FieldType) &&
                    NormalizeMemberName(field.Name) == parameterName)
                {
                    value = field.GetValue(source);
                    return true;
                }
            }

            FieldInfo singleField = null;
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!parameterType.IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                if (singleField != null)
                {
                    return false;
                }

                singleField = field;
            }

            if (singleField == null)
            {
                return false;
            }

            value = singleField.GetValue(source);
            return true;
        }

        private static string NormalizeMemberName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var characters = new List<char>(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsLetterOrDigit(name[i]))
                {
                    characters.Add(char.ToLowerInvariant(name[i]));
                }
            }

            const string backingFieldSuffix = "kbackingfield";
            string normalized = new string(characters.ToArray());
            if (normalized.EndsWith(backingFieldSuffix, StringComparison.Ordinal))
            {
                normalized = normalized.Substring(
                    0,
                    normalized.Length - backingFieldSuffix.Length
                );
            }

            return normalized;
        }

        private sealed class UnsupportedArgument
        {
            public static readonly UnsupportedArgument Value = new UnsupportedArgument();

            private UnsupportedArgument()
            {
            }
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

    internal static class Player2ScreenUpdatePatch
    {
        private const int NotPlayer2 = -1;
        private static readonly FieldInfo CameraScreenField = AccessTools.Field(
            typeof(Camera),
            "_current_screen"
        );

        public static void Prefix(Entity __instance, out int __state)
        {
            PlayerEntity player = __instance as PlayerEntity;
            if (!MultiplayerRuntime.IsPlayer2(player) || CameraScreenField == null)
            {
                __state = NotPlayer2;
                return;
            }

            BodyComp body = player.GetComponent<BodyComp>();
            if (body == null)
            {
                __state = NotPlayer2;
                return;
            }

            __state = Camera.CurrentScreen;
            int screen = -(int)Math.Floor(body.GetHitbox().Center.Y / 360f);
            screen = Math.Max(0, Math.Min(LevelManager.TotalScreens - 1, screen));
            CameraScreenField.SetValue(null, screen);
        }

        public static void Postfix(int __state)
        {
            if (__state != NotPlayer2)
            {
                CameraScreenField.SetValue(null, __state);
            }
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
        private static readonly MethodInfo BodyIsOnBlockMethod = AccessTools.Method(
            typeof(BodyComp),
            "IsOnBlock",
            new Type[] { typeof(Type) }
        );

        private static RenderTarget2D _player1Target;
        private static RenderTarget2D _player2Target;
        private static bool _drawingPass;
        private static bool _player2CameraInitialized;
        private static int _player2CameraScreen;

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
                DrawView(game, host, graphics, _player1Target, previousScreen);
                DrawView(game, host, graphics, _player2Target, GetPlayerScreen(player2));
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
            _player2CameraInitialized = false;
            _player2CameraScreen = 0;
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

        private static int GetPlayerScreen(PlayerEntity player)
        {
            BodyComp body = player.GetComponent<BodyComp>();
            if (body == null)
            {
                return Camera.CurrentScreen;
            }

            int player1Screen = Camera.CurrentScreen;
            if (!_player2CameraInitialized)
            {
                _player2CameraScreen = player1Screen;
                _player2CameraInitialized = true;
            }

            CameraScreenField.SetValue(null, _player2CameraScreen);

            try
            {
                bool isOnSand = BodyIsOnBlockMethod != null &&
                    (bool)BodyIsOnBlockMethod.Invoke(
                        body,
                        new object[] { typeof(SandBlock) }
                    );

                if (!isOnSand)
                {
                    Camera.UpdateCameraWithVelocity(
                        body.GetHitbox().Center,
                        body.Velocity
                    );
                }

                _player2CameraScreen = Camera.CurrentScreen;
                return _player2CameraScreen;
            }
            finally
            {
                CameraScreenField.SetValue(null, player1Screen);
            }
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

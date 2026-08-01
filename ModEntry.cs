using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.GameManager.MultiEnding;
using JumpKing.Level;
using JumpKing.Mods;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Player;
using JumpKing.Util;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace RadioControlMod
{
    [JumpKingMod("eski4869.RadioControlMod")]
    public static class ModEntry
    {
        internal const string CommandTarget = "radio_control";
        internal const string MenuCommandTarget = "menu_control";
        private const string SettingsFileName = "eski4869.RadioControlMod.Settings.xml";

        private static Harmony _harmony;
        private static RadioControlPreferences _preferences;
        private static string _settingsPath;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            EnsurePreferencesLoaded();
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
            BrokerCommandClient.Register(MenuCommandTarget);
            EskiUiClient.Resolve();
            PlayerResolver.ResolveProvider();
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            EnsurePreferencesLoaded();
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
            BrokerCommandClient.Register(MenuCommandTarget);
            EskiUiClient.Resolve();
            PlayerResolver.ResolveProvider();
            RadioControlOverlay.EnsureAdded();
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            RadioControlRuntime.Stop();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            RadioControlRuntime.Stop();
        }

        internal static double JumpFrameLaplaceAlpha
        {
            get
            {
                EnsurePreferencesLoaded();
                return _preferences.JumpFrameLaplaceAlpha;
            }
        }

        internal static bool IsEnabled
        {
            get
            {
                EnsurePreferencesLoaded();
                return _preferences.IsEnabled;
            }
        }

        internal static bool IsDebugEnabled
        {
            get
            {
                EnsurePreferencesLoaded();
                return _preferences.IsDebugEnabled;
            }
        }

        internal static void SetEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.IsEnabled == isEnabled)
            {
                return;
            }

            _preferences.IsEnabled = isEnabled;

            if (!isEnabled)
            {
                RadioControlRuntime.Stop();
            }

            SavePreferences();
        }

        internal static void SetDebugEnabled(bool isDebugEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.IsDebugEnabled == isDebugEnabled)
            {
                return;
            }

            _preferences.IsDebugEnabled = isDebugEnabled;
            SavePreferences();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static RadioControlToggle RadioControlMenu(object factory, JumpKing.PauseMenu.GuiFormat format)
        {
            return new RadioControlToggle();
        }

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static RadioDebugToggle RadioDebugMenu(object factory, JumpKing.PauseMenu.GuiFormat format)
        {
            return new RadioDebugToggle();
        }

        private static void EnsurePatched()
        {
            if (_harmony != null)
            {
                return;
            }

            try
            {
                MethodInfo getPadState = AccessTools.Method(
                    typeof(ControllerManager),
                    "GetPadState"
                );
                MethodInfo getPressedPadState = AccessTools.Method(
                    typeof(ControllerManager),
                    "GetPressedPadState"
                );
                MethodInfo getInputState = AccessTools.Method(
                    typeof(InputComponent),
                    "GetState"
                );
                MethodInfo getPressedInputState = AccessTools.Method(
                    typeof(InputComponent),
                    "GetPressedState"
                );
                MethodInfo gameUpdate = AccessTools.Method(
                    typeof(Game1),
                    "Update"
                );
                MethodInfo gameUpdatePrefix = AccessTools.Method(
                    typeof(GameUpdatePatch),
                    "Prefix"
                );
                MethodInfo padStatePostfix = AccessTools.Method(
                    typeof(ControllerManagerPadStatePatch),
                    "Postfix"
                );
                MethodInfo pressedPadStatePostfix = AccessTools.Method(
                    typeof(ControllerManagerPressedPadStatePatch),
                    "Postfix"
                );
                MethodInfo inputStatePostfix = AccessTools.Method(
                    typeof(InputComponentStatePatch),
                    "Postfix"
                );
                MethodInfo pressedInputStatePostfix = AccessTools.Method(
                    typeof(InputComponentPressedStatePatch),
                    "Postfix"
                );
                if (getPadState == null ||
                    getPressedPadState == null ||
                    getInputState == null ||
                    getPressedInputState == null ||
                    gameUpdate == null ||
                    gameUpdatePrefix == null ||
                    padStatePostfix == null ||
                    pressedPadStatePostfix == null ||
                    inputStatePostfix == null ||
                    pressedInputStatePostfix == null)
                {
                    JumpKing.Program.crashLog.AddErrorMessage(
                        "RadioControl patch target not found."
                    );
                    return;
                }

                _harmony = new Harmony("eski4869.RadioControlMod");
                _harmony.Patch(gameUpdate, prefix: new HarmonyMethod(gameUpdatePrefix));
                _harmony.Patch(getPadState, postfix: new HarmonyMethod(padStatePostfix));
                _harmony.Patch(getPressedPadState, postfix: new HarmonyMethod(pressedPadStatePostfix));
                _harmony.Patch(getInputState, postfix: new HarmonyMethod(inputStatePostfix));
                _harmony.Patch(
                    getPressedInputState,
                    postfix: new HarmonyMethod(pressedInputStatePostfix)
                );
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl patch failed: " + ex.Message
                );
            }
        }

        private static void EnsurePreferencesLoaded()
        {
            if (_preferences != null)
            {
                return;
            }

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _settingsPath = Path.Combine(assemblyDir, SettingsFileName);
            bool shouldSavePreferences = false;

            try
            {
                if (File.Exists(_settingsPath))
                {
                    string settingsText = File.ReadAllText(_settingsPath);
                    shouldSavePreferences =
                        !settingsText.Contains("JumpFrameLaplaceAlpha") ||
                        !settingsText.Contains("IsEnabled") ||
                        !settingsText.Contains("IsDebugEnabled");

                    var serializer = new XmlSerializer(typeof(RadioControlPreferences));

                    using (var stream = File.OpenRead(_settingsPath))
                    {
                        _preferences = (RadioControlPreferences)serializer.Deserialize(stream);
                    }
                }
            }
            catch
            {
            }

            if (_preferences == null)
            {
                _preferences = new RadioControlPreferences();
                shouldSavePreferences = true;
            }

            if (shouldSavePreferences)
            {
                SavePreferences();
            }
        }

        private static void SavePreferences()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(RadioControlPreferences));

                using (var stream = File.Create(_settingsPath))
                {
                    serializer.Serialize(stream, _preferences);
                }
            }
            catch
            {
            }
        }
    }

    public class RadioControlPreferences
    {
        public bool IsEnabled { get; set; } = true;
        public bool IsDebugEnabled { get; set; } = false;
        public double JumpFrameLaplaceAlpha { get; set; } = 0.1;
    }

    public class RadioControlToggle : ITextToggle
    {
        public RadioControlToggle() : base(ModEntry.IsEnabled)
        {
        }

        protected override string GetName()
        {
            return "Radio Control";
        }

        protected override void OnToggle()
        {
            ModEntry.SetEnabled(toggle);
        }
    }

    public class RadioDebugToggle : ITextToggle
    {
        public RadioDebugToggle() : base(ModEntry.IsDebugEnabled)
        {
        }

        protected override string GetName()
        {
            return "Radio Debug";
        }

        protected override void OnToggle()
        {
            ModEntry.SetDebugEnabled(toggle);
        }
    }

    internal static class ControllerManagerPadStatePatch
    {
        public static void Postfix(ref PadState __result)
        {
            RadioVirtualInput.ApplyGlobalHeld(ref __result);
        }
    }

    internal static class GameUpdatePatch
    {
        public static void Prefix()
        {
            MenuControlRuntime.BeginFrame();
            RadioControlRuntime.UpdateInputFrame();
        }
    }

    internal static class ControllerManagerPressedPadStatePatch
    {
        public static void Postfix(ref PadState __result)
        {
            MenuControlRuntime.ApplyPressed(ref __result);
            RadioVirtualInput.ApplyGlobalPressed(ref __result);
        }
    }

    internal static class InputComponentStatePatch
    {
        public static void Postfix(
            InputComponent __instance,
            ref InputComponent.State __result
        )
        {
            RadioVirtualInput.ApplyHeld(__instance, ref __result);
        }
    }

    internal static class InputComponentPressedStatePatch
    {
        public static void Postfix(
            InputComponent __instance,
            ref InputComponent.State __result
        )
        {
            RadioVirtualInput.ApplyPressed(__instance, ref __result);
        }
    }

    internal static class MenuControlRuntime
    {
        private static string _command;

        public static void BeginFrame()
        {
            _command = null;
            BrokerCommandClient.Register(ModEntry.MenuCommandTarget);

            IReadOnlyDictionary<string, string> parameters;
            if (!BrokerCommandClient.TryDequeue(
                ModEntry.MenuCommandTarget,
                out parameters
            ) ||
                !parameters.TryGetValue("command", out _command) ||
                string.IsNullOrWhiteSpace(_command))
            {
                _command = null;
            }
        }

        public static void ApplyPressed(ref PadState state)
        {
            if (_command == null)
            {
                return;
            }

            switch (_command.Trim().ToLowerInvariant())
            {
                case "up":
                    state.up = true;
                    break;
                case "down":
                    state.down = true;
                    break;
                case "space":
                    state.jump = true;
                    state.confirm = true;
                    break;
                case "confirm":
                    state.confirm = true;
                    break;
                case "jump":
                    state.jump = true;
                    break;
                case "esc":
                    state.cancel = true;
                    state.pause = true;
                    break;
                case "pause":
                    state.pause = true;
                    break;
                case "cancel":
                    state.cancel = true;
                    break;
            }
        }
    }

    internal static class RadioVirtualInput
    {
        private static readonly Dictionary<PlayerEntity, VirtualPad> Players =
            new Dictionary<PlayerEntity, VirtualPad>();

        public static void Set(
            PlayerEntity target,
            bool left,
            bool right,
            bool jump,
            bool boots,
            bool snake
        )
        {
            if (target == null)
            {
                return;
            }

            GetOrCreatePad(target).Set(left, right, jump, boots, snake);
        }

        public static void Clear(PlayerEntity target)
        {
            VirtualPad pad;
            if (target != null && Players.TryGetValue(target, out pad))
            {
                pad.Clear();
            }
        }

        public static void ClearAll()
        {
            Players.Clear();
        }

        public static void ApplyHeld(
            InputComponent input,
            ref InputComponent.State state
        )
        {
            VirtualPad pad;
            if (!TryGetActivePad(input, out pad) || !pad.HasHeld)
            {
                return;
            }

            if (pad.Left)
            {
                state.left = true;
            }

            if (pad.Right)
            {
                state.right = true;
            }

            if (pad.Jump)
            {
                state.jump = true;
            }
        }

        public static void ApplyPressed(
            InputComponent input,
            ref InputComponent.State state
        )
        {
            VirtualPad pad;
            if (!TryGetActivePad(input, out pad) || !pad.HasPressed)
            {
                return;
            }

            if (pad.PressedLeft)
            {
                state.left = true;
            }

            if (pad.PressedRight)
            {
                state.right = true;
            }

            if (pad.PressedJump)
            {
                state.jump = true;
            }
        }

        public static void ApplyGlobalHeld(ref PadState state)
        {
            VirtualPad pad;
            if (!TryGetPrimaryPad(out pad))
            {
                return;
            }

            if (pad.Boots)
            {
                state.boots = true;
            }

            if (pad.Snake)
            {
                state.snake = true;
            }
        }

        public static void ApplyGlobalPressed(ref PadState state)
        {
            VirtualPad pad;
            if (!TryGetPrimaryPad(out pad))
            {
                return;
            }

            if (pad.PressedBoots)
            {
                state.boots = true;
            }

            if (pad.PressedSnake)
            {
                state.snake = true;
            }
        }

        /// <summary>
        /// Delivers boots/snake presses for every player except the primary.
        ///
        /// The primary player's press still goes out through
        /// <see cref="ApplyGlobalPressed" /> so the base game's own toggle path -
        /// sound, shoe swap, achievement bookkeeping - runs unchanged. The other
        /// players have no such path, so their press is handed to the multiplayer
        /// mod's per-player item state instead of being dropped.
        ///
        /// Called once per frame, unlike the pad-state postfixes, which run many
        /// times and must stay free of side effects.
        /// </summary>
        public static void DispatchAdditionalPlayerItemToggles()
        {
            if (RadioGameState.IsPaused() || Players.Count == 0)
            {
                return;
            }

            PlayerEntity primary = EntityManager.instance == null ? null :
                EntityManager.instance.Find<PlayerEntity>();

            foreach (KeyValuePair<PlayerEntity, VirtualPad> entry in Players)
            {
                PlayerEntity player = entry.Key;
                if (player == null || !player.IsAlive ||
                    ReferenceEquals(player, primary))
                {
                    continue;
                }

                VirtualPad pad = entry.Value;
                if (pad.PressedBoots)
                {
                    MultiplayerItems.ToggleBoots(player);
                }

                if (pad.PressedSnake)
                {
                    MultiplayerItems.ToggleSnake(player);
                }
            }
        }

        private static bool TryGetActivePad(
            InputComponent input,
            out VirtualPad pad
        )
        {
            pad = null;
            if (RadioGameState.IsPaused() || input == null || input.gameObject == null)
            {
                return false;
            }

            PlayerEntity player = input.gameObject as PlayerEntity;
            return player != null && Players.TryGetValue(player, out pad);
        }

        private static bool TryGetPrimaryPad(out VirtualPad pad)
        {
            pad = null;
            if (RadioGameState.IsPaused() || EntityManager.instance == null)
            {
                return false;
            }

            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();
            return player != null && Players.TryGetValue(player, out pad);
        }

        private static VirtualPad GetOrCreatePad(PlayerEntity target)
        {
            VirtualPad pad;
            if (!Players.TryGetValue(target, out pad))
            {
                pad = new VirtualPad();
                Players.Add(target, pad);
            }

            return pad;
        }

        private sealed class VirtualPad
        {
            public bool Left;
            public bool Right;
            public bool Jump;
            public bool Boots;
            public bool Snake;
            public bool PressedLeft;
            public bool PressedRight;
            public bool PressedJump;
            public bool PressedBoots;
            public bool PressedSnake;

            public bool HasHeld
            {
                get { return Left || Right || Jump || Boots || Snake; }
            }

            public bool HasPressed
            {
                get
                {
                    return PressedLeft || PressedRight || PressedJump ||
                        PressedBoots || PressedSnake;
                }
            }

            public void Set(bool left, bool right, bool jump, bool boots, bool snake)
            {
                PressedLeft = left && !Left;
                PressedRight = right && !Right;
                PressedJump = jump && !Jump;
                PressedBoots = boots && !Boots;
                PressedSnake = snake && !Snake;
                Left = left;
                Right = right;
                Jump = jump;
                Boots = boots;
                Snake = snake;
            }

            public void Clear()
            {
                Left = false;
                Right = false;
                Jump = false;
                Boots = false;
                Snake = false;
                PressedLeft = false;
                PressedRight = false;
                PressedJump = false;
                PressedBoots = false;
                PressedSnake = false;
            }
        }
    }

    internal static class RadioCommandParser
    {
        private static readonly Random Random = new Random();

        public static bool TryParse(string text, out RadioProgram program, out string error)
        {
            program = null;
            error = null;

            string source = (text ?? string.Empty).Trim().ToLowerInvariant();
            List<RadioCommandToken> tokens;
            if (!RadioCommandLexer.TryTokenize(source, out tokens, out error))
            {
                return false;
            }

            if (tokens.Count == 0)
            {
                return false;
            }

            List<RadioStep> steps;
            if (!RadioCommandCompiler.TryCompile(
                tokens,
                ModEntry.JumpFrameLaplaceAlpha,
                SampleDiscreteLaplace,
                out steps,
                out error
            ))
            {
                return false;
            }

            program = new RadioProgram(steps, source);
            return true;
        }

        private static int SampleDiscreteLaplace(double alpha)
        {
            if (alpha <= 0.0 || alpha >= 1.0)
            {
                return 0;
            }

            double zeroProbability = (1.0 - alpha) / (1.0 + alpha);
            if (Random.NextDouble() < zeroProbability)
            {
                return 0;
            }

            int magnitude = 1;
            while (Random.NextDouble() < alpha)
            {
                magnitude++;
            }

            return Random.Next(2) == 0 ? -magnitude : magnitude;
        }
    }

    internal static class RadioGameState
    {
        private static bool _resolved;
        private static FieldInfo _pauseManagerInstanceField;
        private static PropertyInfo _isPausedProperty;

        public static bool IsPaused()
        {
            ResolvePauseManager();

            if (_pauseManagerInstanceField == null || _isPausedProperty == null)
            {
                return false;
            }

            try
            {
                object manager = _pauseManagerInstanceField.GetValue(null);
                if (manager == null)
                {
                    return false;
                }

                object value = _isPausedProperty.GetValue(manager, null);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static void ResolvePauseManager()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            Type pauseManagerType = AccessTools.TypeByName("JumpKing.PauseMenu.PauseManager");
            if (pauseManagerType == null)
            {
                return;
            }

            _pauseManagerInstanceField = pauseManagerType.GetField(
                "instance",
                BindingFlags.Public | BindingFlags.Static
            );
            _isPausedProperty = pauseManagerType.GetProperty(
                "IsPaused",
                BindingFlags.Public | BindingFlags.Instance
            );
        }
    }

    internal static class RadioControlRuntime
    {
        private static readonly List<PlayerCommandChannel> Channels =
            new List<PlayerCommandChannel>();

        public static bool IsRunning
        {
            get
            {
                for (int i = 0; i < Channels.Count; i++)
                {
                    if (Channels[i].IsRunning)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static void UpdateInputFrame()
        {
            BrokerCommandClient.Register(ModEntry.CommandTarget);

            if (!ModEntry.IsEnabled)
            {
                Stop();
                DiscardPendingCommands();
                return;
            }

            if (RadioGameState.IsPaused())
            {
                RadioVirtualInput.ClearAll();
                return;
            }

            if (EntityManager.instance == null ||
                EntityManager.instance.Find<PlayerEntity>() == null)
            {
                RadioVirtualInput.ClearAll();
                return;
            }

            DispatchOnePendingCommand();
            for (int i = Channels.Count - 1; i >= 0; i--)
            {
                PlayerCommandChannel channel = Channels[i];
                if (channel.Target == null || !channel.Target.IsAlive)
                {
                    channel.Stop();
                    Channels.RemoveAt(i);
                    continue;
                }

                channel.Update();
            }

            RadioVirtualInput.DispatchAdditionalPlayerItemToggles();
        }

        public static void Stop()
        {
            RadioVirtualInput.ClearAll();
            for (int i = 0; i < Channels.Count; i++)
            {
                Channels[i].Stop();
            }
            Channels.Clear();
        }

        public static void ShowConfigurationError(string error)
        {
            EskiUiClient.Notify(error, 6000);
        }

        private static void DispatchOnePendingCommand()
        {
            IReadOnlyDictionary<string, string> parameters;
            string command;

            if (!BrokerCommandClient.TryDequeue(
                ModEntry.CommandTarget,
                out parameters
            ) ||
                !parameters.TryGetValue("command", out command) ||
                string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            string user;
            parameters.TryGetValue("user", out user);
            PlayerEntity target = PlayerResolver.Resolve(user);
            if (target == null)
            {
                return;
            }

            RadioProgram program;
            string error = null;
            if (!RadioCommandParser.TryParse(command, out program, out error))
            {
                if (ShouldShowReject(error))
                {
                    EskiUiClient.Notify("Radio rejected: " + error, 4000);
                }

                return;
            }

            GetOrCreateChannel(target).Enqueue(program);
            NotifyDebug("Radio queued: " + command, 2000);
        }

        private static bool ShouldShowReject(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return false;
            }

            return error.StartsWith("frames must be", StringComparison.Ordinal) ||
                error.StartsWith("command count must be", StringComparison.Ordinal) ||
                error.StartsWith("total frames must be", StringComparison.Ordinal);
        }

        private static void DiscardPendingCommands()
        {
            IReadOnlyDictionary<string, string> ignored;

            while (BrokerCommandClient.TryDequeue(
                ModEntry.CommandTarget,
                out ignored
            ))
            {
            }
        }

        private static void NotifyDebug(string message, int durationMs)
        {
            if (!ModEntry.IsDebugEnabled)
            {
                return;
            }

            EskiUiClient.Notify(message, durationMs);
        }

        private static PlayerCommandChannel GetOrCreateChannel(
            PlayerEntity target
        )
        {
            for (int i = 0; i < Channels.Count; i++)
            {
                if (ReferenceEquals(Channels[i].Target, target))
                {
                    return Channels[i];
                }
            }

            var channel = new PlayerCommandChannel(target);
            Channels.Add(channel);
            return channel;
        }

        private sealed class PlayerCommandChannel
        {
            private readonly PlayerEntity _target;
            private readonly Queue<RadioProgram> _programs = new Queue<RadioProgram>();
            private RadioProgram _program;
            private int _lastNotifiedStep;

            public PlayerCommandChannel(PlayerEntity target)
            {
                _target = target;
            }

            public PlayerEntity Target
            {
                get { return _target; }
            }

            public bool IsRunning
            {
                get { return _program != null || _programs.Count > 0; }
            }

            public void Enqueue(RadioProgram program)
            {
                _programs.Enqueue(program);
            }

            public void Update()
            {
                if (_program == null && _programs.Count > 0)
                {
                    _program = _programs.Dequeue();
                    _lastNotifiedStep = 0;
                }

                if (_program == null)
                {
                    RadioVirtualInput.Clear(_target);
                    return;
                }

                RadioStep step = _program.ActiveStep;
                if (step == null)
                {
                    RadioVirtualInput.Clear(_target);
                }
                else
                {
                    RadioVirtualInput.Set(
                        _target,
                        step.Left,
                        step.Right,
                        step.Jump,
                        step.Boots,
                        step.Snake
                    );
                }

                if (_lastNotifiedStep != _program.StepIndex)
                {
                    _lastNotifiedStep = _program.StepIndex;
                    NotifyDebug(
                        "Radio " + _program.StepIndex + "/" +
                            _program.StepCount + ": " + _program.Status,
                        1200
                    );
                }

                _program.AdvanceOneFrame();

                if (_program.IsComplete)
                {
                    NotifyDebug("Radio done", 2000);
                    _program = null;
                    _lastNotifiedStep = 0;
                }
            }

            public void Stop()
            {
                _program = null;
                _lastNotifiedStep = 0;
                _programs.Clear();
                RadioVirtualInput.Clear(_target);
            }
        }
    }

    public sealed class RadioControlOverlay : Entity, IForeground
    {
        private static RadioControlOverlay _instance;

        public static void EnsureAdded()
        {
            if (EntityManager.instance == null)
            {
                return;
            }

            if (_instance != null && _instance.IsAlive)
            {
                return;
            }

            _instance = new RadioControlOverlay();
            EntityManager.instance.AddObject(_instance);
        }

        public void ForegroundDraw()
        {
            DrawRajikonMode();
        }

        private void DrawRajikonMode()
        {
            if (!ModEntry.IsEnabled)
            {
                return;
            }

            SpriteFont font = GetFont();
            if (font == null)
            {
                return;
            }

            TextHelper.DrawString(
                font,
                "Rajikon Mode",
                new Vector2(10f, 336f),
                Color.Red,
                Vector2.Zero,
                true
            );
        }

        protected override void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private static SpriteFont GetFont()
        {
            if (Game1.instance == null || Game1.instance.contentManager == null)
            {
                return null;
            }

            if (Game1.instance.contentManager.font.MenuFontSmall != null)
            {
                return Game1.instance.contentManager.font.MenuFontSmall;
            }

            return Game1.instance.contentManager.font.MenuFont;
        }
    }

    internal static class EskiUiClient
    {
        private static Action<string, int> _notify;

        public static void Resolve()
        {
            if (_notify != null)
            {
                return;
            }

            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    if (!string.Equals(
                        assemblies[i].GetName().Name,
                        "EskiUI",
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Type apiType = assemblies[i].GetType("EskiUI", false);
                    if (apiType == null)
                    {
                        return;
                    }

                    MethodInfo notifyMethod = apiType.GetMethod(
                        "Notify",
                        new[] { typeof(string), typeof(int) }
                    );
                    if (notifyMethod != null)
                    {
                        _notify = (Action<string, int>)Delegate.CreateDelegate(
                            typeof(Action<string, int>),
                            notifyMethod
                        );
                    }

                    return;
                }
            }
            catch
            {
                _notify = null;
            }
        }

        public static void Notify(string message, int durationMs)
        {
            Resolve();
            if (_notify == null)
            {
                return;
            }

            try
            {
                _notify(message, durationMs);
            }
            catch
            {
            }
        }
    }

    internal static class BrokerCommandClient
    {
        private const string RegistryTypeName = "JumpKingHttpCommandBroker.CommandQueueRegistry";

        private static object _registry;
        private static MethodInfo _registerMethod;
        private static MethodInfo _tryDequeueMethod;
        private static int _lastResolveAssemblyCount = -1;
        private static bool _loggedMissingBroker;
        private static readonly HashSet<string> RegisteredTargets =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string target)
        {
            if (RegisteredTargets.Contains(target))
            {
                return;
            }

            if (!Resolve())
            {
                return;
            }

            try
            {
                _registerMethod.Invoke(_registry, new object[] { target });
                RegisteredTargets.Add(target);
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl broker register failed: " + ex.Message
                );
            }
        }

        public static bool TryDequeue(
            string target,
            out IReadOnlyDictionary<string, string> parameters
        )
        {
            parameters = null;

            if (!RegisteredTargets.Contains(target))
            {
                Register(target);
            }

            if (!RegisteredTargets.Contains(target) || !Resolve())
            {
                return false;
            }

            try
            {
                object[] args = new object[] { target, null };
                bool dequeued = (bool)_tryDequeueMethod.Invoke(_registry, args);
                parameters = args[1] as IReadOnlyDictionary<string, string>;
                return dequeued;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl broker dequeue failed: " + ex.Message
                );
                return false;
            }
        }

        private static bool Resolve()
        {
            if (_registry != null)
            {
                return true;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (_lastResolveAssemblyCount == assemblies.Length)
            {
                return false;
            }

            _lastResolveAssemblyCount = assemblies.Length;
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type registryType = assemblies[i].GetType(RegistryTypeName, false);
                if (registryType == null)
                {
                    continue;
                }

                FieldInfo instanceField = registryType.GetField(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static
                );
                MethodInfo registerMethod = registryType.GetMethod(
                    "Register",
                    new Type[] { typeof(string) }
                );
                MethodInfo tryDequeueMethod = registryType.GetMethod(
                    "TryDequeue",
                    new Type[]
                    {
                        typeof(string),
                        typeof(IReadOnlyDictionary<string, string>).MakeByRefType()
                    }
                );

                if (instanceField == null || registerMethod == null || tryDequeueMethod == null)
                {
                    continue;
                }

                _registry = instanceField.GetValue(null);
                _registerMethod = registerMethod;
                _tryDequeueMethod = tryDequeueMethod;
                return _registry != null;
            }

            if (!_loggedMissingBroker)
            {
                _loggedMissingBroker = true;
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl: JumpKingHttpCommandBroker is not loaded."
                );
            }

            return false;
        }
    }
}

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
        private static UserCommandRouter _userRouter;
        private static string _settingsPath;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            EnsurePreferencesLoaded();
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
            BrokerCommandClient.Register(MenuCommandTarget);
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            EnsurePreferencesLoaded();
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
            BrokerCommandClient.Register(MenuCommandTarget);
            RadioControlOverlay.EnsureAdded();
            MultiplayerRuntime.OnLevelStart();
        }

        [OnLevelEnd]
        public static void OnLevelEnd()
        {
            MultiplayerRuntime.OnLevelEnd();
        }

        [OnLevelUnload]
        public static void OnLevelUnload()
        {
            MultiplayerRuntime.OnLevelEnd();
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

        internal static bool IsMultiplayerEnabled
        {
            get
            {
                EnsurePreferencesLoaded();
                return _preferences.MultiplayerEnabled;
            }
        }

        internal static PlayerTargets ResolvePlayerTargets(string user)
        {
            EnsurePreferencesLoaded();
            return _userRouter.Resolve(_preferences.MultiplayerEnabled, user);
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

        internal static bool SetMultiplayerEnabled(bool isEnabled)
        {
            EnsurePreferencesLoaded();

            if (_preferences.MultiplayerEnabled == isEnabled)
            {
                return isEnabled;
            }

            if (isEnabled)
            {
                string error;
                if (!TryReloadPreferences(out error))
                {
                    RadioControlRuntime.ShowConfigurationError(error);
                    return false;
                }
            }

            _preferences.MultiplayerEnabled = isEnabled;
            SavePreferences();

            if (!isEnabled)
            {
                RadioControlRuntime.StopPlayer2();
            }

            MultiplayerRuntime.SetEnabled(isEnabled);
            return isEnabled;
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

        [PauseMenuItemSetting]
        [MainMenuItemSetting]
        public static RadioMultiplayerToggle RadioMultiplayerMenu(
            object factory,
            JumpKing.PauseMenu.GuiFormat format
        )
        {
            return new RadioMultiplayerToggle();
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
                MethodInfo inputGetState = AccessTools.Method(typeof(InputComponent), "GetState");
                MethodInfo inputGetPressedState = AccessTools.Method(
                    typeof(InputComponent),
                    "GetPressedState"
                );
                MethodInfo inputStatePrefix = AccessTools.Method(
                    typeof(Player2InputStatePatch),
                    "Prefix"
                );
                MethodInfo playerUpdate = AccessTools.Method(typeof(PlayerEntity), "Update");
                MethodInfo playerUpdatePrefix = AccessTools.Method(
                    typeof(Player2SaveUpdatePatch),
                    "Prefix"
                );
                MethodInfo entityUpdateComponents = AccessTools.Method(
                    typeof(Entity),
                    "UpdateComponents"
                );
                MethodInfo player2ScreenPrefix = AccessTools.Method(
                    typeof(Player2ScreenUpdatePatch),
                    "Prefix"
                );
                MethodInfo player2ScreenPostfix = AccessTools.Method(
                    typeof(Player2ScreenUpdatePatch),
                    "Postfix"
                );
                MethodInfo jumpGameDraw = AccessTools.Method(typeof(JumpGame), "Draw");
                MethodInfo jumpGameDrawPrefix = AccessTools.Method(
                    typeof(MultiplayerDrawPatch),
                    "Prefix"
                );
                Type endingManagerType = AccessTools.TypeByName(
                    "JumpKing.GameManager.MultiEnding.EndingManager"
                );
                MethodInfo checkWin = endingManagerType == null ? null :
                    AccessTools.Method(endingManagerType, "CheckWin");
                MethodInfo checkWinPostfix = AccessTools.Method(
                    typeof(MultiplayerEndingPatch),
                    "Postfix"
                );
                if (getPadState == null ||
                    getPressedPadState == null ||
                    gameUpdate == null ||
                    gameUpdatePrefix == null ||
                    padStatePostfix == null ||
                    pressedPadStatePostfix == null ||
                    inputGetState == null ||
                    inputGetPressedState == null ||
                    inputStatePrefix == null ||
                    playerUpdate == null ||
                    playerUpdatePrefix == null ||
                    entityUpdateComponents == null ||
                    player2ScreenPrefix == null ||
                    player2ScreenPostfix == null ||
                    jumpGameDraw == null ||
                    jumpGameDrawPrefix == null ||
                    checkWin == null ||
                    checkWinPostfix == null)
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
                _harmony.Patch(inputGetState, prefix: new HarmonyMethod(inputStatePrefix));
                _harmony.Patch(inputGetPressedState, prefix: new HarmonyMethod(inputStatePrefix));
                _harmony.Patch(playerUpdate, prefix: new HarmonyMethod(playerUpdatePrefix));
                _harmony.Patch(
                    entityUpdateComponents,
                    prefix: new HarmonyMethod(player2ScreenPrefix),
                    postfix: new HarmonyMethod(player2ScreenPostfix)
                );
                _harmony.Patch(jumpGameDraw, prefix: new HarmonyMethod(jumpGameDrawPrefix));
                _harmony.Patch(checkWin, postfix: new HarmonyMethod(checkWinPostfix));
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
                        !settingsText.Contains("IsDebugEnabled") ||
                        !settingsText.Contains("MultiplayerEnabled") ||
                        !settingsText.Contains("SingleMode") ||
                        !settingsText.Contains("MultiplayerMode");

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

            EnsurePreferenceSections(_preferences);

            try
            {
                _userRouter = CreateUserRouter(_preferences);
            }
            catch (FormatException ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl settings error: " + ex.Message
                );
                _preferences.MultiplayerEnabled = false;
                _userRouter = CreateUserRouter(new RadioControlPreferences());
            }

            if (shouldSavePreferences)
            {
                SavePreferences();
            }
        }

        private static bool TryReloadPreferences(out string error)
        {
            error = null;

            try
            {
                var serializer = new XmlSerializer(typeof(RadioControlPreferences));
                RadioControlPreferences candidate;

                using (var stream = File.OpenRead(_settingsPath))
                {
                    candidate = (RadioControlPreferences)serializer.Deserialize(stream);
                }

                EnsurePreferenceSections(candidate);
                UserCommandRouter candidateRouter = CreateUserRouter(candidate);
                _preferences = candidate;
                _userRouter = candidateRouter;
                return true;
            }
            catch (Exception ex)
            {
                error = "Multiplayer settings were not loaded: " + ex.Message;
                JumpKing.Program.crashLog.AddErrorMessage("RadioControl: " + error);
                return false;
            }
        }

        private static UserCommandRouter CreateUserRouter(RadioControlPreferences preferences)
        {
            return new UserCommandRouter(
                preferences.SingleMode.Player1Users,
                preferences.MultiplayerMode.Player1Users,
                preferences.MultiplayerMode.Player2Users
            );
        }

        private static void EnsurePreferenceSections(RadioControlPreferences preferences)
        {
            if (preferences.SingleMode == null)
            {
                preferences.SingleMode = new SingleModePreferences();
            }

            if (preferences.MultiplayerMode == null)
            {
                preferences.MultiplayerMode = new MultiplayerModePreferences();
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
        public bool MultiplayerEnabled { get; set; } = false;
        public SingleModePreferences SingleMode { get; set; } = new SingleModePreferences();
        public MultiplayerModePreferences MultiplayerMode { get; set; } =
            new MultiplayerModePreferences();
    }

    public class SingleModePreferences
    {
        public string Player1Users { get; set; } = "*";
    }

    public class MultiplayerModePreferences
    {
        public string Player1Users { get; set; } = "[a-m]*";
        public string Player2Users { get; set; } = "[n-z]*";
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

    public class RadioMultiplayerToggle : ITextToggle
    {
        public RadioMultiplayerToggle() : base(ModEntry.IsMultiplayerEnabled)
        {
        }

        protected override string GetName()
        {
            return "Multiplayer Mode";
        }

        protected override void OnToggle()
        {
            OverrideToggle(ModEntry.SetMultiplayerEnabled(toggle));
        }
    }

    internal static class ControllerManagerPadStatePatch
    {
        public static void Postfix(ref PadState __result)
        {
            RadioVirtualInput.ApplyHeld(ref __result);
        }
    }

    internal static class GameUpdatePatch
    {
        public static void Prefix()
        {
            MenuControlRuntime.BeginFrame();
            MultiplayerRuntime.SynchronizeBlockBehaviours();
            RadioControlRuntime.UpdateInputFrame();
        }
    }

    internal static class ControllerManagerPressedPadStatePatch
    {
        public static void Postfix(ref PadState __result)
        {
            MenuControlRuntime.ApplyPressed(ref __result);
            RadioVirtualInput.ApplyPressed(ref __result);
        }
    }

    internal static class MenuControlRuntime
    {
        private static string _command;

        public static void BeginFrame()
        {
            _command = null;
            BrokerCommandClient.Register(ModEntry.MenuCommandTarget);

            if (!BrokerCommandClient.TryDequeue(
                ModEntry.MenuCommandTarget,
                out _command
            ))
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
        private static readonly VirtualPad Player1 = new VirtualPad();
        private static readonly VirtualPad Player2 = new VirtualPad();

        public static void Set(
            PlayerTargets target,
            bool left,
            bool right,
            bool jump,
            bool boots,
            bool snake
        )
        {
            GetPad(target).Set(left, right, jump, boots, snake);
        }

        public static void Clear(PlayerTargets target)
        {
            GetPad(target).Clear();
        }

        public static void ClearAll()
        {
            Player1.Clear();
            Player2.Clear();
        }

        public static void ApplyHeld(ref PadState state)
        {
            if (!Player1.HasHeld)
            {
                return;
            }

            if (RadioGameState.IsPaused())
            {
                return;
            }

            if (EntityManager.instance == null ||
                EntityManager.instance.Find<PlayerEntity>() == null)
            {
                return;
            }

            if (Player1.Left)
            {
                state.left = true;
            }

            if (Player1.Right)
            {
                state.right = true;
            }

            if (Player1.Jump)
            {
                state.jump = true;
            }

            if (Player1.Boots)
            {
                state.boots = true;
            }

            if (Player1.Snake)
            {
                state.snake = true;
            }
        }

        public static void ApplyPressed(ref PadState state)
        {
            if (!Player1.HasPressed)
            {
                return;
            }

            if (RadioGameState.IsPaused())
            {
                return;
            }

            if (EntityManager.instance == null ||
                EntityManager.instance.Find<PlayerEntity>() == null)
            {
                return;
            }

            if (Player1.PressedLeft)
            {
                state.left = true;
            }

            if (Player1.PressedRight)
            {
                state.right = true;
            }

            if (Player1.PressedJump)
            {
                state.jump = true;
            }

            if (Player1.PressedBoots)
            {
                state.boots = true;
            }

            if (Player1.PressedSnake)
            {
                state.snake = true;
            }
        }

        public static InputComponent.State GetPlayer2State(bool pressed)
        {
            var state = new InputComponent.State();

            if (RadioGameState.IsPaused())
            {
                return state;
            }

            state.left = pressed ? Player2.PressedLeft : Player2.Left;
            state.right = pressed ? Player2.PressedRight : Player2.Right;
            state.jump = pressed ? Player2.PressedJump : Player2.Jump;
            return state;
        }

        private static VirtualPad GetPad(PlayerTargets target)
        {
            return target == PlayerTargets.Player2 ? Player2 : Player1;
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
        private static readonly PlayerCommandChannel Player1 =
            new PlayerCommandChannel(PlayerTargets.Player1, "P1");
        private static readonly PlayerCommandChannel Player2 =
            new PlayerCommandChannel(PlayerTargets.Player2, "P2");

        public static string DisplayText { get; private set; }
        public static float MessageSeconds { get; private set; }
        private static bool _forceDisplay;

        public static bool HasDisplay
        {
            get
            {
                return Player1.IsRunning || Player2.IsRunning ||
                    (MessageSeconds > 0f && !string.IsNullOrEmpty(DisplayText));
            }
        }

        public static bool IsRunning
        {
            get { return Player1.IsRunning || Player2.IsRunning; }
        }

        public static bool ShouldDrawDisplay
        {
            get { return HasDisplay && (ModEntry.IsDebugEnabled || _forceDisplay); }
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
            Player1.Update();
            Player2.Update();
        }

        public static void UpdateUi(float delta)
        {
            TickMessage(delta);
        }

        public static void Stop()
        {
            RadioVirtualInput.ClearAll();
            Player1.Stop();
            Player2.Stop();
            _forceDisplay = false;
        }

        public static void StopPlayer2()
        {
            Player2.Stop();
        }

        public static void ShowConfigurationError(string error)
        {
            _forceDisplay = true;
            DisplayText = error;
            MessageSeconds = 6f;
        }

        private static void DispatchOnePendingCommand()
        {
            string user;
            string command;

            if (!BrokerCommandClient.TryDequeue(ModEntry.CommandTarget, out user, out command))
            {
                return;
            }

            PlayerTargets targets = ModEntry.ResolvePlayerTargets(user);
            if (targets == PlayerTargets.None)
            {
                return;
            }

            bool accepted = false;
            string error = null;

            if ((targets & PlayerTargets.Player1) != 0)
            {
                accepted |= Player1.TryEnqueue(command, out error);
            }

            if ((targets & PlayerTargets.Player2) != 0)
            {
                string player2Error;
                accepted |= Player2.TryEnqueue(command, out player2Error);
                if (error == null)
                {
                    error = player2Error;
                }
            }

            if (accepted)
            {
                _forceDisplay = false;
                DisplayText = "Radio queued: " + command;
                MessageSeconds = 2f;
                return;
            }

            if (ShouldShowReject(error))
            {
                _forceDisplay = true;
                DisplayText = "Radio rejected: " + error;
                MessageSeconds = 4f;
            }
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
            string ignoredUser;
            string ignored;

            while (BrokerCommandClient.TryDequeue(
                ModEntry.CommandTarget,
                out ignoredUser,
                out ignored
            ))
            {
            }
        }

        private static void TickMessage(float delta)
        {
            if (MessageSeconds <= 0f)
            {
                return;
            }

            MessageSeconds = Math.Max(0f, MessageSeconds - delta);
        }

        private sealed class PlayerCommandChannel
        {
            private readonly PlayerTargets _target;
            private readonly string _label;
            private readonly Queue<RadioProgram> _programs = new Queue<RadioProgram>();
            private RadioProgram _program;

            public PlayerCommandChannel(PlayerTargets target, string label)
            {
                _target = target;
                _label = label;
            }

            public bool IsRunning
            {
                get { return _program != null || _programs.Count > 0; }
            }

            public bool TryEnqueue(string command, out string error)
            {
                RadioProgram parsed;
                if (!RadioCommandParser.TryParse(command, out parsed, out error))
                {
                    return false;
                }

                _programs.Enqueue(parsed);
                return true;
            }

            public void Update()
            {
                if (_program == null && _programs.Count > 0)
                {
                    _program = _programs.Dequeue();
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

                _forceDisplay = false;
                DisplayText = "Radio " + _label + " " + _program.StepIndex + "/" +
                    _program.StepCount + ": " + _program.Status;
                MessageSeconds = 1.2f;
                _program.AdvanceOneFrame();

                if (_program.IsComplete)
                {
                    DisplayText = "Radio " + _label + " done";
                    MessageSeconds = 2f;
                    _program = null;
                }
            }

            public void Stop()
            {
                _program = null;
                _programs.Clear();
                RadioVirtualInput.Clear(_target);
            }
        }
    }

    public sealed class RadioControlOverlay : Entity, IForeground
    {
        private static RadioControlOverlay _instance;
        private Texture2D _pixel;

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

        protected override void Update(float delta)
        {
            RadioControlRuntime.UpdateUi(delta);
        }

        public void ForegroundDraw()
        {
            DrawRajikonMode();

            if (!RadioControlRuntime.ShouldDrawDisplay)
            {
                return;
            }

            SpriteFont font = GetFont();
            if (font == null)
            {
                return;
            }

            EnsurePixel();
            if (_pixel == null)
            {
                return;
            }

            string text = RadioControlRuntime.DisplayText ?? string.Empty;
            Vector2 size = font.MeasureString(text);
            int paddingX = 8;
            int paddingY = 5;
            int width = (int)Math.Ceiling(size.X) + paddingX * 2;
            int height = (int)Math.Ceiling(size.Y) + paddingY * 2;
            int x = 480 - width - 10;
            int y = 10;

            Game1.spriteBatch.Draw(
                _pixel,
                new Rectangle(x, y, width, height),
                new Color((byte)0, (byte)0, (byte)0, (byte)185)
            );
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 1), Color.Gray);
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y + height - 1, width, 1), Color.Gray);
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, height), Color.Gray);
            Game1.spriteBatch.Draw(_pixel, new Rectangle(x + width - 1, y, 1, height), Color.Gray);

            TextHelper.DrawString(
                font,
                text,
                new Vector2(x + paddingX, y + paddingY),
                Color.White,
                Vector2.Zero,
                true
            );
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
            if (_pixel != null)
            {
                _pixel.Dispose();
                _pixel = null;
            }

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private void EnsurePixel()
        {
            if (_pixel != null || Game1.instance == null)
            {
                return;
            }

            _pixel = new Texture2D(Game1.instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
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

    internal static class BrokerCommandClient
    {
        private const string RegistryTypeName = "JumpKingHttpCommandBroker.CommandQueueRegistry";

        private static object _registry;
        private static MethodInfo _registerMethod;
        private static MethodInfo _tryDequeueMethod;
        private static MethodInfo _tryDequeueWithUserMethod;
        private static DateTime _nextResolveUtc = DateTime.MinValue;
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

        public static bool TryDequeue(string target, out string command)
        {
            command = null;

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
                command = args[1] as string;
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

        public static bool TryDequeue(string target, out string user, out string command)
        {
            user = null;
            command = null;

            if (!RegisteredTargets.Contains(target))
            {
                Register(target);
            }

            if (!RegisteredTargets.Contains(target) || !Resolve())
            {
                return false;
            }

            if (_tryDequeueWithUserMethod == null)
            {
                return TryDequeue(target, out command);
            }

            try
            {
                object[] args = new object[] { target, null, null };
                bool dequeued = (bool)_tryDequeueWithUserMethod.Invoke(_registry, args);
                user = args[1] as string;
                command = args[2] as string;
                return dequeued;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl broker user dequeue failed: " + ex.Message
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

            DateTime nowUtc = DateTime.UtcNow;
            if (nowUtc < _nextResolveUtc)
            {
                return false;
            }

            _nextResolveUtc = nowUtc.AddSeconds(1);

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
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
                    new Type[] { typeof(string), typeof(string).MakeByRefType() }
                );
                MethodInfo tryDequeueWithUserMethod = registryType.GetMethod(
                    "TryDequeue",
                    new Type[]
                    {
                        typeof(string),
                        typeof(string).MakeByRefType(),
                        typeof(string).MakeByRefType()
                    }
                );

                if (instanceField == null || registerMethod == null || tryDequeueMethod == null)
                {
                    continue;
                }

                _registry = instanceField.GetValue(null);
                _registerMethod = registerMethod;
                _tryDequeueMethod = tryDequeueMethod;
                _tryDequeueWithUserMethod = tryDequeueWithUserMethod;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Mods;
using JumpKing.PauseMenu.BT.Actions;
using JumpKing.Player;
using JumpKing.Util;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace RadioControlMod
{
    [JumpKingMod("eski4869.RadioControlMod")]
    public static class ModEntry
    {
        internal const string CommandTarget = "radio_control";
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
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            EnsurePreferencesLoaded();
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
            RadioControlOverlay.EnsureAdded();
        }

        internal static Keys JumpKey
        {
            get
            {
                EnsurePreferencesLoaded();
                return _preferences.JumpKey;
            }
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
                MethodInfo controllerUpdate = AccessTools.Method(
                    typeof(ControllerManager),
                    "Update"
                );
                MethodInfo getKeyboardButtons = AccessTools.Method(
                    "JumpKing.Controller.KeyboardPad:GetPressedButtons"
                );
                MethodInfo controllerUpdatePrefix = AccessTools.Method(
                    typeof(ControllerManagerUpdatePatch),
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
                MethodInfo keyboardButtonsPostfix = AccessTools.Method(
                    typeof(KeyboardPadGetPressedButtonsPatch),
                    "Postfix"
                );

                if (getPadState == null ||
                    getPressedPadState == null ||
                    controllerUpdate == null ||
                    getKeyboardButtons == null ||
                    controllerUpdatePrefix == null ||
                    padStatePostfix == null ||
                    pressedPadStatePostfix == null ||
                    keyboardButtonsPostfix == null)
                {
                    JumpKing.Program.crashLog.AddErrorMessage(
                        "RadioControl patch target not found."
                    );
                    return;
                }

                _harmony = new Harmony("eski4869.RadioControlMod");
                _harmony.Patch(controllerUpdate, prefix: new HarmonyMethod(controllerUpdatePrefix));
                _harmony.Patch(getPadState, postfix: new HarmonyMethod(padStatePostfix));
                _harmony.Patch(getPressedPadState, postfix: new HarmonyMethod(pressedPadStatePostfix));
                _harmony.Patch(getKeyboardButtons, postfix: new HarmonyMethod(keyboardButtonsPostfix));
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

    internal static class ControllerManagerUpdatePatch
    {
        public static void Prefix()
        {
            RadioControlRuntime.UpdateInputFrame();
        }
    }

    public class RadioControlPreferences
    {
        public bool IsEnabled { get; set; } = true;
        public bool IsDebugEnabled { get; set; } = true;
        public Keys JumpKey { get; set; } = Keys.LeftControl;
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
            RadioVirtualInput.ApplyHeld(ref __result);
        }
    }

    internal static class ControllerManagerPressedPadStatePatch
    {
        public static void Postfix(ref PadState __result)
        {
            RadioVirtualInput.ApplyPressed(ref __result);
        }
    }

    internal static class KeyboardPadGetPressedButtonsPatch
    {
        public static void Postfix(ref int[] __result)
        {
            RadioVirtualInput.AppendKeyboardButtons(ref __result);
        }
    }

    internal static class RadioVirtualInput
    {
        private static bool _left;
        private static bool _right;
        private static bool _jump;
        private static bool _pressedLeft;
        private static bool _pressedRight;
        private static bool _pressedJump;

        public static void Set(bool left, bool right, bool jump)
        {
            _pressedLeft = left && !_left;
            _pressedRight = right && !_right;
            _pressedJump = jump && !_jump;
            _left = left;
            _right = right;
            _jump = jump;
        }

        public static void Clear()
        {
            _left = false;
            _right = false;
            _jump = false;
            _pressedLeft = false;
            _pressedRight = false;
            _pressedJump = false;
        }

        public static void ApplyHeld(ref PadState state)
        {
            if (!_left && !_right && !_jump)
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

            if (_left)
            {
                state.left = true;
            }

            if (_right)
            {
                state.right = true;
            }

            if (_jump)
            {
                state.jump = true;
            }
        }

        public static void ApplyPressed(ref PadState state)
        {
            if (!_pressedLeft && !_pressedRight && !_pressedJump)
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

            if (_pressedLeft)
            {
                state.left = true;
            }

            if (_pressedRight)
            {
                state.right = true;
            }

            if (_pressedJump)
            {
                state.jump = true;
            }
        }

        public static void AppendKeyboardButtons(ref int[] buttons)
        {
            if (!_jump)
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

            int jumpKey = (int)ModEntry.JumpKey;
            List<int> merged = new List<int>(buttons ?? new int[0]);

            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i] == jumpKey)
                {
                    return;
                }
            }

            merged.Add(jumpKey);
            buttons = merged.ToArray();
        }
    }

    internal sealed class RadioProgram
    {
        private readonly List<RadioStep> _steps;
        private readonly string _source;
        private int _index;
        private int _remainingFrames;

        public RadioProgram(List<RadioStep> steps, string source)
        {
            _steps = steps;
            _source = source;
            _index = 0;
            _remainingFrames = steps.Count > 0 ? steps[0].Frames : 0;
        }

        public string Source
        {
            get { return _source; }
        }

        public int StepIndex
        {
            get { return _index + 1; }
        }

        public int StepCount
        {
            get { return _steps.Count; }
        }

        public int RemainingFrames
        {
            get { return _remainingFrames; }
        }

        public bool IsComplete
        {
            get { return _index >= _steps.Count; }
        }

        public string Status
        {
            get
            {
                if (IsComplete)
                {
                    return "Done";
                }

                return _steps[_index].Name + " " + _remainingFrames + "f";
            }
        }

        public void ApplyCurrentInput()
        {
            if (IsComplete)
            {
                RadioVirtualInput.Clear();
                return;
            }

            RadioStep step = _steps[_index];
            RadioVirtualInput.Set(step.Left, step.Right, step.Jump);
        }

        public void AdvanceOneFrame()
        {
            if (IsComplete)
            {
                return;
            }

            _remainingFrames--;

            if (_remainingFrames > 0)
            {
                return;
            }

            _index++;

            if (!IsComplete)
            {
                _remainingFrames = _steps[_index].Frames;
            }
        }
    }

    internal sealed class RadioStep
    {
        public RadioStep(string name, int frames, bool left, bool right, bool jump)
        {
            Name = name;
            Frames = frames;
            Left = left;
            Right = right;
            Jump = jump;
        }

        public string Name { get; private set; }
        public int Frames { get; private set; }
        public bool Left { get; private set; }
        public bool Right { get; private set; }
        public bool Jump { get; private set; }
    }

    internal static class RadioCommandParser
    {
        private const int DefaultFrames = 35;
        private const int MaxCommandsPerMessage = 20;
        private const int MaxFramesPerCommand = 600;
        private const int MaxTotalFramesPerMessage = 1800;
        private static readonly Random Random = new Random();

        public static bool TryParse(string text, out RadioProgram program, out string error)
        {
            program = null;
            error = null;

            text = (text ?? string.Empty).Trim().ToLowerInvariant();

            if (text.Length == 0)
            {
                error = "empty command";
                return false;
            }

            string[] tokens = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                error = "empty command";
                return false;
            }

            if (tokens.Length > MaxCommandsPerMessage)
            {
                error = "too many commands";
                return false;
            }

            List<RadioStep> steps = new List<RadioStep>();
            int totalFrames = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                RadioStep step;

                if (!TryParseToken(tokens[i], out step))
                {
                    error = "invalid token: " + tokens[i];
                    return false;
                }

                if (step.Frames > MaxFramesPerCommand)
                {
                    error = "too many frames: " + tokens[i];
                    return false;
                }

                totalFrames += step.Frames;

                if (totalFrames > MaxTotalFramesPerMessage)
                {
                    error = "program too long";
                    return false;
                }

                steps.Add(step);
            }

            program = new RadioProgram(steps, text);
            return true;
        }

        private static bool TryParseToken(string token, out RadioStep step)
        {
            step = null;

            if (token.StartsWith("jr", StringComparison.Ordinal))
            {
                return TryMakeStep(token, 2, true, true, false, out step);
            }

            if (token.StartsWith("jl", StringComparison.Ordinal))
            {
                return TryMakeStep(token, 2, true, false, true, out step);
            }

            if (token.StartsWith("j", StringComparison.Ordinal))
            {
                return TryMakeStep(token, 1, true, false, false, out step);
            }

            if (token.StartsWith("r", StringComparison.Ordinal))
            {
                return TryMakeStep(token, 1, false, true, false, out step);
            }

            if (token.StartsWith("l", StringComparison.Ordinal))
            {
                return TryMakeStep(token, 1, false, false, true, out step);
            }

            if (token.StartsWith("w", StringComparison.Ordinal))
            {
                return TryMakeStep(token, 1, false, false, false, out step);
            }

            return false;
        }

        private static bool TryMakeStep(
            string token,
            int prefixLength,
            bool jump,
            bool right,
            bool left,
            out RadioStep step
        )
        {
            step = null;
            int frames = DefaultFrames;

            if (token.Length > prefixLength)
            {
                if (!int.TryParse(token.Substring(prefixLength), out frames) || frames <= 0)
                {
                    return false;
                }
            }

            if (jump && frames != DefaultFrames)
            {
                frames += SampleDiscreteLaplace(ModEntry.JumpFrameLaplaceAlpha);

                if (frames < 1)
                {
                    frames = 1;
                }
                else if (frames > MaxFramesPerCommand)
                {
                    frames = MaxFramesPerCommand;
                }
            }

            step = new RadioStep(token.Substring(0, prefixLength), frames, left, right, jump);
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
        private static RadioProgram _program;

        public static string DisplayText { get; private set; }
        public static float MessageSeconds { get; private set; }

        public static bool HasDisplay
        {
            get
            {
                return _program != null ||
                    (MessageSeconds > 0f && !string.IsNullOrEmpty(DisplayText));
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
                RadioVirtualInput.Clear();
                return;
            }

            if (_program == null)
            {
                RadioVirtualInput.Clear();
                TryStartNextProgram();
            }

            if (_program == null)
            {
                return;
            }

            _program.ApplyCurrentInput();
            DisplayText = "Radio " + _program.StepIndex + "/" + _program.StepCount + ": " + _program.Status;
            MessageSeconds = 1.2f;

            _program.AdvanceOneFrame();

            if (_program.IsComplete)
            {
                DisplayText = "Radio done";
                MessageSeconds = 2f;
                _program = null;
            }
        }

        public static void UpdateUi(float delta)
        {
            TickMessage(delta);
        }

        public static void Stop()
        {
            RadioVirtualInput.Clear();
            _program = null;
        }

        private static void TryStartNextProgram()
        {
            string command;

            if (!BrokerCommandClient.TryDequeue(ModEntry.CommandTarget, out command))
            {
                return;
            }

            RadioProgram parsed;
            string error;

            if (!RadioCommandParser.TryParse(command, out parsed, out error))
            {
                DisplayText = "Radio rejected: " + error;
                MessageSeconds = 2f;
                return;
            }

            _program = parsed;
            DisplayText = "Radio start: " + parsed.Source;
            MessageSeconds = 2f;
        }

        private static void DiscardPendingCommands()
        {
            string ignored;

            while (BrokerCommandClient.TryDequeue(ModEntry.CommandTarget, out ignored))
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
            if (!ModEntry.IsDebugEnabled || !RadioControlRuntime.HasDisplay)
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
        private static DateTime _nextResolveUtc = DateTime.MinValue;
        private static bool _loggedMissingBroker;
        private static bool _registered;

        public static void Register(string target)
        {
            if (_registered)
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
                _registered = true;
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

            if (!_registered)
            {
                Register(target);
            }

            if (!_registered || !Resolve())
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

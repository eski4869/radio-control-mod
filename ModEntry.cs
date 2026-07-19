using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
                MethodInfo inputComponentUpdate = AccessTools.Method(
                    typeof(InputComponent),
                    "Update"
                );
                MethodInfo inputComponentUpdatePrefix = AccessTools.Method(
                    typeof(InputComponentUpdatePatch),
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
                if (getPadState == null ||
                    getPressedPadState == null ||
                    inputComponentUpdate == null ||
                    inputComponentUpdatePrefix == null ||
                    padStatePostfix == null ||
                    pressedPadStatePostfix == null)
                {
                    JumpKing.Program.crashLog.AddErrorMessage(
                        "RadioControl patch target not found."
                    );
                    return;
                }

                _harmony = new Harmony("eski4869.RadioControlMod");
                _harmony.Patch(inputComponentUpdate, prefix: new HarmonyMethod(inputComponentUpdatePrefix));
                _harmony.Patch(getPadState, postfix: new HarmonyMethod(padStatePostfix));
                _harmony.Patch(getPressedPadState, postfix: new HarmonyMethod(pressedPadStatePostfix));
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

    internal static class InputComponentUpdatePatch
    {
        public static void Prefix()
        {
            RadioControlRuntime.UpdateInputFrame();
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

    internal static class RadioVirtualInput
    {
        private static bool _left;
        private static bool _right;
        private static bool _jump;
        private static bool _boots;
        private static bool _snake;
        private static bool _pressedLeft;
        private static bool _pressedRight;
        private static bool _pressedJump;
        private static bool _pressedBoots;
        private static bool _pressedSnake;

        public static void Set(bool left, bool right, bool jump, bool boots, bool snake)
        {
            _pressedLeft = left && !_left;
            _pressedRight = right && !_right;
            _pressedJump = jump && !_jump;
            _pressedBoots = boots && !_boots;
            _pressedSnake = snake && !_snake;
            _left = left;
            _right = right;
            _jump = jump;
            _boots = boots;
            _snake = snake;
        }

        public static void Clear()
        {
            _left = false;
            _right = false;
            _jump = false;
            _boots = false;
            _snake = false;
            _pressedLeft = false;
            _pressedRight = false;
            _pressedJump = false;
            _pressedBoots = false;
            _pressedSnake = false;
        }

        public static void ApplyHeld(ref PadState state)
        {
            if (!_left && !_right && !_jump && !_boots && !_snake)
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

            if (_boots)
            {
                state.boots = true;
            }

            if (_snake)
            {
                state.snake = true;
            }
        }

        public static void ApplyPressed(ref PadState state)
        {
            if (!_pressedLeft && !_pressedRight && !_pressedJump && !_pressedBoots && !_pressedSnake)
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

            if (_pressedBoots)
            {
                state.boots = true;
            }

            if (_pressedSnake)
            {
                state.snake = true;
            }
        }
    }

    internal sealed class RadioProgram
    {
        private readonly List<RadioStep> _steps;
        private readonly string _source;
        private int _index;
        private int _remainingFrames;
        private int _releaseFrames;

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

                if (_releaseFrames > 0)
                {
                    return "release " + _releaseFrames + "f";
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

            if (_releaseFrames > 0)
            {
                RadioVirtualInput.Clear();
                return;
            }

            RadioStep step = _steps[_index];
            RadioVirtualInput.Set(step.Left, step.Right, step.Jump, step.Boots, step.Snake);
        }

        public void AdvanceOneFrame()
        {
            if (IsComplete)
            {
                return;
            }

            if (_releaseFrames > 0)
            {
                _releaseFrames--;

                if (_releaseFrames > 0)
                {
                    return;
                }

                _index++;

                if (!IsComplete)
                {
                    _remainingFrames = _steps[_index].Frames;
                }

                return;
            }

            _remainingFrames--;

            if (_remainingFrames > 0)
            {
                return;
            }

            RadioStep step = _steps[_index];

            if ((step.Jump || step.Left || step.Right || step.Boots || step.Snake) && _index + 1 < _steps.Count)
            {
                _releaseFrames = 1;
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
        public RadioStep(string name, int frames, bool left, bool right, bool jump, bool boots, bool snake)
        {
            Name = name;
            Frames = frames;
            Left = left;
            Right = right;
            Jump = jump;
            Boots = boots;
            Snake = snake;
        }

        public string Name { get; private set; }
        public int Frames { get; private set; }
        public bool Left { get; private set; }
        public bool Right { get; private set; }
        public bool Jump { get; private set; }
        public bool Boots { get; private set; }
        public bool Snake { get; private set; }
    }

    internal static class RadioCommandParser
    {
        private const int DefaultFrames = 35;
        private const int MaxCommandsPerMessage = 32;
        private const int MaxJumpFrames = 300;
        private const int MaxMoveFrames = 300;
        private const int MaxWaitFrames = 300;
        private const int MaxTotalFramesPerMessage = 1200;
        private static readonly Random Random = new Random();

        public static bool TryParse(string text, out RadioProgram program, out string error)
        {
            program = null;
            error = null;

            string source = (text ?? string.Empty).Trim().ToLowerInvariant();
            string command = Normalize(source);

            if (command.Length == 0)
            {
                error = "empty command";
                return false;
            }

            List<RadioStep> steps = new List<RadioStep>();
            int totalFrames = 0;
            int index = 0;

            while (index < command.Length)
            {
                if (steps.Count >= MaxCommandsPerMessage)
                {
                    error = "too many commands";
                    return false;
                }

                RadioStep step;
                int nextIndex;

                if (!TryParseStep(command, index, out step, out nextIndex, out error))
                {
                    return false;
                }


                totalFrames += step.Frames;

                if (totalFrames > MaxTotalFramesPerMessage)
                {
                    error = "program too long";
                    return false;
                }

                steps.Add(step);
                index = nextIndex;
            }

            program = new RadioProgram(steps, source);
            return true;
        }

        private static string Normalize(string text)
        {
            StringBuilder builder = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsWhiteSpace(c) || c == ',')
                {
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static bool TryParseStep(
            string command,
            int index,
            out RadioStep step,
            out int nextIndex,
            out string error
        )
        {
            step = null;
            nextIndex = index;
            error = null;

            char c = command[index];

            if (IsInputChar(c))
            {
                return TryParseInputStep(command, index, out step, out nextIndex, out error);
            }

            if (c == 'w')
            {
                int frames;

                if (!TryReadFrames(command, index + 1, DefaultFrames, MaxWaitFrames, out frames, out nextIndex))
                {
                    error = "too many frames: w";
                    return false;
                }

                step = new RadioStep("w", frames, false, false, false, false, false);
                return true;
            }

            if (c == 'o' || c == 'p')
            {
                nextIndex = index + 1;

                if (nextIndex < command.Length && char.IsDigit(command[nextIndex]))
                {
                    error = c + " does not take frames";
                    return false;
                }

                step = new RadioStep(c.ToString(), 1, false, false, false, c == 'p', c == 'o');
                return true;
            }

            error = "invalid command";
            return false;
        }

        private static bool TryParseInputStep(
            string command,
            int index,
            out RadioStep step,
            out int nextIndex,
            out string error
        )
        {
            step = null;
            nextIndex = index;
            error = null;

            int start = index;
            bool jump = false;
            bool left = false;
            bool right = false;

            while (nextIndex < command.Length && IsInputChar(command[nextIndex]))
            {
                char c = command[nextIndex];

                if (c == 'j')
                {
                    jump = true;
                }
                else if (c == 'l')
                {
                    left = true;
                }
                else if (c == 'r')
                {
                    right = true;
                }

                nextIndex++;
            }

            int frames;
            int nameEnd = nextIndex;
            int maxFrames = jump ? MaxJumpFrames : MaxMoveFrames;

            if (!TryReadFrames(command, nextIndex, DefaultFrames, maxFrames, out frames, out nextIndex))
            {
                error = "too many frames: " + command.Substring(start, nextIndex - start);
                return false;
            }

            if (jump && frames != DefaultFrames)
            {
                frames += SampleDiscreteLaplace(ModEntry.JumpFrameLaplaceAlpha);

                if (frames < 1)
                {
                    frames = 1;
                }
                else if (frames > maxFrames)
                {
                    frames = maxFrames;
                }
            }

            step = new RadioStep(command.Substring(start, nameEnd - start), frames, left, right, jump, false, false);
            return true;
        }

        private static bool TryReadFrames(
            string command,
            int index,
            int defaultFrames,
            int maxFrames,
            out int frames,
            out int nextIndex
        )
        {
            frames = defaultFrames;
            nextIndex = index;

            while (nextIndex < command.Length && char.IsDigit(command[nextIndex]))
            {
                nextIndex++;
            }

            if (nextIndex == index)
            {
                return true;
            }

            if (!int.TryParse(command.Substring(index, nextIndex - index), out frames) || frames <= 0)
            {
                return false;
            }

            return frames <= maxFrames;
        }

        private static bool IsInputChar(char c)
        {
            return c == 'j' || c == 'l' || c == 'r';
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
        private static bool _forceDisplay;

        public static bool HasDisplay
        {
            get
            {
                return _program != null ||
                    (MessageSeconds > 0f && !string.IsNullOrEmpty(DisplayText));
            }
        }

        public static bool IsRunning
        {
            get { return _program != null; }
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
            _forceDisplay = false;
            DisplayText = "Radio " + _program.StepIndex + "/" + _program.StepCount + ": " + _program.Status;
            MessageSeconds = 1.2f;

            _program.AdvanceOneFrame();

            if (_program.IsComplete)
            {
                _forceDisplay = false;
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
            _forceDisplay = false;
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
                if (ShouldShowReject(error))
                {
                    _forceDisplay = true;
                    DisplayText = "Radio rejected: " + error;
                    MessageSeconds = 4f;
                }

                return;
            }

            _program = parsed;
            _forceDisplay = false;
            DisplayText = "Radio start: " + parsed.Source;
            MessageSeconds = 2f;
        }

        private static bool ShouldShowReject(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return false;
            }

            return error.StartsWith("too many", StringComparison.Ordinal) ||
                error.StartsWith("program too long", StringComparison.Ordinal) ||
                error.EndsWith("does not take frames", StringComparison.Ordinal);
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

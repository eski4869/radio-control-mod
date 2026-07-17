using System;
using System.Collections.Generic;
using System.Reflection;
using EntityComponent;
using HarmonyLib;
using JumpKing;
using JumpKing.Controller;
using JumpKing.Mods;
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

        private static Harmony _harmony;

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            EnsurePatched();
            BrokerCommandClient.Register(CommandTarget);
            RadioControlOverlay.EnsureAdded();
        }

        private static void EnsurePatched()
        {
            if (_harmony != null)
            {
                return;
            }

            try
            {
                MethodInfo original = AccessTools.Method(
                    "JumpKing.Controller.KeyboardPad:GetPressedButtons"
                );
                MethodInfo postfix = AccessTools.Method(
                    typeof(KeyboardPadGetPressedButtonsPatch),
                    "Postfix"
                );

                if (original == null || postfix == null)
                {
                    JumpKing.Program.crashLog.AddErrorMessage(
                        "RadioControl patch target not found."
                    );
                    return;
                }

                _harmony = new Harmony("eski4869.RadioControlMod");
                _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "RadioControl patch failed: " + ex.Message
                );
            }
        }
    }

    internal static class KeyboardPadGetPressedButtonsPatch
    {
        public static void Postfix(ref int[] __result)
        {
            RadioVirtualInput.AppendButtons(ref __result);
        }
    }

    internal static class RadioVirtualInput
    {
        private static bool _left;
        private static bool _right;
        private static bool _jump;

        public static void Set(bool left, bool right, bool jump)
        {
            _left = left;
            _right = right;
            _jump = jump;
        }

        public static void Clear()
        {
            _left = false;
            _right = false;
            _jump = false;
        }

        public static void AppendButtons(ref int[] buttons)
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

            List<int> merged = new List<int>(buttons ?? new int[0]);

            if (_left)
            {
                AddIfMissing(merged, (int)JKpadButtons.Left);
            }

            if (_right)
            {
                AddIfMissing(merged, (int)JKpadButtons.Right);
            }

            if (_jump)
            {
                AddIfMissing(merged, (int)JKpadButtons.Jump);
            }

            buttons = merged.ToArray();
        }

        private static void AddIfMissing(List<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return;
                }
            }

            values.Add(value);
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
        private const int MaxCommandsPerMessage = 20;
        private const int MaxFramesPerCommand = 600;
        private const int MaxTotalFramesPerMessage = 1800;

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

            if (token.Length <= prefixLength)
            {
                return false;
            }

            int frames;
            if (!int.TryParse(token.Substring(prefixLength), out frames) || frames <= 0)
            {
                return false;
            }

            step = new RadioStep(token.Substring(0, prefixLength), frames, left, right, jump);
            return true;
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

        public static void Update(float delta)
        {
            BrokerCommandClient.Register(ModEntry.CommandTarget);

            if (RadioGameState.IsPaused())
            {
                RadioVirtualInput.Clear();
                TickMessage(delta);
                return;
            }

            if (_program == null)
            {
                RadioVirtualInput.Clear();
                TryStartNextProgram();
            }

            if (_program == null)
            {
                TickMessage(delta);
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
            RadioControlRuntime.Update(delta);
        }

        public void ForegroundDraw()
        {
            if (!RadioControlRuntime.HasDisplay)
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

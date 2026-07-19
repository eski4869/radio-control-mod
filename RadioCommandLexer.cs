using System;
using System.Collections.Generic;

namespace RadioControlMod
{
    internal enum RadioCommandKind
    {
        Jump,
        JumpLeft,
        JumpRight,
        Left,
        Right,
        Wait,
        Snake,
        Boots
    }

    internal sealed class RadioCommandToken
    {
        public RadioCommandToken(RadioCommandKind kind, int? frames, string text)
        {
            Kind = kind;
            Frames = frames;
            Text = text;
        }

        public RadioCommandKind Kind { get; private set; }
        public int? Frames { get; private set; }
        public string Text { get; private set; }

        public string Name
        {
            get
            {
                switch (Kind)
                {
                    case RadioCommandKind.Jump:
                        return "j";
                    case RadioCommandKind.JumpLeft:
                        return "jl";
                    case RadioCommandKind.JumpRight:
                        return "jr";
                    case RadioCommandKind.Left:
                        return "l";
                    case RadioCommandKind.Right:
                        return "r";
                    case RadioCommandKind.Wait:
                        return "w";
                    case RadioCommandKind.Snake:
                        return "o";
                    case RadioCommandKind.Boots:
                        return "p";
                    default:
                        throw new InvalidOperationException("Unknown radio command kind.");
                }
            }
        }
    }

    internal static class RadioCommandLexer
    {
        public static bool TryTokenize(
            string text,
            out List<RadioCommandToken> tokens,
            out string error
        )
        {
            tokens = new List<RadioCommandToken>();
            error = null;

            string source = (text ?? string.Empty).Trim().ToLowerInvariant();
            int index = 0;

            while (true)
            {
                SkipSeparators(source, ref index);

                if (index >= source.Length)
                {
                    break;
                }

                RadioCommandToken token;
                if (!TryReadToken(source, ref index, out token, out error))
                {
                    return false;
                }

                tokens.Add(token);
            }

            return true;
        }

        private static bool TryReadToken(
            string source,
            ref int index,
            out RadioCommandToken token,
            out string error
        )
        {
            token = null;
            error = null;

            int start = index;
            char c = source[index];
            RadioCommandKind kind;

            if (c == 'j')
            {
                kind = ReadJumpKind(source, ref index);
            }
            else if (c == 'l')
            {
                kind = RadioCommandKind.Left;
                index++;
            }
            else if (c == 'r')
            {
                kind = RadioCommandKind.Right;
                index++;
            }
            else if (c == 'w')
            {
                kind = RadioCommandKind.Wait;
                index++;
            }
            else if (c == 'o' || c == 'p')
            {
                kind = c == 'o' ? RadioCommandKind.Snake : RadioCommandKind.Boots;
                index++;

                token = new RadioCommandToken(
                    kind,
                    null,
                    source.Substring(start, index - start)
                );
                return true;
            }
            else
            {
                error = "invalid command";
                return false;
            }

            int? frames;
            if (!TryReadFrames(source, start, ref index, out frames, out error))
            {
                return false;
            }

            token = new RadioCommandToken(
                kind,
                frames,
                source.Substring(start, index - start)
            );
            return true;
        }

        private static RadioCommandKind ReadJumpKind(string source, ref int index)
        {
            index++;

            if (index >= source.Length)
            {
                return RadioCommandKind.Jump;
            }

            if (source[index] == 'l')
            {
                index++;
                return RadioCommandKind.JumpLeft;
            }

            if (source[index] == 'r')
            {
                index++;
                return RadioCommandKind.JumpRight;
            }

            return RadioCommandKind.Jump;
        }

        private static bool TryReadFrames(
            string source,
            int tokenStart,
            ref int index,
            out int? frames,
            out string error
        )
        {
            frames = null;
            error = null;

            int numberStart = index;
            while (index < source.Length && char.IsDigit(source[index]))
            {
                index++;
            }

            if (index == numberStart)
            {
                return true;
            }

            int value;
            if (!int.TryParse(source.Substring(numberStart, index - numberStart), out value))
            {
                error = "too many frames: " + source.Substring(tokenStart, index - tokenStart);
                return false;
            }

            frames = value;
            return true;
        }

        private static void SkipSeparators(string source, ref int index)
        {
            while (index < source.Length)
            {
                char c = source[index];
                if (!char.IsWhiteSpace(c) && c != ',')
                {
                    return;
                }

                index++;
            }
        }
    }
}

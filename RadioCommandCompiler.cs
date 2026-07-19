using System;
using System.Collections.Generic;

namespace RadioControlMod
{
    internal sealed class RadioStep
    {
        public RadioStep(
            string name,
            int frames,
            bool left,
            bool right,
            bool jump,
            bool boots,
            bool snake
        )
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

    internal static class RadioCommandCompiler
    {
        private const int DefaultFrames = 35;
        private const int MaxCommandsPerMessage = 32;
        private const int MaxJumpFrames = 300;
        private const int MaxMoveFrames = 300;
        private const int MaxWaitFrames = 300;
        private const int MaxTotalFramesPerMessage = 1200;

        public static bool TryCompile(
            IList<RadioCommandToken> tokens,
            double jumpFrameLaplaceAlpha,
            Func<double, int> sampleJumpVariance,
            out List<RadioStep> steps,
            out string error
        )
        {
            steps = new List<RadioStep>();
            error = null;

            if (tokens.Count > MaxCommandsPerMessage)
            {
                error = "command count must be 32 or fewer";
                return false;
            }

            int totalFrames = 0;

            for (int i = 0; i < tokens.Count; i++)
            {
                RadioCommandToken token = tokens[i];
                int frames = GetFrames(token);
                int maxFrames = GetMaxFrames(token.Kind);

                if (frames <= 0 || frames > maxFrames)
                {
                    error = "frames must be between 1 and " +
                        maxFrames +
                        ": " +
                        token.Text;
                    return false;
                }

                if (IsJump(token.Kind) && frames != DefaultFrames)
                {
                    frames += sampleJumpVariance(jumpFrameLaplaceAlpha);

                    if (frames < 1)
                    {
                        frames = 1;
                    }
                    else if (frames > maxFrames)
                    {
                        frames = maxFrames;
                    }
                }

                totalFrames += frames;

                if (totalFrames > MaxTotalFramesPerMessage)
                {
                    error = "total frames must be 1200 or fewer";
                    return false;
                }

                steps.Add(CreateStep(token, frames));
            }

            return true;
        }

        private static int GetFrames(RadioCommandToken token)
        {
            if (token.Kind == RadioCommandKind.Snake || token.Kind == RadioCommandKind.Boots)
            {
                return 1;
            }

            return token.Frames.HasValue ? token.Frames.Value : DefaultFrames;
        }

        private static int GetMaxFrames(RadioCommandKind kind)
        {
            if (IsJump(kind))
            {
                return MaxJumpFrames;
            }

            if (kind == RadioCommandKind.Left || kind == RadioCommandKind.Right)
            {
                return MaxMoveFrames;
            }

            if (kind == RadioCommandKind.Wait)
            {
                return MaxWaitFrames;
            }

            return 1;
        }

        private static bool IsJump(RadioCommandKind kind)
        {
            return kind == RadioCommandKind.Jump ||
                kind == RadioCommandKind.JumpLeft ||
                kind == RadioCommandKind.JumpRight;
        }

        private static RadioStep CreateStep(RadioCommandToken token, int frames)
        {
            RadioCommandKind kind = token.Kind;
            bool jump = IsJump(kind);
            bool left = kind == RadioCommandKind.JumpLeft || kind == RadioCommandKind.Left;
            bool right = kind == RadioCommandKind.JumpRight || kind == RadioCommandKind.Right;
            bool boots = kind == RadioCommandKind.Boots;
            bool snake = kind == RadioCommandKind.Snake;

            return new RadioStep(token.Name, frames, left, right, jump, boots, snake);
        }
    }
}

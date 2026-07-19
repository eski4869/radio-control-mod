using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RadioControlMod.Tests
{
    [TestClass]
    public sealed class RadioCommandCompilerTests
    {
        [TestMethod]
        [DataRow("j")]
        [DataRow("jl")]
        [DataRow("jr")]
        [DataRow("l")]
        [DataRow("r")]
        [DataRow("w")]
        public void MissingFramesUseThirtyFive(string text)
        {
            List<RadioStep> steps = Compile(Token(Kind(text), null, text));

            Assert.HasCount(1, steps);
            Assert.AreEqual(35, steps[0].Frames);
        }

        [TestMethod]
        public void SnakeAndBootsCompileAsSingleFrameActions()
        {
            List<RadioStep> steps = Compile(
                Token(RadioCommandKind.Snake, null, "o"),
                Token(RadioCommandKind.Boots, null, "p")
            );

            Assert.HasCount(2, steps);
            Assert.AreEqual(1, steps[0].Frames);
            Assert.IsTrue(steps[0].Snake);
            Assert.AreEqual(1, steps[1].Frames);
            Assert.IsTrue(steps[1].Boots);
        }

        [TestMethod]
        [DataRow("j")]
        [DataRow("jl")]
        [DataRow("jr")]
        [DataRow("l")]
        [DataRow("r")]
        [DataRow("w")]
        public void OneAndThreeHundredFramesAreAccepted(string text)
        {
            RadioCommandKind kind = Kind(text);
            Assert.AreEqual(1, Compile(Token(kind, 1, text + "1"))[0].Frames);
            Assert.AreEqual(300, Compile(Token(kind, 300, text + "300"))[0].Frames);
        }

        [TestMethod]
        [DataRow("j")]
        [DataRow("l")]
        [DataRow("w")]
        public void ZeroFramesAreRejected(string text)
        {
            AssertRejected(
                new[] { Token(Kind(text), 0, text + "0") },
                "frames must be between 1 and 300: " + text + "0"
            );
        }

        [TestMethod]
        [DataRow("j")]
        [DataRow("l")]
        [DataRow("w")]
        public void ThreeHundredOneFramesAreRejected(string text)
        {
            AssertRejected(
                new[] { Token(Kind(text), 301, text + "301") },
                "frames must be between 1 and 300: " + text + "301"
            );
        }

        [TestMethod]
        public void ThirtyTwoCommandsAreAccepted()
        {
            List<RadioCommandToken> tokens = Repeat(
                Token(RadioCommandKind.Snake, null, "o"),
                32
            );

            List<RadioStep> steps = Compile(tokens);

            Assert.HasCount(32, steps);
        }

        [TestMethod]
        public void ThirtyThreeCommandsAreRejected()
        {
            AssertRejected(
                Repeat(Token(RadioCommandKind.Snake, null, "o"), 33),
                "command count must be 32 or fewer"
            );
        }

        [TestMethod]
        public void TwelveHundredTotalFramesAreAccepted()
        {
            List<RadioStep> steps = Compile(
                Token(RadioCommandKind.Wait, 300, "w300"),
                Token(RadioCommandKind.Wait, 300, "w300"),
                Token(RadioCommandKind.Wait, 300, "w300"),
                Token(RadioCommandKind.Wait, 300, "w300")
            );

            Assert.HasCount(4, steps);
        }

        [TestMethod]
        public void MoreThanTwelveHundredTotalFramesAreRejected()
        {
            AssertRejected(
                new[]
                {
                    Token(RadioCommandKind.Wait, 300, "w300"),
                    Token(RadioCommandKind.Wait, 300, "w300"),
                    Token(RadioCommandKind.Wait, 300, "w300"),
                    Token(RadioCommandKind.Wait, 300, "w300"),
                    Token(RadioCommandKind.Snake, null, "o")
                },
                "total frames must be 1200 or fewer"
            );
        }

        [TestMethod]
        public void ThirtyFiveFrameJumpDoesNotSampleVariance()
        {
            List<RadioStep> steps;
            string error;

            bool compiled = RadioCommandCompiler.TryCompile(
                new[] { Token(RadioCommandKind.Jump, 35, "j35") },
                0.1,
                delegate(double alpha)
                {
                    Assert.Fail("The exact 35-frame jump must not sample variance.");
                    return 0;
                },
                out steps,
                out error
            );

            Assert.IsTrue(compiled, error);
            Assert.AreEqual(35, steps[0].Frames);
        }

        [TestMethod]
        public void NonDefaultJumpAppliesInjectedVariance()
        {
            List<RadioStep> steps = CompileWithVariance(
                2,
                Token(RadioCommandKind.JumpRight, 34, "jr34")
            );

            Assert.AreEqual(36, steps[0].Frames);
        }

        [TestMethod]
        public void JumpVarianceIsClampedToValidRange()
        {
            Assert.AreEqual(
                1,
                CompileWithVariance(-1000, Token(RadioCommandKind.Jump, 1, "j1"))[0].Frames
            );
            Assert.AreEqual(
                300,
                CompileWithVariance(1000, Token(RadioCommandKind.Jump, 300, "j300"))[0].Frames
            );
        }

        [TestMethod]
        public void TokensCompileToExpectedInputFlags()
        {
            AssertFlags(RadioCommandKind.Jump, false, false, true, false, false);
            AssertFlags(RadioCommandKind.JumpLeft, true, false, true, false, false);
            AssertFlags(RadioCommandKind.JumpRight, false, true, true, false, false);
            AssertFlags(RadioCommandKind.Left, true, false, false, false, false);
            AssertFlags(RadioCommandKind.Right, false, true, false, false, false);
            AssertFlags(RadioCommandKind.Wait, false, false, false, false, false);
            AssertFlags(RadioCommandKind.Snake, false, false, false, false, true);
            AssertFlags(RadioCommandKind.Boots, false, false, false, true, false);
        }

        private static void AssertFlags(
            RadioCommandKind kind,
            bool left,
            bool right,
            bool jump,
            bool boots,
            bool snake
        )
        {
            RadioStep step = Compile(Token(kind, null, Name(kind)))[0];

            Assert.AreEqual(left, step.Left);
            Assert.AreEqual(right, step.Right);
            Assert.AreEqual(jump, step.Jump);
            Assert.AreEqual(boots, step.Boots);
            Assert.AreEqual(snake, step.Snake);
        }

        private static List<RadioStep> Compile(params RadioCommandToken[] tokens)
        {
            return Compile((IList<RadioCommandToken>)tokens);
        }

        private static List<RadioStep> Compile(IList<RadioCommandToken> tokens)
        {
            return CompileWithVariance(0, tokens);
        }

        private static List<RadioStep> CompileWithVariance(
            int variance,
            params RadioCommandToken[] tokens
        )
        {
            return CompileWithVariance(variance, (IList<RadioCommandToken>)tokens);
        }

        private static List<RadioStep> CompileWithVariance(
            int variance,
            IList<RadioCommandToken> tokens
        )
        {
            List<RadioStep> steps;
            string error;

            bool compiled = RadioCommandCompiler.TryCompile(
                tokens,
                0.1,
                delegate(double alpha) { return variance; },
                out steps,
                out error
            );

            Assert.IsTrue(compiled, error);
            return steps;
        }

        private static void AssertRejected(
            IList<RadioCommandToken> tokens,
            string expectedError
        )
        {
            List<RadioStep> steps;
            string error;

            bool compiled = RadioCommandCompiler.TryCompile(
                tokens,
                0.1,
                delegate(double alpha) { return 0; },
                out steps,
                out error
            );

            Assert.IsFalse(compiled);
            Assert.AreEqual(expectedError, error);
        }

        private static RadioCommandToken Token(
            RadioCommandKind kind,
            int? frames,
            string text
        )
        {
            return new RadioCommandToken(kind, frames, text);
        }

        private static List<RadioCommandToken> Repeat(
            RadioCommandToken token,
            int count
        )
        {
            List<RadioCommandToken> tokens = new List<RadioCommandToken>();

            for (int i = 0; i < count; i++)
            {
                tokens.Add(token);
            }

            return tokens;
        }

        private static string Name(RadioCommandKind kind)
        {
            return new RadioCommandToken(kind, null, string.Empty).Name;
        }

        private static RadioCommandKind Kind(string name)
        {
            switch (name)
            {
                case "j":
                    return RadioCommandKind.Jump;
                case "jl":
                    return RadioCommandKind.JumpLeft;
                case "jr":
                    return RadioCommandKind.JumpRight;
                case "l":
                    return RadioCommandKind.Left;
                case "r":
                    return RadioCommandKind.Right;
                case "w":
                    return RadioCommandKind.Wait;
                default:
                    throw new ArgumentOutOfRangeException("name");
            }
        }
    }
}

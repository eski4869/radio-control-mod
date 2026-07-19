using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RadioControlMod.Tests
{
    [TestClass]
    public sealed class RadioProgramTests
    {
        [TestMethod]
        public void EmptyProgramIsAlreadyComplete()
        {
            RadioProgram program = Program("empty");

            Assert.IsTrue(program.IsComplete);
            Assert.IsFalse(program.IsReleasing);
            Assert.IsNull(program.ActiveStep);
            Assert.AreEqual(0, program.StepCount);
            Assert.AreEqual(0, program.RemainingFrames);
            Assert.AreEqual("Done", program.Status);
        }

        [TestMethod]
        public void ConstructorExposesInitialProgramState()
        {
            RadioProgram program = Program(
                "r2 j1",
                Step("r", 2, right: true),
                Step("j", 1, jump: true)
            );

            Assert.AreEqual("r2 j1", program.Source);
            Assert.AreEqual(1, program.StepIndex);
            Assert.AreEqual(2, program.StepCount);
            Assert.AreEqual(2, program.RemainingFrames);
            Assert.AreEqual("r", program.ActiveStep.Name);
            Assert.AreEqual("r 2f", program.Status);
            Assert.IsFalse(program.IsComplete);
            Assert.IsFalse(program.IsReleasing);
        }

        [TestMethod]
        public void OneFrameStepRunsOnceThenReleasesOnce()
        {
            RadioProgram program = Program("r1", Step("r", 1, right: true));

            CollectionAssert.AreEqual(
                new[] { "r", "release" },
                RunToCompletion(program)
            );
        }

        [TestMethod]
        public void MultiFrameStepRunsForItsExactFrameCount()
        {
            RadioProgram program = Program("r3", Step("r", 3, right: true));

            CollectionAssert.AreEqual(
                new[] { "r", "r", "r", "release" },
                RunToCompletion(program)
            );
        }

        [TestMethod]
        public void MultipleStepsKeepTheirOrderAndEachReleaseOnce()
        {
            RadioProgram program = Program(
                "r2 j2",
                Step("r", 2, right: true),
                Step("j", 2, jump: true)
            );

            CollectionAssert.AreEqual(
                new[] { "r", "r", "release", "j", "j", "release" },
                RunToCompletion(program)
            );
        }

        [TestMethod]
        public void EveryCommandKindReceivesAReleaseFrame()
        {
            RadioProgram program = Program(
                "all",
                Step("j", 1, jump: true),
                Step("jl", 1, left: true, jump: true),
                Step("jr", 1, right: true, jump: true),
                Step("l", 1, left: true),
                Step("r", 1, right: true),
                Step("w", 1),
                Step("o", 1, snake: true),
                Step("p", 1, boots: true)
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    "j", "release",
                    "jl", "release",
                    "jr", "release",
                    "l", "release",
                    "r", "release",
                    "w", "release",
                    "o", "release",
                    "p", "release"
                },
                RunToCompletion(program)
            );
        }

        [TestMethod]
        public void ReleaseFrameHasNoActiveStep()
        {
            RadioProgram program = Program("r1", Step("r", 1, right: true));

            Assert.IsNotNull(program.ActiveStep);
            program.AdvanceOneFrame();

            Assert.IsTrue(program.IsReleasing);
            Assert.IsNull(program.ActiveStep);
            Assert.AreEqual("release 1f", program.Status);
        }

        [TestMethod]
        public void NextStepStartsAfterTheReleaseFrame()
        {
            RadioProgram program = Program(
                "r1 j1",
                Step("r", 1, right: true),
                Step("j", 1, jump: true)
            );

            program.AdvanceOneFrame();
            Assert.IsTrue(program.IsReleasing);

            program.AdvanceOneFrame();
            Assert.IsFalse(program.IsReleasing);
            Assert.AreEqual(2, program.StepIndex);
            Assert.AreEqual(1, program.RemainingFrames);
            Assert.AreEqual("j", program.ActiveStep.Name);
        }

        [TestMethod]
        public void StatusAndRemainingFramesFollowEachTransition()
        {
            RadioProgram program = Program("r2", Step("r", 2, right: true));

            Assert.AreEqual(2, program.RemainingFrames);
            Assert.AreEqual("r 2f", program.Status);

            program.AdvanceOneFrame();
            Assert.AreEqual(1, program.RemainingFrames);
            Assert.AreEqual("r 1f", program.Status);

            program.AdvanceOneFrame();
            Assert.IsTrue(program.IsReleasing);
            Assert.AreEqual("release 1f", program.Status);

            program.AdvanceOneFrame();
            Assert.IsTrue(program.IsComplete);
            Assert.AreEqual("Done", program.Status);
        }

        [TestMethod]
        public void AdvancingACompletedProgramDoesNothing()
        {
            RadioProgram program = Program("r1", Step("r", 1, right: true));
            RunToCompletion(program);

            program.AdvanceOneFrame();
            program.AdvanceOneFrame();

            Assert.IsTrue(program.IsComplete);
            Assert.IsFalse(program.IsReleasing);
            Assert.IsNull(program.ActiveStep);
            Assert.AreEqual("Done", program.Status);
        }

        private static string[] RunToCompletion(RadioProgram program)
        {
            List<string> frames = new List<string>();

            while (!program.IsComplete)
            {
                frames.Add(program.IsReleasing ? "release" : program.ActiveStep.Name);
                program.AdvanceOneFrame();
            }

            return frames.ToArray();
        }

        private static RadioProgram Program(string source, params RadioStep[] steps)
        {
            return new RadioProgram(new List<RadioStep>(steps), source);
        }

        private static RadioStep Step(
            string name,
            int frames,
            bool left = false,
            bool right = false,
            bool jump = false,
            bool boots = false,
            bool snake = false
        )
        {
            return new RadioStep(name, frames, left, right, jump, boots, snake);
        }
    }
}

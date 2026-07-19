using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RadioControlMod.Tests
{
    [TestClass]
    public sealed class RadioCommandLexerTests
    {
        [TestMethod]
        [DataRow("jrl", "jr|l")]
        [DataRow("jlr", "jl|r")]
        [DataRow("jrr", "jr|r")]
        [DataRow("jll", "jl|l")]
        public void JumpConsumesAtMostOneDirection(string input, string expected)
        {
            AssertTokens(input, expected);
        }

        [TestMethod]
        public void CommaSeparatesJumpFromDirection()
        {
            AssertTokens("jr", "jr");
            AssertTokens("j,r", "j|r");
        }

        [TestMethod]
        [DataRow("j r l", "j|r|l")]
        [DataRow("j,r,l", "j|r|l")]
        [DataRow("jr l", "jr|l")]
        [DataRow("j,rl", "j|r|l")]
        public void SeparatorsEndTheCurrentCommand(string input, string expected)
        {
            AssertTokens(input, expected);
        }

        [TestMethod]
        [DataRow("jr35l20", "jr35|l20")]
        [DataRow("j35r35", "j35|r35")]
        [DataRow("jrl35", "jr|l35")]
        [DataRow("jlr35", "jl|r35")]
        public void FramesBelongToTheImmediatelyPrecedingCommand(string input, string expected)
        {
            AssertTokens(input, expected);
        }

        [TestMethod]
        [DataRow("jr, l20, w5, o, p,", "jr|l20|w5|o|p")]
        [DataRow("JR35,W10,P", "jr35|w10|p")]
        [DataRow(",,  jr35  ,,  l20  ,,", "jr35|l20")]
        public void WhitespaceCommasAndCaseAreHandled(string input, string expected)
        {
            AssertTokens(input, expected);
        }

        [TestMethod]
        [DataRow("o35", "o")]
        [DataRow("p1", "p")]
        public void SingleCharacterCommandsFinishBeforeFollowingDigits(
            string input,
            string completedToken
        )
        {
            List<RadioCommandToken> tokens;
            string error;

            bool parsed = RadioCommandLexer.TryTokenize(input, out tokens, out error);

            Assert.IsFalse(parsed);
            Assert.AreEqual("invalid command", error);
            Assert.AreEqual(completedToken, string.Join("|", tokens.Select(token => token.Text)));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(",,,")]
        [DataRow(" , , ")]
        public void EmptyOrSeparatorOnlyInputProducesNoTokens(string input)
        {
            List<RadioCommandToken> tokens;
            string error;

            bool parsed = RadioCommandLexer.TryTokenize(input, out tokens, out error);

            Assert.IsTrue(parsed, error);
            Assert.IsEmpty(tokens);
            Assert.IsNull(error);
        }

        [TestMethod]
        [DataRow("35j", "invalid command")]
        [DataRow("j,35", "invalid command")]
        [DataRow("hello", "invalid command")]
        public void InvalidInputIsRejected(string input, string expectedError)
        {
            List<RadioCommandToken> tokens;
            string error;

            bool parsed = RadioCommandLexer.TryTokenize(input, out tokens, out error);

            Assert.IsFalse(parsed);
            Assert.AreEqual(expectedError, error);
        }

        private static void AssertTokens(string input, string expected)
        {
            List<RadioCommandToken> tokens;
            string error;

            bool parsed = RadioCommandLexer.TryTokenize(input, out tokens, out error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual(expected, string.Join("|", tokens.Select(token => token.Text)));
        }
    }
}

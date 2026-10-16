using NUnit.Framework;
using WalkGame.Application.ReadModels;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-125: PlayerCopy.Title treats any non-blank title as text, dots included. Unity-editor-tested.</summary>
    public sealed class TitleTypingShellTests
    {
        [Test]
        public void ADottedTitle_IsText()
        {
            Assert.AreEqual("St. Mary's Weir", PlayerCopy.Title("St. Mary's Weir"));
            Assert.AreEqual("Nr. 2 Mill", PlayerCopy.Title("Nr. 2 Mill"));
        }

        [Test]
        public void ABlankTitle_IsTheUntitledPhrase()
        {
            Assert.AreEqual(GlancePhrases.UntitledProject, PlayerCopy.Title(""));
            Assert.AreEqual(GlancePhrases.UntitledProject, PlayerCopy.Title("   "));
            Assert.AreEqual(GlancePhrases.UntitledProject, PlayerCopy.Title(null));
        }
    }
}

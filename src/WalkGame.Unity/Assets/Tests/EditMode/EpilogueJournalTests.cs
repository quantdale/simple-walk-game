using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-129 Region 1 epilogue in the Story journal. Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class EpilogueJournalTests
    {
        private static readonly string[] Pressure = { "missed", "streak", "hurry", "don't forget", "of 8", "collect", "region 2", "next region", "coming soon" };

        [Test] public void No_epilogue_lines_before_completion()
        {
            Assert.IsEmpty(ShellController.EpilogueLines(EpilogueReadModel.None));
            Assert.IsEmpty(ShellController.EpilogueLines(null!));
        }

        [Test] public void A_new_game_story_shows_no_epilogue()
        {
            var dir = Path.Combine(Path.GetTempPath(), "wg-epi-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir), () => 42UL, devTools: true);
                c.Boot(); c.Invoke("start"); c.Invoke("nav:Story");
                Assert.IsFalse(c.Render().Lines.Any(l => l.StartsWith("Epilogue") || l.StartsWith("In the valley now")));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test] public void After_completion_the_journal_shows_entry_letters_and_the_current_note_calmly()
        {
            var notes = Region1Epilogue.ValleyNotes.Take(3).Select(n => n.Body).ToList();
            var e = new EpilogueReadModel(true, Region1Epilogue.JournalEntry.Body,
                Region1Epilogue.ClosingLetters.Select(l => l.Body).ToList(), notes, notes[2]);
            var lines = ShellController.EpilogueLines(e);
            Assert.AreEqual("Epilogue · Millbrook, whole again", lines[0]);
            Assert.AreEqual(Region1Epilogue.JournalEntry.Body, lines[1]);
            Assert.AreEqual(2, lines.Count(l => l.StartsWith("A letter from Nell Hartley: ")));
            Assert.AreEqual(1, lines.Count(l => l == "In the valley now: " + notes[2]));
            Assert.AreEqual(1, lines.Count(l => l == notes[0]), "earlier notes stay readable");
            Assert.IsFalse(lines.Contains(notes[2]), "the current note is not repeated");
            foreach (var l in lines) foreach (var bad in Pressure) StringAssert.DoesNotContain(bad, l.ToLowerInvariant());
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-198: the welcome-back line is calm, localized, and never shaming.</summary>
    public sealed class LongAbsenceWelcomeTests
    {
        [TearDown] public void ResetLocale() => PlayerStrings.Locale = ContentText.English;

        [Test] public void No_line_for_a_short_gap()
        {
            Assert.IsNull(ShellController.AbsenceLine(AbsenceTier.None));
        }

        [Test] public void Each_tier_has_its_own_reassuring_line()
        {
            var days = ShellController.AbsenceLine(AbsenceTier.FewDays);
            var lng = ShellController.AbsenceLine(AbsenceTier.Long);
            Assert.IsNotNull(days); Assert.IsNotNull(lng);
            Assert.AreNotEqual(days, lng);
            StringAssert.Contains("Everything you restored is still here", days);
            StringAssert.Contains("Everything you restored is still here", lng);
        }

        [Test] public void Lines_never_shame_or_threaten_loss()
        {
            var banned = new[] { "miss", "lost", "lose", "neglect", "streak", "abandon", "forgot", "decay", "wither", "too long", "finally", "late" };
            foreach (var tier in new[] { AbsenceTier.FewDays, AbsenceTier.Long })
            {
                var line = ShellController.AbsenceLine(tier)!.ToLowerInvariant();
                foreach (var word in banned) StringAssert.DoesNotContain(word, line, tier + ": " + word);
            }
        }

        [Test] public void Lines_follow_the_locale()
        {
            var k1 = ContentText.Key(PlayerStrings.OverlayId("shell.welcomeBack.days"), PlayerStrings.Field);
            var k2 = ContentText.Key(PlayerStrings.OverlayId("shell.welcomeBack.long"), PlayerStrings.Field);
            PlayerStrings.Locale = ContentText.ForLocale("xx", new Dictionary<string, string> { [k1] = "[xx] days", [k2] = "[xx] long" });
            Assert.AreEqual("[xx] days", ShellController.AbsenceLine(AbsenceTier.FewDays));
            Assert.AreEqual("[xx] long", ShellController.AbsenceLine(AbsenceTier.Long));
        }
    }
}

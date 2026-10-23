using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Domain.Regions;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-215: the text shell in direction A — OFL fonts, painted world behind a compact card, world-first boot. Unity-editor-tested.</summary>
    public sealed class ShellDirectionATests
    {
        [Test] public void Fraunces_and_Nunito_ship_and_are_used()
        {
            Assert.IsNotNull(ShellView.DisplayFont, "Fonts/Fraunces-SemiBold"); Assert.IsNotNull(ShellView.BodyFont, "Fonts/Nunito-Medium"); Assert.IsNotNull(ShellView.ButtonFont, "Fonts/Nunito-ExtraBold");
            var root = new VisualElement();
            var m = new ScreenModel(ShellScreen.Home, ScreenState.Normal, "Home");
            m.Lines.Add("line"); m.Actions.Add(new ShellAction("nav:Projects", "Projects"));
            ShellView.Render(root, m, _ => { }, 1f);
            Assert.AreEqual(ShellView.DisplayFont, root.Q<Label>("title").style.unityFont.value);
            Assert.AreEqual(ShellView.ButtonFont, root.Q<ShellView.ActionButton>().style.unityFont.value);
        }

        [Test] public void With_a_backdrop_the_world_dominates_and_the_card_is_compact()
        {
            var tex = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + TrailheadArt.BackdropFor(RestorationStage.Restored));
            Assert.IsNotNull(tex);
            var prev = ShellView.Backdrop; ShellView.Backdrop = tex;
            try
            {
                var root = new VisualElement();
                var m = new ScreenModel(ShellScreen.Home, ScreenState.Normal, "Home");
                ShellView.Render(root, m, _ => { }, 2f);
                Assert.AreEqual(tex, root.style.backgroundImage.value.texture);
                Assert.AreEqual(ShellView.Background, root.style.backgroundColor.value, "audited page colour unchanged");
                var card = root.Q<ScrollView>("content");
                Assert.AreEqual(LengthUnit.Percent, card.style.maxHeight.value.unit);
                Assert.LessOrEqual(card.style.maxHeight.value.value, 60f, "the painted world keeps ≥ 40 % of the screen");
                Assert.AreEqual(ShellView.Background, card.style.backgroundColor.value, "text sits on the audited canvas, not on the painting");
            }
            finally { ShellView.Backdrop = prev; }
        }

        [Test] public void Backdrop_follows_the_trailhead_stage()
        {
            Assert.AreEqual(TrailheadArt.GroundForgotten, TrailheadArt.BackdropFor(null));
            Assert.AreEqual(TrailheadArt.GroundForgotten, TrailheadArt.BackdropFor(RestorationStage.Stabilized));
            Assert.AreEqual(TrailheadArt.GroundFlourishing, TrailheadArt.BackdropFor(RestorationStage.Functional));
        }

        [Test] public void The_app_opens_on_the_world_once_and_never_under_a_screen_reader()
        {
            Assert.IsTrue(TrailheadArt.OpenWorldFirst(true, false, false));
            Assert.IsFalse(TrailheadArt.OpenWorldFirst(true, true, false), "screen reader: the accessible text shell is the entry point");
            Assert.IsFalse(TrailheadArt.OpenWorldFirst(true, false, true), "only once per launch: Home stays reachable");
            Assert.IsFalse(TrailheadArt.OpenWorldFirst(false, false, false), "onboarding / return summary first");
        }
    }
}

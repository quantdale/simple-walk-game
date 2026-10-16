using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>Settings text-size preview (B3, D-083). Unity-editor-tested; device UNVERIFIED (KI-002).</summary>
    public sealed class TextSizePreviewTests
    {
        private string _dir = "";
        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "wg-text-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private ShellController Settings(Func<float> scale)
        {
            var c = new ShellController(ShellBootstrap.CreateSession(_dir), () => 42UL, devTools: true);
            c.Boot(); c.Invoke("start");
            c.TextScaleSource = scale;
            c.Invoke("nav:Settings");
            return c;
        }

        [Test] public void Settings_shows_the_effective_text_size_and_a_preview()
        {
            var m = Settings(() => 1.3f).Render();
            Assert.IsTrue(m.Lines.Contains("Text size: 130%. It follows your phone's font size setting."));
            Assert.AreEqual(ShellController.TextPreviewSample, m.TextPreview);
            var outline = AccessibleOutline.Build(m);
            int preview = outline.FindIndex(i => i.Label == ShellController.TextPreviewSample);
            Assert.Greater(preview, 0);
            Assert.Less(preview, outline.FindIndex(i => i.Role == AccessibleRole.Button), "preview is read before the actions");
        }

        [TestCase(0.5f, "Text size: 100%.")]
        [TestCase(float.NaN, "Text size: 100%.")]
        [TestCase(float.NegativeInfinity, "Text size: 100%.")]
        [TestCase(3.5f, "Text size: 200%, the largest this game supports.")]
        [TestCase(float.PositiveInfinity, "Text size: 200%, the largest this game supports.")]
        [TestCase(2f, "Text size: 200%.")]
        public void Hostile_or_out_of_range_scales_are_clamped(float raw, string prefix)
        {
            StringAssert.StartsWith(prefix, ShellController.TextSizeLine(raw));
        }

        [Test] public void A_throwing_scale_source_never_breaks_settings()
        {
            var m = Settings(() => throw new InvalidOperationException("no accessibility service")).Render();
            Assert.AreEqual(ScreenState.Normal, m.State);
            Assert.IsTrue(m.Lines.Any(l => l.StartsWith("Text size: 100%.")));
        }

        [Test] public void The_preview_renders_at_the_effective_scale_and_only_on_settings()
        {
            var c = Settings(() => 1.5f);
            var root = new VisualElement();
            ShellView.Render(root, c.Render(), _ => { }, 1.5f);
            var preview = root.Q<Label>("text-preview");
            Assert.IsNotNull(preview);
            Assert.AreEqual(ShellView.BodySize * 1.5f, preview.style.fontSize.value.value, 1e-3f);
            c.Invoke("nav:Home");
            ShellView.Render(root, c.Render(), _ => { }, 1.5f);
            Assert.IsNull(root.Q<Label>("text-preview"));
        }
    }
}

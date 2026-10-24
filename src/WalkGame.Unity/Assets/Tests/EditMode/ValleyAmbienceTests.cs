using System;
using System.Collections.Generic;
using NUnit.Framework;
using WalkGame.Domain.Regions;
using WalkGame.Shell;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-101: restoration ambience (footpaths, flowers, birds) is derived, bounded and static. Unity-editor-tested.</summary>
    public sealed class ValleyAmbienceTests
    {
        private static List<LandmarkVisual> Row(params RestorationStage[] stages)
        {
            var l = new List<LandmarkVisual>();
            for (int i = 0; i < stages.Length; i++) l.Add(new LandmarkVisual("lm.test-" + i, "L" + i, stages[i], (i + 0.5f) / stages.Length, 0.6f));
            return l;
        }

        [Test] public void Fresh_valley_has_no_ambience()
        {
            var a = RegionScene.Ambient(Row(RestorationStage.Ruined, RestorationStage.Ruined, RestorationStage.Ruined), 0f);
            Assert.AreEqual((0, 0, 0), a);
        }

        [Test] public void Footpaths_link_only_recovered_neighbours()
        {
            var a = RegionScene.Ambient(Row(RestorationStage.Stabilized, RestorationStage.Functional, RestorationStage.Ruined, RestorationStage.Restored), 0.2f);
            Assert.AreEqual(1, a.Trail);
        }

        [Test] public void Ambience_grows_monotonically_with_greenness()
        {
            var full = Row(RestorationStage.Flourishing, RestorationStage.Flourishing);
            int lastF = -1, lastB = -1;
            for (float g = 0f; g <= 1.0001f; g += 0.05f)
            {
                var a = RegionScene.Ambient(full, g);
                Assert.That(a.Flowers, Is.GreaterThanOrEqualTo(lastF)); Assert.That(a.Birds, Is.GreaterThanOrEqualTo(lastB));
                lastF = a.Flowers; lastB = a.Birds;
            }
            Assert.AreEqual((1, RegionScene.MaxFlowers, RegionScene.MaxBirds), RegionScene.Ambient(full, 1f));
            Assert.AreEqual(1, RegionScene.Ambient(full, 0.01f).Flowers, "the first restoration already blooms");
        }

        [Test] public void Worst_case_stays_inside_the_phone_safe_budget()
        {
            var stages = new RestorationStage[RegionScene.MaxLandmarks];
            for (int i = 0; i < stages.Length; i++) stages[i] = RestorationStage.Flourishing;
            var a = RegionScene.Ambient(Row(stages), 5f);
            Assert.That(a.Trail + a.Flowers + a.Birds, Is.LessThanOrEqualTo(RegionScene.MaxAmbientShapes));
            Assert.That(RegionScene.MaxShapes, Is.LessThanOrEqualTo(100));
        }
    }
}

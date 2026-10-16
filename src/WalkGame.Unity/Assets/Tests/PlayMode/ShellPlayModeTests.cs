using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WalkGame.Shell;

namespace WalkGame.Shell.PlayModeTests
{
    /// <summary>Player-loop smoke: the shell boots and survives real frames (batchmode -nographics).</summary>
    public sealed class ShellPlayModeTests
    {
        [UnityTest] public IEnumerator Shell_boots_new_game_and_survives_frames()
        {
            var dir = Path.Combine(UnityEngine.Application.temporaryCachePath, "wg-pm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var c = new ShellController(ShellBootstrap.CreateSession(dir), () => 7UL, devTools: true);
                c.Boot();
                Assert.AreEqual(ShellScreen.Onboarding, c.Current);
                yield return null;
                c.Invoke("start");
                yield return null;
                Assert.AreEqual(ShellScreen.Guide, c.Current);
                for (int i = 0; i < 3; i++) yield return null;
                Assert.Greater(Time.frameCount, 0);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}

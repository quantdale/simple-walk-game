using System;

namespace WalkGame.Shell
{
    /// <summary>D-104: the only moments that may make a sound. Everything else stays silent.</summary>
    public enum SoundCue { None, Restoration, Chapter, Celebration }

    /// <summary>
    /// D-104 calm, optional sound cues. Pure and engine-free so every rule is testable headless:
    /// a cue exists only for a return summary that shows real change, only when the player's
    /// sound setting is on, and plays once per arrival on that screen (never on redraws).
    /// Samples are synthesised (soft sine tones, no audio assets, so nothing to license).
    /// </summary>
    public static class SoundCues
    {
        public const int SampleRate = 22050;

        /// <summary>Peak amplitude: quiet by design (about -14 dBFS).</summary>
        public const float Peak = 0.2f;

        /// <summary>Which cue a return summary earns. Celebration outranks a chapter, which outranks a restoration.</summary>
        public static SoundCue ForSummary(bool soundEnabled, bool celebrating, bool newChapter, bool restorationMoments)
        {
            if (!soundEnabled) return SoundCue.None;
            if (celebrating) return SoundCue.Celebration;
            if (newChapter) return SoundCue.Chapter;
            return restorationMoments ? SoundCue.Restoration : SoundCue.None;
        }

        /// <summary>Plays only on arriving at a screen, so a redraw or a text-size change never repeats it.</summary>
        public static bool ShouldPlay(ShellScreen? previousScreen, ScreenModel model) =>
            model != null && model.Cue != SoundCue.None && previousScreen != model.Screen;

        /// <summary>Notes (Hz) of each cue: rising, consonant, unhurried.</summary>
        public static double[] Notes(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.Restoration: return new[] { 523.25, 659.25 };          // C5 E5
                case SoundCue.Chapter: return new[] { 392.00, 493.88, 587.33 };      // G4 B4 D5
                case SoundCue.Celebration: return new[] { 523.25, 659.25, 783.99, 1046.5 }; // C5 E5 G5 C6
                default: return Array.Empty<double>();
            }
        }

        /// <summary>
        /// Mono samples: each note 0.28 s, overlapping by half, with a smooth (raised-cosine)
        /// attack and decay so there are no clicks. Ends at exactly zero. Deterministic.
        /// </summary>
        public static float[] Samples(SoundCue cue)
        {
            var notes = Notes(cue);
            if (notes.Length == 0) return Array.Empty<float>();
            int noteLen = (int)(SampleRate * 0.28);
            int step = noteLen / 2;
            int total = step * (notes.Length - 1) + noteLen;
            var buf = new float[total];
            for (int n = 0; n < notes.Length; n++)
            {
                int start = n * step;
                for (int i = 0; i < noteLen; i++)
                {
                    double t = (double)i / SampleRate;
                    double env = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (noteLen - 1)); // 0 → 1 → 0
                    buf[start + i] += (float)(Math.Sin(2.0 * Math.PI * notes[n] * t) * env * Peak * 0.7);
                }
            }
            // Overlap can only sum two notes; clamp anyway so Peak is a hard ceiling.
            for (int i = 0; i < total; i++)
                buf[i] = Math.Max(-Peak, Math.Min(Peak, buf[i]));
            buf[total - 1] = 0f;
            return buf;
        }
    }
}

using System;
using WalkGame.Application;

namespace WalkGame.Shell
{
    /// <summary>
    /// Maps every <see cref="StartStatus"/> to a screen. An unreadable, invalid or
    /// newer-content save routes to a non-destructive error: the shell NEVER starts a new
    /// game silently over a save it could not read (KI-009 lesson, D-052).
    /// </summary>
    public static class BootRouter
    {
        /// <summary>
        /// D-182: like <see cref="Route(StartStatus)"/>, but an unreadable save whose last decode
        /// failure was a NEWER schema says "update the game" instead of "could not be read".
        /// Both fail closed and write nothing; only the advice differs.
        /// </summary>
        public static (ShellScreen Screen, string? Notice) Route(StartStatus status, CodecFailureCategory lastCodecFailure)
        {
            if (status == StartStatus.SaveUnreadable && lastCodecFailure == CodecFailureCategory.VersionTooNew)
                return (ShellScreen.BootError, PlayerStrings.T("boot.newer"));
            return Route(status);
        }

        public static (ShellScreen Screen, string? Notice) Route(StartStatus status)
        {
            switch (status)
            {
                case StartStatus.NewGameCreated: return (ShellScreen.Home, null);
                case StartStatus.Loaded: return (ShellScreen.Home, null);
                case StartStatus.RecoveredFromBackup: return (ShellScreen.Home, PlayerStrings.T("boot.recovered"));
                case StartStatus.NoSaveFound: return (ShellScreen.Onboarding, null);
                case StartStatus.SaveUnreadable: return (ShellScreen.BootError, PlayerStrings.T("boot.unreadable"));
                case StartStatus.StateInvalid: return (ShellScreen.BootError, PlayerStrings.T("boot.invalid"));
                case StartStatus.ContentIncompatible: return (ShellScreen.BootError, PlayerStrings.T("boot.newer"));
                // D-085: never produced by a boot; mapped so the router stays total. Retry loads the save.
                case StartStatus.ExistingSaveRefused: return (ShellScreen.BootError, PlayerStrings.T("boot.existing"));
                default: throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped boot outcome."); // l10n-ignore: developer exception
            }
        }
    }
}

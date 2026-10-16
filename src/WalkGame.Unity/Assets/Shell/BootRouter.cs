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
        public static (ShellScreen Screen, string? Notice) Route(StartStatus status)
        {
            switch (status)
            {
                case StartStatus.NewGameCreated: return (ShellScreen.Home, null);
                case StartStatus.Loaded: return (ShellScreen.Home, null);
                case StartStatus.RecoveredFromBackup: return (ShellScreen.Home, "Your last save was damaged; your world was restored from the backup copy.");
                case StartStatus.NoSaveFound: return (ShellScreen.Onboarding, null);
                case StartStatus.SaveUnreadable: return (ShellScreen.BootError, "Your save could not be read. Nothing has been changed or deleted.");
                case StartStatus.StateInvalid: return (ShellScreen.BootError, "Your save failed its integrity check. Nothing has been changed or deleted.");
                case StartStatus.ContentIncompatible: return (ShellScreen.BootError, "This save was made by a newer version of the game. Please update.");
                // D-085: never produced by a boot; mapped so the router stays total. Retry loads the save.
                case StartStatus.ExistingSaveRefused: return (ShellScreen.BootError, "A saved game is already on this device; it was not replaced. Try again to load it.");
                default: throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped boot outcome.");
            }
        }
    }
}

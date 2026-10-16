using System;
using System.Collections.Generic;
using System.Globalization;

namespace WalkGame.Shell
{
    /// <summary>
    /// D-120: the single table for player-facing shell strings (localization readiness).
    /// Today it holds English only; a translation replaces the table, not the call sites.
    /// Templates use string.Format placeholders ({0}, {1}) so word order can change per language.
    /// Migration is ratcheted by <c>LocalizationReadinessTests</c>: migrated files may contain no
    /// player-facing literals, and the remaining files may never gain new ones.
    /// </summary>
    public static class PlayerStrings
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Boot outcomes (BootRouter)
            ["boot.recovered"] = "Your last save was damaged; your world was restored from the backup copy.",
            ["boot.unreadable"] = "Your save could not be read. Nothing has been changed or deleted.",
            ["boot.invalid"] = "Your save did not pass its safety check. Nothing has been changed or deleted.",
            ["boot.newer"] = "This save was made by a newer version of the game. Update the game to open it; nothing has been changed.",
            ["boot.existing"] = "A saved game is already on this device; it was not replaced. Try again to load it.",
            // Settings status lines (D-097)
            ["settings.motion.on"] = "Reduced motion is on. Your choice is saved on this device.",
            ["settings.motion.off"] = "Reduced motion is off. You can turn it on at any time.",
            ["settings.sound.on"] = "Sound is on. You can turn it off at any time.",
            ["settings.sound.off"] = "Sound is off. The game stays quiet.",
            ["settings.diagnostics.on"] = "Support details are shown. They are only for looking into a step-count question.",
            ["settings.diagnostics.off"] = "Support details are hidden. You only need them to look into a step-count question.",
            ["settings.textsize.max"] = "Text size: {0}, the largest this game supports. It follows your phone's font size setting.",
            ["settings.textsize"] = "Text size: {0}. It follows your phone's font size setting.",
            // Gentle reminder (D-119)
            ["reminder.title"] = "Millbrook",
            ["reminder.body"] = "The valley is here whenever you feel like a walk.",
            ["reminder.unavailable"] = "Daily reminder: not available on this device.",
            ["reminder.off"] = "Daily reminder is off. If you like, the game can send one gentle note a day.",
            ["reminder.blocked"] = "Daily reminder is on, but notifications are not allowed for this game. You can allow them in your phone's settings.",
            ["reminder.on"] = "Daily reminder is on, around {0}. It is one quiet note, and you can turn it off at any time.",
            ["reminder.action.on"] = "Turn on a gentle daily reminder",
            ["reminder.action.off"] = "Turn off the daily reminder",
            ["reminder.error.unavailable"] = "Reminders aren't available on this device.",
            // Landmark letters (D-118)
            ["letters.count"] = "Letters from {0}: {1} of {2} found.",
            ["letters.line"] = "Found at {0}: \"{1}\"",
            ["letters.inJournal"] = "Read them in the Story journal.",
            // Player copy tables (PlayerCopy, KI-023)
            ["project.status.available"] = "ready to start",
            ["project.status.active"] = "being restored now",
            ["project.status.completed"] = "restored",
            ["project.status.locked"] = "not open yet",
            ["activity.connected"] = "Connected. Your walks are being counted.",
            ["activity.permissionNeeded"] = "Walking data is not connected yet. You can still explore everything.",
            ["activity.permissionDenied"] = "Access to walking data is turned off. Your progress is safe, but new walks will not count until it is turned back on.",
            ["activity.sourceUnavailable"] = "This device has no walking data source we can read. Your progress is safe.",
            ["activity.waiting"] = "Connected. Waiting for your first walk to arrive.",
            ["activity.refreshFailed"] = "We could not refresh your walks just now. Nothing is lost; we will try again.",
            ["activity.providerUpdate"] = "Health Connect needs an update before new walks can count. Your progress is safe.",
            ["activity.unknown"] = "Walking data status is unknown right now. Your progress is safe.",
            ["activity.action.connect"] = "Next: connect your walking data.",
            ["activity.action.openSettings"] = "Next: turn access back on in your phone's settings.",
            ["activity.action.retryLater"] = "Next: nothing to do. It will try again on its own.",
            ["activity.action.updateProvider"] = "Next: update Health Connect from your app store, then come back.",
            ["discovery.kind.infrastructure"] = "piece of valley history",
            ["discovery.kind.settlement"] = "settlement story",
            ["discovery.kind.archive"] = "archive fragment",
            ["discovery.kind.flora"] = "plant life",
            ["discovery.kind.science"] = "science record",
            ["project.status.queued"] = "waiting, {0} in line",
            // Valley picture alt text (RegionScene, KI-023); leading spaces are part of the sentence joins
            ["scene.alt.complete"] = "A picture of the valley, green and fully restored.",
            ["scene.alt.progress"] = "A picture of the valley: {0} of {1} landmarks restored, the hills {2}",
            ["scene.alt.trail.one"] = " A footpath links two recovered places.",
            ["scene.alt.birds"] = " Birds are back over the valley.",
            ["scene.alt.moments.one"] = " One landmark that changed while you were away is circled.",
            ["scene.alt.build"] = " Work on {0} stands at stage {1} of {2}.",
            ["scene.alt.hills.dry"] = "still dry.",
            ["scene.alt.hills.greening"] = "turning green.",
            ["scene.alt.hills.green"] = "mostly green.",
            ["scene.alt.trail.many"] = " Footpaths link {0} recovered places.",
            ["scene.alt.moments.many"] = " {0} landmarks that changed while you were away are circled.",
            // Shell screens (ShellController: errors, onboarding, home, summary, projects, region; KI-023)
            ["shell.unknownAction"] = "Unknown action.",
            ["shell.thatScreenDoesNot"] = "That screen does not exist.",
            ["shell.startOrContinueA"] = "Start or continue a game first.",
            ["shell.yourWorldIsOpen"] = "Your world is open and fine. There is no problem to show.",
            ["shell.aGameIsAlready"] = "A game is already here; it was not replaced.",
            ["shell.weCouldnTStart"] = "We couldn't start a new world. Nothing on this device was changed.",
            ["shell.walkingDataCanT"] = "Walking data can't be connected on this device.",
            ["shell.allLocalDataWas"] = "All local data was deleted.",
            ["shell.yourDataCouldNot"] = "Your data could not be deleted, so nothing was changed. You can try again.",
            ["shell.developmentToolsAreDisabled"] = "Development tools are disabled.",
            ["shell.thatCouldNotBe"] = "That could not be done just now. Your progress is safe.",
            ["shell.yourMovementRestoresThis"] = "Your movement restores this world",
            ["shell.aForgottenValleyIs"] = "A forgotten valley is waiting. Each walk you take brings it back, a little at a time.",
            ["shell.nothingIsLostWhile"] = "Nothing is lost while you are away.",
            ["shell.beginRestoring"] = "Begin restoring",
            ["shell.nothingIsBeingRestored"] = "Nothing is being restored yet. Choose a project to begin.",
            ["shell.whileYouWereAway"] = "While you were away",
            ["shell.theValley"] = "The valley",
            ["shell.yourProgress"] = "Your progress",
            ["shell.devSimulateADay"] = "[dev] Simulate a day of walking",
            ["shell.nothingNewSinceYour"] = "Nothing new since your last visit.",
            ["shell.theValleyChangedWhile"] = "The valley changed while you were away.",
            ["shell.yourWalksChangedThe"] = "Your walks changed the valley.",
            ["shell.seeTheRestoredValley"] = "See the restored valley",
            ["shell.readTheNewChapter"] = "Read the new chapter",
            ["shell.everyRestorationInThis"] = "Every restoration in this valley is finished. It stays restored; check the valley to see it flourish.",
            ["shell.noProjectsAreOpen"] = "No projects are open right now. Finishing a restoration opens new ones.",
            ["shell.nothingIsQueuedChoose"] = "Nothing is queued. Choose a project below and your walks will start restoring it.",
            ["shell.theValleyIsFully"] = "The valley is fully restored. It stays this way; nothing will reset.",
            ["shell.notRunningYetRestore"] = "not running yet. Restore nearby landmarks to bring it back.",
            ["shell.storageFullHoldingWhat"] = "storage full, holding what it made.",
            ["shell.producingWhileYouAre"] = "producing while you are away.",
            ["shell.noDiscoveriesYetWalks"] = "No discoveries yet. Walks that restore landmarks reveal them.",
            ["shell.noExpeditionRoutesAre"] = "No expedition routes are open yet. Restoring the valley opens them.",
            ["shell.discoveriesAndExpeditions"] = "Discoveries and expeditions",
            ["shell.projects.locked.one"] = "{0} more project unlocks as the valley recovers.",
            ["shell.projects.locked.many"] = "{0} more projects unlock as the valley recovers.",
            ["shell.region.finished"] = "{0} of {1} restorations finished.",
            ["shell.region.expeditions"] = "{0} expedition routes open, {1} completed.",
            ["shell.region.discoveries.one"] = "{0} discovery found.",
            ["shell.region.discoveries.many"] = "{0} discoveries found.",
        };

        /// <summary>The string for key; a missing key returns an empty string (never the raw key).</summary>
        public static string T(string key) => key != null && En.TryGetValue(key, out var v) ? v : string.Empty;

        /// <summary>Formats a template with invariant culture (numbers are pre-formatted by callers).</summary>
        public static string F(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(key), args);

        public static IEnumerable<string> Keys => En.Keys;
        public static bool Has(string key) => key != null && En.ContainsKey(key);
    }
}

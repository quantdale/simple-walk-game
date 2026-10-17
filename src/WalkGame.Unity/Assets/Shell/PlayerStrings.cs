using System;
using System.Collections.Generic;
using System.Globalization;
using WalkGame.Application.Content;

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
            ["settings.motion.os"] = "Reduced motion is on because your phone is set to show less motion.",
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
            ["reminder.action.earlier"] = "Remind me earlier, around {0}",
            ["reminder.action.later"] = "Remind me later, around {0}",
            ["reminder.moved"] = "The daily reminder will now come around {0}.",
            // Landmark letters (D-118)
            ["letters.count"] = "Letters from {0}: {1} of {2} found.",
            ["letters.line"] = "Found at {0}: \"{1}\"",
            ["letters.inJournal"] = "Read them in the Story journal.",
            ["epilogue.heading"] = "Epilogue · Millbrook, whole again",
            ["epilogue.letter"] = "A letter from {0}: \"{1}\"",
            ["epilogue.now"] = "In the valley now: {0}",
            ["epilogue.earlier"] = "Earlier valley notes:",
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
            ["shell.notSavedYet"] = "Your latest progress could not be saved just now. It is kept here, and saving will be tried again when you come back.",
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
            ["shell.projects.opens"] = "Finishing it opens: {0}.",
            ["home.today.earned"] = "Today: {0} Vitality earned from your walks.",
            ["shell.projects.locked.many"] = "{0} more projects unlock as the valley recovers.",
            ["shell.region.finished"] = "{0} of {1} restorations finished.",
            ["shell.region.expeditions"] = "{0} expedition routes open, {1} completed.",
            ["shell.region.discoveries.one"] = "{0} discovery found.",
            ["shell.region.discoveries.many"] = "{0} discoveries found.",
            // Shell screens (ShellController: discoveries, story, progress, settings, guide, privacy; KI-023)
            ["shell.nothingFoundYetRestoring"] = "Nothing found yet. Restoring landmarks reveals what the valley kept.",
            ["shell.moreRestoration"] = "more restoration",
            ["shell.theValleyIsRestored"] = "The valley is restored",
            ["shell.everyPlaceYouBrought"] = "Every place you brought back stays restored. Your walks keep the valley flourishing.",
            ["shell.theStoryBeginsSoon"] = "The story begins soon.",
            ["shell.youHaveReadThe"] = "You have read the whole story of the valley. It stays here to reread.",
            ["shell.yourFirstWalkWill"] = "Your first walk will show up here. There is no rush.",
            ["shell.nothingHereExpiresEvery"] = "Nothing here expires. Every walk counts, whenever it happens.",
            ["shell.privacyAndYourData"] = "Privacy and your data",
            ["shell.deleteAllLocalData"] = "Delete all local data…",
            ["shell.delete.title"] = "Delete all local data?",
            ["shell.thisPermanentlyRemovesYour"] = "This permanently removes your world, its backup and your preferences from this device. It cannot be undone.",
            ["shell.keepMyData"] = "Keep my data",
            ["shell.deleteEverything"] = "Delete everything",
            ["shell.connectWalkingData"] = "Connect walking data",
            ["shell.noWalkingDataHas"] = "No walking data has arrived yet. Your progress starts with your first walk.",
            ["shell.theValleyToday"] = "The valley today",
            ["shell.theValleyIsQuiet"] = "The valley is quiet and worn. Your walks will bring it back.",
            ["shell.updateHealthConnect"] = "Update Health Connect",
            ["shell.connectYourWalking"] = "Connect your walking",
            ["shell.theGameReadsOnly"] = "The game reads only your step counts, on this device. Nothing is uploaded.",
            ["shell.youCanKeepGoing"] = "You can keep going without connecting; nothing is locked.",
            ["shell.chooseAFirstRestoration"] = "Choose a first restoration",
            ["shell.goodChoiceItWill"] = "Good choice. It will grow as you move.",
            ["shell.pickOnePlaceTo"] = "Pick one place to restore first. You can change the order later.",
            ["shell.progressWhileYouRe"] = "Progress while you're away",
            ["shell.stepsYouTakeWith"] = "Steps you take with the app closed still count. When you return, you'll see what changed.",
            ["shell.thatSAll"] = "That's all",
            ["shell.youDonTNeed"] = "You don't need to keep the app open. Walk as you normally do and check in whenever you like.",
            ["shell.goToTheValley"] = "Go to the valley",
            ["shell.everythingThisGameKnows"] = "Everything this game knows stays on this phone, in the game's own private storage. Your phone's encryption protects it.",
            ["shell.itKeepsYourValley"] = "It keeps your valley: projects, restored places, story chapters and discoveries.",
            ["shell.itKeepsYourWalks"] = "It keeps your walks: step counts, when each walk started and ended, the day it was, and the record number your walking app gave it, so no walk is counted twice.",
            ["shell.itKeepsYourSettings"] = "It keeps your settings, such as sound and reduced motion, how far you are through the guide, and which valley changes and conversations you have already seen.",
            ["shell.itNeverAsksFor"] = "It never asks for your name, email or location. There are no accounts, ads or analytics, and nothing is sent anywhere.",
            ["shell.youCanShareA"] = "You can share a copy of your save. Your phone's share menu opens and you choose where it goes. The copy includes your walks.",
            ["shell.shareACopyOf"] = "Share a copy of your save",
            ["shell.sharingIsnTAvailable"] = "Sharing isn't available on this device. Your save is safe.",
            ["shell.weCouldnTMake"] = "We couldn't make a copy just now. Your save is safe.",
            ["shell.yourSaveIsToo"] = "Your save is too large to share this way. Your save is safe.",
            ["shell.yourPhoneSShare"] = "Your phone's share menu has the copy. You choose where it goes.",
            ["shell.theShareMenuDidn"] = "The share menu didn't open. Your save is safe.",
            ["shell.weCouldnTOpen"] = "We couldn't open your world",
            ["shell.tryAgain"] = "Try again",
            ["shell.discoveries.origin"] = "Where it came from: {0}",
            ["shell.discoveries.hidden.one"] = "{0} more find is still waiting in the valley.",
            ["shell.discoveries.hidden.many"] = "{0} more finds are still waiting in the valley.",
            ["shell.discoveries.ahead"] = "Something new turns up when you finish: {0}.",
            ["shell.story.path.open"] = "Getting there: {0} — {1} ({2}).",
            ["shell.story.path.amount"] = "{0} of {1} vitality",
            ["shell.story.path.locked.one"] = "Getting there: {0} (opens after: {1}).",
            ["shell.story.path.locked.many"] = "Getting there: {0} (opens after: {1}).",
            ["shell.story.path.lockedLater"] = "Getting there: {0} (opens as the valley recovers).",
            ["shell.discoveries.expeditions"] = "Expedition routes: {0} open, {1} of {2} completed.",
            ["shell.expedition.open"] = "{0} · open; it completes on its own while you walk.",
            ["shell.expedition.locked"] = "{0} · opens after: {1}.",
            ["shell.story.progress"] = "{0} of {1} chapters unlocked. Restoring the valley reveals the next one.",
            ["shell.story.progressTending"] = "{0} of {1} chapters unlocked. Tending the restored valley reveals the last one.",
            ["shell.progress.daysAtLeast.one"] = "You have walked on at least {0} day.",
            ["shell.progress.daysAtLeast.many"] = "You have walked on at least {0} days.",
            ["shell.progress.days.one"] = "You have walked on {0} day.",
            ["shell.progress.days.many"] = "You have walked on {0} days.",
            ["shell.progress.vitality"] = "Vitality gathered from your walks: {0}.",
            ["shell.progress.steps"] = "That is about {0} steps, counted whenever they happened.",
            ["shell.progress.projects.complete"] = "Every restoration in the valley is finished ({0} of {1}).",
            ["shell.progress.projects"] = "Restorations finished: {0} of {1}.",
            ["shell.progress.discoveries"] = "Discoveries found: {0} of {1}.",
            ["shell.progress.chapters"] = "Story chapters unlocked: {0} of {1}.",
            ["shell.settings.motion.on"] = "Turn on reduced motion",
            ["shell.settings.motion.off"] = "Turn off reduced motion",
            ["shell.settings.sound.on"] = "Turn on sound",
            ["shell.settings.sound.off"] = "Turn off sound",
            ["shell.activity.lastBatch"] = "Your last walks added {0} vitality.",
            ["shell.textPreview"] = "Preview: The valley remembers every walk you take.",
            ["shell.celebration.headline"] = "You restored the whole valley.",
            // D-160: "what you restored" recap on the celebration
            ["shell.recap.heading"] = "What you restored:",
            ["shell.recap.projects.one"] = "{0} restoration project finished.",
            ["shell.recap.projects.many"] = "{0} restoration projects finished.",
            ["shell.recap.places"] = "Places brought back: {0}.",
            ["shell.recap.discoveries.one"] = "{0} discovery recorded.",
            ["shell.recap.discoveries.many"] = "{0} discoveries recorded.",
            ["shell.recap.expeditions.one"] = "{0} expedition walked.",
            ["shell.recap.expeditions.many"] = "{0} expeditions walked.",
            ["shell.recap.letters.one"] = "{0} letter from Nell found.",
            ["shell.recap.letters.many"] = "{0} letters from Nell found.",
            // Accessibility and share sheet (KI-023 part 2)
            ["a11y.primaryHint"] = "Main action on this screen.",
            ["share.subject"] = "Simple Walk Game save",
            ["share.chooser"] = "Share a copy of your save",
            // Single-word labels and short label templates (KI-023 remainder, D-138)
            ["label.home"] = "Home",
            ["label.projects"] = "Projects",
            ["label.story"] = "Story",
            ["label.activity"] = "Activity",
            ["label.settings"] = "Settings",
            ["label.continue"] = "Continue",
            ["label.earlier"] = "Earlier: {0}",
            ["label.problem"] = "Problem: {0}",
            ["label.note"] = "Note: {0}",
            ["label.start"] = "Start: {0}",
            ["label.moveUp"] = "Move up: {0}",
            ["label.diagnostics.show"] = "Show diagnostics",
            ["label.diagnostics.hide"] = "Hide diagnostics",
            ["label.resources"] = "Vitality {0} · Materials {1} · Knowledge {2}",
            ["label.resourcesUnspent"] = "Unspent: Vitality {0} · Materials {1} · Knowledge {2}",
            ["label.vitalityInvested"] = "Your Vitality goes straight into {0}: {1} invested so far.",
            ["label.discoveriesFound"] = "Discoveries: {0} of {1} found.",
            ["label.step"] = "Step {0} of {1}",
            ["label.prologue"] = "Prologue · {0}",
            ["label.chapter"] = "Chapter {0} · {1}",
            ["label.storyLine"] = "Story: {0} Next: {1}",
            // World dialogue actions (KI-027d, bible §15–§16)
            ["dialogue.action.inspect"] = "Inspect",
            ["dialogue.action.helpRestore"] = "Help restore",
            ["dialogue.action.maybeLater"] = "Maybe later",
            ["shell.story.epilogue"] = "Story: Millbrook's story is complete. The valley stays restored, and every chapter is here to reread.",
        };

        /// <summary>The string for key; a missing key returns an empty string (never the raw key).</summary>
        /// <summary>
        /// D-126 (KI-023): optional presentation-only locale for shell strings, the same
        /// <see cref="ContentText"/> overlay content copy uses (D-122). Entries are keyed
        /// <c>ui.&lt;key&gt;#text</c>. A missing or blank entry falls back to the English table,
        /// a broken localized template falls back to the English template, and a key absent
        /// from the English table stays empty (an overlay cannot add keys). Never persisted.
        /// </summary>
        public static ContentText Locale { get; set; } = ContentText.English;

        public const string Field = "text";
        public static string OverlayId(string key) => "ui." + key;

        public static string T(string key)
        {
            if (key == null || !En.TryGetValue(key, out var english)) return string.Empty;
            var locale = Locale;
            return locale == null ? english : locale.Resolve(OverlayId(key), Field, english);
        }

        /// <summary>Formats a template with invariant culture (numbers are pre-formatted by callers).</summary>
        public static string F(string key, params object[] args)
        {
            string template = T(key);
            try { return string.Format(CultureInfo.InvariantCulture, template, args); }
            catch (FormatException)
            {
                string english = key != null && En.TryGetValue(key, out var e) ? e : string.Empty;
                return string.Format(CultureInfo.InvariantCulture, english, args);
            }
        }

        public static IEnumerable<string> Keys => En.Keys;
        public static bool Has(string key) => key != null && En.ContainsKey(key);
    }
}

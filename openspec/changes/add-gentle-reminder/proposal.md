# Add an optional gentle daily reminder (local notification, opt-in, off by default)

**Status:** Approved for implementation by the executing agent (D-119), on the same basis as D-103: Region 1 / shell scope.

## Why

The existing preferences (`NotificationsOptIn`, `DailyReminderEnabled`, `DailyReminderMinutesOfDay`) were stored but nothing delivered them. Some players want a soft nudge. It must stay optional and calm.

## What Changes

- Shell port `IReminderScheduler` (permission, request, schedule daily, cancel) with `AndroidReminderScheduler` behind `#if UNITY_ANDROID`. Tests use a fake.
- Java `ReminderBridge` and `ReminderReceiver` in the existing androidlib. `AlarmManager.setInexactRepeating(RTC, …, INTERVAL_DAY)` never wakes the device and is batched by the OS. The notification uses a low-importance channel and opens the game.
- Manifest: `POST_NOTIFICATIONS` (asked only when the player turns the reminder on) and a non-exported receiver.
- Settings: a status line and a toggle. The reminder is wanted only when the master opt-in AND the reminder are on; it is scheduled only when the OS permission is granted. It is re-synced on boot, resume and toggle. A refusal is never re-asked automatically.
- Copy: "Millbrook" / "The valley is here whenever you feel like a walk." No streak, count or guilt wording (test-pinned).

## Non-goals

No server push, no network, no streak, no per-day comparison, no exact alarms (`SCHEDULE_EXACT_ALARM`), no boot receiver (the schedule is restored the next time the game opens). iOS delivery is not part of this change.

## Compatibility

No save or preference schema change (the fields already exist, default off).

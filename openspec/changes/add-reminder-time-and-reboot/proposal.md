# Reminder time picker and re-arm after a phone restart

**Status:** Approved for implementation by the executing agent (D-121), on the same basis as D-103/D-119: Region 1 / shell scope, extending `add-gentle-reminder`.

## Why

D-119 residuals: the reminder always used the stored 08:00 default, and a phone restart cleared the alarm until the game was next opened.

## What Changes

- Settings: while the reminder is on, two plain buttons, "Remind me earlier, around HH:MM" and "Remind me later, around HH:MM". Each moves the time by 30 minutes inside 06:00–21:00 (no night-time notes). The label names the resulting time, so a screen reader announces it. A button is hidden at the window edge. Moving the time saves it, re-schedules once and shows a calm note. It never asks for permission again.
- Android: `ReminderBootReceiver` (non-exported, `BOOT_COMPLETED` only). It re-arms the same inexact RTC alarm from what `ReminderBridge` last armed, and only if the reminder was armed and notifications are still allowed. `cancel` clears the armed flag. `RECEIVE_BOOT_COMPLETED` is a normal install-time permission (no prompt).

## Battery and permission review

The receiver makes one `AlarmManager.setInexactRepeating(RTC, …)` call per boot. It holds no wake lock, starts no service or job, makes no network call, and needs no exact alarm. Nothing runs when the reminder is off.

## Non-goals

No free-form clock dial, no exact alarms, no `MY_PACKAGE_REPLACED` or time-zone receivers (RTC follows the wall clock; the game re-syncs on open). No iOS delivery.

## Compatibility

No save or preference schema change: `DailyReminderMinutesOfDay` already exists and is validated 0–1439. A stored time outside the window still works; the first move snaps it onto the half-hour grid inside the window.

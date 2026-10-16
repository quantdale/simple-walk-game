## Purpose

Defines the optional local gentle daily reminder.

## ADDED Requirements

### Requirement: Off by default and only with consent

The reminder SHALL be off for new and existing players. It SHALL be scheduled only when the player has turned it on AND the OS notification permission is granted. Turning it off or revoking permission SHALL cancel it.

#### Scenario: Permission refused
- **WHEN** the player turns the reminder on and refuses the OS permission
- **THEN** nothing is scheduled, Settings explains how to allow notifications, and the game does not ask again on its own

### Requirement: Calm and battery-safe

The notification copy SHALL contain no streak, count, guilt or urgency wording. Scheduling SHALL use an inexact, non-waking daily alarm, and SHALL make no network call.

#### Scenario: Scheduled reminder
- **WHEN** the reminder is on and permitted
- **THEN** one inexact daily RTC alarm near the chosen device-local time is set, replacing any previous one

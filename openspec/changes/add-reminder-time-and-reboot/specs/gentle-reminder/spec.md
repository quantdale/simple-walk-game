## ADDED Requirements

### Requirement: Calm reminder time choice

While the reminder is on, Settings SHALL offer earlier and later controls. Each moves the time by 30 minutes within 06:00–21:00, and each label names the resulting time. Moving SHALL save the time and re-schedule once, and SHALL NOT request permission.

#### Scenario: Later
- **WHEN** the reminder is on at 08:00 and the player chooses "Remind me later, around 08:30"
- **THEN** the stored time is 08:30, one alarm near 08:30 replaces the old one, and Settings says it is on around 08:30

### Requirement: Survives a phone restart without new prompts

After the device boots, the reminder SHALL be re-armed with its last time and copy only if it was armed and notifications are still allowed. Re-arming SHALL use the same inexact non-waking alarm, with no wake lock, service or network.

#### Scenario: Reminder off before restart
- **WHEN** the player turned the reminder off and then restarts the phone
- **THEN** nothing is scheduled

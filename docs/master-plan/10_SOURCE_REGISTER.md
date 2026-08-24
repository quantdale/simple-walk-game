# 10 — Primary Source Register

These sources anchor the implementation decisions. Agents should re-check current documentation when implementing because platform APIs evolve.

## Expo / React Native

1. Expo SDK 57 changelog — https://expo.dev/changelog/sdk-57
2. Expo SDK version reference — https://docs.expo.dev/versions/latest/
3. Create Expo project — https://docs.expo.dev/get-started/create-a-project/
4. Expo Router — https://docs.expo.dev/versions/latest/sdk/router/
5. Expo Notifications — https://docs.expo.dev/versions/latest/sdk/notifications/
6. Expo Background Task — https://docs.expo.dev/versions/latest/sdk/background-task/
7. Expo TaskManager — https://docs.expo.dev/versions/latest/sdk/task-manager/
8. Expo Sensors — https://docs.expo.dev/versions/latest/sdk/sensors/
9. Expo SQLite — https://docs.expo.dev/versions/latest/sdk/sqlite/
10. React Native releases/archive — https://reactnative.dev/blog/archive
11. React Native 0.86 release — https://reactnative.dev/blog/2026/06/11/react-native-0.86

## Android Health Connect

12. Health Connect overview/get started — https://developer.android.com/health-and-fitness/health-connect/get-started
13. Health Connect client API — https://developer.android.com/reference/androidx/health/connect/client/HealthConnectClient
14. Health Connect platform data types — https://developer.android.com/reference/android/health/connect/datatypes/package-summary
15. Health Connect aggregate data — https://developer.android.com/health-and-fitness/health-connect/aggregate-data
16. Health Connect read data — https://developer.android.com/health-and-fitness/health-connect/read-data
17. Health Connect background read guidance/codelab — https://developer.android.com/codelabs/health-connect
18. Android WorkManager — https://developer.android.com/develop/background-work/background-tasks/persistent/getting-started

Relevant Health Connect types include steps, distance, elevation gained, exercise sessions, active calories, activity intensity, speed and more. The core product should request only the subset actually required.

## Apple HealthKit

19. HealthKit overview — https://developer.apple.com/documentation/healthkit
20. Setting up HealthKit — https://developer.apple.com/documentation/healthkit/setting-up-healthkit
21. Authorizing access — https://developer.apple.com/documentation/healthkit/authorizing-access-to-health-data
22. Protecting user privacy — https://developer.apple.com/documentation/healthkit/protecting-user-privacy
23. HKObserverQuery — https://developer.apple.com/documentation/healthkit/hkobserverquery
24. Executing observer queries/background delivery — https://developer.apple.com/documentation/healthkit/executing-observer-queries
25. HealthKit background-delivery entitlement — https://developer.apple.com/documentation/bundleresources/entitlements/com.apple.developer.healthkit.background-delivery
26. HKAnchoredObjectQuery — https://developer.apple.com/documentation/healthkit/hkanchoredobjectquery
27. HealthKit quantity types — https://developer.apple.com/documentation/healthkit/hkquantitytypeidentifier
28. Running workout sessions — https://developer.apple.com/documentation/healthkit/running-workout-sessions
29. Apple HIG HealthKit — https://developer.apple.com/design/human-interface-guidelines/healthkit

## Storage/security/platform

30. SQLite documentation — https://sqlite.org/docs.html
31. SQLite WAL — https://sqlite.org/wal.html
32. Apple Keychain Services — https://developer.apple.com/documentation/security/keychain-services
33. Android Keystore — https://developer.android.com/privacy-and-security/keystore
34. Expo SecureStore — https://docs.expo.dev/versions/latest/sdk/securestore/

## Testing/release

35. Maestro documentation — https://docs.maestro.dev/
36. React Native Testing Library — https://callstack.github.io/react-native-testing-library/
37. GitHub Actions — https://docs.github.com/actions
38. Expo EAS Build — https://docs.expo.dev/build/introduction/

## Optional backend

39. Supabase React Native auth quickstart — https://supabase.com/docs/guides/auth/quickstarts/react-native
40. Supabase database docs — https://supabase.com/docs/guides/database
41. Supabase Row Level Security — https://supabase.com/docs/guides/database/postgres/row-level-security

## Source policy

- Prefer current official platform docs over blog snippets.
- Re-check package compatibility immediately before adopting a native/graphics dependency.
- Record exact package/API versions in ADRs and lockfiles.
- Treat background-execution documentation as constraints, not guarantees.
- Health-data privacy guidance is a release gate, not optional reading.

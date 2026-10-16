# KI-024: System.Text.Json 8.0.5 → 10.0.13 inside Unity (grok0215, 2026-10-17)

Base: main `cc77218`. Unity 6000.3.26f1 on the Linux box. Labels: Unity-editor-tested, IL2CPP-compiled; device UNVERIFIED (KI-002).

| Step | Result |
|---|---|
| `dotnet publish` (netstandard2.1) | STJ 10.0.0.0, System.Text.Encodings.Web 10.0.0.0, Microsoft.Bcl.AsyncInterfaces 10.0.0.13, **System.IO.Pipelines 10.0.0.0 (new)**, System.Runtime.CompilerServices.Unsafe 6.1.2.0 |
| `tools/unity/sync-plugins.sh` | now copies System.IO.Pipelines too (STJ 10 references it on netstandard2.x) |
| .NET tests | 815/815 (Domain 157, Infrastructure 185, Application 473) |
| EditMode (Mono, save/load through STJ) | 205/205 passed (`editmode-results.xml`) |
| PlayMode | 5 cases: 4 passed, 1 ignored (the PNG capture test, which needs `WG_CAPTURE_PNG`) (`playmode-results.xml`) |
| arm64 IL2CPP debug APK | `WG_BUILD_RESULT Succeeded`, 31.6 MB |
| iOS Xcode export (local only, Linux) | `WG_BUILD_RESULT Succeeded`; `Il2CppOutputProject` has `System.Text.Json*.cpp` and `System.IO.Pipelines.cpp`. Not pushed anywhere (D-116); no xcodebuild was run (that needs macOS). |
| IL2CPP stripping | `Library/Bee/artifacts/{Android,iOS}/ManagedStripped/`: STJ 10.0.0.0 753,664 B (input 767,784 B; `link.xml` preserve="all" keeps it), Encodings.Web 75,776 B, Pipelines 15,360 B (85 KB input, linker-trimmed to the members STJ references), Unsafe 6.1.2.0. Microsoft.Bcl.AsyncInterfaces is stripped entirely (unused on this profile). APK `global-metadata.dat` has `JsonSerializerOptions`, `Utf8JsonWriter`, `JsonConverter` and `System.IO.Pipelines`. |
| `scripts/verify-dependency-health.sh` | PASS: no advisories; one runtime package (STJ 10.0.13) |

Not verified: runtime save/load under IL2CPP on a device (KI-002: no KVM, no phone); a macOS xcodebuild of this export (the public export transport is retired, D-116).

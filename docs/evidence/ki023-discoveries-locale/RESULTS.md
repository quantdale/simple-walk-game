# KI-023 slice 3 (D-122): glance phrases, producer/expedition/care titles, Discoveries with a test locale (grok0215)

- `discoveries-pseudo-locale-editor-rendered.png`: editor-rendered (software GL, NOT device). Completed-region fixture, `WG_CAPTURE_SCREEN=Discoveries WG_CAPTURE_LOCALE=pseudo`. Discovery titles, body and origin copy come from the overlay ("[xx] "). Shell-table text ("Discoveries: 13 of 13 found.", "Where it came from:", category words, dates) stays English: `PlayerStrings` has no locale loader yet.
- EditMode 206/206 (`editmode-results.xml`), including the new `DiscoveriesLocaleRenderTests`. It renders the screen through `ShellView` into a VisualElement tree with a pseudo locale. Every found discovery's title and body, and every expedition title, is localized. No stable id leaks, save bytes are unchanged, and English comes back exactly.
- .NET 820/820, including the new `ContentGlanceOverlayTests` (5).

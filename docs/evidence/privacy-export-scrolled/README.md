# Privacy screen scrolled to the export button (grok2230)

**Label: editor-rendered** (Unity editor PlayMode, Mesa llvmpipe under Xvfb). NOT device-rendered.

`privacy-export-editor-rendered.png` is the D-111 Privacy screen with the shared content ScrollView
scrolled to the end, so "Share a copy of your save" and "Settings" are visible. The earlier tour PNG
(`docs/evidence/screen-tour/privacy-editor-rendered.png`) shows only the top of the screen.

The capture test asserts the button is fully inside the viewport (`WG_CAPTURE_EXPECT_BUTTON`), so a
layout change that pushes it off-screen fails the capture instead of producing a misleading PNG.
See RUN.txt for offsets and bounds.

Caveat: the scrollbar thumb is drawn near the top even though the offset is at its maximum
(290.67 of 290.67 px); this looks like a render-texture capture quirk of the scrollbar, not a layout
fault. Device share sheet: UNVERIFIED (KI-002).

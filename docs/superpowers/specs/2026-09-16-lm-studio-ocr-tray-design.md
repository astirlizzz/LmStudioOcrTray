# LM Studio OCR Tray — Design

Date: 2026-09-16

## Goal

Create a small Windows tray utility that remembers the most recently copied image and, on a configurable global hotkey, sends that image to a currently loaded LM Studio vision model for OCR. The recognized text is copied back to the Windows clipboard.

The utility must remain unobtrusive: no polling-heavy behavior, no visible UI unless requested, and no notification when LM Studio or an eligible vision model is unavailable.

## Platform and technology

- Windows 11 focused.
- C# / .NET 8.
- WinForms application without a normal main window.
- Custom borderless non-activating status popup rather than Windows toast notifications.
- Self-contained deployment is preferred for release builds.

## Application lifecycle

The application runs from the system tray using a custom `ApplicationContext`.

Tray menu:

- `Enabled` checkbox.
- `Settings...`.
- separator.
- `Exit`.

`Enabled = false` unregisters the OCR global hotkey and prevents OCR requests. Clipboard image monitoring remains active so that re-enabling the application immediately has access to the newest copied image.

`Start with Windows` is enabled by default and can be disabled in Settings.

## Clipboard behavior

The application registers for Windows clipboard change notifications using `AddClipboardFormatListener` and handles `WM_CLIPBOARDUPDATE` in a hidden WinForms/native-message window.

When the clipboard contains an image:

1. Copy the image into application-owned memory.
2. Atomically replace the previous cached image.
3. Dispose the previous cached image after replacement.

Clipboard changes containing text, files, or other non-image data do not clear the cached image.

The cached image remains available after successful or failed OCR. A later copied image simply replaces it.

No temporary image file is required; the image is encoded in memory when sending it to LM Studio.

## Global OCR hotkey

Default: `Win+Shift+Z`.

The hotkey is configurable in Settings.

Implementation uses `RegisterHotKey` / `WM_HOTKEY`.

When a user chooses a new combination, registration is tested immediately. If Windows reports the combination is already in use, the setting is rejected and the user is told that the hotkey is unavailable.

## OCR concurrency

Only one OCR operation may run at a time.

If the hotkey is pressed while OCR is already running:

- do not create another request;
- do not queue work;
- leave the current request untouched;
- show `OCR already running` for about 1.5 seconds.

If the hotkey is pressed before any image has ever been cached, show `No image captured` for about 2 seconds.

## LM Studio connection

### Server selection

Setting: `Server`.

Default: `Auto`.

Auto-detection order:

1. Run `lms server status --json --quiet` and, when successful, use the reported local server port.
2. If the CLI is unavailable or cannot provide server information, try `http://127.0.0.1:1234` as a fallback.
3. If the user supplied an explicit server URL, skip auto-detection and use that URL only.

LM Studio is expected to run on the same PC, but an explicit URL remains supported.

### Authentication

Optional `API key` setting.

- Empty: send no authentication header.
- Present: send the LM Studio API authentication header.
- Store the API key protected with Windows DPAPI rather than as plaintext in the settings JSON.

### Model discovery

Use LM Studio `GET /api/v1/models`.

Eligible Auto models must satisfy both:

- `capabilities.vision == true`;
- the model has at least one currently loaded instance.

Model setting:

- `Auto`: choose the first eligible loaded vision model returned by LM Studio.
- explicit model: use only that configured model, and only while it is currently loaded and vision-capable.

Settings provides a `Refresh models` action that queries the current server and displays only currently loaded vision-capable models. If refresh fails, the previously saved explicit model selection is not destroyed.

### Silent unavailable state

Before displaying the `OCR...` status popup, the application verifies that:

- the LM Studio server is reachable;
- a usable loaded vision model is available.

If either check fails, pressing the OCR hotkey produces no popup and no request.

This makes an offline LM Studio instance behave as though the OCR feature simply is not currently available.

## OCR request

Use LM Studio native `POST /api/v1/chat`.

Request contains:

- selected model identifier;
- configured OCR prompt;
- cached image encoded as an in-memory PNG and supplied as a base64 data URL;
- `temperature: 0`;
- `store: false`.

When model capabilities indicate that explicit reasoning-off mode is supported, include `reasoning: "off"`. Otherwise omit the reasoning parameter rather than sending an unsupported value.

Default request timeout: 60 seconds.

Timeout is configurable in Settings.

On timeout, cancel the operation and treat it as OCR failure.

## Default OCR prompt

Default:

> Transcribe all visible text from the image. Return only the transcription. Preserve line breaks, layout, lists, tables, and code formatting as closely as possible. Do not explain, summarize, or add commentary. Do not reason extensively; perform the transcription quickly and directly.

The prompt is fully editable in Settings and has a `Reset default` action.

## OCR result handling

On successful LM Studio response:

1. Extract only the final assistant text intended as the result.
2. Write that text to the Windows clipboard.
3. Keep the cached image unchanged.
4. Show the success popup.

If the LM Studio request was started but fails, times out, or returns an unusable result, show the failure popup.

## Status popup

Implement as a custom small borderless WinForms `Form` positioned near the system tray / work-area corner.

Properties:

- topmost;
- does not activate or steal keyboard focus;
- no taskbar button;
- compact dimensions, approximately 120–180 px wide and 35–45 px high depending on text;
- brief fade-out is acceptable;
- should look modern rather than like a stock WinForms message box.

States:

- `OCR...` — shown only after LM Studio and a usable model have been validated and immediately before starting the request; visible for about 1.5–2 seconds, then disappears even if OCR is still running.
- `✓ Copied` — green, clearly visible check mark; about 1.5–2 seconds.
- `✕ OCR Failed!` — red, clearly visible cross; about 2 seconds.
- `OCR already running` — neutral/informational; about 1.5 seconds.
- `No image captured` — warning/error style; about 2 seconds.

The popup itself never remains visible for the entire inference duration.

## Settings UI

Single compact Settings form.

### General

- `Hotkey`: defaults to `Win+Shift+Z`; control captures a replacement combination.
- `Start with Windows`: checked by default.
- `Timeout`: numeric seconds, default 60.

### LM Studio

- `Server`: `Auto` by default or explicit URL.
- `API key`: optional masked field.
- `Vision model`: `Auto` or explicit loaded vision model.
- `Refresh models` button.
- connection/status label for interactive diagnostics inside Settings only.

### OCR

- multiline editable prompt.
- `Reset default`.

Actions:

- `Save`.
- `Cancel`.

Settings are persisted as JSON under the user's local application data directory. Secrets are protected separately with DPAPI.

## Suggested components

- `TrayApplicationContext` — application lifecycle, tray icon/menu, Settings and Exit.
- `ClipboardMonitor` — clipboard listener and application-owned cached-image replacement.
- `GlobalHotkey` — registration, unregistration, conflict detection, `WM_HOTKEY` handling.
- `LmStudioDiscovery` — Auto server resolution and fallback.
- `LmStudioClient` — API calls, authentication, model listing, chat request.
- `VisionModelSelector` — Auto/explicit eligibility logic.
- `OcrService` — single-flight coordination, timeout, prompt/image request and result extraction.
- `ClipboardWriter` — robust text write-back to Windows clipboard.
- `StatusPopup` — non-activating compact transient UI.
- `SettingsForm` — settings editor and connection/model refresh diagnostics.
- `SettingsStore` — JSON settings persistence and DPAPI-backed API-key storage.
- `StartupRegistration` — start-with-Windows integration.

Each component should have a narrow interface so Win32 details, LM Studio protocol handling, and UI behavior can be tested independently.

## Main flow

1. User copies or captures an image.
2. `ClipboardMonitor` receives `WM_CLIPBOARDUPDATE` and caches a private copy of that image.
3. User presses `Win+Shift+Z`.
4. If another OCR is running, show `OCR already running` and stop.
5. If no image has been cached, show `No image captured` and stop.
6. Resolve the LM Studio server.
7. Query available models and resolve the configured vision model.
8. If server/model is unavailable, stop silently.
9. Mark OCR operation active.
10. Show `OCR...` briefly.
11. Send request with configured timeout.
12. On success, place recognized text in clipboard and show green `✓ Copied`.
13. On request failure/timeout/invalid response, show red `✕ OCR Failed!`.
14. Clear active-operation state. Cached image remains untouched.

## Error handling

- Clipboard may be temporarily locked by another process. Image reads and result writes should use a small bounded retry strategy rather than fail on the first contention error.
- Failures before a request is possible because LM Studio/model is unavailable are silent by design.
- Failures after OCR begins are visible via `✕ OCR Failed!`.
- Exceptions must not terminate the tray process; log diagnostic details locally if logging is enabled.
- No unbounded queues, retries, or background reconnect loops.

## Testing strategy

Unit-test independently:

- model eligibility and Auto/explicit selection;
- settings serialization and defaults;
- server resolution behavior;
- single-flight OCR coordination;
- timeout/cancellation behavior;
- LM Studio response parsing.

Integration/manual Windows tests:

- `Win+Shift+S` screenshot enters clipboard and is captured;
- copying text afterward does not remove cached image;
- copying a new image replaces the old one;
- global hotkey conflict is detected;
- LM Studio closed: hotkey remains silent;
- LM Studio open with no vision model: hotkey remains silent;
- loaded vision model: `OCR...` appears briefly, then result replaces clipboard and `✓ Copied` appears;
- request error/timeout: `✕ OCR Failed!`;
- repeated hotkey while running: no second request and `OCR already running`;
- startup setting behaves across Windows sign-in;
- popup never steals focus from the currently active application.

## Out of scope for v1

- OCR history.
- Multiple queued images.
- Automatic OCR immediately on screenshot capture.
- Cloud model providers.
- Image preprocessing/cropping UI.
- Rich-text/HTML clipboard reconstruction.
- Continuous LM Studio polling or background reconnect loops.

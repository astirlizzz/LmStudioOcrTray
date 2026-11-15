# LM Studio OCR Tray Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows 11 WinForms tray utility that caches the most recently copied image and sends it to a loaded LM Studio vision model for OCR on a configurable global hotkey, then copies the result back to the clipboard.

**Architecture:** A small `net8.0-windows` WinForms executable owns the tray UI and Win32 integration. Protocol/model-selection logic lives in a dependency-free `net8.0` core project so it can be tested without Windows; Windows-specific clipboard, hotkey, popup, startup registration, DPAPI settings, and forms remain in the app project. A dependency-free console test project exercises core behavior without NuGet test packages.

**Tech Stack:** C# 12, .NET 8, WinForms, Win32 P/Invoke, `HttpClient`, `System.Text.Json`, Windows DPAPI via `CryptProtectData`/`CryptUnprotectData`, HKCU Run key.

**Spec:** `docs/superpowers/specs/2026-09-16-lm-studio-ocr-tray-design.md`

## Global Constraints

- Windows 11 focused; C# / .NET 8.
- WinForms application without a normal main window.
- No third-party NuGet dependencies.
- Default OCR hotkey is `Win+Shift+Z` and it is configurable.
- Clipboard image monitoring uses `AddClipboardFormatListener`; non-image clipboard changes never clear the cached image.
- LM Studio is checked only when OCR is requested; no background reconnect/poll loop.
- Auto server discovery first tries `lms server status --json --quiet`, then `http://127.0.0.1:1234`.
- Auto model selection uses the first currently loaded model with `capabilities.vision == true`.
- `POST /api/v1/chat` sends the configured prompt plus an in-memory PNG data URL, `temperature: 0`, `store: false`, and `reasoning: "off"` only when allowed by model capabilities.
- Default timeout is 60 seconds and configurable.
- API key is stored using Windows DPAPI, not plaintext JSON.
- Popup UI is custom, compact, borderless, topmost and non-activating; no Windows toast notifications.
- Only one OCR operation is active; repeated hotkeys do not queue work.
- `Start with Windows` is enabled by default.

---

### Task 1: Solution skeleton, settings model, and model selection

**Files:**
- Create: `LmStudioOcrTray.sln`
- Create: `src/LmStudioOcrTray.Core/LmStudioOcrTray.Core.csproj`
- Create: `src/LmStudioOcrTray.Core/AppSettings.cs`
- Create: `src/LmStudioOcrTray.Core/VisionModel.cs`
- Create: `src/LmStudioOcrTray.Core/VisionModelSelector.cs`
- Create: `tests/LmStudioOcrTray.Tests/LmStudioOcrTray.Tests.csproj`
- Create: `tests/LmStudioOcrTray.Tests/Program.cs`

**Interfaces:**
- Produces: `AppSettings.CreateDefault()`, `VisionModel`, `VisionModelSelector.Select(IReadOnlyList<VisionModel>, string?)`.

- [ ] **Step 1: Write the failing dependency-free tests** for default timeout/hotkey/prompt and Auto/explicit vision-model selection.
- [ ] **Step 2: Run `dotnet run --project tests/LmStudioOcrTray.Tests`** and verify compilation/tests fail because core types are missing.
- [ ] **Step 3: Implement the minimal core settings/model types** with the exact defaults from the spec.
- [ ] **Step 4: Run the test project** and verify all Task 1 tests pass.
- [ ] **Step 5: Commit** with `feat: add core settings and model selection`.

### Task 2: LM Studio discovery, protocol DTOs, and response parsing

**Files:**
- Create: `src/LmStudioOcrTray.Core/LmStudioDiscovery.cs`
- Create: `src/LmStudioOcrTray.Core/LmStudioClient.cs`
- Create: `src/LmStudioOcrTray.Core/LmStudioProtocol.cs`
- Modify: `tests/LmStudioOcrTray.Tests/Program.cs`

**Interfaces:**
- Produces: `LmStudioDiscovery.ResolveServerUriAsync(string?, CancellationToken)`, `LmStudioClient.ListLoadedVisionModelsAsync(Uri,string?,CancellationToken)`, `LmStudioClient.ChatOcrAsync(Uri,string?,VisionModel,string,string,CancellationToken)`, and pure parsing helpers used by tests.

- [ ] **Step 1: Add failing tests** for `lms server status` JSON parsing, model-list parsing (vision + loaded filtering, reasoning-off capability), and chat output extraction that ignores reasoning items and returns message content.
- [ ] **Step 2: Run tests and verify failure.**
- [ ] **Step 3: Implement discovery and HTTP protocol handling** using only `HttpClient`/`System.Text.Json`, Bearer auth when an API key is present, and `/api/v1/models` + `/api/v1/chat`.
- [ ] **Step 4: Run tests and verify pass.**
- [ ] **Step 5: Commit** with `feat: add LM Studio API integration`.

### Task 3: Windows integration and persistent settings

**Files:**
- Create: `src/LmStudioOcrTray/LmStudioOcrTray.csproj`
- Create: `src/LmStudioOcrTray/Program.cs`
- Create: `src/LmStudioOcrTray/Win32.cs`
- Create: `src/LmStudioOcrTray/ClipboardMonitor.cs`
- Create: `src/LmStudioOcrTray/GlobalHotkey.cs`
- Create: `src/LmStudioOcrTray/HotkeyFormatter.cs`
- Create: `src/LmStudioOcrTray/SettingsStore.cs`
- Create: `src/LmStudioOcrTray/DpapiProtector.cs`
- Create: `src/LmStudioOcrTray/StartupRegistration.cs`
- Create: `src/LmStudioOcrTray/AppLogger.cs`

**Interfaces:**
- Produces: `ClipboardMonitor.GetLatestImageClone()`, `GlobalHotkey.TryRegister(HotkeySettings)`, `SettingsStore.Load()/Save(AppSettings)`, and `StartupRegistration.SetEnabled(bool)`.

- [ ] **Step 1: Add pure tests** for hotkey formatting/serialization-compatible values where possible without Windows UI.
- [ ] **Step 2: Implement Win32 wrappers and Windows services**: clipboard listener with bounded retries and owned bitmap replacement, `RegisterHotKey`, DPAPI P/Invoke, HKCU Run registration, JSON persistence under LocalAppData, bounded best-effort logging.
- [ ] **Step 3: Build the Windows project** (or at minimum compile the core/tests if the current host lacks Windows targeting packs) and fix compiler errors.
- [ ] **Step 4: Run core tests again.**
- [ ] **Step 5: Commit** with `feat: add Windows tray infrastructure`.

### Task 4: Popup, settings form, and tray workflow

**Files:**
- Create: `src/LmStudioOcrTray/StatusPopup.cs`
- Create: `src/LmStudioOcrTray/StatusPopupService.cs`
- Create: `src/LmStudioOcrTray/HotkeyCaptureBox.cs`
- Create: `src/LmStudioOcrTray/SettingsForm.cs`
- Create: `src/LmStudioOcrTray/ImageEncoder.cs`
- Create: `src/LmStudioOcrTray/TrayApplicationContext.cs`

**Interfaces:**
- `TrayApplicationContext` coordinates `ClipboardMonitor`, `GlobalHotkey`, `LmStudioDiscovery`, `LmStudioClient`, settings, and popups.
- `StatusPopupService` exposes informational/success/error transient popups without activation.
- `SettingsForm` edits a clone of current settings, refreshes currently loaded vision models, and delegates hotkey/apply validation to the application context.

- [ ] **Step 1: Implement the non-activating custom popup** with the exact transient states and durations from the spec.
- [ ] **Step 2: Implement Settings UI** for hotkey, startup, timeout, server Auto/manual, API key, model Auto/explicit, refresh diagnostics, editable prompt, reset default, Save/Cancel.
- [ ] **Step 3: Implement tray menu and main OCR flow** including silent server/model unavailability, single-flight guard, PNG data URL encoding, timeout cancellation, clipboard text write retry, `OCR...`, green `✓ Copied`, red `✕ OCR Failed!`, `OCR already running`, and `No image captured`.
- [ ] **Step 4: Build and run automated tests.**
- [ ] **Step 5: Commit** with `feat: implement tray OCR workflow`.

### Task 5: Build ergonomics, documentation, and packaging verification

**Files:**
- Create: `README.md`
- Create: `.gitignore`
- Create: `Directory.Build.props`
- Modify: `LmStudioOcrTray.sln` if required.

**Interfaces:**
- Produces: Visual Studio-ready solution and documented Debug/Release/self-contained publish commands.

- [ ] **Step 1: Add build/publish documentation** for Visual Studio and Developer PowerShell/Command Prompt.
- [ ] **Step 2: Add compiler defaults** (`Nullable`, implicit usings, deterministic build) and ignore `bin/obj/.vs`.
- [ ] **Step 3: Run `dotnet run --project tests/LmStudioOcrTray.Tests -c Release` and `dotnet build LmStudioOcrTray.sln -c Release`.**
- [ ] **Step 4: Inspect the source tree for accidentally committed secrets/build output and verify `git status`.**
- [ ] **Step 5: Commit** with `docs: add build and usage instructions`, then create a source ZIP containing the solution, source, tests, docs, and README but excluding `.git`, `.vs`, `bin`, and `obj`.

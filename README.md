# LM Studio OCR Tray

Small Windows 11 tray utility that remembers the most recently copied image and sends it to a currently loaded LM Studio vision model for OCR when you press a global hotkey. The recognized text is copied back to the clipboard.

## Default workflow

1. Capture a region with `Win+Shift+S` (or copy any image).
2. Press `Win+Shift+Z`.
3. If LM Studio is running and has a loaded vision model, a small `OCR…` popup appears briefly.
4. On success, the recognized text replaces the clipboard text and a green `✓ Copied` popup appears.
5. Paste with `Ctrl+V`.

The cached image is not cleared by copying text/files and is not cleared after OCR. A newly copied image simply replaces it.

## Requirements

- Windows 11
- .NET 8 / Visual Studio with the .NET desktop development workload
- LM Studio with the native v1 REST API (`/api/v1/models`, `/api/v1/chat`)

There are no third-party NuGet dependencies.

## Build in Visual Studio

Open:

```text
LmStudioOcrTray.sln
```

`LmStudioOcrTray` is the WinForms application project. Build `Release | Any CPU` and run it. If Visual Studio does not automatically choose it as the startup project, right-click `LmStudioOcrTray` -> **Set as Startup Project**.

The normal framework-dependent output is under:

```text
src\LmStudioOcrTray\bin\Release\net8.0-windows\
```

## Build from Developer PowerShell / Command Prompt

```powershell
dotnet build .\LmStudioOcrTray.sln -c Release
```

Run directly:

```powershell
dotnet run --project .\src\LmStudioOcrTray\LmStudioOcrTray.csproj -c Release
```

Run the dependency-free core tests:

```powershell
dotnet run --project .\tests\LmStudioOcrTray.Tests\LmStudioOcrTray.Tests.csproj -c Release
```

## Self-contained win-x64 build

Either run:

```text
publish-win-x64.cmd
```

or:

```powershell
dotnet publish .\src\LmStudioOcrTray\LmStudioOcrTray.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\artifacts\publish\win-x64
```

## Tray menu

- **Enabled** — registers/unregisters the OCR global hotkey. Clipboard image monitoring stays active while disabled.
- **Settings...** — hotkey, Windows startup, timeout, LM Studio connection, model selection and OCR prompt.
- **Exit** — closes the utility.

## LM Studio connection

`Server = Auto` is the default.

Auto mode:

1. If the LM Studio desktop process is already running, the utility tries the bundled `%USERPROFILE%\.lmstudio\bin\lms.exe` (then `lms.exe` from `PATH`) with `lms server status --json --quiet` to discover a custom local port.
2. It deliberately does not invoke `lms` while the desktop app is absent, avoiding waking LM Studio as a side effect.
3. It falls back to `http://127.0.0.1:1234`.

You can enter an explicit HTTP/HTTPS server address instead. An optional API key is sent as `Authorization: Bearer ...` and stored on disk encrypted with Windows DPAPI.

`Vision model = Auto` selects the first model returned by LM Studio that is both vision-capable and currently loaded. You can also select a specific loaded vision model in Settings.

If LM Studio is offline or no usable vision model is loaded, pressing the OCR hotkey is intentionally silent.

## OCR request

The request uses LM Studio `POST /api/v1/chat` with:

- the cached image encoded in memory as PNG/base64;
- the configurable OCR prompt;
- `temperature: 0`;
- `store: false`;
- `reasoning: "off"` only when the model reports that `off` is supported.

Default timeout: **60 seconds**.

Default prompt:

> Transcribe all visible text from the image. Return only the transcription. Preserve line breaks, layout, lists, tables, and code formatting as closely as possible. Do not explain, summarize, or add commentary. Do not reason extensively; perform the transcription quickly and directly.

## Popups

The app uses a custom small non-activating WinForms popup instead of Windows notifications:

- `OCR…` — about 1.7 s, even if inference continues longer.
- green `✓ Copied` — about 1.7 s.
- red `✕ OCR Failed!` — about 2 s.
- `OCR already running` — about 1.5 s; repeated requests are not queued.
- `No image captured` — about 2 s.

## Local files

Settings and diagnostics are stored under:

```text
%LOCALAPPDATA%\LmStudioOcrTray\
```

The API key itself is not stored as plaintext; only the DPAPI-protected blob is serialized to `settings.json`.

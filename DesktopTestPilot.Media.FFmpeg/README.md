# DesktopTestPilot.Media.FFmpeg

This optional package supplies `ffmpeg.exe` for DesktopTestPilot video recording. The core `DesktopTestPilot` package does not contain FFmpeg and UI automation does not require this package.

Install this package alongside `DesktopTestPilot` only when recording video. The package copies the executable to `DesktopTestPilotResources\ffmpeg.exe`, where DesktopTestPilot discovers it automatically. Applications can instead set `AppDriver.RecordingFfmpegPathOverride` to a separately managed FFmpeg executable.

See `NOTICE.md` and `provenance/ffmpeg.sha256` in the package for binary provenance and integrity information.

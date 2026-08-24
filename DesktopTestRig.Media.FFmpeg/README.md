# DesktopTestRig.Media.FFmpeg

This optional package supplies `ffmpeg.exe` for DesktopTestRig video recording. The core `DesktopTestRig` package does not contain FFmpeg and UI automation does not require this package.

Install this package alongside `DesktopTestRig` only when recording video. The package copies the executable to `DesktopTestRigResources\ffmpeg.exe`, where DesktopTestRig discovers it automatically. Applications can instead set `AppDriver.RecordingFfmpegPathOverride` to a separately managed FFmpeg executable.

See `NOTICE.md` and `provenance/ffmpeg.sha256` in the package for binary provenance and integrity information.

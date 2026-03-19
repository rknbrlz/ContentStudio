# Hgerman Content Studio V2

A modular ASP.NET Core MVC + Worker solution for AI-assisted short-form video production.

## What's new in V2
- OpenAI Responses API integration for script and scene planning
- OpenAI Images API integration for scene still generation
- OpenAI text-to-speech integration for voiceover
- SRT subtitle generation
- FFmpeg-based MP4 rendering
- Hybrid storage: Azure Blob when configured, local disk fallback when not

## Setup
1. Use the Azure SQL database named `HgermanAppsDB`.
2. Run `database/HgermanAppsDB_ContentStudio.sql` against that database.
3. Open `Hgerman.ContentStudio.sln` in Visual Studio 2022.
4. Restore NuGet packages.
5. Install FFmpeg and make sure `ffmpeg.exe` is in PATH, or point `Ffmpeg:ExecutablePath` to the full path.
6. Update connection strings and API settings in:
   - `src/Hgerman.ContentStudio.Web/appsettings.json`
   - `src/Hgerman.ContentStudio.Worker/appsettings.json`
7. Set `Hgerman.ContentStudio.Web` as the startup project and run it.
8. Run the worker project separately.

## Important
- If `Storage:ConnectionString` is empty, the app stores generated files locally.
- If `AiProviders:OpenAiApiKey` is empty, the app falls back to deterministic placeholders for text, image, and audio where possible.
- Final MP4 rendering requires FFmpeg and a real MP3 voice asset.

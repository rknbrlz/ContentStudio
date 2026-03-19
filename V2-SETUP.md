# Hgerman Content Studio V2 Setup

## Required
- Visual Studio 2022
- .NET 8 SDK
- FFmpeg
- Azure SQL Database: `HgermanAppsDB`

## Configuration checklist
1. Run SQL script in `database/HgermanAppsDB_ContentStudio.sql`.
2. Add Azure SQL connection string to both appsettings files.
3. Add your OpenAI API key to:
   - `AiProviders:OpenAiApiKey`
4. Optionally set:
   - `AiProviders:OpenAiProject`
   - `Storage:ConnectionString`
5. Set local folders:
   - `Storage:LocalRootPath`
   - `Ffmpeg:WorkingDirectory`
6. Install FFmpeg and verify:
   - `ffmpeg -version`

## Flow
1. Create a video job in the web app
2. Queue it
3. Run the worker
4. Worker will generate:
   - script
   - scenes
   - image prompts
   - images
   - voice
   - subtitles
   - final mp4

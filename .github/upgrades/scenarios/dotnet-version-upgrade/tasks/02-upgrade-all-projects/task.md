# 02-upgrade-all-projects: Update TFMs and NuGet packages

Update the `TargetFramework` property from `net8.0` to `net10.0` in all 8 projects. Then update all NuGet package references to versions compatible with net10.0. Skip any incompatible packages that have no net10.0-compatible release — document those as known blockers.

Projects:
- `TelegramMultiBot.Database` (foundation library)
- `DtekParsers`, `ImageDownloader`, `VideoDownloader` (class libraries)
- `TelegramMultiBot` (console app)
- `ConfigUI`, `SdHostApi` (ASP.NET Core apps)
- `BotTests` (test project)

**Done when**: All project files target net10.0; all package references updated to compatible versions; solution restores and builds with 0 errors.

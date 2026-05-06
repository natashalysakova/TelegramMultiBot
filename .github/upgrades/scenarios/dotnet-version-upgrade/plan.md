# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade all projects from net8.0 to net10.0
**Scope**: 8 projects (1 class library shared base, 3 class libraries, 2 ASP.NET Core apps, 1 console app, 1 test project)

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 8 projects, all on net8.0, upgrade scope limited to TFM bumps and NuGet package updates only (no code changes).

## Tasks

### 01-prerequisites: Validate .NET 10 SDK

Verify that the .NET 10 SDK is installed and that any global.json files are compatible with net10.0. This ensures the build toolchain can target net10.0 before any project files are modified.

**Done when**: .NET 10 SDK confirmed installed; global.json (if present) allows net10.0 toolchain.

---

### 02-upgrade-all-projects: Update TFMs and NuGet packages

Update the `TargetFramework` property from `net8.0` to `net10.0` in all 8 projects. Then update all NuGet package references to versions compatible with net10.0. Skip any incompatible packages that have no net10.0-compatible release — document those as known blockers.

Projects:
- `TelegramMultiBot.Database` (foundation library)
- `DtekParsers`, `ImageDownloader`, `VideoDownloader` (class libraries)
- `TelegramMultiBot` (console app)
- `ConfigUI`, `SdHostApi` (ASP.NET Core apps)
- `BotTests` (test project)

**Done when**: All project files target net10.0; all package references updated to compatible versions; solution restores and builds with 0 errors.

---

### 03-validate: Build and test

Restore dependencies, build the full solution, and run the test suite to confirm the upgrade is stable.

**Done when**: `dotnet build` succeeds with 0 errors; all tests pass (or pre-existing failures are documented).

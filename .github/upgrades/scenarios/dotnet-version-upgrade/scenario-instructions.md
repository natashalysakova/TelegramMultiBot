## Strategy
**Selected**: All-At-Once
**Rationale**: 8 projects, all net8.0, upgrade limited to TFM + NuGet package updates only (no code changes).

### Execution Constraints
- Single atomic upgrade — all projects updated together
- Only update TargetFramework and NuGet package versions — no code changes
- Validate full solution build after upgrade
- Document any packages with no net10.0-compatible version as known blockers

## Preferences

### Flow Mode
Automatic

### Target Framework
net10.0

### Commit Strategy
Single Commit at End

### Source Control
- Source branch: `image-downloader`
- Working branch: `upgrade-to-NET10`

## Decisions
- No code changes — only TFM and NuGet package updates per user instruction

## Custom Instructions
<!-- Task-specific overrides: "For {taskId}: {instruction}" -->

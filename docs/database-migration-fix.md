# Database Migration Issue Fix

## Problem Description (问题描述)

When a character has a scheduled battle task (计划任务) configured, closing VS debugging and reopening causes the database to fail, preventing the server from starting and the frontend from connecting.

当角色设置了战斗的计划任务之后，如果关闭VS调试再打开，数据库会异常，服务器无法正常启动，前端也无法连接服务器。

## Root Cause (根本原因)

The issue was caused by a database schema mismatch:

1. The `ActivityPlan` entity model includes a `BattleStateJson` property
2. The database table already had the `BattleStateJson` column (added manually or directly)
3. However, there was NO proper EF Core migration recorded in the migration history to account for this column
4. There were duplicate migration files in a wrong directory: `Infrastructure/Persistence/Migrations/` 
5. EF Core's expected migration directory is: `Migrations/`

When the application restarted and tried to run `db.Database.Migrate()`, EF Core detected a mismatch between:
- The model snapshot (which includes `BattleStateJson`)
- The database schema (which has the column)
- The migration history (which doesn't record the column addition)

This mismatch caused the database migration system to fail, preventing the server from starting.

## Solution (解决方案)

The fix involved three steps:

### 1. Remove Duplicate Migration Files (删除重复的迁移文件)

Removed all duplicate migration files from the wrong directory:
```
BlazorIdle.Server/Infrastructure/Persistence/Migrations/
```

These files included:
- `20250107000000_AddBattleStateToActivityPlan.cs` (incorrect timestamp, wrong location)
- And 25 other duplicate migration files

### 2. Create Proper Migration (创建正确的迁移)

Created a new migration with the correct timestamp and location:
```
BlazorIdle.Server/Migrations/20251016072539_AddBattleStateToActivityPlan.cs
```

This migration is intentionally empty because the column already exists in the database. Its purpose is to record in the migration history that the `BattleStateJson` column has been accounted for.

### 3. Apply Migration (应用迁移)

Applied the migration to update the `__EFMigrationsHistory` table:
```bash
dotnet ef database update
```

## Verification (验证)

After the fix:
1. ✅ The server starts successfully on first run
2. ✅ The server restarts successfully multiple times
3. ✅ All ActivityPlan queries work correctly, including the `BattleStateJson` column
4. ✅ All 20 ActivityPlan-related tests pass
5. ✅ The database schema remains consistent with the EF Core model

## Files Changed (修改的文件)

- **Removed**: 26 duplicate migration files from `Infrastructure/Persistence/Migrations/`
- **Added**: `Migrations/20251016072539_AddBattleStateToActivityPlan.cs` (empty migration)
- **Added**: `Migrations/20251016072539_AddBattleStateToActivityPlan.Designer.cs`
- **Updated**: Database migration history table (`__EFMigrationsHistory`)

## Prevention (预防措施)

To prevent similar issues in the future:

1. **Always use EF Core tools** to add migrations:
   ```bash
   dotnet ef migrations add MigrationName
   ```

2. **Never manually modify the database schema** without creating a corresponding migration

3. **Keep migration files in the correct directory**: `Migrations/` (at project root level)

4. **Don't create multiple Migrations directories** in subdirectories

5. **Verify migration history** after any database schema changes:
   ```bash
   dotnet ef migrations list
   ```

## Technical Details (技术细节)

- **Entity**: `ActivityPlan`
- **Column**: `BattleStateJson` (nullable string/TEXT)
- **Purpose**: Stores battle state snapshot for offline/online seamless switching
- **Database**: SQLite (`gamedata.db`)
- **EF Core Version**: 9.0.9
- **Migration Tool**: Entity Framework Core CLI

# Activity Planning System - Operation Guide

## Quick Reference

### System Health Indicators

✅ **Healthy System**:
- ActivityHostedService running and logging every 1-10 seconds
- Plans transitioning from Pending → Running → Completed
- No "Failed to start" errors in logs
- Thread pool thread count stable

❌ **Unhealthy System**:
- Plans stuck in Running state for abnormally long time
- Frequent "Failed to start activity plan" errors
- Queued plans not starting
- Thread pool thread count growing continuously

## Common Scenarios

### 1. Battle Taking Longer Than Expected

**Symptom**: Battle plan shows Running but progress is slow

**Diagnosis**:
```bash
# Check battle coordinator status
grep "AdvanceAll" application.log | tail -20

# Check if StepBattleHostedService is running
grep "StepBattleHostedService" application.log | tail -10
```

**Resolution**:
- This is normal for long battles (1+ hours)
- Progress is checked every 1 second by default
- No action needed unless plan is truly stuck (no progress for 5+ minutes)

### 2. Plan Stuck in Running State

**Symptom**: Plan.State = Running but no progress for > 5 minutes

**Diagnosis**:
```bash
# Check for errors
grep "Error advancing activity plan" application.log | tail -20

# Check if underlying battle exists
# Look for the battleId in StepBattleCoordinator logs
```

**Resolution**:
1. Cancel the stuck plan via API:
   ```http
   POST /api/activities/plans/{planId}/cancel
   ```
2. Check database connectivity
3. Check if StepBattleCoordinator has the battle instance
4. Review error logs for root cause

### 3. Queued Plans Not Starting

**Symptom**: Current plan completed but next plan still in Pending

**Diagnosis**:
```bash
# Check if ActivityHostedService is running
grep "ActivityHostedService" application.log | tail -10

# Check for startup errors
grep "Failed to start activity plan" application.log | tail -20
```

**Resolution**:
1. Check application logs for startup failures
2. Verify database connectivity
3. Ensure character data exists
4. Check if TryStartPendingPlansAsync is being called

### 4. High CPU Usage

**Symptom**: Server CPU at 100%, slow response times

**Diagnosis**:
```bash
# Check number of active battles
# Count Running plans in database

# Check advance interval configuration
grep "AdvanceIntervalSeconds" appsettings.json
```

**Resolution**:
1. **If advance interval is too low (< 0.5s)**:
   - Increase to 1.0 seconds or higher
   - Edit `appsettings.json`:
     ```json
     {
       "Activity": {
         "AdvanceIntervalSeconds": 2.0
       }
     }
     ```
   - Restart application

2. **If too many concurrent battles**:
   - Limit concurrent plans per character
   - Consider adding rate limiting

### 5. Memory Growing Over Time

**Symptom**: Application memory usage continuously increases

**Diagnosis**:
```bash
# Check completed plans cleanup
grep "Pruned.*completed activity plans" application.log | tail -10

# Check if cleanup is running
grep "PruneCompletedPlans" application.log | tail -10
```

**Resolution**:
1. Verify PruneIntervalMinutes is set (default: 10 minutes)
2. Check if plans are properly transitioning to Completed state
3. Consider lowering cleanup threshold:
   ```json
   {
     "Activity": {
       "PruneIntervalMinutes": 5.0
     }
   }
   ```

## Configuration Reference

### appsettings.json

```json
{
  "Activity": {
    "AdvanceIntervalSeconds": 1.0,    // How often to check plan progress
    "PruneIntervalMinutes": 10.0,     // How often to cleanup completed plans
    "SlotsPerCharacter": 3            // Number of activity slots per character
  },
  "Combat": {
    "EnablePeriodicRewards": true,
    "RewardFlushIntervalSeconds": 10.0
  }
}
```

### Tuning Guidelines

**Low Activity System** (< 10 concurrent battles):
```json
{
  "Activity": {
    "AdvanceIntervalSeconds": 0.5,
    "PruneIntervalMinutes": 5.0
  }
}
```

**High Activity System** (> 100 concurrent battles):
```json
{
  "Activity": {
    "AdvanceIntervalSeconds": 5.0,
    "PruneIntervalMinutes": 30.0
  }
}
```

**Memory-Constrained System**:
```json
{
  "Activity": {
    "AdvanceIntervalSeconds": 2.0,
    "PruneIntervalMinutes": 3.0,
    "SlotsPerCharacter": 2
  }
}
```

## Monitoring Checklist

### Daily Checks

- [ ] ActivityHostedService is running
- [ ] No abnormal error spikes in logs
- [ ] Completed plans are being pruned
- [ ] Average plan duration is within expected range

### Weekly Checks

- [ ] Review slow query logs for database bottlenecks
- [ ] Check thread pool statistics
- [ ] Analyze plan success/failure rates
- [ ] Review average battle completion times

### Monthly Checks

- [ ] Review configuration for optimization opportunities
- [ ] Analyze peak load periods
- [ ] Check for memory leaks or growing resource usage
- [ ] Update documentation for any config changes

## Log Queries

### Count Plans by State
```bash
grep "State:" application.log | awk '{print $NF}' | sort | uniq -c
```

### Find Slow Advances
```bash
grep "AdvanceAllAsync" application.log | grep -oP "took \K[0-9]+ms" | sort -n | tail -20
```

### Track Plan Lifecycle
```bash
grep "plan.*{planId}" application.log | grep -E "(Created|Started|Completed|Failed)"
```

### Monitor Errors
```bash
grep -E "(Error|Exception)" application.log | grep -i "activity" | tail -50
```

## API Endpoints

### Get Character Slots
```http
GET /api/activities/characters/{characterId}/slots
```

### Get Plan Details
```http
GET /api/activities/plans/{planId}
```

### Create Combat Plan
```http
POST /api/activities/plans
Content-Type: application/json

{
  "characterId": "guid",
  "slotIndex": 0,
  "type": "combat",
  "limitType": "duration",
  "limitValue": 3600,
  "payloadJson": "{\"enemyId\":\"dummy\",\"enemyCount\":1,\"mode\":\"duration\"}"
}
```

### Cancel Plan
```http
POST /api/activities/plans/{planId}/cancel
```

## Emergency Procedures

### System Appears Frozen

1. **Check if services are running**:
   ```bash
   grep "HostedService" application.log | tail -5
   ```

2. **Check database connectivity**:
   ```bash
   # Try to connect to database
   sqlite3 gamedata.db "SELECT COUNT(*) FROM Characters;"
   ```

3. **Check thread pool**:
   - If thread pool exhaustion suspected, restart application
   - After restart, review logs for recurring patterns

4. **Kill stuck plans** (last resort):
   ```sql
   -- Mark all running plans as cancelled
   UPDATE ActivityPlans 
   SET State = 3, EndedAtUtc = datetime('now') 
   WHERE State = 1;
   ```

### Database Locked

**Symptom**: "Database is locked" errors

**Resolution**:
1. Check for long-running transactions
2. Ensure Write-Ahead Logging (WAL) is enabled:
   ```sql
   PRAGMA journal_mode=WAL;
   ```
3. Consider connection pooling settings
4. May need to increase database timeout

### Memory Exhaustion

**Symptom**: OutOfMemoryException

**Resolution**:
1. **Immediate**: Restart application
2. **Short-term**: Reduce PruneIntervalMinutes to 1-2 minutes
3. **Long-term**: 
   - Investigate memory leaks
   - Consider plan persistence to disk
   - Implement battle result caching

## Performance Baselines

### Expected Metrics

| Metric | Good | Warning | Critical |
|--------|------|---------|----------|
| AdvanceAllAsync duration | < 50ms | 50-200ms | > 200ms |
| Plan start success rate | > 99% | 95-99% | < 95% |
| Battle completion rate | > 98% | 90-98% | < 90% |
| Memory growth per hour | < 50MB | 50-200MB | > 200MB |
| Thread pool threads | 8-16 | 16-32 | > 32 |

## Troubleshooting Decision Tree

```
Plan not progressing?
├── Is ActivityHostedService running?
│   ├── No → Check application startup logs
│   └── Yes → Continue
├── Is plan in Running state?
│   ├── No → Check why it's not starting
│   │   └── Check "Failed to start" errors
│   └── Yes → Continue
├── Does underlying battle exist?
│   ├── No → Battle may have been pruned
│   │   └── Cancel plan and restart
│   └── Yes → Continue
├── Is battle progressing?
│   ├── No → Check StepBattleHostedService
│   └── Yes → Wait, system is working normally
└── Is progress updating in plan?
    ├── No → Check CombatActivityExecutor.AdvanceAsync
    └── Yes → System is healthy
```

## Contact and Escalation

### Self-Service Resources
1. Check this operation guide
2. Review application logs
3. Check GitHub issues for similar problems

### When to Escalate
- System completely frozen for > 5 minutes
- Data corruption suspected
- Memory exhaustion recurring daily
- Critical production impact

### Information to Provide
1. Application logs (last 1000 lines)
2. Current configuration (appsettings.json)
3. Database size and number of plans
4. System resources (CPU, memory, threads)
5. Recent changes or deployments

---

**Document Version**: 1.0  
**Last Updated**: 2024  
**Maintained By**: Development Team

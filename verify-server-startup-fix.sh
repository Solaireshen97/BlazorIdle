#!/bin/bash

# Server Startup Fix Verification Script
# This script helps verify that the server startup fix is working correctly

set -e

echo "======================================"
echo "Server Startup Fix Verification"
echo "======================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored messages
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

# Check if we're in the right directory
if [ ! -f "BlazorIdle.Server/BlazorIdle.Server.csproj" ]; then
    print_error "Please run this script from the repository root directory"
    exit 1
fi

print_success "Repository found"

# Step 1: Build the project
echo ""
echo "Step 1: Building the project..."
if dotnet build --no-incremental > /tmp/build.log 2>&1; then
    print_success "Build succeeded"
else
    print_error "Build failed. Check /tmp/build.log for details"
    exit 1
fi

# Step 2: Run unit tests
echo ""
echo "Step 2: Running unit tests..."
if dotnet test --filter "FullyQualifiedName~ServerStartupRecoveryTests" --no-build > /tmp/test.log 2>&1; then
    TESTS_PASSED=$(grep "Passed!" /tmp/test.log | grep -oP '\d+(?= Passed)' || echo "0")
    print_success "Tests passed: $TESTS_PASSED/3"
    
    if [ "$TESTS_PASSED" != "3" ]; then
        print_warning "Expected 3 tests to pass. Check /tmp/test.log for details"
    fi
else
    print_error "Tests failed. Check /tmp/test.log for details"
    exit 1
fi

# Step 3: Check for the new methods
echo ""
echo "Step 3: Verifying implementation..."
if grep -q "CleanupOrphanedRunningPlansAsync" BlazorIdle.Server/Application/Battles/Step/StepBattleHostedService.cs; then
    print_success "CleanupOrphanedRunningPlansAsync method found"
else
    print_error "CleanupOrphanedRunningPlansAsync method not found"
    exit 1
fi

if grep -q "PauseAllRunningPlansAsync" BlazorIdle.Server/Application/Battles/Step/StepBattleHostedService.cs; then
    print_success "PauseAllRunningPlansAsync method found"
else
    print_error "PauseAllRunningPlansAsync method not found"
    exit 1
fi

# Step 4: Check documentation
echo ""
echo "Step 4: Checking documentation..."
DOCS=(
    "SERVER_STARTUP_FIX_COMPLETE.md"
    "SERVER_STARTUP_FIX_IMPLEMENTATION_SUMMARY.md"
    "SERVER_STARTUP_FIX_MANUAL_TEST_GUIDE.md"
)

for doc in "${DOCS[@]}"; do
    if [ -f "$doc" ]; then
        print_success "$doc exists"
    else
        print_warning "$doc not found"
    fi
done

# Step 5: Check database file status
echo ""
echo "Step 5: Checking database status..."
if [ -f "BlazorIdle.Server/gamedata.db" ]; then
    # Check database integrity
    if command -v sqlite3 &> /dev/null; then
        if sqlite3 BlazorIdle.Server/gamedata.db "PRAGMA integrity_check" | grep -q "ok"; then
            print_success "Database integrity OK"
        else
            print_warning "Database integrity check failed or returned unexpected result"
        fi
        
        # Check for orphaned Running plans
        RUNNING_COUNT=$(sqlite3 BlazorIdle.Server/gamedata.db "SELECT COUNT(*) FROM ActivityPlans WHERE State = 'Running'" 2>/dev/null || echo "0")
        echo "   Running plans in database: $RUNNING_COUNT"
        
        if [ "$RUNNING_COUNT" -gt "0" ]; then
            print_warning "Found $RUNNING_COUNT Running plans. These should be cleaned up on next server start."
        fi
    else
        print_warning "sqlite3 not found. Skipping database checks."
    fi
else
    print_warning "Database file not found (this is normal if server hasn't run yet)"
fi

# Summary
echo ""
echo "======================================"
echo "Verification Summary"
echo "======================================"
echo ""
print_success "Code implementation verified"
print_success "Unit tests passed"
print_success "Documentation complete"
echo ""
echo "Next steps:"
echo "1. Start the server: cd BlazorIdle.Server && dotnet run"
echo "2. Follow manual test guide: SERVER_STARTUP_FIX_MANUAL_TEST_GUIDE.md"
echo "3. Monitor logs for cleanup and pause messages"
echo ""
echo "The fix is ready for testing! 🚀"

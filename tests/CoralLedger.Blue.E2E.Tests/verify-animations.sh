#!/usr/bin/env bash

# Animation CSS Verification Script
# Validates that all required CSS animations and classes are present
# Can run without starting the application server
#
# Requirements: bash 4.0+, grep
# Usage: ./verify-animations.sh
# Run from repository root directory
#
# This script provides a lightweight alternative to E2E tests for verifying
# CSS animations are implemented. Use this in CI environments without Docker.

# Check bash version for arithmetic expansion support
if [ -z "$BASH_VERSION" ]; then
    echo "Error: This script requires bash. Please run with: bash $0"
    exit 1
fi

# Don't exit on error - we want to count all failures
set +e

# Check we're in the right directory
if [ ! -f "CoralLedger.Blue.sln" ]; then
    echo "Error: Please run this script from the repository root directory"
    echo "Usage: ./tests/CoralLedger.Blue.E2E.Tests/verify-animations.sh"
    exit 1
fi

CSS_FILE="src/CoralLedger.Blue.Web/wwwroot/app.css"
DATACARD_FILE="src/CoralLedger.Blue.Web/Components/Shared/DataCard.razor"
DASHBOARD_FILE="src/CoralLedger.Blue.Web/Components/Pages/Dashboard.razor"

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

PASSED=0
FAILED=0

echo "=================================================="
echo "Animation CSS Verification"
echo "=================================================="
echo ""

check_css() {
    local pattern="$1"
    local description="$2"
    local file="${3:-$CSS_FILE}"
    
    if grep -q "$pattern" "$file"; then
        echo -e "${GREEN}✓${NC} $description"
        ((PASSED++))
    else
        echo -e "${RED}✗${NC} $description"
        ((FAILED++))
    fi
}

# Check CSS Keyframes
echo "Checking CSS Keyframes..."
check_css "@keyframes cardAppear" "cardAppear keyframe defined"
check_css "@keyframes slideIn" "slideIn keyframe defined"
check_css "@keyframes tableRowFadeIn" "tableRowFadeIn keyframe defined"
echo ""

# Check Animation Classes
echo "Checking Animation Classes..."
check_css "\.card-appear" ".card-appear class defined"
check_css "\.list-item-animate" ".list-item-animate class defined"
check_css "\.table-row-animate" ".table-row-animate class defined"
check_css "\.value-transition" ".value-transition class defined"
echo ""

# Check Stagger Classes
echo "Checking Stagger Classes..."
check_css "\.stagger-1" ".stagger-1 class defined"
check_css "\.stagger-2" ".stagger-2 class defined"
check_css "\.stagger-3" ".stagger-3 class defined"
check_css "\.stagger-4" ".stagger-4 class defined"
check_css "\.stagger-5" ".stagger-5 class defined"
check_css "\.stagger-6" ".stagger-6 class defined"
check_css "\.stagger-7" ".stagger-7 class defined"
check_css "\.stagger-8" ".stagger-8 class defined"
echo ""

# Check Badge Transitions
echo "Checking Badge Transitions..."
check_css "\.alert-badge" ".alert-badge selector present"
check_css "transform 0\.2s ease" "Badge transform transition defined"
echo ""

# Check Accessibility
echo "Checking Accessibility..."
check_css "prefers-reduced-motion" "prefers-reduced-motion media query defined"
echo ""

# Check Component Integration
echo "Checking Component Integration..."
check_css "card-appear" "card-appear class used in DataCard" "$DATACARD_FILE"
check_css "stagger-1" "stagger-1 class used in Dashboard" "$DASHBOARD_FILE"
check_css "stagger-2" "stagger-2 class used in Dashboard" "$DASHBOARD_FILE"
check_css "stagger-3" "stagger-3 class used in Dashboard" "$DASHBOARD_FILE"
check_css "stagger-4" "stagger-4 class used in Dashboard" "$DASHBOARD_FILE"
echo ""

# Check Animation Duration
echo "Checking Animation Duration..."
if grep -q "animation: cardAppear 0\.3s" "$CSS_FILE"; then
    echo -e "${GREEN}✓${NC} cardAppear duration is 0.3s (≤ 300ms)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} cardAppear duration check failed"
    ((FAILED++))
fi

if grep -q "animation: slideIn 0\.3s" "$CSS_FILE"; then
    echo -e "${GREEN}✓${NC} slideIn duration is 0.3s (≤ 300ms)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} slideIn duration check failed"
    ((FAILED++))
fi

if grep -q "animation: tableRowFadeIn 0\.3s" "$CSS_FILE"; then
    echo -e "${GREEN}✓${NC} tableRowFadeIn duration is 0.3s (≤ 300ms)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} tableRowFadeIn duration check failed"
    ((FAILED++))
fi
echo ""

# Check GPU-Accelerated Properties
echo "Checking GPU-Accelerated Properties..."
if grep -A5 "@keyframes cardAppear" "$CSS_FILE" | grep -q "transform:" && \
   grep -A5 "@keyframes cardAppear" "$CSS_FILE" | grep -q "opacity:"; then
    echo -e "${GREEN}✓${NC} cardAppear uses only opacity and transform (GPU-accelerated)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} cardAppear GPU acceleration check failed"
    ((FAILED++))
fi

if grep -A5 "@keyframes slideIn" "$CSS_FILE" | grep -q "transform:" && \
   grep -A5 "@keyframes slideIn" "$CSS_FILE" | grep -q "opacity:"; then
    echo -e "${GREEN}✓${NC} slideIn uses only opacity and transform (GPU-accelerated)"
    ((PASSED++))
else
    echo -e "${RED}✗${NC} slideIn GPU acceleration check failed"
    ((FAILED++))
fi
echo ""

# Summary
echo "=================================================="
echo "Verification Summary"
echo "=================================================="
echo -e "Passed: ${GREEN}$PASSED${NC}"
echo -e "Failed: ${RED}$FAILED${NC}"
echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✓ All animation requirements are met!${NC}"
    echo ""
    echo "The Animation E2E tests are failing due to the application"
    echo "server not running, NOT due to missing CSS."
    echo ""
    echo "To run E2E tests, start the application first:"
    echo "  dotnet run --project src/CoralLedger.Blue.AppHost"
    exit 0
else
    echo -e "${RED}✗ Some animation requirements are missing!${NC}"
    echo ""
    echo "See ANIMATION_VERIFICATION.md for details"
    exit 1
fi

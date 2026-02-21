#!/usr/bin/env bash
set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Find project root directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "CrossMacro - NuGet Dependencies Update Script"
echo "=============================================="
echo ""

# Navigate to root directory
cd "$PROJECT_ROOT"

# Check required tools
echo -e "${BLUE}Checking required tools...${NC}"
MISSING_TOOLS=0

if ! command -v jq &> /dev/null; then
    echo -e "${RED}Error: 'jq' is not installed.${NC}"
    echo "  Install: nix-shell -p jq"
    MISSING_TOOLS=1
fi

if ! command -v nix-prefetch-url &> /dev/null; then
    echo -e "${RED}Error: 'nix-prefetch-url' not found. Is Nix installed?${NC}"
    MISSING_TOOLS=1
fi

if ! command -v nix-hash &> /dev/null; then
    echo -e "${RED}Error: 'nix-hash' not found. Is Nix installed?${NC}"
    MISSING_TOOLS=1
fi

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: 'dotnet' is not installed.${NC}"
    MISSING_TOOLS=1
fi

if [ $MISSING_TOOLS -eq 1 ]; then
    exit 1
fi

echo -e "${GREEN}✓ All tools found${NC}"
echo ""

# Restore projects used by flake targets (Linux + macOS UI hosts, plus daemon)
echo -e "${BLUE}Restoring projects...${NC}"
PROJECTS=(
  "src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj"
  "src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj"
  "src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"
)

for project in "${PROJECTS[@]}"; do
    echo "  restoring: $project"
    dotnet restore "$project" /p:RestoreUseStaticGraphEvaluation=false /m:1
done

# Collect project.assets.json files
ASSETS_FILES=(
  "src/CrossMacro.UI.Linux/obj/project.assets.json"
  "src/CrossMacro.UI.MacOS/obj/project.assets.json"
  "src/CrossMacro.Daemon/obj/project.assets.json"
)

for assets_file in "${ASSETS_FILES[@]}"; do
    if [ ! -f "$assets_file" ]; then
        echo -e "${RED}Error: $assets_file not found after restore${NC}"
        exit 1
    fi
    echo -e "${GREEN}✓ Found: $assets_file${NC}"
done

echo ""

# Generate deps.json (new format - recommended by nixpkgs)
echo -e "${BLUE}Generating deps.json...${NC}"

# Create temporary file for atomic write
TEMP_DEPS=$(mktemp)
trap "rm -f $TEMP_DEPS" EXIT

echo "[" > "$TEMP_DEPS"

# Extract packages from all assets files
# Exclude SDK-provided toolchain packages that are already injected by nixpkgs
# buildDotnetModule (adding them to deps.json causes duplicate fallback links).
mapfile -t PACKAGES < <(
  for assets_file in "${ASSETS_FILES[@]}"; do
    jq -r '
      def excluded: [
        "Microsoft.NET.ILLink.Tasks",
        "Microsoft.DotNet.ILCompiler"
      ];
      .libraries
      | to_entries[]
      | select(.value.type == "package")
      | (.key | split("/") | .[0]) as $name
      | select((excluded | index($name)) | not)
      | "\(.key)"
    ' "$assets_file"
  done | sort -u
)

TOTAL=${#PACKAGES[@]}
CURRENT=0
FAILED=0
FIRST=1

echo "Found $TOTAL NuGet packages to process"
echo ""

# Process each package
for package in "${PACKAGES[@]}"; do
    CURRENT=$((CURRENT + 1))

    # Split package name and version
    IFS='/' read -r name version <<< "$package"

    # Skip if version is empty
    if [ -z "$version" ]; then
        echo -e "${YELLOW}[$CURRENT/$TOTAL] Skipping: $name (no version)${NC}"
        continue
    fi

    echo -n "[$CURRENT/$TOTAL] Fetching: $name/$version ... "

    # Try lowercase name first (standard NuGet behavior)
    name_lower=$(echo "$name" | tr '[:upper:]' '[:lower:]')
    url="https://api.nuget.org/v3-flatcontainer/${name_lower}/${version}/${name_lower}.${version}.nupkg"

    # Fetch hash and convert to SRI format
    if hash=$(nix-prefetch-url --type sha256 "$url" 2>/dev/null); then
        # Convert base32 hash to SRI format (sha256-base64)
        sri_hash=$(nix-hash --type sha256 --to-sri "$hash")

        echo -e "${GREEN}✓${NC}"
        
        # Add comma before entry (except first)
        if [ $FIRST -eq 1 ]; then
            FIRST=0
        else
            echo "," >> "$TEMP_DEPS"
        fi
        
        # JSON format for deps.json
        cat >> "$TEMP_DEPS" <<EOF
  {
    "pname": "${name}",
    "version": "${version}",
    "hash": "${sri_hash}"
  }
EOF
    else
        echo -e "${RED}✗${NC}"
        FAILED=$((FAILED + 1))

        # Try alternative URL with original casing
        echo -n "    Retrying with original casing ... "
        url_alt="https://api.nuget.org/v3-flatcontainer/${name}/${version}/${name}.${version}.nupkg"

        if hash=$(nix-prefetch-url --type sha256 "$url_alt" 2>/dev/null); then
            sri_hash=$(nix-hash --type sha256 --to-sri "$hash")
            echo -e "${GREEN}✓${NC}"
            
            if [ $FIRST -eq 1 ]; then
                FIRST=0
            else
                echo "," >> "$TEMP_DEPS"
            fi
            
            cat >> "$TEMP_DEPS" <<EOF
  {
    "pname": "${name}",
    "version": "${version}",
    "hash": "${sri_hash}"
  }
EOF
            FAILED=$((FAILED - 1))
        else
            echo -e "${RED}✗ FAILED${NC}"
            echo -e "${YELLOW}    Warning: Could not fetch $name/$version${NC}"
        fi
    fi
done

echo "" >> "$TEMP_DEPS"
echo "]" >> "$TEMP_DEPS"

# Move temp file to final location only if successful
mv "$TEMP_DEPS" deps.json

echo ""
if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}✓ deps.json successfully generated! ($CURRENT packages)${NC}"
else
    echo -e "${YELLOW}⚠ deps.json generated with $FAILED failures out of $CURRENT packages${NC}"
fi

# Validate deps.json
echo ""
echo -e "${BLUE}Validating deps.json...${NC}"

# Check JSON syntax
if ! jq empty deps.json 2>/dev/null; then
    echo -e "${RED}✗ deps.json has syntax errors${NC}"
    echo ""
    echo "Parse error details:"
    jq . deps.json 2>&1 | head -20
    exit 1
fi

PKG_COUNT=$(jq 'length' deps.json)
echo -e "${GREEN}✓ JSON syntax is valid ($PKG_COUNT packages)${NC}"

# Show summary
echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Summary:"
echo "  Total packages: $CURRENT"
echo "  Failed: $FAILED"
echo "  Output: deps.json ($PKG_COUNT packages)"
echo ""
echo "Note: deps.json now includes Linux host + macOS host + daemon dependencies."
echo "      Windows-specific host dependencies are intentionally excluded from this script."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo -e "${GREEN}✓ Done! You can now run:${NC}"
echo "  nix build -L"
echo ""

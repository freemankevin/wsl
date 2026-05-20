#!/usr/bin/env bash
#
# WSL Manager - Development Startup Script
# Usage: ./startup.sh [options]
#   Options:
#     --no-restore    Skip dependency restore
#     --no-build      Skip syntax check build
#     --no-kill       Skip port/process cleanup
#     --port PORT     Specify port to check/clean (default: auto-detect)
#

set -euo pipefail

# ============================================
# Configuration
# ============================================
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SLN_FILE="${SCRIPT_DIR}/WSLManager.sln"
MAIN_PROJECT="${SCRIPT_DIR}/src/WSLManager"
PROJECT_NAME="WSLManager"

# Optional: override via DEV_PORT env var or --port argument
DEV_PORT="${DEV_PORT:-}"

# Parse arguments
SKIP_RESTORE=false
SKIP_BUILD=false
SKIP_KILL=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-restore) SKIP_RESTORE=true; shift ;;
        --no-build)   SKIP_BUILD=true; shift ;;
        --no-kill)    SKIP_KILL=true; shift ;;
        --port)       DEV_PORT="$2"; shift 2 ;;
        *)            echo "Unknown option: $1"; exit 1 ;;
    esac
done

# ============================================
# Colors & Styles
# ============================================
if [[ -t 1 ]]; then
    RED='\033[0;31m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    BLUE='\033[0;34m'
    CYAN='\033[0;36m'
    MAGENTA='\033[0;35m'
    BOLD='\033[1m'
    DIM='\033[2m'
    NC='\033[0m'
else
    RED=''; GREEN=''; YELLOW=''; BLUE=''; CYAN=''; MAGENTA=''; BOLD=''; DIM=''; NC=''
fi

# ============================================
# Logging Utilities
# ============================================
log_header() {
    echo -e "\n${BOLD}${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║${NC}  $1${NC}"
    echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
}

log_step() {
    echo -e "\n${BOLD}${BLUE}▶${NC} ${BOLD}$1${NC}"
}

log_info() {
    echo -e "  ${BLUE}ℹ${NC}  $1"
}

log_success() {
    echo -e "  ${GREEN}✔${NC}  $1"
}

log_warn() {
    echo -e "  ${YELLOW}⚠${NC}  $1"
}

log_error() {
    echo -e "  ${RED}✖${NC}  $1"
}

log_detail() {
    echo -e "     ${DIM}$1${NC}"
}

# ============================================
# System Detection
# ============================================
detect_system() {
    log_step "System Detection"

    # OS
    local os="Unknown"
    if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || "${OS:-}" == "Windows_NT" ]]; then
        os="Windows"
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        os="Linux"
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        os="macOS"
    fi

    # Architecture
    local arch
    arch="$(uname -m 2>/dev/null || echo "${PROCESSOR_ARCHITECTURE:-unknown}")"
    case "$arch" in
        x86_64|AMD64|amd64)   arch="x64" ;;
        arm64|aarch64|ARM64)  arch="arm64" ;;
        i386|i686|x86)        arch="x86" ;;
        *)                    arch="unknown" ;;
    esac

    log_info "Operating System: ${BOLD}$os${NC}"
    log_info "Architecture:     ${BOLD}$arch${NC}"
    log_info "Working Dir:      ${BOLD}$(pwd)${NC}"
}

# ============================================
# Dependency Check & Install
# ============================================
check_dotnet() {
    log_step "Checking .NET SDK"

    if ! command -v dotnet &>/dev/null; then
        log_error ".NET SDK not found!"
        log_detail "Install from: https://dotnet.microsoft.com/download"
        log_detail "Required: .NET 8 SDK or later"
        exit 1
    fi

    local sdk_version
    sdk_version="$(dotnet --version)"
    log_success "Found .NET SDK ${BOLD}$sdk_version${NC}"

    # Check if .NET 8+ is installed
    local major_version
    major_version="$(echo "$sdk_version" | cut -d. -f1)"
    if [[ "$major_version" -lt 8 ]]; then
        log_warn ".NET 8+ is recommended. Current: $sdk_version"
    fi
}

install_dependencies() {
    if [[ "$SKIP_RESTORE" == true ]]; then
        log_step "Skipping dependency restore (--no-restore)"
        return
    fi

    log_step "Restoring NuGet packages"

    dotnet restore "$SLN_FILE" \
        --verbosity minimal

    log_success "Dependencies restored"
}

# ============================================
# Syntax Check
# ============================================
check_syntax() {
    if [[ "$SKIP_BUILD" == true ]]; then
        log_step "Skipping syntax check (--no-build)"
        return
    fi

    log_step "Checking code for syntax errors"

    if dotnet build "$MAIN_PROJECT" \
        --no-restore \
        --verbosity minimal \
        -p:TreatWarningsAsErrors=false; then
        log_success "Build passed — no syntax errors"
    else
        log_error "Build failed — syntax or compilation errors detected"
        exit 1
    fi
}

# ============================================
# Port Conflict Resolution
# ============================================
cleanup_ports() {
    if [[ "$SKIP_KILL" == true ]]; then
        log_step "Skipping port cleanup (--no-kill)"
        return
    fi

    if [[ -z "$DEV_PORT" ]]; then
        log_step "Port cleanup (no DEV_PORT configured, skipping)"
        log_info "Set DEV_PORT env var or use --port to enable auto-cleanup"
        return
    fi

    log_step "Checking port ${BOLD}$DEV_PORT${NC} for conflicts"

    local pid=""

    # Windows (Git Bash / MSYS)
    if command -v netstat &>/dev/null && command -v taskkill &>/dev/null; then
        pid="$(netstat -ano 2>/dev/null | grep ":$DEV_PORT" | grep LISTENING | awk '{print $NF}' | head -1 || true)"

        if [[ -n "$pid" && "$pid" != "0" ]]; then
            log_warn "Port $DEV_PORT is occupied by PID $pid"
            log_info "Killing process $pid..."

            if taskkill //F //PID "$pid" &>/dev/null; then
                sleep 1
                log_success "Port $DEV_PORT freed (PID $pid terminated)"
            else
                log_error "Failed to kill PID $pid (may require admin rights)"
            fi
        else
            log_success "Port $DEV_PORT is available"
        fi
    else
        log_warn "netstat/taskkill not found, skipping port cleanup"
    fi
}

# ============================================
# Old Process Cleanup
# ============================================
cleanup_old_instances() {
    if [[ "$SKIP_KILL" == true ]]; then
        return
    fi

    log_step "Checking for old $PROJECT_NAME instances"

    local found=false

    # Windows: find dotnet processes running our project
    if command -v tasklist &>/dev/null && command -v taskkill &>/dev/null; then
        # Get PIDs of dotnet processes, then filter by command line for our project
        local pids
        pids="$(tasklist //FI "IMAGENAME eq dotnet.exe" //FO CSV 2>/dev/null | grep -o '[0-9]\+' | head -20 || true)"

        for pid in $pids; do
            # Try to get command line (wmic is available on most Windows)
            if command -v wmic &>/dev/null; then
                local cmdline
                cmdline="$(wmic process where "ProcessId=$pid" get CommandLine //NOINTERACTIVE 2>/dev/null | grep -i "$PROJECT_NAME" || true)"
                if [[ -n "$cmdline" ]]; then
                    log_warn "Found old instance (PID: $pid)"
                    if taskkill //F //PID "$pid" &>/dev/null; then
                        log_success "Terminated old instance (PID: $pid)"
                        found=true
                    fi
                fi
            fi
        done
    fi

    if [[ "$found" == false ]]; then
        log_success "No old instances found"
    fi
}

# ============================================
# Launch Development Environment
# ============================================
start_dev() {
    log_step "Starting development environment"
    log_info "Project: ${BOLD}$MAIN_PROJECT${NC}"
    log_info "Command: ${BOLD}dotnet run${NC}"

    echo -e "\n${BOLD}${GREEN}────────────────────────────────────────${NC}"
    echo -e "${BOLD}${GREEN}  🚀 Launching $PROJECT_NAME${NC}"
    echo -e "${BOLD}${GREEN}────────────────────────────────────────${NC}\n"

    # Trap Ctrl+C to print a clean exit message
    trap 'echo -e "\n\n${YELLOW}👋 Development session ended.${NC}\n"; exit 0' INT TERM

    dotnet run \
        --project "$MAIN_PROJECT" \
        --verbosity minimal
}

# ============================================
# Main
# ============================================
main() {
    log_header "  WSL Manager - Dev Startup  "

    detect_system
    check_dotnet
    cleanup_ports
    cleanup_old_instances
    install_dependencies
    check_syntax
    start_dev
}

main "$@"

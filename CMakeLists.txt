cmake_minimum_required(VERSION 3.20)
project(Launchpad NONE)

# ---------------------------------------------------------------------------
# CMake orchestrates the build; the .NET SDK does the actual compilation.
#
#   cmake -B build
#   cmake --build build
#
# Platform behaviour (controlled by Launchpad.csproj conditions):
#   Linux   → .NET 10, cross-compiled win-x64, self-contained single file
#   Windows → .NET Framework 4.7.2, small exe (framework pre-installed)
#
# Options:
#   -DCONFIG=Release   (default)  or Debug
#   -DOUTPUT_DIR=path  override publish destination
# ---------------------------------------------------------------------------

set(CONFIG     "Release" CACHE STRING "Build configuration: Debug or Release")
set(OUTPUT_DIR "${CMAKE_BINARY_DIR}/publish" CACHE PATH "Publish output directory")

# ── Find dotnet (soft — configure succeeds; build fails with instructions) ──
find_program(DOTNET_EXE dotnet
    HINTS "$ENV{HOME}/.dotnet" "$ENV{DOTNET_ROOT}"
    DOC "Path to the .NET SDK CLI")

if(NOT DOTNET_EXE)
    set(DOTNET_EXE "dotnet")
    message(WARNING
        "dotnet SDK not found on PATH.\n"
        "Install .NET 10 SDK from https://dot.net/download\n"
        "  Arch/CachyOS:  sudo pacman -S dotnet-sdk-bin\n"
        "  Ubuntu/Debian: sudo apt install dotnet-sdk-10.0\n"
        "  Script:        curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0\n"
        "The build will fail until dotnet is available.")
endif()

# ── Platform-specific publish flags ─────────────────────────────────────────
if(WIN32)
    # Windows: target .NET 4.7.2 — no runtime to bundle, small exe output
    set(DOTNET_PUBLISH_FLAGS "")
    set(PLATFORM_LABEL "Windows (.NET 4.7.2)")
else()
    # Linux: cross-compile to win-x64, self-contained single file
    set(DOTNET_PUBLISH_FLAGS
        -r win-x64
        --self-contained true
        /p:PublishSingleFile=true)
    set(PLATFORM_LABEL "Linux (.NET 10, win-x64 self-contained)")
endif()

# ── Main build target ────────────────────────────────────────────────────────
add_custom_target(Launchpad ALL
    COMMAND "${DOTNET_EXE}" publish
            "${CMAKE_SOURCE_DIR}/Launchpad/Launchpad.csproj"
            -c "${CONFIG}"
            ${DOTNET_PUBLISH_FLAGS}
            -o "${OUTPUT_DIR}"
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Publishing Launchpad [${PLATFORM_LABEL}] → ${OUTPUT_DIR}/Launchpad.exe"
    VERBATIM
)

# ── Clean target ─────────────────────────────────────────────────────────────
add_custom_target(clean_publish
    COMMAND "${CMAKE_COMMAND}" -E rm -rf "${OUTPUT_DIR}"
    COMMENT "Removing publish output at ${OUTPUT_DIR}"
    VERBATIM
)

# ── Install ───────────────────────────────────────────────────────────────────
install(PROGRAMS "${OUTPUT_DIR}/Launchpad.exe"
        DESTINATION ".")

# ── Optional tests ────────────────────────────────────────────────────────────
if(EXISTS "${CMAKE_SOURCE_DIR}/Launchpad.Tests/Launchpad.Tests.csproj")
    add_custom_target(test_launchpad
        COMMAND "${DOTNET_EXE}" test
                "${CMAKE_SOURCE_DIR}/Launchpad.Tests/Launchpad.Tests.csproj"
                -c "${CONFIG}"
        WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
        COMMENT "Running Launchpad.Tests"
        VERBATIM
    )
endif()

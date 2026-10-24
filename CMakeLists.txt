cmake_minimum_required(VERSION 3.20)
project(Launchpad NONE)

# ---------------------------------------------------------------------------
# CMake orchestrates the build; the .NET 8 SDK does the actual compilation.
#
#   cmake -B build
#   cmake --build build
#
# Output: build/publish/Launchpad.exe  (win-x64 self-contained single file)
#
# Options:
#   -DCONFIG=Release   (default)  or Debug
#   -DOUTPUT_DIR=path  override publish destination
# ---------------------------------------------------------------------------

# ── Options ─────────────────────────────────────────────────────────────────
set(CONFIG     "Release" CACHE STRING "Build configuration: Debug or Release")
set(OUTPUT_DIR "${CMAKE_BINARY_DIR}/publish" CACHE PATH "Publish output directory")

# ── Find dotnet (soft — configure succeeds; build fails with instructions) ────
find_program(DOTNET_EXE dotnet
    HINTS "$ENV{HOME}/.dotnet" "$ENV{DOTNET_ROOT}"
    DOC "Path to the .NET 8 SDK CLI")

if(NOT DOTNET_EXE)
    set(DOTNET_EXE "dotnet")   # let the shell resolve it at build time
    message(WARNING
        "dotnet SDK not found on PATH.\n"
        "Install .NET 8 SDK from https://dot.net/download\n"
        "  Arch/CachyOS:  sudo pacman -S dotnet-sdk-8.0\n"
        "  Ubuntu/Debian: sudo apt install dotnet-sdk-8.0\n"
        "  Script:        curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0\n"
        "The build will fail until dotnet is available.")
endif()

# ── Main build target ────────────────────────────────────────────────────────
add_custom_target(Launchpad ALL
    COMMAND "${DOTNET_EXE}" publish
            "${CMAKE_SOURCE_DIR}/Launchpad/Launchpad.csproj"
            -c "${CONFIG}"
            -r win-x64
            --self-contained true
            -o "${OUTPUT_DIR}"
            /p:PublishSingleFile=true
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    COMMENT "Publishing Launchpad (${CONFIG}, win-x64) → ${OUTPUT_DIR}/Launchpad.exe"
    VERBATIM
)

# ── Clean target: remove publish output ──────────────────────────────────────
add_custom_target(clean_publish
    COMMAND "${CMAKE_COMMAND}" -E rm -rf "${OUTPUT_DIR}"
    COMMENT "Removing publish output at ${OUTPUT_DIR}"
    VERBATIM
)

# ── Install: copy exe to CMAKE_INSTALL_PREFIX ────────────────────────────────
install(PROGRAMS "${OUTPUT_DIR}/Launchpad.exe"
        DESTINATION ".")

# ── Optional tests ───────────────────────────────────────────────────────────
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

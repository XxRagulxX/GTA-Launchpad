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

# ── Require dotnet ───────────────────────────────────────────────────────────
find_program(DOTNET_EXE dotnet REQUIRED
    DOC "Path to the .NET 8 SDK CLI (https://dot.net)")

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

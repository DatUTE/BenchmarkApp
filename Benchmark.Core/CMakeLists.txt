cmake_minimum_required(VERSION 3.20)

project(BenchmarkCore VERSION 1.0.0 LANGUAGES CXX)

# ── Compiler requirements ─────────────────────────────────────────────────────
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ── Sources ───────────────────────────────────────────────────────────────────
set(CORE_SOURCES
    src/BenchmarkEngine.cpp
    src/ProcessAccess.cpp
    src/CpuMonitor.cpp
    src/MemoryMonitor.cpp
    src/IoMonitor.cpp
    src/HandleMonitor.cpp
    src/LifetimeMonitor.cpp
    src/exports.cpp
)

set(CORE_HEADERS
    include/BenchmarkCore.h
    include/BenchmarkEngine.h
    include/CpuMonitor.h
    include/HandleGuard.h
    include/HandleMonitor.h
    include/IMonitor.h
    include/IoMonitor.h
    include/LifetimeMonitor.h
    include/MemoryMonitor.h
    include/ProcessMetrics.h
    include/ProcessAccess.h
)

# ── DLL target ────────────────────────────────────────────────────────────────
add_library(Benchmark.Core SHARED ${CORE_SOURCES} ${CORE_HEADERS})

target_include_directories(Benchmark.Core
    PUBLIC  $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_compile_definitions(Benchmark.Core PRIVATE BENCHMARKCORE_EXPORTS)

target_link_libraries(Benchmark.Core PRIVATE psapi)

# No "lib" prefix on Windows
set_target_properties(Benchmark.Core PROPERTIES
    OUTPUT_NAME   "Benchmark.Core"
    PREFIX        ""
)

# ── MSVC-specific flags ───────────────────────────────────────────────────────
if(MSVC)
    target_compile_options(Benchmark.Core PRIVATE
        /W4
        /WX
        /permissive-
        /utf-8
        /Zc:__cplusplus
        /EHsc
    )

    # NOMINMAX prevents windows.h from defining min/max macros that clash with std::max/std::min
    target_compile_definitions(Benchmark.Core PRIVATE NOMINMAX _CRT_SECURE_NO_WARNINGS)

    # Disable manifest embedding — mt.exe requires the full Windows SDK bin.
    # The host EXE (Benchmark.UI) carries its own app.manifest; the DLL needs none.
    target_link_options(Benchmark.Core PRIVATE /MANIFEST:NO)

    # Generate PDB for Release as well (useful for profiling)
    target_compile_options(Benchmark.Core PRIVATE $<$<CONFIG:Release>:/Zi>)
    target_link_options(Benchmark.Core PRIVATE $<$<CONFIG:Release>:/DEBUG /INCREMENTAL:NO /OPT:REF /OPT:ICF>)
endif()

# ── Copy DLL to UI project output on build ────────────────────────────────────
# Resolve absolute path to the UI project root (no ".." in runtime path)
get_filename_component(UI_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../Benchmark.UI" ABSOLUTE)

add_custom_command(TARGET Benchmark.Core POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory
        "${UI_ROOT}/bin/$<CONFIG>/net9.0-windows"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:Benchmark.Core>
        "${UI_ROOT}/bin/$<CONFIG>/net9.0-windows/"
    COMMENT "Copying Benchmark.Core.dll to Benchmark.UI output directory"
)

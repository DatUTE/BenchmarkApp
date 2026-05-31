@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 > nul 2>&1
set PATH=%PATH%;C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja
"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -B "D:\Project\Benmark_Window\Benchmark.Core\build" -S "D:\Project\Benmark_Window\Benchmark.Core" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_COMPILER="C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207\bin\Hostx64\x64\cl.exe" "-DCMAKE_EXE_LINKER_FLAGS=/MANIFEST:NO" "-DCMAKE_SHARED_LINKER_FLAGS=/MANIFEST:NO"

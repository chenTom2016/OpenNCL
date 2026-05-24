# OpenNclNative (C++ DLL)

Goal: replace the fragile `stdin/stdout` bridge with a native DLL that hosts CPython and directly calls `kernel/openncl_kernel.py`.

## Exports

- `int openncl_init(const wchar_t* appBaseDir)`
- `wchar_t* openncl_exec(const wchar_t* cmd)` (caller must free via `openncl_free`)
- `void openncl_free(void* p)`
- `const wchar_t* openncl_last_error()`

## Build (Visual Studio)

1. Install a matching Python (e.g. 3.11 x64) and **Python development headers/libs**.
2. Open the solution and build the `OpenNclNative` project to get `OpenNclNative.dll`.
3. Copy `OpenNclNative.dll` next to the WinUI app output exe/dll.

Notes:
- This DLL still depends on the CPython runtime (`python3xx.dll`) unless you build Python static.
- `openncl_init()` expects `appBaseDir` to contain `kernel/` and `python/` folders (they are already copied to output by the .csproj).


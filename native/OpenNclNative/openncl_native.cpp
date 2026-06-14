#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objbase.h>

#include <string>
#include <mutex>

// Requires CPython headers/libs. Configure include/lib paths in the .vcxproj user settings.
#include <Python.h>

static std::mutex g_mu;
static bool g_inited = false;
static std::wstring g_lastErr;
static PyObject* g_kernel = nullptr;

static void set_err(const std::wstring& e) { g_lastErr = e; }

static std::wstring env_wstr(const wchar_t* name)
{
  wchar_t buf[32768];
  DWORD n = GetEnvironmentVariableW(name, buf, (DWORD)(sizeof(buf) / sizeof(buf[0])));
  if (n == 0 || n >= (sizeof(buf) / sizeof(buf[0]))) return L"";
  return std::wstring(buf, buf + n);
}

static std::wstring find_python_home()
{
  auto home = env_wstr(L"OPENNCL_PYTHONHOME");
  if (!home.empty()) return home;
  home = env_wstr(L"PYTHONHOME");
  if (!home.empty()) return home;

  wchar_t found[MAX_PATH];
  DWORD n = SearchPathW(nullptr, L"python.exe", nullptr, MAX_PATH, found, nullptr);
  if (n > 0 && n < MAX_PATH)
  {
    std::wstring p(found);
    auto pos = p.find_last_of(L"\\/");
    if (pos != std::wstring::npos) return p.substr(0, pos);
  }
  return L"";
}

static std::wstring widen_utf8(const char* s)
{
  if (!s) return L"";
  int len = MultiByteToWideChar(CP_UTF8, 0, s, -1, nullptr, 0);
  std::wstring out;
  out.resize(len > 0 ? (size_t)len - 1 : 0);
  if (len > 1) MultiByteToWideChar(CP_UTF8, 0, s, -1, out.data(), len);
  return out;
}

static std::wstring get_pyerr_string()
{
  if (!PyErr_Occurred()) return L"";
  PyObject *ptype = nullptr, *pvalue = nullptr, *ptrace = nullptr;
  PyErr_Fetch(&ptype, &pvalue, &ptrace);
  PyErr_NormalizeException(&ptype, &pvalue, &ptrace);
  PyObject* s = pvalue ? PyObject_Str(pvalue) : nullptr;
  std::wstring w;
  if (s)
  {
    PyObject* utf8 = PyUnicode_AsUTF8String(s);
    Py_DECREF(s);
    if (utf8)
    {
      w = widen_utf8(PyBytes_AsString(utf8));
      Py_DECREF(utf8);
    }
  }
  Py_XDECREF(ptype);
  Py_XDECREF(pvalue);
  Py_XDECREF(ptrace);
  return w;
}

static wchar_t* dup_wstr(const std::wstring& s)
{
  size_t bytes = (s.size() + 1) * sizeof(wchar_t);
  auto p = (wchar_t*)CoTaskMemAlloc(bytes);
  if (!p) return nullptr;
  memcpy(p, s.c_str(), bytes);
  return p;
}

extern "C" __declspec(dllexport) int __cdecl openncl_init(const wchar_t* appBaseDir)
{
  std::scoped_lock lk(g_mu);
  if (g_inited) return 1;

  try
  {
    if (!appBaseDir || !*appBaseDir)
    {
      set_err(L"appBaseDir is empty");
      return 0;
    }

    // When embedding CPython, set PythonHome/ProgramName so it can find Lib/encodings reliably.
    auto pyHome = find_python_home();
    PyStatus status;
    PyConfig config;
    PyConfig_InitPythonConfig(&config);

    PyConfig_SetString(&config, &config.program_name, L"OpenNclNative");
    if (!pyHome.empty())
      PyConfig_SetString(&config, &config.home, pyHome.c_str());

    status = PyConfig_Read(&config);
    if (PyStatus_Exception(status))
    {
      set_err(L"PyConfig_Read failed");
      PyConfig_Clear(&config);
      return 0;
    }

    std::wstring base(appBaseDir);
    std::wstring kernelDir = base + L"\\kernel";
    std::wstring pythonDir = base + L"\\python";

    PyWideStringList_Append(&config.module_search_paths, base.c_str());
    PyWideStringList_Append(&config.module_search_paths, kernelDir.c_str());
    PyWideStringList_Append(&config.module_search_paths, pythonDir.c_str());
    if (!pyHome.empty())
    {
      std::wstring lib = pyHome + L"\\Lib";
      std::wstring dlls = pyHome + L"\\DLLs";
      std::wstring site = pyHome + L"\\Lib\\site-packages";
      PyWideStringList_Append(&config.module_search_paths, lib.c_str());
      PyWideStringList_Append(&config.module_search_paths, dlls.c_str());
      PyWideStringList_Append(&config.module_search_paths, site.c_str());
    }

    status = Py_InitializeFromConfig(&config);
    PyConfig_Clear(&config);
    if (PyStatus_Exception(status) || !Py_IsInitialized())
    {
      set_err(L"Py_InitializeFromConfig failed");
      return 0;
    }

    PyGILState_STATE g = PyGILState_Ensure();

    PyObject* mod = PyImport_ImportModule("kernel.openncl_kernel");
    if (!mod)
    {
      auto pyerr = get_pyerr_string();
      if (pyerr.empty()) pyerr = L"(no details)";
      set_err(L"Failed to import kernel.openncl_kernel: " + pyerr);
      PyGILState_Release(g);
      return 0;
    }

    PyObject* cls = PyObject_GetAttrString(mod, "OpenNCLKernel");
    Py_DECREF(mod);
    if (!cls)
    {
      set_err(L"OpenNCLKernel not found");
      PyGILState_Release(g);
      return 0;
    }

    g_kernel = PyObject_CallObject(cls, nullptr);
    Py_DECREF(cls);
    if (!g_kernel)
    {
      auto pyerr = get_pyerr_string();
      if (pyerr.empty()) pyerr = L"(no details)";
      set_err(L"Failed to create OpenNCLKernel(): " + pyerr);
      PyGILState_Release(g);
      return 0;
    }

    PyGILState_Release(g);
    g_inited = true;
    set_err(L"");
    return 1;
  }
  catch (...)
  {
    set_err(L"openncl_init crashed");
    return 0;
  }
}

extern "C" __declspec(dllexport) wchar_t* __cdecl openncl_exec(const wchar_t* cmd)
{
  std::scoped_lock lk(g_mu);
  if (!g_inited || !g_kernel)
  {
    set_err(L"Kernel not initialized. Call openncl_init(appBaseDir) first.");
    return nullptr;
  }
  if (!cmd) cmd = L"";

  PyGILState_STATE g = PyGILState_Ensure();
  wchar_t* ret = nullptr;

  PyObject* pyCmd = PyUnicode_FromWideChar(cmd, -1);
  PyObject* result = PyObject_CallMethod(g_kernel, "exec", "O", pyCmd);
  Py_DECREF(pyCmd);

  if (!result)
  {
    set_err(L"Kernel exec() failed");
    PyErr_Clear();
    PyGILState_Release(g);
    return nullptr;
  }

  PyObject* utf8 = PyUnicode_AsUTF8String(result);
  Py_DECREF(result);
  if (!utf8)
  {
    set_err(L"Failed to encode result");
    PyErr_Clear();
    PyGILState_Release(g);
    return nullptr;
  }

  std::wstring w = widen_utf8(PyBytes_AsString(utf8));
  Py_DECREF(utf8);
  ret = dup_wstr(w);

  set_err(L"");
  PyGILState_Release(g);
  return ret;
}

extern "C" __declspec(dllexport) void __cdecl openncl_free(void* p)
{
  if (p) CoTaskMemFree(p);
}

extern "C" __declspec(dllexport) const wchar_t* __cdecl openncl_last_error()
{
  std::scoped_lock lk(g_mu);
  return g_lastErr.empty() ? nullptr : g_lastErr.c_str();
}

BOOL APIENTRY DllMain(HMODULE, DWORD ul_reason_for_call, LPVOID)
{
  if (ul_reason_for_call == DLL_PROCESS_DETACH)
  {
    std::scoped_lock lk(g_mu);
    if (g_kernel)
    {
      PyGILState_STATE g = PyGILState_Ensure();
      Py_DECREF(g_kernel);
      g_kernel = nullptr;
      PyGILState_Release(g);
    }
    if (g_inited)
    {
      Py_Finalize();
      g_inited = false;
    }
  }
  return TRUE;
}

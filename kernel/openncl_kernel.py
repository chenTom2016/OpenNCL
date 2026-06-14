import os
import sys
import subprocess
import datetime
import urllib.request
import json

class OpenNCLKernel:
    def __init__(self):
        self.history = []
        self.modules = ["math", "net", "fs", "encrypt", "qrcode", "translate", "ai"]
        self.plugins = {}        # name -> (run_callable, description)
        self.plugin_errors = []  # list of "filename: error"
        self._load_plugins()

    def _load_plugins(self):
        import importlib.util
        import glob
        root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        plugins_dir = os.path.join(root, "plugins")
        if not os.path.isdir(plugins_dir):
            return
        for path in sorted(glob.glob(os.path.join(plugins_dir, "*.py"))):
            fname = os.path.basename(path)
            if fname.startswith("__"):
                continue
            try:
                spec = importlib.util.spec_from_file_location(f"openncl_plugin_{fname[:-3]}", path)
                mod = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(mod)
                name = getattr(mod, "COMMAND", None)
                run = getattr(mod, "run", None)
                if not name or not callable(run):
                    self.plugin_errors.append(f"{fname}: missing COMMAND or run()")
                    continue
                desc = getattr(mod, "DESCRIPTION", "")
                self.plugins[str(name).strip().lower()] = (run, desc)
            except Exception as e:
                self.plugin_errors.append(f"{fname}: {e}")

    def _cmd_plugins(self) -> str:
        lines = [f"Loaded plugins: {len(self.plugins)}"]
        for name, (_, desc) in sorted(self.plugins.items()):
            lines.append(f"  {name}  -  {desc}" if desc else f"  {name}")
        if self.plugin_errors:
            lines.append("Errors:")
            for err in self.plugin_errors:
                lines.append(f"  [ERROR] {err}")
        if len(self.plugins) == 0 and not self.plugin_errors:
            lines.append("  (none) — drop .py files into the plugins/ folder")
        return "\n".join(lines)

    def exec(self, cmd: str) -> str:
        self.history.append(cmd)
        cmd_lower = cmd.strip().lower()

        if cmd_lower == "about": return self._cmd_about()
        if cmd_lower == "help": return self._cmd_help()
        if cmd_lower == "modules": return self._cmd_modules()
        if cmd_lower == "plugins": return self._cmd_plugins()
        if cmd_lower in ("date", "time"): return self._cmd_date()
        if cmd_lower == "ip": return self._cmd_ip()
        if cmd_lower == "pwd": return self._cmd_pwd()
        if cmd_lower in ("dir", "ls"): return self._cmd_dir()
        if cmd_lower == "sysinfo" or cmd_lower == "system": return self._cmd_sysinfo()
        if cmd_lower in ("version", "ver"): return "OpenNCL Kernel v4.0"
        if cmd_lower == "terminfo": return self._cmd_terminfo()
        if cmd_lower in ("clear", "cls"): return "__CLEAR__"
        if cmd_lower in ("exit", "quit"): return "__EXIT__"

        if cmd_lower.startswith("cd "): return self._cmd_cd(cmd[3:].strip())
        if cmd_lower.startswith("echo "): return cmd[5:]
        if cmd_lower.startswith("calculate "): return self._cmd_calculate(cmd[10:])
        if cmd_lower.startswith("calc "): return self._cmd_calculate(cmd[5:])
        if cmd_lower.startswith("open "): return self._cmd_open(cmd[5:])
        if cmd_lower.startswith("install "): return self._cmd_install(cmd[8:])

        # System shortcuts
        if cmd_lower in ("python", "node", "cmd", "powershell", "explorer",
                         "notepad", "control", "taskmgr", "mspaint", "regedit"):
            return self._cmd_sys_shortcut(cmd_lower)

        # Search commands
        if cmd_lower.startswith("{search:google}:") or cmd_lower.startswith("google "):
            return self._cmd_search(cmd, "google")
        if cmd_lower.startswith("{search:bing}:") or cmd_lower.startswith("bing "):
            return self._cmd_search(cmd, "bing")
        if cmd_lower.startswith("{search:youtube}:") or cmd_lower.startswith("youtube "):
            return self._cmd_search(cmd, "youtube")
        if cmd_lower.startswith("{open:"):
            return self._cmd_search_open(cmd)

        # Encrypt / Decrypt
        if cmd_lower.startswith("encrypt "): return self._cmd_encrypt(cmd[8:].strip())
        if cmd_lower.startswith("decrypt "): return self._cmd_decrypt(cmd[8:].strip())

        # Color config
        import re as _re
        if _re.match(r"^color\(\s*[a-zA-Z]+\s*,\s*[a-zA-Z]+\s*\)$", cmd_lower):
            m = _re.search(r"color\((.*),(.*)\)", cmd_lower)
            return f"Color: fg={m.group(1).strip()}, bg={m.group(2).strip()}"

        # Mode pro
        if cmd_lower == "mode pro": return self._cmd_mode_pro()

        # X++
        if cmd_lower == "x++": return self._cmd_xpp()

        # Logo
        if cmd_lower.startswith("logo "): return self._cmd_logo(cmd)

        # Sandbox / Bridge / Edit
        if cmd_lower.startswith("sandbox "):
            return self._cmd_sandbox(cmd[8:])
        if cmd_lower == "bridge start":
            return self._cmd_bridge()
        if cmd_lower.startswith("edit "):
            return self._cmd_edit(cmd[5:])

        # Calculator / Screenshot / QRcode (GUI)
        if cmd_lower == "calculator":
            return self._cmd_calculator_gui()
        if cmd_lower == "screenshot":
            return self._cmd_screenshot()
        if cmd_lower == "qrcode":
            return self._cmd_qrcode(cmd)

        # Linux / WSL
        if cmd_lower in ("linux", "wsl"):
            return self._cmd_linux()

        if cmd_lower.startswith("kill "):
            return self._cmd_kill(cmd[5:].strip())

        # Translate
        if cmd_lower.startswith("translate"):
            return self._cmd_translate(cmd)

        # AI
        if cmd_lower.startswith("ai "):
            return self._cmd_ai(cmd[3:].strip())
        if cmd_lower.startswith("ask "):
            return self._cmd_ai(cmd[4:].strip())
        if cmd_lower == "config ai show":
            return self._cmd_config_ai_show()
        if cmd_lower.startswith("config ai model "):
            return self._cmd_config_ai_set("model", cmd[17:].strip())
        if cmd_lower.startswith("config ai api "):
            return self._cmd_config_ai_set("api", cmd[14:].strip())
        if cmd_lower.startswith("config ai key "):
            return self._cmd_config_ai_set("key", cmd[15:].strip())
        if cmd_lower.startswith("config ai provider "):
            return self._cmd_config_ai_set("provider", cmd[20:].strip())

        # URL detection
        if _re.match(r'^https?://', cmd_lower):
            self._cmd_open(cmd_lower)
            return f"Opened: {cmd_lower}"

        # Plugins (user-extensible commands from plugins/ folder)
        parts = cmd.strip().split(None, 1)
        if parts:
            pname = parts[0].lower()
            if pname in self.plugins:
                run, _ = self.plugins[pname]
                pargs = parts[1] if len(parts) > 1 else ""
                try:
                    return str(run(pargs))
                except Exception as e:
                    return f"[ERROR] Plugin '{pname}' failed: {e}"

        return f"Unknown command: {cmd}\nType 'help' to see available commands.\nDid you mean '{self._suggest(cmd_lower)}'?" if self._suggest(cmd_lower) else f"Unknown command: {cmd}\nType 'help' to see available commands."

    def get_history(self):
        return self.history[-50:]

    def list_modules(self):
        return self.modules

    def _suggest(self, inp):
        cmds = ["help","about","version","ver","date","time","dir","ls","pwd","sysinfo","system",
                "modules","clear","cls","exit","quit","cd","echo","calc","calculate",
                "google","bing","youtube","open","ip","encrypt","decrypt","install",
                "cmd","powershell","explorer","notepad","control","taskmgr","mspaint","regedit",
                "mode pro","x++","linux","wsl","sandbox","logo","bridge start","edit","translate",
                "calculator","screenshot","qrcode","kill","ai","ask","config ai","config ai model",
                "config ai api","config ai key","config ai provider","config ai show"]
        best, best_d = None, 99
        for c in cmds:
            d = self._lev(inp, c.lower())
            if d < best_d and d <= 2:
                best_d, best = d, c
        return best

    @staticmethod
    def _lev(a, b):
        if not a: return len(b)
        if not b: return len(a)
        d = [[0]*(len(b)+1) for _ in range(len(a)+1)]
        for i in range(len(a)+1): d[i][0] = i
        for j in range(len(b)+1): d[0][j] = j
        for i in range(1, len(a)+1):
            for j in range(1, len(b)+1):
                d[i][j] = min(d[i-1][j]+1, d[i][j-1]+1, d[i-1][j-1]+(0 if a[i-1]==b[j-1] else 1))
        return d[len(a)][len(b)]

    def _cmd_about(self):
        title = "OpenNCL  Open New Command Line OS  v4.0"
        author = "Chen Tom 2016"
        platform = "Windows + Python + .NET WinUI 3"
        kernel = "CL-Kernel v4.0 (Hybrid)"
        footer = "\"Open source, open mind.\""
        github = "github.com/chenTom2016"
        try:
            import json
            cfg_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "config", "brand.json")
            if os.path.exists(cfg_path):
                with open(cfg_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                ab = data.get("about", {})
                title = ab.get("title", title)
                author = ab.get("author", author)
                platform = ab.get("platform", platform)
                kernel = ab.get("kernel", kernel)
                footer = ab.get("footer", footer)
                github = ab.get("github", github)
        except Exception:
            pass
        return (
            "          ██████╗ ██████╗ ███████╗███╗   ██╗███╗   ██╗ ██████╗██╗     \n"
            "         ██╔═══██╗██╔══██╗██╔════╝████╗  ██║████╗  ██║██╔════╝██║     \n"
            "         ██║   ██║██████╔╝█████╗  ██╔██╗ ██║██╔██╗ ██║██║     ██║     \n"
            "         ██║   ██║██╔═══╝ ██╔══╝  ██║╚██╗██║██║╚██╗██║██║     ██║     \n"
            "         ╚██████╔╝██║     ███████╗██║ ╚████║██║ ╚████║╚██████╗███████╗\n"
            "          ╚═════╝ ╚═╝     ╚══════╝╚═╝  ╚═══╝╚═╝  ╚═══╝ ╚═════╝╚══════╝\n"
            f"                        {title}\n"
            "----------------------------------------------\n"
            f"Author   : {author}\n"
            f"Platform : {platform}\n"
            f"Kernel   : {kernel}\n"
            "----------------------------------------------\n"
            f"{footer}\n"
            f"{github}"
        )

    def _cmd_help(self):
        return (
            "------------------------------------------------------\n"
            "  OpenNCL v4.0 Commands\n"
            "------------------------------------------------------\n"
            "\n"
            "  Basic:\n"
            "    help                Show this help\n"
            "    about               About OpenNCL\n"
            "    exit                Exit / restart\n"
            "    pwd                 Print working directory\n"
            "    cd <path>           Change directory\n"
            "    dir / ls            List directory\n"
            "    date                Current date & time\n"
            "    version / ver       Kernel version\n"
            "    sysinfo             System information\n"
            "    ip                  Public IP\n"
            "    clear / cls         Clear terminal\n"
            "\n"
            "  Modules:\n"
            "    modules             List all modules\n"
            "    install <name>      Install a module\n"
            "\n"
            "  Tools:\n"
            "    calc <expr>         Calculate expression\n"
            "    echo <text>         Echo text\n"
            "    encrypt <path>      Encrypt file/folder\n"
            "    decrypt <path>      Decrypt file/folder\n"
            "\n"
            "  System Shortcuts:\n"
            "    cmd | powershell | explorer | notepad\n"
            "    control | taskmgr | mspaint | regedit\n"
            "    calculator | screenshot\n"
            "\n"
            "  Web:\n"
            "    {search:Google}:    Search Google\n"
            "    {search:Bing}:      Search Bing\n"
            "    {search:YouTube}:   Search YouTube\n"
            "    {open:url}          Open URL in browser\n"
            "    open <path>         Open file/folder/URL\n"
            "\n"
            "  Advanced:\n"
            "    mode pro            Professional mode\n"
            "    X++                 X++ interpreter\n"
            "    sandbox <code>      Run sandboxed code\n"
            "    logo show <img>     Show ASCII logo\n"
            "    logo save <img>     Save ASCII logo\n"
            "    bridge start        Start Python bridge\n"
            "    edit <file>         Open editor\n"
            "    translate <s> <t> <text>  Translate text\n"
            "    qrcode <text>       Generate QR code\n"
            "\n"
            "  AI (OpenAI-compatible):\n"
            "    ai <prompt>         Ask AI\n"
            "    ask <prompt>        Same as ai\n"
            "    config ai show      Show AI config\n"
            "    config ai model <m> Set model\n"
            "    config ai api <url> Set API endpoint\n"
            "    config ai key <k>   Set API key\n"
            "    config ai provider <p> Set provider\n"
            "\n"
            "------------------------------------------------------"
        )

    def _cmd_modules(self):
        return "Installed modules: " + ", ".join(self.modules)

    def _cmd_date(self):
        return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    def _cmd_ip(self):
        try:
            with urllib.request.urlopen("https://api.ipify.org", timeout=5) as resp:
                return f"Public IP: {resp.read().decode()}"
        except Exception as e:
            return f"Error retrieving IP: {e}"

    def _cmd_pwd(self):
        return os.getcwd()

    def _cmd_cd(self, path):
        if not path:
            return os.getcwd()
        try:
            os.chdir(path.strip().strip('"'))
            return os.getcwd()
        except Exception as e:
            return f"Error: {e}"

    def _cmd_dir(self):
        try:
            files = os.listdir(".")
            if not files:
                return "(empty directory)"
            result = []
            for f in sorted(files):
                full = os.path.join(".", f)
                if os.path.isdir(full):
                    result.append(f"  [DIR]  {f}")
                else:
                    try:
                        size = os.path.getsize(full)
                        result.append(f"  {size:>8,}  {f}")
                    except OSError:
                        result.append(f"         ?  {f}")
            return "\n".join(result)
        except Exception as e:
            return f"Error listing directory: {e}"

    def _cmd_calculate(self, expr):
        try:
            expr = expr.replace("x", "*").replace("X", "*")
            result = eval(expr, {"__builtins__": {}}, {})
            return f"{expr} = {result}"
        except Exception as e:
            return f"Error: {e}"

    def _cmd_sysinfo(self):
        try:
            info = []
            info.append(f"Platform : {sys.platform}")
            info.append(f"Python   : {sys.version}")
            info.append(f"Exec     : {sys.executable}")
            info.append(f"CWD      : {os.getcwd()}")
            if sys.platform == "win32":
                try:
                    result = subprocess.run(["ver"], capture_output=True, text=True, shell=True)
                    info.append(f"OS       : {result.stdout.strip()}")
                except Exception:
                    pass
            return "\n".join(info)
        except Exception as e:
            return f"Error: {e}"

    def _cmd_open(self, path):
        path = path.strip().strip('"')
        try:
            if sys.platform == "win32":
                os.startfile(path)
                return f"Opened: {path}"
            elif sys.platform == "darwin":
                subprocess.run(["open", path])
                return f"Opened: {path}"
            else:
                subprocess.run(["xdg-open", path])
                return f"Opened: {path}"
        except Exception as e:
            return f"Unable to open: {path} ({e})"

    def _cmd_install(self, name):
        return f"Module '{name}' installation is handled by TomLang package manager.\nUse 'pip install {name}' for Python packages."

    def _cmd_sys_shortcut(self, name):
        try:
            if name == "cmd":
                os.system("start cmd")
            elif name == "powershell":
                os.system("start powershell")
            elif name == "python":
                os.system("start python")
            elif name == "node":
                os.system("start node")
            else:
                subprocess.run(name, shell=True)
            return f"Launched: {name}"
        except FileNotFoundError:
            return f"Command '{name}' not found. Check environment variables."

    def _cmd_search(self, cmd, engine):
        import urllib.parse
        import webbrowser
        q = cmd
        for prefix in [f"{{search:{engine}}}:", f"{engine} "]:
            if q.lower().startswith(prefix):
                q = q[len(prefix):]
                break
        if not q:
            return f"Usage: {engine} <query>"
        urls = {"google": "https://www.google.com/search?q=",
                "bing": "https://www.bing.com/search?q=",
                "youtube": "https://www.youtube.com/results?search_query="}
        url = urls[engine] + urllib.parse.quote(q)
        webbrowser.open(url)
        return f"Opened {engine} search: {q}"

    def _cmd_search_open(self, cmd):
        import webbrowser
        url = cmd[6:].rstrip("}").strip()
        if not url.startswith("http"):
            url = "https://" + url
        webbrowser.open(url)
        return f"Opened: {url}"

    def _cmd_encrypt(self, text):
        if not text:
            return "Usage: encrypt <text>"
        import base64
        return "Encrypted: " + base64.b64encode(text.encode()).decode()

    def _cmd_decrypt(self, text):
        if not text:
            return "Usage: decrypt <text>"
        import base64
        try:
            return "Decrypted: " + base64.b64decode(text).decode()
        except Exception:
            return "Error: Invalid base64 string"

    def _cmd_mode_pro(self):
        return ("Entering Professional Mode...\n"
                "Commands: ping, scan, encrypt, open\n"
                "Type 'exit' to return to normal mode.")

    def _cmd_xpp(self):
        return ("X++ Interpreter v0.4  Interactive Mode\n"
                "  x = input()     get input\n"
                "  x = <expr>      assign variable\n"
                "  print(<expr>)   print expression\n"
                "  if <cond>: { } else { }\n"
                "Type 'exit' to quit X++ mode.")

    def _cmd_sandbox(self, code):
        if not code.strip():
            return "Usage: sandbox <python expression>"
        safe_builtins = {"abs": abs, "min": min, "max": max, "sum": sum, "len": len,
                         "range": range, "int": int, "float": float, "str": str,
                         "list": list, "dict": dict, "bool": bool, "round": round,
                         "print": lambda *a, **kw: None}
        try:
            result = eval(code, {"__builtins__": safe_builtins}, {})
            return f"Sandbox result: {result}"
        except Exception as e:
            return f"Sandbox error: {e}"

    def _cmd_bridge(self):
        try:
            import threading
            import socket
            def serve():
                s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                s.bind(("127.0.0.1", 9090))
                s.listen(1)
                s.settimeout(10)
                try:
                    conn, addr = s.accept()
                    conn.sendall(b"OpenNCL Bridge v4.0\n")
                    conn.close()
                except socket.timeout:
                    pass
                s.close()
            t = threading.Thread(target=serve, daemon=True)
            t.start()
            return "Bridge server started on port 9090.\nWaiting for connections..."
        except Exception as e:
            return f"Bridge error: {e}"

    def _cmd_edit(self, path):
        path = path.strip().strip('"')
        if not path:
            return "Usage: edit <file>"
        try:
            if sys.platform == "win32":
                os.startfile(path)
            elif sys.platform == "darwin":
                subprocess.run(["open", path])
            else:
                subprocess.run(["xdg-open", path])
            return f"Opened: {path}"
        except Exception as e:
            return f"Edit error: {e}"

    def _cmd_calculator_gui(self):
        try:
            if sys.platform == "win32":
                subprocess.Popen("calc.exe")
                return "Calculator launched."
            elif sys.platform == "darwin":
                subprocess.Popen(["open", "-a", "Calculator"])
                return "Calculator launched."
            else:
                return "GUI calculator not available on this platform. Use 'calc <expr>'."
        except Exception as e:
            return f"Calculator error: {e}"

    def _cmd_screenshot(self):
        try:
            from PIL import ImageGrab
            ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            filename = f"openncl_screenshot_{ts}.png"
            img = ImageGrab.grab()
            img.save(filename)
            return f"Screenshot saved: {filename} ({img.width}x{img.height})"
        except ImportError:
            return "PIL not installed. Run: pip install Pillow"
        except Exception as e:
            return f"Screenshot error: {e}"

    def _cmd_qrcode(self, cmd):
        text = cmd[7:].strip() if len(cmd) > 7 else ""
        if not text:
            return "Usage: qrcode <text or url>"
        try:
            import qrcode
            qr = qrcode.QRCode(version=1, box_size=2, border=1)
            qr.add_data(text)
            qr.make(fit=True)
            img = qr.make_image(fill_color="black", back_color="white")
            lines = []
            for y in range(0, img.height, 2):
                line = ""
                for x in range(img.width):
                    px_top = img.getpixel((x, y))
                    px_bot = img.getpixel((x, y + 1)) if y + 1 < img.height else 255
                    if px_top == 0 and px_bot == 0:
                        line += "█"
                    elif px_top == 0:
                        line += "▀"
                    elif px_bot == 0:
                        line += "▄"
                    else:
                        line += " "
                lines.append(line)
            return "QR Code:\n" + "\n".join(lines)
        except ImportError:
            return "qrcode module not installed. Run: pip install qrcode[pil]"
        except Exception as e:
            return f"QR error: {e}"

    def _cmd_logo(self, cmd):
        parts = cmd.split()
        if len(parts) < 3:
            return "Usage: logo show <path> [width]\n       logo save <path> [width]\n\nGenerates color ASCII art from an image."
        sub = parts[1]
        path = parts[2] if len(parts) >= 3 else ""
        width = int(parts[3]) if len(parts) >= 4 else 80
        if sub == "show":
            if not path or not os.path.exists(path):
                return "Image file not found."
            try:
                from PIL import Image
                img = Image.open(path).convert("L")
                aspect = img.height / img.width * 0.5
                h = int(width * aspect)
                img = img.resize((width, h))
                chars = "@%#*+=-:. "
                lines = []
                for y in range(h):
                    line = "".join(chars[min(p * len(chars) // 256, len(chars) - 1)] for p in list(img.getdata())[y * width:(y + 1) * width])
                    lines.append(line)
                return "\n".join(lines)
            except ImportError:
                return "PIL not installed. Run: pip install Pillow"
            except Exception as e:
                return f"Logo error: {e}"
        if sub == "save":
            if not path or not os.path.exists(path):
                return "Image file not found."
            try:
                from PIL import Image
                img = Image.open(path).convert("L")
                aspect = img.height / img.width * 0.5
                h = int(width * aspect)
                img = img.resize((width, h))
                chars = "@%#*+=-:. "
                lines = []
                for y in range(h):
                    line = "".join(chars[min(p * len(chars) // 256, len(chars) - 1)] for p in list(img.getdata())[y * width:(y + 1) * width])
                    lines.append(line)
                out_path = os.path.join(os.getcwd(), "ascii_logo_color.txt")
                with open(out_path, "w", encoding="utf-8") as f:
                    f.write("\n".join(lines))
                return f"Saved ASCII logo to {out_path}"
            except ImportError:
                return "PIL not installed. Run: pip install Pillow"
            except Exception as e:
                return f"Logo error: {e}"
        return f"Unknown: logo {sub}"

    def _cmd_linux(self):
        info = []
        if sys.platform != "win32":
            return "WSL is only available on Windows."
        try:
            r = subprocess.run(["wsl", "--status"], capture_output=True, text=True, timeout=5)
            info.append("WSL Status:")
            info.append(r.stdout.strip())
        except FileNotFoundError:
            info.append("WSL not installed. Run 'wsl --install' as admin.")
        except Exception as e:
            info.append(f"WSL error: {e}")
        return "\n".join(info)

    def _cmd_translate(self, cmd):
        parts = cmd.split()
        if len(parts) < 4:
            return "Usage: translate <from_lang> <to_lang> <text>\nExample: translate en zh Hello world"
        src = parts[1]
        tgt = parts[2]
        text = " ".join(parts[3:])
        try:
            from googletrans import Translator
            t = Translator()
            result = t.translate(text, src=src, dest=tgt)
            return f"[{src}\u2192{tgt}]: {result.text}"
        except ImportError:
            return f"[{src}\u2192{tgt}]: {text}\n(Install googletrans for auto-translation: pip install googletrans==4.0.0rc1)"
        except Exception as e:
            return f"Translate error: {e}"

    def _cmd_terminfo(self):
        info = [
            "Terminal Info",
            "------------",
            f"Mode     : normal",
            f"CWD      : {os.getcwd()}",
            f"Platform : {sys.platform}",
            f"Python   : {sys.version.split()[0]}",
            f"History  : {len(self.history)} entries",
            f"Modules  : {', '.join(self.modules)}",
        ]
        return "\n".join(info)

    def _cmd_kill(self, name):
        if not name:
            return "[ERROR] Usage: kill <process name>"
        try:
            result = subprocess.run(["taskkill", "/F", "/IM", name], capture_output=True, text=True, timeout=10)
            out = result.stdout.strip()
            return out if out else f"Killed: {name}"
        except Exception as e:
            return f"[ERROR] {e}"

    # ==================== AI MODULE ====================
    def _get_ai_config(self):
        cfg = {"provider": "openai", "model": "gpt-4o",
               "api_url": "https://api.openai.com/v1/chat/completions", "api_key": ""}
        try:
            cfg_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                                    "config", "brand.json")
            if os.path.exists(cfg_path):
                with open(cfg_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
                cfg.update(data.get("ai", {}))
        except Exception:
            pass
        return cfg

    def _save_ai_config(self, cfg):
        try:
            cfg_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                                    "config", "brand.json")
            data = {}
            if os.path.exists(cfg_path):
                with open(cfg_path, "r", encoding="utf-8") as f:
                    data = json.load(f)
            data["ai"] = cfg
            os.makedirs(os.path.dirname(cfg_path), exist_ok=True)
            with open(cfg_path, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
            return True
        except Exception:
            return False

    def _cmd_ai(self, prompt):
        if not prompt:
            return ("Usage: ai <prompt>\n\n"
                    "Configure AI:\n"
                    "  config ai model <name>\n"
                    "  config ai api <url>\n"
                    "  config ai key <key>\n"
                    "  config ai provider <name>\n"
                    "  config ai show")

        cfg = self._get_ai_config()
        if not cfg.get("api_key"):
            return ("[ERROR] AI API key not set.\n"
                    "Use: config ai key <your-api-key>\n\n"
                    "Supports any OpenAI-compatible API:\n"
                    "  OpenAI, Anthropic (via proxy), Ollama, vLLM, local LLMs, etc.\n"
                    "Default endpoint: https://api.openai.com/v1/chat/completions")

        try:
            body = json.dumps({
                "model": cfg["model"],
                "messages": [{"role": "user", "content": prompt}],
                "max_tokens": 4096
            }).encode("utf-8")

            req = urllib.request.Request(cfg["api_url"], data=body, method="POST")
            req.add_header("Content-Type", "application/json")
            req.add_header("Authorization", f"Bearer {cfg['api_key']}")

            with urllib.request.urlopen(req, timeout=120) as resp:
                data = json.loads(resp.read().decode("utf-8"))

            choices = data.get("choices", [])
            if not choices:
                return "[ERROR] No response from AI model."

            message = choices[0].get("message", {}).get("content", "")
            model_used = data.get("model", cfg["model"])

            return f"[{model_used}]\n{message}"

        except Exception as e:
            error_msg = str(e)
            if "timed out" in error_msg.lower() or "timeout" in error_msg.lower():
                return "[ERROR] AI request timed out (120s). Try a shorter prompt or check your network/API endpoint."
            return f"[ERROR] AI request failed: {error_msg}"

    def _cmd_config_ai_show(self):
        cfg = self._get_ai_config()
        key = cfg.get("api_key", "")
        masked = key[:4] + "****" + key[-4:] if len(key) > 8 else ("****" if key else "(not set)")
        return (f"AI Configuration\n"
                f"  Provider : {cfg.get('provider', 'openai')}\n"
                f"  Model    : {cfg.get('model', 'gpt-4o')}\n"
                f"  API URL  : {cfg.get('api_url', 'https://api.openai.com/v1/chat/completions')}\n"
                f"  API Key  : {masked}")

    def _cmd_config_ai_set(self, key, value):
        if not value:
            return f"[ERROR] Usage: config ai {key} <value>"
        cfg = self._get_ai_config()
        key_map = {
            "model": "model",
            "api": "api_url",
            "key": "api_key",
            "provider": "provider"
        }
        mapped = key_map.get(key, key)
        cfg[mapped] = value
        if self._save_ai_config(cfg):
            return f"AI {key} updated."
        return f"[ERROR] Failed to save AI config."

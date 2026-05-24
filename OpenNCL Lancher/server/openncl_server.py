import sys
import os
import json
import datetime
import traceback
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from flask import Flask, request, jsonify, send_from_directory
from kernel.openncl_kernel import OpenNCLKernel

app = Flask(__name__, static_folder="../webui", static_url_path="")
kernel = OpenNCLKernel()

LOGO = r"""          ██████╗ ██████╗ ███████╗███╗   ██╗███╗   ██╗ ██████╗██╗
         ██╔═══██╗██╔══██╗██╔════╝████╗  ██║████╗  ██║██╔════╝██║
         ██║   ██║██████╔╝█████╗  ██╔██╗ ██║██╔██╗ ██║██║     ██║
         ██║   ██║██╔═══╝ ██╔══╝  ██║╚██╗██║██║╚██╗██║██║     ██║
         ╚██████╔╝██║     ███████╗██║ ╚████║██║ ╚████║╚██████╗███████╗
          ╚═════╝ ╚═╝     ╚══════╝╚═╝  ╚═══╝╚═╝  ╚═══╝ ╚═════╝╚══════╝
                        Open New Command Line  v4.0"""


@app.route("/")
def index():
    return send_from_directory(app.static_folder, "index.html")


@app.after_request
def add_cors_headers(resp):
    # Allow WebUI opened from file:// or a different origin to call the API.
    resp.headers["Access-Control-Allow-Origin"] = "*"
    resp.headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS"
    resp.headers["Access-Control-Allow-Headers"] = "Content-Type"
    return resp


@app.route("/api/ping", methods=["GET"])
def ping():
    return jsonify({"ok": True})


@app.route("/api/exec", methods=["POST", "OPTIONS"])
def exec_cmd():
    if request.method == "OPTIONS":
        return ("", 204)
    data = request.get_json()
    cmd = data.get("cmd", "").strip()
    if not cmd:
        return jsonify({"output": "", "error": False})
    try:
        result = kernel.exec(cmd)
        error = result.startswith("[ERROR]") or "Error" in result
        return jsonify({"output": result, "error": error})
    except Exception:
        return jsonify({"output": "[Kernel Crash]\n" + traceback.format_exc(), "error": True})


@app.route("/api/history")
def history():
    return jsonify(kernel.get_history())


@app.route("/api/modules")
def modules():
    return jsonify(kernel.list_modules())


@app.route("/api/logo")
def logo():
    return jsonify({"logo": LOGO})


@app.route("/api/info")
def info():
    return jsonify({
        "cwd": os.getcwd(),
        "platform": sys.platform,
        "python": sys.version,
        "date": datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    })


if __name__ == "__main__":
    print("OpenNCL WebUI Server starting on http://127.0.0.1:7878")
    app.run(host="127.0.0.1", port=7878)

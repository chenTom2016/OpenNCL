const output = document.getElementById("output");
const pathDisplay = document.getElementById("path-display");
const promptText = document.getElementById("prompt-text");
const inputText = document.getElementById("input-text");
const terminal = document.getElementById("terminal");

const API = "http://127.0.0.1:7878/api";
let history = [];
let histIdx = -1;
let inputBuf = "";
let proMode = false;

function scrollDown() {
    requestAnimationFrame(() => { terminal.scrollTop = terminal.scrollHeight; });
}

function appendOutput(text, cls) {
    const div = document.createElement("div");
    if (cls) div.className = cls;
    div.textContent = text;
    output.appendChild(div);
    scrollDown();
}

function appendLogo(lines) {
    lines.split("\n").forEach(line => {
        const div = document.createElement("div");
        div.className = "logo-line";
        div.textContent = line;
        output.appendChild(div);
    });
}

function flushPrompt(cmd) {
    inputText.textContent = "";
    const full = pathDisplay.textContent + promptText.textContent + cmd;
    appendOutput(full, "ok");
}

function renderPrompt() {
    inputBuf = "";
    inputText.textContent = "";
}

function refreshInput() {
    inputText.textContent = inputBuf;
    scrollDown();
}

async function fetchLogo() {
    try {
        const res = await fetch(API + "/logo");
        const data = await res.json();
        appendLogo(data.logo);
    } catch (e) {
        appendLogo("OpenNCL v4.0");
    }
    appendOutput("Type \"help\" for commands.", "dim");
    appendOutput("", "dim");
}

async function fetchInfo() {
    try {
        const res = await fetch(API + "/info");
        const info = await res.json();
        pathDisplay.textContent = info.cwd + " ";
    } catch (e) {
        pathDisplay.textContent = "~/ ";
    }
    renderPrompt();
}

async function execCommand(cmd) {
    try {
        const res = await fetch(API + "/exec", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ cmd })
        });
        const data = await res.json();
        if (data.output) {
            const lines = data.output.split("\n");
            lines.forEach(line => {
                if (line.startsWith("[ERROR]") || line.includes("Error")) {
                    appendOutput(line, "error");
                } else {
                    appendOutput(line);
                }
            });
        }
        fetchInfo();
    } catch (e) {
        appendOutput("[ERROR] Cannot reach kernel.", "error");
    }
}

function handleCommand(cmd) {
    cmd = cmd.trim();
    if (!cmd) return;

    history.push(cmd);
    histIdx = -1;
    flushPrompt(cmd);

    const lower = cmd.toLowerCase();
    if (lower === "clear" || lower === "cls") {
        output.innerHTML = "";
        renderPrompt();
        return;
    }
    if (lower === "exit" || lower === "quit") {
        if (proMode) {
            proMode = false;
            promptText.textContent = "openncl> ";
            appendOutput("Exiting pro mode.", "dim");
        } else {
            appendOutput("Session ended.", "dim");
        }
        renderPrompt();
        return;
    }
    if (lower === "mode pro") {
        proMode = true;
        promptText.textContent = "root@Command:~# ";
    }

    execCommand(cmd);
}

document.addEventListener("keydown", e => {
    if (e.ctrlKey || e.metaKey || e.altKey) return;

    if (e.key === "Enter") {
        e.preventDefault();
        handleCommand(inputBuf);
        inputBuf = "";
        inputText.textContent = "";
    } else if (e.key === "Backspace") {
        e.preventDefault();
        if (inputBuf.length > 0) {
            inputBuf = inputBuf.slice(0, -1);
            refreshInput();
        }
    } else if (e.key === "ArrowUp") {
        e.preventDefault();
        if (history.length > 0) {
            histIdx = histIdx < 0 ? history.length - 1 : Math.max(0, histIdx - 1);
            inputBuf = history[histIdx];
            refreshInput();
        }
    } else if (e.key === "ArrowDown") {
        e.preventDefault();
        if (histIdx >= 0) {
            histIdx++;
            inputBuf = histIdx < history.length ? history[histIdx] : "";
            refreshInput();
        }
    } else if (e.key === "Tab") {
        e.preventDefault();
        inputBuf += "    ";
        refreshInput();
    } else if (e.key.length === 1 && !e.ctrlKey && !e.metaKey) {
        e.preventDefault();
        inputBuf += e.key;
        refreshInput();
    }
});

document.addEventListener("click", () => {
    terminal.focus();
});

fetchLogo();
fetchInfo();

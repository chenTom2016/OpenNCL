# OpenNCL plugin example.
#
# Drop a .py file like this into the plugins/ folder and the kernel will
# load it automatically on startup. Each plugin defines one command.
#
# Contract:
#   COMMAND      -> command name (lowercase), typed in the terminal
#   DESCRIPTION  -> short text shown by the "plugins" command
#   run(args)    -> called with the text after the command name; returns a string

COMMAND = "hello"
DESCRIPTION = "Example plugin: greets you back"


def run(args: str) -> str:
    args = args.strip()
    if args:
        return f"Hello, {args}! (from plugin)"
    return "Hello from the OpenNCL plugin system! Try: hello <your name>"

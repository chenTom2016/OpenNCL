import sys
import os
import traceback

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from kernel.openncl_kernel import OpenNCLKernel

kernel = OpenNCLKernel()
sys.stdout.write("__READY__\n")
sys.stdout.flush()
while True:
    try:
        line = sys.stdin.readline()
        if not line:
            break

        cmd = line.strip()
        if cmd == "__exit__":
            break

        result = kernel.exec(cmd)
        sys.stdout.write(result + "\n")
        sys.stdout.flush()

    except Exception:
        sys.stdout.write("[Kernel Crash]\n")
        sys.stdout.write(traceback.format_exc())
        sys.stdout.flush()

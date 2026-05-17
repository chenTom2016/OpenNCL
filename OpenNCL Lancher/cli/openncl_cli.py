from kernel.openncl_kernel import OpenNCLKernel

kernel = OpenNCLKernel()

print("OpenNCL CLI (type 'exit' to quit)")

while True:
    try:
        cmd = input("openncl> ").strip()
        if cmd == "exit":
            break
        print(kernel.exec(cmd))
    except KeyboardInterrupt:
        break

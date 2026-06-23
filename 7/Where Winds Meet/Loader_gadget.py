#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import frida
import argparse
import os
import sys
import time
import json
import signal
import threading
import traceback

# 控制台 GBK 环境下 emoji 会抛 UnicodeEncodeError，主动切到 UTF-8
try:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 27042
DEFAULT_LOG = "frida_hook_log.json"

def on_message_factory(log_handle, log_lock):
    """log_handle: 已 open 的文件句柄，复用避免每条消息 open/close。"""
    def on_message(message, data):
        try:
            mtype = message.get("type")
            if mtype == "send":
                payload = message.get("payload")
                # Console display
                print(f"[JS] {json.dumps(payload, ensure_ascii=False)}")
                # JSONL append
                line = json.dumps(payload, ensure_ascii=False) + "\n"
                with log_lock:
                    log_handle.write(line)
                    log_handle.flush()
            elif mtype == "error":
                stack = message.get("stack")
                if stack:
                    print(f"[JS ERROR]\n{stack}")
                else:
                    print(f"[JS ERROR] {message}")
            else:
                print(f"[MSG] {message}")
        except Exception as e:
            print(f"[PY ERROR] handling message: {e}")
            traceback.print_exc()
    return on_message

def connect_to_gadget(host, port, timeout_sec=5.0):
    address = f"{host}:{port}"
    dm = frida.get_device_manager()
    deadline = time.time() + timeout_sec
    last_exc = None
    # 优先 get_remote_device，避免重复 add 抛错
    while time.time() < deadline:
        try:
            try:
                return dm.get_remote_device(address)
            except Exception:
                return dm.add_remote_device(address)
        except Exception as e:
            last_exc = e
            time.sleep(0.25)
    raise last_exc or RuntimeError(f"Unable to connect to {address}")

def choose_target_process(dev, target):
    procs = dev.enumerate_processes()
    if target:
        if target.isdigit():
            pid = int(target)
            for p in procs:
                if p.pid == pid:
                    return p
            raise RuntimeError(f"PID {pid} not found on the remote device.")
        matches = [p for p in procs if target.lower() in p.name.lower()]
        if len(matches) == 1:
            return matches[0]
        if len(matches) > 1:
            for p in matches:
                if p.name.lower() == target.lower():
                    return p
            return matches[0]
        raise RuntimeError(f"No process matching '{target}' found.")
    # 未指定 target 时不再随机选 non_gadget[0]，否则会误注入桌面无关进程
    candidates = [p for p in procs if p.name.lower() != "gadget"]
    if not candidates:
        for p in procs:
            if p.name.lower() == "gadget":
                return p
        raise RuntimeError("No available process to attach to on the remote device.")
    print("[!] 未通过 --target 指定目标，候选进程列表如下，请用 -t <name|pid> 显式选择：")
    for p in candidates[:32]:
        print(f"    pid={p.pid:<6} name={p.name}")
    raise RuntimeError("Refusing to auto-pick a target process; pass --target.")

def main():
    parser = argparse.ArgumentParser(description="Load a Frida script via Frida Gadget (simplified loader)")
    parser.add_argument("-H", "--host", default=DEFAULT_HOST, help="Gadget address (default: 127.0.0.1)")
    parser.add_argument("-P", "--port", type=int, default=DEFAULT_PORT, help="Gadget port (default: 27042)")
    parser.add_argument("-s", "--script", default="hook.js", help="Path to the JS script to inject (default: hook.js)")
    parser.add_argument("-l", "--log", default=DEFAULT_LOG, help=f"JSONL log file (default: {DEFAULT_LOG})")
    parser.add_argument("--append", action="store_true", help="Append to log instead of truncating it on start")
    parser.add_argument("--resume", action="store_true", help="Call device.resume(pid) after loading (useful if on_load:'wait')")
    parser.add_argument("--timeout", type=float, default=30.0, help="Gadget connection timeout in seconds (default: 30s)")
    parser.add_argument("-t", "--target", default=None, help="Name (or pid) of the target process. Required if multiple non-Gadget processes exist.")
    args = parser.parse_args()

    if not os.path.isfile(args.script):
        print(f"[!] JS script not found: {args.script}")
        sys.exit(1)

    # Open log once and reuse across messages (避免每条消息 open/close)
    try:
        log_handle = open(args.log, "a" if args.append else "w", encoding="utf-8")
    except Exception as e:
        print(f"[!] Unable to open log '{args.log}': {e}")
        sys.exit(1)
    log_lock = threading.Lock()
    stop_event = threading.Event()

    # 显式 SIGINT 处理：Windows 下 time.sleep 偶尔吞 Ctrl+C
    def _on_sigint(signum, frame):
        stop_event.set()
    try:
        signal.signal(signal.SIGINT, _on_sigint)
    except Exception:
        pass

    print(f"[*] Connecting to Gadget at {args.host}:{args.port} ...")
    try:
        dev = connect_to_gadget(args.host, args.port, args.timeout)
    except Exception as e:
        print(f"[X] Connection failed: {e}")
        traceback.print_exc()
        log_handle.close()
        sys.exit(1)

    session = None
    script = None
    try:
        proc = choose_target_process(dev, args.target)
        print(f"[OK] Selected process: pid={proc.pid} name={proc.name}")

        print("[*] Attach ...")
        session = dev.attach(proc.pid)

        with open(args.script, "r", encoding="utf-8") as f:
            code = f.read()

        script = session.create_script(code)
        script.on("message", on_message_factory(log_handle, log_lock))
        script.load()
        print(f"[OK] Script loaded: {args.script}")

        if args.resume:
            try:
                dev.resume(proc.pid)
                print("[OK] Process resumed (resume).")
            except Exception as e:
                print(f"[i] resume() not supported or failed: {e}")

        print(f"[LOG] -> {os.path.abspath(args.log)}")
        print("[..] Ctrl+C to quit.")
        # 用 Event.wait 替代 while True: sleep，Ctrl+C 立即生效
        stop_event.wait()

    except Exception as e:
        print(f"[X] Error: {e}")
        traceback.print_exc()
    finally:
        try:
            if script is not None:
                script.unload()
        except Exception:
            pass
        try:
            if session is not None:
                session.detach()
        except Exception:
            pass
        try:
            log_handle.close()
        except Exception:
            pass
        print("[*] Done.")

if __name__ == "__main__":
    main()

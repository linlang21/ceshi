'use strict';

if (Process.platform !== 'windows' || Process.arch !== 'x64')
  throw new Error('Windows x64 only.');

send("Mensaje desde hook.js");  // Esto debería llegar al loader

/* ========= CONFIG ========= */

const MODULE      = "wwm.exe";
const HOTKEY_VK   = 0x31; // key '1'
const TEST_PATH   = "C:\\temp\\Where Winds Meet\\Scripts\\test.lua";

/* ========= Injected Lua script (minimal loader) ========= */
/*
 * This loader only does:
 *   - loadfile(TEST_PATH)
 *   - pcall(f)
 * The real code is only in test.lua.
 */
const LUA_CHUNK_SOURCE = `
local path = [[${TEST_PATH}]]

local f, err = loadfile(path)
if not f then
  print("[inject] loadfile failed:", err)
else
  local ok, err2 = pcall(f)
  if not ok then
    print("[inject] error in test.lua:", err2)
  end
end
`;

/* ========= size_t helpers ========= */

const is64 = Process.pointerSize === 8;

function writeSize(ptr, v) {
  if (is64) ptr.writeU64(v);
  else      ptr.writeU32(v);
}

function readSize(ptr) {
  return is64 ? ptr.readU64() : ptr.readU32();
}

const NULL_PTR = ptr(0);

/* ========= Function resolution ========= */

// 模块未加载时给可定位错误，而不是脚本静默崩溃
const mod  = (function () {
  try { return Process.getModuleByName(MODULE); }
  catch (e) {
    throw new Error("Target module " + MODULE + " not loaded yet (inject after game starts): " + e.message);
  }
})();
const base = mod.base;

function scan(sig, name) {
  const res = Memory.scanSync(base, mod.size, sig);
  if (!res.length) {
    console.error("[!] Signature not found:", name);
    return null;
  }
  const addr = res[0].address;
  console.log("[+] " + name + " @", addr.sub(base));
  return addr;
}

// 官方版 + Steam 版 lua_load 双签名（参考 ysls 版）
const SIG_LUA_LOAD_PRIMARY =
  "48 89 5C 24 10 56 48 83 EC 50 49 8B D9 48 8B F1 4D 8B C8 4C 8B C2 48 8D 54 24 20";
const SIG_LUA_LOAD_FALLBACK =
  "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 50 48 8B E9 49 8B F1";

const SIG_LUA_PCALL =
  "48 89 74 24 18 57 48 83 EC 40 33 F6 48 89 6C 24 58 49 63 C1 41 8B E8 48 8B F9 45 85 C9";

let lua_load_addr  = scan(SIG_LUA_LOAD_PRIMARY,  "lua_load (primary)");
if (!lua_load_addr) lua_load_addr = scan(SIG_LUA_LOAD_FALLBACK, "lua_load (fallback)");
const lua_pcall_addr = scan(SIG_LUA_PCALL, "lua_pcall");

if (!lua_load_addr || !lua_pcall_addr)
  throw new Error("lua_load or lua_pcall not found");

const lua_load = new NativeFunction(
  lua_load_addr,
  "int",
  ["pointer", "pointer", "pointer", "pointer", "pointer"]
);

const lua_pcall = new NativeFunction(
  lua_pcall_addr,
  "int",
  ["pointer", "int", "int", "int", "pointer", "pointer"]
);

/* ========= C reader for lua_load ========= */
/*
   struct LoadS { const char *s; size_t size; };

   Lua 5.x 在 chunk 较大时会**多次**回调 reader，原写死 one-shot 在大脚本下会丢数据。
   按 offset 切片消费：每次回调返回剩余字节起始指针 + 剩余长度，size 字段同时清零。
*/

const luaReader = new NativeCallback(function (L, data, pSize) {
  const ls        = data;
  const sPtr      = ls;                          // field s
  const sizeField = ls.add(Process.pointerSize); // field size

  const remaining = readSize(sizeField);
  if (remaining === 0 || (typeof remaining === 'object' && remaining.toString() === '0')) {
    writeSize(pSize, 0);
    return NULL_PTR;
  }

  writeSize(pSize, remaining);
  // 把 size 字段推进到 0，下一次回调返回 EOF
  writeSize(sizeField, 0);
  return sPtr.readPointer();
}, "pointer", ["pointer", "pointer", "pointer"]);

/* ========= Lua loader buffer + reusable LoadS ========= */

const CHUNK_NAME = Memory.allocUtf8String("=(inject)");
const MODE_TEXT  = Memory.allocUtf8String("t");  // "t" = text only

// Loader script buffer
const SCRIPT_BUF = Memory.allocUtf8String(LUA_CHUNK_SOURCE);
let SCRIPT_LEN = 0;
while (SCRIPT_BUF.add(SCRIPT_LEN).readU8() !== 0)
  SCRIPT_LEN++;

if (SCRIPT_LEN === 0)
  console.warn("[!] LUA_CHUNK_SOURCE is empty.");

// Global LoadS struct reused (no alloc on each injection)
const LOADS = Memory.alloc(Process.pointerSize * 2);
function prepareLoadS() {
  LOADS.writePointer(SCRIPT_BUF);
  writeSize(LOADS.add(Process.pointerSize), SCRIPT_LEN);
  return LOADS;
}

/* ========= Injection into the game thread ========= */

let pendingInject = false;  // armed by key 1
let inInjection   = false;  // avoid recursion

function injectOnState(L) {
  if (SCRIPT_LEN === 0) {
    console.error("[!] Empty Lua chunk, injection cancelled.");
    return;
  }

  console.log("[*] Injection via internal lua_load on L =", L, "size =", SCRIPT_LEN);

  const loadS = prepareLoadS();

  const loadRes = lua_load(L, luaReader, loadS, CHUNK_NAME, MODE_TEXT);
  console.log("[*] lua_load ->", loadRes);
  if (loadRes !== 0) {
    console.error("[!] lua_load returned an error:", loadRes);
    return;
  }

  const pcallRes = lua_pcall(L, 0, 0, 0, NULL_PTR, NULL_PTR);
  console.log("[*] lua_pcall ->", pcallRes);
  if (pcallRes !== 0) {
    console.error("[!] lua_pcall returned an error:", pcallRes);
    return;
  }

  console.log("[+] Lua loader injected and executed successfully (test.lua loadfile+pcall).");
}

// Hook on lua_pcall: injection when the game executes Lua
Interceptor.attach(lua_pcall_addr, {
  onEnter(args) {
    if (!pendingInject || inInjection)
      return;

    pendingInject = false;
    inInjection   = true;

    try {
      const L = args[0];
      injectOnState(L);
    } catch (e) {
      console.error("[!] Exception during injection:", e);
    } finally {
      inInjection = false;
    }
  }
});

/* ========= Hotkey: key '1' (按下沿检测，避免 100ms 轮询丢键) ========= */

(function setupHotkey() {
  const user32 = Module.load("user32.dll");
  const GetAsyncKeyState = new NativeFunction(
    user32.getExportByName("GetAsyncKeyState"),
    "int16",
    ["int"]
  );

  let prevDown = false;
  // 32ms ≈ 30Hz；用 0x8000 检测「按住」并自己求边沿，避免 LSB 被读光
  setInterval(() => {
    const down = (GetAsyncKeyState(HOTKEY_VK) & 0x8000) !== 0;
    if (down && !prevDown) {
      console.log("[*] Key 1 detected → injection armed (next lua_pcall).");
      pendingInject = true;
    }
    prevDown = down;
  }, 32);
})();

console.log("[OK] Script loaded.");
console.log("[OK] Press 1: on the next entry into lua_pcall,");
console.log("     the game will do lua_load(mode=\"t\") + lua_pcall on a loader that does loadfile/pcall of:");
console.log("     " + TEST_PATH);

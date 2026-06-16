'use strict';
 
// 1. 安全检查
if (Process.platform !== 'windows' || Process.arch !== 'x64')
  throw new Error('仅支持 Windows x64 系统。');
 
const MODULE = "yysls.exe"; // 目标进程模块名
 
// =================================================================================
// 配置 & 热键定义
// =================================================================================
 
// 修饰键 Scrol_Lock
const VK_MENU             = 0x91; 
 
// 系统热键
const HOTKEY_F3           = 0x72; // F3: 注入菜单
const HOTKEY_NUMPAD_MINUS = 0x6D; // 小键盘减号 : 切换菜单显示/隐藏
 
// 导航键（原生小键盘）
const HOTKEY_NUMPAD8      = 0x80; // 上
const HOTKEY_NUMPAD2      = 0x81; // 下
const HOTKEY_NUMPAD4      = 0x64; // 左
const HOTKEY_NUMPAD6      = 0x66; // 右
const HOTKEY_NUMPAD5      = 0x65; // 确认
 
// 功能键（需配合 Scrol_Lock 键）
const HOTKEY_NUMPAD1      = 0x61; 
const HOTKEY_NUMPAD3      = 0x63; // 新增：攻击速度热键（小键盘3）
const HOTKEY_NUMPAD7      = 0x67; 
const HOTKEY_NUMPAD9      = 0x69; 
 
// 文件路径（仅需 Test.lua）
const TEST_PATH = "C:\\temp\\Where Winds Meet\\Scripts\\Test.lua";
 
/* =================================================================================
   LUA 代码片段（执行载荷）
   ================================================================================= */
 
// 1. 主菜单加载器（仍从文件加载）
const LUA_LOAD_MENU = `
local path = [[${TEST_PATH}]]
local f, err = loadfile(path)
if f then pcall(f) else print("加载菜单出错:", err) end
`;
 
// 2. 命令字符串（执行 Test.lua 中的函数）
const CMD_MENU_VISIBLE     = "if _G.ToggleGMMenu then _G.ToggleGMMenu() elseif _G.GM_MENU then _G.GM_MENU:setVisible(not _G.GM_MENU:isVisible()) end";
const CMD_TOGGLE_SPEED     = "if _G.ToggleSpeed then _G.ToggleSpeed() end";
const CMD_TOGGLE_ATTACK_SPEED = "if _G.CycleAttackSpeed then _G.CycleAttackSpeed(1) elseif _G.RunSetGMSpeed then _G.RunSetGMSpeed(_G.ATTACK_SPEED_INDEX or 2) end"; // 新增：攻击速度切换命令
const CMD_TOGGLE_LOOT_LOOP = "if _G.ToggleAutoLootLoop then _G.ToggleAutoLootLoop() end";
const CMD_RUN_RECOVER      = "if _G.RunRecover then _G.RunRecover() end";
const CMD_RUN_KILLNPC      = "if _G.RunKillNPC then _G.RunKillNPC() end";
const CMD_RUN_AUTOLOOT     = "if _G.RunAutoLoot then _G.RunAutoLoot() end";
 
// 3. 导航命令
const CMD_NAV_UP           = "if _G.MenuUp then _G.MenuUp() end";
const CMD_NAV_DOWN         = "if _G.MenuDown then _G.MenuDown() end";
const CMD_NAV_LEFT         = "if _G.MenuLeft then _G.MenuLeft() end";
const CMD_NAV_RIGHT        = "if _G.MenuRight then _G.MenuRight() end";
const CMD_NAV_CONFIRM      = "if _G.MenuConfirm then _G.MenuConfirm() end";
 
/* =================================================================================
   内存辅助函数 & 签名定义
   ================================================================================= */
 
const is64 = Process.pointerSize === 8; // 判断是否为64位进程
 
// 写入对应位数的数值（适配32/64位）
function writeSize(ptr, v) {
  if (is64) ptr.writeU64(v);
  else      ptr.writeU32(v);
}
 
// 读取对应位数的数值（适配32/64位）
function readSize(ptr) {
  return is64 ? ptr.readU64() : ptr.readU32();
}
 
const NULL_PTR = ptr(0); // 空指针
const mod  = Process.getModuleByName(MODULE); // 获取目标模块
const base = mod.base; // 模块基地址
 
// 获取控制台宽度
function getConsoleWidth() {
  try {
    if (typeof process !== 'undefined' && process.stdout && process.stdout.columns && Number.isInteger(process.stdout.columns)) {
      return process.stdout.columns;
    }
    if (typeof console !== 'undefined' && console.columns && Number.isInteger(console.columns)) {
      return console.columns;
    }
  } catch (e) {
  }
  return 80; // 默认宽度
}
 
// 格式化日期用于分隔栏
function formatDateForDivider(d) {
  const dt = d instanceof Date ? d : new Date();
  const monthNames = ["一月","二月","三月","四月","五月","六月","七月","八月","九月","十月","十一月","十二月"];
  const month = monthNames[dt.getMonth()];
  const day = dt.getDate();
  let hour = dt.getHours();
  const minute = dt.getMinutes();
  const ampm = hour >= 12 ? "下午" : "上午";
  hour = hour % 12;
  if (hour === 0) hour = 12;
  const minuteStr = minute < 10 ? "0" + minute : String(minute);
  return `${month} ${day}日 ${hour}:${minuteStr} ${ampm}`;
}
 
// 构建带内容的分隔栏
function buildDividerWithContent(content) {
  const width = Math.max(40, getConsoleWidth());
  const overhead = 2; // 左右括号的长度 '[' 和 ']'
  const minSide = 1; // 最小侧边等号数量
  const remaining = Math.max(0, width - overhead - content.length);
  const left = Math.floor(remaining / 2);
  const right = remaining - left;
  const leftPad = "=".repeat(Math.max(minSide, left));
  const rightPad = "=".repeat(Math.max(minSide, right));
  return `[${leftPad}${content}${rightPad}]`;
}
 
// 原始日志输出
function logRaw(s) {
  console.log(s);
}
 
// 输出分隔栏（可带备注）
function logDivider(note) {
  const content = note ? ` ${note} ` : " ";
  logRaw(buildDividerWithContent(content));
}
 
// 输出信息日志
function logInfo(msg) {
  logRaw(msg);
}
 
/* ========= 控制台更新器 =========
*/
// 清空控制台
function clearConsole() {
  try {
    process.stdout.write('\x1b[2J\x1b[0f');
  } catch (e) {
    // 兼容模式：输出100行空行
    for (let i = 0; i < 100; i++) console.log('');
  }
}
 
// 重新打印头部信息块
function reprintHeaderBlock() {
  const timeLine = buildDividerWithContent(` ${formatDateForDivider(new Date())} `);
  logRaw(timeLine);
 
  logDivider("使用说明");
  logInfo("[!] 重要：请先按 F3 加载菜单逻辑。");
  logDivider();
  logInfo("[热键] 小键盘减号 : 显示/隐藏菜单");
  logInfo("[导航] 小键盘 8/2/4/6/5 : 上/下/左/右/确认");
  logDivider();
  logInfo("[热键] Scrol_Lock + 小键盘1 : 切换移速");
  logInfo("[热键] Scrol_Lock + 小键盘3 : 切换攻击速度"); // 新增：攻击速度热键说明
  logInfo("[热键] Scrol_Lock + 小键盘2 : 切换自动拾取循环");
  logInfo("[热键] Scrol_Lock + 小键盘5 : 击杀NPC");
  logInfo("[热键] Scrol_Lock + 小键盘7 : 恢复生命值/体力");
  logInfo("[热键] Scrol_Lock + 小键盘9 : 手动拾取");
  logDivider();
  logRaw(buildDividerWithContent("=== 燕云十六声 菜单 v1.4 ==="));
 
  logRaw(buildDividerWithContent(" 如果你觉得好用，请支持开发者 "));
}
 
// 打印头部信息（仅首次）
function printHeaderOnce() {
  const timeLine = buildDividerWithContent(` ${formatDateForDivider(new Date())} `);
  try {
    process.stdout.write(timeLine + '\n');
  } catch (e) {
    console.log(timeLine);
  }
 
  // 使用说明区块
  logDivider("使用说明");
  logInfo("[!] 重要：请先按 F3 加载菜单逻辑。");
  logDivider();
  logInfo("[热键] 小键盘减号 : 显示/隐藏菜单");
  logInfo("[导航] 小键盘 8/2/4/6/5 : 上/下/左/右/确认");
  logDivider();
  logInfo("[热键] Scrol_Lock + 小键盘1 : 切换移速");
  logInfo("[热键] Scrol_Lock + 小键盘3 : 切换攻击速度"); // 新增：攻击速度热键说明
  logInfo("[热键] Scrol_Lock + 小键盘2 : 切换自动拾取循环");
  logInfo("[热键] Scrol_Lock + 小键盘5 : 击杀NPC");
  logInfo("[热键] Scrol_Lock + 小键盘7 : 恢复生命值/体力");
  logInfo("[热键] Scrol_Lock + 小键盘9 : 手动拾取");
  logDivider();
  logRaw(buildDividerWithContent("=== 燕云十六声 菜单 v1.4 ==="));
 
  logRaw(buildDividerWithContent(" 如果你觉得好用，请支持开发者 "));
}
 
 
// 启动控制台清理和更新循环
function startClearAndUpdateLoop(intervalMs = 60000) {
  printHeaderOnce(); // 首次打印头部
 
  setInterval(() => {
    clearConsole(); // 清空控制台
    reprintHeaderBlock(); // 重新打印头部
  }, intervalMs);
}
 
/* =================================================================================
   主逻辑
   ================================================================================= */
 
try {
  // 签名定义
  const SIG_LUA_LOAD_OFFICIAL = "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 50 48 8B E9 49 8B F1";
  const SIG_LUA_LOAD_STEAM    = "48 89 5C 24 10 56 48 83 EC 50 49 8B D9 48 8B F1 4D 8B C8 4C 8B C2 48 8D 54 24";
 
  // 扫描签名函数
  function scanSignature(sig, platformLabel) {
    logDivider("启动中");
    logInfo("[*] 解析 lua_load 签名（官方版 -> 蒸汽版）");
    logDivider();
    logDivider();
    logInfo(`[*] 扫描 lua_load 签名 [${platformLabel}]`);
    logDivider();
    const res = Memory.scanSync(base, mod.size, sig);
    if (!res.length) {
      logDivider();
      logInfo(`[✘] lua_load 签名 [${platformLabel}] 未找到 → ${sig}`);
      logDivider();
      return null;
    }
    const addr = res[0].address;
    logDivider();
    logInfo(`[✔] lua_load 签名 [${platformLabel}] 找到 → ${sig} @ 偏移量 ${addr.sub(base)}`);
    logDivider();
    return addr;
  }
 
  // 先扫描官方版签名，失败则扫描蒸汽版
  let lua_load_addr = scanSignature(SIG_LUA_LOAD_OFFICIAL, "官方版");
  if (!lua_load_addr) {
    lua_load_addr = scanSignature(SIG_LUA_LOAD_STEAM, "蒸汽版");
  }
 
  // 两个签名都没找到则抛出错误
  if (!lua_load_addr) {
    logInfo("[✘] 扫描两个签名后仍未找到 lua_load");
    throw new Error("未找到 lua_load");
  }
 
  // 扫描 lua_pcall 签名
  const SIG_LUA_PCALL = "48 89 74 24 18 57 48 83 EC 40 33 F6 48 89 6C 24 58 49 63 C1 41 8B E8 48 8B F9 45 85 C9";
  logDivider();
  logInfo("[*] 扫描 lua_pcall 签名");
  logDivider();
  const resPcall = Memory.scanSync(base, mod.size, SIG_LUA_PCALL);
  if (!resPcall.length) {
    logInfo(`[✘] lua_pcall 签名未找到 → ${SIG_LUA_PCALL}`);
    logInfo("[✘] 绑定/启动运行时失败");
    throw new Error("未找到 lua_pcall");
  }
  const lua_pcall_addr = resPcall[0].address;
  logInfo(`[✔] lua_pcall 签名找到 → ${SIG_LUA_PCALL} @ 偏移量 ${lua_pcall_addr.sub(base)}`);
 
  logDivider("绑定函数");
  logInfo("[*] 为 lua_load 和 lua_pcall 创建 NativeFunction 包装器");
 
  // 创建 NativeFunction 包装器（绑定底层函数）
  const lua_load = new NativeFunction(lua_load_addr, "int", ["pointer", "pointer", "pointer", "pointer", "pointer"]);
  const lua_pcall = new NativeFunction(lua_pcall_addr, "int", ["pointer", "int", "int", "int", "pointer", "pointer"]);
 
  logInfo(`[OK] 已绑定 lua_load @ ${lua_load_addr.sub(base)} 和 lua_pcall @ ${lua_pcall_addr.sub(base)}`);
  logInfo("[OK] 钩子已加载。");
  logInfo("[OK] 运行时已初始化（拦截器已附加）。");
  console.log("[✔] 附加完成。热键和自动注入已启用。");
  logDivider();
 
  startClearAndUpdateLoop(60000); // 每60秒更新一次控制台
 
  /* =================================================================================
     注入逻辑
     ================================================================================= */
 
  // Lua 读取器回调函数
  const luaReader = new NativeCallback(function (L, data, pSize) {
    const ls        = data;
    const sPtr      = ls;
    const sizeField = ls.add(Process.pointerSize);
    let remaining = readSize(sizeField);
    if (remaining === 0) {
      writeSize(pSize, 0);
      return NULL_PTR;
    }
    writeSize(pSize, remaining);
    writeSize(sizeField, 0);
    return sPtr.readPointer();
  }, "pointer", ["pointer", "pointer", "pointer"]);
 
  // 分配脚本缓冲区
  function allocScriptBuffer(src) {
    const buf = Memory.allocUtf8String(src);
    let len = 0;
    while (buf.add(len).readU8() !== 0) len++;
    return { buf, len };
  }
 
  const CHUNK_NAME = Memory.allocUtf8String("=(inject)"); // 代码块名称
  const MODE_TEXT  = Memory.allocUtf8String("t"); // 文本模式
  const LOADS      = Memory.alloc(Process.pointerSize * 2); // 加载缓冲区
 
  // 准备加载脚本数据
  function prepareLoadS(scriptBuf, scriptLen) {
    LOADS.writePointer(scriptBuf);
    writeSize(LOADS.add(Process.pointerSize), scriptLen);
    return LOADS;
  }
 
  let LOG_KEY_EVENTS = true;       // 设为true启用按键/热键日志
  let LOG_INJECTION_EVENTS = true; // 设为true启用注入生命周期日志
 
  // 注入字符串到Lua环境
  function injectString(L, code) {
    if (LOG_INJECTION_EVENTS) logInfo(`[注入] 调用 injectString L=${L} 代码长度=${code.length}`);
    const { buf, len } = allocScriptBuffer(code);
    const loadS = prepareLoadS(buf, len);
    
    if (LOG_INJECTION_EVENTS) logInfo("[注入] 调用 lua_load");
    const loadRes = lua_load(L, luaReader, loadS, CHUNK_NAME, MODE_TEXT);
    if (loadRes !== 0) {
      console.error("[!] lua_load 错误:", loadRes, "代码块=", CHUNK_NAME.readUtf8String());
      // 如果是菜单加载器，允许重新排队尝试
      if (code === LUA_LOAD_MENU) {
        menuQueued = false;
      }
      return false;
    }
    if (LOG_INJECTION_EVENTS) logInfo("[注入] lua_load 返回 0（成功）");
 
    if (LOG_INJECTION_EVENTS) logInfo("[注入] 调用 lua_pcall");
    const pcallRes = lua_pcall(L, 0, 0, 0, NULL_PTR, NULL_PTR);
    if (pcallRes !== 0) {
      console.error("[!] lua_pcall 错误:", pcallRes);
      // 如果是菜单加载器，允许重新排队尝试
      if (code === LUA_LOAD_MENU) {
        menuQueued = false;
      }
      return false;
    } else {
      if (LOG_INJECTION_EVENTS) logInfo("[注入] lua_pcall 返回 0（成功）");
      // 如果注入的是菜单加载器，标记为已注入（禁用F3）
      if (code === LUA_LOAD_MENU) {
        menuInjected = true;
        menuQueued = false;
        if (LOG_KEY_EVENTS) logInfo("[热键] 菜单加载器注入成功。F3热键已禁用，请使用 [热键] 小键盘减号 : 显示/隐藏菜单");
      }
      return true;
    }
  }
 
  /* =================================================================================
     队列拦截器
     ================================================================================= */
 
  let queue = []; // 注入命令队列
  let inInjection = false; // 注入中标记（防止并发）
 
  // 控制F3单次触发的标记
  let menuInjected = false; // 菜单已注入（F3禁用）
  let menuQueued = false;   // 菜单已排队（防止重复排队）
 
  // 加入命令到队列
  function trigger(code, name) {
    queue.push(code);
    if (LOG_KEY_EVENTS) {
      logInfo(`[触发] 已排队命令 "${name}" 队列长度=${queue.length}`);
    }
  }
 
  // 拦截 lua_pcall 调用，执行队列中的命令
  Interceptor.attach(lua_pcall_addr, {
    onEnter(args) {
      if (inInjection || queue.length === 0) return;
      const L = args[0];
      inInjection = true;
      if (LOG_INJECTION_EVENTS) logInfo(`[注入] 拦截器触发，准备注入 L=${L} 队列长度=${queue.length}`);
      try {
        if (LOG_INJECTION_EVENTS) logInfo(`[队列] 注入前队列快照: 长度=${queue.length}`);
        const code = queue.shift(); // 取出队列第一个命令
        if (LOG_INJECTION_EVENTS) logInfo(`[队列] 取出命令，新队列长度=${queue.length}`);
        injectString(L, code); // 执行注入
      } catch (e) {
        console.error("[!] 注入过程中发生异常:", e);
      } finally {
        inInjection = false;
        if (LOG_INJECTION_EVENTS) logInfo("[注入] 注入周期完成");
      }
    }
  });
 
  /* =================================================================================
     输入监听器
     ================================================================================= */
 
  (function setupHotkeys() {
    const user32 = Module.load("user32.dll"); // 加载user32.dll（窗口/输入相关）
    const GetAsyncKeyState = new NativeFunction(user32.getExportByName("GetAsyncKeyState"), "int16", ["int"]); // 检测按键状态
 
    // 每100ms检测一次按键
    setInterval(() => {
      // --- 始终生效的热键 ---
      // F3仅允许排队一次，防止等待注入时重复排队
      if (!menuInjected && !menuQueued && (GetAsyncKeyState(HOTKEY_F3) & 1)) {
        trigger(LUA_LOAD_MENU, "注入菜单 (F3)");
        menuQueued = true; // 标记为已排队，防止重复
        if (LOG_KEY_EVENTS) logInfo("[热键] 按下F3 — 菜单加载器已排队。等待注入完成。");
      } else if (menuInjected && (GetAsyncKeyState(HOTKEY_F3) & 1)) {
        // 菜单已注入，忽略F3
        if (LOG_KEY_EVENTS) logInfo("[热键] 按下F3但菜单已注入 — 忽略。请使用 [热键] 小键盘减号 : 显示/隐藏菜单");
      } else if (!menuInjected && menuQueued && (GetAsyncKeyState(HOTKEY_F3) & 1)) {
        // 菜单已排队，忽略重复F3
        if (LOG_KEY_EVENTS) logInfo("[热键] 按下F3但加载器已排队 — 忽略重复。请使用 [热键] 小键盘减号 : 显示/隐藏菜单");
      }
 
      // --- 检测修饰键 ---
      const isAltDown = (GetAsyncKeyState(VK_MENU) & 0x8000) !== 0; // Scrol_Lock键是否按下
 
      if (isAltDown) {
          // === Scrol_Lock + 快捷键 ===
          if (GetAsyncKeyState(HOTKEY_NUMPAD1) & 1) trigger(CMD_TOGGLE_SPEED,     "切换移速 (Scrol_Lock+1)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD3) & 1) trigger(CMD_TOGGLE_ATTACK_SPEED, "切换攻击速度 (Scrol_Lock+3)"); // 新增：Scrol_Lock+小键盘3触发攻击速度
          if (GetAsyncKeyState(HOTKEY_NUMPAD2) & 1) trigger(CMD_TOGGLE_LOOT_LOOP, "切换自动拾取循环 (Scrol_Lock+2)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD5) & 1) trigger(CMD_RUN_KILLNPC,      "击杀NPC (Scrol_Lock+5)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD7) & 1) trigger(CMD_RUN_RECOVER,      "恢复状态 (Scrol_Lock+7)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD9) & 1) trigger(CMD_RUN_AUTOLOOT,     "手动拾取 (Scrol_Lock+9)");
          
      } else {
          // === 原生按键 ===
          if (GetAsyncKeyState(HOTKEY_NUMPAD_MINUS) & 1) trigger(CMD_MENU_VISIBLE, "切换菜单显示/隐藏 (-)");
 
          // 导航键
          if (GetAsyncKeyState(HOTKEY_NUMPAD8) & 1) trigger(CMD_NAV_UP,      "菜单上移 (8)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD2) & 1) trigger(CMD_NAV_DOWN,    "菜单下移 (2)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD4) & 1) trigger(CMD_NAV_LEFT,    "菜单左移 (4)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD6) & 1) trigger(CMD_NAV_RIGHT,   "菜单右移 (6)");
          if (GetAsyncKeyState(HOTKEY_NUMPAD5) & 1) trigger(CMD_NAV_CONFIRM, "菜单确认 (5)");
      }
    }, 100);
  })();
 
} catch (e) {
  console.error("[✘] 绑定/启动运行时失败:", e);
  console.error("[✘] 签名匹配但绑定失败。请考虑更新签名。");
}
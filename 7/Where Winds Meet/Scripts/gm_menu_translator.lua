-- gm_string_translator_v2_sethook.lua
-- Lua 5.4
--
-- 钩子拦截 Text:set_text(self, s, ...) 函数实现：
--   - 如果字典文件中存在翻译，则替换文本显示
--   - 可选将新文本收集到字典文件（无重复）
-- 字典文件格式（每行一个条目）：
--   ["原文"] = "译文",

if rawget(_G, "GM_STRING_TRANSLATOR_V2_INSTALLED") then
  -- 已安装：避免重复安装钩子
  return
end
_G.GM_STRING_TRANSLATOR_V2_INSTALLED = true

-------------------------------------------------
-- 配置项
-------------------------------------------------
-- 字典条目存储文件路径
local DICT_PATH = [[C:\temp\Where Winds Meet\Scripts\gm_dict_translation.lua]]

-- 功能开关：
local ENABLE_TRANSLATION     = true   -- 使用字典中的翻译
local ENABLE_CAPTURE_NEW     = false   -- 为true时追加新文本到字典；为false时仅翻译

-- 要挂钩的目标源文件和函数名
local TARGET_SOURCE_SUBSTR   = "hexm/client/ui/base/text.lua"
local TARGET_FUNC_NAME       = "set_text"

-------------------------------------------------
-- 本地引用（微优化）
-------------------------------------------------
local debug_getinfo  = debug.getinfo
local debug_getlocal = debug.getlocal
local debug_setlocal = debug.setlocal
local string_find    = string.find
local io_open        = io.open

-------------------------------------------------
-- IO辅助函数
-------------------------------------------------
local function append_to_file(path, line)
  local f = io_open(path, "a")
  if f then
    f:write(line, "\n")
    f:close()
  end
end

-------------------------------------------------
-- 字符串转义/反转义
-------------------------------------------------
-- 字典文件存储用的最小化转义
local function escape_lua_string(s)
  return (s
    :gsub("\\", "\\\\")
    :gsub("\n", "\\n")
    :gsub("\r", "\\r")
    :gsub('"', '\\"'))
end

-- escape_lua_string的逆操作（用于读取文件中的键值）
local function unescape_lua_string(s)
  -- 使用占位符避免反斜杠替换冲突
  s = s:gsub("\\\\", "\0")   -- 临时占位符代替'\'
  s = s:gsub("\\n", "\n")
  s = s:gsub("\\r", "\r")
  s = s:gsub('\\"', '"')
  s = s:gsub("\0", "\\")    -- 恢复'\'
  return s
end

-- 规范化翻译文本中的引号：
-- 如果翻译文本包含双引号(")，替换为单引号(')
local function normalize_translation_quotes(s)
  if s:find('"', 1, true) then
    s = s:gsub('"', "'")
  end
  return s
end

-------------------------------------------------
-- 字典存储：
--   TEXT_SEEN:        当前运行中见过的原文集合（可选信息）
--   WRITTEN_KEYS:     已写入文件的键集合（转义形式，用于跨运行去重）
--   TRANSLATIONS:     原文到译文的映射表
-------------------------------------------------
local TEXT_SEEN    = {}
local WRITTEN_KEYS = {}
local TRANSLATIONS = {}

-- 暴露用于调试（如需）
_G.GM_SET_TEXT_DICT = TEXT_SEEN
_G.GM_TRANSLATION_DICT = TRANSLATIONS

-------------------------------------------------
-- 从文件加载现有字典条目
-------------------------------------------------
do
  local f = io_open(DICT_PATH, "r")
  if f then
    for line in f:lines() do
      -- 匹配格式：["转义键"] = "转义值",
      local escaped_key, escaped_value = line:match('%["(.-)"%]%s*=%s*"(.-)"')
      if escaped_key then
        -- 标记该键已写入（防止跨运行重复）
        WRITTEN_KEYS[escaped_key] = true

        -- 还原原始键
        local original_key = unescape_lua_string(escaped_key)
        TEXT_SEEN[original_key] = true

        -- 如果有非空翻译，则注册
        if escaped_value and escaped_value ~= "" then
          -- 规范化翻译文本中的引号："foo "bar"" -> "foo 'bar'"
          escaped_value = normalize_translation_quotes(escaped_value)
          local translation = unescape_lua_string(escaped_value)
          if translation ~= "" then
            TRANSLATIONS[original_key] = translation
          end
        end
      end
    end
    f:close()
  end
end

-------------------------------------------------
-- 注册新文本到字典文件
--   - 无重复行，即使跨多次运行
-------------------------------------------------
local function register_new_string(s)
  if not ENABLE_CAPTURE_NEW then
    return
  end

  if type(s) ~= "string" or s == "" then
    return
  end

  -- 文件中显示的转义形式
  local escaped = escape_lua_string(s)

  -- 如果该转义键已写入，直接返回
  -- 确保字典文件中无重复行
  if WRITTEN_KEYS[escaped] then
    return
  end

  WRITTEN_KEYS[escaped] = true
  TEXT_SEEN[s] = true

  -- 写入条目格式：["原文"] = "",
  append_to_file(DICT_PATH, ('["%s"] = "",'):format(escaped))
end

-------------------------------------------------
-- 如果存在翻译则转换文本
-------------------------------------------------
local function translate_text(s)
  if not ENABLE_TRANSLATION then
    return s
  end

  local t = TRANSLATIONS[s]
  if t and t ~= "" then
    return t
  end
  return s
end

-------------------------------------------------
-- debug.sethook回调函数
-------------------------------------------------
local function hook(event)
  if event ~= "call" then
    return
  end

  local info = debug_getinfo(2, "nS")
  if not info then
    return
  end

  local src  = info.source
  local name = info.name

  if not src or not name then
    return
  end

  -- 移除源码路径前的'@'（Lua脚本文件标识）
  if src:sub(1, 1) == "@" then
    src = src:sub(2)
  end

  -- 快速检查：只监控指定文件+函数名
  if name ~= TARGET_FUNC_NAME then
    return
  end

  if not string_find(src, TARGET_SOURCE_SUBSTR, 1, true) then
    return
  end

  -- 此时已进入Text:set_text(self, s, ...)函数
  -- 局部变量1 = self，局部变量2 = s
  local _, s = debug_getlocal(2, 2)
  if type(s) ~= "string" then
    return
  end

  -- 可选注册新文本到字典文件
  register_new_string(s)

  -- 尽可能翻译
  local new_s = translate_text(s)
  if new_s ~= s then
    debug_setlocal(2, 2, new_s)
  end
end

-------------------------------------------------
-- 安装全局钩子（仅监控call事件）
-------------------------------------------------
debug.sethook(hook, "c")

return true
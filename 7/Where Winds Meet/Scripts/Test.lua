-- SCRIPT START TEST
print("=== 脚本运行中 ===")

-- =================================================================================
-- 第一部分: 调试 & 初始化设置
-- =================================================================================
local DEBUG_FILE_ENABLED = true
local DEBUG_FILE_PATH    = "C:\\temp\\Where Winds Meet\\Scripts\\script_debug.txt"

local function write_debug(message)
    if DEBUG_FILE_ENABLED then
        pcall(function()
            local file = io.open(DEBUG_FILE_PATH, "a")
            if file then
                file:write(os.date("%H:%M:%S") .. " " .. message .. "\n")
                file:close()
            end
        end)
    end
    print(message)
end

-- 初始化调试文件
if DEBUG_FILE_ENABLED then
    pcall(function()
        local file = io.open(DEBUG_FILE_PATH, "w")
        if file then
            file:write("=== 脚本调试日志 ===\n")
            file:close()
        end
    end)
end

-- 检查游戏环境
if not G or not G.main_player or not cc or not cc.Director then
    write_debug("错误: 游戏环境未就绪 (G 或 cc 全局对象缺失)")
    return
end
local mp = G.main_player
local director = cc.Director:getInstance()
local scene = director:getRunningScene()
local size = director:getVisibleSize() -- 用于拖拽边界限制

-- 如果存在旧菜单则移除
if _G.GM_MENU then
    _G.GM_MENU:removeFromParent()
    _G.GM_MENU = nil
end

-- === 调试标记集成 (仅追加，自动应用) ===
local FLAGS_TO_SET = {
  DEBUG                     = false,
  DISABLE_ACSDK             = true,   -- 始终为true
  ENABLE_DEBUG_PRINT        = false,
  ENABLE_FORCE_SHOW_GM      = false,
  FORCE_OPEN_DEBUG_SHORTCUT = false,
  GM_IS_OPEN_GUIDE          = false,
  GM_USE_PUBLISH            = false,
  acsdk_info_has_inited     = false,  -- 始终为false
}

local MAX_DEPTH = 10
-- 默认使用 _G 作为根表，避免修改 package.loaded (除非显式指定)
local ROOT = rawget(_G, "DUMP_ROOT") or _G
local ROOT_NAME = rawget(_G, "DUMP_ROOT_NAME") or "根表"
local visited = setmetatable({}, { __mode = "k" })

-- 安全的单行日志器 (确保每次写入是原子操作且以换行结尾)
local function safe_log(msg)
  if not DEBUG_FILE_ENABLED then return end
  pcall(function()
    local f = io.open(DEBUG_FILE_PATH, "a")
    if not f then return end
    f:write(os.date("%H:%M:%S") .. " " .. tostring(msg) .. "\n")
    f:close()
  end)
end

local function modify_flags(tbl, path, depth)
  if depth >= MAX_DEPTH or visited[tbl] then return end
  visited[tbl] = true

  for k, v in next, tbl do
    -- 仅匹配字符串类型的标记键
    if type(k) == "string" and FLAGS_TO_SET[k] ~= nil then
      -- 安全尝试设置标记; rawset 极少出错，但仍用 pcall 做防御性包装
      local ok, err = pcall(function() rawset(tbl, k, FLAGS_TO_SET[k]) end)
      if ok then
        safe_log(string.format("[✔] %s%s 已设置为 %s", path, k, tostring(FLAGS_TO_SET[k])))
      else
        safe_log(string.format("[✖] 设置 %s%s 失败: %s", path, k, tostring(err)))
      end
    end

    -- 仅递归处理真正的表 (跳过用户数据/元表)
    if type(v) == "table" then
      -- 构建可读路径; 仅追加字符串键
      local nextPath = path
      if type(k) == "string" then
        nextPath = path .. k .. "."
      else
        nextPath = path .. "[" .. tostring(k) .. "]."
      end
      modify_flags(v, nextPath, depth + 1)
    end
  end
end

-- 启动时应用一次标记 (用 pcall 包装避免运行时错误)
pcall(function() modify_flags(ROOT, ROOT_NAME .. ".", 0) end)

-- 传送黑名单处理器
local TELEPORT_BLACKLIST_PATH = "C:\\temp\\Where Winds Meet\\Scripts\\teleport_blacklist.txt"
_G.TELEPORT_BLACKLIST = _G.TELEPORT_BLACKLIST or {} -- 查找表: id -> true

local function parse_ids_from_braced_list(s)
    local start_brace = s:find("{", 1, true)
    local end_brace = s:find("}", 1, true)
    if not start_brace or not end_brace then return {} end
    local inner = s:sub(start_brace + 1, end_brace - 1)
    local ids = {}
    for token in inner:gmatch("[^,]+") do
        local num = tonumber((token:gsub("%s+", "")))
        if num then table.insert(ids, num) end
    end
    return ids
end

-- 写入器: 始终写入大括号包裹的列表
local function write_teleport_blacklist_file(line_or_ids)
    pcall(function()
        local f = io.open(TELEPORT_BLACKLIST_PATH, "w")
        if not f then return end

        local out = nil
        if type(line_or_ids) == "table" then
            out = "{" .. table.concat(line_or_ids, ",") .. "}"
        elseif type(line_or_ids) == "string" then
            local start_brace = line_or_ids:find("{", 1, true)
            local end_brace = line_or_ids:find("}", 1, true)
            if start_brace and end_brace then
                out = line_or_ids:sub(start_brace, end_brace)
            else
                local ids = {}
                for token in line_or_ids:gmatch("%d+") do
                    table.insert(ids, tonumber(token))
                end
                out = "{" .. table.concat(ids, ",") .. "}"
            end
        else
            out = "{}"
        end

        f:write(out .. "\n")
        f:close()
    end)
end

local function load_teleport_blacklist_from_debug()
    -- 读取调试文件
    local ok, content = pcall(function()
        local f = io.open(DEBUG_FILE_PATH, "r")
        if not f then return nil end
        local data = f:read("*a")
        f:close()
        return data
    end)
    if not ok or not content then return end

    local last_line = nil
    for line in content:gmatch("[^\r\n]+") do
        if line:find("建议黑名单:", 1, true) or line:find("建议黑名单: {", 1, true) then
            last_line = line
        end
    end

    if not last_line then return end

    -- 解析ID并填充查找表
    local ids = parse_ids_from_braced_list(last_line)
    local new_lookup = {}
    for _, id in ipairs(ids) do new_lookup[id] = true end
    _G.TELEPORT_BLACKLIST = new_lookup

    -- 将标准化的黑名单写入文件持久化
    write_teleport_blacklist_file(ids)
    write_debug("[✔] 传送黑名单: 从调试文件加载 " .. tostring(#ids) .. " 个ID，并写入 " .. TELEPORT_BLACKLIST_PATH)
end

-- 启动时尝试加载已存在的 teleport_blacklist.txt (如果有) 并标准化格式
pcall(function()
    local f = io.open(TELEPORT_BLACKLIST_PATH, "r")
    if f then
        local content = f:read("*a")
        f:close()
        if content and content:find("{", 1, true) then
            local ids = parse_ids_from_braced_list(content)
            local lookup = {}
            for _, id in ipairs(ids) do lookup[id] = true end
            _G.TELEPORT_BLACKLIST = lookup
            -- 确保文件标准化为仅含大括号的格式
            write_teleport_blacklist_file(ids)
            write_debug("[✔] 传送黑名单: 从 " .. TELEPORT_BLACKLIST_PATH .. " 加载 " .. tostring(#ids) .. " 个ID")
        end
    end
end)

pcall(load_teleport_blacklist_from_debug)

-- 随机传送按钮的防抖调用器
_G._TELEPORT_DEBOUNCE = _G._TELEPORT_DEBOUNCE or false

local function SafeRunTeleportRandom(start_id, end_id, blacklist)
    if _G._TELEPORT_DEBOUNCE then
        write_debug("[传送] 忽略: 防抖机制已激活")
        return false
    end
    _G._TELEPORT_DEBOUNCE = true

    pcall(function()
        local director = cc.Director:getInstance()
        local scheduler = director and director:getScheduler()
        if scheduler and scheduler.scheduleScriptFunc then
            local id
            id = scheduler:scheduleScriptFunc(function()
                scheduler:unscheduleScriptEntry(id)
                _G._TELEPORT_DEBOUNCE = false
            end, 0.5, false)
        else
            _G._TELEPORT_DEBOUNCE = false
        end
    end)

    -- 如果有新的建议，从调试文件刷新黑名单
    pcall(load_teleport_blacklist_from_debug)

    -- 从传入参数或全局查找表构建黑名单数组
    local final_blacklist = {}
    if type(blacklist) == "table" then
        for _, v in ipairs(blacklist) do final_blacklist[#final_blacklist+1] = v end
    end
    -- 追加全局传送黑名单的键
    for id, _ in pairs(_G.TELEPORT_BLACKLIST or {}) do
        final_blacklist[#final_blacklist+1] = id
    end

    -- 确保随机种子 (仅需初始化一次)
    math.randomseed(os.time() + (os.clock() * 1000))

    -- 使用显式的起始/结束变量
    local s = tonumber(start_id) or 1 -- 起始ID (可修改此处)
    local e = tonumber(end_id) or 999999 -- 结束ID (可修改此处)

    -- 第一次尝试: 使用随机范围实现
    local ok, res = pcall(function()
        return _G.RunTeleportRandom_impl(s, e, final_blacklist)
    end)

    if ok and res then
        return res
    end

    -- 如果随机范围尝试失败，降级方案: 从 teleport_validList.txt 中选取一个有效ID
    -- 读取 teleport_validList.txt (支持大括号列表或换行分隔的ID)
    local valid_ids = {}
    pcall(function()
        local vf = io.open("C:\\temp\\Where Winds Meet\\Scripts\\Teleport_validList.txt", "r")
        if vf then
            local data = vf:read("*a")
            vf:close()
            if data then
                -- 优先尝试大括号列表
                local parsed = parse_ids_from_braced_list(data)
                if #parsed > 0 then
                    valid_ids = parsed
                else
                    -- 降级: 收集文件中所有数字
                    for token in data:gmatch("%d+") do
                        table.insert(valid_ids, tonumber(token))
                    end
                end
            end
        end
    end)

    -- 选取一个不在黑名单中的有效ID
    local chosen = nil
    if #valid_ids > 0 then
        -- 随机选取直到找到不在黑名单中的ID
        for i = 1, #valid_ids do
            local idx = math.random(1, #valid_ids)
            local cand = valid_ids[idx]
            if not _G.TELEPORT_BLACKLIST or not _G.TELEPORT_BLACKLIST[cand] then
                chosen = cand
                break
            end
        end
    end

    if chosen then
        local ok2, res2 = pcall(function()
            -- 通过调用范围实现来传送到单个ID (start=end=chosen)
            return _G.RunTeleportRandom_impl(chosen, chosen, final_blacklist)
        end)
        if ok2 and res2 then
            write_debug("[传送] 降级方案: 已传送到有效ID " .. tostring(chosen))
            return res2
        else
            write_debug("[传送] 降级方案传送ID " .. tostring(chosen) .. " 失败: " .. tostring(res2))
            _G._TELEPORT_DEBOUNCE = false
            return false
        end
    end

    -- 所有尝试均失败
    write_debug("[传送] SafeRunTeleportRandom 执行错误: " .. tostring(res))
    _G._TELEPORT_DEBOUNCE = false
    return false
end

_G.SafeRunTeleportRandom = SafeRunTeleportRandom

-- =================================================================================
-- 第二部分: 全局逻辑函数 (核心功能)
-- =================================================================================

-- 1. 视觉同步 (关联热键与菜单UI)
_G.ForceUpdateVisuals = function() end -- 前置声明
_G.ForceUpdateVisuals = _G.ForceUpdateVisuals or function() end -- 确保调试辅助函数存在

-- 确保全局标记存在
_G.GM_GODMODE   = _G.GM_GODMODE or false          -- 无敌模式
_G.GM_ONEHITKILL = _G.GM_ONEHITKILL or false      -- 一击必杀
_G.GM_STAMINA   = _G.GM_STAMINA or false          -- 无限体力
_G.GM_INVISIBLE = _G.GM_INVISIBLE or false        -- 隐身模式
_G.GM_NPCAI     = _G.GM_NPCAI or true             -- true = NPC AI启用 (智能)
_G.GM_NPCDUMB   = _G.GM_NPCDUMB or false          -- true = NPC智障 (应用buff)

-- 导入包装器 (降级到 package.loaded)
local function try_import(modname)
    local ok, mod = pcall(portable.import, modname)
    if ok and mod then return mod end
    -- 检查 package.loaded 表
    ok, mod = pcall(function() return package.loaded[modname] end)
    if ok and mod then return mod end
    -- 最后尝试 require (pcall 避免错误)
    ok, mod = pcall(require, modname)
    if ok and mod then return mod end

    return nil
end

-- 2. 无敌模式
function _G.GM_EnableGodmode()
    local mp_local = G and G.main_player
    if mp_local and mp_local.add_buff then
        pcall(mp_local.add_buff, mp_local, 70063)
    end
    _G.GM_GODMODE = true
    write_debug("[无敌模式] [✔] 无敌模式已启用")
    return true
end

function _G.GM_DisableGodmode()
    local a = try_import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
    if a and a.rm_buff then pcall(a.rm_buff, 70063) end
    _G.GM_GODMODE = false
    write_debug("[无敌模式] [✔] 无敌模式已禁用")
    return false
end

function _G.GM_ToggleGodmode()
    if _G.GM_GODMODE then return _G.GM_DisableGodmode() else return _G.GM_EnableGodmode() end
end

-- 3. 一击必杀
function _G.GM_EnableOneHit()
    local a = try_import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
    if a and a.set_niubility then pcall(a.set_niubility, 1) end
    _G.GM_ONEHITKILL = true
    write_debug("[一击必杀] 已启用")
    return true
end

function _G.GM_DisableOneHit()
    local a = try_import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
    if a and a.set_niubility then pcall(a.set_niubility, 0) end
    _G.GM_ONEHITKILL = false
    write_debug("[一击必杀] 已禁用")
    return false
end

-- 4. 无限体力
function _G.GM_EnableStamina()
    local a = try_import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
    if a and a.set_lock_res_consume then pcall(a.set_lock_res_consume, true) end
    _G.GM_STAMINA = true
    write_debug("[无限体力] 已启用")
    return true
end

function _G.GM_DisableStamina()
    local a = try_import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
    if a and a.set_lock_res_consume then pcall(a.set_lock_res_consume, false) end
    _G.GM_STAMINA = false
    write_debug("[无限体力] 已禁用")
    return false
end

-- 5. 隐身模式
_G.INVIS_STATE = _G.INVIS_STATE or 0 -- 0=关闭 1=单次生效 2=循环生效

function _G.GM_EnableInvisibility()
    local mp_local = G and G.main_player
    if mp_local and mp_local.add_buff then pcall(mp_local.add_buff, mp_local, 108010) end
    _G.GM_INVISIBLE = true
    write_debug("[隐身模式] [✔] 隐身模式已启用")
    return true
end

function _G.GM_DisableInvisibility()
    local a = try_import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
    if a and a.rm_buff then pcall(a.rm_buff, 108010) end
    _G.GM_INVISIBLE = false
    write_debug("[隐身模式] [✔] 隐身模式已禁用")
    return false
end

-- 执行隐身逻辑
_G.RunInvisibility = function()
    local mp = G and G.main_player
    if not mp then
        write_debug("[隐身模式] 错误: 未找到主玩家对象")
        return false
    end

    local state = tonumber(_G.INVIS_STATE) or 0

    -- 关闭状态: 确保移除buff并更新视觉
    if state == 0 then
        if _G.GM_INVISIBLE then
            pcall(_G.GM_DisableInvisibility)
            pcall(_G.ForceUpdateVisuals)
        end
        return false
    end

    -- 单次生效: 应用一次隐身，然后清空状态
    if state == 1 then
        local ok = pcall(function() _G.GM_EnableInvisibility() end)
        if ok then
            pcall(_G.ForceUpdateVisuals)
            write_debug("[隐身模式] 单次生效已应用 (状态=1)")
            -- 清空单次生效状态，避免下次循环重复应用
            _G.INVIS_STATE = 0
            return true
        else
            write_debug("[隐身模式] 单次生效应用失败")
            return false
        end
    end

    -- 循环生效: 确保每次循环都应用buff
    if state == 2 then
        local ok = pcall(function() _G.GM_EnableInvisibility() end)
        if ok then
            pcall(_G.ForceUpdateVisuals)
            write_debug("[隐身模式] 循环生效已确保应用 (状态=2)")
            return true
        else
            write_debug("[隐身模式] 循环生效应用失败")
            return false
        end
    end

    -- 意外值降级处理: 视为关闭
    if _G.GM_INVISIBLE then
        pcall(_G.GM_DisableInvisibility)
        pcall(_G.ForceUpdateVisuals)
    end
    return false
end

-- 执行器: 运行隐身逻辑的安全包装
_G.ExecuteInvisibility = function()
    -- 确保状态为数字
    local s = tonumber(_G.INVIS_STATE) or 0
    if s > 0 then
        return pcall(_G.RunInvisibility)
    else
        if _G.GM_INVISIBLE then
            pcall(_G.GM_DisableInvisibility)
            pcall(_G.ForceUpdateVisuals)
        end
        return false
    end
end

-- 6. NPC智障模式
_G.GM_NPCDUMB = _G.GM_NPCDUMB or false  -- false = NPC正常 (AI启用)

function _G.GM_EnableNPCDUMB()
    _G.GM_NPCDUMB = true

    local applied = false
    pcall(function()
        -- 尝试通过主玩家添加buff
        if G and G.main_player and G.main_player.add_buff then
            applied = pcall(G.main_player.add_buff, G.main_player, 380013)
        end

        -- 如果存在则调用 toggle_npc_ai
        if type(toggle_npc_ai) == "function" then
            pcall(toggle_npc_ai)
        end
    end)

    write_debug("[NPC智障模式] [✔] NPC智障模式已启用 (首次添加: " .. tostring(applied) .. ")")
    return true
end

function _G.GM_DisableNPCDUMB()
    _G.GM_NPCDUMB = false

    local removed = false
    pcall(function()
        -- 优先尝试通过模块移除
        local a = portable and portable.import and portable.import("hexm.client.ui.windows.gm.gm_combat.combat_train_action")
        if a and a.rm_buff then
            pcall(a.rm_buff, 380013)
            removed = true
        end

        -- 降级到主玩家移除方式
        if G and G.main_player then
            if G.main_player.remove_buff then pcall(G.main_player.remove_buff, G.main_player, 380013); removed = true end
            if G.main_player.rm_buff then pcall(G.main_player.rm_buff, G.main_player, 380013); removed = true end
        end

        -- 如果存在则调用 toggle_npc_ai
        if type(toggle_npc_ai) == "function" then
            pcall(toggle_npc_ai)
        end
    end)

    write_debug("[NPC智障模式] [✔] NPC智障模式已禁用 (Buff已移除: " .. tostring(removed) .. ")")
    return false
end 

-- 向下兼容的别名
_G.GM_EnableGod = _G.GM_EnableGodmode
_G.GM_DisableGod = _G.GM_DisableGodmode

_G.GM_EnableNPCAI = function()
    return _G.GM_DisableNPCDUMB()
end

_G.GM_DisableNPCAI = function()
    return _G.GM_EnableNPCDUMB()
end

_G.GM_ToggleNPCAI = function()
    if _G.GM_NPCDUMB then
        return _G.GM_DisableNPCDUMB() -- 当前智障 -> 恢复智能
    else
        return _G.GM_EnableNPCDUMB()  -- 当前智能 -> 设置智障
    end
end

-- 7. 状态恢复
_G.RunRecover = function()
    local function main()
        local mp_local = G and G.main_player
        if not mp_local then
            write_debug("[状态恢复] 错误: 未找到主玩家对象")
            return
        end

        local mod = "hexm.client.ui.windows.gm.gm_combat.combat_train_action"
        local action = nil
        pcall(function() action = portable.import(mod) end)

        if not action then
            write_debug("[状态恢复] 模块 "..mod.." 未找到，使用Buff降级方案")
            -- 降级方案: 直接应用恢复buff
            pcall(mp_local.add_buff, mp_local, 70141) -- 生命恢复buff
            pcall(mp_local.add_buff, mp_local, 70063) -- 体力/全状态恢复buff
        else
            -- 使用战斗动作模块
            pcall(function() action.recover_hp(1) end)               -- 恢复生命值
            pcall(function() action.fullfill_all_combat_res(1) end) -- 补满体力
			pcall(function() action.fulfill_all_combat_res(1) end) -- 补满体力(兼容拼写错误)
            write_debug("[状态恢复] 通过战斗动作模块应用恢复效果")
        end
    end

    pcall(main)
end

-- 8. 击杀NPC
_G.RunKillNPC = function()
    write_debug("[击杀NPC] 已触发")
    local count = 0
    local combat_action = nil
    pcall(function() combat_action = portable.import('hexm.client.ui.windows.gm.gm_combat.combat_train_action') end)

    if combat_action then
        pcall(function() combat_action.set_npc_mortal(true) end)
        pcall(function() combat_action.kill_all_npc() end)
    end

    -- BOSS伤害逻辑（从旧文件恢复）
    pcall(function()
        local target_id = mp:get_lock_target_id()
        if target_id then
            local target = G.space:get_entity(target_id)
            if target then
                pcall(function() target:force_set_HP(0, mp.entity_id, "gm") end)
                pcall(function() target:attr_set_HP(0, mp.entity_id, true, false) end)
                pcall(function() target:do_direct_damage(999999999, mp.entity_id, 0, 0, 0, 0) end)
                count = count + 1
            end
        end
    end)

    -- 区域清理循环
    pcall(function()
        local all = MEntityManager:GetAOIEntities()
        for i = 1, #all do
            local ent = all[i]
            local ok, name = pcall(function() return ent:GetName() end)
            if ok and name and (name:find('AiAvatar') or name:find('Npc') or name:find('Boss')) then
                local ok2, eid = pcall(function() return ent.entity_id end)
                if ok2 and eid then
                    local target = G.space:get_entity(eid)
                    if target and target ~= mp then
                        pcall(function() target:force_set_HP(0, mp.entity_id, "gm") end)
                        pcall(function() target:do_direct_damage(999999999, mp.entity_id, 0, 0, 0, 0) end)
                        count = count + 1
                    end
                end
            end
        end
    end)
    write_debug("[击杀NPC] 已击杀实体数量: " .. count)
end


-- 9. 自动拾取
_G.RunAutoLoot = function()
    local mp_local = G and G.main_player
    if not mp_local then
        write_debug("[自动拾取] 错误: 未找到主玩家对象")
        return
    end

    local interact_misc = nil
    pcall(function() interact_misc = portable.import('hexm.common.misc.interact_misc') end)

    -- 1) 收集物拾取
    pcall(function() mp_local:ride_skill_collect_nearby_collections(5000) end)

    -- 2) 奖励拾取
    pcall(function()
        local rewards = mp_local:ride_skill_find_nearest_kill_reward(5000)
        if rewards then
            pcall(function() mp_local:ride_skill_get_kill_reward(rewards) end)
        end
    end)

    -- 3) 掉落物拾取
    pcall(function()
        local drops = DropManager.get_nearby_drop_entities(5000)
        if drops then
            for _, eid in ipairs(drops) do
                pcall(function() mp_local:pick_drop_item(eid) end)
                pcall(function() mp_local:pick_reward_item(eid) end)
            end
        end
    end)

    -- 4) 过滤逻辑
    local ok_pos, playerPos = pcall(function() return mp_local:get_position() end)
    if not ok_pos or not playerPos then playerPos = {x=0,y=0,z=0} end

    local entities = MEntityManager:GetAOIEntities() or {}
    local targets = {}

    -- 忽略的名称模式（不区分大小写）
    local IGNORE_NAME_PATTERNS = {
        "开锁器", "电梯", "迷你游戏", "小游戏", "解谜", "象棋", "节奏", 
        "酒坛", "投壶", "老虎机", "扭蛋", "撬锁", "拉杆"
    }

    -- 忽略的状态/配置ID（填入需要加入黑名单的ID）
    local IGNORE_STATUS_IDS = {
        -- [12345] = true, -- 示例：添加需要跳过的status_no值
    }
    local IGNORE_CONFIG_IDS = {
        -- [54321] = true, -- 示例：添加需要跳过的config_no值
    }

    local function name_matches_ignore(name)
        if not name then return false end
        local lname = string.lower(name)
        for _, pat in ipairs(IGNORE_NAME_PATTERNS) do
            if lname:find(pat, 1, true) then
                return true
            end
        end
        return false
    end

    for i = 1, #entities do
        local ent = entities[i]
        local ok, name = pcall(function() return ent:GetName() end)
        if ok and name and name:find('InteractComEntity') then
            -- 快速名称过滤
            if name_matches_ignore(name) then
                write_debug("[自动拾取] 根据名称跳过: " .. tostring(name))
            else
                local ok2, eno = pcall(function() return ent:GetEntityNo() end)
                local ok3, eid = pcall(function() return ent.entity_id end)
                if ok2 and ok3 and eid then
                    local luaEnt = G.space and G.space:get_entity(eid)
                    if luaEnt then
                        local ok4, comp = pcall(function() return luaEnt:get_interact_comp(eid) end)
                        if ok4 and comp and comp.position then
                            -- 组件级过滤：检查状态/配置ID
                            local skip = false

                            -- 检查status_no（可能是单个值或表）
                            if comp.status_no then
                                if type(comp.status_no) == "table" then
                                    for _, sid in ipairs(comp.status_no) do
                                        if IGNORE_STATUS_IDS[sid] then skip = true; break end
                                    end
                                else
                                    if IGNORE_STATUS_IDS[comp.status_no] then skip = true end
                                end
                            end

                            -- 检查config_no（可能是单个值或表）
                            if not skip and comp.config_no then
                                if type(comp.config_no) == "table" then
                                    for _, cid in ipairs(comp.config_no) do
                                        if IGNORE_CONFIG_IDS[cid] then skip = true; break end
                                    end
                                else
                                    if IGNORE_CONFIG_IDS[comp.config_no] then skip = true end
                                end
                            end

                            if skip then
                                write_debug(string.format("[自动拾取] 根据黑名单组件ID跳过实体 id=%s", tostring(eid)))
                            else
                                local dx = playerPos.x - comp.position[1]
                                local dy = playerPos.y - comp.position[2]
                                local dz = playerPos.z - comp.position[3]
                                local dist = math.sqrt(dx*dx + dy*dy + dz*dz)

                                -- 根据实体名称确定优先级（优先处理包含特定名称的实体）
                                local ent_name = nil
                                if name and type(name) == "string" then
                                    ent_name = name
                                else
                                    local okn, n2 = pcall(function() return ent:GetName() end)
                                    if okn and type(n2) == "string" then ent_name = n2 end
                                end

                                local priority = 1
                                if ent_name and ent_name:find("ins_entity", 1, true) then
                                    priority = 0
                                end

                                table.insert(targets, {
                                    entity_no = eno,
                                    entity_id = eid,
                                    luaEnt = luaEnt,
                                    comp = comp,
                                    distance = dist,
                                    priority = priority
                                })
                            end
                        end
                    end
                end
            end
        end
    end

    table.sort(targets, function(a, b)
        if a.priority ~= b.priority then return a.priority < b.priority end
        return a.distance < b.distance
    end)

    for i = 1, #targets do
        local t = targets[i]
        local ways = {}
        local seen = {}

        if interact_misc then
            local ok_ways, possible = pcall(function()
                return interact_misc.get_all_possible_active_ways(t.entity_no)
            end)
            if ok_ways and possible then
                for _, w in ipairs(possible) do
                    if not seen[w] then seen[w] = true; table.insert(ways, w) end
                end
            end
        end

        local comp_id = nil
        if t.comp and t.comp.components then
            for cid, comp_data in pairs(t.comp.components) do
                comp_id = cid
                if comp_data.status_no and not seen[comp_data.status_no] then
                    seen[comp_data.status_no] = true
                    table.insert(ways, comp_data.status_no)
                end
                if comp_data.config_no and not seen[comp_data.config_no] then
                    seen[comp_data.config_no] = true
                    table.insert(ways, comp_data.config_no)
                end
            end
        end

        if #ways > 0 then
            pcall(function() mp_local:set_interact_target_id(t.entity_id) end)
            for _, way in ipairs(ways) do
                pcall(function()
                    mp_local:trigger_active_interact(way, t.entity_id, nil, nil, comp_id)
                end)
            end
            pcall(function() mp_local:trigger_active_interact() end)
        end
    end

    write_debug("[自动拾取] 执行完成")
end

-- 10. 速度调节逻辑（拆分独立控制版本）

-- ==================== 旧版对话速度调节（独立控制） ====================
local function apply_old_dialog_speed(speed, enable)
    if G then
        if G.dialog_global_time_scale ~= nil then G.dialog_global_time_scale = speed end
        if G.space then
            local space = G.space
            if space.dialog_global_time_scale ~= nil then space.dialog_global_time_scale = speed end
            if space.dialog_set_global_time_scale then pcall(space.dialog_set_global_time_scale, space, speed) end
            if space.imp_dialogs_manager then
                local dm = space.imp_dialogs_manager
                if dm.dialog_set_global_time_scale then pcall(dm.dialog_set_global_time_scale, dm, speed) end
            end
        end
        local mp = G.main_player
        if mp and mp.dialog_set_time_speed_scale then
            pcall(mp.dialog_set_time_speed_scale, mp, enable, speed)
        end
    end
end

-- 对外暴露：仅设置对话速度的函数
_G.RunSetDialogSpeed = function(valIndex)
    local speeds = {1.0}
    local targetSpeed = speeds[valIndex] or 1.0
    
    -- 仅执行对话速度调节
    apply_old_dialog_speed(targetSpeed, false)
    write_debug("通过旧版接口设置对话速度为: x" .. targetSpeed)
end

-- ==================== 新版GM攻击速度调节（独立控制） ====================
local function apply_new_gm_speed(speed)
    local mod = "hexm.client.ui.windows.gm.gm_combat.combat_train_action"
    local action = nil
    pcall(function() action = portable.import(mod) end)
    if action and action.set_game_speed then
        pcall(action.set_game_speed, speed)
        return true
    end
    return false
end

-- 对外暴露：仅设置GM攻击速度的函数
_G.RunSetGMSpeed = function(valIndex)
    local speeds = {1.0, 1.5, 3.0, 5.0, 7.5, 10.0, 30.0}
    local targetSpeed = speeds[valIndex] or 1.0

    -- 仅执行GM攻击速度调节
    local ok_new = apply_new_gm_speed(targetSpeed)

    if ok_new then
        write_debug("通过GM接口设置攻击速度为: x" .. targetSpeed)
    else
        write_debug("未找到GM速度设置接口，攻击速度未修改")
    end
end

-- （可选）保留原有函数，兼容旧调用（同时设置两种速度）
_G.RunSetSpeed = function(valIndex)
    -- 先调用对话速度设置
    _G.RunSetDialogSpeed(valIndex)
    -- 再调用GM攻击速度设置
    _G.RunSetGMSpeed(valIndex)
end

_G.RunSetSpeed = function(valIndex)
    local speeds = {1.0, 1.5, 3.0, 5.0, 7.5, 10.0, 30.0}
    local targetSpeed = speeds[valIndex] or 1.0
    local isEnable = (valIndex > 1)

    -- 移除GM接口调用逻辑，仅执行对话加速
    apply_old_dialog_speed(targetSpeed, isEnable)

    -- 修改调试日志，仅提示对话加速相关信息
    write_debug("通过对话接口设置速度为: " .. targetSpeed .. "倍")
end

-- 自动Buff、永久Buff、移除Buff共享的Buff列表
-- 字段说明:
--   id     : Buff ID（数字）
--   note   : Buff描述
--   auto   : 是否在自动Buff中应用
--   perm   : 是否在永久Buff中应用
--   remove : 是否可移除（false则跳过移除）
local BUFF_LIST = {
    -- 伤害类Buff
	{ id = 1053027, note = "增伤32倍%", auto = true, perm = true, remove = true },

    -- 伤害类Buff（暂禁用，待测试）
	{ id = 1053026, note = "增伤16倍%", auto = false, perm = false, remove = true },

    -- 防御类Buff
    { id = 109927, note = "受到伤害减少80%", auto = true, perm = true, remove = true },

    -- 治疗类Buff（暂禁用，待测试）
    { id = 102707, note = "恢复20%生命值", auto = false, perm = false, remove = true },
    { id = 109603, note = "药品治疗效果+15%（系列）", auto = false, perm = false, remove = true },
    { id = 102410, note = "击杀时恢复2%-4%生命值", auto = false, perm = false, remove = true },
    { id = 102460, note = "击杀时恢复4%生命值", auto = false, perm = false, remove = true },

    -- 功能类Buff
	{ id = 200005, note = "+10%移动速度", auto = true,  perm = true,  remove = true },

	
	-- 功能类Buff（暂禁用，待测试）
    { id = 30005,  note = "免疫控制效果", auto = false, perm = false, remove = true },   
    { id = 70025,  note = "免疫招架", auto = false, perm = false, remove = true },
    { id = 70110,  note = "免疫沉默", auto = false, perm = false, remove = true },
    { id = 104009, note = "心智：心智+5%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 104010, note = "体魄：体魄+5%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 104011, note = "经脉：经脉+5%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 104012, note = "脏腑：脏腑+5%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 103007, note = "物理攻击降低，鸣钟式伤害提升", auto = false, perm = false, remove = true },
    { id = 103008, note = "物理攻击降低，裂石式伤害提升", auto = false, perm = false, remove = true },
    { id = 102502, note = "闪避技能内力消耗减少10%；闪避后获得10%减伤效果持续10秒", auto = false, perm = false, remove = true },
    { id = 102503, note = "成功弹反后，受到的所有伤害减少30%，造成的所有伤害增加10%", auto = false, perm = false, remove = true },
    { id = 102505, note = "招架强化：成功招架不再消耗内力；减伤效果提升10%", auto = false, perm = false, remove = true },
    { id = 102508, note = "内力恢复：恢复速度+20%；闪避后获得10%增伤和20%加速效果持续5秒", auto = false, perm = false, remove = true },
    { id = 102705, note = "闪避后伤害+5% 持续10秒", auto = false, perm = false, remove = true },
    { id = 102706, note = "弹反后伤害+5% 持续10秒", auto = false, perm = false, remove = true },
    { id = 104001, note = "亲和：获得的好感度+10%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 102605, note = "鸣钟式伤害+200", auto = false, perm = false, remove = true },
    { id = 102606, note = "裂石式伤害+200", auto = false, perm = false, remove = true },
    { id = 102607, note = "缠丝式伤害+200", auto = false, perm = false, remove = true },
    { id = 102701, note = "技能冷却时间减少20%", auto = false, perm = false, remove = true },
    { id = 102702, note = "内力消耗减少20%", auto = false, perm = false, remove = true },
    { id = 102703, note = "闪避无敌时间+50%", auto = false, perm = false, remove = true },
    { id = 102704, note = "弹反判定窗口+50%", auto = false, perm = false, remove = true },
    { id = 104002, note = "美食：食谱图鉴 - 获得的食物效果+20%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 104003, note = "疾行：移动速度+2%（系列不叠加）", auto = false, perm = false, remove = true },
    { id = 104004, note = "轻身：轻身术效果+5%（系列不叠加）", auto = false, perm = false, remove = true },

    -- 恢复类Buff
    { id = 70141, note = "恢复类Buff", auto = true,  perm = true,  remove = true },
    { id = 70063, note = "体力/全状态恢复", auto = true,  perm = true,  remove = true },

    -- 食物类Buff
       { id = 109506, note = "食物效果：最大生命值+5600（系列不叠加）", auto = true, perm = true, remove = true },

    -- 采集类Buff（暂禁用，待测试）
    { id = 104013, note = "采药：采药暴击率+1%（系列）", auto = false, perm = true, remove = true },
    { id = 104014, note = "采矿：采矿暴击率+1%（系列）", auto = false, perm = true, remove = true },
    { id = 104015, note = "伐木：伐木暴击率+1%（系列）", auto = false, perm = true, remove = true },
    { id = 104016, note = "垂钓：钓鱼暴击率+1%（系列）", auto = false, perm = true, remove = true },
    { id = 104017, note = "狩猎：狩猎暴击率+1%（系列）", auto = false, perm = true, remove = true },
    { id = 104018, note = "锻造1：锻造1暴击率+1%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104019, note = "锻造2：锻造2暴击率+1%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104020, note = "锻造3：锻造3暴击率+1%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104021, note = "锻造4：锻造4暴击率+1%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104022, note = "采药：采药暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104023, note = "采药：采药暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104024, note = "采药：采药暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104025, note = "采药：采药暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104026, note = "采药：采药暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104027, note = "采药：采药暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104028, note = "采矿：采矿暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104029, note = "采矿：采矿暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104030, note = "采矿：采矿暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104031, note = "采矿：采矿暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104032, note = "采矿：采矿暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104033, note = "采矿：采矿暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104034, note = "伐木：伐木暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104035, note = "伐木：伐木暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104036, note = "伐木：伐木暴击率+20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104037, note = "伐木：伐木暴击率+20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104038, note = "伐木：伐木暴击率+20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104039, note = "伐木：伐木暴击率+20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104040, note = "垂钓：钓鱼暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104041, note = "垂钓：钓鱼暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104042, note = "垂钓：钓鱼暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104043, note = "垂钓：钓鱼暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104044, note = "垂钓：钓鱼暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104045, note = "垂钓：钓鱼暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104046, note = "狩猎：狩猎暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104047, note = "狩猎：狩猎暴击率+7.5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104048, note = "狩猎：狩猎暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104049, note = "狩猎：狩猎暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104050, note = "狩猎：狩猎暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 104051, note = "狩猎：狩猎暴击率+10%（系列不叠加）", auto = false, perm = true, remove = true },
    
    -- 卷轴类Buff
    { id = 109604, note = "卷轴&心法：闪避消耗-5%，速度+5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 109605, note = "卷轴&心法：打断抗性+20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 109606, note = "卷轴&心法：内力恢复提升5%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 109607, note = "卷轴&心法：生命值低于30%时，造成/受到的伤害+20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 109608, note = "卷轴&心法：生命值低于30%时，造成/受到的伤害-20%（系列不叠加）", auto = false, perm = true, remove = true },
    { id = 109609, note = "卷轴&心法：被控期间受到的伤害-20%（系列不叠加）", auto = false, perm = true, remove = true },

    -- BOSS变身Buff（暂禁用，待测试）
    { id = 30385, note = "一刀（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30386, note = "天鹰（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30387, note = "睡道人（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30388, note = "木鸢（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30389, note = "郑鄂（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30390, note = "地煞神（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30391, note = "龙王（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30392, note = "虚空之王（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30393, note = "幸运十七（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30394, note = "蛇医（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30395, note = "河伯（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30396, note = "幸运十七第二阶段（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30397, note = "道主（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30398, note = "傀儡师（BOSS变身）", auto = false, perm = false, remove = true },
    { id = 30399, note = "傀儡师？（额外检查）", auto = false, perm = false, remove = true },
    { id = 30384,  note = "BOSS动画/变身效果", auto = false, perm = false, remove = true },

    -- 有问题/冗余/特效过亮/已禁用
	{ id = 30335, note = "照明效果", auto = false, perm = false, remove = true },
    { id = 70138, note = "复活等级3，VIP等级+4（有问题）", auto = false, perm = false, remove = true },
    { id = 30346, note = "神闪避：远程攻击弹反（有问题）", auto = false, perm = false, remove = true },
    { id = 70021, note = "[警告] 会将你传送到地图另一端（避免使用）", auto = false, perm = false, remove = true },
    { id = 70132, note = "复活等级1", auto = false, perm = false, remove = true },
    { id = 70182, note = "冰盾（VIP专属）", auto = false, perm = false, remove = true },
    { id = 70183, note = "火盾（VIP专属）", auto = false, perm = false, remove = true },
    { id = 70186, note = "火焰之力", auto = false, perm = false, remove = true },
    { id = 70187, note = "冰花之力", auto = false, perm = false, remove = true },
    { id = 200095, note = "+20% 基于生命值的伤害", auto = false, perm = false, remove = true },
    { id = 70045,  note = "未知/不稳定状态", auto = false, perm = false, remove = true },

    -- 警告！会导致角色死亡（已禁用）
    { id = 30355, note = "[警告] 会导致角色死亡（重复检查）", auto = false, perm = false, remove = true },
    { id = 109620, note = "[警告] 会导致角色死亡（重复检查）", auto = false, perm = false, remove = true },
    { id = 109624, note = "[警告] 会导致角色死亡", auto = false, perm = false, remove = true },

    -- 待测试（暂禁用）
    { id = 106006, note = "禁锢状态", auto = false, perm = false, remove = true },
    { id = 107102, note = "轻身步效果", auto = false, perm = false, remove = true },
    { id = 109623, note = "添加发光效果", auto = false, perm = false, remove = true },

    -- 警告！除非测试否则始终禁用
    { id = 30404,  note = "强刃（受击时反击）", auto = false, perm = false, remove = true },
    { id = 30308,  note = "伤害转移", auto = false, perm = false, remove = true },
	{ id = 108010, note = "隐身效果", auto = false, perm = false, remove = true },
    { id = 200031, note = "GM无敌状态 - 游戏管理员永生效果", auto = false, perm = false, remove = true },
    { id = 380013, note = "NPC智障模式", auto = false, perm = false, remove = true },
}

-- 11. 自动施加Buff
_G.RunAutoBuff = function()
    local mp = G and G.main_player
    if not mp then
        write_debug("[自动施加Buff] 错误：未找到主玩家对象")
        return
    end

    write_debug("[自动施加Buff] 开始自动施加Buff流程")

    local applied_count = 0
    for _, entry in ipairs(BUFF_LIST) do
        if entry.auto then
            local ok = pcall(mp.add_buff, mp, entry.id)
            if ok then
                applied_count = applied_count + 1
                write_debug(string.format("[自动施加Buff] 成功施加Buff %d - %s", entry.id, entry.note or ""))
            else
                write_debug(string.format("[自动施加Buff] 施加Buff %d 失败 - %s", entry.id, entry.note or ""))
            end
        end
    end

    write_debug(string.format("[自动施加Buff] 自动施加Buff流程完成。累计成功施加：%d", applied_count))
end

-- 12. 永久Buff
_G.RunPermanentBuffs = function()
    write_debug("[永久Buff] 开始施加Buff")

    local mp_local = G and G.main_player
    if not mp_local then
        write_debug("[永久Buff] 错误：未找到主玩家对象")
        return
    end

    local action = nil
    pcall(function()
        action = portable.import('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
    end)

    if not action then
        write_debug("[永久Buff] 错误：combat_train_action模块不可用")
        return
    end

    local eid      = mp_local.entity_id
    local reason   = "gm"
    local level    = 5
    local duration = 10675199116730015 -- 极长的持续时间

    local function apply_buff(buff_id, note)
        local ok = pcall(function()
            action.add_buff(buff_id, eid, duration, level, eid, reason)
        end)
        if ok then
            write_debug(string.format("[永久Buff] 成功添加Buff ID=%d (%s)", buff_id, note or ""))
            return true
        else
            local ok2 = pcall(function()
                action:add_buff(buff_id, eid, duration, level, eid, reason)
            end)
            if ok2 then
                write_debug(string.format("[永久Buff] 成功添加Buff(方法调用) ID=%d (%s)", buff_id, note or ""))
                return true
            else
                write_debug(string.format("[永久Buff] 添加Buff失败 ID=%d (%s)", buff_id, note or ""))
                return false
            end
        end
    end

    local applied = 0
    for _, entry in ipairs(BUFF_LIST) do
        if entry.perm then
            if apply_buff(entry.id, entry.note) then
                applied = applied + 1
            end
        end
    end

    write_debug(string.format("[永久Buff] 施加流程完成。累计成功施加：%d", applied))
end

-- 13. 移除Buff
_G.RunRemoveBuffs = function()
    write_debug("[移除Buff] 开始移除Buff流程")

    local mp_local = G and G.main_player
    if not mp_local then
        write_debug("[移除Buff] 错误：未找到主玩家对象")
        return
    end

    local mod_name = "hexm.client.ui.windows.gm.gm_combat.combat_train_action"
    local action = nil
    pcall(function() action = portable.import(mod_name) end)
    write_debug("[移除Buff] combat_train_action模块已加载：" .. tostring(action ~= nil))

    local function safe_call(desc, fn)
        local ok, err = pcall(fn)
        if ok then
            write_debug("[移除Buff] ✔ " .. desc)
            return true
        else
            write_debug("[移除Buff] ✘ " .. desc .. " -> " .. tostring(err))
            return false
        end
    end

    local function has_buff(buff_id)
        local ok, res

        ok, res = pcall(function()
            if mp_local.has_buff then return mp_local.has_buff(mp_local, buff_id) end
            if mp_local["has_buff"] and type(mp_local["has_buff"]) == "function" then return mp_local:has_buff(buff_id) end
            return nil
        end)
        if ok and res ~= nil then return res end

        ok, res = pcall(function()
            if mp_local.get_buff then return mp_local.get_buff(mp_local, buff_id) ~= nil end
            if mp_local["get_buff"] and type(mp_local["get_buff"]) == "function" then return mp_local:get_buff(buff_id) ~= nil end
            return nil
        end)
        if ok and res ~= nil then return res end

        ok, res = pcall(function()
            if mp_local.buffs and type(mp_local.buffs) == "table" then
                if mp_local.buffs[buff_id] then return true end
                for _, v in pairs(mp_local.buffs) do
                    if type(v) == "table" and (v.id == buff_id or v.buff_id == buff_id) then
                        return true
                    end
                end
            end
            return nil
        end)
        if ok and res ~= nil then return res end

        return false
    end

    local function remove_buff(buff_id)
        local eid = mp_local and mp_local.entity_id
        local removed = false

        if action and action.rm_buff then
            removed = safe_call(string.format("action.rm_buff(%d)", buff_id), function() action.rm_buff(buff_id) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:rm_buff(%d)", buff_id), function() action:rm_buff(buff_id) end) or removed
            if removed then return true end
        end

        if action and action.remove_buff then
            removed = safe_call(string.format("action.remove_buff(%d, %s)", buff_id, tostring(eid)), function() action.remove_buff(buff_id, eid) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:remove_buff(%d, %s)", buff_id, tostring(eid)), function() action:remove_buff(buff_id, eid) end) or removed
            if removed then return true end
        end

        if action and action.del_buff then
            removed = safe_call(string.format("action.del_buff(%d, %s)", buff_id, tostring(eid)), function() action.del_buff(buff_id, eid) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:del_buff(%d, %s)", buff_id, tostring(eid)), function() action:del_buff(buff_id, eid) end) or removed
            if removed then return true end
        end

        if action and action.clear_buff then
            removed = safe_call(string.format("action.clear_buff(%d, %s)", buff_id, tostring(eid)), function() action.clear_buff(buff_id, eid) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:clear_buff(%d, %s)", buff_id, tostring(eid)), function() action:clear_buff(buff_id, eid) end) or removed
            if removed then return true end
        end

        if mp_local then
            if mp_local.remove_buff then
                removed = safe_call(string.format("mp.remove_buff(mp, %d)", buff_id), function() mp_local.remove_buff(mp_local, buff_id) end) or removed
                if removed then return true end
                removed = safe_call(string.format("mp:remove_buff(%d)", buff_id), function() mp_local:remove_buff(buff_id) end) or removed
                if removed then return true end
            end

            if mp_local.del_buff then
                removed = safe_call(string.format("mp.del_buff(mp, %d)", buff_id), function() mp_local.del_buff(mp_local, buff_id) end) or removed
                if removed then return true end
                removed = safe_call(string.format("mp:del_buff(%d)", buff_id), function() mp_local:del_buff(buff_id) end) or removed
                if removed then return true end
            end

            if mp_local.clear_all_buffs then
                removed = safe_call("mp.clear_all_buffs(mp)", function() mp_local.clear_all_buffs(mp_local) end) or removed
                if removed then return true end
            end
        end

        if not removed then
            write_debug(string.format("[移除Buff] 无法通过已知方法移除Buff ID=%d", buff_id))
        end

        return removed
    end

    -- 遍历共享的BUFF_LIST，尝试移除所有remove≠false的条目
    local removed_count = 0
    for _, entry in ipairs(BUFF_LIST) do
        if entry.remove == false then
            write_debug(string.format("[移除Buff] 跳过ID=%d (%s) - 移除标记为false", entry.id or -1, entry.note or ""))
        else
            local id = entry.id
            local note = entry.note or ""
            local present = false
            local ok_has, pres = pcall(function() return has_buff(id) end)
            if ok_has and pres then present = true end

            if not present then
                write_debug(string.format("[移除Buff] 跳过ID=%d (%s) - 未检测到该Buff", id, note))
            else
                if remove_buff(id) then
                    write_debug(string.format("[移除Buff] 成功移除Buff ID=%d (%s)", id, note))
                    removed_count = removed_count + 1
                else
                    write_debug(string.format("[移除Buff] 移除Buff失败 ID=%d (%s)", id, note))
                end
            end
        end
    end

-- 配置开关
RUN_FALLBACK_SWEEP = false        -- 必须设为true或"force"才会执行兜底扫描
FALLBACK_THRESHOLD = 0            -- 当移除成功数≤该值时执行兜底扫描

-- 兜底扫描函数（仅作为最后手段执行）
local function fallback_sweep()
    pcall(function()
        collectgarbage("collect")
        local mp = G and G.main_player
        if not mp then return end

        local ok, act = pcall(require, "hexm.client.ui.windows.gm.gm_combat.combat_train_action")
        local use_act = ok and act and act.rm_buff

        write_debug("[移除Buff] 开始兜底扫描 ID范围30000 -> 201000; 使用act.rm_buff=" .. tostring(use_act))

        local start_id, end_id = 30000, 201000

        -- 可调整的参数
        local chunk_size = 5        -- 内层循环大小，控制单次迭代耗时
        local group_size = 50      -- 每组处理的ID数量（处理完后暂停）
        local pause_seconds = 2      -- 组间暂停秒数（避免界面卡死）
        local removed_total = 0

        -- 不依赖os.execute的便携版sleep辅助函数
        local function sleep(sec)
            if not sec or sec <= 0 then return end

            -- 1) 如果有scheduler.yield，通过重复yield模拟秒数
            if type(scheduler) == "table" and type(scheduler.yield) == "function" then
                local t0 = os.time()
                while os.time() - t0 < sec do
                    scheduler.yield()
                end
                return
            end

            -- 2) 如果有LuaSocket库
            local ok_socket, socket = pcall(require, "socket")
            if ok_socket and socket and type(socket.sleep) == "function" then
                socket.sleep(sec)
                return
            end

            -- 3) 如果运行在协程中，使用coroutine.yield模拟
            if coroutine and type(coroutine.yield) == "function" then
                local t0 = os.time()
                while os.time() - t0 < sec do
                    coroutine.yield()
                end
                return
            end

            -- 4) 兜底方案：使用os.clock忙等（尽可能降低阻塞）
            local t0 = os.clock()
            while os.clock() - t0 < sec do
                -- 空操作避免死循环；如果有coroutine.yield则短暂yield
                if coroutine and type(coroutine.yield) == "function" then
                    coroutine.yield()
                end
            end
        end

        -- 按组迭代（大区间拆分为小组，避免长时间阻塞）
        for group_base = start_id, end_id, group_size do
            local group_top = math.min(group_base + group_size - 1, end_id)
            -- 小组内再拆分为更小的块处理
            for base = group_base, group_top, chunk_size do
                local top = math.min(base + chunk_size - 1, group_top)
                local removed_in_chunk = 0

                for i = base, top do
                    local ok1 = false
                    if mp.remove_buff then
                        ok1 = pcall(mp.remove_buff, mp, i)
                    end
                    if use_act then
                        pcall(act.rm_buff, i)
                    end
                    if ok1 then removed_in_chunk = removed_in_chunk + 1 end
                end

                removed_total = removed_total + removed_in_chunk
                write_debug(string.format("[移除Buff] 已处理ID范围%d-%d，本次移除%d个，累计移除%d个", base, top, removed_in_chunk, removed_total))
                collectgarbage("collect")
            end

            -- 组间暂停，避免环境卡死
            write_debug(string.format("[移除Buff] 完成ID组%d-%d处理，暂停%d秒", group_base, group_top, pause_seconds))
            sleep(pause_seconds)
        end

        write_debug("[移除Buff] 兜底扫描完成。累计移除数量（mp.remove_buff成功数）：" .. removed_total)
        collectgarbage("collect")
    end)
end

-- 判断是否执行兜底扫描
-- 强制将RUN_FALLBACK_SWEEP作为总开关：除非显式启用，否则不会执行兜底扫描
if not (RUN_FALLBACK_SWEEP == true or RUN_FALLBACK_SWEEP == "force") then
    write_debug(string.format("[移除Buff] 跳过兜底扫描，原因：RUN_FALLBACK_SWEEP=%s", tostring(RUN_FALLBACK_SWEEP)))
else
    if removed_count <= FALLBACK_THRESHOLD then
        write_debug(string.format("[移除Buff] 执行兜底扫描，原因：移除成功数=%d ≤ 阈值%d", removed_count, FALLBACK_THRESHOLD))
        fallback_sweep()
    else
        write_debug(string.format("[移除Buff] 不执行兜底扫描，原因：移除成功数=%d > 阈值%d", removed_count, FALLBACK_THRESHOLD))
    end
end


    write_debug(string.format("[移除Buff] 移除流程完成。累计移除成功：%d", removed_count))
end

-- 14. 重置犯罪值
_G.ResetCrime = function()
    pcall(function()
        local dec = package.loaded["hexm.client.debug.gm.gm_decorator"]
            or (pcall(require, "hexm.client.debug.gm.gm_decorator") and package.loaded["hexm.client.debug.gm.gm_decorator"])

        if dec and dec.gm_command_short_cuts and dec.gm_command_short_cuts.game then
            local cmds = dec.gm_command_short_cuts.game
            -- [重置犯罪值] —— 传送或重置副本/区域时生效
            if cmds["$forbid_witness_wanfa"] then pcall(cmds["$forbid_witness_wanfa"], 1) end
            if cmds["$forbid_police_wanfa"] then pcall(cmds["$forbid_police_wanfa"], 1) end
        end
    end)

    if type(write_debug) == "function" then
        write_debug("[重置犯罪值] 已执行 —— 传送或重置副本/区域时生效")
    end
end

-- 向后兼容的本地函数引用
local function reset_crime()
    return _G.ResetCrime()
end

-- 15. 传送至暮云顶
_G.RunTeleportBlissfulRetreat = function()
    pcall(function()
        local ok, skip = pcall(require, "hexm.client.ui.windows.gm.gm_skip_window")
        if ok and skip and skip.gm_skip_flow_imp then
            -- 使用场景ID 7 传送至忘忧谷（注：原注释room id 7对应Blissful Retreat，实际代码传11）
            pcall(skip.gm_skip_flow_imp, 11, nil)
            write_debug("[传送至暮云顶] 已传送至场景ID 11")
        else
            write_debug("[传送至暮云顶] 跳转模块不可用")
        end
    end)
end

-- 16. 随机传送
-- 16. 随机传送
-- 16. 随机传送（已修改：持久化保存成功的跳转传送ID）
_G._TELEPORT_INVALID_IDS = _G._TELEPORT_INVALID_IDS or {}

-- 有效ID列表持久化存储路径
local TELEPORT_VALID_PATH = "C:\\temp\\Where Winds Meet\\Scripts\\Teleport_validList.txt"
_G._TELEPORT_VALID_IDS = _G._TELEPORT_VALID_IDS or {}

-- 用户指定的初始有效ID（会被持久化保存）
local rids = {6,7,8,9,10,11,12,13,14,16}

-- 辅助函数：写入有效ID列表文件（覆盖写入）
local function write_teleport_valid_file(line_text)
    pcall(function()
        local f = io.open(TELEPORT_VALID_PATH, "w")
        if f then
            f:write(line_text .. "\n")
            f:close()
        end
    end)
end

-- 辅助函数：将当前_G._TELEPORT_VALID_IDS持久化为大括号包裹的列表
local function persist_valid_set()
    local merged_list = {}
    for id, _ in pairs(_G._TELEPORT_VALID_IDS) do
        merged_list[#merged_list + 1] = tonumber(id)
    end
    table.sort(merged_list)
    local parts = {}
    for i = 1, #merged_list do parts[#parts+1] = tostring(merged_list[i]) end
    local list_str = "{" .. table.concat(parts, ",") .. "}"
    write_teleport_valid_file(list_str)
end

-- 辅助函数：添加单个ID到有效集合并持久化
local function add_valid_id(id)
    id = tonumber(id)
    if not id then return end
    -- 已存在则不处理（避免重复写入）
    if _G._TELEPORT_VALID_IDS[id] then
        write_debug("[随机传送] 有效ID已存在：" .. tostring(id))
        return
    end
    -- 添加并持久化
    _G._TELEPORT_VALID_IDS[id] = true
    persist_valid_set()
    write_debug("[随机传送] 已将有效ID添加至传送有效列表：" .. tostring(id))
end

-- 将初始ID种子写入有效集合（通过add_valid_id触发文件写入）
pcall(function()
    local initial_ids = rids or {}
    for _, id in ipairs(initial_ids) do add_valid_id(id) end
end)

-- 解析调试文件中的建议黑名单条目
local function parse_debug_blacklists()
    local result = {}
    pcall(function()
        if not DEBUG_FILE_ENABLED or not DEBUG_FILE_PATH then return end
        local f = io.open(DEBUG_FILE_PATH, "r")
        if not f then return end
        for line in f:lines() do
            local s = line:match("%[Teleport%]%s*建议黑名单:%s*%{(.-)%}")
            if s then
                for num in s:gmatch("%d+") do
                    local n = tonumber(num)
                    if n then result[n] = true end
                end
            end
        end
        f:close()
    end)
    return result
end

-- 将ID表合并到查找集合中
local function merge_ids_into_set(set, ids)
    if not set or not ids then return end
    for k, _ in pairs(ids) do
        set[tonumber(k)] = true
    end
end

-- 兜底处理传送无效ID记忆与调试文件解析
_G.RunTeleportRandom_impl = function(start_id, end_id, blacklist)
    local DEFAULT_BLACKLIST = {2,5} -- 始终拉黑GM房间ID 5
    local blacklist_lookup = {}

    if type(blacklist) == "table" then
        for _, v in ipairs(blacklist) do
            local n = tonumber(v)
            if n then blacklist_lookup[n] = true end
        end
    end
    for _, v in ipairs(DEFAULT_BLACKLIST) do blacklist_lookup[tonumber(v)] = true end

    merge_ids_into_set(blacklist_lookup, _G._TELEPORT_INVALID_IDS)

    -- 解析调试文件中的建议黑名单并合并
    local parsed = parse_debug_blacklists()
    merge_ids_into_set(blacklist_lookup, parsed)

    -- 随机数种子初始化
    pcall(function()
        math.randomseed(os.time() + (mp and mp.entity_id or 0))
        math.random(); math.random(); math.random()
    end)

    local invalid_ids_set = {} -- 本次运行中标记为无效的ID本地集合
    local max_attempts = 150 -- 点击按钮后的最大尝试次数（设为较小值避免卡顿/卡死）
    local chosen, chosen_pos, valid = nil, nil, false
    local teleported = false

    local function try_skip(candidate)
        local ok_req, skip = pcall(require, "hexm.client.ui.windows.gm.gm_skip_window")
        if ok_req and skip and skip.gm_skip_flow_imp then
            local ok_call, res = pcall(skip.gm_skip_flow_imp, candidate, nil)
            write_debug(string.format("[随机传送] 尝试调用skip.gm_skip_flow_imp(%s) -> 调用成功=%s 返回值=%s", tostring(candidate), tostring(ok_call), tostring(res)))
            return ok_call and (res ~= false)
        end
        return false
    end

    local function try_entity(candidate)
        local ok_ent, ent = pcall(function() return G and G.space and G.space:get_entity(candidate) end)
        if not (ok_ent and ent) then
            write_debug(string.format("[随机传送] ID=%s 未在场景中找到，跳过", tostring(candidate)))
            return nil
        end
        local ok_pos, pos = pcall(function()
            if ent.get_position then return ent:get_position() end
            if ent.getPosition then return ent:getPosition() end
            if ent.position then return ent.position end
            return nil
        end)
        if not (ok_pos and pos and type(pos) == "table" and (pos.x or pos[1])) then
            write_debug(string.format("[随机传送] ID=%s 无可用坐标，跳过", tostring(candidate)))
            return nil
        end
        local px = pos.x or pos[1]; local py = pos.y or pos[2]; local pz = pos.z or pos[3]
        if not (px and py and pz) then
            write_debug(string.format("[随机传送] ID=%s 返回了坐标但缺少具体值，跳过", tostring(candidate)))
            return nil
        end
        return { x = px, y = py, z = pz }
    end

    -- 检查是否所有候选ID都已标记为无效
    local function all_attempted_for_table(tbl, invalid_set)
        if not tbl or #tbl == 0 then return false end
        for _, v in ipairs(tbl) do
            if not invalid_set[tonumber(v)] then
                return false
            end
        end
        return true
    end

    local function all_attempted_for_range(s, e, invalid_set)
        if not s or not e then return false end
        local total = e - s + 1
        if total <= 0 then return false end
        local count = 0
        for id, _ in pairs(invalid_set) do
            if tonumber(id) >= s and tonumber(id) <= e then count = count + 1 end
        end
        return count >= total
    end

    -- 从指定的候选ID列表中选择
    local provided_table = (type(start_id) == "table")
    if provided_table then
        local candidates = start_id
        for attempt = 1, max_attempts do
            local idx = math.random(1, #candidates)
            local candidate = tonumber(candidates[idx])
            write_debug(string.format("[随机传送] 第%d次尝试：选中候选ID=%s", attempt, tostring(candidate)))

            if blacklist_lookup[candidate] then
                write_debug(string.format("[随机传送] 候选ID=%s 在黑名单中，跳过", tostring(candidate)))
                invalid_ids_set[candidate] = true
            else
                local ok_skip = false
                pcall(function() ok_skip = try_skip(candidate) end)
                if ok_skip then
                    -- 记录有效ID并持久化
                    add_valid_id(candidate)
                    write_debug(string.format("[随机传送] 通过skip.gm_skip_flow_imp传送至ID %s 成功", tostring(candidate)))
                    return true
                end

                local pos = nil
                pcall(function() pos = try_entity(candidate) end)
                if pos then
                    chosen = candidate; chosen_pos = pos; valid = true; break
                else
                    invalid_ids_set[candidate] = true
                end
            end
        end
    else
        local s = tonumber(start_id) or 1 -- 起始ID（可自行修改）
        local e = tonumber(end_id) or 999999 -- 结束ID（可自行修改）
        for attempt = 1, max_attempts do
            local candidate = math.random(s, e)
            write_debug(string.format("[随机传送] 第%d次尝试：选中ID=%s", attempt, tostring(candidate)))

            if blacklist_lookup[candidate] then
                write_debug(string.format("[随机传送] ID=%s 在黑名单中，跳过", tostring(candidate)))
                invalid_ids_set[candidate] = true
            else
                local ok_skip = false
                pcall(function() ok_skip = try_skip(candidate) end)
                if ok_skip then
                    -- 记录有效ID并持久化
                    add_valid_id(candidate)
                    write_debug(string.format("[随机传送] 通过skip.gm_skip_flow_imp传送至ID %s 成功", tostring(candidate)))
                    return true
                end

                local pos = nil
                pcall(function() pos = try_entity(candidate) end)
                if pos then
                    chosen = candidate; chosen_pos = pos; valid = true; break
                else
                    invalid_ids_set[candidate] = true
                end
            end
        end
    end

    -- 如果找到有效候选ID，尝试执行传送
    if valid and chosen_pos then
        pcall(function()
            if mp and mp.teleport_to_entity then
                local ok, res = pcall(mp.teleport_to_entity, mp, chosen)
                teleported = ok and (res ~= false)
                write_debug(string.format("[随机传送] 尝试调用mp.teleport_to_entity(%s) -> 传送成功=%s", tostring(chosen), tostring(teleported)))
            end
        end)

        if not teleported then
            pcall(function()
                if mp and mp.teleport_to then
                    local ok, res = pcall(mp.teleport_to, mp, chosen_pos.x, chosen_pos.y, chosen_pos.z)
                    teleported = ok and (res ~= false)
                    write_debug(string.format("[随机传送] 尝试调用mp.teleport_to(x,y,z) -> 传送成功=%s", tostring(teleported)))
                elseif mp and mp.set_position then
                    local ok, res = pcall(mp.set_position, mp, chosen_pos.x, chosen_pos.y, chosen_pos.z)
                    teleported = ok and (res ~= false)
                    write_debug(string.format("[随机传送] 尝试调用mp.set_position(x,y,z) -> 传送成功=%s", tostring(teleported)))
                elseif mp and mp.setPosition then
                    local ok, res = pcall(mp.setPosition, mp, chosen_pos.x, chosen_pos.y, chosen_pos.z)
                    teleported = ok and (res ~= false)
                    write_debug(string.format("[随机传送] 尝试调用mp.setPosition(x,y,z) -> 传送成功=%s", tostring(teleported)))
                end
            end)
        end

        if not teleported then
            pcall(function()
                if mp and mp.entity_id and G and G.space and G.space.move_entity_to_position then
                    local ok, res = pcall(G.space.move_entity_to_position, G.space, mp.entity_id, chosen_pos.x, chosen_pos.y, chosen_pos.z)
                    teleported = ok and (res ~= false)
                    write_debug(string.format("[随机传送] 尝试调用G.space.move_entity_to_position -> 传送成功=%s", tostring(teleported)))
                end
            end)
        end

        if not teleported then
            pcall(function()
                if mp then
                    if mp.position then
                        mp.position.x = chosen_pos.x; mp.position.y = chosen_pos.y; mp.position.z = chosen_pos.z
                        teleported = true
                        write_debug("[随机传送] 直接修改mp.position字段（尽力尝试）")
                    elseif mp.pos then
                        mp.pos = { chosen_pos.x, chosen_pos.y, chosen_pos.z }
                        teleported = true
                        write_debug("[随机传送] 直接修改mp.pos（尽力尝试）")
                    end
                end
            end)
        end

        write_debug(string.format("[随机传送] 最终结果：ID=%s 有效=%s 传送成功=%s", tostring(chosen), tostring(valid), tostring(teleported)))

        -- 如果传送失败，将选中的ID标记为无效（供后续运行使用）
        if not teleported and chosen then
            invalid_ids_set[chosen] = true
        end

        -- 将本次无效ID合并到全局持久化集合中
        for id, _ in pairs(invalid_ids_set) do
            _G._TELEPORT_INVALID_IDS[tonumber(id)] = true
        end

        -- 如果通过非skip方式传送成功，可选添加到有效列表
        if teleported and chosen then
            -- 取消注释下面这行，如果你想将非skip方式的成功传送也持久化到有效列表中
			-- 如果你希望所有成功的传送（包括mp.teleport_to_entity、mp.teleport_to等）都被添加到Teleport_validList.txt
			-- 如果你不清楚用途，保持当前状态即可
            -- add_valid_id(chosen)
        end

        return teleported
    end

    -- 执行到此处说明在最大尝试次数内未找到有效ID
    -- 检查是否已尝试过所有ID
    if provided_table then
        if all_attempted_for_table(start_id, invalid_ids_set) then
            write_debug(string.format("[随机传送] 提供的候选列表中所有ID均已尝试并标记为无效。列表长度=%d", #start_id))
        end
    else
        local s = tonumber(start_id) or 1 -- 起始ID（可自行修改）
        local e = tonumber(end_id) or 999999 -- 结束ID（可自行修改）
        if all_attempted_for_range(s, e, invalid_ids_set) then
            write_debug(string.format("[随机传送] 范围%d-%d内的所有ID均已尝试并标记为无效。", s, e))
        end
    end

    -- 将默认黑名单、传入黑名单、本次无效ID合并为建议黑名单字符串
    local merged_set = {}
    for _, v in ipairs(DEFAULT_BLACKLIST) do merged_set[tonumber(v)] = true end
    if type(blacklist) == "table" then for _, v in ipairs(blacklist) do merged_set[tonumber(v)] = true end end
    for id, _ in pairs(invalid_ids_set) do merged_set[tonumber(id)] = true end
    for id, _ in pairs(_G._TELEPORT_INVALID_IDS) do merged_set[tonumber(id)] = true end

    local merged_list = {}
    for id, _ in pairs(merged_set) do table.insert(merged_list, tonumber(id)) end
    table.sort(merged_list)
    local parts = {}
    for i = 1, #merged_list do parts[#parts+1] = tostring(merged_list[i]) end
    local blacklist_str = "{" .. table.concat(parts, ",") .. "}"

    write_debug(string.format("[随机传送] 在范围%s-%s内尝试%d次后未找到有效ID", tostring(start_id or 1), tostring(end_id or 999999), max_attempts)) -- 仅用于日志记录
    write_debug("[随机传送] 建议黑名单：" .. blacklist_str)

    -- 将合并后的列表持久化到全局无效ID集合（供后续运行使用）
    for _, id in ipairs(merged_list) do _G._TELEPORT_INVALID_IDS[tonumber(id)] = true end

    -- 最终兜底方案：从候选列表中按顺序尝试一次确定性传送（第一个非黑名单候选ID）
    if provided_table and type(start_id) == "table" and #start_id > 0 then
        write_debug("[随机传送] 已达最大尝试次数；尝试从提供的候选列表中执行一次确定性最终传送")
        for _, candidate in ipairs(start_id) do
            candidate = tonumber(candidate)
            if candidate and not blacklist_lookup[candidate] then
                write_debug(string.format("[随机传送] 最终列表尝试：测试ID=%s", tostring(candidate)))
                -- 先尝试skip方式
                local ok_skip = false
                pcall(function() ok_skip = try_skip(candidate) end)
                if ok_skip then
                    -- 记录有效ID并持久化
                    add_valid_id(candidate)
                    write_debug(string.format("[随机传送] 最终列表通过skip.gm_skip_flow_imp传送至ID %s 成功", tostring(candidate)))
                    return true
                end

                local pos = nil
                pcall(function() pos = try_entity(candidate) end)
                if pos then
                    local ok_try = false
                    pcall(function()
                        if mp and mp.teleport_to_entity then
                            local ok, res = pcall(mp.teleport_to_entity, mp, candidate)
                            ok_try = ok and (res ~= false)
                            write_debug(string.format("[随机传送] 最终列表尝试调用mp.teleport_to_entity(%s) -> 成功=%s", tostring(candidate), tostring(ok_try)))
                        end
                    end)
                    if not ok_try then
                        pcall(function()
                            if mp and mp.teleport_to then
                                local ok, res = pcall(mp.teleport_to, mp, pos.x, pos.y, pos.z)
                                ok_try = ok and (res ~= false)
                                write_debug(string.format("[随机传送] 最终列表尝试调用mp.teleport_to(x,y,z) -> 成功=%s", tostring(ok_try)))
                            end
                        end)
                    end
                    if not ok_try then
                        pcall(function()
                            if mp and mp.entity_id and G and G.space and G.space.move_entity_to_position then
                                local ok, res = pcall(G.space.move_entity_to_position, G.space, mp.entity_id, pos.x, pos.y, pos.z)
                                ok_try = ok and (res ~= false)
                                write_debug(string.format("[随机传送] 最终列表尝试调用G.space.move_entity_to_position -> 成功=%s", tostring(ok_try)))
                            end
                        end)
                    end
                    if not ok_try then
                        pcall(function()
                            if mp then
                                if mp.position then
                                    mp.position.x = pos.x; mp.position.y = pos.y; mp.position.z = pos.z
                                    ok_try = true
                                    write_debug("[随机传送] 最终列表直接修改mp.position字段（尽力尝试）")
                                elseif mp.pos then
                                    mp.pos = { pos.x, pos.y, pos.z }
                                    ok_try = true
                                    write_debug("[随机传送] 最终列表直接修改mp.pos（尽力尝试）")
                                end
                            end
                        end)
                    end

                    if ok_try then
                        write_debug(string.format("[随机传送] 最终列表传送至ID=%s 成功", tostring(candidate)))
                        return true
                    else
                        write_debug(string.format("[随机传送] 最终列表传送至ID=%s 失败；标记为无效", tostring(candidate)))
                        _G._TELEPORT_INVALID_IDS[candidate] = true
                    end
                else
                    write_debug(string.format("[随机传送] 最终列表候选ID=%s 无可用坐标；标记为无效", tostring(candidate)))
                    _G._TELEPORT_INVALID_IDS[candidate] = true
                end
            else
                write_debug(string.format("[随机传送] 最终列表候选ID=%s 跳过（在黑名单中或之前已标记为无效）", tostring(candidate)))
            end
        end

        write_debug("[随机传送] 最终列表确定性尝试已耗尽；无传送操作成功")
    end

    return false
end

-- 00. 禁用日志输出 ??
_G.DisableLogsAndChecks = function()
    local ok_combat, gm_combat = pcall(require, "hexm.client.debug.gm.gm_commands.gm_combat")
    if ok_combat and gm_combat then
        if gm_combat.gm_forbid_behit_highlight then pcall(gm_combat.gm_forbid_behit_highlight, 1) end
        if gm_combat.gm_enable_stopframe_debug then pcall(gm_combat.gm_enable_stopframe_debug, 0) end
    end

    local ok_cutscene, gm_cutscene = pcall(require, "hexm.client.debug.gm.gm_commands.gm_cutscene")
    if ok_cutscene and gm_cutscene then
        if gm_cutscene.gm_cutscene_clear_log then pcall(gm_cutscene.gm_cutscene_clear_log) end
        if gm_cutscene.gm_cutscene_debug_terminate then pcall(gm_cutscene.gm_cutscene_debug_terminate) end
    end

    local ok_activity, gm_activity = pcall(require, "hexm.client.debug.gm.gm_commands.gm_activity_center")
    if ok_activity and gm_activity and gm_activity.gm_activity_center_clear then
        pcall(gm_activity.gm_activity_center_clear)
    end

    local ok_hotfix, gm_hotfix = pcall(require, "hexm.client.debug.gm.gm_commands.gm_hotfix")
    if ok_hotfix and gm_hotfix then
        if gm_hotfix.gm_hotfix_del_local_cache then pcall(gm_hotfix.gm_hotfix_del_local_cache) end
    end

    local ok_story, gm_story = pcall(require, "hexm.client.debug.gm.gm_commands.gm_storyline")
    if ok_story and gm_story and gm_story.gm_server_not_run_lua_script then
        pcall(gm_story.gm_server_not_run_lua_script)
    end

    if type(write_debug) == "function" then
        write_debug("[✔] 日志输出已禁用")
    end
end

-- 000. 隐藏工具函数/通用功能
local function refresh_combat_menu()
    local gm_combat = package.loaded["hexm.client.debug.gm.gm_commands.gm_combat"]
    if gm_combat and gm_combat.gm_open_combat_train then gm_combat.gm_open_combat_train() end
end

local function weapon_guise()
    local gm_decorator = package.loaded["hexm.client.debug.gm.gm_decorator"] or require("hexm.client.debug.gm.gm_decorator")
    if gm_decorator.gm_command_short_cuts.game then
        local cmds = gm_decorator.gm_command_short_cuts.game
        if cmds["$weapon_guise"] then pcall(cmds["$weapon_guise"], 1) end
    end
end

-- =================================================================================
-- 第三部分：状态管理与核心逻辑
-- =================================================================================

-- 状态变量
_G.SPEED_INDEX   = 1 -- 1=正常速度
_G.LOOT_STATE    = 0 -- 0=关闭, 1=执行一次, 2=循环执行
_G.BUFF_STATE    = 0
_G.RECOVER_STATE = 0
-- 循环间隔（秒）- 可修改以下参数
_G.LOOT_LOOP_SEC    = _G.LOOT_LOOP_SEC or 1.0 -- 如需修改自动拾取的循环间隔，调整此值
_G.BUFF_LOOP_SEC    = _G.BUFF_LOOP_SEC or 3.0 -- 如需修改自动施加Buff的循环间隔，调整此值
_G.RECOVER_LOOP_SEC = _G.RECOVER_LOOP_SEC or 5.0 -- 如需修改自动恢复的循环间隔，调整此值

-- 定义常量
local SPEED_LABELS = {"x1.0", "x1.5", "x3.0", "x5.0", "x7.5", "10.0", "30.0"}
local STATE_LABELS = {[0]="关闭", [1]="执行一次", [2]="循环执行"}
--local FOV_VALUES   = {10,20,30,40,50,60,70,80,90,100}
--_G.FOV_INDEX       = 6

-- 速度选择弹窗（可在queueButton调用前定义）
local speedPickerPanel = nil

local SPEED_MENU_LABELS = {"x1.0", "x1.5", "x3.0", "x5.0", "x7.5", "10.0", "30.0"}
local SPEED_MENU_VALUES = {1.0, 1.5, 3.0, 5.0, 7.5, 10.0, 30.0}
-- 注意：我特意加入了一个明确的"正常"值（0.0），使其与"正常"标签完全对应。
-- 如果你的RunSetSpeed函数对"正常"速度需要不同的标记值，请相应修改SPEED_MENU_VALUES[1]。

local function CloseSpeedPicker()
    if speedPickerPanel and type(speedPickerPanel.removeFromParent) == "function" then
        pcall(function() speedPickerPanel:removeFromParent() end)
    end
    speedPickerPanel = nil
end

local function ShowSpeedPicker()
    if speedPickerPanel then
        CloseSpeedPicker()
        return
    end

    local w = 420
    local h = 60 + (#SPEED_MENU_LABELS * 64)
    speedPickerPanel = ccui.Layout:create()
    speedPickerPanel:setContentSize(cc.size(w, h))
    speedPickerPanel:setBackGroundColorType(1)
    speedPickerPanel:setBackGroundColor(cc.c3b(30, 30, 35))
    speedPickerPanel:setBackGroundColorOpacity(230)

    -- 刷新速度选择项视觉状态的辅助函数
    local function RefreshSpeedPickerVisuals()
        if not speedPickerPanel then return end
        for _, child in ipairs(speedPickerPanel:getChildren()) do
            local t = child._speedTxt
            local idx = child._index
            if t and idx then
                if _G.SPEED_INDEX == idx then
                    t:setColor(cc.c3b(0,255,0))
                else
                    t:setColor(cc.c3b(200,200,200))
                end
            end
        end
    end

    local centerX, centerY
    if panel and type(panel.getPositionX) == "function" then
        local px, py = panel:getPositionX(), panel:getPositionY()
        local pw, ph = panel:getContentSize().width, panel:getContentSize().height
        centerX = px + pw * 0.5
        centerY = py + ph * 0.5
    else
        centerX = size.width * 0.5
        centerY = size.height * 0.5
    end
    speedPickerPanel:setPosition(cc.p(centerX - w * 0.5, centerY - h * 0.5))

    local title = ccui.Text:create("选择速度倍率", "Arial", 28)
    title:setColor(cc.c3b(220,220,220))
    title:setAnchorPoint(cc.p(0.5, 1))
    title:setPosition(cc.p(w * 0.5, h - 12))
    speedPickerPanel:addChild(title)

    for i, label in ipairs(SPEED_MENU_LABELS) do
        local btn = ccui.Button:create()
        btn:setTitleText("")
        btn:setScale9Enabled(true)
        btn:setContentSize(cc.size(w - 40, 56))
        btn:setAnchorPoint(cc.p(0.5, 1))
        local y = h - 40 - ((i-1) * 64)
        btn:setPosition(cc.p(w * 0.5, y))
        btn:setColor(cc.c3b(30,30,35))

        local txt = ccui.Text:create(label, "Arial", 28)
        txt:setColor(cc.c3b(200,200,200))
        txt:setAnchorPoint(cc.p(0.5, 0.5))
        txt:setPosition(cc.p((w - 40) * 0.5, 28))
        btn:addChild(txt)

        -- 存储引用，以便RefreshSpeedPickerVisuals函数更新状态
        btn._index = i
        btn._speedTxt = txt

        btn:addTouchEventListener(function(s, e)
            if e == 2 then
                pcall(function()
                    -- 设置索引（用于视觉显示）并设置实际速度值
                    _G.SPEED_INDEX = i
                    -- 将选中的数值存储到全局变量，供其他代码读取
                    _G.SPEED_VALUE = SPEED_MENU_VALUES[i]
                    -- 如果RunSetSpeed函数需要数值型速度参数，则传入该值
                    if type(_G.RunSetSpeed) == "function" then
                        -- 优先传入数值；如果你的RunSetSpeed需要索引作为参数，
                        -- 将此行改为：pcall(_G.RunSetSpeed, _G.SPEED_INDEX)
                        pcall(_G.RunSetSpeed, _G.SPEED_INDEX)
                    end
                end)
                pcall(UpdateButtonVisuals)
                pcall(RefreshSpeedPickerVisuals) -- 确保弹窗视觉状态立即更新
                CloseSpeedPicker()
            end
        end)
        speedPickerPanel:addChild(btn)
    end

    -- 确保弹窗打开时初始视觉状态正确
    pcall(RefreshSpeedPickerVisuals)

    local overlay = ccui.Layout:create()
    overlay:setContentSize(cc.size(size.width, size.height))
    overlay:setBackGroundColorType(1)
    overlay:setBackGroundColor(cc.c3b(0,0,0))
    overlay:setBackGroundColorOpacity(120)
    overlay:setPosition(cc.p(0,0))
    overlay:addTouchEventListener(function(s,e)
        if e == 2 then
            CloseSpeedPicker()
        end
    end)

    if scene then
        scene:addChild(overlay, 10000)
        scene:addChild(speedPickerPanel, 10001)
    end

    local oldClose = CloseSpeedPicker
    CloseSpeedPicker = function()
        if overlay and type(overlay.removeFromParent) == "function" then
            pcall(function() overlay:removeFromParent() end)
        end
        if speedPickerPanel and type(speedPickerPanel.removeFromParent) == "function" then
            pcall(function() speedPickerPanel:removeFromParent() end)
        end
        speedPickerPanel = nil
        overlay = nil
        CloseSpeedPicker = oldClose
    end
end


-- 开关状态
_G.GM_ONEHITEXHAUST = _G.GM_ONEHITEXHAUST or false
_G.GM_DIVEAIR       = _G.GM_DIVEAIR       or false

-- 动作计时器
local loot_action, buff_action, recover_action = nil, nil, nil
local buttonList = {} 
local selectedIndex = 1

-- 前置声明
local function UpdateButtonVisuals() end 
_G.ForceUpdateVisuals = function() UpdateButtonVisuals() end
_G.ForceUpdateVisuals = _G.ForceUpdateVisuals or function() end

-- --- 逻辑包装函数 ---

_G.ToggleSpeed = function()
    _G.SPEED_INDEX = (_G.SPEED_INDEX % #SPEED_LABELS) + 1
    _G.RunSetSpeed(_G.SPEED_INDEX)
    UpdateButtonVisuals()
end

local function HandleLoopAction(state, actionRef, func, delaySec)
    -- 若延迟参数无效，使用默认值
    delaySec = tonumber(delaySec) or 5.0
    if delaySec < 0.05 then delaySec = 0.05 end

    -- 停止旧的循环动作
    if actionRef then
        local scene = cc.Director:getInstance():getRunningScene()
        if scene then scene:stopAction(actionRef) end
        actionRef = nil
    end

    -- 无论"执行一次"还是"循环执行"，都立即执行一次
    if state == 1 or state == 2 then
        func()
    end

    -- 如果是"循环执行"：为下一次执行启动计时器
    if state == 2 then
        local scene = cc.Director:getInstance():getRunningScene()
        if scene then
            actionRef = cc.RepeatForever:create(cc.Sequence:create({
                cc.DelayTime:create(delaySec),
                cc.CallFunc:create(function()
                    func()
                end)
            }))
            scene:runAction(actionRef)
        end
    end

    return actionRef
end

_G.ToggleAutoLootLoop = function()
    _G.LOOT_STATE = (_G.LOOT_STATE + 1) % 3
    loot_action = HandleLoopAction(_G.LOOT_STATE, loot_action, _G.RunAutoLoot, _G.LOOT_LOOP_SEC)
    UpdateButtonVisuals()
end

_G.ToggleAutoBuffLoop = function()
    _G.BUFF_STATE = (_G.BUFF_STATE + 1) % 3
    buff_action = HandleLoopAction(_G.BUFF_STATE, buff_action, _G.RunAutoBuff, _G.BUFF_LOOP_SEC)
    UpdateButtonVisuals()
end

_G.ToggleRecover = function()
    _G.RECOVER_STATE = (_G.RECOVER_STATE + 1) % 3
    recover_action = HandleLoopAction(_G.RECOVER_STATE, recover_action, _G.RunRecover, _G.RECOVER_LOOP_SEC)
    UpdateButtonVisuals()
end

-- --- 循环切换函数 ---

_G.CycleSpeed = function(dir)
    _G.SPEED_INDEX = _G.SPEED_INDEX + dir
    if _G.SPEED_INDEX < 1 then _G.SPEED_INDEX = #SPEED_LABELS end
    if _G.SPEED_INDEX > #SPEED_LABELS then _G.SPEED_INDEX = 1 end
    _G.RunSetSpeed(_G.SPEED_INDEX) 
    UpdateButtonVisuals()
end

_G.CycleState = function(globalVarName, dir, maxState) 
    local val = _G[globalVarName] + dir
    if val < 0 then val = maxState end
    if val > maxState then val = 0 end
    _G[globalVarName] = val
    UpdateButtonVisuals()
end

--_G.CycleFOV = function(dir)
--    _G.FOV_INDEX = _G.FOV_INDEX + dir
--    if _G.FOV_INDEX < 1 then _G.FOV_INDEX = #FOV_VALUES end
--    if _G.FOV_INDEX > #FOV_VALUES then _G.FOV_INDEX = 1 end
--    local cam = package.loaded["hexm.client.debug.gm.gm_commands.gm_camera"] or require("hexm.client.debug.gm.gm_commands.gm_camera")
--    if cam and cam.test_camera_fov then pcall(cam.test_camera_fov, FOV_VALUES[_G.FOV_INDEX]) end
--    UpdateButtonVisuals()
--end

_G.ExecuteLoot = function()
    loot_action = HandleLoopAction(_G.LOOT_STATE, loot_action, _G.RunAutoLoot, _G.LOOT_LOOP_SEC)
end

_G.ExecuteBuff = function()
    buff_action = HandleLoopAction(_G.BUFF_STATE, buff_action, _G.RunAutoBuff, _G.BUFF_LOOP_SEC)
end

_G.ExecuteRecover = function()
    recover_action = HandleLoopAction(_G.RECOVER_STATE, recover_action, _G.RunRecover, _G.RECOVER_LOOP_SEC)
end

-- =================================================================================
-- 第四部分：导航与视觉样式（所有按钮统一风格）
-- =================================================================================

-- 1. 精确RGB颜色值
local C_BG          = cc.c3b(20, 20, 25)      -- 深色背景
local C_TEXT_OFF    = cc.c3b(192, 192, 192)   -- 未激活状态（银色）
local C_TEXT_ON     = cc.c3b(0, 255, 0)       -- 激活状态（绿色）
local C_SELECTED    = cc.c3b(255, 0, 255)     -- 悬停/选中状态（品红色）
local C_READONLY    = cc.c3b(153, 255, 255)   -- 只读/数值部分（青色）

-- 2. 选中状态逻辑
local function UpdateSelection()
    for i, item in ipairs(buttonList) do
        local isSelected = (i == selectedIndex)

        -- 默认使用未激活颜色
        local targetMainColor = C_TEXT_OFF

        -- 如果选项处于激活状态（开启/循环等），显示绿色
        if item.isActive and item.isActive() then
            targetMainColor = C_TEXT_ON
        end

        -- 选中状态会覆盖主颜色
        if isSelected then
            targetMainColor = C_SELECTED
            item.btn:setScale(1.05)
        else
            item.btn:setScale(1.0)
        end

        -- 所有按钮文本现在通过ccui.Text节点渲染
        if item.mainText then
            item.mainText:setColor(targetMainColor)
        end

        -- 数值部分始终显示青色（仅数值按钮有此部分）
        if item.valueText then
            item.valueText:setColor(C_READONLY)
        end
    end
end

UpdateButtonVisuals = function()
    for _, item in ipairs(buttonList) do
        if item.updateText then item.updateText() end
    end
    UpdateSelection()
end

-- 导航逻辑
_G.MenuUp = function()
    selectedIndex = selectedIndex - 1
    if selectedIndex < 1 then selectedIndex = #buttonList end
    UpdateSelection()
end

_G.MenuDown = function()
    selectedIndex = selectedIndex + 1
    if selectedIndex > #buttonList then selectedIndex = 1 end
    UpdateSelection()
end

-- 左/右键现在用于修改数值（小键盘4/6）
_G.MenuLeft = function()
    local item = buttonList[selectedIndex]
    if item and item.cycleFunc then
        item.cycleFunc(-1)
        UpdateButtonVisuals()
    end
end

_G.MenuRight = function()
    local item = buttonList[selectedIndex]
    if item and item.cycleFunc then
        item.cycleFunc(1)
        UpdateButtonVisuals()
    end
end

-- 确认键用于执行操作（小键盘5）
_G.MenuConfirm = function()
    local item = buttonList[selectedIndex]
    if item and item.execFunc then
        pcall(item.execFunc)
    end
    UpdateButtonVisuals()
end

-- =================================================================================
-- 第五部分：UI界面构建（修复版）
-- =================================================================================

-- 尺寸参数（双倍大小）
local btnHeight   = 90
local spacing     = 16
local headerSpace = 160
local footerSpace = 400
local panelWidth  = 940

-- 按钮宽度与面板宽度匹配（保留小边距）
local buttonWidth = panelWidth - 60  -- 两侧各留30像素内边距

local MENU_VERSION = "v1.4.2"

-- 确保按钮列表和选中状态已初始化
buttonList = buttonList or {}
selectedIndex = selectedIndex or 1

-- 添加调试信息
write_debug("开始创建UI界面...")
write_debug("屏幕尺寸: " .. size.width .. "x" .. size.height)
write_debug("面板尺寸: " .. panelWidth .. "x" .. (headerSpace + footerSpace))

-- 修复字体设置函数
local function createText(content, fontSize)
    local text = ccui.Text:create(content, "", fontSize)  -- 使用空字体名，使用系统默认字体
    text:setColor(cc.c3b(255, 255, 255))  -- 确保白色
    text:setOpacity(255)  -- 确保不透明
    text:setCascadeColorEnabled(true)
    text:setCascadeOpacityEnabled(true)
    return text
end

-- 添加按钮的辅助函数
local tempButtons = {}

local function queueButton(label, execFunc, cycleFunc, isActiveFunc, dynamicLabelFunc, isValueBtn)
    local item = {
        label = label,
        execFunc = execFunc,
        cycleFunc = cycleFunc,
        isActive = isActiveFunc,
        isValueBtn = isValueBtn,
        dynamicLabelFunc = dynamicLabelFunc
    }
    table.insert(tempButtons, item)
end

-- --- 1. 定义按钮 ---

-- 第一组：单次执行按钮（顶部）
queueButton("清除所有NPC (Alt+5)", _G.RunKillNPC, nil, nil, nil, false)
queueButton("施加永久Buff", _G.RunPermanentBuffs, nil, nil, nil, false)
queueButton("移除所有Buff", _G.RunRemoveBuffs, nil, nil, nil, false)
queueButton("重置犯罪值", _G.ResetCrime, nil, nil, nil, false)
queueButton("禁用日志输出", _G.DisableLogsAndChecks, nil, nil, nil, false)

-- 第二组：开关按钮（开启/关闭）
local function makeToggle(name, varName, onFunc, offFunc)
    local toggle = function()
        _G[varName] = not _G[varName]
        if _G[varName] then
            pcall(onFunc)
        else
            pcall(offFunc)
        end
        if UpdateButtonVisuals then pcall(UpdateButtonVisuals) end
    end

    queueButton(
        name,
        nil,                                  -- execFunc设为nil，点击仅触发cycleFunc
        function(d) toggle() end,             -- cycleFunc执行切换逻辑
        function() return _G[varName] end,
        function() return name .. " < " .. (_G[varName] and "开启" or "关闭") .. " >" end,
        true
    )
end

-- 无敌模式
makeToggle("无敌模式", "GM_GODMODE",
    function() -- 开启
        pcall(_G.GM_EnableGodmode)
        pcall(_G.ForceUpdateVisuals)
    end,
    function() -- 关闭
        pcall(_G.GM_DisableGodmode)
        pcall(_G.ForceUpdateVisuals)
    end)

-- 一击必杀
makeToggle("一击必杀", "GM_ONEHITKILL",
    function() -- 开启
        pcall(_G.GM_EnableOneHit)
        pcall(_G.ForceUpdateVisuals)
    end,
    function() -- 关闭
        pcall(_G.GM_DisableOneHit)
        pcall(_G.ForceUpdateVisuals)
    end)

-- 无限体力
makeToggle("无限体力", "GM_STAMINA",
    function() -- 开启
        pcall(_G.GM_EnableStamina)
        pcall(_G.ForceUpdateVisuals)
    end,
    function() -- 关闭
        pcall(_G.GM_DisableStamina)
        pcall(_G.ForceUpdateVisuals)
    end)

-- NPC智障模式
makeToggle("NPC智障模式", "GM_NPCDUMB",
    function() -- 开启NPC智障模式（施加Buff）
        pcall(_G.GM_EnableNPCDUMB)
        pcall(_G.ForceUpdateVisuals)
    end,
    function() -- 关闭NPC智障模式（移除Buff）
        pcall(_G.GM_DisableNPCDUMB)
        pcall(_G.ForceUpdateVisuals)
    end)

-- 第三组：可修改值按钮（底部）
queueButton(
    "隐身模式",
    nil, -- execFunc设为nil：鼠标点击触发cycleFunc（同时添加/移除Buff）
    function(d)
        d = tonumber(d) or 1
        local maxState = 2
        if d > 1 then d = 1 elseif d < -1 then d = -1 end

        local cur = tonumber(_G.INVIS_STATE) or 0
        -- 切换状态并在[0..maxState]范围内循环
        cur = (cur + d) % (maxState + 1)
        _G.INVIS_STATE = cur

        -- 直接添加/移除Buff，避免RunInvisibility的一次性清除行为
        if cur == 0 then
            pcall(function()
                if type(_G.GM_DisableInvisibility) == "function" then
                    _G.GM_DisableInvisibility()
                end
            end)
        else
            pcall(function()
                if type(_G.GM_EnableInvisibility) == "function" then
                    _G.GM_EnableInvisibility()
                end
            end)
        end

        -- 确保视觉样式和标签立即更新
        if type(_G.ForceUpdateVisuals) == "function" then pcall(_G.ForceUpdateVisuals) end
        if UpdateButtonVisuals then pcall(UpdateButtonVisuals) end
    end,
    -- isActive：数值状态>0 或 Buff标记已设置时返回true
    function()
        local s = tonumber(_G.INVIS_STATE) or 0
        if s > 0 then return true end
        return _G.GM_INVISIBLE == true
    end,
    -- 动态标签：优先显示数值状态标签；否则显示实际Buff状态
    function()
        local s = tonumber(_G.INVIS_STATE) or 0
        if s > 0 then
            return "隐身模式 < " .. (STATE_LABELS[s] or ("状态 " .. tostring(s))) .. " >"
        end
        if _G.GM_INVISIBLE then
            return "隐身模式 < 开启 >"
        end
        return "隐身模式 < 关闭 >"
    end,
    true
)

-- 速度：点击时打开选择器；用户在选择器中确认后才实际应用
-- 先定义速度标签（如果未定义）
local SPEED_LABELS = SPEED_LABELS or {"1.0x", "1.5x", "3.0x", "5.0x", "7.5x", "10.0x", "30.0x"}

-- 新增：定义攻击倍率标签（与配置表对应）
local ATTACK_MULTIPLE_LABELS = ATTACK_MULTIPLE_LABELS or {"x1.0", "x2.0", "x4.0", "x8.0"}

-- ==================== 原有全局索引（保留并新增攻击倍率索引） ====================
-- 剧情速度全局索引
_G.SPEED_INDEX = _G.SPEED_INDEX or 1
-- 攻击速度全局索引（新增，独立存储）
_G.ATTACK_SPEED_INDEX = _G.ATTACK_SPEED_INDEX or 1
-- 攻击倍率全局索引（新增，独立存储）
_G.ATTACK_MULTIPLE_INDEX = _G.ATTACK_MULTIPLE_INDEX or 1
-- 新增：记录当前生效的攻击倍率Buff ID（关键：用于精准移除上一个Buff）
_G.CURRENT_ATTACK_BUFF_ID = _G.CURRENT_ATTACK_BUFF_ID or 0

-- ==================== 攻击倍率配置表（新增） ====================
local attack_buff_config = {
    [1] = {buff_id = 0,       multiple = 1.0, desc = "默认倍率(无BUFF)"},  -- 基准值
    [2] = {buff_id = 1053017, multiple = 2.0, desc = "2倍攻击倍率"},
    [3] = {buff_id = 1053018, multiple = 4.0, desc = "4倍攻击倍率"},
    [4] = {buff_id = 1053019, multiple = 8.0, desc = "8倍攻击倍率"},
}

-- 定义需要移除的攻击倍率Buff列表（核心）
local ATTACK_MULTIPLE_BUFFS = {
    {id = 1053017, note = "2倍攻击倍率"},
    {id = 1053018, note = "4倍攻击倍率"},
    {id = 1053019, note = "8倍攻击倍率"}
}

-- 强制更新视觉效果的通用函数（如果未定义）
if not _G.ForceUpdateVisuals then
    _G.ForceUpdateVisuals = function()
        -- 可在此添加通用的视觉更新逻辑
    end
end

-- 更新按钮视觉效果的通用函数（如果未定义）
function UpdateButtonVisuals()
    -- 可在此添加按钮样式、状态等视觉更新逻辑
end

-- 速度选择器展示函数（扩展支持攻击倍率）
function ShowSpeedPicker(callback, pickerType)
    -- pickerType: "dialog" | "attack_speed" | "attack_multiple"
    local pickerType = pickerType or "dialog"
    local selectedIndex = 1
    
    -- 根据类型获取对应索引
    if pickerType == "dialog" then
        selectedIndex = _G.SPEED_INDEX or 1
    elseif pickerType == "attack_speed" then
        selectedIndex = _G.ATTACK_SPEED_INDEX or 1
    elseif pickerType == "attack_multiple" then
        selectedIndex = _G.ATTACK_MULTIPLE_INDEX or 1
    end
    
    -- 模拟速度选择器逻辑：弹出选择框，用户选择后调用回调
    -- 实际需根据游戏UI框架实现，这里仅做示例
    callback(selectedIndex)
end

-- 循环切换速度/倍率的通用函数（扩展支持攻击倍率）
function CycleSpeedBase(delta, speedType)
    local maxIndex = 0
    
    -- 剧情速度
    if speedType == "dialog" then
        maxIndex = #SPEED_LABELS
        _G.SPEED_INDEX = _G.SPEED_INDEX + delta
        if _G.SPEED_INDEX > maxIndex then _G.SPEED_INDEX = 1 end
        if _G.SPEED_INDEX < 1 then _G.SPEED_INDEX = maxIndex end
        -- 应用剧情速度
        if type(_G.RunSetDialogSpeed) == "function" then
            _G.RunSetDialogSpeed(_G.SPEED_INDEX)
        end
    -- 攻击速度
    elseif speedType == "attack_speed" then
        maxIndex = #SPEED_LABELS
        _G.ATTACK_SPEED_INDEX = _G.ATTACK_SPEED_INDEX + delta
        if _G.ATTACK_SPEED_INDEX > maxIndex then _G.ATTACK_SPEED_INDEX = 1 end
        if _G.ATTACK_SPEED_INDEX < 1 then _G.ATTACK_SPEED_INDEX = maxIndex end
        -- 应用攻击速度
        if type(_G.RunSetGMSpeed) == "function" then
            _G.RunSetGMSpeed(_G.ATTACK_SPEED_INDEX)
        end
    -- 攻击倍率（新增分支）
    elseif speedType == "attack_multiple" then
        maxIndex = #ATTACK_MULTIPLE_LABELS
        _G.ATTACK_MULTIPLE_INDEX = _G.ATTACK_MULTIPLE_INDEX + delta
        if _G.ATTACK_MULTIPLE_INDEX > maxIndex then _G.ATTACK_MULTIPLE_INDEX = 1 end
        if _G.ATTACK_MULTIPLE_INDEX < 1 then _G.ATTACK_MULTIPLE_INDEX = maxIndex end
        -- 应用攻击倍率
        if type(_G.RunSetAttackMultiple) == "function" then
            _G.RunSetAttackMultiple(_G.ATTACK_MULTIPLE_INDEX)
        end
    end
    
    -- 更新视觉
    if type(_G.ForceUpdateVisuals) == "function" then pcall(_G.ForceUpdateVisuals) end
    UpdateButtonVisuals()
end

-- 新增：攻击倍率循环切换函数
_G.CycleAttackMultiple = function(d)
    CycleSpeedBase(d, "attack_multiple")
end

-- ==================== 核心复用：参照RunRemoveBuffs逻辑实现攻击倍率Buff移除 ====================
local function safe_call(desc, fn)
    local ok, err = pcall(fn)
    if ok then
        write_debug("[攻击倍率Buff移除] ✔ " .. desc)
        return true
    else
        write_debug("[攻击倍率Buff移除] ✘ " .. desc .. " -> " .. tostring(err))
        return false
    end
end

-- 检测玩家是否拥有指定Buff
local function has_attack_buff(mp_local, buff_id)
    if not mp_local then return false end
    
    local ok, res

    -- 方式1：has_buff方法（带self/不带self）
    ok, res = pcall(function()
        if mp_local.has_buff then return mp_local.has_buff(mp_local, buff_id) end
        if mp_local["has_buff"] and type(mp_local["has_buff"]) == "function" then return mp_local:has_buff(buff_id) end
        return nil
    end)
    if ok and res ~= nil then return res end

    -- 方式2：get_buff方法检测
    ok, res = pcall(function()
        if mp_local.get_buff then return mp_local.get_buff(mp_local, buff_id) ~= nil end
        if mp_local["get_buff"] and type(mp_local["get_buff"]) == "function" then return mp_local:get_buff(buff_id) ~= nil end
        return nil
    end)
    if ok and res ~= nil then return res end

    -- 方式3：遍历buffs表检测
    ok, res = pcall(function()
        if mp_local.buffs and type(mp_local.buffs) == "table" then
            if mp_local.buffs[buff_id] then return true end
            for _, v in pairs(mp_local.buffs) do
                if type(v) == "table" and (v.id == buff_id or v.buff_id == buff_id) then
                    return true
                end
            end
        end
        return nil
    end)
    if ok and res ~= nil then return res end

    return false
end

-- 移除单个攻击倍率Buff（复用RunRemoveBuffs的移除逻辑）
local function remove_single_attack_buff(mp_local, buff_id, eid)
    if not mp_local then return false end
    
    local removed = false
    local action = nil
    pcall(function() action = portable.import('hexm.client.ui.windows.gm.gm_combat.combat_train_action') end)

    -- 1. 通过combat_train_action模块移除（优先级最高）
    if action then
        if action.rm_buff then
            removed = safe_call(string.format("action.rm_buff(%d)", buff_id), function() action.rm_buff(buff_id) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:rm_buff(%d)", buff_id), function() action:rm_buff(buff_id) end) or removed
            if removed then return true end
        end

        if action.remove_buff then
            removed = safe_call(string.format("action.remove_buff(%d, %s)", buff_id, tostring(eid)), function() action.remove_buff(buff_id, eid) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:remove_buff(%d, %s)", buff_id, tostring(eid)), function() action:remove_buff(buff_id, eid) end) or removed
            if removed then return true end
        end

        if action.del_buff then
            removed = safe_call(string.format("action.del_buff(%d, %s)", buff_id, tostring(eid)), function() action.del_buff(buff_id, eid) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:del_buff(%d, %s)", buff_id, tostring(eid)), function() action:del_buff(buff_id, eid) end) or removed
            if removed then return true end
        end

        if action.clear_buff then
            removed = safe_call(string.format("action.clear_buff(%d, %s)", buff_id, tostring(eid)), function() action.clear_buff(buff_id, eid) end) or removed
            if removed then return true end
            removed = safe_call(string.format("action:clear_buff(%d, %s)", buff_id, tostring(eid)), function() action:clear_buff(buff_id, eid) end) or removed
            if removed then return true end
        end
    end

    -- 2. 通过玩家对象自身方法移除
    if mp_local.remove_buff then
        removed = safe_call(string.format("mp.remove_buff(mp, %d)", buff_id), function() mp_local.remove_buff(mp_local, buff_id) end) or removed
        if removed then return true end
        removed = safe_call(string.format("mp:remove_buff(%d)", buff_id), function() mp_local:remove_buff(buff_id) end) or removed
        if removed then return true end
    end

    if mp_local.del_buff then
        removed = safe_call(string.format("mp.del_buff(mp, %d)", buff_id), function() mp_local.del_buff(mp_local, buff_id) end) or removed
        if removed then return true end
        removed = safe_call(string.format("mp:del_buff(%d)", buff_id), function() mp_local:del_buff(buff_id) end) or removed
        if removed then return true end
    end

    if not removed then
        write_debug(string.format("[攻击倍率Buff移除] 无法通过已知方法移除Buff ID=%d", buff_id))
    end

    return removed
end

-- 批量移除所有攻击倍率Buff（核心函数）
local function remove_all_attack_multiple_buffs()
    write_debug("[攻击倍率Buff移除] 开始移除1053017/1053018/1053019 Buff")

    local mp_local = G and G.main_player
    if not mp_local then
        write_debug("[攻击倍率Buff移除] 错误：未找到主玩家对象")
        return false
    end

    local eid = mp_local.entity_id
    local removed_count = 0

    -- 遍历攻击倍率Buff列表，逐个移除
    for _, entry in ipairs(ATTACK_MULTIPLE_BUFFS) do
        local buff_id = entry.id
        local note = entry.note or ""
        
        -- 先检测是否存在该Buff
        local present = false
        local ok_has, pres = pcall(function() return has_attack_buff(mp_local, buff_id) end)
        if ok_has and pres then present = true end

        if not present then
            write_debug(string.format("[攻击倍率Buff移除] 跳过ID=%d (%s) - 未检测到该Buff", buff_id, note))
        else
            -- 执行移除
            if remove_single_attack_buff(mp_local, buff_id, eid) then
                write_debug(string.format("[攻击倍率Buff移除] 成功移除Buff ID=%d (%s)", buff_id, note))
                removed_count = removed_count + 1
            else
                write_debug(string.format("[攻击倍率Buff移除] 移除Buff失败 ID=%d (%s)", buff_id, note))
            end
        end
    end

    -- 重置全局状态
    _G.CURRENT_ATTACK_BUFF_ID = 0
    write_debug(string.format("[攻击倍率Buff移除] 移除流程完成。累计移除成功：%d", removed_count))
    
    return removed_count > 0
end

-- ==================== 优化应用攻击倍率函数 ====================
local function apply_attack_multiple_buff(valIndex)
    -- 校验参数有效性
    if not valIndex or type(valIndex) ~= "number" then
        write_debug("攻击倍率设置失败：无效的索引值 " .. tostring(valIndex))
        return false
    end
    
    -- 获取目标倍率配置
    local target_config = attack_buff_config[valIndex] or attack_buff_config[1]
    local target_buff_id = target_config.buff_id
    local target_multiple = target_config.multiple
    
    -- 检查G全局对象和主玩家是否存在
    if not G or not G.main_player then
        write_debug("攻击倍率设置失败：主玩家对象不存在")
        return false
    end
    
    local main_player = G.main_player
    local eid = main_player.entity_id

    -- 核心逻辑：切换到1.0倍时，调用复用的移除函数
    if valIndex == 1 then  -- 1.0倍倍率的索引
        write_debug("[攻击倍率切换] 切换到1.0倍，执行Buff移除逻辑")
        -- 强制移除所有攻击倍率Buff
        remove_all_attack_multiple_buffs()
        return true
    end

    -- 非1.0倍逻辑：先移除旧Buff，再添加新Buff
    remove_all_attack_multiple_buffs()
    
    -- 添加新Buff（复用RunPermanentBuffs的添加逻辑）
    local apply_ok = true
    if target_buff_id ~= 0 then
        local action = nil
        pcall(function()
            action = portable.import('hexm.client.ui.windows.gm.gm_combat.combat_train_action')
        end)

        if action then
            local reason = "gm"
            local level = 5
            local duration = 10675199116730015 -- 极长的持续时间
            
            apply_ok = pcall(function()
                action.add_buff(target_buff_id, eid, duration, level, eid, reason)
            end)
            
            if not apply_ok then
                apply_ok = pcall(function()
                    action:add_buff(target_buff_id, eid, duration, level, eid, reason)
                end)
            end
        else
            apply_ok = false
        end

        if apply_ok then
            _G.CURRENT_ATTACK_BUFF_ID = target_buff_id
            write_debug(string.format("攻击倍率设置成功：%s (Buff ID: %d)", target_config.desc, target_buff_id))
        else
            write_debug(string.format("攻击倍率设置失败：无法添加Buff %d (倍率x%.1f)", target_buff_id, target_multiple))
        end
    else
        _G.CURRENT_ATTACK_BUFF_ID = 0
    end
    
    return apply_ok
end

-- ==================== 补充之前的速度设置核心函数（确保可用） ====================
-- 旧版对话速度调节（剧情加速）
local function apply_old_dialog_speed(speed, enable)
    if G then
        if G.dialog_global_time_scale ~= nil then G.dialog_global_time_scale = speed end
        if G.space then
            local space = G.space
            if space.dialog_global_time_scale ~= nil then space.dialog_global_time_scale = speed end
            if space.dialog_set_global_time_scale then pcall(space.dialog_set_global_time_scale, space, speed) end
            if space.imp_dialogs_manager then
                local dm = space.imp_dialogs_manager
                if dm.dialog_set_global_time_scale then pcall(dm.dialog_set_global_time_scale, dm, speed) end
            end
        end
        local mp = G.main_player
        if mp and mp.dialog_set_time_speed_scale then
            pcall(mp.dialog_set_time_speed_scale, mp, enable, speed)
        end
    end
end

-- 新版GM攻击速度调节
local function apply_new_gm_speed(speed)
    local mod = "hexm.client.ui.windows.gm.gm_combat.combat_train_action"
    local action = nil
    pcall(function() action = portable.import(mod) end)
    if action and action.set_game_speed then
        pcall(action.set_game_speed, speed)
        return true
    end
    return false
end

-- 剧情速度设置入口（独立）
_G.RunSetDialogSpeed = function(valIndex)
    local speeds = {1.0, 1.5, 3.0, 5.0, 7.5, 10.0, 30.0}
    local targetSpeed = speeds[valIndex] or 1.0
    apply_old_dialog_speed(targetSpeed, true)
    write_debug("通过旧版接口设置对话速度为: x" .. targetSpeed)
end

-- 攻击速度设置入口（独立）
_G.RunSetGMSpeed = function(valIndex)
    local speeds = {1.0, 1.5, 3.0, 5.0, 7.5, 10.0, 30.0}
    local targetSpeed = speeds[valIndex] or 1.0
    local ok_new = apply_new_gm_speed(targetSpeed)
    if ok_new then
        write_debug("通过GM接口设置攻击速度为: x" .. targetSpeed)
    else
        write_debug("未找到GM速度设置接口，攻击速度未修改")
    end
end

-- 攻击倍率设置入口（新增，与原有按钮逻辑风格一致）
_G.RunSetAttackMultiple = function(valIndex)
    local ok = apply_attack_multiple_buff(valIndex)
    local target_config = attack_buff_config[valIndex] or attack_buff_config[1]
    return ok, target_config.multiple, target_config.desc
end

-- ==================== 原有剧情加速按钮（保留） ====================
queueButton("剧情加速",
    function()
        -- 打开速度选择器并提供确认回调函数
        ShowSpeedPicker(function(selectedIndex)
            -- 验证选中的索引
            if type(selectedIndex) ~= "number" then return end

            -- 更新剧情速度全局索引
            _G.SPEED_INDEX = selectedIndex

            -- 尝试使用现有辅助函数应用新速度（尽力尝试）
            pcall(function()
                if type(_G.ApplySpeed) == "function" then
                    _G.ApplySpeed(_G.SPEED_INDEX)
                elseif type(_G.SetSpeedIndex) == "function" then
                    _G.SetSpeedIndex(_G.SPEED_INDEX)
                elseif type(_G.CycleSpeed) == "function" then
                    pcall(_G.CycleSpeed, 0)
                end
            end)

            -- 强制更新视觉效果和按钮文本
            if type(_G.ForceUpdateVisuals) == "function" then pcall(_G.ForceUpdateVisuals) end
            UpdateButtonVisuals()
        end, "dialog")  -- 指定选择器类型
    end,
    function(d) _G.CycleSpeed(d) end,
    function() return _G.SPEED_INDEX > 1 end,
    function() return "剧情加速 < " .. SPEED_LABELS[_G.SPEED_INDEX] .. " >" end,
    true)

-- ==================== 原有攻击速度按钮（保留并优化） ====================
queueButton("攻击速度",
    function()
        -- 打开速度选择器（攻击速度专用）
        ShowSpeedPicker(function(selectedIndex)
            -- 验证选中的索引
            if type(selectedIndex) ~= "number" then return end

            -- 更新攻击速度全局索引（独立存储）
            _G.ATTACK_SPEED_INDEX = selectedIndex

            -- 应用攻击速度（调用专属函数）
            pcall(function()
                if type(_G.RunSetGMSpeed) == "function" then
                    _G.RunSetGMSpeed(_G.ATTACK_SPEED_INDEX)
                end
            end)

            -- 强制更新视觉效果和按钮文本
            if type(_G.ForceUpdateVisuals) == "function" then pcall(_G.ForceUpdateVisuals) end
            UpdateButtonVisuals()
        end, "attack_speed")  -- 指定选择器类型
    end,
    function(d) _G.CycleAttackSpeed(d) end,  -- 绑定攻击速度循环函数
    function() return _G.ATTACK_SPEED_INDEX > 1 end,  -- 按钮激活条件（非1倍速）
    function() return "攻击速度 < " .. SPEED_LABELS[_G.ATTACK_SPEED_INDEX] .. " >" end,  -- 按钮文本（显示当前攻击速度）
    true)  -- 保持按钮可见

-- ==================== 新增：攻击倍率按钮 ====================
queueButton("攻击倍率",
    function()
        -- 打开速度选择器（攻击倍率专用）
        ShowSpeedPicker(function(selectedIndex)
            -- 验证选中的索引
            if type(selectedIndex) ~= "number" then return end

            -- 更新攻击倍率全局索引（独立存储）
            _G.ATTACK_MULTIPLE_INDEX = selectedIndex

            -- 应用攻击倍率（调用专属函数）
            pcall(function()
                if type(_G.RunSetAttackMultiple) == "function" then
                    _G.RunSetAttackMultiple(_G.ATTACK_MULTIPLE_INDEX)
                end
            end)

            -- 强制更新视觉效果和按钮文本
            if type(_G.ForceUpdateVisuals) == "function" then pcall(_G.ForceUpdateVisuals) end
            UpdateButtonVisuals()
        end, "attack_multiple")  -- 指定选择器类型为攻击倍率
    end,
    function(d) _G.CycleAttackMultiple(d) end,  -- 绑定攻击倍率循环函数
    function() return _G.ATTACK_MULTIPLE_INDEX > 1 end,  -- 按钮激活条件（非1倍速）
    function() return "攻击倍率 < " .. ATTACK_MULTIPLE_LABELS[_G.ATTACK_MULTIPLE_INDEX] .. " >" end,  -- 按钮文本（显示当前攻击倍率）
    true)  -- 保持按钮可见

queueButton("自动拾取", _G.ExecuteLoot, function(d) _G.CycleState("LOOT_STATE", d, 2) end, function() return _G.LOOT_STATE > 0 end,
    function() return "自动拾取 < " .. STATE_LABELS[_G.LOOT_STATE] .. " >" end, true)

queueButton("自动施加Buff", _G.ExecuteBuff, function(d) _G.CycleState("BUFF_STATE", d, 2) end, function() return _G.BUFF_STATE > 0 end,
    function() return "自动施加Buff < " .. STATE_LABELS[_G.BUFF_STATE] .. " >" end, true)

queueButton("自动恢复", _G.ExecuteRecover, function(d) _G.CycleState("RECOVER_STATE", d, 2) end, function() return _G.RECOVER_STATE > 0 end,
    function() return "自动恢复 < " .. STATE_LABELS[_G.RECOVER_STATE] .. " >" end, true)

-- --- 2. 计算动态高度 ---
local contentHeight = #tempButtons * (btnHeight + spacing)
local totalHeight   = headerSpace + contentHeight + footerSpace

write_debug("计算面板高度: 内容=" .. contentHeight .. ", 总高=" .. totalHeight)

-- --- 3. 创建面板 ---
local panel = ccui.Layout:create()

-- 重要：如果面板高度超过屏幕，确保面板在屏幕内显示
local screenW, screenH = size.width, size.height
panel:setContentSize(cc.size(panelWidth, totalHeight))
panel:setBackGroundColorType(1)
panel:setBackGroundColor(C_BG)
panel:setBackGroundColorOpacity(240)

-- 限制位置以确保可见
local startX = 40
local startY = math.max(20, screenH - totalHeight - 20)
panel:setPosition(cc.p(startX, startY))

-- 设置层级和可见性
panel:setLocalZOrder(9999)
panel:setVisible(true)

scene:addChild(panel, 9999)
_G.GM_MENU = panel

write_debug("面板创建成功，位置: " .. startX .. ", " .. startY)

-- 验证创建是否成功
local function verify_menu_created()
    local ok, parent = pcall(function() return _G.GM_MENU and _G.GM_MENU:getParent() end)
    if not ok or not parent then
        write_debug(("UI：菜单创建失败。版本：%s"):format(MENU_VERSION))
        return false
    end
    return true
end

-- --- 4. 创建头部标题 ---
local headerY = totalHeight - 40

local headerLeft = createText("燕云十六声 菜单 " .. MENU_VERSION, 54)
headerLeft:setColor(cc.c3b(255, 230, 120))
headerLeft:enableOutline(cc.c4b(0, 255, 0, 255), 2)
headerLeft:setAnchorPoint(cc.p(0, 1))
panel:addChild(headerLeft)

local headerRight = createText("", 54)
headerRight:setColor(cc.c3b(140, 20, 20)) -- 深红色
headerRight:setAnchorPoint(cc.p(0, 1))
panel:addChild(headerRight)

-- 将两个文本作为整体居中显示
local w1 = headerLeft:getContentSize().width
local w2 = headerRight:getContentSize().width
local totalW = w1 + w2
local startX = (panelWidth - totalW) / 2

headerLeft:setPosition(cc.p(startX, headerY))
headerRight:setPosition(cc.p(startX + w1, headerY))

write_debug("头部标题创建完成")

-- --- 5. 创建按钮 ---
local startY = totalHeight - headerSpace

write_debug("开始创建按钮，数量: " .. #tempButtons)

for i, itemData in ipairs(tempButtons) do
    local b = ccui.Button:create()
    b:setTitleFontSize(56)
    b:setScale9Enabled(true)
    b:setContentSize(cc.size(buttonWidth, btnHeight))
    b:setColor(C_BG)

    -- 重要：所有按钮的文本由我们自行渲染
    b:setTitleText("")

    -- 位置：从上到下排列
    b:setAnchorPoint(cc.p(0.5, 1))
    b:setPosition(cc.p(panelWidth / 2, startY - ((i-1) * (btnHeight + spacing))))

    if type(b.setTouchEnabled) == "function" then 
        b:setTouchEnabled(true) 
    end

    -- 文本节点（统一颜色管理）
    local mainTextNode = nil
    local valueTextNode = nil

    if itemData.isValueBtn then
        -- 主文本（左侧）
        mainTextNode = createText("", 56)
        mainTextNode:setAnchorPoint(cc.p(0, 0.5))
        mainTextNode:setPosition(cc.p(30, btnHeight / 2))
        b:addChild(mainTextNode, 1)

        -- 数值文本（右侧）：< ... >
        valueTextNode = createText("", 56)
        valueTextNode:setAnchorPoint(cc.p(1, 0.5))
        valueTextNode:setPosition(cc.p(buttonWidth - 30, btnHeight / 2))
        b:addChild(valueTextNode, 1)
    else
        -- 普通按钮：单个居中文本
        mainTextNode = createText("", 56)
        mainTextNode:setAnchorPoint(cc.p(0.5, 0.5))
        mainTextNode:setPosition(cc.p(buttonWidth / 2, btnHeight / 2))
        b:addChild(mainTextNode, 1)
    end

    -- 存储功能逻辑
    local btnObj = {
        btn = b,
        execFunc = itemData.execFunc,
        cycleFunc = itemData.cycleFunc,
        isActive = itemData.isActive,

        -- 暴露节点供设置颜色
        mainText = mainTextNode,
        valueText = valueTextNode,

        updateText = function()
            local text = itemData.label
            if itemData.dynamicLabelFunc then
                text = itemData.dynamicLabelFunc()
            elseif itemData.isActive then
                local ok, val = pcall(itemData.isActive)
                if ok then
                    text = itemData.label .. (val and " < 开启 >" or " < 关闭 >")
                else
                    text = itemData.label
                end
            end

            if valueTextNode then
                -- 拆分文本为两部分："内容 " + "<...>"
                local mainPart, valuePart = text:match("^(.-)%s*(<.*>)%s*$")
                if not mainPart then
                    mainPart = text
                    valuePart = ""
                end
                mainTextNode:setString(mainPart)
                valueTextNode:setString(valuePart)
            else
                -- 普通按钮：所有文本放入主文本节点
                mainTextNode:setString(text)
            end
        end
    }

    panel:addChild(b)
    -- 在添加监听器前插入按钮列表，确保监听器可立即引用
    table.insert(buttonList, btnObj)
    local idx = #buttonList -- 捕获当前按钮的实际索引

    -- 触摸事件处理器：修复重复切换问题，确保标签即时更新
    b:addTouchEventListener(function(sender, eventType)
        -- eventType: 0 = 开始触摸, 1 = 触摸移动, 2 = 触摸结束（Cocos2d-x 约定）
        if eventType == 0 then
            -- 触摸开始：切换选中状态到当前按钮，但不执行操作
            selectedIndex = idx
            UpdateSelection()
            return true
        elseif eventType == 2 then
            -- 触摸结束：执行按钮操作
            local isSpeedButton = (itemData.label == "剧情加速")

            if isSpeedButton then
                -- 速度按钮仅通过execFunc打开选择器
                if itemData.execFunc then
                    pcall(itemData.execFunc)
                end
            else
                if itemData.isValueBtn then
                    -- 数值按钮：
                    -- 鼠标点击优先使用cycleFunc（确保开关和状态切换行为正确）
                    if itemData.cycleFunc then
                        pcall(function() itemData.cycleFunc(1) end)
                    end
                    -- 切换后执行execFunc（如隐身模式需要执行ExecuteInvisibility）
                    if itemData.execFunc then
                        pcall(itemData.execFunc)
                    end
                else
                    -- 普通按钮：优先使用execFunc；若无则回退到cycleFunc
                    if itemData.execFunc then
                        pcall(itemData.execFunc)
                    elseif itemData.cycleFunc then
                        pcall(function() itemData.cycleFunc(1) end)
                    end
                end
            end

            -- 立即更新当前按钮文本（确保可变值和开关状态即时显示）
            pcall(function()
                if buttonList[idx] and buttonList[idx].updateText then
                    buttonList[idx].updateText()
                end
            end)

            -- 操作后刷新所有按钮视觉样式
            pcall(UpdateButtonVisuals)
        end
    end)

    write_debug("按钮 " .. i .. " 创建完成: " .. itemData.label)
end

write_debug("所有按钮创建完成，总数: " .. #buttonList)

-- 鼠标移动悬停选中
pcall(function()
    -- 创建鼠标监听器（适配Cocos2d-x v3风格）
    local listener = cc.EventListenerMouse:create()
    listener:registerScriptHandler(function(event)
        local x, y = event:getCursorX(), event:getCursorY()
        -- 将全局光标坐标转换为面板本地坐标
        local localPt = panel:convertToNodeSpace(cc.p(x, y))
        local lx, ly = localPt.x, localPt.y

        -- 遍历按钮并检查边界框
        for idx, btnObj in ipairs(buttonList) do
            local btn = btnObj.btn
            if btn and type(btn.getPosition) == "function" and type(btn.getContentSize) == "function" then
                local pos = btn:getPosition()
                local size = btn:getContentSize()
                -- 锚点为(0.5,1)；据此计算矩形范围
                local left = pos.x - (size.width * 0.5)
                local right = pos.x + (size.width * 0.5)
                local top = pos.y
                local bottom = pos.y - size.height

                -- localPt为面板坐标系（原点在左下角）
                if lx >= left and lx <= right and ly >= bottom and ly <= top then
                    if selectedIndex ~= idx then
                        selectedIndex = idx
                        UpdateSelection()
                    end
                    return -- 找到悬停按钮；停止检查
                end
            end
        end
    end, cc.Handler.EVENT_MOUSE_MOVE)

    -- 将监听器附加到面板的事件分发器
    local dispatcher = panel:getEventDispatcher()
    if dispatcher and listener then
        dispatcher:addEventListenerWithSceneGraphPriority(listener, panel)
    end
    write_debug("鼠标监听器注册完成")
end)

-- --- 6. 创建页脚（双列 + 背景，无溢出） ---
local dividerY = footerSpace - 50

-- 分隔线
local div = ccui.Layout:create()
div:setBackGroundColorType(1)
div:setBackGroundColor(cc.c3b(255, 255, 255))
div:setBackGroundColorOpacity(80)
div:setContentSize(cc.size(panelWidth - 60, 4))
div:setPosition(cc.p(30, dividerY))
panel:addChild(div)

local FOOTER_COLOR = cc.c3b(160, 160, 160)
local FOOTER_FONT  = 34

-- 左列：快速快捷键
local leftText =
"快速快捷键:\n" ..
"Scrol_Lock+数字1: 剧情加速\n" ..
"Scrol_Lock+数字2: 拾取循环\n" ..
"Scrol_Lock+数字3: 攻击加速\n" ..
"Scrol_Lock+数字5: 清除NPC\n" ..
"Scrol_Lock+数字7: 自动恢复\n" ..
"Scrol_Lock+数字8: 施加一次Buff\n" ..
"Scrol_Lock+数字9: 拾取一次"

local lblLeft = createText(leftText, FOOTER_FONT)
lblLeft:setColor(FOOTER_COLOR)
lblLeft:setAnchorPoint(cc.p(0, 1))
lblLeft:ignoreContentAdaptWithSize(false)
lblLeft:setContentSize(cc.size((panelWidth * 0.55) - 40, dividerY - 30))
lblLeft:setPosition(cc.p(30, dividerY - 20))
panel:addChild(lblLeft)

-- 右列：导航操作
local rightText =
"导航操作:\n" ..
"小键盘 [8/2] 上/下\n" ..
"小键盘 [4/6] 修改数值\n" ..
"小键盘 [5] 确认执行"

local lblRight = createText(rightText, FOOTER_FONT)
lblRight:setColor(FOOTER_COLOR)
lblRight:setAnchorPoint(cc.p(0, 1))
lblRight:ignoreContentAdaptWithSize(false)
lblRight:setContentSize(cc.size((panelWidth * 0.45) - 40, dividerY - 30))
lblRight:setPosition(cc.p(panelWidth * 0.55 + 10, dividerY - 20))
panel:addChild(lblRight)

write_debug("页脚创建完成")

-- === UpdateButtonVisuals 和 UpdateSelection 实现 ===
-- 更新所有按钮的视觉样式（文本 + 颜色）
function UpdateButtonVisuals()
    for idx, btnObj in ipairs(buttonList) do
        -- 使用按钮专属更新函数更新文本
        pcall(function() 
            if btnObj.updateText then 
                btnObj.updateText() 
            end 
        end)

        -- 颜色 / 高亮逻辑
        pcall(function()
            local main = btnObj.mainText
            local value = btnObj.valueText
            local isSel = (idx == selectedIndex)
            local active = false
            if btnObj.isActive and type(btnObj.isActive) == "function" then
                local ok, val = pcall(btnObj.isActive)
                if ok then active = val end
            end

            -- 选择颜色
            local colorMain = C_TEXT_OFF  -- 使用预定义的颜色常量
            local colorValue = C_READONLY

            -- 激活状态颜色（绿色）
            if active then
                colorMain = C_TEXT_ON
                colorValue = cc.c3b(180, 240, 180)
            end

            -- 选中高亮色：紫色
            if isSel then
                colorMain = C_SELECTED
            end

            if main and type(main.setColor) == "function" then 
                main:setColor(colorMain) 
            end
            if value and type(value.setColor) == "function" then 
                value:setColor(colorValue) 
            end

            -- 为选中按钮添加描边
            if main and type(main.enableOutline) == "function" then
                if isSel then
                    main:enableOutline(cc.c4b(120, 40, 255, 200), 2)
                else
                    main:enableOutline(cc.c4b(0,0,0,0), 0)
                end
            end
        end)
    end
end

-- 更新选中状态视觉样式
function UpdateSelection()
    -- 确保索引在有效范围内
    if selectedIndex < 1 then selectedIndex = 1 end
    if selectedIndex > #buttonList then selectedIndex = #buttonList end
    -- 刷新视觉样式
    pcall(UpdateButtonVisuals)
end

-- 初始化
UpdateButtonVisuals()
UpdateSelection()

write_debug("UI视觉样式初始化完成")

-- 添加测试文本验证显示
local testText = createText("UI创建成功 - " .. MENU_VERSION, 24)
testText:setColor(cc.c3b(0, 255, 0))
testText:setPosition(cc.p(panelWidth/2, 30))
panel:addChild(testText)

write_debug("=== UI界面构建完成 ===")
-- =================================================================================
-- 第六部分：拖拽与最小化逻辑
-- =================================================================================

-- 边界检查
local function keepPanelInBounds(newX, newY)
    local pSize = panel:getContentSize()
    local minX, maxX = 0, size.width - pSize.width
    local minY, maxY = 0, size.height - pSize.height
    return math.max(minX, math.min(maxX, newX)), math.max(minY, math.min(maxY, newY))
end

-- === 极简交互辅助函数（仅定义一次） ===
if not _G._GM_MENU_INTERACTION_DEFINED then
    _G._GM_MENU_INTERACTION_DEFINED = true

    function _G.enable_menu_interaction()
        pcall(function()
            local p = _G.GM_MENU
            if not p then return end

            -- 优先使用registerScriptTouchHandler（若可用）
            if type(p.registerScriptTouchHandler) == "function" then
                p:registerScriptTouchHandler(function(t, ev)
                    if ev == 0 then
                        local loc = t:getLocation()
                        p.__drag_start = {x = loc.x, y = loc.y}
                        p.__orig_pos = {x = p:getPositionX(), y = p:getPositionY()}
                        return true
                    elseif ev == 1 then
                        if p.__drag_start and p.__orig_pos then
                            local loc = t:getLocation()
                            p:setPosition(p.__orig_pos.x + (loc.x - p.__drag_start.x),
                                          p.__orig_pos.y + (loc.y - p.__drag_start.y))
                        end
                    else
                        p.__drag_start = nil; p.__orig_pos = nil
                    end
                end, false, 0, true)
                if type(p.setTouchEnabled) == "function" then p:setTouchEnabled(true) end
                return
            end

            -- 回退方案：使用addTouchEventListener风格
            if type(p.addTouchEventListener) == "function" then
                p:addTouchEventListener(function(sender, ev)
                    if ev == 0 then
                        local pos = sender:getTouchBeganPosition()
                        p.__drag_start = {x = pos.x, y = pos.y}
                        p.__orig_pos = {x = p:getPositionX(), y = p:getPositionY()}
                        return true
                    elseif ev == 1 then
                        if p.__drag_start and p.__orig_pos then
                            local pos = sender:getTouchMovePosition()
                            p:setPosition(p.__orig_pos.x + (pos.x - p.__drag_start.x),
                                          p.__orig_pos.y + (pos.y - p.__drag_start.y))
                        end
                    else
                        p.__drag_start = nil; p.__orig_pos = nil
                    end
                end)
                if type(p.setTouchEnabled) == "function" then p:setTouchEnabled(true) end
                return
            end

            -- 最终回退：尝试使用touchLayer（若存在）
            if p.touchLayer and type(p.touchLayer.registerScriptTouchHandler) == "function" then
                p.touchLayer:registerScriptTouchHandler(function(t, ev)
                    if ev == 0 then
                        local loc = t:getLocation()
                        p.__drag_start = {x = loc.x, y = loc.y}
                        p.__orig_pos = {x = p:getPositionX(), y = p:getPositionY()}
                        return true
                    elseif ev == 1 then
                        if p.__drag_start and p.__orig_pos then
                            local loc = t:getLocation()
                            p:setPosition(p.__orig_pos.x + (loc.x - p.__drag_start.x),
                                          p.__orig_pos.y + (loc.y - p.__drag_start.y))
                        end
                    else
                        p.__drag_start = nil; p.__orig_pos = nil
                    end
                end, false, 0, true)
                if type(p.touchLayer.setTouchEnabled) == "function" then p.touchLayer:setTouchEnabled(true) end
            end
        end)
    end

    function _G.disable_menu_interaction()
        pcall(function()
            local p = _G.GM_MENU
            if not p then return end
            if type(p.unregisterScriptTouchHandler) == "function" then p:unregisterScriptTouchHandler() end
            if type(p.removeTouchEventListener) == "function" then
                -- 尽力尝试：移除所有监听器（API可能不同）
                p:removeTouchEventListener()
            end
            if type(p.setTouchEnabled) == "function" then p:setTouchEnabled(false) end
            if p.touchLayer and type(p.touchLayer.setTouchEnabled) == "function" then p.touchLayer:setTouchEnabled(false) end
            p.__drag_start = nil; p.__orig_pos = nil
        end)
    end
end

-- 绑定交互事件（初始化）
_G.GM_MENU = panel
if type(_G.enable_menu_interaction) == "function" then pcall(_G.enable_menu_interaction) end

-- 最小化按钮
local btn_minimize = ccui.Button:create()
btn_minimize:setTitleText("−")
btn_minimize:setTitleFontSize(52) -- 字体大小从26调整为52
btn_minimize:setTitleColor(cc.c3b(255, 255, 120))
-- 位置偏移加倍（65→130，25→50）
btn_minimize:setPosition(cc.p(panelWidth - 130, totalHeight - 50))
panel:addChild(btn_minimize)

-- 快捷键提示（最小化时显示的悬浮按钮）
keybindInfo = ccui.Button:create()
keybindInfo:setTitleText(" 点击打开 ")
keybindInfo:setTitleFontSize(48) -- 字体大小从24调整为48
keybindInfo:setTitleColor(cc.c3b(200,200,255))
keybindInfo:setVisible(false)
keybindInfo:setPosition(cc.p(panelWidth/2, 50)) -- 高度加倍（25→50）
panel:addChild(keybindInfo)

-- 最小化/恢复逻辑（已更新：隐藏头部和底部区域）
local function ToggleMinimize()
    isMinimized = not isMinimized
    if isMinimized then
        -- 保存原始尺寸和位置
        originalSize = panel:getContentSize()
        panel:setContentSize(cc.size(panelWidth, 100)) -- 最小化后的高度

        -- 隐藏所有按钮
        for _, item in ipairs(buttonList) do item.btn:setVisible(false) end

        -- 隐藏头部和底部元素
        if headerLeft and type(headerLeft.setVisible) == "function" then headerLeft:setVisible(false) end
        if headerRight and type(headerRight.setVisible) == "function" then headerRight:setVisible(false) end
        if div and type(div.setVisible) == "function" then div:setVisible(false) end
        if div2 and type(div2.setVisible) == "function" then div2:setVisible(false) end
        if lblLeft and type(lblLeft.setVisible) == "function" then lblLeft:setVisible(false) end
        if lblRight and type(lblRight.setVisible) == "function" then lblRight:setVisible(false) end
        if closeBtn and type(closeBtn.setVisible) == "function" then closeBtn:setVisible(false) end

        -- 显示小型快捷键提示，并更新最小化按钮的样式/位置
        keybindInfo:setVisible(true)
        btn_minimize:setTitleText("⬜")
        btn_minimize:setPosition(cc.p(panelWidth - 130, 50))

        -- 最小化时禁用交互
        if type(_G.disable_menu_interaction) == "function" then pcall(_G.disable_menu_interaction) end
    else
        -- 恢复面板尺寸
        panel:setContentSize(originalSize)

        -- 恢复按钮显示
        for _, item in ipairs(buttonList) do item.btn:setVisible(true) end

        -- 恢复头部和底部元素
        if headerLeft and type(headerLeft.setVisible) == "function" then headerLeft:setVisible(true) end
        if headerRight and type(headerRight.setVisible) == "function" then headerRight:setVisible(true) end
        if div and type(div.setVisible) == "function" then div:setVisible(true) end
        if div2 and type(div2.setVisible) == "function" then div2:setVisible(true) end
        if lblLeft and type(lblLeft.setVisible) == "function" then lblLeft:setVisible(true) end
        if lblRight and type(lblRight.setVisible) == "function" then lblRight:setVisible(true) end
        if closeBtn and type(closeBtn.setVisible) == "function" then closeBtn:setVisible(true) end

        -- 隐藏快捷键提示，恢复最小化按钮的样式/位置
        keybindInfo:setVisible(false)
        btn_minimize:setTitleText("−")
        btn_minimize:setPosition(cc.p(panelWidth - 130, totalHeight - 50))

        -- 恢复显示时重新启用交互
        if type(_G.enable_menu_interaction) == "function" then pcall(_G.enable_menu_interaction) end
    end
end

btn_minimize:addTouchEventListener(function(s,e) if e == 2 then ToggleMinimize() end end)
keybindInfo:addTouchEventListener(function(s,e) if e == 2 then ToggleMinimize() end end)

-- 关闭按钮（最小化而非销毁）
local closeBtn = ccui.Button:create()
closeBtn:setTitleText("X")
closeBtn:setTitleFontSize(48) -- 字体大小从24调整为48
closeBtn:setTitleColor(cc.c3b(255, 80, 80))
-- 位置偏移加倍（25→50）
closeBtn:setPosition(cc.p(panelWidth - 50, totalHeight - 50))

closeBtn:addTouchEventListener(function(s, e)
    if e == 2 then
        pcall(function()
            -- 隐藏面板（而非移除），以便JS快捷键后续恢复显示
            if panel and type(panel.setVisible) == "function" then
                panel:setVisible(false)
            end

            -- 禁用交互，避免隐藏的面板捕获输入事件
            if type(_G.disable_menu_interaction) == "function" then pcall(_G.disable_menu_interaction) end

            -- 保留全局引用，以便Toggle/Show函数能恢复面板

            -- 触发视觉刷新辅助函数（若存在）
            if _G and type(_G.ForceUpdateVisuals) == "function" then
                pcall(_G.ForceUpdateVisuals)
            end
        end)
    end
end)

panel:addChild(closeBtn)

-- JS可调用的切换辅助函数（显示时确保重新启用交互）
_G.ToggleGMMenu = _G.ToggleGMMenu or function()
    if not _G.GM_MENU then
        if type(_G.CreateGMMenu) == "function" then pcall(_G.CreateGMMenu) end
    end
    if not _G.GM_MENU then return end

    local ok, vis = pcall(function() return _G.GM_MENU:isVisible() end)
    if ok and vis then
        pcall(function()
            _G.GM_MENU:setVisible(false)
            if type(_G.disable_menu_interaction) == "function" then pcall(_G.disable_menu_interaction) end
        end)
    else
        pcall(function()
            _G.GM_MENU:setVisible(true)
            if type(_G.enable_menu_interaction) == "function" then pcall(_G.enable_menu_interaction) end
            if _G.GM_MENU.getParent and _G.GM_MENU:getParent().reorderChild then
                local parent = _G.GM_MENU:getParent()
                pcall(function() parent:reorderChild(_G.GM_MENU, 9999) end)
            end
            if type(_G.ForceUpdateVisuals) == "function" then pcall(_G.ForceUpdateVisuals) end
        end)
    end
end

-- 初始化视觉样式
UpdateButtonVisuals()
write_debug("菜单创建完成。尺寸：" .. panelWidth .. "x" .. totalHeight)

-- 最终成功报告
if DEBUG_FILE_ENABLED then
    pcall(function()
        local file = io.open(DEBUG_FILE_PATH, "a")
        if file then
            file:write(os.date("%H:%M:%S") .. " === 脚本执行成功（菜单版本 " .. MENU_VERSION .. "）===\n")
            file:close()
        end
    end)
end
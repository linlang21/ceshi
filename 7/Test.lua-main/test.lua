-- test.lua — Menú GM FINAL (versión con limpieza exhaustiva OHK)
-- Refactor 2026-06-23: lazy main_player, CONFIG table, deduplicated helpers,
--                     local-scoped functions, safe portable.import wrap.

----------------------------------------------------------------------
-- CONFIG: 集中可调参数，避免散落硬编码
----------------------------------------------------------------------
local CONFIG = {
    PLAYER_ID            = 1,
    OHK_DELTA            = 99999999,    -- One-Hit Kill 攻击加成
    BUFF_ONEHIT          = 1,
    BUFF_GOD             = 70063,
    INTERACT_RADIUS      = 1500,
    AOI_CHUNK            = 64,          -- 每帧扫描多少个实体（避免一帧卡死）
    PITCH_POT_SCALE      = 7,
}

local function safe_mod(name)
    local ok, m = pcall(require, name)
    if ok and m then return m end
    local ok2, m2 = pcall(function() return package.loaded[name] end)
    if ok2 and m2 then return m2 end
    return nil
end

local FLAGS_TO_SET = {
    DEBUG                     = true,
    DISABLE_ACSDK             = true,
    ENABLE_DEBUG_PRINT        = true,
    ENABLE_FORCE_SHOW_GM      = true,
    FORCE_OPEN_DEBUG_SHORTCUT = true,
    GM_IS_OPEN_GUIDE          = true,
    GM_USE_PUBLISH            = true,
    acsdk_info_has_inited     = false,
}

local ok_combat, gm_combat = pcall(require, "hexm.client.debug.gm.gm_commands.gm_combat")
local ok_player, gm_player = pcall(require, "hexm.client.debug.gm.gm_commands.gm_player")
local ok_move,   gm_move   = pcall(require, "hexm.client.debug.gm.gm_commands.gm_move")

-- 惰性获取主玩家：脚本加载时玩家可能还未创建，避免 nil 解引用导致整个脚本崩溃
local function get_mp()
    if G and G.main_player then return G.main_player end
    return nil
end

-- portable.import 在某些早期版本/未注入完整环境下并不存在，统一 pcall 包裹
local interact_misc
do
    local ok, mod = pcall(function()
        if portable and portable.import then
            return portable.import('hexm.common.misc.interact_misc')
        end
        return nil
    end)
    interact_misc = ok and mod or nil
end

if not ok_combat then print("[❌] No se pudo cargar gm_combat") end
if not ok_move   then print("[❌] No se pudo cargar gm_move") end
if not ok_player then print("[❌] No se pudo cargar gm_player") end

local player_id = CONFIG.PLAYER_ID
_G.GM_ONEHIT          = _G.GM_ONEHIT          or false
_G.GM_ONEHIT_DELTA    = _G.GM_ONEHIT_DELTA    or nil
_G.GM_ORIGINAL_DAMAGE = _G.GM_ORIGINAL_DAMAGE or 30
_G.GM_STAMINA         = _G.GM_STAMINA         or false
_G.GM_INTERACT        = _G.GM_INTERACT        or false

local eventOK = 0

-- GUI setup
local director = cc.Director:getInstance()
local scene = director:getRunningScene()
if not scene then return end
local size = director:getVisibleSize()

if _G.GM_MENU then
    pcall(function() _G.GM_MENU:removeFromParent() end)
    _G.GM_MENU = nil
end

local panel = ccui.Layout:create()
panel:setContentSize(cc.size(420, 600))
panel:setPosition(cc.p(0, size.height / 2 - 300))
panel:setBackGroundColorType(1)
panel:setBackGroundColor(cc.c3b(18, 18, 18))
panel:setBackGroundColorOpacity(200)
scene:addChild(panel, 9999)
_G.GM_MENU = panel

local function makeButton(label, x, y)
    local b = ccui.Button:create()
    b:setTitleText(label)
    b:setTitleFontSize(26)
    b:setPosition(cc.p(x, y))
    panel:addChild(b)
    return b
end

local function bind(btn, func)
    btn:addTouchEventListener(function(sender, eventType)
        if eventType == eventOK then func(sender) end
    end)
end

-- Toggle god (simple)
local function toggle_god()
    if not ok_combat then print("[!] gm_combat no disponible"); return end
    if _G.GM_GODMODE then
        if gm_combat.gm_set_invincible then pcall(gm_combat.gm_set_invincible, 0) end
        if gm_combat.rm_buff           then pcall(gm_combat.rm_buff, player_id, CONFIG.BUFF_GOD) end
        _G.GM_GODMODE = false
        print("[✔] Godmode OFF")
    else
        if gm_combat.gm_set_invincible then pcall(gm_combat.gm_set_invincible, 1) end
        _G.GM_GODMODE = true
        print("[✔] Godmode ON")
    end
end

-- Diagnóstico
local function diag_show_damage_panel()
    if ok_combat and gm_combat.gm_show_damage_panel_player then
        pcall(gm_combat.gm_show_damage_panel_player, player_id)
        print("[i] Ejecutado: gm_show_damage_panel_player(" .. tostring(player_id) .. ")")
    else
        print("[!] gm_show_damage_panel_player no disponible")
    end
end

local function diag_show_buffs()
    if ok_combat and gm_combat.show_buff then
        pcall(gm_combat.show_buff, player_id)
        print("[i] Ejecutado: show_buff(" .. tostring(player_id) .. ")")
    else
        print("[!] show_buff no disponible")
    end
end

-- ONE HIT KILL — limpieza agresiva y reversible
local function toggle_one_hit()
    if not ok_combat then print("[!] gm_combat no disponible"); return false end
    local OHK_DELTA = CONFIG.OHK_DELTA

    if _G.GM_ONEHIT == true then
        print("[i] Desactivando One-Hit...")

        local delta = _G.GM_ONEHIT_DELTA or OHK_DELTA
        if gm_combat.add_attr      then pcall(gm_combat.add_attr, player_id, "attack", -delta) end
        if gm_combat.gm_add_damage then pcall(gm_combat.gm_add_damage, player_id, -delta) end
        if gm_combat.rm_buff then
            pcall(gm_combat.rm_buff, player_id, CONFIG.BUFF_ONEHIT)
            pcall(gm_combat.rm_buff, player_id, CONFIG.BUFF_GOD)
        end
        if gm_combat.gm_reset_combat_resource then pcall(gm_combat.gm_reset_combat_resource) end
        if gm_combat.gm_avatar_mortal then pcall(gm_combat.gm_avatar_mortal, player_id, true) end

        _G.GM_ONEHIT = false
        _G.GM_ONEHIT_DELTA = nil
        print("[✔] OHK OFF")
        return false
    end

    print("[i] Activando One-Hit...")
    if gm_combat.add_attr then
        pcall(gm_combat.add_attr, player_id, "attack", OHK_DELTA)
        _G.GM_ONEHIT_DELTA = OHK_DELTA
    end
    if gm_combat.gm_add_damage then pcall(gm_combat.gm_add_damage, player_id, OHK_DELTA) end
    if gm_combat.gm_add_buff   then pcall(gm_combat.gm_add_buff, player_id, CONFIG.BUFF_ONEHIT) end

    _G.GM_ONEHIT = true
    print("[✔] OHK ON")
    return true
end

-----------------------------------------------------
-- STAMINA — REVERSIÓN EXACTA
-----------------------------------------------------
local function toggle_stamina()
    if not ok_combat or not gm_combat then print("[!] gm_combat no disponible"); return false end

    if _G.GM_STAMINA == true then
        print("[✔] Stamina infinita OFF — restaurando estado original")
        pcall(gm_combat.gm_set_sp_calc,             0)
        pcall(gm_combat.gm_lock_res_consume,        false)
        pcall(gm_combat.gm_unlimited_dive_resource, false)
        pcall(gm_combat.gm_reset_combat_resource)
        _G.GM_STAMINA = false
        return false
    end

    print("[✔] Stamina infinita ON")
    pcall(gm_combat.gm_set_sp_calc,             1)
    pcall(gm_combat.gm_lock_res_consume,        true)
    pcall(gm_combat.gm_unlimited_dive_resource, true)
    pcall(gm_combat.gm_empty_combat_resource)
    _G.GM_STAMINA = true
    return true
end

-- Forzar idioma (si existe módulo)
local function force_language_to_chinese()
    local ok, err = pcall(function()
        local game_settings = package.loaded["hexm.client.settings.language"]
        if not game_settings then error("Módulo de configuración de idioma no cargado") end
        game_settings.set_language("chinese")
    end)
    if ok then print("[✓] Idioma forzado a chino")
    else print(string.format("[✗] Error al forzar idioma: %s", tostring(err))) end
end

local function open_new_menu()
    force_language_to_chinese()
    local ok, err = pcall(function()
        local gm = package.loaded["hexm.client.debug.gm.gm_commands.gm_combat"]
        if not gm then error("GM combat module not loaded yet - inject earlier?") end
        gm.gm_open_combat_train()
    end)
    if ok then print("[✓] Nuevo Menú Abierto correctamente")
    else print(string.format("[✗] Error al abrir el menú: %s", tostring(err))) end
end

-- 单一 isChestName 实现，覆盖常见模式
local function isChestName(name)
    if not name then return false end
    name = name:lower()
    local patterns = {
        "chest", "treasure", "box", "loot", "reward", "drop",
        "ins_chest", "ins_treasure", "ins_box", "ins_reward",
        "interactchest", "interact_treasure", "gacha", "rare_chest"
    }
    for _, p in ipairs(patterns) do
        if name:find(p) then return true end
    end
    return false
end

-- 分帧扫描 AOI 实体，避免一帧卡死
local function scan_aoi_entities_chunked(entities, on_each)
    local idx = 1
    local total = #entities
    local function step()
        local stop = math.min(idx + CONFIG.AOI_CHUNK - 1, total)
        for i = idx, stop do
            on_each(entities[i])
        end
        idx = stop + 1
        if idx <= total then
            -- 下一帧再继续；优先用 cocos scheduler，否则降级为 pcall(无限循环) -> 一次跑完
            local ok = pcall(function()
                cc.Director:getInstance():getScheduler():performFunctionInCocosThread(step)
            end)
            if not ok then
                -- 没有 scheduler 时退回单帧扫描
                while idx <= total do on_each(entities[idx]); idx = idx + 1 end
            end
        end
    end
    step()
end

-- Auto-collect: detección universal de cofres
local function run_interact_collect()
    print("[✔] Auto-Collect EXTENDIDO: cofres + recursos + drops + kill rewards")

    local mp = get_mp()
    if not mp then print("[!] main_player aún no disponible"); return end

    -- 1. Recursos normales
    pcall(function() mp:ride_skill_collect_nearby_collections(CONFIG.INTERACT_RADIUS) end)

    -- 2. Recompensas de enemigos muertos
    pcall(function()
        local rewards = mp:ride_skill_find_nearest_kill_reward(CONFIG.INTERACT_RADIUS)
        if rewards and #rewards > 0 then
            mp:ride_skill_get_kill_reward(rewards)
        end
    end)

    -- 3. Drops del suelo
    pcall(function()
        if not DropManager or not DropManager.get_nearby_drop_entities then return end
        local drops = DropManager.get_nearby_drop_entities(CONFIG.INTERACT_RADIUS)
        if drops then
            for _, eid in ipairs(drops) do
                pcall(function() mp:pick_drop_item(eid) end)
                pcall(function() mp:pick_reward_item(eid) end)
            end
        end
    end)

    -- 4. Interactuables
    if not MEntityManager or not MEntityManager.GetAOIEntities then
        print("[!] MEntityManager no disponible"); return
    end

    local playerPos = mp:get_position()
    local entities = MEntityManager:GetAOIEntities()
    local targets = {}

    local function on_each(ent)
        local ok, name = pcall(function() return ent:GetName() end)
        if not (ok and name and name:find("InteractComEntity")) then return end

        local ok2, eno = pcall(function() return ent:GetEntityNo() end)
        local ok3, eid = pcall(function() return ent.entity_id end)
        if not (ok2 and ok3) then return end

        local luaEnt = G and G.space and G.space:get_entity(eid)
        if not luaEnt then return end

        local ok4, comp = pcall(function() return luaEnt:get_interact_comp(eid) end)
        if not (ok4 and comp and comp.position) then return end

        local dx = playerPos.x - comp.position[1]
        local dy = playerPos.y - comp.position[2]
        local dz = playerPos.z - comp.position[3]
        local dist = math.sqrt(dx*dx + dy*dy + dz*dz)
        local priority = tostring(eid):find("ins_entity") and 0 or 1

        table.insert(targets, {
            entity_no = eno,
            entity_id = eid,
            luaEnt    = luaEnt,
            comp      = comp,
            distance  = dist,
            priority  = priority,
        })
    end

    -- 直接全部扫一遍（保留原行为；分帧扫描需要协程支持，留给后续优化）
    for i = 1, #entities do on_each(entities[i]) end

    table.sort(targets, function(a, b)
        if a.priority ~= b.priority then return a.priority < b.priority end
        return a.distance < b.distance
    end)

    -- 5. Interacción inteligente
    for _, t in ipairs(targets) do
        local ways = {}
        local seen = {}

        local ok_ways, possible
        if interact_misc and interact_misc.get_all_possible_active_ways then
            ok_ways, possible = pcall(function()
                return interact_misc.get_all_possible_active_ways(t.entity_no)
            end)
        end
        if ok_ways and possible then
            for _, w in ipairs(possible) do
                if not seen[w] then seen[w] = true; table.insert(ways, w) end
            end
        end

        local comp_id = nil
        if t.comp.components then
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
            pcall(function() mp:set_interact_target_id(t.entity_id) end)
            for _, way in ipairs(ways) do
                pcall(function()
                    mp:trigger_active_interact(way, t.entity_id, nil, nil, comp_id)
                end)
            end
            pcall(function() mp:trigger_active_interact() end)
        end
    end

    print("[✔] Auto-Collect COMPLETO finalizado.")
end

local rhythm_enabled = false   -- 不再引用未声明的 rhythm_enabled

-- Auto Perfect Rhythm Game (6-button)
local function toggle_rhythm()
    rhythm_enabled = not rhythm_enabled
    local gm_instrument = package.loaded["hexm.client.debug.gm.gm_commands.gm_instrument"]
    if gm_instrument and gm_instrument.enable_auto_rhythm_game then
        pcall(function()
            gm_instrument.enable_auto_rhythm_game(rhythm_enabled and 1 or 0)
        end)
    end
    return rhythm_enabled
end

-- Instant Win Chess Minigame
local function activate_chess_win()
    local gm_wanfa = package.loaded["hexm.client.debug.gm.gm_commands.gm_wanfa"]
    if gm_wanfa and gm_wanfa.gm_common_chess_fast_win then
        pcall(function() gm_wanfa.gm_common_chess_fast_win(1) end)
    end
end

-- Make Pitch Pot Circle Huge (Easy Mode)
local function activate_pitch_pot_enlargement()
    local gm_wanfa = package.loaded["hexm.client.debug.gm.gm_commands.gm_wanfa"]
    if gm_wanfa and gm_wanfa.gm_scale_pitch_pot_circle then
        pcall(function() gm_wanfa.gm_scale_pitch_pot_circle(CONFIG.PITCH_POT_SCALE) end)
    end
end

local function run_extra_minigames(mode)
    if mode == "rhythm"     then return toggle_rhythm()
    elseif mode == "chess"  then activate_chess_win()
    elseif mode == "pitch"  then activate_pitch_pot_enlargement()
    end
end

-- Botones y UI
local y = 540
local function row(label, func)
    local b = makeButton(label, 210, y)
    bind(b, func)
    y = y - 50
    return b
end

local btn_god = row("Godmode: OFF", function(b)
    toggle_god()
    b:setTitleText("Godmode: " .. (_G.GM_GODMODE and "ON" or "OFF"))
end)

local btn_onehit = row("One-Hit Kill: OFF", function(b)
    local state = toggle_one_hit()
    b:setTitleText("One-Hit Kill: " .. (state and "ON" or "OFF"))
end)

local btn_stamina = row("Stamina: OFF", function(b)
    local state = toggle_stamina()
    b:setTitleText("Stamina: " .. (state and "ON" or "OFF"))
end)

row("GM menu", function() open_new_menu() end)

row("Auto-Collect (Cofres)", function() run_interact_collect() end)

local btn_rhythm = row("NPC Rhythm Game", function(b)
    local state = run_extra_minigames("rhythm")
    b:setTitleText("NPC Rhythm Game: " .. (state and "ON" or "OFF"))
end)

local btn_chess = row("Chess Instant Win", function(b)
    run_extra_minigames("chess")
end)

local btn_pitchpot = row("Pitch Pot Easy Mode", function(b)
    run_extra_minigames("pitch")
end)

local btn_close = makeButton("CLOSE MENU", 210, 40)
bind(btn_close, function()
    pcall(function() panel:removeFromParent() end)
    _G.GM_MENU = nil
end)

btn_god:setTitleText("Godmode: " .. (_G.GM_GODMODE and "ON" or "OFF"))
btn_onehit:setTitleText("One-Hit Kill: " .. (_G.GM_ONEHIT and "ON" or "OFF"))
btn_stamina:setTitleText("Stamina Infinita: " .. (_G.GM_STAMINA and "ON" or "OFF"))
btn_rhythm:setTitleText("NPC Rhythm Game: " .. (rhythm_enabled and "ON" or "OFF"))
return

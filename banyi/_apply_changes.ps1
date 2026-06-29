$ErrorActionPreference = 'Stop'
$path = 'E:\ceshi\banyi\FridaGMTool.NetFx.cs'
$content = [System.IO.File]::ReadAllText($path)
$orig = $content

# --- Item 3: delete "open file" button (3 lines + trailing blank line) ---
$old3 = "            Button btnOpenCoordFile = new Button { Text = `"打开文件`", Location = new Point(listRightX, dgvTopY + listBtnH + 4), Size = new Size(listBtnW, listBtnH), Tag = `"mem`" };`r`n" +
        "            btnOpenCoordFile.Click += (s, ev) => OpenCoordFile();`r`n" +
        "            tabCoord.Controls.Add(btnOpenCoordFile);`r`n" +
        "`r`n"
if ($content.Contains($old3)) {
    $content = $content.Replace($old3, "")
    Write-Host "Item 3: OK"
} else {
    Write-Host "Item 3: NOT FOUND"
}

# --- Item 4: delete "live coord" section header line ---
$old4 = "            addTabSection(tabCoord, `"实时坐标`", yCoord);`r`n"
if ($content.Contains($old4)) {
    $content = $content.Replace($old4, "")
    Write-Host "Item 4: OK"
} else {
    Write-Host "Item 4: NOT FOUND"
}

# --- Item 5: delete live coord label block, keep first 2 lines ---
$old5 = "            int inputY = yCoord[0];`r`n" +
        "            int inputH = 22;`r`n" +
        "            // 实时坐标显示 (替代原 X/Y/Z 输入框, 由定时器自动刷新)`r`n" +
        "            tabCoord.Controls.Add(new Label { Text = `"当前坐标:`", Location = new Point(12, inputY + 3), Size = new Size(64, 20), Tag = `"mem`" });`r`n" +
        "            lblLiveCoord = new Label { Text = `"X=--  Y=--  Z=--`", Location = new Point(80, inputY + 3), Size = new Size(300, 20), ForeColor = Color.Blue, Tag = `"mem`" };`r`n" +
        "            tabCoord.Controls.Add(lblLiveCoord);`r`n" +
        "`r`n" +
        "            inputY += inputH + 4;`r`n"
$new5 = "            int inputY = yCoord[0];`r`n" +
        "            int inputH = 22;`r`n"
if ($content.Contains($old5)) {
    $content = $content.Replace($old5, $new5)
    Write-Host "Item 5: OK"
} else {
    Write-Host "Item 5: NOT FOUND"
}

# --- Item 6: insert lblLiveCoord before "int dgvTopY = yCoord[0];" and change value ---
$old6 = "            int dgvTopY = yCoord[0];`r`n"
$new6 = "            // 实时坐标显示 (放选择文件按钮上方, 只显示 xyz 数值)`r`n" +
        "            lblLiveCoord = new Label { Text = `"X=--  Y=--  Z=--`", Location = new Point(listRightX, yCoord[0]), Size = new Size(listBtnW, 20), ForeColor = Color.Blue, Tag = `"mem`" };`r`n" +
        "            tabCoord.Controls.Add(lblLiveCoord);`r`n" +
        "            int dgvTopY = yCoord[0] + 24;`r`n"
if ($content.Contains($old6)) {
    $content = $content.Replace($old6, $new6)
    Write-Host "Item 6: OK"
} else {
    Write-Host "Item 6: NOT FOUND"
}

# --- Item 7: fix yCoord[0] advance value ---
$old7 = "            yCoord[0] += 116;`r`n"
$new7 = "            yCoord[0] = dgvTopY + 330 + 8;`r`n"
if ($content.Contains($old7)) {
    $content = $content.Replace($old7, $new7)
    Write-Host "Item 7: OK"
} else {
    Write-Host "Item 7: NOT FOUND"
}

# --- Item 8: delete "add current to list" button (3 lines) ---
$old8 = "            Button btnSetCurrent = new Button { Text = `"添加当前到列表`", Location = new Point(12, inputY), Size = new Size(opBtnW, btnH), Tag = `"mem`" };`r`n" +
        "            btnSetCurrent.Click += (s, ev) => SetCustomCurrent();`r`n" +
        "            tabCoord.Controls.Add(btnSetCurrent);`r`n" +
        "`r`n"
if ($content.Contains($old8)) {
    $content = $content.Replace($old8, "")
    Write-Host "Item 8: OK"
} else {
    Write-Host "Item 8: NOT FOUND"
}

# --- Item 9: fix teleport-prev button location and size ---
$old9 = "            Button btnTeleportPrev = new Button { Text = `"← 传送上一条`", Location = new Point(12 + opBtnW + opGap, inputY), Size = new Size(90, btnH), Tag = `"mem`" };"
$new9 = "            Button btnTeleportPrev = new Button { Text = `"← 传送上一条`", Location = new Point(12, inputY), Size = new Size(100, btnH), Tag = `"mem`" };"
if ($content.Contains($old9)) {
    $content = $content.Replace($old9, $new9)
    Write-Host "Item 9: OK"
} else {
    Write-Host "Item 9: NOT FOUND"
}

# --- Item 10: fix teleport-selected button location and size ---
$old10 = "            Button btnTeleportSelected = new Button { Text = `"传送到选中`", Location = new Point(12 + opBtnW + opGap + 90 + 4, inputY), Size = new Size(opBtnW, btnH), Tag = `"mem`" };"
$new10 = "            Button btnTeleportSelected = new Button { Text = `"传送到选中`", Location = new Point(12 + 100 + opGap, inputY), Size = new Size(100, btnH), Tag = `"mem`" };"
if ($content.Contains($old10)) {
    $content = $content.Replace($old10, $new10)
    Write-Host "Item 10: OK"
} else {
    Write-Host "Item 10: NOT FOUND"
}

# --- Item 11: fix teleport-next button location and size ---
$old11 = "            Button btnTeleportNext = new Button { Text = `"传送下一条 →`", Location = new Point(12 + opBtnW + opGap + (90 + 4) * 2, inputY), Size = new Size(90, btnH), Tag = `"mem`" };"
$new11 = "            Button btnTeleportNext = new Button { Text = `"传送下一条 →`", Location = new Point(12 + (100 + opGap) * 2, inputY), Size = new Size(100, btnH), Tag = `"mem`" };"
if ($content.Contains($old11)) {
    $content = $content.Replace($old11, $new11)
    Write-Host "Item 11: OK"
} else {
    Write-Host "Item 11: NOT FOUND"
}

if ($content -ne $orig) {
    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new(0))
    Write-Host "File written."
} else {
    Write-Host "No changes made."
}

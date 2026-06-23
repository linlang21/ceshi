Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$processName = "AAA"
$outputDir = "e:\ceshi\6\analysis"

Write-Host "正在查找进程: $processName"
$processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

if ($processes.Count -eq 0) {
    Write-Host "未找到进程: $processName"
    exit 1
}

$targetProcess = $processes[0]
Write-Host "找到进程 PID: $($targetProcess.Id)"

$automation = [System.Windows.Automation.AutomationElement]
$rootElement = $automation::RootElement

$condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $targetProcess.Id)

Write-Host "正在搜索窗口..."
$windows = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)

if ($windows.Count -eq 0) {
    Write-Host "未找到窗口"
    exit 1
}

Write-Host "找到 $($windows.Count) 个窗口"

function Get-ControlTree {
    param(
        [System.Windows.Automation.AutomationElement]$element,
        [int]$depth = 0
    )
    
    $indent = "  " * $depth
    $name = $element.Current.Name
    $type = $element.Current.ControlType.ProgrammaticName
    $id = $element.Current.AutomationId
    $className = $element.Current.ClassName
    $enabled = $element.Current.IsEnabled
    
    $output = "$indent[$type] Name='$name' ID='$id' Class='$className' Enabled='$enabled'"
    Write-Host $output
    $script:allText += "$output`r`n"
    
    $children = $element.FindAll([System.Windows.Automation.TreeScope]::Children, 
        [System.Windows.Automation.Condition]::TrueCondition)
    
    foreach ($child in $children) {
        Get-ControlTree -element $child -depth ($depth + 1)
    }
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:allText = ""

foreach ($window in $windows) {
    $title = $window.Current.Name
    $className = $window.Current.ClassName
    
    Write-Host "`n========== 窗口 =========="
    Write-Host "标题: $title"
    Write-Host "类名: $className"
    Write-Host "========================`n"
    
    $script:allText += "========== 窗口 ==========`r`n"
    $script:allText += "标题: $title`r`n"
    $script:allText += "类名: $className`r`n"
    $script:allText += "PID: $($targetProcess.Id)`r`n"
    $script:allText += "========================`r`n`r`n"
    
    Get-ControlTree -element $window -depth 0
}

$outputFile = "$outputDir\aaa_ui_tree_$timestamp.txt"
$script:allText | Out-File -FilePath $outputFile -Encoding UTF8

Write-Host "`n捕获完成，输出文件: $outputFile"

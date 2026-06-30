\ = 'E:\ceshi\banyi\FridaGMTool.NetFx.cs'
\ = New-Object System.Text.UTF8Encoding \True
\ = [System.IO.File]::ReadAllText(\, \)

# fix1: add imports after EnumWindowsProc delegate
\ = '        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);'
\ = \ + " 
\ + ' [DllImport(\user32.dll\, SetLastError = true)]' + \
\ + ' static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);' + \
\ + ' [DllImport(\user32.dll\, CharSet = CharSet.Auto, SetLastError = true)]' + \
\ + ' static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);' + \
\ + ' [DllImport(\user32.dll\, SetLastError = true)]' + \
\ + ' static extern IntPtr SetFocus(IntPtr hWnd);' + \
\ + ' [DllImport(\user32.dll\, SetLastError = true)]' + \
\ + ' static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);' + \
\ + ' [DllImport(\kernel32.dll\, SetLastError = true)]' + \
\ + ' static extern uint GetCurrentThreadId();'
if (\.Contains(\) -and -not \.Contains('EnumChildWindows')) { \ = \.Replace(\, \); Write-Output 'fix1 OK' } else { Write-Output 'fix1 SKIP' }

[System.IO.File]::WriteAllText(\, \, \)

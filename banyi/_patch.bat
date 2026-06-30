@echo off
cd /d E:\ceshi\banyi
copy _patch.csx _patch_src.cs >nul
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /out:_patch.exe _patch_src.cs
if errorlevel 1 (
  echo COMPILE FAILED
  exit /b 1
)
_patch.exe
del _patch.exe _patch_src.cs 2>nul

$f = 'E:\ceshi\banyi\FridaGMTool.NetFx.cs'
$c = Get-Content $f -Raw -Encoding UTF8
$c = $c -replace 'Size = new Size\(360, 110\),', 'Size = new Size(360, 330),'
Set-Content $f -Value $c -Encoding UTF8 -NoNewline
Write-Host 'done'

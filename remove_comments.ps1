$files = Get-ChildItem "c:\Users\1\Desktop\פרויקט שיבוץ\OptiLoad\server" -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\" }
Write-Host "Processing $($files.Count) files..."
foreach ($file in $files) {
    $txt = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $txt = [regex]::Replace($txt, '/\*.*?\*/', '', 'Singleline')
    $txt = [regex]::Replace($txt, '(?m)^\s*///.*$', '')
    $txt = [regex]::Replace($txt, '(?m)(?<!https?:)//.*$', '')
    $txt = [regex]::Replace($txt, '(\r?\n\s*){3,}', "`r`n`r`n")
    $txt = $txt.TrimStart()
    [System.IO.File]::WriteAllText($file.FullName, $txt, [System.Text.Encoding]::UTF8)
    Write-Host "  OK: $($file.Name)"
}
Write-Host "Done."

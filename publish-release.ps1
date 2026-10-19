param(
    [string]$OutputDir = "C:\DEV\SERVSIC PV\Release-SERVSIC-PV",
    [switch]$SingleFile
)

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "PUBLICANDO SERVSIC PV EM MODO RELEASE" -ForegroundColor Cyan
Write-Host "Destino: $OutputDir" -ForegroundColor Cyan
Write-Host "SingleFile: $SingleFile" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Write-Host "Limpando pasta de destino..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir -ErrorAction SilentlyContinue
}

$publishArgs = @(
    "publish",
    "ServsicPV\ServsicPV.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-o", $OutputDir,
    "-p:SatelliteResourceLanguages=pt-BR"
)

if ($SingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
    $publishArgs += "-p:EnableCompressionInSingleFile=true"
}

Write-Host "Executando dotnet $($publishArgs -join ' ')..." -ForegroundColor Yellow
& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Erro durante a publicacao!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Limpando pastas de idiomas e arquivos desnecessários..." -ForegroundColor Yellow

# Remove pastas de idiomas (mantém apenas Assets e pt-BR)
$keepFolders = @("Assets", "pt-BR", "pt")
$subDirs = Get-ChildItem -Path $OutputDir -Directory
$removedFoldersCount = 0

foreach ($dir in $subDirs) {
    if ($keepFolders -notcontains $dir.Name) {
        Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removedFoldersCount++
    }
}

# Remove arquivos de debug (.pdb)
$pdbFiles = Get-ChildItem -Path $OutputDir -Filter "*.pdb" -Recurse
$removedPdbCount = $pdbFiles.Count
$pdbFiles | Remove-Item -Force -ErrorAction SilentlyContinue

# Garante que a pasta Assets esteja presente
if ((Test-Path "ServsicPV\Assets") -and !(Test-Path "$OutputDir\Assets")) {
    Copy-Item -Path "ServsicPV\Assets" -Destination "$OutputDir\Assets" -Recurse -Force
}

Write-Host "Pastas de idiomas removidas: $removedFoldersCount" -ForegroundColor Green
Write-Host "Arquivos .pdb removidos: $removedPdbCount" -ForegroundColor Green

$totalFiles = (Get-ChildItem -Path $OutputDir -Recurse -File).Count
$totalDirs = (Get-ChildItem -Path $OutputDir -Directory).Count
$totalSizeMB = [math]::Round(((Get-ChildItem -Path $OutputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB), 2)

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "PUBLICACAO CONCLUIDA COM SUCESSO!" -ForegroundColor Green
Write-Host "Total de arquivos no pacote: $totalFiles" -ForegroundColor White
Write-Host "Tamanho total: $totalSizeMB MB" -ForegroundColor White
Write-Host "Pasta final pronta: $OutputDir" -ForegroundColor White
Write-Host "=========================================" -ForegroundColor Cyan

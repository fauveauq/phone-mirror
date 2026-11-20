<#
.SYNOPSIS
    Construit l'installeur de Phone Mirror (Velopack) : tests, publication autonome, empaquetage.

.DESCRIPTION
    Produit installer\out\PhoneMirror-win-Setup.exe (à transmettre aux collègues) et les paquets
    de mise à jour. Avec -Publish, crée en plus une release GitHub ; avec -SharePath, copie la
    version dans un dossier réseau. Les applications installées la téléchargent alors automatiquement
    (source choisie par « UpdateSource » dans %APPDATA%\PhoneMirror\settings.json, GitHub par défaut).

.EXAMPLE
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -Publish
    .\installer\build-installer.ps1 -SharePath \\serveur\partage\PhoneMirror
#>
param(
    [switch] $Publish,
    [string] $SharePath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root 'publish'
$outDir = Join-Path $PSScriptRoot 'out'
$repoUrl = 'https://github.com/fauveauq/phone-mirror'

function Invoke-Step([string] $title, [scriptblock] $command) {
    Write-Host "==> $title" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "Échec : $title (code $LASTEXITCODE)" }
}

Push-Location $root
try {
    # Dépendances non versionnées (voir README) : sans elles l'application ne fonctionnerait pas.
    foreach ($required in 'third_party\adb\adb.exe', 'third_party\scrcpy-server', 'third_party\ffmpeg\avcodec-63.dll') {
        if (-not (Test-Path $required)) { throw "Fichier manquant : $required. Voir la section Installation du README." }
    }

    $version = ([xml](Get-Content 'PhoneMirror.csproj')).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    Write-Host "Phone Mirror $version" -ForegroundColor Green

    Invoke-Step 'Restauration des outils' { dotnet tool restore }
    Invoke-Step 'Tests' { dotnet test PhoneMirror.slnx -c Release --nologo }

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    Invoke-Step 'Publication autonome (win-x64)' {
        dotnet publish PhoneMirror.csproj -c Release -r win-x64 --self-contained true -o $publishDir --nologo
    }

    # Velopack refuse de reconstruire une version déjà présente dans le dossier de sortie.
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

    if ($Publish) {
        # Récupère les versions déjà publiées pour produire des mises à jour différentielles (plus légères).
        # Pensez à augmenter <Version> dans PhoneMirror.csproj avant chaque publication.
        Invoke-Step 'Téléchargement des versions précédentes' {
            dotnet vpk download github --repoUrl $repoUrl --token (gh auth token) --outputDir $outDir
        }
    }
    if ($SharePath -and (Test-Path $SharePath)) {
        Invoke-Step 'Récupération des versions précédentes (dossier réseau)' {
            dotnet vpk download local --path $SharePath --outputDir $outDir
        }
    }

    Invoke-Step 'Empaquetage Velopack' {
        dotnet vpk pack --packId PhoneMirror --packVersion $version --packTitle 'Phone Mirror' `
            --packAuthors 'Phone Mirror' --packDir $publishDir --mainExe PhoneMirror.exe --outputDir $outDir
    }

    if ($Publish) {
        Invoke-Step 'Publication de la release GitHub' {
            dotnet vpk upload github --repoUrl $repoUrl --token (gh auth token) --outputDir $outDir `
                --publish --releaseName "Phone Mirror $version" --tag "v$version"
        }
    }
    if ($SharePath) {
        Invoke-Step "Copie vers $SharePath" {
            dotnet vpk upload local --path $SharePath --outputDir $outDir
        }
    }

    Write-Host "Installeur : $(Join-Path $outDir 'PhoneMirror-win-Setup.exe')" -ForegroundColor Green
}
finally {
    Pop-Location
}

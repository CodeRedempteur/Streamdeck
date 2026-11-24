# Appelé quand une requête arrive sur /ceres?token=...

# On lance Spotify en tout premier pour qu'il ne passe pas devant
# les fenêtres ouvertes juste après (Ceres.sln, Chrome).
$NFCServerDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $NFCServerDir "SpotifyHelper.ps1")
. (Join-Path $NFCServerDir "WindowUtils.ps1")

Open-SpotifyMinimized

$StartTime = Get-Date

Start-Process "C:\Users\SnifL\Desktop\save\VersionReparerPostGit\BeforeDashboardCeres\Ceres\src\Ceres.sln"

# Attend que Visual Studio s'ouvre puis le passe en plein écran
$vsProcess = Wait-ForProcessWindow -ProcessName "devenv" -TimeoutSeconds 40 -StartedAfter $StartTime
Set-WindowMaximized -Process $vsProcess

# Ouvre claude.ai/new dans une NOUVELLE fenêtre Chrome (même si Chrome est déjà ouvert),
# puis la place sur l'écran de droite.
Open-BrowserWindow -Url "https://claude.ai/new" -ScreenIndex -1 -Maximize | Out-Null

# Lance la lecture de "Titres likés" (Spotify déjà ouvert, on ne le relance pas)
Start-Sleep -Seconds 5
Start-SpotifyLikedSongs -SkipLaunch
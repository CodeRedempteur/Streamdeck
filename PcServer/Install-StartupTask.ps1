<#
    Install-StartupTask.ps1
    Solution plus robuste que RunHidden.vbs : enregistre une tâche planifiée Windows qui lance server.ps1,
    sans aucune fenêtre, à l'ouverture de TA session (nécessaire : le serveur pilote le volume/les touches
    média de la session en cours, donc il doit tourner dedans, pas comme service).

    À exécuter UNE FOIS, depuis ce dossier (clic droit sur ce fichier > Exécuter avec PowerShell, ou :
        powershell -ExecutionPolicy Bypass -File .\Install-StartupTask.ps1
    ). Ne nécessite pas d'être administrateur (la tâche est enregistrée pour l'utilisateur courant).
#>

$ErrorActionPreference = 'Stop'
$ServerPath = Join-Path $PSScriptRoot 'server.ps1'
$TaskName = 'StreamDeck-Server'

if (-not (Test-Path -LiteralPath $ServerPath)) {
    Write-Host "server.ps1 est introuvable à côté de ce script ($PSScriptRoot)." -ForegroundColor Red
    exit 1
}

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$ServerPath`""

$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -Hidden -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal `
    -Description 'Lance le serveur NFC / Stream Deck (server.ps1) en arrière-plan, sans fenêtre, à la connexion.' | Out-Null

Write-Host "Tâche « $TaskName » installée : le serveur démarrera automatiquement, sans fenêtre, à ta prochaine connexion." -ForegroundColor Green
Write-Host "Pour le lancer tout de suite sans redémarrer : Start-ScheduledTask -TaskName '$TaskName'" -ForegroundColor Cyan
Write-Host "Pour le désinstaller : Unregister-ScheduledTask -TaskName '$TaskName'" -ForegroundColor Cyan

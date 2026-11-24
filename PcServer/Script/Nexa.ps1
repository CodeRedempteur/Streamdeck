# Charge les fonctions WindowUtils
$NFCServerDir = Split-Path -Parent $PSScriptRoot
. (Join-Path $NFCServerDir "WindowUtils.ps1")

$NexaflowPath = "C:\Program Files\Smile-forge\Nexaflow\Nexaflow.exe"

$StartTime = Get-Date

# Lance Nexaflow
Start-Process $NexaflowPath

# Attend que la fenêtre soit disponible
$nexaflowProcess = Wait-ForProcessWindow `
    -ProcessName "Nexaflow" `
    -TimeoutSeconds 15 `
    -StartedAfter $StartTime

if ($nexaflowProcess) {

    # Maximise la fenêtre
    Set-WindowMaximized -Process $nexaflowProcess

    # Met Nexaflow tout devant
    Set-WindowForeground -Process $nexaflowProcess
}
# =========================================
#  NFC Server v2
#  - config externe (config.txt) + actions par dossier (Script\*.ps1), comme avant
#  - les scripts sont lancés en arrière-plan : le serveur répond tout de suite et ne se bloque plus
#  - actions Windows intégrées sous /_/ : volume, média, alimentation, bureau
# =========================================
param(
    # Pour les tests : définit les fonctions sans démarrer l'écoute.
    [switch]$NoListen
)

$ScriptDir  = $PSScriptRoot
$ConfigPath = Join-Path $ScriptDir "config.txt"
$ActionsDir = Join-Path $ScriptDir "Script"
$LogsDir    = Join-Path $ScriptDir "logs"
$SchedulesPath = Join-Path $ScriptDir "Schedules.json"
$ServerVersion = 2

. (Join-Path $ScriptDir "NfcNative.ps1")

foreach ($dir in @($ActionsDir, $LogsDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
}

# --- Charger (ou créer) la config ---
if (-not (Test-Path $ConfigPath)) {
    @"
PORT=8765
SECRET=
"@ | Set-Content -Path $ConfigPath -Encoding UTF8
    Write-Host "config.txt créé avec des valeurs par défaut (pense à définir SECRET)."
}

$Config = @{}
Get-Content $ConfigPath | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
        $parts = $line -split "=", 2
        $Config[$parts[0].Trim()] = $parts[1].Trim()
    }
}

$PORT   = if ($Config.ContainsKey("PORT"))   { [int]$Config["PORT"] } else { 8765 }
$SECRET = if ($Config.ContainsKey("SECRET")) { $Config["SECRET"] }   else { "" }
# HOST=+ : toutes les cartes réseau (défaut). Mets une adresse pour restreindre l'écoute.
$BindHost = if ($Config.ContainsKey("HOST") -and $Config["HOST"]) { $Config["HOST"] } else { "+" }

# Scripts lancés : id -> @{ Name; Process; Started }
$script:Jobs = @{}

# ---------------------------------------------------------------
#  Outils
# ---------------------------------------------------------------

# Comparaison du token en temps constant (pas d'indice sur le nombre de caractères justes).
function Test-Token {
    param([string]$Given)

    if ([string]::IsNullOrEmpty($SECRET) -or $null -eq $Given) { return $false }

    $a = [System.Text.Encoding]::UTF8.GetBytes($Given)
    $b = [System.Text.Encoding]::UTF8.GetBytes($SECRET)
    $diff = $a.Length -bxor $b.Length
    $max = [Math]::Max($a.Length, $b.Length)
    for ($i = 0; $i -lt $max; $i++) {
        $x = if ($i -lt $a.Length) { [int]$a[$i] } else { 0 }
        $y = if ($i -lt $b.Length) { [int]$b[$i] } else { 0 }
        $diff = $diff -bor ($x -bxor $y)
    }
    return ($diff -eq 0)
}

function New-Result {
    param([int]$Status = 200, $Body = $null, [string]$Text = $null, [scriptblock]$After = $null)

    if ($null -ne $Body) {
        return [pscustomobject]@{
            Status = $Status; ContentType = "application/json; charset=utf-8"
            Body = (ConvertTo-Json -InputObject $Body -Compress); After = $After
        }
    }
    return [pscustomobject]@{
        Status = $Status; ContentType = "text/plain; charset=utf-8"; Body = $Text; After = $After
    }
}

function Write-Reply {
    param($Response, $Result)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]$Result.Body)
    $Response.StatusCode = $Result.Status
    $Response.ContentType = $Result.ContentType
    $Response.ContentLength64 = $bytes.Length
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
}

function Get-IntQuery {
    param($Request, [string]$Name)

    $raw = $Request.QueryString[$Name]
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }

    $value = 0
    if ([int]::TryParse($raw, [ref]$value)) { return $value }
    throw [System.ArgumentException]::new("Paramètre '$Name' invalide.")
}

# Variables d'un script (Stream Deck Script Builder) : tout paramètre de la requête autre que "token", dont le nom
# est un identifiant PowerShell sûr (lettres/chiffres/_ , commence par une lettre ou _) - le reste est ignoré,
# sans erreur, pour ne jamais bloquer un appel à cause d'un paramètre inattendu ou malformé dans l'URL.
function Get-NfcScriptParams {
    param($Request)

    $params = @{}
    foreach ($key in $Request.QueryString.AllKeys) {
        if ($null -eq $key -or $key -eq "token") { continue }
        if ($key -notmatch '^[a-zA-Z_][a-zA-Z0-9_]*$') { continue }
        $params[$key] = $Request.QueryString[$key]
    }
    return $params
}

# ---------------------------------------------------------------
#  Scripts du dossier Script (lancés en arrière-plan)
# ---------------------------------------------------------------

function Remove-OldJobs {
    $now = Get-Date
    foreach ($id in @($script:Jobs.Keys)) {
        $job = $script:Jobs[$id]
        if ($job.Process.HasExited -and ($now - $job.Started).TotalMinutes -gt 10) {
            $script:Jobs.Remove($id)
        }
    }
}

# Lance le script dans son propre PowerShell (caché), sa sortie va dans logs\. Renvoie l'identifiant de suivi.
# $Params (variables Stream Deck Script Builder, ex : {season}/{episode}) est transmis au script par "splatting"
# (& script @params) : le script les reçoit dans son propre bloc param(), comme des arguments nommés normaux.
function Start-NfcScript {
    param([string]$Name, [string]$ScriptPath, [hashtable]$Params = @{})

    Remove-OldJobs

    $id = [guid]::NewGuid().ToString("N").Substring(0, 12)
    $safeName = $Name -replace '[^\p{L}\p{N}_-]', '_'
    $logFile = Join-Path $LogsDir ("{0}-{1:yyyyMMdd-HHmmss}.log" -f $safeName, (Get-Date))

    # Les apostrophes sont doublées : chemins avec espaces ou apostrophes acceptés. Idem pour chaque valeur de
    # $Params (venue de l'URL, donc non fiable) : jamais interpolée telle quelle dans la commande.
    $s = $ScriptPath.Replace("'", "''")
    $l = $logFile.Replace("'", "''")

    $paramsLiteral = "@{}"
    if ($Params.Count -gt 0) {
        $pairs = foreach ($key in $Params.Keys) {
            $k = $key.Replace("'", "''")
            $v = ([string]$Params[$key]).Replace("'", "''")
            "'$k' = '$v'"
        }
        $paramsLiteral = "@{" + ($pairs -join "; ") + "}"
    }

    $command = "`$__p = $paramsLiteral; try { & '$s' @__p *>&1 | Out-File -LiteralPath '$l' -Encoding utf8 } catch { `$_ | Out-File -LiteralPath '$l' -Encoding utf8 -Append; exit 1 }"
    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))

    $psExe = (Get-Process -Id $PID).Path
    $startArgs = @{
        FilePath     = $psExe
        PassThru     = $true
        ArgumentList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encoded)
    }
    if ($script:NfcIsWindows) { $startArgs.WindowStyle = "Hidden" }   # option Windows uniquement
    $process = Start-Process @startArgs
    $null = $process.Handle   # garde le code de sortie disponible

    $script:Jobs[$id] = @{ Name = $Name; Process = $process; Started = Get-Date }
    return $id
}

# Comme Start-NfcScript, mais relance le même script plusieurs fois de suite (avec une attente entre chaque),
# en un seul job suivi : utilisé par les planifications (Stream Deck Script Builder) qui répètent une macro par
# exemple. Repeat=1 revient exactement à Start-NfcScript (juste avec un nom de job "schedule:...").
function Start-NfcScheduledRun {
    param([string]$Name, [string]$ScriptPath, [int]$Repeat = 1, [int]$DelaySeconds = 0)

    Remove-OldJobs

    $id = [guid]::NewGuid().ToString("N").Substring(0, 12)
    $safeName = $Name -replace '[^\p{L}\p{N}_-]', '_'
    $logFile = Join-Path $LogsDir ("{0}-{1:yyyyMMdd-HHmmss}-schedule.log" -f $safeName, (Get-Date))

    $s = $ScriptPath.Replace("'", "''")
    $l = $logFile.Replace("'", "''")
    $repeat = [Math]::Max(1, $Repeat)
    $delay = [Math]::Max(0, $DelaySeconds)

    $command = @"
`$__log = '$l'
for (`$__i = 1; `$__i -le $repeat; `$__i++) {
    try { & '$s' *>&1 | Out-File -LiteralPath `$__log -Encoding utf8 -Append } catch { `$_ | Out-File -LiteralPath `$__log -Encoding utf8 -Append }
    if (`$__i -lt $repeat) { Start-Sleep -Seconds $delay }
}
"@
    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))

    $psExe = (Get-Process -Id $PID).Path
    $startArgs = @{
        FilePath     = $psExe
        PassThru     = $true
        ArgumentList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encoded)
    }
    if ($script:NfcIsWindows) { $startArgs.WindowStyle = "Hidden" }
    $process = Start-Process @startArgs
    $null = $process.Handle

    $script:Jobs[$id] = @{ Name = "schedule:$Name"; Process = $process; Started = Get-Date }
    return $id
}

# ---------------------------------------------------------------
#  Planification (Stream Deck Script Builder) : Schedules.json, relu à chaque vérification
# ---------------------------------------------------------------

# Toutes les règles actuelles (liste vide si le fichier n'existe pas encore ou est illisible - jamais une erreur qui
# arrêterait le serveur). Relu depuis le disque à chaque appel : l'application peut modifier ce fichier à tout moment
# pendant que le serveur tourne, sans qu'il faille le redémarrer.
function Get-NfcSchedules {
    if (-not (Test-Path -LiteralPath $SchedulesPath -PathType Leaf)) { return @() }
    try {
        $data = Get-Content -LiteralPath $SchedulesPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($null -eq $data -or $null -eq $data.schedules) { return @() }
        return @($data.schedules)
    }
    catch {
        Write-Host "Schedules.json illisible pour l'instant (ignoré) : $($_.Exception.Message)"
        return @()
    }
}

# Enregistre l'heure du dernier déclenchement d'UNE règle (par son id), sans toucher aux autres — relit puis réécrit
# tout le fichier (comme l'application), avec le même remplacement par un fichier temporaire.
function Set-NfcScheduleLastFired {
    param([string]$Id, [datetime]$WhenUtc)

    if (-not (Test-Path -LiteralPath $SchedulesPath -PathType Leaf)) { return }
    try {
        $data = Get-Content -LiteralPath $SchedulesPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($null -eq $data -or $null -eq $data.schedules) { return }

        foreach ($rule in $data.schedules) {
            if ($rule.id -eq $Id) { $rule.lastFiredUtc = $WhenUtc.ToString("o") }
        }

        $tmp = "$SchedulesPath.tmp"
        ($data | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $tmp -Encoding utf8
        Move-Item -LiteralPath $tmp -Destination $SchedulesPath -Force
    }
    catch {
        Write-Host "Impossible d'enregistrer le dernier déclenchement d'une planification : $($_.Exception.Message)"
    }
}

# Vérifie chaque règle active et déclenche celles qui sont dues. Appelée périodiquement par la boucle principale
# (voir Start-NfcServer), entre deux requêtes HTTP — jamais bloquante, jamais plus fréquente que le délai d'attente
# de cette boucle (par défaut 15 s : largement assez pour une planification en minutes).
function Invoke-NfcScheduleCheck {
    $now = Get-Date
    $disabledPath = Join-Path $ActionsDir ".sdb-disabled.txt"
    $disabledNames = if (Test-Path -LiteralPath $disabledPath) { @(Get-Content -LiteralPath $disabledPath -ErrorAction SilentlyContinue) } else { @() }

    foreach ($rule in (Get-NfcSchedules)) {
        try {
            if (-not $rule.enabled) { continue }

            $name = [string]$rule.scriptName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }

            $lastFiredUtc = [datetime]::MinValue
            if ($rule.lastFiredUtc) { try { $lastFiredUtc = [datetime]$rule.lastFiredUtc } catch { } }
            $sinceLast = (Get-Date).ToUniversalTime() - $lastFiredUtc

            $due = $false
            if ($rule.mode -eq 'DailyTimes') {
                $hhmm = $now.ToString("HH:mm")
                # Garde-fou de 90 s : la même minute peut être revue par plusieurs vérifications d'affilée
                # (le délai d'attente de la boucle n'est pas forcément un diviseur exact de 60 s).
                if (($rule.times -contains $hhmm) -and $sinceLast.TotalSeconds -gt 90) { $due = $true }
            }
            else {
                $inWindow = $true
                if ($rule.activeFrom -and $rule.activeTo) {
                    $hhmm = $now.ToString("HH:mm")
                    $inWindow = ($hhmm -ge [string]$rule.activeFrom) -and ($hhmm -le [string]$rule.activeTo)
                }
                $minutes = [Math]::Max(1, [int]$rule.intervalMinutes)
                if ($inWindow -and $sinceLast.TotalMinutes -ge $minutes) { $due = $true }
            }

            if (-not $due) { continue }

            # Résolution du script (mêmes règles que pour un appel normal : à plat, sinon cherché dans les sous-dossiers).
            $scriptPath = Join-Path $ActionsDir "$name.ps1"
            $found = Test-Path -LiteralPath $scriptPath -PathType Leaf
            if (-not $found) {
                $match = Get-ChildItem -LiteralPath $ActionsDir -Filter "$name.ps1" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($match) { $scriptPath = $match.FullName; $found = $true }
            }

            if ($found -and ($disabledNames -notcontains $name)) {
                $jobId = Start-NfcScheduledRun -Name $name -ScriptPath $scriptPath -Repeat ([int]$rule.repeat) -DelaySeconds ([int]$rule.repeatDelaySeconds)
                Write-Host "$(Get-Date -Format 'HH:mm:ss') -> planification : '$name' déclenché (tâche $jobId)"
            }
            else {
                Write-Host "$(Get-Date -Format 'HH:mm:ss') -> planification : '$name' introuvable ou désactivé, ignoré"
            }

            Set-NfcScheduleLastFired -Id ([string]$rule.id) -WhenUtc (Get-Date).ToUniversalTime()
        }
        catch {
            Write-Host "Erreur de planification (règle $($rule.id)) : $($_.Exception.Message)"
        }
    }
}

# ---------------------------------------------------------------
#  Affichage plein écran sur le PC : heure, date, jour ou image (fenêtre WPF, son propre PowerShell caché)
# ---------------------------------------------------------------

# Lance une fenêtre plein écran (noire, sans bordure, toujours au premier plan) dans son propre PowerShell caché,
# comme Start-NfcScript. Ferme avec la croix en haut à droite, la touche Échap, ou toute seule après DurationSeconds
# (0 = infini). $Kind : clock, date, dayofweek ou image. $ImageSource (kind=image) : lien http(s) ou chemin local.
function Start-NfcInfoDisplay {
    param([string]$Kind, [string]$ImageSource, [int]$DurationSeconds)

    Remove-OldJobs

    $id  = [guid]::NewGuid().ToString("N").Substring(0, 12)
    $k   = $Kind.Replace("'", "''")
    $src = ($(if ($ImageSource) { $ImageSource } else { "" })).Replace("'", "''")
    $dur = [Math]::Max(0, $DurationSeconds)

    # Ce bloc tourne dans SON PROPRE PowerShell (fenêtre cachée) : c'est lui qui affiche la fenêtre plein écran et
    # se termine quand elle se ferme. $__Kind / $__ImageSource / $__DurationSeconds sont substitués juste avant
    # (valeurs déjà échappées pour tenir dans des chaînes entre apostrophes).
    $body = @'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$fr = [System.Globalization.CultureInfo]::GetCultureInfo("fr-FR")
$window = New-Object System.Windows.Window
$window.WindowState = "Maximized"
$window.WindowStyle = "None"
$window.Topmost     = $true
$window.Background  = [System.Windows.Media.Brushes]::Black
$window.ResizeMode  = "NoResize"
$window.Cursor      = [System.Windows.Input.Cursors]::None

$grid = New-Object System.Windows.Controls.Grid
$window.Content = $grid
$tick = $null

if ($__Kind -eq 'image') {
    $img = New-Object System.Windows.Controls.Image
    $img.Stretch = "Uniform"
    try {
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
        $bmp.BeginInit()
        if ($__ImageSource -match '^https?://') {
            $tmp = Join-Path $env:TEMP ("sdinfo-{0}.tmp" -f ([guid]::NewGuid().ToString("N")))
            Invoke-WebRequest -Uri $__ImageSource -OutFile $tmp -UseBasicParsing -TimeoutSec 20
            $bmp.UriSource = New-Object System.Uri($tmp, [System.UriKind]::Absolute)
        } else {
            $bmp.UriSource = New-Object System.Uri($__ImageSource, [System.UriKind]::Absolute)
        }
        $bmp.CacheOption = "OnLoad"
        $bmp.EndInit()
        $img.Source = $bmp
    } catch {
        $err = New-Object System.Windows.Controls.TextBlock
        $err.Text = "Image introuvable : $($_.Exception.Message)"
        $err.Foreground = [System.Windows.Media.Brushes]::White
        $err.FontSize = 28
        $err.TextWrapping = "Wrap"
        $err.MaxWidth = 900
        $err.HorizontalAlignment = "Center"
        $err.VerticalAlignment = "Center"
        $grid.Children.Add($err) | Out-Null
    }
    $grid.Children.Add($img) | Out-Null
} else {
    $text = New-Object System.Windows.Controls.TextBlock
    $text.Foreground = [System.Windows.Media.Brushes]::White
    $text.FontSize = 160
    $text.FontWeight = "Bold"
    $text.HorizontalAlignment = "Center"
    $text.VerticalAlignment = "Center"
    $grid.Children.Add($text) | Out-Null

    $refresh = {
        $now = Get-Date
        switch ($__Kind) {
            'date'      { $text.Text = (Get-Culture -Name "fr-FR").TextInfo.ToTitleCase($now.ToString("dddd d MMMM yyyy", $fr)) }
            'dayofweek' { $text.Text = (Get-Culture -Name "fr-FR").TextInfo.ToTitleCase($now.ToString("dddd", $fr)) }
            default     { $text.Text = $now.ToString("HH:mm:ss") }
        }
    }
    & $refresh
    $tick = New-Object System.Windows.Threading.DispatcherTimer
    $tick.Interval = [TimeSpan]::FromSeconds(1)
    $tick.Add_Tick($refresh)
    $tick.Start()
}

$close = New-Object System.Windows.Controls.Button
$close.Content = [char]0x2715
$close.FontSize = 28
$close.Width = 56
$close.Height = 56
$close.HorizontalAlignment = "Right"
$close.VerticalAlignment = "Top"
$close.Margin = "0,24,32,0"
$close.Background = [System.Windows.Media.Brushes]::Transparent
$close.Foreground = [System.Windows.Media.Brushes]::White
$close.BorderThickness = [System.Windows.Thickness]::new(0)
$close.Cursor = [System.Windows.Input.Cursors]::Hand
$close.Add_Click({ $window.Close() })
$grid.Children.Add($close) | Out-Null

$window.Add_KeyDown({ if ($_.Key -eq 'Escape') { $window.Close() } })
if ($tick) { $window.Add_Closed({ $tick.Stop() }) }

if ($__DurationSeconds -gt 0) {
    $auto = New-Object System.Windows.Threading.DispatcherTimer
    $auto.Interval = [TimeSpan]::FromSeconds($__DurationSeconds)
    $auto.Add_Tick({ $auto.Stop(); $window.Close() })
    $auto.Start()
}

$null = $window.ShowDialog()
'@

    $command = "`$__Kind = '$k'; `$__ImageSource = '$src'; `$__DurationSeconds = $dur; $body"
    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))

    $psExe = (Get-Process -Id $PID).Path
    $startArgs = @{
        FilePath     = $psExe
        PassThru     = $true
        ArgumentList = @("-NoProfile", "-STA", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encoded)
    }
    if ($script:NfcIsWindows) { $startArgs.WindowStyle = "Hidden" }
    $process = Start-Process @startArgs
    $null = $process.Handle

    $script:Jobs[$id] = @{ Name = "info-$Kind"; Process = $process; Started = Get-Date }
    return $id
}

# ---------------------------------------------------------------
#  Actions intégrées : /_/<commande>
# ---------------------------------------------------------------

function Invoke-NfcBuiltIn {
    param([string]$Command, $Request)

    switch ($Command) {

        'ping' {
            return New-Result -Body @{ ok = $true; name = [Environment]::MachineName; version = $ServerVersion }
        }

        'audio' {
            $level = Get-IntQuery $Request 'level'
            $delta = Get-IntQuery $Request 'delta'
            $mute  = $Request.QueryString['mute']

            if ($null -ne $level) {
                Set-NfcVolume -Level $level
                if ($level -gt 0) { Set-NfcMute -Muted $false }
            }
            elseif ($null -ne $delta) {
                Step-NfcVolume -Delta $delta
                if ($delta -gt 0) { Set-NfcMute -Muted $false }
            }

            if ($mute) {
                switch ($mute.ToLowerInvariant()) {
                    'on'     { Set-NfcMute -Muted $true }
                    'off'    { Set-NfcMute -Muted $false }
                    'toggle' { Set-NfcMute -Muted (-not (Get-NfcAudio).muted) }
                    default  { return New-Result -Status 400 -Text "mute: on, off ou toggle" }
                }
            }

            return New-Result -Body (Get-NfcAudio)
        }

        'media' {
            $cmd = $Request.QueryString['cmd']
            if ($cmd -notin @('playpause', 'next', 'prev', 'stop')) {
                return New-Result -Status 400 -Text "cmd: playpause, next, prev ou stop"
            }
            Send-NfcMediaKey -Key $cmd
            return New-Result -Body @{ ok = $true; cmd = $cmd }
        }

        'desktop' {
            $cmd = $Request.QueryString['cmd']
            if ($cmd -notin @('show', 'taskview')) {
                return New-Result -Status 400 -Text "cmd: show ou taskview"
            }
            Invoke-NfcDesktop -Command $cmd
            return New-Result -Body @{ ok = $true; cmd = $cmd }
        }

        'power' {
            $cmd = $Request.QueryString['cmd']
            $delay = Get-IntQuery $Request 'delay'
            if ($null -eq $delay) { $delay = 5 }

            switch ($cmd) {
                { $_ -in @('shutdown', 'restart', 'abort') } {
                    Invoke-NfcShutdown -Mode $cmd -DelaySeconds $delay
                    return New-Result -Body @{ ok = $true; cmd = $cmd; delay = $delay }
                }
                # Ces actions se font APRÈS l'envoi de la réponse (la veille ne rend la main qu'au réveil).
                'sleep'      { return New-Result -Body @{ ok = $true; cmd = $cmd } -After { Start-Sleep -Milliseconds 500; Invoke-NfcSleep } }
                'hibernate'  { return New-Result -Body @{ ok = $true; cmd = $cmd } -After { Start-Sleep -Milliseconds 500; Invoke-NfcHibernate } }
                'lock'       { return New-Result -Body @{ ok = $true; cmd = $cmd } -After { Invoke-NfcLock } }
                'monitoroff' { return New-Result -Body @{ ok = $true; cmd = $cmd } -After { Start-Sleep -Milliseconds 500; Invoke-NfcMonitorOff } }
                default      { return New-Result -Status 400 -Text "cmd: shutdown, restart, sleep, hibernate, lock, monitoroff ou abort" }
            }
        }

        'info' {
            $kind = $Request.QueryString['kind']
            if ($kind -notin @('clock', 'date', 'dayofweek', 'image')) {
                return New-Result -Status 400 -Text "kind: clock, date, dayofweek ou image"
            }

            $duration = Get-IntQuery $Request 'duration'
            if ($null -eq $duration) { $duration = 0 }

            $image = $Request.QueryString['image']
            if ($kind -eq 'image' -and [string]::IsNullOrWhiteSpace($image)) {
                return New-Result -Status 400 -Text "image: lien (https://…) ou chemin de fichier requis pour kind=image"
            }

            $jobId = Start-NfcInfoDisplay -Kind $kind -ImageSource $image -DurationSeconds $duration
            return New-Result -Body @{ ok = $true; kind = $kind; job = $jobId }
        }

        'job' {
            $id = $Request.QueryString['id']
            if (-not $id -or -not $script:Jobs.ContainsKey($id)) {
                return New-Result -Body @{ state = "unknown" }
            }

            $process = $script:Jobs[$id].Process
            if ($process.HasExited) {
                return New-Result -Body @{ state = "done"; exitCode = $process.ExitCode }
            }
            return New-Result -Body @{ state = "running" }
        }

        default {
            return New-Result -Status 404 -Text "Unknown command"
        }
    }
}

# ---------------------------------------------------------------
#  Traitement d'une requête. Renvoie un bloc à exécuter après la réponse (ou $null).
# ---------------------------------------------------------------

function Invoke-NfcRequest {
    param($Context)

    $request  = $Context.Request
    $response = $Context.Response
    $after = $null

    try {
        $path = $request.Url.AbsolutePath
        Write-Host "$(Get-Date -Format 'HH:mm:ss') -> $path"

        # --- Vérification du secret ---
        if (-not (Test-Token $request.QueryString["token"])) {
            Write-Reply $response (New-Result -Status 401 -Text "Unauthorized")
            Write-Host "  Refusé (token invalide)"
            return $null
        }

        $name = [Uri]::UnescapeDataString($path).Trim("/")

        # --- Actions Windows intégrées : /_/commande ---
        if ($name -like "_/*") {
            $command = $name.Substring(2)
            try {
                $result = Invoke-NfcBuiltIn -Command $command -Request $request
            }
            catch [System.ArgumentException] {
                $result = New-Result -Status 400 -Text $_.Exception.Message
            }
            catch {
                $result = New-Result -Status 500 -Text "Erreur: $($_.Exception.Message)"
                Write-Host "  Erreur dans '_/$command' : $($_.Exception.Message)"
            }

            Write-Reply $response $result
            $after = $result.After
            return $after
        }

        # --- Script du dossier Script : "/ceres" -> Script\ceres.ps1 ---
        # Lettres, chiffres, espace, _ et - seulement (sous-dossiers avec /) : pas de "..", pas de chemin absolu.
        $validName = $name -match '^[\p{L}\p{N}_ -]+(/[\p{L}\p{N}_ -]+)*$'
        $actionScript = if ($validName) { Join-Path $ActionsDir "$name.ps1" } else { $null }

        $inside = $false
        if ($actionScript) {
            $root = [System.IO.Path]::GetFullPath($ActionsDir).TrimEnd([char]'\', [char]'/') + [System.IO.Path]::DirectorySeparatorChar
            $inside = [System.IO.Path]::GetFullPath($actionScript).StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)
        }

        # Filet de sécurité : un nom simple (sans "/") pas trouvé directement est aussi cherché dans les sous-dossiers de
        # Script (ex : un script rangé dans un dossier par Stream Deck Script Builder). Le premier trouvé est utilisé.
        if ($validName -and $name -notmatch '/' -and -not (Test-Path -LiteralPath $actionScript -PathType Leaf)) {
            $found = Get-ChildItem -LiteralPath $ActionsDir -Filter "$name.ps1" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { $actionScript = $found.FullName; $inside = $true }
        }

        # Script désactivé (Stream Deck Script Builder) : liste à plat, un nom par ligne, dans Script\.sdb-disabled.txt.
        $disabledPath = Join-Path $ActionsDir ".sdb-disabled.txt"
        $disabled = $false
        if ($inside -and (Test-Path -LiteralPath $disabledPath -PathType Leaf)) {
            $disabledNames = @(Get-Content -LiteralPath $disabledPath -ErrorAction SilentlyContinue)
            $shortName = [System.IO.Path]::GetFileNameWithoutExtension($actionScript)
            if ($disabledNames -contains $shortName) { $disabled = $true }
        }

        if ($disabled) {
            Write-Reply $response (New-Result -Status 423 -Text "'$name' est désactivé (Stream Deck Script Builder)")
            Write-Host "  '$name' refusé : désactivé"
        }
        elseif ($inside -and (Test-Path -LiteralPath $actionScript -PathType Leaf)) {
            try {
                $scriptParams = Get-NfcScriptParams $request
                $id = Start-NfcScript -Name $name -ScriptPath $actionScript -Params $scriptParams
                Write-Reply $response (New-Result -Body @{ ok = $true; name = $name; job = $id })
                Write-Host "  '$name' lancé (tâche $id)"
            }
            catch {
                Write-Reply $response (New-Result -Status 500 -Text "Erreur: $($_.Exception.Message)")
                Write-Host "  Erreur au lancement de '$name' : $($_.Exception.Message)"
            }
        }
        else {
            Write-Reply $response (New-Result -Status 404 -Text "Unknown command")
            Write-Host "  Commande inconnue : '$name'"
        }
    }
    catch {
        Write-Host "Erreur : $($_.Exception.Message)"
    }
    finally {
        try { $response.Close() } catch { }
    }

    return $after
}

function Start-NfcServer {
    if ([string]::IsNullOrEmpty($SECRET)) {
        Write-Host "SECRET est vide dans $ConfigPath : le serveur refuse de démarrer sans token." -ForegroundColor Red
        return
    }

    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add("http://${BindHost}:$PORT/")

    try {
        $listener.Start()
    }
    catch {
        Write-Host "Impossible d'écouter sur le port $PORT : $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Lance PowerShell en administrateur, ou autorise l'adresse une fois :"
        Write-Host "  netsh http add urlacl url=http://+:$PORT/ user=$env:USERNAME"
        return
    }

    Write-Host "================================="
    Write-Host " NFC Server v$ServerVersion"
    Write-Host " Port      : $PORT"
    Write-Host " Actions   : $ActionsDir"
    Write-Host " Journaux  : $LogsDir"
    Write-Host " Config    : $ConfigPath"
    Write-Host "================================="
    Write-Host "En attente d'une commande..."
    Write-Host ""

    # Intervalle entre deux vérifications des planifications pendant qu'aucune requête n'arrive (millisecondes).
    # Pas trop court (inutile : les planifications se comptent en minutes) ni trop long (un déclenchement peut
    # attendre jusqu'à ce délai avant d'être vu).
    $scheduleCheckMs = 15000

    while ($listener.IsListening) {
        $context = $null
        $after = $null

        try {
            # BeginGetContext + attente avec délai (au lieu de GetContext(), bloquant) : permet de vérifier les
            # planifications à intervalles réguliers même quand aucune requête HTTP n'arrive. EndGetContext n'est
            # appelé qu'une fois le signal reçu (une requête est bien arrivée) — jamais pendant qu'on attend encore.
            $asyncResult = $listener.BeginGetContext($null, $null)
            while (-not $asyncResult.AsyncWaitHandle.WaitOne($scheduleCheckMs)) {
                try { Invoke-NfcScheduleCheck } catch { Write-Host "Erreur de planification : $($_.Exception.Message)" }
            }
            $context = $listener.EndGetContext($asyncResult)
            $after = Invoke-NfcRequest $context
        }
        catch {
            Write-Host "Erreur : $($_.Exception.Message)"
            try { $context.Response.Abort() } catch { }
        }

        if ($after) {
            try { & $after } catch { Write-Host "Erreur (après réponse) : $($_.Exception.Message)" }
        }
    }
}

if (-not $NoListen) { Start-NfcServer }

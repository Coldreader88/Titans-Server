# Checks the setup, then starts every Titans server in order, each in its own window.
# Run it from Bin\ (every build copies it there), or double-click Start-Servers.bat.
#
#   Start-Servers.bat              check, then start Lobby, Chat, Earth, Space and Login
#   Start-Servers.bat -NoLogin     the same without the Login-Server (launcher status)
#   Start-Servers.bat -CheckOnly   only check, start nothing
#
# The checks: the server programs are there, the three configs point at the same database, the database
# accepts that login and has the tables, the chat passwords and ports match, and no port is taken.
param(
    [switch]$NoLogin,
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$bin = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $bin

$problems = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Ok($text) { Write-Host "  ok    $text" -ForegroundColor Green }
function Bad($text) { $script:problems.Add($text); Write-Host "  FAIL  $text" -ForegroundColor Red }
function Warn($text) { $script:warnings.Add($text); Write-Host "  note  $text" -ForegroundColor Yellow }

function Read-Config($name) {
    $path = Join-Path $bin "Config\$name"
    if (-not (Test-Path $path)) {
        Bad "Config\$name is missing (build the solution: it copies ConfigFiles\ to Bin\Config)"
        return $null
    }
    try {
        return ([xml](Get-Content -Raw -Encoding UTF8 $path)).DocumentElement
    } catch {
        Bad "Config\${name} is not valid XML: $($_.Exception.Message)"
        return $null
    }
}

function Text($node, $default) {
    if ($node -eq $null) { return $default }
    $value = if ($node -is [string]) { $node } else { $node.InnerText }
    if ($value -eq $null) { return $default }
    return $value.Trim()
}

# Whether something answers on the port (a server is listening there).
function Test-Port($port, $timeoutMs = 300) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $wait = $client.BeginConnect('127.0.0.1', [int]$port, $null, $null)
        if ($wait.AsyncWaitHandle.WaitOne($timeoutMs) -and $client.Connected) {
            $client.EndConnect($wait)
            return $true
        }
        return $false
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

function Wait-Port($port, $seconds) {
    $until = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $until) {
        if (Test-Port $port) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

Write-Host ""
Write-Host "Titans-Server: checking the setup in $bin"
Write-Host ""

# --- Programs -----------------------------------------------------------------------------------------
$exes = @('Lobby-Server.exe', 'Cms-Server.exe', 'Game-Server.exe')
if (-not $NoLogin) { $exes += 'Login-Server.exe' }
foreach ($exe in $exes) {
    if (Test-Path (Join-Path $bin $exe)) { Ok $exe } else { Bad "$exe is missing (build Titans-UCGO.sln in Visual Studio)" }
}
if (-not (Test-Path (Join-Path $bin 'DB\Encryption\XORTable.dat'))) { Bad "DB\Encryption\XORTable.dat is missing (the build copies DB\ to Bin\DB)" }

# --- Configs ------------------------------------------------------------------------------------------
$lobby = Read-Config 'LobbyServer.xml'
$game = Read-Config 'GameServer.xml'
$chat = Read-Config 'CMSServer.xml'
$login = if ($NoLogin) { $null } else { Read-Config 'LoginServer.xml' }

$lobbyPort = [int](Text $lobby.Port 42018)
$chatPort = [int](Text $chat.Port 42016)
$linkPort = [int](Text $chat.GameLinkPort 10241)
$gamePort = [int](Text $game.Port 42010)
$spacePort = $gamePort + 1
$loginPort = [int](Text $login.Port 42012)

# The servers share one database (the Login-Server reads the others' state from it for its status).
$db = @{}
foreach ($pair in @(@('LobbyServer.xml', $lobby), @('GameServer.xml', $game), @('CMSServer.xml', $chat), @('LoginServer.xml', $login))) {
    $node = if ($pair[1] -ne $null) { $pair[1].Database } else { $null }
    if ($node -eq $null) { continue }
    $db[$pair[0]] = [ordered]@{
        Host = Text $node.Host '127.0.0.1'; Port = Text $node.Port '3306'; Name = Text $node.Name 'titans-server'
        User = Text $node.User 'root'; Password = Text $node.Password ''
    }
}
$first = $db['LobbyServer.xml']
if ($first -ne $null) {
    $same = $true
    foreach ($key in $db.Keys) {
        foreach ($field in @('Host', 'Port', 'Name', 'User', 'Password')) {
            if ($db[$key][$field] -ne $first[$field]) {
                Bad "$key <Database><$field> differs from LobbyServer.xml (all servers must use the same database)"
                $same = $false
            }
        }
    }
    if ($same) { Ok "the configs use the same database: $($first.User)@$($first.Host):$($first.Port)/$($first.Name)" }
}

# The game servers log in to the chat server's game link.
if ($game -ne $null -and $chat -ne $null) {
    $chatPassword = Text $game.ChatPassword ''
    $linkPassword = Text $chat.GameLinkPassword ''
    if ($chatPassword -ne $linkPassword) { Bad "GameServer.xml ChatPassword does not match CMSServer.xml GameLinkPassword (GM commands will not reach the game)" }
    elseif ($chatPassword -eq '') { Warn "the chat game link is off (ChatPassword is empty): GM commands and teams will not reach the game" }
    else { Ok "the chat link passwords match" }
    $chatLinkPort = [int](Text $game.ChatPort 10241)
    if ($chatLinkPort -ne $linkPort) { Bad "GameServer.xml ChatPort ($chatLinkPort) is not CMSServer.xml GameLinkPort ($linkPort)" } else { Ok "the chat link port matches ($linkPort)" }
}

# The Lobby sends players to the game server's port (Space: port + 1).
if ($lobby -ne $null) {
    $handoff = [int](Text $lobby.GameServerPort 42010)
    if ($handoff -ne $gamePort) { Bad "LobbyServer.xml GameServerPort ($handoff) is not GameServer.xml Port ($gamePort)" } else { Ok "the Lobby hands players to port $gamePort (Space: $spacePort)" }
    $address = Text $lobby.GameServerIP '127.0.0.1'
    $transfer = Text $game.TransferHost '127.0.0.1'
    if ($address -ne $transfer) { Warn "LobbyServer.xml GameServerIP ($address) and GameServer.xml TransferHost ($transfer) differ; both should be the address players use" }
    if ($address -eq '127.0.0.1') { Warn "GameServerIP is 127.0.0.1: only a client on this PC can play. Put this PC's address there for others." }
}

# --- Database -----------------------------------------------------------------------------------------
if ($first -ne $null) {
    $driver = Join-Path $bin 'MySql.Data.dll'
    $checked = $false
    if (Test-Path $driver) {
        try {
            Add-Type -Path $driver
            $connection = New-Object MySql.Data.MySqlClient.MySqlConnection(
                "Server=$($first.Host);Port=$($first.Port);Database=$($first.Name);Uid=$($first.User);Pwd=$($first.Password);SslMode=None;")
            try {
                $connection.Open()
                Ok "logged in to the database"
                $command = $connection.CreateCommand()
                $command.CommandText = "SHOW TABLES"
                $reader = $command.ExecuteReader()
                $tables = @()
                while ($reader.Read()) { $tables += $reader.GetString(0) }
                $reader.Close()
                $missing = @('accounts', 'characters', 'login_sessions', 'team', 'friends', 'tele_bookmark', 'ground_items') | Where-Object { $tables -notcontains $_ }
                if ($missing.Count -gt 0) { Bad ("tables missing: " + ($missing -join ', ') + " (load DB\SQL\accounts.sql, characters.sql, cms.sql, tele_bookmark.sql and world.sql)") }
                else { Ok "the tables are there" }
                if ($tables -contains 'accounts') {
                    $command.CommandText = "SELECT COUNT(*) FROM accounts"
                    $accounts = [int]$command.ExecuteScalar()
                    if ($accounts -eq 0) { Warn "there are no accounts yet: make one on the Lobby console with: account <name> <password> [level]" }
                    else { Ok "$accounts account(s)" }
                }
                $checked = $true
            } finally {
                $connection.Close()
            }
        } catch {
            $message = $_.Exception.Message
            if ($_.Exception.InnerException -ne $null) { $message = $_.Exception.InnerException.Message }
            if ($message -match 'gssapi|auth_gssapi|authentication method') {
                Bad "the database refuses the login method. In the MariaDB client run: ALTER USER '$($first.User)'@'localhost' IDENTIFIED VIA mysql_native_password USING PASSWORD('...');"
            } elseif ($message -match 'Unknown database') {
                Bad "there is no database '$($first.Name)'. Create it and load DB\SQL\*.sql (see README)"
            } elseif ($message -match 'Access denied') {
                Bad "the database refuses $($first.User) with that password (<Database> in the configs)"
            } elseif ($message -match 'Unable to connect|connect to any') {
                Bad "no database answers on $($first.Host):$($first.Port). Is MariaDB running?"
            } else {
                Warn "could not check the database with MySql.Data ($message)"
            }
            $checked = $true
        }
    }
    if (-not $checked) {
        if (Test-Port $first.Port 1000) { Ok "something answers on the database port $($first.Port)" }
        else { Bad "no database answers on $($first.Host):$($first.Port). Is MariaDB running?" }
    }
}

# --- Ports --------------------------------------------------------------------------------------------
$ports = [ordered]@{ "Lobby-Server $lobbyPort" = $lobbyPort; "Cms-Server $chatPort" = $chatPort; "the chat game link $linkPort" = $linkPort
    "Game-Server (Earth) $gamePort" = $gamePort; "Game-Server (Space) $spacePort" = $spacePort }
if (-not $NoLogin) { $ports["Login-Server $loginPort"] = $loginPort }
$busy = @()
foreach ($name in $ports.Keys) {
    if (Test-Port $ports[$name]) { $busy += $name }
}
if ($busy.Count -gt 0) { Bad ("already in use (a server still running?): " + ($busy -join ', ')) } else { Ok "the ports are free" }

Write-Host ""
if ($problems.Count -gt 0) {
    Write-Host "$($problems.Count) problem(s) to fix first; nothing was started." -ForegroundColor Red
    exit 1
}
if ($CheckOnly) {
    Write-Host "The setup looks right." -ForegroundColor Green
    exit 0
}

# --- Start --------------------------------------------------------------------------------------------
function Start-Server($title, $exe, $arguments, $port) {
    Write-Host "Starting $title..." -NoNewline
    $startArgs = @{ FilePath = (Join-Path $bin $exe); WorkingDirectory = $bin }
    if ($arguments) { $startArgs.ArgumentList = $arguments }
    $process = Start-Process @startArgs -PassThru
    if (Wait-Port $port 60) {
        Write-Host " listening on $port" -ForegroundColor Green
        return $true
    }
    if ($process.HasExited) { Write-Host " it stopped (see its window or Log\)" -ForegroundColor Red }
    else { Write-Host " not listening on $port after 60 seconds (see its window or Log\)" -ForegroundColor Red }
    return $false
}

$started = (Start-Server 'Lobby-Server' 'Lobby-Server.exe' $null $lobbyPort) -and
           (Start-Server 'Cms-Server' 'Cms-Server.exe' $null $chatPort) -and
           (Start-Server 'Game-Server (Earth)' 'Game-Server.exe' $null $gamePort) -and
           (Start-Server 'Game-Server (Space)' 'Game-Server.exe' '-instance=space' $spacePort)
if ($started -and -not $NoLogin) {
    $started = Start-Server 'Login-Server' 'Login-Server.exe' $null $loginPort
}

Write-Host ""
if ($started) {
    Write-Host "All servers are running. Close their windows (or type exit in each) to stop them." -ForegroundColor Green
    exit 0
}
Write-Host "A server did not start; the ones before it are still running." -ForegroundColor Red
exit 1

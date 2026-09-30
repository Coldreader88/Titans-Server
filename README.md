# Titans-Server

A server emulator for Universal Century Gundam Online (UCGO), written in C# (.NET Framework 4.8). It works with the
official client, version 4265. `Java.zip` holds the older Java server this one was ported from.

## Servers

| Server | Default port | What it does |
|---|---|---|
| Login-Server | 42012 | Server status for the client's launcher |
| Lobby-Server | 42018 | Account login, character list, create, delete, and the hand-off to the game server |
| Game-Server | 42010 (Earth), 42011 (Space) | The game world. Run one copy for Earth and a second with `-instance=space` for Space |
| Cms-Server | 42016 (clients), 10241 (game link) | Chat, friends, teams and GM commands |

## Build

- **Windows:** open `Titans-UCGO.sln` in Visual Studio and build. The servers go into `Bin\`, and every build copies
  `ConfigFiles\` to `Bin\Config` and `DB\` to `Bin\DB`, overwriting what is there.
- **Linux with mono:** `Tests/run-tests.sh` builds everything with `mcs` into `$WORK/bin` (see Tests below).

## Database

The servers share one MariaDB (or MySQL) database called `titans-server`.

1. Install MariaDB.
2. Create the database and load the tables:
   ```
   mariadb -uroot -p -e "CREATE DATABASE \`titans-server\`"
   mariadb -uroot -p titans-server < DB/SQL/accounts.sql      (then characters.sql, cms.sql, tele_bookmark.sql, world.sql)
   ```
   The game server creates its other tables itself.
3. Put the user and password in `ConfigFiles/LobbyServer.xml`, `GameServer.xml` and `CMSServer.xml`
   (`<Database>`; the default is root / titans).

The MySql.Data driver cannot log in with MariaDB's Windows default (`auth_gssapi_client`). Switch the user to
native passwords:
```
ALTER USER 'root'@'localhost' IDENTIFIED VIA mysql_native_password USING PASSWORD('titans');
```

## Accounts

Create accounts on the Lobby-Server console:
```
account <name> <password> [level]
```
The level is 10 for a player (the default), 4 for a GM and 9 for an admin. The same command sets a new password and
level for an existing account. GMs can use the `#` commands in chat (`#help` lists them).

`AutoCreateAccounts` in `LobbyServer.xml` (off by default) makes an account the first time an unknown user name logs
in. Only turn it on for a private server.

Passwords are stored salted (PBKDF2-SHA256). Accounts from the Java server's database keep working: their old SHA-1
hash is replaced the next time they log in. Five wrong passwords from one address lock it out for 15 minutes (`LoginFailLimit`,
`LoginLockMinutes`; `unlock` on the Lobby console lifts every lock). The game and chat servers only accept a character from the address its
player logged in to the Lobby from.

## Run

Double-click `Bin\Start-Servers.bat` (every build copies it there). It checks the setup first: the programs are
built, the three configs use the same database, the database accepts that login and has its tables, the chat
passwords and ports match, and no port is already taken. If all is well it starts Lobby-Server, Cms-Server,
Game-Server (Earth), Game-Server `-instance=space` and Login-Server, each in its own window, waiting for each to
listen before the next. `Start-Servers.bat -CheckOnly` only checks; `-NoLogin` leaves out the Login-Server.

To start them by hand, run them from `Bin\` in the same order.

Settings worth knowing:
- `LobbyServer.xml` `GameServerIP`: the address the client connects to for the game (127.0.0.1 when everything
  runs on one PC).
- `GameServer.xml` `TransferHost`: the same for the flights between Earth and Space.
- `GameServer.xml` `ChatPassword` must match `CMSServer.xml` `GameLinkPassword`.
- `GameServer.xml` also holds the game rules: wreck chance, skill caps, crime and exile, promotions, town wars, NPC
  aggro range and the autosave interval.

Console commands:
- Lobby-Server: `account <name> <password> [level]`, `ban <account> [days]` (no days: for good), `unban <account>`,
  `unlock` (lifts the wrong-password lockouts).
- Game-Server: `players` (who is online, with account and address), `kick <character>`, `ban <character> [days]`
  and `unban <character>` (the whole account; a banned player is logged out), `backup`, `save`,
  `spawn <player> <what>`, `gm <player> <#command>`.

In chat, admins can run `#script name`: the GM commands in `DB/Scripts/name.txt`, one per line.

## Backups and logs

The Earth game server writes the whole database to `Backups\titans-server-<date>-<time>.sql` every 6 hours and keeps
the newest 28 (`BackupHours`, `BackupFolder`, `BackupKeep` in `GameServer.xml`; `backup` on its console makes one
now). To restore one: `mariadb -uroot -p titans-server < Backups\titans-server-....sql`.

On Windows every server also writes what it shows to `Log\<date>\<server>_<date>.log` next to its exe
(`NLog.config`).

Error 4002 in the client comes from the client itself. It shows when an overlay (RivaTuner and similar) hooks the
client's timers.

## Tests

`Tests/run-tests.sh` builds the servers with mono, starts them against MariaDB and runs a scripted client
(`Tests/TestClient.cs`) through logins, characters, items, shops, crafting, vehicles, combat, NPCs, crimes, town wars,
chat and the GM commands. It needs `mono-complete`, the MariaDB client, and either a local `mariadbd` (the script
starts a private one) or `DBHOST` pointing at a server whose root password is `titans`.

```
Tests/run-tests.sh          # everything
Tests/run-tests.sh full     # the client test only
Tests/run-tests.sh spawn    # the GM console test only
```

GitHub runs the same script on every push (`.github/workflows/tests.yml`). The tests are scripted, so a real client
test is still needed after changes to packets.

## Reference material

- `UCGO Packet Logs.zip`: decrypted captures of the official servers, used to check packet layouts.
- The client's `DATA` files, decoded to CSV, are the source of items, shops, recipes, quests and NPC positions
  (`DB/Templates`, `DB/Npcs`).

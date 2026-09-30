#!/bin/bash
# Builds the servers with mono's mcs and runs the scripted client tests (Tests/TestClient.cs) against MariaDB.
#
#   Tests/run-tests.sh [all|full|spawn]
#
# full:  Lobby, Earth and Space game servers and the chat server; a scripted client logs in three players and
#        goes through characters, items, shops, crafting, vehicles, combat, NPCs, crimes, towns, chat and more.
# spawn: GM console commands typed into the game server (spawns, #skill, #town, #tp, NPC fights, quests).
#
# Needs mono (mcs + runtime), the MariaDB client and either
#   - mariadbd and mariadb-install-db: a private server is started in $WORK/db (the default), or
#   - DBHOST=<host>: an existing server there on port 3306 with root password "titans" (as in CI).
# Everything goes into WORK (default /tmp/titans-tests); logs in $WORK/logs. Exit code 0 = all passed.
set -u
REPO=$(cd "$(dirname "$0")/.." && pwd)
WORK=${WORK:-/tmp/titans-tests}
WHICH=${1:-all}
BIN=$WORK/bin
LOGS=$WORK/logs
export TITANS_CONSOLE_LOG=1
mkdir -p "$BIN" "$LOGS"

# ---- build -------------------------------------------------------------------------------------------------
files() { # the Compile items of a csproj
  local dir
  dir=$(dirname "$1")
  grep -o 'Compile Include="[^"]*"' "$1" | sed 's/Compile Include="//;s/"$//' | sed 's|\\|/|g' |
    while read -r f; do echo "$dir/$f"; done
}
build() { # csproj target output refs...
  local proj=$1 target=$2 name=$3
  shift 3
  local src
  mapfile -t src < <(files "$proj")
  mcs -nologo -nowarn:0618,0168,0219,0414,0649,0169,0108,0114,0162,0105,1591,0659,0661,0660,0612,0465,0675 \
    -unsafe -target:"$target" -out:"$BIN/$name" -lib:"$BIN" \
    -r:System.dll -r:System.Core.dll -r:System.Data.dll -r:System.Xml.dll -r:System.Xml.Linq.dll \
    -r:System.Drawing.dll -r:System.Numerics.dll -r:Microsoft.CSharp.dll -r:System.Windows.Forms.dll \
    "$@" "${src[@]}" || { echo "BUILD FAILED: $name"; exit 1; }
}
echo "== Building into $BIN"
cd "$REPO"
cp Dependencies/NLog/NLog.dll Dependencies/Collections/C5.dll Dependencies/MySql.Data/*.dll "$BIN"/
build Framework/IO/Bytebuffer/Bytebuffer.csproj library Bytebuffer.dll
build SmartEngine.Core/Core.csproj library SmartEngine.Core.dll -r:NLog.dll
build SmartEngine.Network/Network.csproj library SmartEngine.Network.dll -r:SmartEngine.Core.dll -r:C5.dll -r:MySql.Data.dll
build Common/Common.csproj library Common.dll -r:SmartEngine.Core.dll -r:SmartEngine.Network.dll -r:Bytebuffer.dll -r:MySql.Data.dll
build Lobby-Server/Lobby-Server.csproj exe Lobby-Server.exe -r:SmartEngine.Core.dll -r:SmartEngine.Network.dll -r:Common.dll -r:MySql.Data.dll
build Game-Server/Game-Server.csproj exe Game-Server.exe -r:SmartEngine.Core.dll -r:SmartEngine.Network.dll -r:Common.dll -r:MySql.Data.dll
build Cms-Server/Cms-Server.csproj exe Cms-Server.exe -r:SmartEngine.Core.dll -r:SmartEngine.Network.dll -r:Common.dll -r:MySql.Data.dll
mcs -nologo -nowarn:0168,0219 -out:"$BIN/TestClient.exe" -r:"$BIN/Common.dll" -r:"$BIN/SmartEngine.Network.dll" \
  -r:"$BIN/SmartEngine.Core.dll" Tests/TestClient.cs || { echo "BUILD FAILED: TestClient.exe"; exit 1; }

# Runtime files, and the test settings: fast town wars, exile at 6 crime points, every kill leaves a wreck,
# unknown user names create accounts, and three test NPCs.
rm -rf "$BIN/DB" "$BIN/Config"
cp -r DB "$BIN/DB"
cp -r ConfigFiles "$BIN/Config"
python3 - "$BIN/Config" <<'EOF'
import re, sys
def patch(path, values):
    s = open(path, encoding='utf-8-sig').read()
    root = re.search(r'</(\w+)>\s*$', s).group(1)
    for key, value in values.items():
        if re.search(r'<%s>' % key, s):
            s = re.sub(r'<%s>[^<]*</%s>' % (key, key), '<%s>%s</%s>' % (key, value, key), s)
        else:
            s = s.replace('</%s>' % root, '  <%s>%s</%s>\n</%s>' % (key, value, key, root))
    open(path, 'w', encoding='utf-8-sig').write(s)
patch(sys.argv[1] + '/GameServer.xml', {'OccupationPeaceMinutes': '0', 'OccupationCaptureSeconds': '3',
                                         'CrimeExileCount': '6', 'WreckChance': '100'})
patch(sys.argv[1] + '/LobbyServer.xml', {'AutoCreateAccounts': 'true'})
EOF

# Three test NPCs next to Char's spot: a target with 300 health, a shooter that fires back, and a vendor.
cat >> "$BIN/DB/Npcs/npcs.csv" <<'EOF'
900001,Target,2,410007,9001,1,30000,30000,30,0,0,0,6,48,280006/-1/280006/280006,300
900002,Shooter,2,410007,9001,1,30100,30000,30,0,0,0,6,48,280006/-1/280006/280006,100000
900003,MachineVender,2,1000003,9002,1,30200,30000,30,0,0,0,5,0,-1/-1/-1/-1,0
EOF

# ---- database ------------------------------------------------------------------------------------------------
if [ -n "${DBHOST:-}" ]; then
  DBARGS=(-uroot -ptitans --protocol=TCP -h "$DBHOST" -P 3306)
  unset DBSOCK
else
  export DBSOCK=$WORK/db.sock
  DBARGS=(-uroot -ptitans --socket="$DBSOCK")
  if ! mariadb "${DBARGS[@]}" -e "select 1" >/dev/null 2>&1; then
    if [ ! -d "$WORK/db/mysql" ]; then
      mkdir -p "$WORK/db"
      mariadb-install-db --user="$(id -un)" --datadir="$WORK/db" --auth-root-authentication-method=normal > "$LOGS/db-install.log" 2>&1
    fi
    (setsid mariadbd --user="$(id -un)" --datadir="$WORK/db" --pid-file="$WORK/db.pid" --socket="$DBSOCK" \
      --port=3306 --bind-address=127.0.0.1 >> "$LOGS/db.log" 2>&1 < /dev/null &)
    for _ in $(seq 1 30); do [ -S "$DBSOCK" ] && break; sleep 1; done
    # A fresh server's root has no password yet: give it the one the configs use.
    mariadb -uroot --socket="$DBSOCK" -e "ALTER USER 'root'@'localhost' IDENTIFIED VIA mysql_native_password USING PASSWORD('titans'); FLUSH PRIVILEGES;" 2>/dev/null
  fi
fi
export DBHOST=${DBHOST:-}
sql() { mariadb "${DBARGS[@]}" "$@"; }
sql -e "select 1" >/dev/null || { echo "Cannot reach MariaDB"; exit 1; }
if ! sql -e "USE \`titans-server\`" 2>/dev/null; then
  echo "== Creating the titans-server database"
  sql -e "CREATE DATABASE \`titans-server\`"
  for f in DB/SQL/*.sql; do sql titans-server < "$f" || { echo "Could not load $f"; exit 1; }; done
fi
reset_db() {
  sql titans-server -e "DELETE FROM characters; DELETE FROM appearance; DELETE FROM container; DELETE FROM garments;
    DELETE FROM skills; DELETE FROM login_sessions; DELETE FROM ground_items;
    DROP TABLE IF EXISTS flights; DROP TABLE IF EXISTS character_state; DROP TABLE IF EXISTS occupation_city;" 2>/dev/null
}

# ---- servers ---------------------------------------------------------------------------------------------------
PIDS=()
stop_all() {
  for p in "${PIDS[@]}"; do kill "$p" 2>/dev/null; done
  PIDS=()
  exec 7>&- 2>/dev/null
  sleep 1
}
trap stop_all EXIT
start() { # log name, args...: runs a server with its stdin held open
  local log=$1
  shift
  (cd "$BIN" && exec mono "$@" < <(sleep 3600 2>/dev/null) > "$LOGS/$log" 2>&1) &
  PIDS+=($!)
}

FAILED=0
run_full() {
  echo "== Full test"
  reset_db
  start lobby.log Lobby-Server.exe
  start cms.log Cms-Server.exe
  start game.log Game-Server.exe
  start space.log Game-Server.exe -instance=space
  sleep 6
  (cd "$BIN" && GAMELOG=$LOGS/game.log timeout 300 mono TestClient.exe > "$LOGS/full.out" 2>&1)
  local rc=$?
  stop_all
  grep -E "^FAIL" "$LOGS/full.out"
  tail -1 "$LOGS/full.out"
  [ $rc -eq 0 ] || { tail -40 "$LOGS/full.out"; FAILED=1; }
}

run_spawn() {
  echo "== Spawn test"
  reset_db
  local fifo=$WORK/game.fifo ready=$WORK/ready
  rm -f "$fifo" "$ready" "${ready}2" "${ready}3"
  mkfifo "$fifo"
  start lobby.log Lobby-Server.exe
  start cms.log Cms-Server.exe
  (cd "$BIN" && exec mono Game-Server.exe < "$fifo" > "$LOGS/game-spawn.log" 2>&1) &
  PIDS+=($!)
  exec 7>"$fifo"
  sleep 6
  (cd "$BIN" && GAMELOG=$LOGS/game-spawn.log READY=$ready timeout 300 mono TestClient.exe spawn > "$LOGS/spawn.out" 2>&1; echo "rc=$?" >> "$LOGS/spawn.out") &
  local client=$!
  say() { for c in "$@"; do echo "$c" >&7; sleep 0.3; done; }
  for _ in $(seq 1 30); do [ -f "$ready" ] && break; sleep 1; done
  say "spawn Gmtest id 540005 100" "spawn Gmtest id 460004" "spawn Gmtest id 280014" "spawn Gmtest id 410000" \
    "gm Gmtest skill ambac 85.5" "gm Gmtest crime 3" "gm Gmtest crime" "gm Gmtest town" "gm Gmtest town newman war 5" \
    "gm Gmtest town newman icf 3 zeon" "gm Gmtest town newman end zeon" "gm Gmtest town newman peace 30" \
    "gm Gmtest town richmond open" "gm Gmtest town rich owner ef" "gm Gmtest town richmond reset" \
    "gm Gmtest town nowhere open" "gm Gmtest town newman dance" "gm Gmtest skill mining 12.5" \
    "gm Gmtest items" "gm Gmtest items weapon zaku" "gm Gmtest items ms 2" "gm Gmtest items all zaku ii 1" \
    "gm Gmtest skill" "gm Gmtest npcs" "gm Gmtest npcs zeon space 2" "gm Gmtest npcs all vender" \
    "spawn Gmtest id 410000" "spawn Gmtest id 410057" "spawn Gmtest id 420000" "spawn Gmtest id 410007" \
    "spawn Gmtest ideng 400000 jet engine typeA lv.3"
  for _ in $(seq 1 60); do [ -f "${ready}2" ] && break; sleep 1; done
  say "spawn Gmtest npc 410007" "spawn Gmtest npc friendly" "spawn Gmtest npc friendly 410000" "gm Gmtest near" \
    "gm Gmtest near 20000" "gm Gmtest tp 1000000005" "gm Gmtest tp nobody" "gm Gmtest tp burchard" "gm Gmtest tp 1090000000"
  for _ in $(seq 1 90); do [ -f "${ready}3" ] && break; sleep 1; done
  say "spawn Gmtest id 550410"
  sleep 3
  say "gm Gmtest rank points 20" "gm Gmtest rank"
  wait $client
  stop_all
  grep -E "^FAIL" "$LOGS/spawn.out"
  tail -2 "$LOGS/spawn.out" | head -1
  grep -q "^rc=0" "$LOGS/spawn.out" || FAILED=1
}

case "$WHICH" in
  full) run_full ;;
  spawn) run_spawn ;;
  *) run_full; run_spawn ;;
esac
[ $FAILED -eq 0 ] && echo "== ALL TESTS PASSED" || echo "== TESTS FAILED (logs in $LOGS)"
exit $FAILED

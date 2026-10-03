#!/bin/bash
# Decrypts every .pcap of UCGO Packet Logs.zip into OUT/caps/*.txt (one file per capture, every packet as hex).
#   Tools/CapturedPlayers/decall.sh <directory with Common.dll, SmartEngine.*.dll and XORTable.dat (a built Bin)> <OUT>
# Needs tshark, mono (mcs) and python3.
set -eu
BIN=$(cd "$1" && pwd); OUT=$(mkdir -p "$2" && cd "$2" && pwd)
HERE=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$HERE/../.." && pwd)
mcs -nologo -out:"$BIN/Dec.exe" -r:"$BIN/Common.dll" -r:"$BIN/SmartEngine.Network.dll" -r:"$BIN/SmartEngine.Core.dll" "$HERE/Dec.cs"
mkdir -p "$OUT/logs" "$OUT/streams" "$OUT/caps"
(cd "$OUT/logs" && unzip -q -o "$REPO/UCGO Packet Logs.zip")
find "$OUT/logs" -name '*.pcap' -print0 | while IFS= read -r -d '' pcap; do
  h=$(echo "$pcap" | md5sum | cut -c1-10)
  mkdir -p "$OUT/streams/$h"
  python3 "$HERE/split.py" "$pcap" "$OUT/streams/$h" > /dev/null
  { echo "FILE $pcap"; (cd "$BIN" && mono Dec.exe "$OUT/streams/$h"/*.bin); } > "$OUT/caps/$h.txt" 2>&1 || true
done
echo "Decrypted $(ls "$OUT/caps" | wc -l) captures into $OUT/caps"

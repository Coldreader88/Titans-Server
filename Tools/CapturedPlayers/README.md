# Captured players

Makes `DB/Npcs/captured_players.csv`, the list the GM command `#spawnfromlist` spawns: every player the official
server showed in `UCGO Packet Logs.zip`, with position, rank, team, vehicle and weapons, or on foot their clothes.

1. Build the servers (Visual Studio, or the build part of `Tests/run-tests.sh`) and copy `DB/Encryption/XORTable.dat`
   next to `Common.dll`.
2. `Tools/CapturedPlayers/decall.sh <that Bin> <work dir>` decrypts every capture (needs tshark, mono, python3).
3. `python3 Tools/CapturedPlayers/extract_players.py <work dir>/caps` writes the CSV.

`split.py` reassembles each TCP stream of a pcap; `Dec.cs` decrypts and prints its packets.

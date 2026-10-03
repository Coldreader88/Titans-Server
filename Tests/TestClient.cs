using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Common.Network.Encryption;
using Common.Network.Encryption.UCGO.Blowfish;
using Common.Network.Packets;

class Conn
{
    TcpClient tcp; NetworkStream s; UCEncryption crypt = new UCEncryption(); uint seq = 0;
    List<byte> buf = new List<byte>();
    public string Name;
    // Skill changes (0x8034) arrive at random after attacks and repairs; unless KeepGains, Recv puts them here.
    public List<byte[]> Gains = new List<byte[]>();
    public bool KeepGains;
    // When set, Recv also copies here every attack result (0x800F) an NPC (1090000000 and up) sends.
    public List<byte[]> NpcResults;
    public Conn(string name, string host, int port) { Name = name; tcp = new TcpClient(host, port); s = tcp.GetStream(); s.ReadTimeout = 5000; }
    // From another local address (127.0.0.2): a different player's machine as far as the servers can tell.
    public Conn(string name, string host, int port, string from)
    {
        Name = name;
        tcp = new TcpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(from), 0));
        tcp.Connect(host, port);
        s = tcp.GetStream(); s.ReadTimeout = 5000;
    }
    public void Send(uint op, byte[] body)
    {
        seq++;
        var p = new UCPacket<uint>(body) { ID = op };
        var wire = p.ToWire(seq);
        crypt.Encrypt(wire, 0, wire.Length);
        s.Write(wire, 0, wire.Length);
    }
    public Tuple<uint, byte[]> Recv(bool quiet = false)
    {
        while (true)
        {
            if (buf.Count >= 64)
            {
                var h = buf.GetRange(0, 64).ToArray();
                uint key = crypt.DecryptHeader(h, 0);
                int xs = BitConverter.ToInt32(h, 16), bs = BitConverter.ToInt32(h, 20); uint op = BitConverter.ToUInt32(h, 24);
                if (buf.Count >= 64 + bs)
                {
                    var p = new byte[64 + bs]; Array.Copy(h, p, 64); buf.CopyTo(64, p, 64, bs);
                    buf.RemoveRange(0, 64 + bs);
                    crypt.DecryptBody(p, 0, p.Length, key);
                    var body = new byte[xs]; Array.Copy(p, 64, body, 0, xs);
                    if (!quiet) Console.WriteLine("  [{0}] <- 0x{1:X5} ({2} bytes) {3}", Name, op, xs, Hex(body, 48));
                    if (op == 0x8034 && !KeepGains) { Gains.Add(body); continue; }
                    if (op == 0x800F && NpcResults != null && body.Length >= 14 && new R(body).U32() >= 1090000000) NpcResults.Add(body);
                    return Tuple.Create(op, body);
                }
            }
            var tmp = new byte[8192];
            int n;
            try { n = s.Read(tmp, 0, tmp.Length); } catch (IOException) { return null; }
            if (n <= 0) return null;
            for (int i = 0; i < n; i++) buf.Add(tmp[i]);
        }
    }
    public static string Hex(byte[] b, int max = 1 << 20)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < b.Length && i < max; i++) sb.AppendFormat("{0:X2} ", b[i]);
        if (b.Length > max) sb.Append("...");
        return sb.ToString();
    }
    public void Close() { tcp.Close(); }
}

class B
{
    List<byte> b = new List<byte>();
    public B U32(uint v) { b.Add((byte)(v >> 24)); b.Add((byte)(v >> 16)); b.Add((byte)(v >> 8)); b.Add((byte)v); return this; }
    public B I32(int v) { return U32((uint)v); }
    public B U16(ushort v) { b.Add((byte)(v >> 8)); b.Add((byte)v); return this; }
    public B Byte(byte v) { b.Add(v); return this; }
    public B Bytes(byte[] v) { b.AddRange(v); return this; }
    public B Size(int n) { if (n <= 0x7F) b.Add((byte)(0x80 | n)); else { b.Add((byte)(n % 0x80)); b.Add((byte)(0x80 | n / 0x80)); } return this; }
    public B Str(string v) { Size(v.Length); b.AddRange(Encoding.Unicode.GetBytes(v)); return this; }
    public byte[] Get() { return b.ToArray(); }
}

class R
{
    byte[] d; public int Pos;
    public byte[] Buf { get { return d; } }
    public R(byte[] d) { this.d = d; }
    public uint U32() { uint v = (uint)(d[Pos] << 24 | d[Pos + 1] << 16 | d[Pos + 2] << 8 | d[Pos + 3]); Pos += 4; return v; }
    public ushort U16() { ushort v = (ushort)(d[Pos] << 8 | d[Pos + 1]); Pos += 2; return v; }
    public int I32() { return (int)U32(); }
    public byte Byte() { return d[Pos++]; }
    public int Size() { byte f = d[Pos++]; if ((f & 0x80) != 0) return f & 0x7F; byte s = d[Pos++]; return (s & 0x7F) * 0x80 + f; }
    public string Str() { int n = Size(); var s = Encoding.Unicode.GetString(d, Pos, n * 2); Pos += n * 2; return s; }
    public int Left { get { return d.Length - Pos; } }
}

class Test
{
    static int failures = 0;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) failures++; }

    static byte[] LoginBody(string user, string pass)
    {
        var nameBytes = Encoding.Unicode.GetBytes(user);
        var key = new byte[nameBytes.Length + 2]; Array.Copy(nameBytes, key, nameBytes.Length);
        var pw = Encoding.Unicode.GetBytes(pass);
        var padded = new byte[(pw.Length + 2 + 7) / 8 * 8]; Array.Copy(pw, padded, pw.Length);
        new Blowfish(key).Encrypt(padded, 0, padded.Length);
        return new B().Str(user).U32(4265).Size(padded.Length).Bytes(padded).Get();
    }

    static byte[] CreateBody(uint acc, string name, byte faction)
    {
        var app = new byte[78];
        app[14] = 1; app[16] = faction; app[37] = 1; app[39] = 1; app[66] = 1; app[68] = 1;
        return new B().U32(acc).Bytes(app).Str(name).Byte(0).Byte(0).I32(-1).Size(3).I32(60).I32(60).I32(50).I32(170).Get();
    }

    class LobbyResult { public uint Key, Acc; public List<uint> Chars = new List<uint>(); public string IP; public int Port; }

    static List<uint> ParseList(byte[] body)
    {
        // 0x38001: 00 03 acc 00 / count / (acc id money...)? Parse loosely: find char ids = uint32 after "acc 00 00" pattern is fiddly,
        // so use the list layout from SM_CHARACTER_LIST: 00, 03, acc(4)?  Fall back to scanning for ids starting with digit 1.
        var ids = new List<uint>();
        var r = new R(body);
        return ids;
    }

    static LobbyResult Lobby(string user, string pass, string charName, byte faction, bool testDelete)
    {
        var res = new LobbyResult();
        var c = new Conn("lobby:" + user, "127.0.0.1", 42018);
        c.Send(0x30000, LoginBody(user, pass));
        var r = new R(c.Recv().Item2);
        uint status = r.U32(); res.Key = r.U32(); res.Acc = r.U32();
        Check(status == 1, user + " logs in");
        Check(res.Key != 0, user + " gets a nonzero session key");

        c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get());
        var list = c.Recv().Item2;

        c.Send(0x30003, CreateBody(res.Acc, charName, faction));
        var created = c.Recv();
        Check(created.Item1 == 0x38003, user + " creates " + charName);
        uint charID = new R(created.Item2) { Pos = 12 }.U32();

        if (testDelete)
        {
            // Names follow the client's CHARAFILTER_NAME.LST: "<" is not allowed, and no reply comes (the list does).
            c.Send(0x30003, CreateBody(res.Acc, "Bad<Name>", faction));
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get());
            var badName = c.Recv();
            Check(badName.Item1 == 0x38001 && Sql("SELECT COUNT(*) FROM characters WHERE char_name = 'Bad<Name>'") == "0",
                "a name with a character the client does not allow is refused");
            c.Send(0x30003, CreateBody(res.Acc, "Seventeen letters", faction));
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get());
            var longName = c.Recv();
            Check(longName.Item1 == 0x38001 && Sql("SELECT COUNT(*) FROM characters WHERE char_name = 'Seventeen letters'") == "0",
                "and so is a name longer than the client's 16 characters");
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get()); c.Recv();
            c.Send(0x30003, CreateBody(res.Acc, charName + "X", faction));
            var second = c.Recv();
            uint delID = new R(second.Item2) { Pos = 12 }.U32();
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get()); c.Recv();
            // A new character is too young to delete (players wait 4 hours): no reply, still listed.
            c.Send(0x30004, new B().U32(delID).U32(res.Acc).Byte(0).Get());
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get());
            var refused = c.Recv();
            string idHex = string.Format("{0:X2} {1:X2} {2:X2} {3:X2}", delID >> 24, (delID >> 16) & 0xFF, (delID >> 8) & 0xFF, delID & 0xFF);
            Check(refused.Item1 == 0x38001 && Conn.Hex(refused.Item2).Contains(idHex), "deleting a character younger than the wait is refused");
            // Once past the wait it can go, and the lobby reports it as older than the client's own 14 days.
            Sql("UPDATE characters SET date_created = UNIX_TIMESTAMP() - 5*3600 WHERE char_id = " + delID.ToString().Substring(1));
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get()); c.Recv();
            c.Send(0x30002, new B().U32(0).U32(delID).Byte(1).Get());
            var oldInfo = c.Recv();
            string infoHex = Conn.Hex(oldInfo.Item2);
            Check(oldInfo.Item1 == 0x38002, "player info for a deletable character");
            c.Send(0x30004, new B().U32(delID).U32(res.Acc).Byte(0).Get());
            var del = c.Recv();
            Check(del.Item1 == 0x38004 && del.Item2.Length == 28 && del.Item2[1] == 5 && del.Item2[3] == 2, "delete reply is 00 05 00 02 + 24 zeros");
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get());
            var after = c.Recv().Item2;
            Check(Conn.Hex(after).Contains(string.Format("{0:X2} {1:X2} {2:X2} {3:X2}", delID >> 24, (delID >> 16) & 0xFF, (delID >> 8) & 0xFF, delID & 0xFF)) == false, "deleted character is gone from the list");
            // Deleting someone else's id is refused silently.
            c.Send(0x30004, new B().U32(12345).U32(res.Acc).Byte(0).Get());
        }
        else
        {
            c.Send(0x30001, new B().Byte(0).U32(res.Acc).U32(0).Get()); c.Recv();
        }

        c.Send(0x30002, new B().U32(0).U32(charID).Byte(1).Get());
        var info = c.Recv();
        Check(info.Item1 == 0x38002, "player info for " + charName);

        // Wrong key is refused.
        c.Send(0x30005, new B().U32(res.Key ^ 0x1234).U32(res.Acc).U32(charID).U16(1).U16(0xFFFF).Byte(0x80).Get());
        var bad = c.Recv();
        Check(bad.Item1 == 0x38005 && new R(bad.Item2).U32() == 5, "handoff with a wrong session key is refused (5)");

        c.Send(0x30005, new B().U32(res.Key).U32(res.Acc).U32(charID).U16(1).U16(0xFFFF).Byte(0x80).Get());
        var gs = c.Recv();
        var gr = new R(gs.Item2);
        uint ok = gr.U32(); int n = gr.Size(); res.IP = Encoding.ASCII.GetString(gs.Item2, gr.Pos, n); gr.Pos += n; res.Port = gr.U16();
        Check(ok == 1 && res.IP == "127.0.0.1" && res.Port == 42010, string.Format("handoff sends {0}:{1}", res.IP, res.Port));
        c.Close();
        res.Chars.Add(charID);
        return res;
    }

    // Passwords are stored salted; an old Java SHA-1 password still logs in and is rehashed; a wrong one does not.
    static void PasswordTests()
    {
        Check(Sql("SELECT password FROM accounts WHERE name = 'tester1'").StartsWith("pbkdf2$100000$"), "new passwords are stored salted (pbkdf2$100000$...)");
        Sql("DELETE FROM accounts WHERE name = 'oldjava'; INSERT INTO accounts (name, password, acc_level, creation_date) VALUES ('oldjava', SHA1('oldpass1'), 10, '')");
        Check(LobbyStatus("oldjava", "wrongpass") != 1, "a wrong password is refused");
        Check(LobbyStatus("oldjava", "oldpass1") == 1, "an old unsalted SHA-1 password (Java server) still logs in");
        Check(Sql("SELECT password FROM accounts WHERE name = 'oldjava'").StartsWith("pbkdf2$"), "and is stored salted from then on");
        Check(LobbyStatus("oldjava", "oldpass1") == 1 && LobbyStatus("oldjava", "OLDPASS1") != 1, "the salted password logs in, the wrong case does not");

        // LoginFailLimit 5: the fifth wrong password from one address locks it out (0x15), other addresses still log in.
        uint last = 0;
        for (int i = 0; i < 5; i++)
        {
            last = LobbyStatus("oldjava", "guess" + i, "127.0.0.3");
        }
        Check(last == 9, "five wrong passwords are each refused as wrong (9)");
        Check(LobbyStatus("oldjava", "oldpass1", "127.0.0.3") == 0x15, "then that address is locked out even with the right password (0x15)");
        Check(LobbyStatus("oldjava", "oldpass1") == 1, "the player still logs in from their own address");
    }

    static uint LobbyStatus(string user, string pass, string from = null)
    {
        var c = from == null ? new Conn("lobby:" + user, "127.0.0.1", 42018) : new Conn("lobby:" + user, "127.0.0.1", 42018, from);
        c.Send(0x30000, LoginBody(user, pass));
        var r = c.Recv();
        c.Close();
        return r == null ? 0 : new R(r.Item2).U32();
    }

    static Conn GameLogin(string who, LobbyResult lr, uint key)
    {
        var g = new Conn("game:" + who, lr.IP, lr.Port);
        g.Send(0x41, new B().U32(0x10000).U32(lr.Chars[0]).U32(key).U32(0).Str(who).U16(2).Byte(0x80).Get());
        return g;
    }

    static byte[] Coord(uint charID, int x, int y, int z)
    {
        return new B().I32(x).I32(y).I32(z).U16(0).U16(0).U16(0x3A7E).U32(charID).U16(0xFFFF).U16(1).U32(0).I32(-1)
            .Byte(5).Byte(0x0A).Byte(0).Byte(0).U16(2).U16(0).U16(1).I32(-1).Byte(0xFF).U32(0).Get();
    }

    static byte[] Move(ushort section, uint me, uint item, uint itemFmt, uint src, uint dst, uint srcStatic, uint dstStatic, uint amount, byte t1, byte t2)
    {
        return new B().U16(section).U16(0).U32(me).U32(item).U32(itemFmt).U32(src).U32(0x14).U32(dst).U32(0x14)
            .I32(-1).U32(srcStatic).U32(dstStatic).U32(0).U32(amount).Byte(t1).Byte(t2).Get();
    }

    // 2 yarn go into the trade pack (in the swap pack) and are saved there (container_id 110005), then come back.
    static void TradePackTests(Conn g, uint me, uint backpack, uint tradePack, uint yarn)
    {
        Check(tradePack != 0, "found the trade pack");
        g.Send(0x17, Move(7, me, yarn, 0x13, backpack, tradePack, 110001, 110005, 2, 0xFF, 0xFF));
        var r = g.Recv(); var rr = new R(r.Item2);
        uint kind = rr.U32(); rr.Pos = 32; uint packed = rr.U32();
        Check(r.Item1 == 0x8017 && kind == 0x02010002 && packed != 0, "split 2 yarn into the trade pack (0x0201)");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT item_amount FROM container WHERE char_id = " + me + " AND container_id = 110005 AND item_id = 240000") == "2",
            "the trade pack's 2 yarn are saved (container_id 110005)");
        g.Send(0x17, Move(7, me, packed, 0x13, tradePack, backpack, 110005, 110001, 2, 0xFF, 0xFF));
        r = g.Recv();
        Check(r.Item1 == 0x8017 && new R(r.Item2).U32() == 0x03010002, "and merge back into the backpack stack (0x0301)");
        Check(Sql("SELECT COUNT(*) FROM container WHERE char_id = " + me + " AND container_id = 110005") == "0", "the trade pack is empty again");

        // RequestMoneyDivide (section 7 from the main money) and RequestMoneyAddition (section 0x0A into it).
        uint money = me + 500000;
        g.Send(0x17, Move(7, me, money, 0x13, 0, tradePack, 0, 110005, 111, 0xFF, 0xFF));
        r = g.Recv(); rr = new R(r.Item2); kind = rr.U32(); rr.Pos = 32; uint coins = rr.U32();
        Check(r.Item1 == 0x8017 && kind == 0x02010002 && coins != 0, "111 money split into the trade pack (0x0201)");
        g.Send(0x17, Move(0x0A, me, coins, 0x13, tradePack, money, 110005, 500000, 111, 0xFF, 0xFF));
        r = g.Recv(); rr = new R(r.Item2); kind = rr.U32(); rr.Pos = 32;
        Check(r.Item1 == 0x8017 && kind == 0x03010002 && rr.U32() == money, "and back into the main money with section 0x0A (0x0301, the money as target, as official)");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT COUNT(*) FROM container WHERE char_id = " + me + " AND container_id = 110005") == "0", "the trade pack holds no money");
    }

    // Amuro (EF, Lieutenant) sets up an EF camp weapon shop far from everyone and buys at it (camp town 37, shop 201).
    static void CampTests(Conn g, uint me, uint backpack, Dictionary<uint, uint[]> info)
    {
        uint ef = 0, zeon = 0;
        foreach (var kv in info) { if (kv.Value[1] == 340001) ef = kv.Key; if (kv.Value[1] == 340003) zeon = kv.Key; }
        Check(ef != 0 && zeon != 0, "found the seeded camps");
        g.Send(0x21, Buy(1, me, backpack, 10, 540000, town: 37, shop: 201));
        Check(Refused(g, 0x8021), "no camp near: the camp town's shop is refused");
        g.Send(0x02, Coord(me, 500000, 500000, 30));
        g.Send(0x23, Drop(1, me, zeon, 0x13, backpack, 1, 340003, 110001, 0, 500100, 500000, 0xFF));
        Check(Refused(g, 0x8023), "an EF player cannot set up a Zeon camp (0x8023 refused)");
        g.Send(0x23, Drop(1, me, ef, 0x13, backpack, 1, 340001, 110001, 0, 500100, 500000, 0xFF));
        var placed = RecvOp(g, 0x8023);
        Check(placed.Item1 == 0x8023 && placed.Item2[3] == 2, "a Lieutenant sets up the EF camp weapon shop (0x8023 done)");
        Drain(g);
        g.Send(0x21, Buy(1, me, backpack, 10, 540001, town: 37, shop: 201));
        var bought = RecvOp(g, 0x8021);
        Check(bought.Item1 == 0x8021 && bought.Item2[3] == 2 && new R(bought.Item2) { Pos = 28 }.U32() == 350, "and buys 10 vulcan cartridges for 350 at the camp (town 37, shop 201)");
        string saved = "";
        for (int i = 0; i < 30 && saved != "1,500100"; i++) { System.Threading.Thread.Sleep(500); saved = Sql("SELECT CONCAT(vehicle, ',', x) FROM ground_items WHERE item_id = 340001"); }
        Check(saved == "1,500100", "the camp is saved with the ground (" + saved + ")");
        g.Send(0x02, Coord(me, 1000, 2000, 30));
        Drain(g);
    }

    static uint MoveTests(Conn g, uint me, uint backpack, uint bank, uint hangar, uint weared, uint vehicle, uint yarn)
    {
        // Split 4 of 10 into the bank: 0x0201 with the new item's description.
        g.Send(0x17, Move(7, me, yarn, 0x13, backpack, bank, 110001, 110002, 4, 0xFF, 0xFF));
        var r = g.Recv(); var rr = new R(r.Item2);
        uint kind = rr.U32(); rr.Pos = 32; uint created = rr.U32();
        Check(r.Item1 == 0x8017 && kind == 0x02010002 && created != 0 && created != yarn, "split 4 yarn into the bank (0x0201)");
        g.Send(0x16, new B().U32(0x00020000).U32(me).U32(bank).U32(0x14).U32(0).U32(0).U32(110002).U32(0).U32(0xB).Byte(0xFF).Get());
        var bk = new R(g.Recv(true).Item2); bk.Pos = 36; bk.Size(); bk.Pos += 28 + 6; int kids = bk.Size();
        Check(kids == 1 && bk.U32() == created, "bank now lists the new stack");

        // Move the other 6: merges into the bank stack, 0x0301.
        g.Send(0x17, Move(7, me, yarn, 0x13, backpack, bank, 110001, 110002, 6, 0xFF, 0xFF));
        r = g.Recv(); rr = new R(r.Item2); kind = rr.U32(); rr.Pos = 32; uint target = rr.U32();
        Check(kind == 0x03010002 && target == created, "the rest merges into the bank stack (0x0301)");
        g.Send(0x16, new B().U32(0x00020000).U32(me).U32(created).U32(0x13).U32(bank).U32(0x14).U32(240000).U32(110002).U32(0xB).Byte(0xFF).Get());
        var st = new R(g.Recv(true).Item2); st.Pos = 36; st.Size(); st.U32(); st.U32(); st.U32();
        Check(st.U32() == 10, "bank stack holds 10");

        // Move the whole stack back: 0x0101.
        g.Send(0x17, Move(7, me, created, 0x13, bank, backpack, 110002, 110001, 10, 0xFF, 0xFF));
        r = g.Recv(); Check(new R(r.Item2).U32() == 0x01010002 && r.Item2.Length == 63, "whole stack moves back (0x0101, 63 bytes like the official reply)");

        // Get in the hangar vehicle.
        g.Send(0x17, Move(8, me, vehicle, 0x14, hangar, weared, 110003, 120001, 1, 0xFF, 0x00));
        r = g.Recv(); rr = new R(r.Item2);
        Check(r.Item1 == 0x8017 && rr.U32() == 0x01020002 && r.Item2.Length == 63, "get in the vehicle (0x0102)");
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        var looks = g.Recv();
        Check(looks.Item1 == 0x800A && Conn.Hex(looks.Item2).StartsWith("00 04 00 02") && looks.Item2.Length == 17, "looks are the vehicle's: " + Conn.Hex(looks.Item2));
        g.Send(0x16, new B().U32(0x00020000).U32(me).U32(weared).U32(0x14).U32(0).U32(0).U32(120001).U32(0).U32(0xB).Byte(0xFF).Get());
        var w = new R(g.Recv(true).Item2); w.Pos = 36; w.Size(); w.Pos += 28 + 6; w.Size();
        Check(w.U32() == vehicle, "weared slot 0 holds the vehicle");

        // Get out again.
        g.Send(0x17, Move(9, me, vehicle, 0x14, weared, hangar, 120001, 110003, 1, 0x00, 0xFF));
        r = g.Recv(); Check(new R(r.Item2).U32() == 0x01030002, "put the vehicle back (0x0103)");
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(g.Recv().Item2).StartsWith("00 03 00 02"), "looks are human again");
        return created;
    }

    static byte[] Drop(ushort op, uint me, uint item, uint fmt, uint container, uint amount, uint stat, uint cstat, uint confirm, int x, int y, byte last)
    {
        return new B().U16(op).U16(0).U32(me).U32(item).U32(fmt).U32(container).U32(container == 0 ? 0u : 0x14u).Bytes(new byte[20])
            .U32(amount).U32(stat).U32(cstat).U32(confirm).I32(x).I32(y).I32(30).Bytes(new byte[] { 1, 2, 3, 4, 5, 6 }).Byte(last).Get();
    }

    static byte[] Pick(ushort op, uint me, uint item, uint fmt, uint dest, uint amount, uint stat, uint dstat, byte last)
    {
        return new B().U16(op).U16(0).U32(me).U32(item).U32(fmt).U32(dest).U32(0x14).Bytes(new byte[28])
            .U32(amount).U32(stat).U32(dstat).U32(1).Byte(last).Get();
    }

    // The test database: DBSOCK (a local server's socket), else DBHOST over TCP (default 127.0.0.1).
    static string DbArgs()
    {
        var sock = Environment.GetEnvironmentVariable("DBSOCK");
        var host = Environment.GetEnvironmentVariable("DBHOST");
        return "-uroot -ptitans " + (!string.IsNullOrEmpty(sock) ? "--socket=" + sock : "--protocol=TCP -h " + (string.IsNullOrEmpty(host) ? "127.0.0.1" : host) + " -P 3306");
    }

    static string Sql(string query)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("mariadb", DbArgs() + " -N -B titans-server -e \"" + query + "\"") { UseShellExecute = false, RedirectStandardOutput = true };
        var proc = System.Diagnostics.Process.Start(psi);
        var output = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();
        return output;
    }

    // A refused item or shop request is answered with its reply carrying 0x000C (the client counts its requests in
    // flight and sends no more until each is answered): 0x8021 in full (45 bytes, byte 0 not 0), 0x8025 with a u32
    // result, the others as an echo. Then nothing else comes.
    static bool Refused(Conn c, uint op)
    {
        var r = c.Recv();
        while (r != null && (r.Item1 == 0x8076 || r.Item1 == 0x8005)) r = c.Recv();
        if (r == null || r.Item1 != op) { Console.WriteLine("  expected a refused 0x{0:X}, got {1}", op, r == null ? "nothing" : "0x" + r.Item1.ToString("X")); return false; }
        bool coded = op == 0x8025 ? r.Item2.Length == 12 && r.Item2[7] == 0x0C : r.Item2[2] == 0 && r.Item2[3] == 0x0C;
        if (op == 0x8021) coded = coded && r.Item2.Length == 45 && r.Item2[0] != 0;
        return coded && NoReply(c);
    }

    // Nothing comes back for a refused request: the next reply is the server time.
    // Town war events (0x8076) go to everyone at any time and are skipped.
    static bool NoReply(Conn c)
    {
        c.Send(0x13, new B().U32(0).I32(-1).Byte(0).Get());
        var r = c.Recv();
        while (r != null && r.Item1 == 0x8076) r = c.Recv();
        return r != null && r.Item1 == 0x8013;
    }

    // Shops: town 25 shop 101 is the EF weapon shop of the official captures, town 51 shop 7 the Zeon one (Zeon).
    const uint EfTown = 25, EfShop = 101, ZeonTown = 51, ZeonShop = 7;

    static byte[] Buy(ushort service, uint me, uint dest, uint amount, uint stat, int ta = -1, int tb = -1, uint town = EfTown, uint shop = EfShop)
    {
        return new B().U16(service).U16(0).U32(me).Bytes(new byte[8]).U32(dest).U32(0x14).U32(0).U32(amount).U32(stat)
            .I32(-1).U32(1).U32(town).U32(shop).I32(ta).I32(tb).U32(0xFFFF).Byte(0x81).Byte(0).I32(1000).I32(2000).I32(30).U16(0xFFFF).Byte(2).Byte(7).Bytes(new byte[3]).Get();
    }

    static byte[] Sell(uint me, uint stat, uint item, uint container, uint cstat, uint amount, uint town = EfTown, uint shop = EfShop)
    {
        return new B().U16(1).U16(0).U32(me).U32(7).U32(stat).U32(item).U32(0x13).U32(container).U32(0x14).U32(cstat)
            .U32(0xB).U32(0).U32(amount).U32(0).U32(0).U32(town).U32(shop).U32(1).Get();
    }

    static Conn FlightTests(Conn g, LobbyResult a, uint me)
    {
        uint weared = me + 120001;
        // Shuttles leave from the faction's spaceport (EF: Perth, 55456000, -58372000).
        g.Send(0x40, new B().U32(a.Key).U32(me).U16(2).Get());
        Check(NoReply(g), "a flight to Space far from Perth Spaceport is refused");
        g.Send(0x02, Coord(me, 55456000 + 3000, -58372000, 0));
        // As Earth_To_Space.pcap: 0x40 first, then the client buys its FREIGHTER, flies, and leaves with 0x42.
        g.Send(0x40, new B().U32(a.Key).U32(me).U16(2).Get());
        var r = RecvOp(g, 0x8040); var rr = new R(r.Item2); uint head = rr.U16(); uint who = rr.U32(); int cluster = rr.U16(); int unk = rr.U16();
        int n = rr.Size(); string ip = Encoding.ASCII.GetString(r.Item2, rr.Pos, n); rr.Pos += n; int port = rr.U16();
        Check(r.Item1 == 0x8040 && head == 2 && who == me && cluster == 2 && unk == 0x17BF && ip == "127.0.0.1" && port == 42011 && r.Item2.Length == 13 + ip.Length,
            "cleared for Space: 0x8040 (00 02, char, 00 02, 17 BF, as official) sends the Space server, 127.0.0.1:42011");
        Check(Sql("SELECT zone FROM characters WHERE char_name = 'Amuro'") == "1", "still on Earth until the client leaves");
        // A failed purchase gives the flight up (0x43), then the client asks again.
        g.Send(0x43, new B().U32(a.Key).U32(me).Byte(0xFF).Get());
        var cancel = RecvOp(g, 0x8043);
        Check(cancel.Item1 == 0x8043 && cancel.Item2.Length == 28 && Conn.Hex(cancel.Item2).StartsWith("00 01 00 02"), "0x43 cancels the flight (0x8043 00 01 00 02, 28 bytes)");
        g.Send(0x40, new B().U32(a.Key).U32(me).U16(2).Get());
        Check(RecvOp(g, 0x8040).Item1 == 0x8040, "cleared again");

        g.Send(0x21, Buy(3, me, weared, 1, 400020, 1, 0x31));
        r = RecvOp(g, 0x8021); rr = new R(r.Item2); rr.Pos = 8; uint shuttle = rr.U32(); rr.Pos = 28;
        Check(r.Item1 == 0x8021 && r.Item2[1] == 3 && rr.U32() == 2000, "then buy the FREIGHTER for 2000 and ride it");
        g.Send(0x02, Coord(me, 1200, 2200, 500));
        g.Send(0x42, new B().U32(me).U32(2).U32(a.Key).Byte(0x80).U16(0).Byte(0x80).Get());
        Check(g.Recv().Item1 == 0x8042, "leave the Earth server");
        g.Close();
        System.Threading.Thread.Sleep(300);

        var bad = GameLogin("Amuro", a, a.Key);
        var refused = bad.Recv();
        bad.Close();
        var space = new LobbyResult { Key = a.Key, Acc = a.Acc, IP = ip, Port = port };
        space.Chars.AddRange(a.Chars);
        var s2 = GameLogin("Amuro", space, a.Key);
        var welcome = s2.Recv();
        Check(welcome.Item1 == 0x8041, "log in to the Space server with the same key after the flight");
        Check(refused.Item1 == 0x8041 && Conn.Hex(refused.Item2) != Conn.Hex(welcome.Item2), "the Earth server refuses Amuro, who is in Space now");
        s2.Send(0x5F, new B().U32(0).U32(me).Byte(1).Get());
        r = s2.Recv(); var body = r.Item2; int end = body.Length;
        rr = new R(body) { Pos = end - 22 };
        int ta = rr.I32(), tb = rr.I32(); rr.U16(); int tx = rr.I32(), ty = rr.I32();
        var pos = new R(body) { Pos = end - 22 - 20 };
        int zone = pos.U16(); uint px = pos.U32();
        Check(r.Item1 == 0x805F && ta == 0x30 && tb == 1 && tx == 1000 && ty == 2000 && zone == 2 && px == 0x7FFFFFFF,
            "0x805F: in Space, position unknown, destination ISAEO 29 (48, the EF route, not the 0x31 the client sent) then 1 (launch), take-off point");
        Check(Sql("SELECT CONCAT(x, ',', y) FROM characters WHERE char_name = 'Amuro'") == "-3424000,5688000",
            "Amuro is saved at ISAEO 29, the EF shuttle's destination");
        s2.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        var looks = s2.Recv(); rr = new R(looks.Item2) { Pos = 8 };
        Check(Conn.Hex(looks.Item2).StartsWith("00 04 00 02") && rr.U32() == 400020, "still in the shuttle on arrival (saved as the piloted vehicle by the Earth server)");
        s2.Send(0x00, Coord(me, 6000000, -2700000, -172));
        Check(s2.Recv().Item1 == 0x8000, "arrive in Space");
        s2.Send(0x15, new B().U16(3).U16(0).U32(me).U32(shuttle).U32(0x14).U32(weared).U32(0x14).U32(400020).U32(120001).U32(1).Byte(0).Get());
        Check(s2.Recv().Item1 == 0x8015, "throw the shuttle away");
        s2.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(s2.Recv().Item2).StartsWith("00 03 00 02"), "on foot in Space");
        return s2;
    }

    // Children (uid, format, static) of a node, read with 0x16.
    static List<uint[]> Children(Conn g, uint me, uint uid, uint fmt, uint parent, uint parentFmt, uint stat, uint parentStat)
    {
        g.Send(0x16, new B().U32(0x00020000).U32(me).U32(uid).U32(fmt).U32(parent).U32(parentFmt).U32(stat).U32(parentStat).U32(0xB).Byte(0xFF).Get());
        var r = new R(RecvOp(g, 0x8016).Item2);
        r.Pos = 36; r.Size();
        r.U32(); uint f = r.U32(); r.U32(); r.U32(); r.U32(); r.U32(); r.U32();
        for (int i = 0; i < (int)f - 14; i++) { int os = r.Size(); r.Pos += (i == 5 && os > 0) ? os * 4 : os; }
        int kids = r.Size();
        var list = new List<uint[]>();
        for (int i = 0; i < kids; i++) list.Add(new uint[] { r.U32(), r.U32(), r.U32() });
        return list;
    }

    static Tuple<uint, byte[]> RecvOp(Conn c, uint op)
    {
        for (int i = 0; i < 10; i++) { var r = c.Recv(true); if (r.Item1 == op) return r; Console.WriteLine("  (skipped {0:X})", r.Item1); }
        return Tuple.Create(0u, new byte[0]);
    }

    // Mining with a mining drill (280147) on block 13 of the Canberra mine (iron ore, no skill needed), from a
    // ThunderGoliath (the one vehicle whose slot 0 takes a drill). Amuro throws it away afterwards.
    static void MiningTests(Conn g, uint me, uint weared)
    {
        g.Send(0x21, Buy(4, me, weared, 1, 400017));
        var r = g.Recv(); var rr = new R(r.Item2); rr.Pos = 8; uint gol = rr.U32();
        var kids = Children(g, me, gol, 0x14, weared, 0x14, 400017, 120001);
        uint ginv = 0; foreach (var k in kids) if (k[2] == 110010) ginv = k[0];
        Check(r.Item1 == 0x8021 && ginv != 0, "Amuro rides a ThunderGoliath away");
        Func<uint, int, byte[]> dig = (tool, block) => new B().U16(1).U16(0).U32(me).U16(1).U32((uint)block).U32(gol).U32(0x14).U32(400017).U32(tool).U32(0x13).Byte(0).Size(0).Get();
        g.Send(0x21, Buy(1, me, ginv, 1, 280147));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint drill = rr.U32();
        g.Send(0x1B, new B().U16(2).U16(0).U32(me).Bytes(new byte[8]).Size(1).U16(1).Byte(1).Byte(3).U32(drill).U32(0x13).Get());
        Check(r.Item1 == 0x8021 && Refused(g, 0x801B), "the drill does not fit its slot 1 either");
        g.Send(0x1B, new B().U16(2).U16(0).U32(me).Bytes(new byte[8]).Size(1).U16(1).Byte(0).Byte(3).U32(drill).U32(0x13).Get());
        Check(g.Recv().Item1 == 0x801B, "mining drill equipped in slot 0");
        g.Send(0x32, dig(drill, 9999));
        r = g.Recv();
        Check(r.Item1 == 0x8032 && r.Item2.Length == 36 && r.Item2[3] == 0x2E && r.Item2[34] == 1 && r.Item2[35] == 0x80, "no mine block there: 0x8032 code 0x2E, durability 1, no items");
        g.Send(0x32, dig(drill, 13));
        r = g.Recv();
        byte code = r.Item2.Length > 3 ? r.Item2[3] : (byte)0xFF;
        bool okLen = code == 2 ? r.Item2.Length == 60 && r.Item2[35] == 0x81 && new R(r.Item2) { Pos = 44 }.U32() == 530001 : r.Item2.Length == 36;
        Check(r.Item1 == 0x8032 && (code == 2 || code == 0x0C) && okLen && new R(r.Item2) { Pos = 14 }.U32() == gol,
            "mining block 13: code " + code + (code == 2 ? " (" + new R(r.Item2) { Pos = 52 }.U32() + " x " + new R(r.Item2) { Pos = 44 }.U32() + ")" : ""));
        g.Send(0x32, dig(drill, 13));
        r = g.Recv();
        Check(r.Item1 == 0x8032 && r.Item2[3] == 0x0C, "mining again at once fails (0x0C)");
        g.Send(0x15, new B().U16(3).U16(0).U32(me).U32(gol).U32(0x14).U32(weared).U32(0x14).U32(400017).U32(120001).U32(1).Byte(0).Get());
        Check(g.Recv().Item1 == 0x8015, "Amuro throws the ThunderGoliath away");
    }

    // Char buys a hover truck and a tank/fighter machine gun, equips and loads it, and shoots Amuro's freighter to pieces.
    static uint[] CombatTests(Conn g, Conn g2, uint accB, uint me, uint other)
    {
        uint oWeared = other + 120001, weared = me + 120001;
        g2.Send(0x21, Buy(4, other, oWeared, 1, 460004));
        var r = g2.Recv(); var rr = new R(r.Item2); rr.Pos = 8; uint car = rr.U32();
        Check(r.Item1 == 0x8021 && r.Item2[0] == 2, "Char rides a hover truck away");
        var kids = Children(g2, other, car, 0x14, oWeared, 0x14, 460004, 120001);
        uint cinv = 0, carms = 0;
        foreach (var k in kids) { if (k[2] == 110010) cinv = k[0]; if (k[2] == 120002) carms = k[0]; }
        Check(cinv != 0 && carms != 0, "the hover truck has an inventory and an armaments container");

        g2.Send(0x21, Buy(1, other, cinv, 1, 280014, town: ZeonTown, shop: ZeonShop));
        r = g2.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint mg = rr.U32();
        Check(r.Item1 == 0x8021 && r.Item2.Length == 111, "machine gun bought into the hover truck's inventory");
        var tail = Conn.Hex(r.Item2);
        Check(tail.Contains("80 80 80 80 81 00 00 00 F0 87 00 00 01 F4 00 00 01 F4 00 00 00 14"), "it comes with 240 rounds and stats [500, 500, 20, ...]");
        g2.Send(0x21, Buy(1, other, cinv, 100, 540005, town: ZeonTown, shop: ZeonShop));
        r = g2.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint ammo = rr.U32();
        Check(r.Item1 == 0x8021, "100 cartridges bought into the hover truck");

        g2.Send(0x1B, new B().U16(2).U16(0).U32(other).Bytes(new byte[8]).Size(1).U16(1).Byte(0).Byte(3).U32(mg).U32(0x13).Get());
        r = g2.Recv();
        Check(r.Item1 == 0x801B && r.Item2.Length == 29 && r.Item2[3] == 2, "equip the machine gun in slot 0 (0x801B echo)");
        kids = Children(g2, other, carms, 0x14, car, 0x14, 120002, 460004);
        Check(kids.Count == 1 && kids[0][0] == mg, "the armaments container lists the machine gun");
        g2.Send(0x1D, new B().U16(1).U16(0).U32(other).U32(ammo).U32(0x13).U32(cinv).U32(0x14).U32(mg).U32(0x13).U32(0).U32(100).Get());
        r = g2.Recv();
        Check(r.Item1 == 0x801D && r.Item2[3] == 2, "reload 100 rounds (0x801D echo)");
        g2.Send(0x1D, new B().U16(1).U16(0).U32(other).U32(ammo).U32(0x13).U32(cinv).U32(0x14).U32(mg).U32(0x13).U32(0).U32(1).Get());
        Check(Refused(g2, 0x801D), "the cartridges are used up");
        Check(Sql("SELECT child FROM container WHERE char_id = " + other + " AND container_id = 500002").Contains("@0-280014~l340"),
            "the machine gun's 340 loaded rounds are saved with the hover truck (@0-280014~l340)");

        g.Send(0x0A, new B().U32(0).U32(other).Byte(5).Get());
        r = g.Recv();
        Check(r.Item1 == 0x800A && Conn.Hex(r.Item2).Contains("00 04 45 CE"), "Amuro sees the machine gun on Char's hover truck");

        // Amuro's target: a freighter with an emergency kit.
        g.Send(0x21, Buy(4, me, weared, 1, 400020));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint fr = rr.U32();
        kids = Children(g, me, fr, 0x14, weared, 0x14, 400020, 120001);
        uint finv = 0; foreach (var k in kids) if (k[2] == 110010) finv = k[0];
        g.Send(0x21, Buy(1, me, finv, 1, 310000));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint kit = rr.U32();
        Check(r.Item1 == 0x8021 && kit != 0, "Amuro rides a freighter with an emergency tool kit");

        // A ZAKU DESERTTYPE (2000 health) explodes next to the freighter: a tenth of that.
        g.Send(0x12, new B().U32(me).U32(fr).U32(0x14).U32(410015).Get());
        r = g.Recv(); rr = new R(r.Item2);
        Check(r.Item1 == 0x8012 && r.Item2.Length == 19 && rr.U16() == 2 && rr.U32() == me && rr.Byte() == 0 && rr.I32() == 200 && rr.U32() == fr,
            "chain explosion: 0x8012 with 200 damage to the freighter");
        g.Send(0x12, new B().U32(me).U32(12345).U32(0x14).U32(410015).Get());
        Check(NoReply(g), "chain explosion on a vehicle they are not in is refused");

        // Char repairs it with an MR tool kit Lv.3 (280169) in the hover truck's slot 1.
        g2.Send(0x69, new B().U32(other).U32(me).U32(fr).Byte(1).U16(0xFFFF).Get());
        Check(NoReply(g2), "repairing without an MR tool kit is refused");
        g2.Send(0x21, Buy(1, other, cinv, 1, 280169, town: ZeonTown, shop: ZeonShop));
        r = g2.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint mr = rr.U32();
        Check(r.Item1 == 0x8021 && mr != 0, "MR tool kit bought into the hover truck");
        g2.Send(0x1B, new B().U16(2).U16(0).U32(other).Bytes(new byte[8]).Size(1).U16(1).Byte(1).Byte(3).U32(mr).U32(0x13).Get());
        Check(g2.Recv().Item1 == 0x801B, "MR tool kit equipped in slot 1");
        g2.Send(0x69, new B().U32(other).U32(me).U32(fr).Byte(1).U16(0xFFFF).Get());
        var rp = RecvOp(g2, 0x8069); var rpr = new R(rp.Item2);
        uint rsrc = rpr.U32(), rtgt = rpr.U32(); rpr.Pos = 12; int ramount = rpr.I32();
        Check(rp.Item2.Length == 50 && rsrc == other && rtgt == me && ramount == 200 && rp.Item2[49] == 0 &&
            new R(rp.Item2) { Pos = 33 }.U32() == 280169 && new R(rp.Item2) { Pos = 37 }.U32() == fr, "repair: 0x8069 to Char, 200 repaired, 0% damage left");
        Check(RecvOp(g2, 0x806A).Item2.Length == 28, "0x806A to those around (Char)");
        Check(RecvOp(g, 0x8069).Item2.Length == 50, "0x8069 to Amuro too");
        Check(RecvOp(g, 0x806A).Item2.Length == 28, "0x806A to Amuro");

        // Mining with a mining drill (280147) on block 13 of the Canberra mine (iron ore, no skill needed). The
        // client only sends it from a vehicle that can mine; the server checks the drill.
        Func<uint, int, byte[]> dig = (tool, block) => new B().U16(1).U16(0).U32(other).U16(1).U32((uint)block).U32(car).U32(0x14).U32(460004).U32(tool).U32(0x13).Byte(0).Size(0).Get();
        g2.Send(0x32, dig(mr, 13));
        r = g2.Recv();
        Check(r.Item1 == 0x8032 && r.Item2.Length == 36 && r.Item2[3] == 1, "mining with the MR tool kit is an error (code 1)");
        g2.Send(0x1B, new B().U16(2).U16(0).U32(other).Bytes(new byte[8]).Size(1).U16(2).Byte(1).Byte(3).U32(0).U32(0).Get());
        g2.Recv();
        g2.Send(0x1B, new B().U16(2).U16(0).U32(other).Bytes(new byte[8]).Size(1).U16(1).Byte(3).Byte(3).U32(mr).U32(0x13).Get());
        Check(Refused(g2, 0x801B), "nor does the MR tool kit in slot 3, which the hover truck does not have");

        // Lock on and fire effect.
        g2.Send(0x39, new B().U32(0x8010).Size(1).U32(me).U32(280014).U32(other).Byte(0xFF).Get());
        r = g.Recv();
        Check(r.Item1 == 0x8010 && Conn.Hex(r.Item2).Trim() == "00 04 45 CE " + Conn.Hex(new B().U32(other).Get()).Trim() + " FF", "Amuro is told Char locked on");
        Check(NoReply(g2), "no reply to the locker");
        var effect = new byte[36]; effect[3] = 2;
        g2.Send(0x3A, new B().U32(accB).U32(other).U16(1).Bytes(new byte[6]).I32(1500).I32(2500).I32(30).U32(0x453B8000).U32(0x803B).Bytes(effect).Get());
        Check(g.Recv().Item1 == 0x803B && g2.Recv().Item1 == 0x803B, "the fire effect reaches both, the shooter included");

        bool destroyed = false, repairedOnce = false; int attacks = 0, hits = 0, lastPct = 0;
        while (!destroyed && attacks < 400)
        {
            attacks++;
            if (attacks > 60 && attacks % 10 == 0)
            {
                Drain(g2); Drain(g);
                Reload(g2, other, new uint[] { car, cinv, mg });
            }
            g2.Send(0x0F, new B().U32(other).U32(me).U32(fr).Byte(0).I32(30).U16(0xFFFF).I32(1000).I32(2000).I32(30).Bytes(new byte[10]).Get());
            var a1 = g2.Recv(true); var a2 = g2.Recv(true);
            var t1 = g.Recv(true); var t2 = g.Recv(true);
            if (!(a1.Item1 == 0x800F && a1.Item2.Length == 59 && a2.Item1 == 0x8036 && a2.Item2.Length == 22 && t1.Item1 == 0x800F && t2.Item1 == 0x8036))
            {
                Check(false, "attack " + attacks + " gives 0x800F + 0x8036 to both: " + a1.Item1.ToString("X") + " " + a2.Item1.ToString("X") + " " + t1.Item1.ToString("X") + " " + t2.Item1.ToString("X"));
                return null;
            }
            byte result = a1.Item2[13], explosion = a1.Item2[18], pct = a1.Item2[58];
            if (result != 6) { hits++; lastPct = pct; }
            if (result != 6 && !repairedOnce)
            {
                repairedOnce = true;
                Check(pct > 0 && a1.Item2[19] == 0 && new R(a1.Item2) { Pos = 46 }.U32() == fr, "a hit damages the freighter (" + pct + "%)");
                g.Send(0x1C, new B().U16(6).U16(0).U32(me).U32(kit).U32(0x13).U32(finv).U32(0x14).U32(0).U32(1).Get());
                var er = g.Recv(); var err = new R(er.Item2) { Pos = 40 };
                Check(er.Item1 == 0x801C && er.Item2[3] == 2 && err.Size() == 6 && err.U32() > 0 && new R(er.Item2) { Pos = 8 }.U32() == fr, "the emergency tool kit repairs it (0x801C success)");
            }
            if (explosion == 1)
            {
                destroyed = true;
                Check(pct == 100 && a2.Item2[16] == 1 && a2.Item2[21] == 100, "destroyed: explosion 1 and 100% in both results");
            }
        }
        Console.WriteLine("  {0} attacks, {1} hits", attacks, hits);
        Check(destroyed, "Char destroys Amuro's freighter");
        if (!destroyed) return null;
        var w1 = g.Recv(); var w2 = g2.Recv();
        var wr = new R(w1.Item2);
        uint action = wr.U32(); uint wuid = wr.U32(); wr.Pos = 4 + 47; int health = wr.I32();
        Check(w1.Item1 == 0x8035 && action == 1 && wuid == fr && health == 0 && w2.Item1 == 0x8035, "the wreck lies on the ground (0x8035 action 1, health 0) for both");
        g.Send(0x24, Pick(3, me, fr, 0x14, weared, 1, 400020, 120001, 0));
        Check(Refused(g, 0x8024), "nobody can get in the wreck");
        g.Send(0x1C, new B().U16(6).U16(0).U32(me).U32(kit).U32(0x13).U32(finv).U32(0x14).U32(0).U32(1).Get());
        var fail = g.Recv();
        Check(fail.Item1 == 0x801C && fail.Item2[3] == 0x0C, "no vehicle left to repair (0x801C failure)");
        MiningTests(g, me, weared);
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(g.Recv().Item2).StartsWith("00 03 00 02"), "Amuro is on foot");
        return new uint[] { car, cinv, mg };
    }

    // The skill changes (0x8034) a player received: "table.index" or "status.n" -> total change. Bad packets (other
    // character, wrong length) count as "bad".
    static Dictionary<string, int> GainsOf(Conn c, uint id)
    {
        var total = new Dictionary<string, int>();
        foreach (var body in c.Gains)
        {
            var r = new R(body);
            string key;
            if (r.U32() != id || r.Size() != 1) { total["bad"] = 1; continue; }
            r.I32();
            int n = r.Size();
            for (int i = 0; i < n; i++) { key = "status." + r.Byte(); int v = r.I32(); r.Byte(); total[key] = (total.ContainsKey(key) ? total[key] : 0) + v; }
            n = r.Size();
            for (int i = 0; i < n; i++) { key = r.Byte() + "." + r.Byte(); int v = r.I32(); r.Byte(); total[key] = (total.ContainsKey(key) ? total[key] : 0) + v; }
            if (r.Size() != 0 || r.Left != 0) total["bad"] = 1;
        }
        return total;
    }

    static string Show(Dictionary<string, int> d) { return string.Join(", ", d.Select(kv => kv.Key + " " + kv.Value)); }

    // Skill growth from the combat, repairs and mining so far, and the Status Setting arrows.
    static void SkillTests(Conn g, Conn g2, uint me, uint other)
    {
        Drain(g); Drain(g2);
        var amuro = GainsOf(g, me); var chr = GainsOf(g2, other);
        Console.WriteLine("  Amuro's gains: " + Show(amuro));
        Console.WriteLine("  Char's gains: " + Show(chr));
        Check(!amuro.ContainsKey("bad") && !chr.ContainsKey("bad"), "every 0x8034 is well formed and for the player's own character");
        Check(amuro.ContainsKey("0.20") && amuro["0.20"] >= 1, "Amuro's ER kit raised emergency repair (0x8034 table 0 index 20)");
        Check(chr.ContainsKey("0.20") && chr["0.20"] >= 2, "Char's MR kit repair of Amuro raised emergency repair by 0.2");
        Check(chr.Keys.Count(k => k == "0.5" || k == "0.16" || k == "0.11" || k == "0.12" || k == "0.9") >= 2,
            "Char's hits raised ground engagement, tactics, weapon manipulation, shooting or shell firing");
        Check(amuro.ContainsKey("0.17") || amuro.ContainsKey("0.19"), "the hits Amuro took raised AMBAC or evasion");
        int tactics = chr.ContainsKey("0.16") ? chr["0.16"] : 0;
        Check(Sql("SELECT skill_level FROM skills WHERE char_id = " + other + " AND skill_idx = 15") == tactics.ToString(), "Char's tactics is saved (" + tactics + ")");

        // Arrows: lock AMBAC, lower mining; bad entries are refused.
        g.Send(0x0B, new B().U32(me).Size(2).Byte(0).Byte(1).Size(2).Byte(17).Byte(0).Size(2).Byte(2).Byte(1).Get());
        var r = RecvOp(g, 0x800B);
        Check(r.Item2.Length == 28 && Conn.Hex(r.Item2).StartsWith("00 1A 00 02"), "0x0B: skill arrows accepted (0x800B code 2)");
        var mgmt = Sql("SELECT management FROM character_state WHERE char_id = " + me);
        Check(mgmt.Length == 27 && mgmt[18] == '2' && mgmt[25] == '1' && mgmt[3] == '0', "AMBAC locked and mining lowered in character_state (" + mgmt + ")");
        g.Send(0x0B, new B().U32(me).Size(1).Byte(0).Size(1).Byte(30).Size(1).Byte(2).Get());
        r = RecvOp(g, 0x800B);
        Check(r.Item2.Length == 28 && r.Item2[3] != 2, "an arrow for a skill that does not exist is refused (code " + r.Item2[3] + ")");
        g.Send(0x0C, new B().U32(me).Size(1).Byte(1).Size(1).Byte(2).Get());
        r = RecvOp(g, 0x800C);
        Check(r.Item2.Length == 28 && r.Item2[3] == 2 && Sql("SELECT management FROM character_state WHERE char_id = " + me)[1] == '2', "0x0C: spirit locked (0x800C code 2)");

        // 0x0D in the hover truck: answered, but Mobile Suit (index 0) is not its operation skill.
        Drain(g2);
        int before = g2.Gains.Count;
        g2.Send(0x0D, new byte[3]);
        r = RecvOp(g2, 0x800D);
        Check(r.Item2.Length == 28 && r.Item2[1] == 0x1A && r.Item2[3] == 2, "0x0D is answered with the official 0x800D");
        Drain(g2);
        Check(g2.Gains.Count == before, "Mobile Suit is not the hover truck's operation skill (" + (g2.Gains.Count - before) + " changes)");
        g2.Send(0x0D, new byte[] { 0, 0, 3 });
        RecvOp(g2, 0x800D);
        System.Threading.Thread.Sleep(300);
        Check(!GameLog().Contains("growth of table 0 index 3 refused"), "Fighter is the hover truck's operation skill (0x0D index 3 is not refused)");
        Check(GameLog().Contains("growth of table 0 index 0 refused"), "while Mobile Suit was");
    }

    // Reads until op; returns every packet read on the way (op last), or null on a timeout.
    static List<Tuple<uint, byte[]>> RecvUntil(Conn c, uint op)
    {
        var all = new List<Tuple<uint, byte[]>>();
        for (int i = 0; i < 40; i++) { var r = c.Recv(true); if (r == null) return null; all.Add(r); if (r.Item1 == op) return all; }
        return null;
    }

    static uint[] CityInfo(Conn k, uint kai, int city)
    {
        k.Send(0x70, new B().U32(0).U32(kai).Byte(0xFF).Get());
        var r = RecvOp(k, 0x8070); var rr = new R(r.Item2); int n = rr.Size();
        for (int i = 0; i < n; i++)
        {
            uint id = rr.U32(); uint owner = rr.U16(); uint status = rr.U32(); rr.U32(); int m = rr.Size();
            var icf = new uint[m]; for (int j = 0; j < m; j++) icf[j] = rr.U16(); rr.U32();
            if (id == city) return new[] { owner, status }.Concat(icf).ToArray();
        }
        return null;
    }

    // Char (Zeon) attacks Newman (Federation), starts the war, registers and takes all five ICFs
    // (OccupationPeaceMinutes 0 and OccupationCaptureSeconds 3 in the test GameServer.xml).
    static void OccupationTests(Conn g, Conn g2, Conn k, uint me, uint other, uint kai, uint[] chr)
    {
        Drain(g); Drain(g2); Drain(k);
        var info = CityInfo(k, kai, 59);
        Check(info != null && info[0] == 1 && info[1] == 1 && info.Skip(2).All(f => f == 1), "Newman is the Federation's and open to attack (0x8070: " + (info == null ? "none" : string.Join(",", info)) + ")");
        info = CityInfo(k, kai, 58);
        Check(info != null && info[0] == 2, "Richmond is Zeon's");

        int tx = 56944253, ty = -54458061, tz = 2260;
        g2.Send(0x02, Coord(other, tx, ty, tz));
        g2.Send(0x05, new B().I32(tx).I32(ty).I32(tz).U32(0x457A0000).U32(2).Get());
        var gl = RecvOp(g2, 0x8005); var lr = new R(gl.Item2) { Pos = 3 }; int count = lr.Size();
        var towers = new List<uint>();
        for (int i = 0; i < count; i++)
        {
            var rec = new R(gl.Item2) { Pos = lr.Pos + 66 * i };
            uint uid = rec.U32(); rec.U32(); uint tpl = rec.U32(); rec.Pos += 12 + 6 + 12 + 1 + 16 + 2;
            if (tpl == 340004 && rec.Size() == 1 && rec.U32() == (59u << 16 | (uint)towers.Count)) towers.Add(uid);
        }
        Check(count == 1 && towers.Count == 1, "Char sees Newman's first laser communication tower, 500 HP, marked town 59 tower 0 (the others stand 20000 away; " + count + " items)");
        if (towers.Count == 0) return;

        Reload(g2, other, chr);
        bool hit = false; int shots = 0; List<Tuple<uint, byte[]>> got = null;
        while (!hit && shots < 30)
        {
            shots++;
            g2.Send(0x11, new B().U32(other).Byte(0).Byte(0).U16(0xFFFF).U32(towers[0]).U32(0x14).U32(340004).Get());
            var r = RecvOp(g2, 0x8011);
            if (r.Item1 != 0x8011) break;
            if (new R(r.Item2) { Pos = 8 }.U32() == 0) continue;
            hit = true;
            got = RecvUntil(g2, 0x8076);
        }
        Check(hit, "Char hits a tower (" + shots + " shots)");
        var ev = got != null ? got.Last().Item2 : new byte[0];
        var er = new R(ev);
        Check(ev.Length == 23 && er.U32() == 0 && er.U32() == other && er.U32() == 59 && ev[12] == 0xFF && new R(ev) { Pos = 13 }.U16() == 3,
            "the hit starts the war (0x8076 type 0 by Char for Newman: " + Conn.Hex(ev) + ")");
        var ek = RecvOp(k, 0x8076);
        Check(ek.Item2.Length == 23 && ek.Item2[3] == 0, "Kai hears of the war too");

        g2.Send(0x71, new B().U32(59).Get());
        var s = RecvOp(g2, 0x8071);
        Check(s.Item2.Length == 28 && s.Item2[3] == 2, "0x71: the war is on (0x8071 code 2)");
        g2.Send(0x74, new B().U32(59).U32(other).Byte(0xFF).Get());
        s = RecvOp(g2, 0x8074);
        Check(s.Item2.Length == 28 && s.Item2[3] == 2, "0x74: Char joins the war (0x8074)");
        Drain(g);
        g.Send(0x74, new B().U32(59).U32(me).Byte(0xFF).Get());
        Check(NoReply(g), "Amuro, far away, cannot join");

        g2.Send(0x73, new B().U32(1).U32(other).U32(59).Byte(0).Get());
        s = RecvOp(g2, 0x8073);
        Check(s.Item2.Length == 28 && s.Item2[3] != 2, "an ICF right after joining is refused (code " + (s.Item2.Length > 3 ? s.Item2[3] : -1) + ")");

        var types = new List<uint>(); uint lastFaction = 0;
        for (byte i = 0; i < 5; i++)
        {
            System.Threading.Thread.Sleep(3100);
            g2.Send(0x73, new B().U32(1).U32(other).U32(59).Byte(i).Get());
            var all = RecvUntil(g2, 0x8073);
            if (all == null) { Check(false, "0x8073 for ICF " + i); return; }
            Check(all.Last().Item2[3] == 2, "0x73: Char captures ICF " + (i + 1) + " (0x8073 code " + all.Last().Item2[3] + ")");
            foreach (var e in all.Where(a => a.Item1 == 0x8076))
            {
                var x = new R(e.Item2); uint t = x.U32(); types.Add(t);
                if (t == 2) Check(x.U32() == other && x.U32() == 59 && e.Item2[12] == i && new R(e.Item2) { Pos = 13 }.U16() == 2, "0x8076 type 2: ICF " + (i + 1) + " is Zeon's");
                if (t == 1) lastFaction = new R(e.Item2) { Pos = 13 }.U16();
            }
        }
        Check(types.Count(t => t == 2) == 5 && types.Contains(1) && lastFaction == 2, "taking all five wins Newman for Zeon (0x8076 type 1, faction " + lastFaction + ")");
        info = CityInfo(k, kai, 59);
        Check(info != null && info[0] == 2 && info[1] != 2 && info.Skip(2).All(f => f == 2), "0x8070: Newman is Zeon's with every ICF (" + (info == null ? "none" : string.Join(",", info)) + ")");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT CONCAT(owner, ' ', icf) FROM occupation_city WHERE city_id = 59") == "2 2,2,2,2,2", "Newman's new owner is saved");
        var medals = Sql("SELECT medals FROM character_state WHERE char_id = " + other);
        Check(medals == "0,36", "Char has 36 Medal of Newman points: 1 for joining, 5 per ICF, 10 for the win (" + medals + ")");

        g2.Send(0x02, Coord(other, 1500, 2500, 30));
        Drain(g); Drain(g2); Drain(k);
    }

    // Kai (Zeon like Char) sits in a freighter; Char shoots it: a crime.
    static void CrimeTests(Conn g2, Conn k, uint other, uint kai, uint accK, uint[] chr)
    {
        uint kWeared = kai + 120001;
        k.Send(0x21, Buy(4, kai, kWeared, 1, 400020));
        var r = RecvOp(k, 0x8021); uint fr = new R(r.Item2) { Pos = 8 }.U32();
        Check(r.Item2[0] == 2 && fr != 0, "Kai rides a freighter");
        Reload(g2, other, chr);

        bool hit = false; int shots = 0;
        while (!hit && shots < 20)
        {
            shots++;
            g2.Send(0x0F, new B().U32(other).U32(kai).U32(fr).Byte(0).I32(300).U16(0xFFFF).I32(1000).I32(2000).I32(30).Bytes(new byte[10]).Get());
            var a1 = RecvOp(g2, 0x800F); var t1 = RecvOp(k, 0x800F);
            RecvOp(g2, 0x8036); RecvOp(k, 0x8036);
            bool miss = a1.Item2[13] == 6;
            Check(a1.Item2[12] == 0 && t1.Item2[12] == 0 && a1.Item2[15] == (miss ? 0 : 1) && t1.Item2[15] == a1.Item2[15],
                "shot " + shots + " at Kai: relation 0 (same faction), crime bit " + a1.Item2[15]);
            hit = !miss;
        }
        Check(hit, "Char hits Kai");
        System.Threading.Thread.Sleep(200);
        var scores = Sql("SELECT scores FROM character_state WHERE char_id = " + other).Split(',');
        Check(scores.Length == 10 && scores[4] == "2" && scores[5] == "1" && scores[6] == "1", "Char's criminal count 2, one previous offense, one kill (" + string.Join(",", scores) + ")");

        k.Send(0x03, new B().U32(accK).U32(kai).U16(1).Bytes(new byte[6]).I32(1000).I32(2000).I32(30).U32(0x45FA0000).Get());
        var list = RecvOp(k, 0x8003); var lr = new R(list.Item2); lr.U16(); int count = lr.Size(); byte state = 0xFF, nation = 0;
        for (int i = 0; i < count; i++) { var rr = new R(list.Item2) { Pos = lr.Pos + 53 * i + 18 }; if (rr.U32() == other) { state = list.Item2[lr.Pos + 53 * i + 38]; nation = list.Item2[lr.Pos + 53 * i + 39]; } }
        Check(state == 1 && nation == 2, "Kai sees Char as a criminal (player state 1, nationality 2)");

        k.Send(0x6F, new B().U32(0).U32(other).Get());
        var pd = RecvOp(k, 0x806F); var pr = new R(pd.Item2); pr.U32(); pr.U16(); pr.Byte(); pr.Str();
        var ints = Enumerable.Range(0, 6).Select(i => pr.I32()).ToArray();
        Check(ints[2] == 1 && ints[4] == 2 && ints[5] == 1, "Char's profile: 1 player kill, criminal count 2, previous offense 1 (" + string.Join(",", ints) + ")");

        g2.Send(0x08, new B().U32(0xFFFFFFFF).U32(other).Byte(1).Get());
        r = RecvOp(g2, 0x8008);
        Check(r.Item2.Length == 28 && Conn.Hex(r.Item2).StartsWith("00 00 00 02 00 00 00 00"), "0x08 before the 3 minutes: 0x8008 with no change");
    }

    // Reads until the answer to a server time request: whatever was queued is dropped.
    static void Drain(Conn c)
    {
        c.Send(0x13, new B().U32(0).I32(-1).Byte(0).Get());
        for (int i = 0; i < 200; i++) { var r = c.Recv(true); if (r == null || r.Item1 == 0x8013) return; }
    }

    // Buys 40 cartridges into Char's hover truck and loads the machine gun.
    static void Reload(Conn g2, uint other, uint[] chr)
    {
        g2.Send(0x21, Buy(1, other, chr[1], 40, 540005, town: ZeonTown, shop: ZeonShop));
        var r = RecvOp(g2, 0x8021); uint ammo = new R(r.Item2) { Pos = 8 }.U32();
        g2.Send(0x1D, new B().U16(1).U16(0).U32(other).U32(ammo).U32(0x13).U32(chr[1]).U32(0x14).U32(chr[2]).U32(0x13).U32(0).U32(40).Get());
        RecvOp(g2, 0x801D);
    }

    // Char keeps shooting Kai until exiled (CrimeExileCount 6 in the test GameServer.xml).
    static void ExileTest(Conn g2, Conn k, uint other, uint kai, uint[] chr)
    {
        Drain(k);
        Reload(g2, other, chr);
        bool exiled = false; int shots = 0;
        while (!exiled && shots < 40)
        {
            shots++;
            g2.Send(0x0F, new B().U32(other).U32(kai).U32(0).Byte(0).I32(300).U16(0xFFFF).I32(1000).I32(2000).I32(30).Bytes(new byte[10]).Get());
            for (int i = 0; i < 6; i++)
            {
                var r = g2.Recv(true);
                if (r == null) { exiled = false; shots = 99; break; }
                if (r.Item1 == 0x803D) { exiled = true; Check(r.Item2.Length == 9 && new R(r.Item2) { Pos = 4 }.U32() == other && r.Item2[8] == 1, "0x803D exiles Char"); break; }
                if (r.Item1 == 0x8036) break;
            }
        }
        Check(exiled, "Char is exiled after " + shots + " shots");
        System.Threading.Thread.Sleep(2500);
        for (int i = 0; i < 30 && g2.Recv(true) != null; i++) { }
        bool gone;
        try { g2.Send(0x13, new B().U32(0).I32(-1).Byte(0).Get()); gone = g2.Recv(true) == null; } catch (Exception) { gone = true; }
        Check(gone, "and disconnected");
        var scores = Sql("SELECT scores FROM character_state WHERE char_id = " + other).Split(',');
        Check(int.Parse(scores[4]) >= 6, "the criminal count stays saved (" + scores[4] + ")");
    }

    // Char flies to a Zeon squad at 30000, 30000 (test rows in bin/DB/Npcs/npcs.csv), destroys one NPC and is
    // shot at by the other.
    static void NpcTests(Conn g2, uint accB, uint other, uint[] chr)
    {
        uint target = 1000900001, shooter = 1000900002, vendor = 1000900003;
        g2.Send(0x03, new B().U32(accB).U32(other).U16(1).Bytes(new byte[6]).I32(30500).I32(30000).I32(30).U32(0x45FA0000).Get());
        var list = RecvOp(g2, 0x8003); var lr = new R(list.Item2); lr.U16(); int count = lr.Size();
        var ids = new Dictionary<uint, byte[]>();
        for (int i = 0; i < count; i++) { var rec = new byte[53]; Array.Copy(list.Item2, lr.Pos + 53 * i, rec, 0, 53); ids[new R(rec) { Pos = 18 }.U32()] = rec; }
        Check(count == 4 && ids.ContainsKey(target) && ids.ContainsKey(shooter) && ids.ContainsKey(vendor), "Char sees the three NPCs in the position list (" + count + ")");
        if (!ids.ContainsKey(target)) return;
        var t = ids[target]; var tr = new R(t) { Pos = 26 };
        uint vuid = tr.U32(); int vtpl = tr.I32(); tr.Byte(); byte acct = tr.Byte(); tr.Pos = 44;
        Check(vuid == 0 && vtpl == 410007 && acct == 0x0F && tr.I32() == 9001, "NPC record: vehicle uid 0, ZAKU2 410007, NPC tag 0x0F, squad 9001");

        g2.Send(0x06, new B().U32(1).U32(target).Byte(1).Get());
        var info = g2.Recv(); var ir = new R(info.Item2);
        Check(info.Item1 == 0x8006 && ir.U32() == 0xFFFFFFFF && ir.U32() == target && new R(info.Item2) { Pos = 10 }.Str() == "Target", "NPC name (0x8006, account FFFFFFFF)");
        g2.Send(0x0A, new B().U32(0).U32(target).Byte(5).Get());
        var looks = Conn.Hex(g2.Recv().Item2);
        Check(looks.StartsWith("00 04 00 02 3B A8 85 A1 00 06 41 97 84 00 04 45 C6 FF FF FF FF 00 04 45 C6 00 04 45 C6 84 00 00 00 00 80 00 06"), "NPC looks: ZAKU2 with 4 armament slots");

        g2.Send(0x0F, new B().U32(other).U32(vendor).U32(0).Byte(0).I32(100).U16(0xFFFF).I32(30200).I32(30000).I32(30).Bytes(new byte[10]).Get());
        Check(NoReply(g2), "vendors cannot be attacked");

        bool destroyed = false; int attacks = 0;
        var extra = new List<Tuple<uint, byte[]>>();
        while (!destroyed && attacks < 400)
        {
            attacks++;
            if (attacks % 10 == 0 && chr != null)
            {
                Reload(g2, other, chr);
            }
            g2.Send(0x0F, new B().U32(other).U32(target).U32(0).Byte(0).I32(30).U16(0xFFFF).I32(30000).I32(30000).I32(30).Bytes(new byte[10]).Get());
            Tuple<uint, byte[]> full = null, near = null;
            for (int i = 0; i < 8 && (full == null || near == null); i++)
            {
                var r = g2.Recv(true);
                if (r == null) break;
                uint tgt = r.Item2.Length >= 8 ? new R(r.Item2) { Pos = 4 }.U32() : 0;
                if (r.Item1 == 0x800F && tgt == target) full = r;
                else if (r.Item1 == 0x8036 && tgt == target) near = r;
                else extra.Add(r);
            }
            if (full == null || near == null) { Check(false, "attack on the NPC gives 0x800F + 0x8036"); return; }
            if (full.Item2[18] == 1) destroyed = true;
        }
        Console.WriteLine("  {0} attacks on the NPC", attacks);
        Check(destroyed, "Char destroys the NPC");
        Tuple<uint, byte[]> wreck = null; int loot = 0;
        for (int i = 0; i < 12 && wreck == null; i++)
        {
            var r = g2.Recv(true); if (r == null) break;
            if (r.Item1 != 0x8035) { extra.Add(r); continue; }
            if (new R(r.Item2) { Pos = 8 }.U32() == 0x14) wreck = r;
            else if (new R(r.Item2) { Pos = 4 + 38 }.U32() == 0xFFFFFFFF && r.Item2[r.Item2.Length - 1] != 2) loot++;
        }
        Check(loot == 0, "an MS NPC drops nothing on the ground (" + loot + " items): its loot is in the wreck");
        Check(wreck != null && new R(wreck.Item2).U32() == 1 && new R(wreck.Item2) { Pos = 4 + 38 }.U32() == other && new R(wreck.Item2) { Pos = 4 + 47 }.I32() == 0
            && new R(wreck.Item2) { Pos = wreck.Item2.Length - 7 }.U32() == target, "its wreck lies on the ground (0x8035 action 1, owner Char, actor the NPC)");
        if (wreck != null)
        {
            uint wuid = new R(wreck.Item2) { Pos = 4 }.U32();
            g2.Send(0x26, new B().U16(1).U16(0x0F98).U32(wuid).U32(0x14).U32(410007).U32(other).Get());
            var lockr = RecvOp(g2, 0x8026);
            Check(lockr.Item1 == 0x8026 && Conn.Hex(lockr.Item2).StartsWith("00 01 00 02") && new R(lockr.Item2) { Pos = 4 }.U32() == wuid, "Char may open the wreck (0x8026 00 01 00 02)");
            g2.Send(0x27, new B().U16(1).U16(0x0F98).U32(wuid).U32(0x14).U32(410007).U32(other).Get());
            var lst = RecvOp(g2, 0x8027); var lsr = new R(lst.Item2) { Pos = 16 }; int n = lsr.Size();
            Check(lst.Item1 == 0x8027 && n >= 1 && lst.Item2.Length == 16 + 1 + 62 * n, "the wreck lists its loot (0x8027, " + n + " items of 62 bytes)");
            bool questItem = false;
            for (int k = 0; k < n && lst.Item2.Length >= 17 + 62 * (k + 1); k++)
            {
                questItem |= new R(lst.Item2) { Pos = 17 + 62 * k + 8 }.I32() == 550029;
            }
            Check(questItem, "a quest leader's wreck holds its quest item (550029)");
            if (n >= 1)
            {
                var it = new R(lst.Item2) { Pos = 17 }; uint iuid = it.U32(); it.U32(); int itpl = it.I32(); it.Pos = 17 + 34; int iamt = it.I32();
                g2.Send(0x16, new B().U32(0x00020000).U32(other).U32(iuid).U32(0x13).U32(wuid).U32(0x14).U32((uint)itpl).U32(110010).U32(0xB).Byte(0xFF).Get());
                Check(RecvOp(g2, 0x8016).Item1 == 0x8016, "0x16 describes an item in the wreck");
                // Part of the stack with 0x17; the real client drags the rest out of the Wreckage Container with 0x24.
                int part = iamt > 1 ? iamt - 1 : iamt;
                g2.Send(0x17, Move(7, other, iuid, 0x13, wuid, other + 110001, 110010, 110001, (uint)part, 0xFF, 0xFF));
                var mv = RecvOp(g2, 0x8017);
                Check(mv.Item1 == 0x8017 && Conn.Hex(mv.Item2).StartsWith("01 01 00 02"), "Char takes " + part + " x " + itpl + " out of the wreck into the backpack (0x8017 moved)");
                Check(Sql("SELECT COUNT(*) FROM container WHERE char_id = " + other + " AND item_id = " + itpl) != "0", "and it is saved");
                if (part < iamt)
                {
                    g2.Send(0x24, Pick(1, other, iuid, 0x13, other + 110001, (uint)(iamt - part), (uint)itpl, 110001, 0xFF));
                    var pk = RecvOp(g2, 0x8024);
                    Check(pk.Item1 == 0x8024 && Conn.Hex(pk.Item2).StartsWith("00 01 00 02") && pk.Item2.Length > 69, "Char picks the last " + (iamt - part) + " out of the wreck with 0x24 (0x8024 done)");
                }
                g2.Send(0x27, new B().U16(1).U16(0x0F98).U32(wuid).U32(0x14).U32(410007).U32(other).Get());
                var lst2 = RecvOp(g2, 0x8027);
                Check(new R(lst2.Item2) { Pos = 16 }.Size() == n - 1, "the wreck has one item less");
            }
        }

        // The squad fires back: fire effect, lock on, then the result about a second later.
        var until = DateTime.Now.AddSeconds(8);
        while (DateTime.Now < until && !(extra.Exists(r => r.Item1 == 0x800F) && extra.Exists(r => r.Item1 == 0x8036)))
        {
            var r = g2.Recv(true); if (r != null) extra.Add(r);
        }
        var fx = extra.Find(r => r.Item1 == 0x803B);
        var lk = extra.Find(r => r.Item1 == 0x8010);
        var res = extra.Find(r => r.Item1 == 0x800F);
        var nr = extra.Find(r => r.Item1 == 0x8036);
        Check(fx != null && fx.Item2.Length == 36 && Conn.Hex(fx.Item2).StartsWith("00 00 00 02 FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 00 04 45 C6"), "the other NPC's fire effect (0x803B with FF x 16 and its weapon)");
        Check(lk != null && Conn.Hex(lk.Item2).Trim() == "00 00 00 00 3B A8 85 A2 01", "it locks on Char (0x8010 0, NPC, 1)");
        Check(res != null && res.Item2.Length == 59 && new R(res.Item2).U32() == shooter && new R(res.Item2) { Pos = 4 }.U32() == other
            && Conn.Hex(res.Item2).Contains("00 00 00 00 00 00 00 00 00 04 45 C6"), "its shot result (0x800F from the NPC, weapon 0, 0, template)");
        Check(nr != null && nr.Item2.Length == 22 && new R(nr.Item2).U32() == shooter, "and 0x8036 to everyone near");

        g2.Send(0x03, new B().U32(accB).U32(other).U16(1).Bytes(new byte[6]).I32(30500).I32(30000).I32(30).U32(0x45FA0000).Get());
        var l2 = RecvOp(g2, 0x8003); var l2r = new R(l2.Item2); l2r.U16();
        Check(l2r.Size() == 3, "the destroyed NPC is gone from the position list");

        // Back home, out of the squad's reach; drop whatever it still sent.
        g2.Send(0x03, new B().U32(accB).U32(other).U16(1).Bytes(new byte[6]).I32(1500).I32(2500).I32(30).U32(0x45FA0000).Get());
        RecvOp(g2, 0x8003);
        System.Threading.Thread.Sleep(1500);
        g2.Send(0x13, new B().U32(0).I32(-1).Byte(0).Get());
        RecvOp(g2, 0x8013);
    }

    static void ShopTests(Conn g, uint me)
    {
        uint backpack = me + 110001, weared = me + 120001;
        g.Send(0x21, Buy(1, me, backpack, 1, 280048));
        Check(Refused(g, 0x8021), "the EF weapon shop does not sell Zeon's 75mm machine gun");
        g.Send(0x21, Buy(1, me, backpack, 1, 280003, town: ZeonTown, shop: ZeonShop));
        Check(Refused(g, 0x8021), "the Zeon weapon shop sells nothing to EF (Amuro)");
        g.Send(0x21, Buy(1, me, backpack, 1, 280003));
        var r = g.Recv(); var rr = new R(r.Item2); rr.Pos = 4; rr.U32(); uint mg = rr.U32(); rr.Pos = 28; uint price = rr.U32();
        Check(r.Item1 == 0x8021 && r.Item2[0] == 2 && r.Item2[1] == 1 && price == 5000 && r.Item2.Length == 45 + 66, "buy an MS head vulcan for 5000 (new item + description with loaded rounds and stats)");

        g.Send(0x21, Buy(1, me, backpack, 10, 540000));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint ammo = rr.U32(); rr.Pos = 28;
        Check(r.Item2[0] == 2 && rr.U32() == 400 && r.Item2.Length == 79, "buy 10 cartridges for 400 (79 bytes like the official reply)");
        g.Send(0x21, Buy(1, me, backpack, 5, 540000, town: 59, shop: 401));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint stack = rr.U32(); rr.Pos = 28;
        Check(r.Item2[0] == 1 && stack == ammo && rr.U32() == 180 && r.Item2.Length == 45, "5 more at Newman's shop 401 (90%) for 180 join the stack (0x01, 45 bytes)");

        g.Send(0x22, Sell(me, 540000, ammo, backpack, 110001, 3, town: 51, shop: 41));
        Check(Refused(g, 0x8022), "Zeon's material shop at ZSSAEO 3 is not for EF");
        g.Send(0x22, Sell(me, 540000, ammo, backpack, 110001, 3, town: 50, shop: 40));
        Check(Refused(g, 0x8022), "the EF material shop does not buy cartridges");
        g.Send(0x22, Sell(me, 540000, ammo, backpack, 110001, 3));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint res = rr.U32(); rr.Pos = 52;
        Check(r.Item1 == 0x8022 && r.Item2.Length == 68 && res == 9 && rr.U32() == 60, "sell 3 cartridges for their sell price 60 (part of the stack: 9)");

        g.Send(0x19, new B().U16(4).U16(0).U32(me).Bytes(new byte[20]).U32(1000).Get());
        r = g.Recv(); Check(r.Item1 == 0x8019 && Conn.Hex(r.Item2).StartsWith("00 04 00 02"), "deposit 1000");
        g.Send(0x19, new B().U16(5).U16(0).U32(me).Bytes(new byte[20]).U32(5000).Get());
        Check(Refused(g, 0x8019), "withdrawing more than the bank holds is refused");
        g.Send(0x19, new B().U16(5).U16(0).U32(me).Bytes(new byte[20]).U32(500).Get());
        Check(g.Recv().Item1 == 0x8019, "withdraw 500");

        // Service 2: the client asked "use the bank?" because the money carried is short; the bank pays it all.
        g.Send(0x21, Buy(2, me, backpack, 10, 540000));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 28;
        Check(r.Item1 == 0x8021 && r.Item2[1] == 2 && r.Item2[3] == 2 && rr.U32() == 400, "10 cartridges bought with bank money (service 2, 400)");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT item_amount FROM container WHERE char_id = " + me + " AND container_id = 500001") == "100", "the bank pays: 500 - 400 = 100 left");
        g.Send(0x21, Buy(2, me, backpack, 10, 540000));
        Check(Refused(g, 0x8021), "the bank's 100 cannot pay 400 (the money carried is not used for service 2)");

        g.Send(0x21, Buy(1, me, backpack, 1, 410000, town: 25, shop: 100));
        Check(Refused(g, 0x8021), "a GM (210000) is refused: not enough money, and not a service 3/4 purchase");
        g.Send(0x21, Buy(4, me, weared, 1, 280003));
        Check(Refused(g, 0x8021), "service 4 sells only vehicles");

        g.Send(0x21, Buy(4, me, weared, 1, 400000));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint car = rr.U32(); uint carFmt = rr.U32();
        Check(r.Item1 == 0x8021 && r.Item2[0] == 2 && r.Item2[1] == 4 && carFmt == 0x14, "buy an elecar and ride it away (service 4)");
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(g.Recv().Item2).StartsWith("00 04 00 02"), "riding the elecar");

        g.Send(0x18, new B().U16(5).U16(0).U32(me).U32(car).U32(0x14).U32(weared).U32(0x14).U32(0).U32(1).Bytes(new byte[52]).Get());
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 28;
        Check(r.Item1 == 0x8018 && r.Item2.Length == 45 && rr.U32() == 0, "repair an undamaged elecar costs 0 (45-byte reply)");

        g.Send(0x15, new B().U16(3).U16(0).U32(me).U32(car).U32(0x14).U32(weared).U32(0x14).U32(400000).U32(120001).U32(1).Byte(0).Get());
        r = g.Recv(); Check(r.Item1 == 0x8015 && r.Item2.Length == 37 && r.Item2[3] == 2, "throw the elecar away (0x8015 echo)");
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(g.Recv().Item2).StartsWith("00 03 00 02"), "on foot again");

        g.Send(0x22, Sell(me, 280003, mg, backpack, 110001, 1));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; res = rr.U32(); rr.Pos = 52;
        Check(res == 8 && rr.U32() == 4900, "sell the vulcan for its sell price 4900 (whole: 8)");
    }


    // town (EFFSS 0157, whose factories are EF's), factory id, facility index in the town, then the product.
    static byte[] Product(ushort action, uint me, uint kind, uint facility, uint product, uint amount, uint factory, uint[][] inputs, bool dismantle = false, byte colour = 0, uint town = 50)
    {
        var b = new B().U16(action).U16(0).U32(me).U32(town).U32(kind).U32(facility).U32(product).U32(amount).U32(factory).U32(0x14).U32(7).I32(-1)
            .Byte((byte)(0x80 | inputs.Length));
        foreach (var i in inputs) // template, uid, container, container static, amount
            b.U32(7).U32(i[0]).U32(i[1]).U32(dismantle ? 0x14u : 0x13u).U32(i[2]).U32(0x14).U32(i[3]).U32(1).U32(0).U32(i[4]);
        return b.Byte(dismantle ? (byte)0xFF : colour).Byte(0).U32(0).U32(0).Get();
    }

    // Reply: result code, ingredient states, and what came out (template, amount).
    static Tuple<int, List<uint>, List<uint[]>> ProductReply(Tuple<uint, byte[]> r, int inputs)
    {
        var rr = new R(r.Item2); rr.Pos = 2; int code = rr.U16();
        var states = new List<uint>();
        for (int i = 0; i < inputs; i++) { rr.Pos = 45 + i * 40; states.Add(rr.U32()); }
        rr.Pos = 45 + inputs * 40 + 10; int n = rr.Size(); var output = new List<uint[]>();
        for (int i = 0; i < n; i++) { output.Add(new uint[] { rr.U32(), rr.U32() }); rr.U32(); }
        Check(r.Item1 == 0x8028 && rr.Pos == r.Item2.Length - 1, "0x8028 layout (" + r.Item2.Length + " bytes)");
        return Tuple.Create(code, states, output);
    }

    // Amuro (EF; MS/MA and arms construction 130, seeded) refines iron ore at EFFSS 0157, is refused a Zaku (Zeon's),
    // builds a GM, takes it to the hangar and back, takes it apart, and makes an RX-78 shield. Recipes from the
    // client's production tables; what each factory makes for each side from FACTORYINFOTEMPLATE.
    static void CraftTests(Conn g, Conn g2, uint me, Dictionary<uint, uint[]> info)
    {
        uint backpack = me + 110001, hangar = me + 110003, factory = me + 110006;
        uint ore = 0, shts = 0, engine = 0, ti = 0, tcc = 0;
        foreach (var kv in info)
        {
            if (kv.Value[2] != backpack) continue;
            if (kv.Value[1] == 530001) ore = kv.Key;
            if (kv.Value[1] == 510001) shts = kv.Key;
            if (kv.Value[1] == 290033) engine = kv.Key;
            if (kv.Value[1] == 510000) ti = kv.Key;
            if (kv.Value[1] == 510002) tcc = kv.Key;
        }
        Check(ore != 0 && shts != 0 && engine != 0 && ti != 0 && tcc != 0, "found the seeded iron ore, steel, titanium, ceramic composite and jet engine");

        // Refine: 1000 iron ore make 500 steel (2 each), 85% of the time; either way the ore is used up.
        g.Send(0x28, Product(2, me, 5, 0x14, 510006, 500, factory, new[] { new uint[] { 530001, ore, backpack, 110001, 1000 } }));
        var pr = ProductReply(RecvOp(g, 0x8028), 1);
        Console.WriteLine("  refine: code {0} state {1} out {2}", pr.Item1, pr.Item2[0], string.Join(",", pr.Item3.Select(o => o[1] + "x" + o[0])));
        Check(pr.Item2[0] == 8, "the 1000 ore are used up (state 8)");
        Check(pr.Item1 == 2 ? pr.Item3.Count == 1 && pr.Item3[0][0] == 510006 && pr.Item3[0][1] >= 500 && pr.Item3[0][1] < 650
            : pr.Item1 == 0x0C && pr.Item3.Count == 1 && pr.Item3[0][0] == 530001 && pr.Item3[0][1] == 700,
            "refined into 500+ steel, or failed and gave 700 ore back");
        var kids = Children(g, me, factory, 0x14, 0, 0, 110006, 0);
        Check(kids.Count == 1, "the factory holds the result");

        g.Send(0x29, new B().U16(2).U16(0).U32(me).U32(factory).U32(0x14).U32(7).U32(510006).U32(0x33).U32(0x14).U32(5).U32(0x47624057).U32(500).U16(2).U16(0).U16(0).Get());
        var done = RecvOp(g, 0x8029);
        Check(done.Item2.Length == 50 && done.Item2[3] == 2, "0x29 is answered with 0x8029");

        // Nothing to refine: refused, nothing used.
        g.Send(0x28, Product(2, me, 5, 0x14, 510006, 500, factory, new[] { new uint[] { 530001, ore, backpack, 110001, 1000 } }));
        pr = ProductReply(RecvOp(g, 0x8028), 1);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7 && pr.Item3.Count == 0, "the ore is gone: refused (0x0C, state 7, nothing out)");

        // Amuro (EF) cannot have a Zaku made: the factory's EF list does not have it.
        g.Send(0x28, Product(1, me, 6, 0x0A, 410046, 1, factory, new[] { new uint[] { 290033, engine, backpack, 110001, 1 }, new uint[] { 510001, shts, backpack, 110001, 60 } }));
        pr = ProductReply(RecvOp(g, 0x8028), 2);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7, "a Zaku is not on the EF factory's list: refused");
        // Nor at a Zeon town's factory (ZSSAEO 3, facility 10).
        g.Send(0x28, Product(1, me, 6, 0x0A, 410000, 1, factory, new[] { new uint[] { 290033, engine, backpack, 110001, 1 }, new uint[] { 510000, ti, backpack, 110001, 34 } }, false, 0, 51));
        pr = ProductReply(RecvOp(g, 0x8028), 2);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7, "a Zeon town's factory refuses Amuro");

        // A GM: an MS/MA rocket or jet engine and 34 titanium alloy; 100% with the skill.
        g.Send(0x28, Product(1, me, 6, 0x0A, 410000, 1, factory, new[] { new uint[] { 290033, engine, backpack, 110001, 1 }, new uint[] { 510000, ti, backpack, 110001, 34 } }));
        pr = ProductReply(RecvOp(g, 0x8028), 2);
        Check(pr.Item1 == 2 && pr.Item2[0] == 8 && pr.Item2[1] == 9 && pr.Item3.Count == 1 && pr.Item3[0][0] == 410000, "a GM is built (engine used up, some titanium alloy left)");
        uint zaku = 0;
        foreach (var k in Children(g, me, factory, 0x14, 0, 0, 110006, 0)) if (k[2] == 410000) zaku = k[0];
        Check(zaku != 0, "the Zaku is in the factory");
        Check(Sql("SELECT item_amount FROM container WHERE char_id = " + me + " AND container_id = 110006 AND item_id = 410000") == "290033",
            "saved in the factory as a vehicle with its jet engine");

        g.Send(0x17, Move(7, me, zaku, 0x14, factory, hangar, 110006, 110003, 1, 0xFF, 0xFF));
        var mv = RecvOp(g, 0x8017);
        Check(new R(mv.Item2).U32() == 0x01010002, "take it out to the hangar (0x0101)");
        g.Send(0x17, Move(7, me, zaku, 0x14, hangar, factory, 110003, 110006, 1, 0xFF, 0xFF));
        Check(new R(RecvOp(g, 0x8017).Item2).U32() == 0x01010002, "and back into the factory");

        // Drag it out of the factory onto the ground and back in (it keeps its unique id).
        g.Send(0x23, Drop(1, me, zaku, 0x14, factory, 1, 410000, 110006, 7, 1200, 2200, 0xFF));
        var dr = RecvOp(g, 0x8023);
        Check(dr.Item2[3] == 2 && new R(dr.Item2) { Pos = 8 }.U32() == zaku, "drag the GM out of the factory onto the ground");
        g.Send(0x24, Pick(1, me, zaku, 0x14, factory, 1, 410000, 110006, 0xFF));
        var pk = RecvOp(g, 0x8024);
        Check(pk.Item2[3] == 2 && Children(g, me, factory, 0x14, 0, 0, 110006, 0).Exists(k => k[0] == zaku), "and drag it back into the factory");
        var seen1 = RecvOp(g2, 0x8035); var seen2 = RecvOp(g2, 0x8035);
        Check(new R(seen1.Item2).U32() == 1 && new R(seen2.Item2).U32() == 2, "Char nearby sees it put down and taken (0x8035 1 and 2, as the capture)");

        UpgradeTests(g, me, info, zaku, factory);

        // Take it apart: 70% of the 34 titanium alloy (23), the engine is lost.
        g.Send(0x28, Product(5, me, 6, 0x0A, 410000, 1, factory, new[] { new uint[] { 410000, zaku, factory, 110006, 1 } }, true));
        pr = ProductReply(RecvOp(g, 0x8028), 1);
        Check(pr.Item1 == 2 && pr.Item3.Count == 1 && pr.Item3[0][0] == 510000 && pr.Item3[0][1] == 23, "dismantled: 23 titanium alloy back (" + (pr.Item3.Count > 0 ? pr.Item3[0][1] + "x" + pr.Item3[0][0] : "nothing") + ")");
        Check(!Children(g, me, factory, 0x14, 0, 0, 110006, 0).Exists(k => k[0] == zaku), "the Zaku is gone");

        // An MS-07 shield is Zeon's; an RX-78 shield from 2 titanium ceramic composite is EF's.
        g.Send(0x28, Product(4, me, 3, 0x10, 360005, 1, factory, new[] { new uint[] { 510001, shts, backpack, 110001, 4 } }));
        pr = ProductReply(RecvOp(g, 0x8028), 1);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7, "the MS-07 shield is not on the EF weapon factory's list: refused");
        g.Send(0x28, Product(4, me, 3, 0x10, 360000, 1, factory, new[] { new uint[] { 510002, tcc, backpack, 110001, 2 } }));
        pr = ProductReply(RecvOp(g, 0x8028), 1);
        Console.WriteLine("  shield: code {0} out {1}", pr.Item1, string.Join(",", pr.Item3.Select(o => o[1] + "x" + o[0])));
        Check(pr.Item2[0] == 8 && (pr.Item1 == 2 ? pr.Item3[0][0] / 100 == 3600 : pr.Item3[0][0] == 510002),
            "an RX-78 shield (or its EX), or some of the composite back");
        Check(Sql("SELECT skill_level FROM skills WHERE char_id = " + me + " AND skill_idx = 22") == "1300", "MS/MA construction stays at its 130 cap");
        DyeTests(g, g2, me, info);
    }

    // Amuro logs in again (to the Space server, where the flight ended): every container, item, stack, vehicle
    // with its cargo, armaments, engine, health and upgrades, weapon state and clothes colour is as it was.
    static void RelogTest(LobbyResult a, uint me, List<string> before)
    {
        var lobby = new Conn("lobby:Amuro2", "127.0.0.1", 42018);
        lobby.Send(0x30000, LoginBody("tester1", "secret1"));
        var lr = new R(lobby.Recv().Item2); uint ok = lr.U32(); uint key = lr.U32(); uint acc = lr.U32();
        Check(ok == 1, "Amuro's player logs in to the Lobby again");
        lobby.Send(0x30001, new B().Byte(0).U32(acc).U32(0).Get()); lobby.Recv();
        lobby.Send(0x30005, new B().U32(key).U32(acc).U32(me).U16(1).U16(0xFFFF).Byte(0x80).Get());
        var gr = new R(lobby.Recv().Item2);
        uint handed = gr.U32(); int n = gr.Size(); string ip = Encoding.ASCII.GetString(gr.Buf, gr.Pos, n); gr.Pos += n; int port = gr.U16();
        lobby.Close();
        Check(handed == 1 && port == a.Port + 1, "and is sent to the Space server (" + ip + ":" + port + ")");
        var space = new LobbyResult { IP = ip, Port = port, Key = key, Acc = acc };
        space.Chars.Add(me);
        var g = GameLogin("Amuro", space, key);
        var login = RecvOp(g, 0x8041);
        Check(Conn.Hex(login.Item2).StartsWith("00 10 00 02"), "Amuro is back on the Space server");
        var after = Snapshot(g, me);
        var lost = before.Except(after).ToList();
        var gained = after.Except(before).ToList();
        foreach (var x in lost) Console.WriteLine("  before only: " + x);
        foreach (var x in gained) Console.WriteLine("  after only:  " + x);
        Check(before.Count > 10 && lost.Count == 0 && gained.Count == 0, "after a relog every item is as it was (" + before.Count + " nodes)");
        g.Send(0x55, new B().U32(me).Get()); RecvOp(g, 0x8055);
        g.Send(0x42, new byte[0]); RecvOp(g, 0x8042);
        g.Close();
    }

    // Every item under the player's containers, without unique ids and times: "parent static > template x amount
    // [options and children formats]", sorted. The same list after a relog means everything came back as it was.
    static List<string> Snapshot(Conn g, uint me)
    {
        int[][] top = { new[] { 110001, 0x14 }, new[] { 120001, 0x14 }, new[] { 110002, 0x14 }, new[] { 500000, 0x13 }, new[] { 500001, 0x13 }, new[] { 110003, 0x14 }, new[] { 110004, 0x14 }, new[] { 110006, 0x14 } };
        var queue = new Queue<uint[]>();
        foreach (var t in top) queue.Enqueue(new uint[] { me + (uint)t[0], (uint)t[1], 0, 0, t[0] == 500001 ? 500000u : (uint)t[0], 0, (uint)t[0] });
        var result = new List<string>();
        Drain(g);
        while (queue.Count > 0)
        {
            var q = queue.Dequeue();
            g.Send(0x16, new B().U32(0x00020000).U32(me).U32(q[0]).U32(q[1]).U32(q[2]).U32(q[3]).U32(q[4]).U32(q[5]).U32(0xB).Byte(0xFF).Get());
            var rep = RecvOp(g, 0x8016);
            var r = new R(rep.Item2);
            r.Pos = 36; r.Size();
            r.U32(); uint fmt = r.U32(); r.U32(); uint amount = r.U32(); uint stat = r.U32(); r.U32(); r.U32();
            int optStart = r.Pos;
            for (int i = 0; i < (int)fmt - 14; i++) { int os = r.Size(); r.Pos += (i == 5 && os > 0) || (fmt == 0x13 && i == 4 && os > 1) ? os * 4 : os; }
            string options = Conn.Hex(rep.Item2).Substring(optStart * 3, (r.Pos - optStart) * 3).Trim();
            int kids = r.Size();
            var formats = new List<string>();
            for (int i = 0; i < kids; i++)
            {
                uint cu = r.U32(), cf = r.U32(), cs = r.U32();
                formats.Add(cu == 0 ? "empty" : cs.ToString());
                if (cu != 0) queue.Enqueue(new uint[] { cu, cf, q[0], q[1], cs, q[4], cs });
            }
            if (fmt != 0x14 || stat != 120001) formats.Sort(); // only weared has fixed slots
            result.Add(string.Format("{0} > {1} x {2} [{3}] ({4})", q[5], stat, amount, options, string.Join(",", formats)));
        }
        result.Sort();
        return result;
    }

    // GM upgrades (UpgradeChance 100 in the tests): level 1 of each type takes 2 of its package; the levels
    // show in the vehicle's stats list (19th int: power bits 0-3, defence 4-7, hit 8-11) and are saved ("^levels").
    static void UpgradeTests(Conn g, uint me, Dictionary<uint, uint[]> info, uint zaku, uint factory)
    {
        uint backpack = me + 110001, power = 0, hit = 0, defence = 0;
        foreach (var kv in info)
        {
            if (kv.Value[2] != backpack) continue;
            if (kv.Value[1] == 510021) power = kv.Key;
            if (kv.Value[1] == 510022) hit = kv.Key;
            if (kv.Value[1] == 510023) defence = kv.Key;
        }
        Check(power != 0 && hit != 0 && defence != 0, "found the seeded improvement packages");
        Func<uint, uint, uint, byte[]> upgrade = (template, uid, amount) =>
        {
            var body = Product(8, me, 6, 0x0A, 410000, 1, factory, new[] { new uint[] { template, uid, backpack, 110001, amount } }, true);
            body[body.Length - 9] = 1; // improve flag
            new B().U32(zaku).Get().CopyTo(body, body.Length - 8);
            return body;
        };
        Func<int> levels = () =>
        {
            g.Send(0x16, new B().U32(0x00020000).U32(me).U32(zaku).U32(0x14).U32(factory).U32(0x14).U32(410000).U32(110006).U32(0xB).Byte(0xFF).Get());
            var d = RecvOp(g, 0x8016).Item2;
            for (int i = 0; i + 77 <= d.Length; i++)
                if (d[i] == 0x93 && d[i + 1] == 0 && d[i + 2] == 0) return (int)new R(d) { Pos = i + 1 + 18 * 4 }.U32();
            return -1;
        };
        Check(levels() == 0, "a new GM has no upgrades");

        g.Send(0x28, upgrade(510022, hit, 2));
        var r = RecvOp(g, 0x8028); var pr = ProductReply(r, 1);
        Check(pr.Item1 == 2 && pr.Item2[0] == 8 && pr.Item3.Count == 0 && r.Item2[r.Item2.Length - 11] == 1,
            "hit upgrade 1 works: the 2 accuracy packages are used up, improve flag 1, nothing out");
        g.Send(0x28, upgrade(510021, power, 10));
        r = RecvOp(g, 0x8028); pr = ProductReply(r, 1);
        Check(pr.Item1 == 2 && pr.Item2[0] == 9 && r.Item2[r.Item2.Length - 11] == 1, "power upgrade 1 works: 2 of the 10 enhancement packages used");
        int packed = levels();
        Check(packed == 0x101, "the stats list shows hit 1 and power 1 (" + packed.ToString("X") + ", as the official 0x111 layout)");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT child FROM container WHERE char_id = " + me + " AND item_id = 410000") == "^257", "saved with the vehicle (^257)");

        g.Send(0x28, upgrade(510023, defence, 1));
        pr = ProductReply(RecvOp(g, 0x8028), 1);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7, "defence upgrade 1 needs 2 packages, 1 is not enough: refused, nothing used");
        g.Send(0x28, upgrade(510022, hit, 2));
        pr = ProductReply(RecvOp(g, 0x8028), 1);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7, "the accuracy packages are gone: refused");
    }

    // Clothes are dyed when they are made: a shirt (240082: 2 silk yarn and a dye) from the seeded silk yarn and dyes.
    static void DyeTests(Conn g, Conn g2, uint me, Dictionary<uint, uint[]> info)
    {
        uint backpack = me + 110001, factory = me + 110006, weared = me + 120001, silk = 0, basic = 0, light = 0;
        foreach (var kv in info)
        {
            if (kv.Value[2] != backpack) continue;
            if (kv.Value[1] == 230009) silk = kv.Key;
            if (kv.Value[1] == 350000) basic = kv.Key;
            if (kv.Value[1] == 350001) light = kv.Key;
        }
        Check(silk != 0 && basic != 0 && light != 0, "found the seeded silk yarn and dyes");

        g.Send(0x28, Product(3, me, 4, 10, 240082, 1, factory, new[] { new uint[] { 230009, silk, backpack, 110001, 2 }, new uint[] { 350001, light, backpack, 110001, 1 } }, false, 40, 2));
        var pr = ProductReply(RecvOp(g, 0x8028), 2);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7 && pr.Item2[1] == 7, "a light colour needs Clothing Manufacturing 19.0: refused, nothing used");
        g.Send(0x28, Product(3, me, 4, 10, 240082, 1, factory, new[] { new uint[] { 230009, silk, backpack, 110001, 2 }, new uint[] { 350000, basic, backpack, 110001, 1 } }, false, 40, 2));
        pr = ProductReply(RecvOp(g, 0x8028), 2);
        Check(pr.Item1 == 0x0C && pr.Item2[0] == 7, "colour 40 is not one of the basic dye's (0-13): refused");

        g.Send(0x28, Product(3, me, 4, 10, 240082, 1, factory, new[] { new uint[] { 230009, silk, backpack, 110001, 2 }, new uint[] { 350000, basic, backpack, 110001, 1 } }, false, 5, 2));
        pr = ProductReply(RecvOp(g, 0x8028), 2);
        Check(pr.Item1 == 2 && pr.Item2[0] == 9 && pr.Item2[1] == 9 && pr.Item3.Count == 1 && pr.Item3[0][0] == 240082, "a shirt in basic colour 5 is made (yarn and dye used)");
        uint shirt = 0;
        foreach (var k in Children(g, me, factory, 0x14, 0, 0, 110006, 0)) if (k[2] == 240082) shirt = k[0];
        g.Send(0x16, new B().U32(0x00020000).U32(me).U32(shirt).U32(0x13).U32(factory).U32(0x14).U32(240082).U32(110006).U32(0xB).Byte(0xFF).Get());
        var info16 = RecvOp(g, 0x8016).Item2;
        var tail = Conn.Hex(info16).Substring(3 * (36 + 1 + 28));
        Check(shirt != 0 && tail.StartsWith("80 80 80 81 05 80"), "its item info carries colour 5 like the official clothes (" + tail.Substring(0, Math.Min(20, tail.Length)) + ")");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT child FROM container WHERE char_id = " + me + " AND item_id = 240082") == "~c5", "the colour is saved with the item");

        g.Send(0x1B, Dress(me, new uint[] { 1, 2, shirt }));
        var ok = RecvOp(g, 0x801B);
        Check(ok.Item2[3] == 2, "Amuro puts the shirt on");
        System.Threading.Thread.Sleep(300);
        Check(Garment("top") == "240082,5", "worn with its colour (garments top " + Garment("top") + ")");
        Drain(g2);
        g2.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        var looks = RecvOp(g2, 0x800A).Item2;
        Check(looks.Length == 72 && looks[17] == 5, "Char sees the shirt in colour 5 (" + Conn.Hex(looks, 20).Substring(33) + ")");
    }

    static void GroundTests(Conn g, Conn g2, uint me, uint other, uint yarnStack, uint vehicle, uint vstat, uint vinv, uint hangar, uint weared)
    {
        // Drop 3 yarn: 0x8023 echoes the request with a new unique id, 0x8035 tells both players.
        g.Send(0x23, Drop(1, me, yarnStack, 0x13, me + 110001, 3, 240000, 110001, 1, 1100, 2100, 0xFF));
        var r = g.Recv(); var rr = new R(r.Item2); rr.Pos = 8; uint gid = rr.U32(); rr.Pos = 24; uint orig = rr.U32();
        Check(r.Item1 == 0x8023 && r.Item2.Length == 79 && r.Item2[3] == 2 && gid != yarnStack && orig == yarnStack, "drop 3 yarn (0x8023 with a new ground id)");
        var b1 = g.Recv(); var b2 = g2.Recv();
        Check(b1.Item1 == 0x8035 && b1.Item2.Length == 73 && new R(b1.Item2).U32() == 1 && new R(b1.Item2) { Pos = 4 }.U32() == gid && b1.Item2[72] == 0, "0x8035 item dropped to the dropper (73 bytes, list 0)");
        Check(b2.Item1 == 0x8035 && Conn.Hex(b2.Item2) == Conn.Hex(b1.Item2), "the other player gets the same 0x8035");

        // The other player sees it in list 0 and picks it up.
        g2.Send(0x05, new B().I32(1500).I32(2500).I32(30).U32(0x457A0000).U32(0).Get());
        var sc = g2.Recv(); var sr = new R(sc.Item2); sr.U16(); sr.Pos = 3; int n = sr.Size();
        Check(sc.Item1 == 0x8005 && n == 1 && sr.U32() == gid && sc.Item2.Length == 4 + 62, "list 0 shows the dropped yarn (62-byte record)");
        g2.Send(0x24, Pick(1, other, gid, 0x13, other + 110001, 3, 240000, 110001, 0xFF));
        r = g2.Recv(); rr = new R(r.Item2); rr.Pos = 69; uint tuid = rr.U32(); rr.U32(); rr.U32(); uint tamount = rr.U32();
        Check(r.Item1 == 0x8024 && r.Item2[3] == 2 && tuid == gid && tamount == 3, "the other player picks it up (0x8024 + item description)");
        b2 = g2.Recv(); b1 = g.Recv();
        Check(b2.Item1 == 0x8035 && new R(b2.Item2).U32() == 2 && b1.Item1 == 0x8035, "0x8035 item picked up to both");
        g.Send(0x24, Pick(1, me, gid, 0x13, me + 110001, 3, 240000, 110001, 0xFF));
        Check(Refused(g, 0x8024), "picking it up again is refused");

        // Mini op 4: 2 more yarn picked up onto the other player's yarn stack (the one just picked up).
        g.Send(0x23, Drop(1, me, yarnStack, 0x13, me + 110001, 2, 240000, 110001, 1, 1100, 2100, 0xFF));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint gid2 = rr.U32();
        g.Recv(); g2.Recv();
        g2.Send(0x24, Pick(4, other, gid2, 0x13, other + 110001, 2, 240000, 110001, 0xFF));
        r = g2.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint joined = rr.U32(); rr.Pos = 40; uint was = rr.U32();
        rr.Pos = 69; uint suid = rr.U32(); rr.U32(); rr.U32(); uint samount = rr.U32();
        Check(r.Item1 == 0x8024 && r.Item2[1] == 4 && r.Item2[3] == 2 && joined != gid2 && was == gid2 && suid == joined && samount >= 5,
            "mini op 4 puts the yarn onto the stack there (0x8024 names the stack, amount " + samount + ")");
        b2 = g2.Recv(); b1 = g.Recv();
        Check(b2.Item1 == 0x8035 && new R(b2.Item2).U32() == 2 && new R(b2.Item2) { Pos = 4 }.U32() == gid2 && b1.Item1 == 0x8035, "0x8035 picked up for the joined yarn");

        // 0x0D (unknown query, 00 00 00): the official 28-byte answer.
        g.Send(0x0D, new byte[3]);
        r = g.Recv();
        Check(r.Item1 == 0x800D && r.Item2.Length == 28 && r.Item2[1] == 0x1A && r.Item2[3] == 2, "0x0D is answered like the official server");

        // Money: Amuro drops 100 (container 0), the other player takes it.
        g.Send(0x23, Drop(1, me, me + 500000, 0x13, 0, 100, 500000, 0, 3, 1100, 2100, 0xFF));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint mid = rr.U32();
        Check(r.Item1 == 0x8023 && mid != me + 500000, "drop 100 money");
        g.Recv(); g2.Recv();
        g2.Send(0x24, Pick(2, other, mid, 0x13, other + 500000, 100, 500000, 500000, 0xFF));
        r = g2.Recv(); rr = new R(r.Item2); rr.Pos = 8; uint mstack = rr.U32(); rr.Pos = 40; uint mwas = rr.U32();
        Check(r.Item1 == 0x8024 && mstack != mid && mwas == mid, "the other player picks up the money (mini op 2, onto their money)");
        g2.Recv(); g.Recv();
        g2.Send(0x16, new B().U32(0x00020000).U32(other).U32(other + 500000).U32(0x13).U32(0).U32(0).U32(500000).U32(0).U32(0xB).Byte(0xFF).Get());
        var mr = new R(g2.Recv(true).Item2); mr.Pos = 36; mr.Size(); mr.U32(); mr.U32(); mr.U32();
        Check(mr.U32() == 50100, "the other player's money is 50100");

        // Put 2 yarn in the vehicle's inventory (saved in container.child).
        g.Send(0x17, Move(7, me, yarnStack, 0x13, me + 110001, vinv, 110001, 110010, 2, 0xFF, 0xFF));
        Check(new R(g.Recv().Item2).U32() == 0x02010002, "2 yarn into the vehicle inventory (split)");

        // Get in at the hangar, then out onto the ground.
        g.Send(0x17, Move(8, me, vehicle, 0x14, hangar, weared, 110003, 120001, 1, 0xFF, 0x00));
        Check(new R(g.Recv().Item2).U32() == 0x01020002, "get in the vehicle at the hangar");
        g.Send(0x23, Drop(2, me, vehicle, 0x14, weared, 1, vstat, 120001, 1, 1200, 2200, 0x00));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 8;
        Check(r.Item1 == 0x8023 && rr.U32() == vehicle && r.Item2[1] == 2 && r.Item2[3] == 2, "get out leaving it on the ground (0x8023, same id)");
        b1 = g.Recv(); g2.Recv();
        Check(new R(b1.Item2).U32() == 3 && b1.Item2[72] == 2, "0x8035 vehicle left (list 2)");
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(g.Recv().Item2).StartsWith("00 03 00 02"), "on foot after getting out");

        g2.Send(0x24, Pick(3, other, vehicle, 0x14, other + 120001, 1, vstat, 120001, 0x00));
        Check(Refused(g2, 0x8024), "the other player cannot take Amuro's vehicle");

        // Owner changes (0x25): only the owner can give it away.
        g2.Send(0x25, new B().U32(other).U32(other).U32(vstat).U32(vehicle).U32(0x14).U32(0).Get());
        Check(Refused(g2, 0x8025), "the other player cannot make Amuro's vehicle theirs");
        g.Send(0x25, new B().U32(me).U32(other).U32(vstat).U32(vehicle).U32(0x14).U32(0).Get());
        r = g.Recv();
        Check(r.Item1 == 0x8025 && Conn.Hex(r.Item2).Trim() == "00 00 00 01 00 00 00 02 00 00 00 00", "Amuro gives the vehicle to Char (0x8025 1, 2, 0)");
        b1 = g.Recv(); var ob2 = g2.Recv(); rr = new R(b1.Item2); rr.Pos = 42;
        Check(b1.Item1 == 0x8035 && new R(b1.Item2).U32() == 0 && rr.U32() == other && ob2.Item1 == 0x8035, "0x8035 action 0 names Char as the owner, to both");
        g2.Send(0x25, new B().U32(other).U32(me).U32(vstat).U32(vehicle).U32(0x14).U32(0).Get());
        Check(g2.Recv().Item1 == 0x8025, "Char gives it back");
        g.Recv(); g2.Recv();

        g.Send(0x05, new B().I32(1000).I32(2000).I32(30).U32(0x457A0000).U32(2).Get());
        sc = g.Recv(); sr = new R(sc.Item2); sr.Pos = 3;
        Check(sr.Size() == 1 && sr.U32() == vehicle, "list 2 shows the vehicle");

        g.Send(0x24, Pick(3, me, vehicle, 0x14, weared, 1, vstat, 120001, 0x00));
        r = g.Recv(); rr = new R(r.Item2); rr.Pos = 69; uint vu = rr.U32(); uint vf = rr.U32();
        Check(r.Item1 == 0x8024 && vu == vehicle && vf == 0x14, "Amuro gets back in (0x8024 + vehicle description)");
        b1 = g.Recv(); g2.Recv();
        Check(new R(b1.Item2).U32() == 4, "0x8035 vehicle taken");
        g.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        Check(Conn.Hex(g.Recv().Item2).StartsWith("00 04 00 02"), "piloting again");

        // Leave it on the ground once more for the save check after logout.
        g.Send(0x23, Drop(2, me, vehicle, 0x14, weared, 1, vstat, 120001, 1, 1200, 2200, 0x00));
        Check(g.Recv().Item1 == 0x8023, "leave the vehicle on the ground");
        g.Recv(); g2.Recv();
    }

    // GM spawn through the game server console (run with "spawn"; run/spawn.sh types the commands).
    // A breakable plant of the GM's own side (330002, EF, 15000 health) placed with #spawn id: shooting it changes its
    // health for everyone near (0x8035 action 5) and is a crime; nobody can make it theirs.
    static void TargetTests(Conn g, uint me)
    {
        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("READY") + "t", "ready");
        Tuple<uint, byte[]> placed = null;
        var until = DateTime.Now.AddSeconds(15);
        while (DateTime.Now < until && placed == null) { var r = g.Recv(true); if (r != null && r.Item1 == 0x8035 && new R(r.Item2) { Pos = 12 }.I32() == 330002) placed = r; }
        if (placed == null) { Check(false, "#spawn id 330002 places a temporary plant"); return; }
        var pr = new R(placed.Item2) { Pos = 4 }; uint plant = pr.U32(); uint fmt = pr.U32();
        int hp0 = new R(placed.Item2) { Pos = 4 + 47 }.I32();
        Check(new R(placed.Item2).U32() == 1 && fmt == 0x14 && hp0 == 15000, "#spawn id 330002 places an EF temporary plant with 15000 health (" + hp0 + ")");
        g.Send(0x25, new B().U32(me).U32(me).U32(330002).U32(plant).U32(0x14).U32(0).Get());
        Check(Refused(g, 0x8025), "nobody can make the plant theirs");
        bool hit = false;
        for (int i = 0; i < 10 && !hit; i++)
        {
            g.Send(0x11, new B().U32(me).Byte(0).Byte(0).U16(0xFFFF).U32(plant).U32(0x14).U32(330002).Get());
            var r = RecvOp(g, 0x8011);
            if (r.Item1 != 0x8011) break;
            if (new R(r.Item2) { Pos = 8 }.U32() == 0) continue;
            hit = true;
            Check(r.Item2[13] == 1, "shooting your own side's plant is a crime (0x8011 crime byte " + r.Item2[13] + ")");
            var u = RecvOp(g, 0x8035); int hp = new R(u.Item2) { Pos = 4 + 47 }.I32();
            Check(new R(u.Item2).U32() == 5 && hp < 15000 && hp > 0, "the plant's new health goes out (0x8035 action 5, " + hp + ")");
        }
        Check(hit, "the plant can be shot");
    }

    static int SpawnTest()
    {
        var a = Lobby("gm1", "secret1", "Gmtest", 1, false);
        uint me = a.Chars[0];
        var g = GameLogin("Gmtest", a, a.Key); g.Recv(); g.KeepGains = true;
        g.Send(0x00, Coord(me, 1000, 2000, 30)); g.Recv();
        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("READY"), "ready");
        var got = new List<Tuple<uint, byte[]>>();
        Tuple<uint, byte[]> gain = null;
        var until = DateTime.Now.AddSeconds(20);
        while (DateTime.Now < until && (got.Count < 4 || gain == null)) { var r = g.Recv(); if (r == null) continue; if (r.Item1 == 0x8035) got.Add(r); if (r.Item1 == 0x8034) gain = r; }
        Check(got.Count == 4, "four ground items spawned");
        Check(GameLog().Contains("Loaded the terrain of Earth (6000 x 4500 heights)."), "the Earth server reads the terrain heights (DB/Terrain/au.tr)");
        if (got.Count < 4) return 1;
        Check(gain != null && Conn.Hex(gain.Item2).Trim() == Conn.Hex(new B().U32(me).Get()).Trim() + " 81 00 00 00 00 80 81 00 11 00 00 03 57 01 80", "#skill::ambac::85.5 sends the client +85.5 AMBAC (0x8034 skill 0x11)");
        Check(Sql("SELECT skill_level FROM skills WHERE char_id = " + me + " AND skill_idx = 18") == "855", "and saves 855");
        var ammo = new R(got[0].Item2); ammo.Pos = 12; int st = ammo.I32(); ammo.Pos = 4 + 34; int amt = ammo.I32();
        Check(new R(got[0].Item2).U32() == 1 && st == 540005 && amt == 100, "100 cartridges lie next to the GM");
        var car = new R(got[1].Item2); car.Pos = 4; uint carUid = car.U32(); uint fmt = car.U32(); int cst = car.I32();
        Check(fmt == 0x14 && cst == 460004 && got[1].Item2[got[1].Item2.Length - 1] == 2, "a hover truck stands there");
        uint mg = new R(got[2].Item2) { Pos = 4 }.U32();
        var ms = new R(got[3].Item2) { Pos = 4 }; uint msUid = ms.U32(); ms.U32(); int msTpl = ms.I32();
        Check(msTpl == 410000, "a GM stands there");

        g.Send(0x24, Pick(3, me, carUid, 0x14, me + 120001, 1, 460004, 120001, 0));
        var board = RecvOp(g, 0x8024);
        Check(board.Item1 == 0x8024, "the GM gets in the spawned hover truck");
        var kids = Children(g, me, carUid, 0x14, me + 120001, 0x14, 460004, 120001);
        uint cinv = 0; foreach (var k in kids) if (k[2] == 110010) cinv = k[0];
        g.Send(0x24, Pick(1, me, mg, 0x13, cinv, 1, 280014, 110010, 0xFF));
        Check(RecvOp(g, 0x8024).Item1 == 0x8024, "picks up the spawned machine gun into the hover truck");
        g.Send(0x1B, new B().U16(2).U16(0).U32(me).Bytes(new byte[8]).Size(1).U16(1).Byte(0).Byte(3).U32(mg).U32(0x13).Get());
        Check(RecvOp(g, 0x801B).Item1 == 0x801B, "and equips it");
        uint ammoUid = new R(got[0].Item2) { Pos = 4 }.U32();
        g.Send(0x24, Pick(1, me, ammoUid, 0x13, cinv, 100, 540005, 110010, 0xFF));
        Check(RecvOp(g, 0x8024).Item1 == 0x8024, "picks up the spawned cartridges into the hover truck");
        g.Send(0x1D, new B().U16(1).U16(0).U32(me).U32(ammoUid).U32(0x13).U32(cinv).U32(0x14).U32(mg).U32(0x13).U32(0).U32(100).Get());
        Check(RecvOp(g, 0x801D).Item2[3] == 2, "and loads them (0x801D echo)");

        bool destroyed = false; int shots = 0, damaged = 0;
        while (!destroyed && shots < 85)
        {
            shots++;
            // The first shot asks for melee special 0, which a machine gun does not have: a normal shot (FFFF back).
            g.Send(0x11, new B().U32(me).Byte(0).Byte(0).U16(shots == 1 ? (ushort)0 : (ushort)0xFFFF).U32(msUid).U32(0x14).U32(410000).Get());
            var r = RecvOp(g, 0x8011);
            if (r.Item1 != 0x8011 || r.Item2.Length != 42) { Check(false, "0x8011 (42 bytes) for a shot at the parked GM"); break; }
            if (shots == 1) Check(r.Item2[14] == 0xFF && r.Item2[15] == 0xFF && r.Item2[20] <= 1, "a machine gun cannot do melee special 0: a normal shot (special FFFF, durability 0 or 1)");
            if (new R(r.Item2) { Pos = 8 }.U32() == 0) continue;
            var u = RecvOp(g, 0x8035); uint action = new R(u.Item2).U32(); int hp = new R(u.Item2) { Pos = 4 + 47 }.I32();
            if (action == 5) damaged++;
            if (action == 1 && hp == 0) destroyed = true;
        }
        Console.WriteLine("  {0} shots at the parked GM", shots);
        Check(damaged > 0, "hits on the parked GM send its new health (0x8035 action 5)");
        Check(destroyed, "the parked GM is destroyed and becomes a wreck (0x8035 action 1, health 0)");

        TargetTests(g, me);

        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("READY") + "2", "ready");
        var ready2 = DateTime.Now;
        g.NpcResults = new List<byte[]>();
        // The hostile ZAKU II spawned 1000 away attacks the GM on its own (aggro range 1500): a lock on and a result,
        // after a fire effect if it shot (a melee strike has none; the script teleports the GM next to it meanwhile).
        // The script also turns on #god for the GM, so the NPC fights below cannot destroy the hover truck.
        var seen = new List<uint>();
        var until2 = DateTime.Now.AddSeconds(15);
        while (DateTime.Now < until2 && !(seen.Contains(0x8010) && seen.Contains(0x800F)))
        {
            var r = g.Recv(true); if (r != null) seen.Add(r.Item1);
        }
        Console.WriteLine("  first NPC attack: " + (seen.Contains(0x803B) ? "a shot" : "a melee strike"));
        Check(seen.Contains(0x8010) && seen.Contains(0x800F), "the spawned hostile NPC attacks the GM without being attacked (lock on, result)");

        // The script teleports the GM next to the NPC (#tp 1090000000, the last of its #tp commands) a few seconds
        // after ready2. From then on the test keeps track of where the GM is: a position list request (0x03) moves the
        // GM to the point it names, so every request names the GM's own position.
        var tpUntil = ready2.AddSeconds(30);
        while (DateTime.Now < tpUntil && !GameLog().Contains("Teleported to NPC Spawned.")) System.Threading.Thread.Sleep(200);
        var tp = System.Text.RegularExpressions.Regex.Matches(GameLog(), @"Gmtest was teleported to (-?\d+), (-?\d+), (-?\d+)\.");
        Check(GameLog().Contains("Teleported to NPC Spawned.") && tp.Count > 0, "#tp puts the GM next to the spawned NPC");
        var last = tp.Count > 0 ? tp[tp.Count - 1].Groups : null;
        int gmX = last != null ? int.Parse(last[1].Value) : 7000, gmY = last != null ? int.Parse(last[2].Value) : 2000,
            gmZ = last != null ? int.Parse(last[3].Value) : 30;
        Action<int, int> moveGm = (x, y) => { gmX = x; gmY = y; g.Send(0x00, Coord(me, x, y, gmZ)); RecvOp(g, 0x8000); };
        Action askList = () => g.Send(0x03, new B().U32(a.Acc).U32(me).U16(1).Bytes(new byte[6]).I32(gmX).I32(gmY).I32(gmZ).U32(0x45FA0000).Get());
        Func<uint, int[]> npcAt = id =>
        {
            askList();
            var list = RecvOp(g, 0x8003);
            if (list.Item1 != 0x8003) return null;
            var lr = new R(list.Item2); lr.U16(); int count = lr.Size();
            for (int i = 0; i < count; i++)
            {
                var rr = new R(list.Item2) { Pos = lr.Pos + 53 * i }; int x = rr.I32(), y = rr.I32(); rr.Pos += 10;
                if (rr.U32() == id) return new[] { x, y };
            }
            return null;
        };
        var p1 = npcAt(1090000000);
        Check(p1 != null, "the spawned hostile ZAKU II (the first NPC spawned, 1090000000) is in the position list");
        // The friendly NPCs spawned together (#spawn npc friendly, then #spawn npc friendly 410000 2 at the same spot
        // right after) are one squad, one team id in the position list; the hostile one has a squad of its own.
        askList();
        var teamList = RecvOp(g, 0x8003);
        var teams = new Dictionary<uint, int>();
        if (teamList.Item1 == 0x8003)
        {
            var tl = new R(teamList.Item2); tl.U16(); int tcount = tl.Size();
            for (int i = 0; i < tcount; i++)
            {
                uint id = new R(teamList.Item2) { Pos = tl.Pos + 53 * i + 18 }.U32();
                teams[id] = new R(teamList.Item2) { Pos = tl.Pos + 53 * i + 44 }.I32();
            }
        }
        Console.WriteLine("  spawned NPC teams: " + string.Join(", ", teams.Where(kv => kv.Key >= 1090000000).Select(kv => kv.Key + " " + kv.Value)));
        Check(teams.ContainsKey(1090000001) && teams.ContainsKey(1090000002) && teams.ContainsKey(1090000003) && teams.ContainsKey(1090000000) &&
            teams[1090000001] == teams[1090000002] && teams[1090000002] == teams[1090000003] && teams[1090000000] != teams[1090000001],
            "the friendly NPCs spawned together are one squad (one team id), the hostile one another");
        p1 = p1 ?? new[] { gmX - 300, gmY };
        // 3500 away: beyond where a gun keeps its distance (3000 at most), within NpcChaseRange (4000).
        moveGm(p1[0] + 3500, p1[1]);
        System.Threading.Thread.Sleep(3000);
        var p2 = npcAt(1090000000) ?? p1;
        Console.WriteLine("  NPC x {0} -> {1}", p1[0], p2[0]);
        Check(p2[0] > p1[0] + 200, "it chases the GM, who moved away");

        NpcWeaponTests(g, moveGm, npcAt);

        // Back where the spawned NPC gave up (it stopped tracking the GM, who went to the Brawler), and next to it,
        // for it to attack again.
        moveGm(p2[0], p2[1]);
        System.Threading.Thread.Sleep(1000);
        var p3 = npcAt(1090000000) ?? p2;
        moveGm(p3[0] + 800, p3[1]);
        var templates = new Dictionary<int, int>();
        var until3 = DateTime.Now.AddSeconds(15);
        while (DateTime.Now < until3)
        {
            var r = g.Recv(true);
            if (r == null) { askList(); continue; }
            if (r.Item1 == 0x800F && new R(r.Item2).U32() >= 1090000000)
            {
                int t = new R(r.Item2) { Pos = 42 }.I32();
                templates[t] = templates.ContainsKey(t) ? templates[t] + 1 : 1;
            }
        }
        Console.WriteLine("  NPC attack weapons: " + string.Join(", ", templates.Select(kv => kv.Key + " x" + kv.Value)));
        Check(templates.Count > 0, "the NPCs keep attacking");

        // #god (on since before the NPCs were spawned): every NPC hit on the GM does 0 damage (0x800F damage at 8,
        // result at 13, 6 = miss).
        var hitsOnGm = g.NpcResults.Where(b => new R(b) { Pos = 4 }.U32() == me && b[13] != 6).ToList();
        g.NpcResults = null;
        Console.WriteLine("  NPC hits on the GM with #god: {0}, damage {1}", hitsOnGm.Count,
            string.Join(" ", hitsOnGm.Select(b => new R(b) { Pos = 8 }.I32())));
        Check(hitsOnGm.Count > 0 && hitsOnGm.All(b => new R(b) { Pos = 8 }.I32() == 0), "with #god on, NPC hits on the GM do no damage");

        QuestTests(g, me);
        SquadTests();
        AdminConsoleTests(g, me);
        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures;
    }

    // The game server console lists the players, backs up the database and bans the GM's account for 3 days: the GM is
    // logged out and the Lobby refuses the account (0x0B) until "unban". The backup loads into an empty database.
    static void AdminConsoleTests(Conn g, uint me)
    {
        foreach (var f in System.IO.Directory.Exists("Backups") ? System.IO.Directory.GetFiles("Backups") : new string[0]) System.IO.File.Delete(f);
        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("READY") + "4", "ready");
        Tuple<uint, byte[]> logout = null;
        var until = DateTime.Now.AddSeconds(30);
        while (DateTime.Now < until && logout == null) { var r = g.Recv(true); if (r != null && r.Item1 == 0x8052) logout = r; }
        Check(logout != null, "\"ban Gmtest 3\" logs the GM out (0x8052)");
        System.Threading.Thread.Sleep(4000);
        var log = GameLog();
        Check(log.Contains("Gmtest (character " + me) && log.Contains("from 127.0.0.1"), "\"players\" lists the GM with their account and address");
        Check(Sql("SELECT DATEDIFF(ban_time, CURDATE()) FROM accounts WHERE name = 'gm1'") == "3", "the account is banned for 3 days");
        Check(LobbyStatus("gm1", "secret1") == 0x0B, "and the Lobby refuses it (0x0B)");
        Check(LobbyStatus("gm1", "wrong") == 9, "a wrong password still gets the wrong-password answer, not the ban");

        var files = System.IO.Directory.Exists("Backups") ? System.IO.Directory.GetFiles("Backups", "titans-server-*.sql") : new string[0];
        Check(files.Length == 1, "\"backup\" wrote one backup (" + files.Length + ")");
        if (files.Length == 1)
        {
            var dump = System.IO.File.ReadAllText(files[0]);
            Check(dump.Contains("CREATE TABLE `accounts`") && dump.Contains("INSERT INTO `characters`"), "with the tables and their rows");
            Sql("DROP DATABASE IF EXISTS titans_restore; CREATE DATABASE titans_restore");
            var psi = new System.Diagnostics.ProcessStartInfo("mariadb", DbArgs() + " titans_restore") { UseShellExecute = false, RedirectStandardInput = true };
            var proc = System.Diagnostics.Process.Start(psi);
            proc.StandardInput.Write(dump); proc.StandardInput.Close(); proc.WaitForExit();
            string[] tables = { "accounts", "characters", "container", "skills" };
            bool same = proc.ExitCode == 0;
            foreach (var t in tables)
            {
                var a = Sql("SELECT COUNT(*) FROM " + t);
                var b = Sql("SELECT COUNT(*) FROM titans_restore." + t);
                if (a != b) { same = false; Console.WriteLine("  " + t + ": " + a + " vs " + b); }
            }
            Check(same, "the backup loads into an empty database with the same rows");
            Sql("DROP DATABASE IF EXISTS titans_restore");
        }
        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("READY") + "5", "ready");
        System.Threading.Thread.Sleep(2000);
        Check(Sql("SELECT status, ban_time IS NULL FROM accounts WHERE name = 'gm1'") == "1\t1" && LobbyStatus("gm1", "secret1") == 1,
            "\"unban Gmtest\" lets the account in again");

        // A ban for good (status 0) is refused too.
        Sql("UPDATE accounts SET status = 0 WHERE name = 'gm1'");
        Check(LobbyStatus("gm1", "secret1") == 0x0B, "an account banned for good (status 0) is refused");
        Sql("UPDATE accounts SET status = 1 WHERE name = 'gm1'");
    }

    static byte[] HandIn(uint me, int quest, params uint[] items)
    {
        var b = new B().U16(1).U16(0).U32(me).U32((uint)quest).Bytes(new byte[16]).U32(0).U32(0).U32(0xFFFFFFFF).Bytes(new byte[8]).U32(0).Size(items.Length);
        foreach (var i in items) b.U32(i).U32(0x13).Bytes(new byte[8]).U32(me + 110001).U32(0x14).U32(7);
        return b.Get();
    }

    static byte[] Dress(uint me, params uint[][] changes)
    {
        var b = new B().U16(1).U16(0).U32(me).Bytes(new byte[8]).Size(changes.Length);
        foreach (var c in changes) b.U16((ushort)c[0]).Byte((byte)c[1]).Byte(0).U32(c[2]).U32(c[2] != 0 ? 0x13u : 0u);
        return b.Get();
    }

    static string Garment(string column)
    {
        return Sql("SELECT " + column + " FROM garments WHERE char_id = (SELECT char_id FROM characters WHERE char_name = 'Amuro')");
    }

    // Changing clothes (0x1B section 1): the seeded cap (240088, looks slot 6) and glasses (240092, slot 7) in Amuro's
    // backpack, the EF uniform (240006) worn in weared slot 1.
    static void ClothesTests(Conn g, Conn g2, uint me, Dictionary<uint, uint[]> info)
    {
        uint backpack = me + 110001, weared = me + 120001, uniform = 0, cap = 0, glasses = 0;
        foreach (var kv in info)
        {
            if (kv.Value[2] == weared && kv.Value[1] == 240006) uniform = kv.Key;
            if (kv.Value[2] == backpack && kv.Value[1] == 240088) cap = kv.Key;
            if (kv.Value[2] == backpack && kv.Value[1] == 240092) glasses = kv.Key;
        }
        Check(uniform != 0 && cap != 0 && glasses != 0, "found the worn uniform and the seeded cap and glasses");
        Drain(g2);
        g2.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        var before = RecvOp(g2, 0x800A).Item2;

        g.Send(0x1B, Dress(me, new uint[] { 2, 1, 0 }, new uint[] { 1, 7, cap }, new uint[] { 1, 8, glasses }));
        var ok = RecvOp(g, 0x801B);
        Check(ok.Item1 == 0x801B && ok.Item2[0] == 0 && ok.Item2[1] == 1 && ok.Item2[3] == 2, "uniform off, cap and glasses on: 0x801B section 1 code 2");
        System.Threading.Thread.Sleep(300);
        Check(Garment("dress") == "-1,0" && Garment("hat") == "240088,0" && Garment("glasses") == "240092,0", "the garments are saved (dress " + Garment("dress") + ", hat " + Garment("hat") + ", glasses " + Garment("glasses") + ")");
        Check(Sql("SELECT COUNT(*) FROM container WHERE char_id = " + me + " AND container_id = 110001 AND item_id = 240006") == "1" &&
            Sql("SELECT COUNT(*) FROM container WHERE char_id = " + me + " AND item_id IN (240088, 240092)") == "0", "the uniform is saved in the backpack, the cap and glasses are no longer there");

        g2.Send(0x0A, new B().U32(0).U32(me).Byte(5).Get());
        var after = RecvOp(g2, 0x800A).Item2;
        Check(after.Length == 72 && (after[8] != before[8] || after[9] != before[9]), "Char gets Amuro's new looks with a new sum (" + Conn.Hex(before, 10).Substring(24) + " -> " + Conn.Hex(after, 10).Substring(24) + ")");
        Check(after[12] == 0xFF && after[13] == 0xFF && (after[30] << 8 | after[31]) != 0xFFFF && (after[33] << 8 | after[34]) != 0xFFFF, "no dress, a cap and glasses in the looks");

        g.Send(0x1B, Dress(me, new uint[] { 1, 6, uniform }));
        var no = RecvOp(g, 0x801B);
        Check(no.Item1 == 0x801B && no.Item2[3] != 2, "the uniform cannot go in the gloves slot (code " + no.Item2[3] + ")");
        g.Send(0x1B, Dress(me, new uint[] { 1, 7, uniform }));
        no = RecvOp(g, 0x801B);
        Check(no.Item1 == 0x801B && no.Item2[3] != 2, "nor into the cap's slot, which is not empty");

        g.Send(0x1B, Dress(me, new uint[] { 1, 1, uniform }, new uint[] { 2, 8, 0 }));
        ok = RecvOp(g, 0x801B);
        Check(ok.Item1 == 0x801B && ok.Item2[3] == 2, "uniform back on, glasses off");
        g.Send(0x16, new B().U32(0x00020000).U32(me).U32(weared).U32(0x14).U32(0).U32(0).U32(120001).U32(0).U32(0xB).Byte(0xFF).Get());
        var w = new R(RecvOp(g, 0x8016).Item2) { Pos = 36 }; w.Size();
        w.U32(); uint fmt = w.U32(); w.U32(); w.U32(); w.U32(); w.U32(); w.U32();
        for (int i = 0; i < (int)fmt - 14; i++) { int os = w.Size(); w.Pos += (i == 5 && os > 0) ? os * 4 : os; }
        int kids = w.Size();
        var slots = new List<uint>();
        for (int i = 0; i < kids; i++) { uint cu = w.U32(); w.U32(); w.U32(); slots.Add(cu); }
        Check(kids == 8 && slots[1] == uniform && slots[7] == cap, "weared is back to 8 slots, the uniform in slot 1 and the cap in slot 7");
        System.Threading.Thread.Sleep(300);
        Check(Garment("dress") == "240006,0" && Garment("glasses") == "-1,0", "and saved");
        Drain(g2);
    }

    // The console spawns the item of Sydney combat quest 1 (550410); the GM hands it in, then adds promotion points.
    /// <summary>
    /// The system lines (0x8004) the chat server sends within a few seconds, until one starts with <paramref name="stop"/>.
    /// </summary>
    static List<string> ChatLines(Conn cms, int seconds, string stop)
    {
        var lines = new List<string>();
        var until = DateTime.Now.AddSeconds(seconds);
        while (DateTime.Now < until)
        {
            var r = cms.Recv(true);
            if (r == null || r.Item1 != 0x8004) continue;
            lines.Add(new R(r.Item2) { Pos = 4 }.Str());
            if (stop != null && lines[lines.Count - 1].StartsWith(stop)) break;
        }
        return lines;
    }

    // The test NPC Brawler (row 900004 at 100000, 100000, Zeon: heat hawk, ZMP-50D, ZAKU bazooka) shoots its
    // longest-range gun, the bazooka, from afar and holds it (slot 0 of its looks); with the GM next to it, it takes
    // up the heat hawk, strikes with it and does not shoot; with the GM away again, it takes the bazooka back; with
    // the GM far away (beyond NpcChaseRange 4000), it stops tracking the GM.
    // The script's "spawn Gmtest squad friendly 410000 3" (after ready3): a new squad of three GMs 1090000004-6, each
    // with its own random name and a rank from Seaman Apprentice (1) to Lieutenant (8), the highest first.
    static void SquadTests()
    {
        string[] ranks = { "Seaman Apprentice", "Seaman", "Petty Officer", "Chief Petty Officer", "Senior Chief Petty Officer",
            "Ensign", "Lieutenant Junior Grade", "Lieutenant" };
        var found = new List<Tuple<int, string>>();
        var squads = new HashSet<string>();
        var until = DateTime.Now.AddSeconds(20);
        while (DateTime.Now < until)
        {
            found.Clear(); squads.Clear();
            var log = GameLog();
            for (uint id = 1090000004; id <= 1090000006; id++)
            {
                var m = System.Text.RegularExpressions.Regex.Match(log, "NPC " + id + @" is (.+?) ([A-Za-z]+)\.");
                if (m.Success) found.Add(Tuple.Create(Array.IndexOf(ranks, m.Groups[1].Value), m.Groups[2].Value));
                var sq = System.Text.RegularExpressions.Regex.Match(log, "spawned friendly NPC " + id + @" \(410000, .*?, squad (\d+)\.");
                if (sq.Success) squads.Add(sq.Groups[1].Value);
            }
            if (found.Count == 3) break;
            System.Threading.Thread.Sleep(500);
        }
        Console.WriteLine("  squad: " + string.Join(", ", found.Select(f => (f.Item1 >= 0 ? ranks[f.Item1] : "?") + " " + f.Item2)));
        Check(found.Count == 3 && found.All(f => f.Item1 >= 0) && found.Select(f => f.Item2).Distinct().Count() == 3 &&
            found[0].Item1 >= found[1].Item1 && found[1].Item1 >= found[2].Item1,
            "#spawn squad: three NPCs with different names and ranks Seaman Apprentice to Lieutenant, the highest first");
        Check(squads.Count == 1, "#spawn squad: they are one squad");
    }

    static void NpcWeaponTests(Conn g, Action<int, int> moveGm, Func<uint, int[]> npcAt)
    {
        const uint brawler = 1000900004;
        const int hawk = 280037, bazooka = 280041;
        Func<int> held = () =>
        {
            g.Send(0x0A, new B().U32(0).U32(brawler).Byte(5).Get());
            var looks = RecvOp(g, 0x800A).Item2;
            return looks.Length >= 17 ? new R(looks) { Pos = 13 }.I32() : 0;
        };
        // Waits for a fire effect (0x803B) with that weapon, or the Brawler's attack result (0x800F) with it.
        Func<uint, int, int, bool> waitFor = (op, weapon, seconds) =>
        {
            var until = DateTime.Now.AddSeconds(seconds);
            while (DateTime.Now < until)
            {
                var r = g.Recv(true); if (r == null || r.Item1 != op) continue;
                if (op == 0x803B && r.Item2.Length >= 24 && new R(r.Item2) { Pos = 20 }.I32() == weapon) return true;
                if (op == 0x800F && r.Item2.Length >= 46 && new R(r.Item2).U32() == brawler && new R(r.Item2) { Pos = 42 }.I32() == weapon) return true;
            }
            return false;
        };

        // Out of its aggro range (1500) even after it wandered (600 at most), then 1000 from it.
        moveGm(103000, 100000);
        var p = npcAt(brawler);
        Check(p != null, "the Brawler (test NPC 900004) is in the position list");
        if (p == null) return;
        moveGm(p[0] + 1000, p[1]);
        Check(waitFor(0x803B, bazooka, 12), "the Brawler shoots the GM 1000 away with its longest-range gun, the bazooka (0x803B 280041)");
        var shot = DateTime.Now;
        Check(held() == bazooka, "and holds it (slot 0 of its looks)");

        // Next to it, it takes up the heat hawk, but strikes only once the bazooka's reload (6-9 s) is over.
        p = npcAt(brawler) ?? p;
        moveGm(p[0] + 150, p[1]);
        Check(waitFor(0x800F, hawk, 15), "with the GM 150 away, it strikes with its heat hawk (0x800F 280037)");
        double wait = (DateTime.Now - shot).TotalSeconds;
        Check(wait >= 5.5, string.Format("only after the bazooka's reload ({0:0.0} s after the shot)", wait));
        Check(held() == hawk, "and holds the heat hawk");
        int shots = 0;
        var quiet = DateTime.Now.AddSeconds(4);
        while (DateTime.Now < quiet) { var r = g.Recv(true); if (r != null && r.Item1 == 0x803B) shots++; }
        Check(shots == 0, "and does not shoot while it holds it (" + shots + " fire effects in 4 s)");

        p = npcAt(brawler) ?? p;
        moveGm(p[0] + 1500, p[1]);
        Check(waitFor(0x803B, bazooka, 12), "with the GM 1500 away, it shoots the bazooka again");
        Check(held() == bazooka, "and holds the bazooka again");

        p = npcAt(brawler) ?? p;
        moveGm(p[0] + 6000, p[1]);
        var giveUp = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < giveUp && !GameLog().Contains("NPC Brawler stopped tracking Gmtest")) System.Threading.Thread.Sleep(200);
        Check(GameLog().Contains("NPC Brawler stopped tracking Gmtest, who is too far away"), "with the GM 6000 away, it stops tracking the GM");
    }

    static void QuestTests(Conn g, uint me)
    {
        int rank0 = int.Parse(Sql("SELECT `rank` FROM appearance WHERE char_id = (SELECT char_id FROM characters WHERE char_name = 'Gmtest')"));
        long money0 = long.Parse(Sql("SELECT char_money FROM characters WHERE char_name = 'Gmtest'"));
        var stranger = new Conn("cms:elsewhere", "127.0.0.1", 42016, "127.0.0.2");
        stranger.Send(0x01, new B().U32(me).Str("Gmtest").U32(0xFFFFFFFF).Get());
        Tuple<uint, byte[]> strangerReply = null;
        try { strangerReply = stranger.Recv(true); } catch (Exception) { }
        Check(strangerReply == null, "a chat login for the GM from another address is refused (disconnected)");
        var cms = new Conn("cms", "127.0.0.1", 42016);
        cms.Send(0x01, new B().U32(me).Str("Gmtest").U32(0xFFFFFFFF).Get());
        Check(RecvOp(cms, 0x8001).Item1 == 0x8001, "the GM logs in to the chat server before the promotion");

        // Far from Sydney (7000, 2000) the Sydney quest cannot be handed in; then the GM walks to Hutton, who gives it.
        g.Send(0x3E, HandIn(me, 151));
        var far = RecvOp(g, 0x803E);
        System.Threading.Thread.Sleep(300);
        Check(far.Item1 == 0x803E && far.Item2[3] != 2 && GameLog().Contains("hand-in of quest 151 refused: you are not near anyone who gives it"),
            "handing in the Sydney quest far from its NPC is refused");
        var told = ChatLines(cms, 3, "You cannot complete");
        Check(told.Exists(l => l.StartsWith("You cannot complete") && l.Contains("not near anyone who gives it")),
            "and the player is told why in the chat window (" + string.Join(" / ", told) + ")");
        g.Send(0x03, new B().U32(0).U32(me).U16(1).Bytes(new byte[6]).I32(72537600).I32(-59309800).I32(7700).U32(0x45FA0000).Get());
        RecvOp(g, 0x8003);
        System.IO.File.WriteAllText(Environment.GetEnvironmentVariable("READY") + "3", "ready");
        uint item = 0;
        var until = DateTime.Now.AddSeconds(20);
        while (DateTime.Now < until && item == 0)
        {
            var r = g.Recv(true);
            if (r != null && r.Item1 == 0x8035 && r.Item2.Length > 16 && new R(r.Item2) { Pos = 12 }.I32() == 550410) item = new R(r.Item2) { Pos = 4 }.U32();
        }
        Check(item != 0, "the quest item 550410 is spawned next to the GM");
        if (item == 0) return;
        g.Send(0x24, Pick(1, me, item, 0x13, me + 110001, 1, 550410, 110001, 0xFF));
        Check(RecvOp(g, 0x8024).Item1 == 0x8024, "the GM picks it up into the backpack");

        g.Send(0x3E, HandIn(me, 1, item));
        var no = RecvOp(g, 0x803E);
        Check(no.Item1 == 0x803E && no.Item2[3] != 2 && new R(no.Item2) { Pos = 4 }.U32() == me, "handing it in for the sunglasses quest is refused (0x803E code " + (no.Item2.Length > 3 ? no.Item2[3] : -1) + ")");

        g.Send(0x3E, HandIn(me, 151, item));
        var ok = RecvOp(g, 0x803E);
        if (ok.Item2.Length < 60 || ok.Item2[3] != 2) { Check(false, "quest 151 is done (0x803E " + Conn.Hex(ok.Item2, 16) + ")"); return; }
        var r2 = new R(ok.Item2);
        ushort section = r2.U16(), code = r2.U16(); uint who = r2.U32(); uint quest = r2.U32(); long price = ((long)r2.I32() << 32) | r2.U32();
        uint cont = r2.U32(); r2.U32(); uint rid = r2.U32(); r2.U32(); int rtpl = r2.I32(); long rcount = ((long)r2.I32() << 32) | r2.U32(); r2.U32();
        int n = r2.Size(); uint used = r2.U32(); r2.U32(); r2.Pos += 16; uint state = r2.U32();
        Console.WriteLine("  0x803E: " + Conn.Hex(ok.Item2, 90));
        Check(ok.Item1 == 0x803E && section == 1 && code == 2 && who == me && quest == 151, "quest 151 is done (0x803E section 1, code 2)");
        Check(price == 50000 && cont == me + 110001 && rtpl == 310007 && rcount == 5 && rid != 0, "it pays 50000 and puts 5 x 310007 in the backpack");
        Check(n == 1 && used == item && state == 8 && ok.Item2.Length > r2.Pos, "the quest item is gone (state 8) and the reward's description follows");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT COUNT(*) FROM container WHERE item_id = 550410") == "0" && Sql("SELECT item_amount FROM container WHERE item_id = 310007 AND char_id = " + me) == "5", "the hand-in and the reward are saved");
        Check(long.Parse(Sql("SELECT char_money FROM characters WHERE char_name = 'Gmtest'")) == money0 + 50000, "and the money");
        Check(Sql("SELECT rank_points FROM character_state WHERE char_id = " + me) == "5", "the quest's bonus gives 5 promotion points");

        g.Send(0x3E, HandIn(me, 151, item));
        var again = RecvOp(g, 0x803E);
        Check(again.Item1 == 0x803E && again.Item2[3] != 2, "the same item cannot be handed in twice");

        // The console adds 20 points: 25 reaches rank 2 (Seaman).
        Tuple<uint, byte[]> promo = null;
        var until2 = DateTime.Now.AddSeconds(15);
        while (DateTime.Now < until2 && promo == null) { var r = g.Recv(true); if (r != null && r.Item1 == 0x8034) promo = r; }
        int delta = promo != null ? (sbyte)promo.Item2[8] : 0;
        Check(promo != null && promo.Item2[4] == 0x81 && delta == 2 - rank0, "#rank points 20 promotes the GM from rank " + rank0 + " to 2 (0x8034 rank change " + delta + ")");
        System.Threading.Thread.Sleep(300);
        Check(Sql("SELECT `rank` FROM appearance WHERE char_id = (SELECT char_id FROM characters WHERE char_name = 'Gmtest')") == "2", "the new rank is saved");

        // The client sends its chat card (CMS 0x13) again with the new rank; the chat server shows the saved one.
        cms.Send(0x13, new B().U32(me).I32(-1).U32(0).Str("Gmtest").Byte(1).Byte(2).Get());
        var card = RecvOp(cms, 0x8012);
        Check(card.Item1 == 0x8012 && card.Item2[card.Item2.Length - 1] == 2, "the chat server's card has the new rank 2 (" + Conn.Hex(card.Item2) + ")");
        TeamAndScriptTests(g, cms, me);
    }

    // The GM makes a team: the game server's position record carries the team at once (CMS link 0x0B). #script is
    // for admins: a GM is told it does not exist; as an admin the GM runs DB/Scripts/example.txt.
    static void TeamAndScriptTests(Conn g, Conn cms, uint me)
    {
        Drain(cms);
        cms.Send(0x0D, new B().Str("Testers").U32(me).Get());
        var made = RecvOp(cms, 0x800C);
        int team = new R(made.Item2) { Pos = 4 }.I32();
        Check(made.Item2[3] == 2 && team >= 51500000, "the GM makes team Testers (" + team + ")");
        System.Threading.Thread.Sleep(300);
        Drain(g);
        g.Send(0x03, new B().U32(0).U32(me).U16(1).Bytes(new byte[6]).I32(72537600).I32(-59309800).I32(7700).U32(0x45FA0000).Get());
        var list = RecvOp(g, 0x8003).Item2;
        var lr = new R(list); lr.U16(); int count = lr.Size(); int mine = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            int o = lr.Pos + i * 53;
            if (new R(list) { Pos = o + 18 }.U32() == me) mine = new R(list) { Pos = o + 44 }.I32();
        }
        Check(mine == team, "the game server's position record of the GM has team " + team + " (" + mine + ")");

        // The help's rule: no new team for 7 days after creating one (CMSServer.xml TeamRecreateDays).
        cms.Send(0x10, new B().U32(me).U32((uint)team).Get());
        System.Threading.Thread.Sleep(300);
        Drain(cms);
        cms.Send(0x0D, new B().Str("Testers Two").U32(me).Get());
        var again = RecvOp(cms, 0x800C);
        Check(again.Item2[3] != 2 && Sql("SELECT COUNT(*) FROM team WHERE name = 'Testers Two'") == "0",
            "the GM left and cannot create another team within 7 days (0x800C code " + again.Item2[3] + ")");

        Func<string, List<string>> say = line =>
        {
            Drain(cms);
            cms.Send(0x03, new B().U32(me).Str(line).U32(2).U32(0).Byte(0x80).Get());
            var lines = new List<string>();
            var until = DateTime.Now.AddSeconds(3);
            while (DateTime.Now < until)
            {
                var r = cms.Recv(true);
                if (r == null) continue;
                if (r.Item1 == 0x8004) { var rr = new R(r.Item2) { Pos = 4 }; lines.Add(rr.Str()); if (lines[lines.Count - 1].StartsWith("Script ")) break; }
            }
            return lines;
        };
        var refused = say("#script example");
        Check(refused.Exists(l => l.Contains("does not exist")), "a GM cannot use #script (" + string.Join(" / ", refused) + ")");
        string access = Sql("SELECT char_access FROM characters WHERE char_id = " + me.ToString().Substring(1));
        Sql("UPDATE characters SET char_access = 9 WHERE char_id = " + me.ToString().Substring(1));
        cms.Close();
        cms = new Conn("cms", "127.0.0.1", 42016);
        cms.Send(0x01, new B().U32(me).Str("Gmtest").U32(0xFFFFFFFF).Get());
        RecvOp(cms, 0x8001);
        var listed = say("#script");
        Check(listed.Exists(l => l.Contains("example")), "an admin's #script lists the scripts (" + string.Join(" / ", listed) + ")");
        var ran = say("#script example");
        Check(ran.Exists(l => l.Contains("restart in 10 minutes")) && ran.Exists(l => l == "Script example: ran 2 commands."),
            "#script example runs its 2 commands (" + string.Join(" / ", ran) + ")");
        var sneaky = say("#script ../Config/CMSServer");
        Check(sneaky.Exists(l => l.Contains("no script called")), "a script name cannot leave DB/Scripts");
        Sql("UPDATE characters SET char_access = " + access + " WHERE char_id = " + me.ToString().Substring(1));
        cms.Close();
    }

    static string GameLog()
    {
        var path = Environment.GetEnvironmentVariable("GAMELOG");
        if (string.IsNullOrEmpty(path)) return "";
        using (var f = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
        using (var r = new System.IO.StreamReader(f)) return r.ReadToEnd();
    }

    // /allianceall (type 1) and /alliance (3) are the faction's chat: the client lists only its own side, and the chat
    // server drops anyone else a modified client lists. /all (0) reaches everyone listed.
    static void AllianceChatTests(uint amuro, uint chr, uint kai)
    {
        Console.WriteLine("== Alliance chat");
        Func<uint, string, Conn> login = (id, name) =>
        {
            var c = new Conn("cms:" + name, "127.0.0.1", 42016);
            c.Send(0x01, new B().U32(id).Str(name).U32(0xFFFFFFFF).Get());
            RecvOp(c, 0x8001);
            return c;
        };
        var ca = login(amuro, "Amuro"); var cc = login(chr, "Char"); var ck = login(kai, "Kai");
        System.Threading.Thread.Sleep(500);
        Func<Conn, List<string>> heard = c =>
        {
            var lines = new List<string>();
            var until = DateTime.Now.AddSeconds(2);
            while (DateTime.Now < until)
            {
                Tuple<uint, byte[]> r;
                try { r = c.Recv(true); } catch (Exception) { break; }
                if (r == null) break;
                if (r.Item1 != 0x8004) continue;
                var rr = new R(r.Item2); uint from = rr.U32(); string text = rr.Str(); uint type = rr.U32();
                if (from == kai) lines.Add(type + ":" + text);
            }
            return lines;
        };
        ca.KeepGains = true;
        ck.Send(0x03, new B().U32(kai).Str("Sieg Zeon").U32(1).U32(0).Size(2).U32(chr).U32(amuro).Get());
        ck.Send(0x03, new B().U32(kai).Str("hello all").U32(0).U32(0).Size(2).U32(chr).U32(amuro).Get());
        var toChar = heard(cc); var toAmuro = heard(ca);
        Check(toChar.Contains("1:Sieg Zeon") && toChar.Contains("0:hello all"), "Char (Zeon) hears Kai's /allianceall and /all (" + string.Join(" / ", toChar) + ")");
        Check(!toAmuro.Contains("1:Sieg Zeon") && toAmuro.Contains("0:hello all"), "Amuro (EF) hears only the /all (" + string.Join(" / ", toAmuro) + ")");
        ca.Close(); cc.Close(); ck.Close();
    }

    // The launcher asks the Login-Server (42012) for the status: 0x8000 body = 0, status (0 online, 1 offline,
    // 2 maintenance). It follows the Lobby and Earth heartbeats and the maintenance flag in server_state.
    static int LoginStatus()
    {
        try
        {
            var l = new Conn("login", "127.0.0.1", 42012);
            l.Send(0x0, new byte[0]);
            var r = l.Recv();
            l.Close();
            if (r == null || r.Item1 != 0x8000 || r.Item2.Length < 8) return -1;
            return r.Item2[4] | r.Item2[5] | r.Item2[6] | r.Item2[7];
        }
        catch (Exception) { return -1; }
    }

    static bool WaitStatus(int want, int seconds = 5)
    {
        for (int i = 0; i < seconds * 4; i++)
        {
            if (LoginStatus() == want) return true;
            System.Threading.Thread.Sleep(250);
        }
        return false;
    }

    static void LoginStatusTests()
    {
        Console.WriteLine("== Login-Server status");
        Check(WaitStatus(0), "launcher status ONLINE while the Lobby and game servers run");
        Sql("UPDATE server_state SET value = 1 WHERE name = 'maintenance'");
        Check(WaitStatus(2), "launcher status MAINTENANCE while the game is closed");
        Sql("UPDATE server_state SET value = 0 WHERE name = 'maintenance'");
        Check(WaitStatus(0), "launcher status ONLINE again after maintenance");
        // A Lobby that stopped beating: the Lobby beats every 10 s, so an old beat may be renewed first; retry.
        bool offline = false;
        for (int i = 0; i < 3 && !offline; i++)
        {
            Sql("UPDATE server_state SET value = 0 WHERE name = 'beat_lobby'");
            offline = WaitStatus(1, 2);
        }
        Check(offline, "launcher status OFFLINE when the Lobby stopped beating");
        Check(WaitStatus(0, 15), "launcher status ONLINE once the Lobby beats again");
    }

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "spawn") return SpawnTest();
        var a = Lobby("tester1", "secret1", "Amuro", 1, true);
        var b = Lobby("tester2", "secret2", "Char", 2, false);
        var c = Lobby("gm1", "secret1", "Kai", 2, false);

        // Seed a stack of 10 cotton yarn in Amuro's backpack for the move tests.
        var seed = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mariadb",
            DbArgs() + " titans-server -e \"INSERT INTO container (char_id, container_id, container_name, item_id, item_name, item_amount, child) VALUES (" + a.Chars[0] + ", 110001, 'backpack', 240000, 'cotton yarn', 10, '')\"") { UseShellExecute = false });
        seed.WaitForExit();
        // Crafting ingredients and skills for CraftTests.
        Sql("INSERT INTO container (char_id, container_id, container_name, item_id, item_name, item_amount, child) VALUES (" + a.Chars[0] + ", 110001, 'backpack', 530001, 'iron ore', 1000, ''), (" + a.Chars[0] + ", 110001, 'backpack', 510001, 'super high tensile steel', 64, ''), (" + a.Chars[0] + ", 110001, 'backpack', 290033, 'jet engine', 1, ''), (" + a.Chars[0] + ", 110001, 'backpack', 510021, 'enhancement package', 10, ''), (" + a.Chars[0] + ", 110001, 'backpack', 510022, 'accuracy improvement package', 2, ''), (" + a.Chars[0] + ", 110001, 'backpack', 510023, 'defensive improvement package', 1, ''), (" + a.Chars[0] + ", 110001, 'backpack', 510000, 'titanium alloy', 40, ''), (" + a.Chars[0] + ", 110001, 'backpack', 510002, 'titanium ceramic composite', 2, '')");
        Sql("INSERT INTO container (char_id, container_id, container_name, item_id, item_name, item_amount, child) VALUES (" + a.Chars[0] + ", 110001, 'backpack', 240088, 'cap', 1, ''), (" + a.Chars[0] + ", 110001, 'backpack', 240092, 'glasses', 1, '')");
        Sql("INSERT INTO container (char_id, container_id, container_name, item_id, item_name, item_amount, child) VALUES (" + a.Chars[0] + ", 110001, 'backpack', 230009, 'silk yarn', 6, ''), (" + a.Chars[0] + ", 110001, 'backpack', 350000, 'basic color dyes', 3, ''), (" + a.Chars[0] + ", 110001, 'backpack', 350001, 'light color dyes', 2, '')");
        // Two camps and the rank to set one up (Lieutenant, 8) for CampTests.
        Sql("INSERT INTO container (char_id, container_id, container_name, item_id, item_name, item_amount, child) VALUES (" + a.Chars[0] + ", 110001, 'backpack', 340001, 'EF Camp Weapon Shop', 1, ''), (" + a.Chars[0] + ", 110001, 'backpack', 340003, 'ZEON Camp Weapon Shop', 1, '')");
        Sql("UPDATE appearance SET `rank` = 8 WHERE char_id = " + a.Chars[0].ToString().Substring(1));
        Sql("DELETE FROM skills WHERE char_id = " + a.Chars[0] + " AND skill_idx IN (22, 24); INSERT INTO skills (char_id, skill_idx, skill_level, skill_exp) VALUES (" + a.Chars[0] + ", 22, 1300, 0), (" + a.Chars[0] + ", 24, 1300, 0)");

        // Game server: wrong key.
        var bad = GameLogin("Amuro", a, a.Key ^ 0xFFFF);
        var badReply = bad.Recv();
        Check(badReply.Item1 == 0x8041 && badReply.Item2.Length == 4 && badReply.Item2[3] == 7, "game login with a wrong key is refused (7)");
        bad.Close();
        // Game server: the right key, but from another address than the Lobby login.
        var elsewhere = new Conn("game:elsewhere", a.IP, a.Port, "127.0.0.2");
        elsewhere.Send(0x41, new B().U32(0x10000).U32(a.Chars[0]).U32(a.Key).U32(0).Str("Amuro").U16(2).Byte(0x80).Get());
        var elsewhereReply = elsewhere.Recv();
        Check(elsewhereReply.Item1 == 0x8041 && elsewhereReply.Item2.Length == 4 && elsewhereReply.Item2[3] == 7,
            "game login with the right key from another address is refused (7)");
        elsewhere.Close();
        PasswordTests();

        var g = GameLogin("Amuro", a, a.Key);
        var login = g.Recv();
        Check(login.Item1 == 0x8041 && Conn.Hex(login.Item2).StartsWith("00 10 00 02"), "game login accepted");
        uint me = a.Chars[0];

        g.Send(0x38, new B().U32(0).U32(me).Byte(1).Get());
        var reg = g.Recv();
        Check(reg.Item1 == 0x8038 && new R(reg.Item2) { Pos = 12 }.U32() == me, "register player echoes the character id");

        g.Send(0x13, new B().U32(0).I32(-1).Byte(0).Get());
        var time = g.Recv();
        Check(time.Item1 == 0x8013, "server time");

        // Walk the container tree like the client does.
        int[][] top = { new[] { 110001, 0x14 }, new[] { 120001, 0x14 }, new[] { 110002, 0x14 }, new[] { 500000, 0x13 }, new[] { 500001, 0x13 }, new[] { 110003, 0x14 }, new[] { 110004, 0x14 }, new[] { 110006, 0x14 } };
        var queue = new Queue<uint[]>();
        foreach (var t in top) queue.Enqueue(new uint[] { me + (uint)t[0], (uint)t[1], 0, 0, t[0] == 500001 ? 500000u : (uint)t[0], 0 });
        int nodes = 0; int items = 0;
        var info = new Dictionary<uint, uint[]>(); // uid -> format, static, parent uid
        while (queue.Count > 0)
        {
            var q = queue.Dequeue();
            g.Send(0x16, new B().U32(0x00020000).U32(me).U32(q[0]).U32(q[1]).U32(q[2]).U32(q[3]).U32(q[4]).U32(q[5]).U32(0xB).Byte(0xFF).Get());
            var rep = g.Recv(true);
            nodes++;
            var r = new R(rep.Item2);
            r.Pos = 36; int tailSize = r.Size(); int tailStart = r.Pos;
            uint uid = r.U32(); uint fmt = r.U32(); r.U32(); uint amount = r.U32(); uint stat = r.U32(); r.U32(); r.U32();
            for (int i = 0; i < (int)fmt - 14; i++) { int os = r.Size(); r.Pos += (i == 5 && os > 0) ? os * 4 : os; }
            int kids = r.Size();
            Console.WriteLine("  item {0:X8} fmt {1:X2} static {2} amount {3} children {4}", uid, fmt, stat, amount, kids);
            if (q[0] == me + 500000) Check(amount == 50000, "money container holds the starting 50000");
            if (q[0] == me + 500001) Check(amount == 0, "credit container is separate and empty");
            Check(uid == q[0] && r.Pos - tailStart <= tailSize, "item info reply for " + q[4]);
            if (q[4] == 120001) Check(kids == 8, "weared has the 8 official slots (" + kids + ")");
            if (q[4] == 110004) Check(kids == 1, "swap pack holds the trade pack");
            for (int i = 0; i < kids; i++)
            {
                uint cu = r.U32(), cf = r.U32(), cs = r.U32();
                if (q[4] == 120001 && i == 0) Check(cu == 0 && cf == 0 && cs == 0xFFFFFFFF, "weared slot 0 is the empty vehicle slot");
                if (cu == 0) continue;
                info[cu] = new uint[] { cf, cs, q[0] };
                queue.Enqueue(new uint[] { cu, cf, q[0], q[1], cs, q[4] });
                items++;
            }
            Check(r.Pos == tailStart + tailSize && rep.Item2[rep.Item2.Length - 1] == 0xFF, "item info tail length matches for " + q[4]);
        }
        Check(items >= 2, "starter kit: " + items + " children found in " + nodes + " nodes");

        uint backpack = me + 110001, bank = me + 110002, hangar = me + 110003, weared = me + 120001;
        uint vehicle = 0, yarn = 0;
        foreach (var kv in info)
        {
            if (kv.Value[2] == hangar) vehicle = kv.Key;
            if (kv.Value[2] == backpack && kv.Value[1] == 240000) yarn = kv.Key;
        }
        Check(vehicle != 0 && yarn != 0, "found the hangar vehicle and the seeded yarn stack");
        uint yarnStack = MoveTests(g, me, backpack, bank, hangar, weared, vehicle, yarn);
        uint tradePack = 0;
        foreach (var kv in info) if (kv.Value[1] == 110005) tradePack = kv.Key;
        TradePackTests(g, me, backpack, tradePack, yarnStack);
        uint vinv = 0;
        foreach (var kv in info) if (kv.Value[2] == vehicle && kv.Value[1] == 110010) vinv = kv.Key;

        g.Send(0x70, new B().U32(0).U32(me).Byte(0xFF).Get());
        Check(g.Recv().Item1 == 0x8070, "occupation cities");

        g.Send(0x00, Coord(me, 1000, 2000, 30));
        Check(g.Recv().Item1 == 0x8000, "register coordinates");
        CampTests(g, me, backpack, info);

        // Second player joins nearby.
        var g2 = GameLogin("Char", b, b.Key);
        Check(g2.Recv().Item1 == 0x8041, "second player logs in");
        uint other = b.Chars[0];
        g2.Send(0x00, Coord(other, 1500, 2500, 30)); g2.Recv();

        g.Send(0x03, new B().U32(a.Acc).U32(me).U16(1).Bytes(new byte[6]).I32(1000).I32(2000).I32(30).U32(0x45FA0000).Get());
        var list = g.Recv();
        var lr = new R(list.Item2); lr.U16(); int count = lr.Size();
        Check(list.Item1 == 0x8003 && count == 2 && list.Item2.Length == 2 + 1 + 2 * 53 + 20, "coordinate list shows both players (" + count + ")");

        g.Send(0x06, new B().U32(1).U32(other).Byte(1).Get());
        var spi = g.Recv(); var sr = new R(spi.Item2) { Pos = 10 };
        Check(spi.Item1 == 0x8006 && sr.Str() == "Char", "simple player info names the other player");

        g.Send(0x0A, new B().U32(0).U32(other).Byte(5).Get());
        var looks = g.Recv();
        Check(looks.Item1 == 0x800A && looks.Item2.Length == 72, "looks of the other player (72 bytes like the capture)");

        g.Send(0x6F, new B().U32(0).U32(other).Get());
        Check(g.Recv().Item1 == 0x806F, "paper doll");
        ClothesTests(g, g2, me, info);

        for (uint i = 0; i < 3; i++)
        {
            g.Send(0x05, new B().I32(1000).I32(2000).I32(3).U32(0x43C80000).U32(i).Get());
            var sc = g.Recv();
            Check(sc.Item1 == 0x8005 && sc.Item2.Length == 4 && sc.Item2[2] == i, "ground items list " + i + " (empty)");
        }

        GroundTests(g, g2, me, other, yarnStack, vehicle, info[vehicle][1], vinv, hangar, weared);
        ShopTests(g, me);
        CraftTests(g, g2, me, info);
        var chr = CombatTests(g, g2, b.Acc, me, other);
        SkillTests(g, g2, me, other);
        uint kai = c.Chars[0];
        var k = GameLogin("Kai", c, c.Key);
        Check(k.Recv().Item1 == 0x8041, "Kai logs in");
        k.Send(0x00, Coord(kai, 1200, 2200, 30)); RecvOp(k, 0x8000);
        if (chr != null) OccupationTests(g, g2, k, me, other, kai, chr);
        if (chr != null) CrimeTests(g2, k, other, kai, c.Acc, chr);
        Drain(g); Drain(k);
        NpcTests(g2, b.Acc, other, chr);
        g = FlightTests(g, a, me);
        Check(NoReply(g2), "Amuro's vehicle stays on the Earth ground while Amuro is in Space");

        var before = Snapshot(g, me);
        g.Send(0x02, Coord(me, 5000, 6000, 40));
        g.Send(0x55, new B().U32(me).Get());
        Check(g.Recv().Item1 == 0x8055, "logout");
        g.Send(0x42, new byte[0]);
        Check(g.Recv().Item1 == 0x8042, "leave game server");
        g.Send(0x3F, new B().U32(me).I32(1).U32(0).Byte(0x80).U16(0).Byte(0x80).Get());
        var quit = g.Recv();
        Check(quit != null && quit.Item1 == 0x803F && quit.Item2.Length == 28 && Conn.Hex(quit.Item2).StartsWith("00 01 00 02"), "0x3F quits (0x803F 00 01 00 02)");
        g.Close();

        // The second player no longer sees the first.
        System.Threading.Thread.Sleep(300);
        g2.Send(0x03, new B().U32(b.Acc).U32(other).U16(1).Bytes(new byte[6]).I32(1500).I32(2500).I32(30).U32(0x45FA0000).Get());
        var list2 = g2.Recv(); var l2 = new R(list2.Item2); l2.U16();
        Check(l2.Size() == 2, "the Earth players (Char and Kai) no longer see Amuro");
        g2.Send(0x05, new B().I32(1500).I32(2500).I32(30).U32(0x457A0000).U32(2).Get());
        var gl = new R(g2.Recv().Item2); gl.Pos = 3;
        int left = gl.Size();
        Check(left == 2, "Amuro's vehicle and the freighter's wreck lie on the ground (" + left + ")");
        Check(Sql("SELECT COUNT(*) FROM container WHERE char_id = " + me + " AND container_id = 110003") == "0", "the ground vehicle is not in the hangar");
        System.Threading.Thread.Sleep(11000);
        var groundRows = Sql("SELECT vehicle, wreck, owner_id, child FROM ground_items WHERE zone = 1 AND vehicle = 1 AND item_id NOT BETWEEN 340000 AND 340003 ORDER BY wreck");
        Console.WriteLine("  ground rows: " + groundRows.Replace("\n", " | "));
        var gr = groundRows.Split('\n');
        Check(gr.Length >= 2 && gr[0].StartsWith("1\t0\t" + me) && gr[0].EndsWith("240000-2"), "the ground vehicle is saved with the world, with its 2 yarn");
        Check(Array.Exists(gr, x => x.StartsWith("1\t1\t" + other)), "the freighter's wreck is saved too, owned by Char who destroyed it");
        Check(Sql("SELECT item_amount FROM container WHERE char_id = " + me + " AND container_id = 110001 AND item_id = 240000") == "3", "3 yarn left in the backpack");
        // 30410 before the mining tests' ThunderGoliath (10000) and drill (2000).
        Check(Sql("SELECT char_money FROM characters WHERE char_name = 'Amuro'") == "21730", "Amuro's money is saved as 21730");
        Check(Sql("SELECT zone FROM characters WHERE char_name = 'Amuro'") == "2", "Amuro is saved in Space");
        Check(Sql("SELECT item_amount FROM container WHERE char_id = " + me + " AND container_id = 500001") == "100", "the bank holds 100 (500 less the 400 paid from it)");
        Check(Sql("SELECT COUNT(*) FROM flights") == "0", "the flight is over");
        RelogTest(a, me, before);
        if (chr != null) ExileTest(g2, k, other, kai, chr);
        g2.Close();
        k.Close();

        AllianceChatTests(a.Chars[0], b.Chars[0], c.Chars[0]);
        LoginStatusTests();
        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures;
    }
}

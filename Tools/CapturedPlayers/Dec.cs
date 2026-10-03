using System; using System.IO; using Common.Network.Encryption;
class Dec { static void Main(string[] a) {
  var crypt = new UCEncryption();
  foreach (var f in a) {
    var buf = File.ReadAllBytes(f); int off = 0;
    Console.WriteLine("##### " + Path.GetFileName(f));
    while (buf.Length - off >= 64) {
      var h = new byte[64]; Array.Copy(buf, off, h, 0, 64);
      uint key = crypt.DecryptHeader(h, 0);
      int seq = BitConverter.ToInt32(h, 12), xs = BitConverter.ToInt32(h, 16), bs = BitConverter.ToInt32(h, 20), op = BitConverter.ToInt32(h, 24);
      if (BitConverter.ToUInt32(h,0) != 0x64616568 || bs < 0 || bs > 65536) { Console.WriteLine("BAD HEADER at " + off); break; }
      if (buf.Length - off < 64 + bs) { Console.WriteLine("TRUNCATED"); break; }
      var p = new byte[64 + bs]; Array.Copy(h, p, 64); Array.Copy(buf, off + 64, p, 64, bs);
      crypt.DecryptBody(p, 0, p.Length, key);
      Console.WriteLine(string.Format("op=0x{0:X5} seq={1} len={2}", op, seq, xs));
      for (int i = 0; i < xs; i += 16) { Console.Write("  {0:X4}: ", i); for (int j = i; j < Math.Min(xs, i + 16); j++) Console.Write("{0:X2} ", p[64 + j]); Console.WriteLine(); }
      off += 64 + bs;
    }
  }
}}

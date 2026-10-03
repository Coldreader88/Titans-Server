# pcap -> per stream+direction raw payload files (seq-ordered, deduplicated)
import subprocess, sys, os, collections
pcap, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
r = subprocess.run(['tshark','-r',pcap,'-Y','tcp.len>0','-T','fields','-e','tcp.stream','-e','ip.src','-e','tcp.srcport','-e','ip.dst','-e','tcp.dstport','-e','tcp.seq_raw','-e','tcp.payload','-e','frame.number'],capture_output=True,text=True)
segs = collections.defaultdict(dict); first = {}
for line in r.stdout.splitlines():
    p = line.split('\t')
    if len(p) < 8 or not p[6]: continue
    key = (int(p[0]), p[1], p[2], p[3], p[4])
    seq = int(p[5]); data = bytes.fromhex(p[6].replace(':',''))
    first.setdefault(key, int(p[7]))
    if seq not in segs[key] or len(data) > len(segs[key][seq]): segs[key][seq] = data
for key in sorted(segs, key=lambda k: first[k]):
    d = segs[key]; seqs = sorted(d); buf = bytearray(); base = seqs[0]
    for s in seqs:
        off = (s - base) & 0xffffffff
        if off > len(buf): buf.extend(b'\0'*(off-len(buf)))
        buf[off:off+len(d[s])] = d[s]
    name = f"{first[key]:06d}_s{key[0]}_{key[1]}_{key[2]}_to_{key[3]}_{key[4]}.bin"
    open(os.path.join(outdir,name),'wb').write(buf)
    print(name, len(buf))

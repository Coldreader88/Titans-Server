# Builds DB/Npcs/captured_players.csv (for the GM command #spawnfromlist) from the decrypted captures that decall.sh made:
#   python3 Tools/CapturedPlayers/extract_players.py <OUT>/caps [output csv]
# Every player (character id below 1000000000; NPCs are 1000000000 + n) in a position list (0x8003), with the name and
# faction of 0x8006 and the looks of 0x800A. One line per player: the last record of the capture that shows them most,
# with the looks that match it (vehicle looks of the same template, or on-foot looks), from that capture if it has them.
import sys, os, glob, struct, collections

CAPS = sys.argv[1] if len(sys.argv) > 1 else 'caps'
OUTFILE = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'DB', 'Npcs', 'captured_players.csv')

def packets():
    """yields (pcap file, stream name, op, seq, body) in file order"""
    for fn in sorted(glob.glob(os.path.join(CAPS,'*.txt'))):
        f=None; st=None; cur=None
        for l in open(fn,errors='replace'):
            if l.startswith('FILE '): f=l[5:].strip(); continue
            if l.startswith('##### '):
                if cur: yield cur
                cur=None; st=l[6:].strip(); continue
            if l.startswith('op='):
                if cur: yield cur
                p=l.split(); cur=(f,st,int(p[0][3:],16),int(p[1][4:]),bytearray())
            elif cur and l.startswith('  ') and ':' in l:
                cur[4].extend(bytes.fromhex(l.split(':',1)[1]))
        if cur: yield cur
def size(b,o):
    v=0;sh=0
    while True:
        c=b[o];o+=1
        v|=(c&0x7f)<<sh; sh+=7
        if c&0x80: return v,o
def coords(b):
    n,o=size(b,2); out=[]
    for i in range(n):
        r=b[o:o+53]; o+=53
        x,y,z,tilt,roll,d,cid,mid,cl,vuid,vtpl,rank,acl,act,state,fac,esum,upc,team,dmg,atk=struct.unpack('>iiihhhIHHIiBBBBHHHiBi',r)
        out.append(dict(x=x,y=y,z=z,tilt=tilt,roll=roll,dir=d,cid=cid,mid=mid,cluster=cl,vuid=vuid,vtpl=vtpl,rank=rank,acl=acl,action=act,state=state,faction=fac,esum=esum,upc=upc,team=team,dmg=dmg,atk=atk))
    return out

isn=lambda v:1000000000<=v<=1100000000
recs=collections.defaultdict(list); names={}; looks=collections.defaultdict(list); order=0
for f,st,op,seq,b in packets():
    b=bytes(b); order+=1
    if op==0x8003:
        try: rs=coords(b)
        except Exception: continue
        for r in rs:
            if not isn(r['cid']) and 0<r['cid']<1000000000: r['file']=f; r['ord']=order; recs[r['cid']].append(r)
    elif op==0x8006 and len(b)>10:
        acc,cid=struct.unpack_from('>II',b,0)
        if isn(cid) or acc==0xFFFFFFFF: continue
        n,o=size(b,10); names[cid]=(acc,b[9],b[o:o+2*n].decode('utf-16-le','replace'))
    elif op==0x800A and len(b)>12:
        kind,cid=struct.unpack_from('>II',b,0)
        if isn(cid): continue
        looks[cid].append(dict(ord=order,kind=kind,b=b,file=f))
def parse_vlooks(b):
    tpl=struct.unpack_from('>i',b,8)[0]; n,o=size(b,12)
    arms=[struct.unpack_from('>i',b,o+4*i)[0] for i in range(n)]; o+=4*n
    m,o=size(b,o); held=b[o:o+m]; o+=m
    return tpl,arms,held
def parse_flooks(b):
    s=struct.unpack_from('>H',b,8)[0]; g=b[10]; o=12
    cl=[]
    for i in range(8): cl.append((struct.unpack_from('>H',b,o)[0],b[o+2])); o+=3
    tail=b[o:]
    skin,face,hs,hc=tail[2],tail[4],tail[7],tail[8]
    rebuilt=bytes(b[:12])+b''.join(struct.pack('>HB',w,st) for w,st in cl)+bytes([0,0,skin,0,face,0,0,hs,hc,0])+bytes(26)
    return dict(sum=s,gender=g,clothes=cl,skin=skin,face=face,hair=hs,haircolour=hc,ok=rebuilt==b)
out=[]; stats=collections.Counter()
for cid,rs in recs.items():
    byfile=collections.Counter(r['file'] for r in rs)
    f=byfile.most_common(1)[0][0]
    r=[x for x in rs if x['file']==f][-1]
    L=looks[cid]
    def pick(pred):
        same=[l for l in L if pred(l) and l['file']==f]
        anyf=[l for l in L if pred(l)]
        return (same or anyf or [None])[-1]
    rec=dict(r); rec['name']=names.get(cid,(0,0,''))[2]; rec['acc']=names.get(cid,(0,0,''))[0]
    if r['vtpl']==-1:
        l=pick(lambda l:l['kind']==0x30002)
        rec['fl']=parse_flooks(l['b']) if l else None
        stats['foot']+=1; stats['foot_looks']+=l is not None
        if l and not rec['fl']['ok']: stats['foot_bad']+=1
    else:
        l=pick(lambda l:l['kind']==0x40002 and struct.unpack_from('>i',l['b'],8)[0]==r['vtpl'])
        rec['vl']=parse_vlooks(l['b']) if l else None
        stats['veh']+=1; stats['veh_looks']+=l is not None
    stats['named']+=bool(rec['name'])
    out.append(rec)

clean=lambda s:s.replace(',',' ').replace('\n',' ').replace('\r',' ').strip()
L=['# Players the official server showed in the packet captures (UCGO Packet Logs.zip), one line each, for the GM command #spawnfromlist.',
'# Each player as last seen in the capture that shows them most: position, vehicle, weapons, clothes. Made by Tools/CapturedPlayers/extract_players.py.',
'# id: the captured character id (for reference; spawns get NPC ids). name: empty when the capture never had it (a random name is used).',
'# faction: 1 EF, 2 Zeon. criminal: 1 = shown as a criminal. zone: 1 Earth, 2 Space. team: captured team id, -1 none (one team = one squad).',
'# vehicle: template, -1 = on foot. armaments: weapon/shield template per slot (-1 empty; -1 alone = no weapons); empty = not captured (random weapons when spawned).',
'# gender..clothes: an on-foot player\'s looks; clothes = 8 slots wear:style (dress top coat bottom shoes gloves hat glasses, -1 empty).',
'# Empty looks = not captured (borrows a captured look of the same faction). capture: the capture file the line comes from.',
'id,name,faction,criminal,zone,x,y,z,tilt,roll,dir,rank,action,team,vehicle,armaments,gender,skin,face,hair,haircolour,clothes,capture']
for r in sorted(out,key=lambda r:r['cid']):
    arms=''; looks=['']*6
    if r['vtpl']!=-1 and r.get('vl'): arms='/'.join(map(str,r['vl'][1])) or '-1'
    if r['vtpl']==-1 and r.get('fl'):
        fl=r['fl']; looks=[fl['gender'],fl['skin'],fl['face'],fl['hair'],fl['haircolour'],'/'.join('%d:%d'%(w if w<0x8000 else w-0x10000,s) for w,s in fl['clothes'])]
    row=[r['cid'],clean(r['name']),r['faction']&0xff,(r['faction']>>8)&1,r['cluster'],r['x'],r['y'],r['z'],r['tilt'],r['roll'],r['dir'],r['rank'],r['action'],r['team'],r['vtpl'],arms]+looks+[clean(os.path.basename(r['file']))]
    L.append(','.join(map(str,row)))
open(OUTFILE,'w',encoding='utf-8',newline='\n').write('\n'.join(L)+'\n')
print('%d players (%d on foot) -> %s' % (len(L)-8, sum(1 for r in out if r['vtpl']==-1), OUTFILE))

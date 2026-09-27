#unmanaged

#include "Stdafx.h"
#include "PacketHandler.h"
#include "XORMask.h"

namespace PacketHandler
{
static unsigned short mwc16_x;
static unsigned short mwc16_y;

void mwc16_Seed(unsigned int seed){
   mwc16_x = (20970 ^ seed) | 1;
   mwc16_y = (30459 ^ seed) | 2;
}

unsigned short mwc16_Rand(){
   mwc16_x = 20970 * (mwc16_x & 0xffff) + (mwc16_x >> 16);
   mwc16_y = 30459 * (mwc16_y & 0xffff) + (mwc16_y >> 16);
   return (mwc16_x * mwc16_y) & 0xffff;
}
//==================================================================================================
PKH::PKH()
{
    RECVBUFF.Reset(0x1000); //4096 bytes, 4kb
    SENDBUFF.Reset(0x1000); //4096 bytes, 4kb
    MaxPacketSize = 0x800;  //2048 bytes, 2kb
    packet = new char[MaxPacketSize];
    mwc16_Seed(time(0));
    blowfish.SetPassword("chrTCPPassword");
    MaxStoredPackets = 16;
    storedPackets = 0;
    sizeQueue = new unsigned int[MaxStoredPackets];

}
//==================================================================================================
PKH::PKH(unsigned int BufferSize,unsigned int PacketSize,char* Password)
{
    RECVBUFF.Reset(BufferSize); //4096 bytes, 4kb
    SENDBUFF.Reset(BufferSize); //4096 bytes, 4kb
    MaxPacketSize = PacketSize;  //2048 bytes, 2kb
    packet = new char[MaxPacketSize];
    mwc16_Seed(time(0));
    blowfish.SetPassword(Password);
    storedPackets = 0;
    MaxStoredPackets = BufferSize/64;
    sizeQueue = new unsigned int[MaxStoredPackets];

}
//==================================================================================================
PKH::~PKH()
{
    delete [] packet;
    delete [] sizeQueue;
}
//==================================================================================================
int PKH::AddRecvBuffer(char* data, unsigned int length)
{
    return RECVBUFF.WriteData(data,length);
}
//==================================================================================================
int PKH::RecvPacket(char* &data, unsigned int &length)
{
    unsigned short randVal = 0;
    unsigned int KEY = 0;
    unsigned int XORSize, blfSize;
    if(RECVBUFF.PeekData(packet,64)) //if ReadData errors.... (not enough data, usually).
        return 0;
    blowfish.Decrypt(packet,64);
    randVal = *(((unsigned int*)packet)+1);
    KEY = XORMask::Keygen(randVal);
    XORMask::Decrypt(packet,64,KEY);
    if(*(unsigned int*)packet != 0x64616568) //if first INT isnt head, packet aint valid!
       return 0;
    *(((unsigned int*)packet) + 1) = (unsigned int)randVal;
    XORSize = *(((unsigned int*)packet) + 4);
    blfSize = *(((unsigned int*)packet) + 5);
    if(XORSize > blfSize) //impossible condition, to ensure packet is correct
        return 0;
    if(blfSize > RECVBUFF.GetSize()-64) //safe measure for buffer overrun protection
        return 0;
    RECVBUFF.Erase(64); //clear the header from our buffer, the rest of the packet is present so extraction is safe.
    RECVBUFF.ReadData(packet+64,blfSize); //there should be enough data for this based on the check above.
    blowfish.Decrypt(packet+64,blfSize);
    XORMask::Decrypt(packet+64,XORSize,KEY);
    data = packet;
    length = 64 + blfSize;
    return 1;
}
//==================================================================================================
int PKH::SendPacket(char* data, unsigned int length)
{
    if(storedPackets == MaxStoredPackets) //if we have hit our theoretical limit (and the buffer would notify this well before in most cases), dont let it go further.
        return -1;
    unsigned short randVal = mwc16_Rand();
    unsigned int KEY = XORMask::Keygen(randVal);
    unsigned int XORSize, blfSize;
    if(length < 64 || length > MaxPacketSize)
        return -1;
    XORSize = *(((unsigned int*)data) + 4);
    blfSize = *(((unsigned int*)data) + 5);
    if(XORSize > length-64) //if it will cause our buffer to exceed specified size... error
        return -1;
    if(blfSize+64 != length) //if given size isnt what the packet says it is... error
        return -1;
    XORMask::Encrypt(data,64+XORSize,KEY);
    *(((unsigned int*)data) + 1) = (unsigned int)randVal;
    blowfish.Encrypt(data,length);
    sizeQueue[storedPackets] = length;
    storedPackets++;
    return SENDBUFF.WriteData(data,length); //attempt to write data in, return error if it occurs.
}
//==================================================================================================
int PKH::GetSendBuffer(char* &data, unsigned int &length)
{
    unsigned int sentPackets;
    unsigned int sendSize = 0;
    for(sentPackets = 0; sentPackets < storedPackets;sentPackets++)
    {
        if(sendSize + sizeQueue[sentPackets] > MaxPacketSize) //if it cant fit
        {
            break; //stop
        }
        else //add it in and move on!
        {
            sendSize += sizeQueue[sentPackets];
        }
    }
    if(sentPackets == 0) //no packets =(
        return 0;
    if(SENDBUFF.GetSize() < sendSize) //somehow more packets are theoretically stored than there really is. lol?
        return 0;
    SENDBUFF.ReadData(packet,sendSize); //read chunk of buffer out. If its here than theres enough data to read.
    //shift size array over
    for(unsigned int i = 0;i < storedPackets-sentPackets;i++)
        sizeQueue[i] = sizeQueue[sentPackets+i];
    //update stored quantity
    storedPackets-=sentPackets;
    //update "return" variables and return
    data = packet;
    length = sendSize;
    return 1;
}
//==================================================================================================
void PKH::SetPassword(char* Password)
{
    blowfish.SetPassword(Password);
}
//==================================================================================================
bool PKH::IsRecvEmpty()
{
    return RECVBUFF.IsEmpty();
}
//==================================================================================================
bool PKH::IsSendEmpty()
{
    return SENDBUFF.IsEmpty();
}
//==================================================================================================
}
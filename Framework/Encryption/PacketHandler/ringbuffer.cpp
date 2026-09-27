#include "Stdafx.h"
#include "ringbuffer.h"
namespace PacketHandler
{
//==================================================================================================
ringbuffer::ringbuffer()
{
    MaxSize = 1024;
    buffer = new char[MaxSize];
    Size = 0;
    getPos = 0;
    putPos = 0;
}
//==================================================================================================
ringbuffer::~ringbuffer()
{
    delete [] buffer;
}
//==================================================================================================
int ringbuffer::WriteData(char* data, unsigned int length)
{
    if(Size+length > MaxSize)
        return -1;
    if(putPos + length <= MaxSize) //fits directly
    {
        memcpy(buffer+putPos,data,length);
    }
    else //wraps
    {
        register unsigned int offset = MaxSize - putPos;
        memcpy(buffer+putPos,data,offset);
        memcpy(buffer,data+offset,length-offset);
    }
    Size += length;
    putPos += length;
    putPos %= MaxSize;
    return 0;
}
//==================================================================================================
int ringbuffer::ReadData(char* data, unsigned int length)
{
    if(length > Size)
        return -1;
    if(getPos + length <= MaxSize) //fits directly
    {
        memcpy(data,buffer+getPos,length);
    }
    else //wraps
    {
        register unsigned int offset = MaxSize - getPos;
        memcpy(data,buffer+getPos,offset);
        memcpy(data+offset,buffer,length-offset);
    }
    Size -= length;
    getPos += length;
    getPos %= MaxSize;
    return 0;
}
//==================================================================================================
int ringbuffer::PeekData(char* data, unsigned int length)
{
    if(length > Size)
        return -1;
    if(getPos + length <= MaxSize) //fits directly
    {
        memcpy(data,buffer+getPos,length);
    }
    else //wraps
    {
        register unsigned int offset = MaxSize - getPos;
        memcpy(data,buffer+getPos,offset);
        memcpy(data+offset,buffer,length-offset);
    }
    return 0;
}
//==================================================================================================
void ringbuffer::Erase(unsigned int length)
{
    if(length > Size)
        length = Size;
    Size -= length;
    getPos += length;
    getPos %= MaxSize;     
}
//==================================================================================================
void ringbuffer::Reset(unsigned int BufferSize)
{
    MaxSize = BufferSize;
    delete [] buffer;
    buffer = new char[MaxSize];
    Size = 0;
    getPos = 0;
    putPos = 0;
}
//==================================================================================================
bool ringbuffer::IsEmpty()
{
    if(Size == 0)
        return true;
    else
        return false;
}
//==================================================================================================
unsigned int ringbuffer::GetMaxSize()
{
    return MaxSize;
}
//==================================================================================================
unsigned int ringbuffer::GetSize()
{
    return Size;
}
//==================================================================================================
}
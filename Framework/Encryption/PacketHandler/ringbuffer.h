#ifndef RING_BUFFER_H
#define RING_BUFFER_H

namespace PacketHandler
{
class ringbuffer
{
  private:
    char* buffer;  
    unsigned int getPos;
    unsigned int putPos;
    unsigned int Size;
    unsigned int MaxSize;  
  public:
    ringbuffer();
    ~ringbuffer();
    int WriteData(char* data, unsigned int length);
    int ReadData(char* data, unsigned int length);
    int PeekData(char* data, unsigned int length);
    void Erase(unsigned int length);
    bool IsEmpty();
    void Reset(unsigned int MaxSize);
    unsigned int GetMaxSize();
    unsigned int GetSize();
};
}

#endif

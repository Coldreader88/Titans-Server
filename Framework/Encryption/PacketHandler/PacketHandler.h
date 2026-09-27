#ifndef UCGO_PACKET_HANDLER_H
#define UCGO_PACKET_HANDLER_H

#include "ringbuffer.h"
#include "blowfish.h"

using namespace System;

namespace PacketHandler
{
class PKH
{
  private:
    //private variables n stuff. Ignore.
    Blowfish blowfish;
    char* packet;
    unsigned int* sizeQueue;
    unsigned int storedPackets;
    unsigned int MaxStoredPackets;
    unsigned int MaxPacketSize;
    ringbuffer RECVBUFF;
    ringbuffer SENDBUFF;
  public:
    //default constructor. Values are BufferSize = 4kb, PacketSize = 2kb, Password = chrTCPPassword (should work fine for UCGO).
    PKH();
    //customizable constructor. In case you want to change settings.
    PKH(unsigned int BufferSize,unsigned int PacketSize,char* Password);
    //Destructor for cleanup.
    ~PKH();
    //pass a encrypted packet into the handler with this function.
    //it will return 0 on success, -1 on failure.
    int AddRecvBuffer(char* data, unsigned int length);
    //send packets into the handler with this function.
    //it will return 0 on succes, -1 on failure.
    int SendPacket(char* data, unsigned int length);
    //read decrypted RECV packets out of the handler with this function. The pointer and integer will get filled in with the correct values.
    //returns 1 on success, 0 on failure. This allows it to be used in a while() loop for repeated extraction.
    int RecvPacket(char* &data, unsigned int &length);
    //read encrypted SEND packets out of the handler with this function. The pointer and integer will get filled in with the correct values.
    //returns 1 on succes, 0 on failure. This allows it to be used in a while() loop for repeated extraction.
    int GetSendBuffer(char* &data, unsigned int &length);
    //allows you to change the blowfish password used in the Crypto.
    void SetPassword(char* Password);
    //returns true if the RECVBUFF is empty.
    bool IsRecvEmpty();
    //returns true if the SENDBUYFF is empty.
    bool IsSendEmpty();
};

public ref class PacketHandler
{
	private: 
		PKH* _pkh;

	public:

		PacketHandler()
		{
			_pkh = new PKH();
		};

		PacketHandler(unsigned int BufferSize,unsigned int PacketSize,unsigned char* Password)
		{
			_pkh = new PKH(BufferSize,PacketSize,(char*)Password);
		};

		~PacketHandler()
		{
			delete _pkh;
		};

    int AddRecvBuffer(unsigned char* data, unsigned int length)
	{
		return _pkh->AddRecvBuffer((char*)data,length);
	};

    int SendPacket(unsigned char* data, unsigned int length)
	{
		return _pkh->SendPacket((char*)data,length);
	};

    int RecvPacket(unsigned char* &data, unsigned int &length)
	{
		return _pkh->RecvPacket((char*&)data,length);
	};

    int GetSendBuffer(unsigned char* &data, unsigned int &length)
	{
		return _pkh->GetSendBuffer((char*&)data,length);
	};

    void SetPassword(unsigned char* Password)
	{
		_pkh->SetPassword((char*)Password);
	};

    bool IsRecvEmpty()
	{	
		return _pkh->IsRecvEmpty();
	};

    bool IsSendEmpty()
	{
		return _pkh->IsSendEmpty();
	};
};

public ref class MBlowfish
{
private:
	Blowfish *blowfish;

public:
	MBlowfish()
	{
		blowfish = new Blowfish();
	}

	~MBlowfish()
	{
		delete blowfish;
	}

	void Reset()
	{
		blowfish->Reset();
	}

	void SetPassword(String^ password, int passwordLength)
	{
		char* pass;
		int length = passwordLength;
		IntPtr^ data = System::Runtime::InteropServices::Marshal::StringToHGlobalAuto(password);
		pass = (char*)data->ToPointer();
		blowfish->SetPassword(pass, length);
	}

	array<unsigned char, 1>^ Decrypt(array<unsigned char, 1> ^data)
	{
		char* data2;
		int length = data->Length;
		System::Runtime::InteropServices::Marshal::Copy(data, 0, (IntPtr)data2, data->Length);
		blowfish->Decrypt(data2,length);
		array<unsigned char, 1>^ data3;
		System::Runtime::InteropServices::Marshal::Copy((System::IntPtr)data2, data3, 0, length);
		return data3;
	}
};
}
#endif

//=============================================================================
//  File:     XORMask.cpp - XORMask Crypto Encrypt() Decrypt() for UCGO
//            Reversed and Recoded by Xenozephyr!! 
//
//  Author:   Eric "Xenozephyr" Dyoniziak
//  Licensees: (write names here)
//
//  Date:     June 23rd, 2007
//
//  Copyright(C) 2007
//  This source code is property of the above author and licensees, and may not
//   be edited, distributed, or compiled without their written permission.
//  This copyright notice must remain in its original form and may not be
//   edited or removed, regardless if permission to compile, edit, or
//   distribute this source code was given.
//  This intellectual property is protected internationally by the
//   World Intellectual Property Organization (WIPO) Copyright Treaty
//   and locally by the Digital Millennium Copyright Act (DMCA). Violation
//   may result in legal actions taken by the author and/or licensees.
//  This source code is distributed in the hope that it will be useful, but
//   WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.

#include "Stdafx.h"
#include "XORMask.h"
#include "XORTable.h"

namespace PacketHandler
{
unsigned int XORMask::Keygen(unsigned short Offset){
   unsigned int KEY = XORTABLE[Offset];
   KEY |= Offset << 16;
   return KEY;
}

void XORMask::Encrypt(void* data, unsigned int length,register unsigned int KEY){
   if((data == 0) || (length <= 0) || (length > 0x800))
      return;
   register unsigned int blocks = length / 4;
   register unsigned int counter = 0;
   register unsigned int* Ipointer = (unsigned int*)data;
   for(;counter < blocks;counter++)
      Ipointer[counter] ^= KEY;
   register unsigned char* Bpointer = (unsigned char*)data + 4*blocks;
   blocks = length & 3;
   counter = 0;
   for(;counter < blocks;counter++)
      *Bpointer ^= (KEY >> (counter * 8));
}


void XORMask::Decrypt(void* data, unsigned int length,register unsigned int KEY){
   if((data == 0) || (length <= 0) || (length > 0x800))
      return;
   register unsigned int blocks = length / 4;
   register unsigned int counter = 0;
   register unsigned int* Ipointer = (unsigned int*)data;
   for(;counter < blocks;counter++)
      Ipointer[counter] ^= KEY;
   register unsigned char* Bpointer = (unsigned char*)data + 4*blocks;
   blocks = length & 3;
   counter = 0;
   for(;counter < blocks;counter++)
      *Bpointer ^= (KEY >> (counter * 8));
}
}
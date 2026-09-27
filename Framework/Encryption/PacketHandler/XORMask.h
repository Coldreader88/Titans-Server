//=============================================================================
//  File:     XORMask.h - XORMask Crypto Encrypt() Decrypt() for UCGO
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

#ifndef __UCGO_XORMASK_
#define __UCGO_XORMASK_


namespace PacketHandler
{
class XORMask
{
 public:
   static unsigned int Keygen(unsigned short Offset);
   static void Encrypt(void* data,unsigned int length,register unsigned int KEY);
   static void Decrypt(void* data,unsigned int length,register unsigned int KEY);
};
}
#endif


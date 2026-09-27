using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SmartEngine.Core;
using SmartEngine.Network.IO;

namespace Common.Network.Encryption.UCGO
{

public class XORMask
{
    public List<short> XORTABLE;
    private SmartEngine.Network.IO.BinaryReaderV2 table;
    
    private void LoadTable()
    {
        var shortBuff = new byte[2];
        short Value = 0;
        //var br = new BinaryReader(table);

        for(int i = 0;i < 65536;i++)
        {
            try
            {
                //shortBuff = table.ReadBytes(2);
                Value = table.ReadInt16(); //br.ReadInt16();
            } catch(Exception ex){
                Logger.ShowError(ex);  
            }

            //Value = (short)(((shortBuff[1] & 0x0ff) << 8) | (shortBuff[0] & 0x0ff));
            XORTABLE.Add(Value);
        }

        if (table != null)
        {
            //if (br != null)
            //{
            //    br.Close();
            //}

            table.SuperClose();
        }

    }

    public XORMask()
    {
        try {
            table = new BinaryReaderV2(File.Open("XORTable.dat", FileMode.Open));//FileStream("XORTable.dat", FileMode.Open);
        } catch(FileNotFoundException ex){
            Logger.ShowError(ex);
        }
        XORTABLE = new List<short>(65536);
        LoadTable();
    }



    public int Keygen(short Offset)
    {   int KEY = (int)XORTABLE[((int)Offset & 0x0ffff)] & 0x0ffff;
        return (((int)Offset) << 16 | KEY);
    }


    public void Encrypt(byte[] data, int length, int KEY)
    {
        byte A = (byte)((KEY >> 24) & 0x0ff);
        byte B = (byte)((KEY >> 16) & 0x0ff);
        byte C = (byte)((KEY >>  8) & 0x0ff);
        byte D = (byte)KEY;
        int blocks = length / 4;
        blocks *= 4;
        int counter = 0;
        while(counter < blocks)
        {
            data[counter++] ^= D; 
            data[counter++] ^= C; 
            data[counter++] ^= B; 
            data[counter++] ^= A;    
        }
        blocks = (length - counter) & 3;
        switch(blocks)
        {
            case 3:
                data[counter] ^= B;
                break;
            case 2:
                data[counter] ^= C;
                break;
            case 1:
                data[counter] ^= D;
                break;
            case 0:
                break;
        }
    }

    public void Decrypt(byte[] data, int length, int KEY)
    {
        byte A = (byte)((KEY >> 24) & 0x0ff);
        byte B = (byte)((KEY >> 16) & 0x0ff);
        byte C = (byte)((KEY >>  8) & 0x0ff);
        byte D = (byte)KEY;
        int blocks = length / 4;
        blocks *= 4;
        int counter = 0;
        while(counter < blocks)
        {
            data[counter++] ^= D; 
            data[counter++] ^= C; 
            data[counter++] ^= B; 
            data[counter++] ^= A;    
        }
        blocks = (length - counter) & 3;
        switch(blocks)
        {
            case 3:
                data[counter] ^= B;
                break;
            case 2:
                data[counter] ^= C;
                break;
            case 1:
                data[counter] ^= D;
                break;
            case 0:
                break;
        }
    }
}
}

using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x19: moves money to or from the bank.
    ///
    /// <code>
    /// uint16 BE   4 = money to the bank, 5 = bank to money
    /// uint16 BE   0
    /// uint32 BE   character id
    /// 20 bytes   0
    /// uint32 BE   amount
    /// </code>
    /// The reply (0x8019) is the request with 2 in bytes 2-3 (Desposite_Money_(1).pcap,
    /// Withdrawal_Money_(1).pcap). Java reference: RequestUpdateDeposite.java / UpdateDeposit.java.
    /// </summary>
    public class CM_UPDATE_DEPOSIT : UCPacket<GSOpcode>
    {
        public const int ToBank = 4;
        public const int FromBank = 5;

        public CM_UPDATE_DEPOSIT()
        {
            this.ID = GSOpcode.CM_UPDATE_DEPOSIT;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_UPDATE_DEPOSIT();
        }

        public byte[] Body { get; private set; }
        public int Direction { get; private set; }
        public uint CharacterID { get; private set; }
        public int Amount { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 32)
            {
                return;
            }
            Body = this.GetBytes((ushort)this.Remaining);
            Direction = Bytes.U16(Body, 0);
            CharacterID = Bytes.U32(Body, 4);
            Amount = (int)Bytes.U32(Body, 28);

            ((UCGameSession)client).OnUpdateDeposit(this);
        }
    }
}

using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x43 CancelAnotherGameFEReservation (UC_IDMsg, 9 bytes: ticket, character id, 0xFF): the client drops the flight
    /// 0x40 reserved, after a failed purchase or when its vehicle is destroyed (specs/re-unknown-packets.md 1.2).
    /// </summary>
    public class CM_CANCEL_ANOTHER_GAME_FE : UCPacket<GSOpcode>
    {
        public CM_CANCEL_ANOTHER_GAME_FE()
        {
            this.ID = GSOpcode.CM_CANCEL_ANOTHER_GAME_FE;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_CANCEL_ANOTHER_GAME_FE();
        }

        public override void OnProcess(Session<GSOpcode> client)
        {
            ((UCGameSession)client).OnCancelAnotherGameFE(this);
        }
    }
}

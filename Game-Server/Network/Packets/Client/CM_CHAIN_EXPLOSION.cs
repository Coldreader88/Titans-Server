using Common.Network.Packets;
using SmartEngine.Network;
using TitansUC.GameServer.Network.Client;

namespace TitansUC.GameServer.Network.Packets.Client
{
    /// <summary>
    /// 0x12: the player's vehicle was caught in a nearby explosion. The client sends it when its own vehicle
    /// is within the exploding vehicle's chain_explosion_radius (MS 40, tank 20, MA 60, Gaw 100); the server
    /// decides the damage.
    ///
    /// <code>
    /// uint32 BE   character id
    /// uint32 BE   own vehicle unique id, format
    /// uint32 BE   template id of the vehicle that exploded
    /// </code>
    /// Field names from the client (UC_ChainExplosion: _playerID, _vehicleID, _expl_tempID); layout from
    /// ismay_symp_damage_megellan.pcap. Java reference: RequestChainExplosion.java.
    /// </summary>
    public class CM_CHAIN_EXPLOSION : UCPacket<GSOpcode>
    {
        public CM_CHAIN_EXPLOSION()
        {
            this.ID = GSOpcode.CM_CHAIN_EXPLOSION;
        }

        public override Packet<GSOpcode> New()
        {
            return new CM_CHAIN_EXPLOSION();
        }

        public uint CharacterID { get; private set; }
        public uint VehicleUniqueID { get; private set; }
        public int VehicleFormat { get; private set; }
        public int ExplodedTemplateID { get; private set; }

        public override void OnProcess(Session<GSOpcode> client)
        {
            if (this.Remaining < 16)
            {
                return;
            }
            var body = this.GetBytes((ushort)this.Remaining);
            CharacterID = Bytes.U32(body, 0);
            VehicleUniqueID = Bytes.U32(body, 4);
            VehicleFormat = (int)Bytes.U32(body, 8);
            ExplodedTemplateID = (int)Bytes.U32(body, 12);

            ((UCGameSession)client).OnChainExplosion(this);
        }
    }
}

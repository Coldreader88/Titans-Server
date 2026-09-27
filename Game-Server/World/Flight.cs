using Common.Characters;

namespace TitansUC.GameServer.World
{
    /// <summary>
    /// A shuttle flight between Earth and Space (Earth_To_Space.pcap, Space_to_Earth.pcap): the client asks
    /// where to go (0x40), leaves the game server (0x42), logs in again with the same session key (0x41), asks
    /// for the player info (0x5F, answered with the shuttle and the take-off point), places itself on the other
    /// side (0x00, still in the shuttle) and throws the shuttle away (0x15). The shuttle is kept here in between
    /// so it keeps its unique id; it is not saved.
    /// </summary>
    public class Flight
    {
        public ushort Cluster { get; set; }
        public ItemNode Shuttle { get; set; }
        public Transport Transport { get; set; }
    }
}

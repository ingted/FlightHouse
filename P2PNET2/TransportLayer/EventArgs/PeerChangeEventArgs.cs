using System.Collections.Generic;


namespace P2PNET.TransportLayer.EventArgs
{
    public class PeerChangeEventArgs : System.EventArgs
    {
        public List<Peer> Peers { get; }
        public Peer removedPeer;
        //constructor
        public PeerChangeEventArgs( List<Peer> peers )
        {
            this.Peers = peers;
        }
    }
}

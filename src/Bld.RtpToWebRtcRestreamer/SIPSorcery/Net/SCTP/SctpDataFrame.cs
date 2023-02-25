namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.SCTP
{
    internal struct SctpDataFrame
    {
        public static SctpDataFrame Empty = new SctpDataFrame();

        private bool Unordered;
        public ushort StreamID;
        public ushort StreamSeqNum;
        public uint PPID;
        public byte[] UserData;

        public SctpDataFrame(bool unordered, ushort streamID, ushort streamSeqNum, uint ppid, byte[] userData)
        {
            Unordered = unordered;
            StreamID = streamID;
            StreamSeqNum = streamSeqNum;
            PPID = ppid;
            UserData = userData;
        }

        public bool IsEmpty()
        {
            return UserData == null;
        }
    }
}
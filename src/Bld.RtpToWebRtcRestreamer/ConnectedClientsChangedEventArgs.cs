namespace Bld.RtpToWebRtcRestreamer
{
    public class ConnectedClientsChangedEventArgs : EventArgs
    {
        public ConnectedClientsChangedEventArgs(int newCount)
        {
            NewCount = newCount;
        }

        public int NewCount { get; }
    }
}
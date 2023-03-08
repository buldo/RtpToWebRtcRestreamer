namespace Bld.RtpToWebRtcRestreamer;

internal class ConnectedClientsChangedEventArgs : EventArgs
{
    public ConnectedClientsChangedEventArgs(int newCount)
    {
        NewCount = newCount;
    }

    public int NewCount { get; }
}
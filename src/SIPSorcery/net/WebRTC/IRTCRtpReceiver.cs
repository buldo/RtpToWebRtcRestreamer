namespace SIPSorcery.Net
{
    /// <summary>
    /// The RTCRtpReceiver interface allows an application to inspect the receipt of a MediaStreamTrack.
    /// </summary>
    /// <remarks>
    /// As specified at https://www.w3.org/TR/webrtc/#rtcrtpreceiver-interface.
    /// </remarks>
    public interface IRTCRtpReceiver
    {
        MediaStreamTrack track { get; }
        //readonly attribute RTCDtlsTransport? transport;
        //static RTCRtpCapabilities? getCapabilities(DOMString kind);
        //RTCRtpReceiveParameters getParameters();
        //sequence<RTCRtpContributingSource> getContributingSources();
        //sequence<RTCRtpSynchronizationSource> getSynchronizationSources();
        //Task<RTCStatsReport> getStats();
    };
}
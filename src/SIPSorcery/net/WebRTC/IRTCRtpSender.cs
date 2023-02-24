namespace SIPSorcery.Net
{
    /// <summary>
    /// The RTCRtpSender interface allows an application to control how a given MediaStreamTrack 
    /// is encoded and transmitted to a remote peer. When setParameters is called on an 
    /// RTCRtpSender object, the encoding is changed appropriately.
    /// </summary>
    /// <remarks>
    /// As specified at https://www.w3.org/TR/webrtc/#rtcrtpsender-interface.
    /// </remarks>
    public interface IRTCRtpSender
    {
        MediaStreamTrack track { get; }
        //readonly attribute RTCDtlsTransport? transport;
        //static RTCRtpCapabilities? getCapabilities(DOMString kind);
        //Task setParameters(RTCRtpSendParameters parameters);
        //RTCRtpSendParameters getParameters();
        //Task replaceTrack(MediaStreamTrack withTrack);
        //void setStreams(MediaStream... streams);
        //Task<RTCStatsReport> getStats();
    };
}
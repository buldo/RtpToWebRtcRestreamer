namespace Bld.RtpToWebRtcRestreamer.RtpReceiver.Rtp;

internal class RTPEvent
{
    /// <summary>
    /// The ID for the event. For a DTMF tone this is the digit/letter to represent.
    /// </summary>
    private byte EventID { get; set; }

    /// <summary>
    /// If true the end of event flag will be set.
    /// </summary>
    private bool EndOfEvent { get; set; }

    /// <summary>
    /// The volume level to set.
    /// </summary>
    private ushort Volume { get; set; }

    /// <summary>
    /// The duration for the full event.
    /// </summary>
    private ushort TotalDuration { get; set; }


    /// <summary>
    /// The ID of the event payload type. This gets set in the RTP header.
    /// </summary>
    private int PayloadTypeID { get;  set; }

    /// <summary>
    /// Create a new RTP event object.
    /// </summary>
    /// <param name="eventID">The ID for the event. For a DTMF tone this is the digit/letter to represent.</param>
    /// <param name="endOfEvent">If true the end of event flag will be set.</param>
    /// <param name="volume">The volume level to set.</param>
    /// <param name="totalDuration">The event duration.</param>
    /// <param name="payloadTypeID">The ID of the event payload type. This gets set in the RTP header.</param>
    public RTPEvent(byte eventID, bool endOfEvent, ushort volume, ushort totalDuration, int payloadTypeID)
    {
        EventID = eventID;
        EndOfEvent = endOfEvent;
        Volume = volume;
        TotalDuration = totalDuration;
        PayloadTypeID = payloadTypeID;
    }
}
//-----------------------------------------------------------------------------
// Filename: DCEP.cs
//
// Description: Contains functions for working with the WebRTC Data
// Channel Establishment Protocol (DCEP).
//
// Remarks:
//
// - RFC8832 "WebRTC Data Channel Establishment Protocol"
//   https://tools.ietf.org/html/rfc8832
//   The Data Channel Establishment Protocol (DCEP) is designed to
//   provide, in the WebRTC data channel context, a simple in-
//   band method for opening symmetric data channels. DCEP messages
//   are sent within SCTP DATA chunks (this is the in-band bit) and
//   uses a two-way handshake to open a data channel.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 24 MAr 2021	Aaron Clauson	Created, Dublin, Ireland.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Text;
using Bld.RtpToWebRtcRestreamer.SIPSorcery.Sys.Net;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
/// Represents a Data Channel Establishment Protocol (DECP) OPEN message.
/// This message is initially sent using the data channel on the stream
/// used for user messages.
/// </summary>
/// <remarks>
/// See https://tools.ietf.org/html/rfc8832#section-5.1
/// </remarks>
internal struct DataChannelOpenMessage
{
    private const int DCEP_OPEN_FIXED_PARAMETERS_LENGTH = 12;

    /// <summary>
    ///  This field holds the IANA-defined message type for the
    /// DATA_CHANNEL_OPEN message.The value of this field is 0x03.
    /// </summary>
    public byte MessageType;

    /// <summary>
    /// This field specifies the type of data channel to be opened.
    /// For a list of the formal options <see cref="DataChannelTypes"/>.
    /// </summary>
    public byte ChannelType;

    /// <summary>
    /// The priority of the data channel.
    /// </summary>
    public ushort Priority;

    /// <summary>
    /// Used to set tolerance for partially reliable data channels.
    /// </summary>
    public uint Reliability;

    /// <summary>
    /// The name of the data channel. May be an empty string.
    /// </summary>
    public string Label;

    /// <summary>
    /// If it is a non-empty string, it specifies a protocol registered in the
    /// "WebSocket Subprotocol Name Registry" created in RFC6455.
    /// </summary>
    /// <remarks>
    /// The websocket subprotocol names and specification are available at
    /// https://tools.ietf.org/html/rfc7118
    /// </remarks>
    public string Protocol;

    /// <summary>
    /// Parses the an DCEP open message from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to parse the message from.</param>
    /// <param name="posn">The position in the buffer to start parsing from.</param>
    /// <returns>A new DCEP open message instance.</returns>
    public static DataChannelOpenMessage Parse(byte[] buffer, int posn)
    {
        if (buffer.Length < DCEP_OPEN_FIXED_PARAMETERS_LENGTH)
        {
            throw new ApplicationException("The buffer did not contain the minimum number of bytes for a DCEP open message.");
        }

        var dcepOpen = new DataChannelOpenMessage();

        dcepOpen.MessageType = buffer[posn];
        dcepOpen.ChannelType = buffer[posn + 1];
        dcepOpen.Priority = NetConvert.ParseUInt16(buffer, posn + 2);
        dcepOpen.Reliability = NetConvert.ParseUInt32(buffer, posn + 4);

        var labelLength = NetConvert.ParseUInt16(buffer, posn + 8);
        var protocolLength = NetConvert.ParseUInt16(buffer, posn + 10);

        if (labelLength > 0)
        {
            dcepOpen.Label = Encoding.UTF8.GetString(buffer, 12, labelLength);
        }

        if (protocolLength > 0)
        {
            dcepOpen.Protocol = Encoding.UTF8.GetString(buffer, 12 + labelLength, protocolLength);
        }

        return dcepOpen;
    }
}
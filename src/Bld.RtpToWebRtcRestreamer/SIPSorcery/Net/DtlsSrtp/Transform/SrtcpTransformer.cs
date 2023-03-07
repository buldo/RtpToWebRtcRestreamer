//-----------------------------------------------------------------------------
// Filename: SrtcpTransformer.cs
//
// Description: Encapsulates the encryption/decryption logic for SRTCP packets.
//
// Derived From:
// https://github.com/RestComm/media-core/blob/master/rtp/src/main/java/org/restcomm/media/core/rtp/crypto/SRTCPTransformer.java
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// Original Source: AGPL-3.0 License
//-----------------------------------------------------------------------------

using System.Collections.Concurrent;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

/// <summary>
/// SRTCPTransformer implements PacketTransformer.
/// It encapsulate the encryption / decryption logic for SRTCP packets
///
/// @author Bing SU (nova.su @gmail.com)
/// @author Werner Dittmann<Werner.Dittmann@t-online.de>
/// </summary>
internal class SrtcpTransformer : IPacketTransformer
{
    private int _isLocked;
    private readonly RawPacket _packet;

    private readonly SrtpTransformEngine _forwardEngine;
    private readonly SrtpTransformEngine _reverseEngine;

    /** All the known SSRC's corresponding SRTCPCryptoContexts */
    private readonly ConcurrentDictionary<long, SrtcpCryptoContext> _contexts;

    public SrtcpTransformer(SrtpTransformEngine engine) : this(engine, engine)
    {

    }

    private SrtcpTransformer(SrtpTransformEngine forwardEngine, SrtpTransformEngine reverseEngine)
    {
        _packet = new RawPacket();
        _forwardEngine = forwardEngine;
        _reverseEngine = reverseEngine;
        _contexts = new ConcurrentDictionary<long, SrtcpCryptoContext>();
    }

    public byte[] Transform(byte[] pkt, int offset, int length)
    {
        var isLocked = Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0;
        try
        {
            // Wrap the data into raw packet for readable format
            var packet = !isLocked ? _packet : new RawPacket();
            packet.Wrap(pkt, offset, length);

            // Associate the packet with its encryption context
            long ssrc = packet.GetRtcpssrc();
            SrtcpCryptoContext context;
            _contexts.TryGetValue(ssrc, out context);

            if (context == null)
            {
                context = _forwardEngine.GetDefaultContextControl().DeriveContext();
                context.DeriveSrtcpKeys();
                _contexts.AddOrUpdate(ssrc, context, (_, _) => context);
            }

            // Secure packet into SRTCP format
            context.TransformPacket(packet);
            var result = packet.GetData();

            return result;
        }
        finally
        {
            //Unlock
            if (!isLocked)
                Interlocked.CompareExchange(ref _isLocked, 0, 1);
        }
    }

    public byte[] ReverseTransform(byte[] pkt, int offset, int length)
    {
        var isLocked = Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0;
        try
        {
            // wrap data into raw packet for readable format
            var packet = !isLocked ? _packet : new RawPacket();
            packet.Wrap(pkt, offset, length);

            // Associate the packet with its encryption context
            long ssrc = packet.GetRtcpssrc();
            SrtcpCryptoContext context;
            _contexts.TryGetValue(ssrc, out context);

            if (context == null)
            {
                context = _reverseEngine.GetDefaultContextControl().DeriveContext();
                context.DeriveSrtcpKeys();
                _contexts.AddOrUpdate(ssrc, context, (_, _) => context);
            }

            // Decode packet to RTCP format
            byte[] result = null;
            var reversed = context.ReverseTransformPacket(packet);
            if (reversed)
            {
                result = packet.GetData();
            }
            return result;
        }
        finally
        {
            //Unlock
            if (!isLocked)
                Interlocked.CompareExchange(ref _isLocked, 0, 1);
        }
    }
}
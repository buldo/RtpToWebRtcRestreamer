//-----------------------------------------------------------------------------
// Filename: SrtpTransformer.cs
//
// Description:  SRTPTransformer implements PacketTransformer and provides
// implementations for RTP packet to SRTP packet transformation and SRTP
// packet to RTP packet transformation logic.
//
// Derived From:
// https://github.com/RestComm/media-core/blob/master/rtp/src/main/java/org/restcomm/media/core/rtp/crypto/SRTPTransformer.java
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

/**
*
* Code derived and adapted from the Jitsi client side SRTP framework.
*
* Distributed under LGPL license.
* See terms of license at gnu.org.
*//**
* SRTPTransformer implements PacketTransformer and provides implementations for
* RTP packet to SRTP packet transformation and SRTP packet to RTP packet
* transformation logic.
*
* It will first find the corresponding SRTPCryptoContext for each packet based
* on their SSRC and then invoke the context object to perform the
* transformation and reverse transformation operation.
*
* @author Bing SU (nova.su@gmail.com)
* @author Rafael Soares (raf.csoares@kyubinteractive.com)
*
*/

using System.Collections.Concurrent;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

internal class SrtpTransformer
{
    private int _isLocked;
    private readonly RawPacket _rawPacket;

    private readonly SrtpTransformEngine _forwardEngine;
    private readonly SrtpTransformEngine _reverseEngine;

    /**
	     * All the known SSRC's corresponding SRTPCryptoContexts
	     */
    private readonly ConcurrentDictionary<long, SrtpCryptoContext> _contexts;

    public SrtpTransformer(SrtpTransformEngine engine) : this(engine, engine)
    {
    }

    private SrtpTransformer(SrtpTransformEngine forwardEngine, SrtpTransformEngine reverseEngine)
    {
        _forwardEngine = forwardEngine;
        _reverseEngine = reverseEngine;
        _contexts = new ConcurrentDictionary<long, SrtpCryptoContext>();
        _rawPacket = new RawPacket();
    }

    public byte[] Transform(byte[] pkt, int offset, int length)
    {
        var isLocked = Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0;

        try
        {
            // Updates the contents of raw packet with new incoming packet
            var rawPacket = !isLocked ? _rawPacket : new RawPacket();
            rawPacket.Wrap(pkt, offset, length);

            // Associate packet to a crypto context
            long ssrc = rawPacket.GetSsrc();
            _contexts.TryGetValue(ssrc, out var context);

            if (context == null)
            {
                context = _forwardEngine.DefaultContext.DeriveContext(0, 0);
                context.DeriveSrtpKeys(0);
                _contexts.AddOrUpdate(ssrc, context, (_, _) => context);
            }

            // Transform RTP packet into SRTP
            context.TransformPacket(rawPacket);
            var result = rawPacket.GetData();

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
            // Wrap data into the raw packet for readable format
            var rawPacket = !isLocked ? _rawPacket : new RawPacket();
            rawPacket.Wrap(pkt, offset, length);

            // Associate packet to a crypto context
            long ssrc = rawPacket.GetSsrc();
            _contexts.TryGetValue(ssrc, out var context);
            if (context == null)
            {
                context = _reverseEngine.DefaultContext.DeriveContext(0, 0);
                context.DeriveSrtpKeys(rawPacket.GetSequenceNumber());
                _contexts.AddOrUpdate(ssrc, context, (_, _) => context);
            }

            byte[] result = null;
            var reversed = context.ReverseTransformPacket(rawPacket);
            if (reversed)
            {
                result = rawPacket.GetData();
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
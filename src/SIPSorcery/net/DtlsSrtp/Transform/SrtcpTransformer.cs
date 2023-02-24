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
using System.Threading;

namespace SIPSorcery.Net
{
    /// <summary>
    /// SRTCPTransformer implements PacketTransformer.
    /// It encapsulate the encryption / decryption logic for SRTCP packets
    ///
    /// @author Bing SU (nova.su @gmail.com)
    /// @author Werner Dittmann<Werner.Dittmann@t-online.de>
    /// </summary>
    public class SrtcpTransformer : IPacketTransformer
    {
        private int _isLocked;
        private RawPacket packet;

        private SrtpTransformEngine forwardEngine;
        private SrtpTransformEngine reverseEngine;

        /** All the known SSRC's corresponding SRTCPCryptoContexts */
        private ConcurrentDictionary<long, SrtcpCryptoContext> contexts;

        public SrtcpTransformer(SrtpTransformEngine engine) : this(engine, engine)
        {

        }

        public SrtcpTransformer(SrtpTransformEngine forwardEngine, SrtpTransformEngine reverseEngine)
        {
            packet = new RawPacket();
            this.forwardEngine = forwardEngine;
            this.reverseEngine = reverseEngine;
            contexts = new ConcurrentDictionary<long, SrtcpCryptoContext>();
        }

        public byte[] Transform(byte[] pkt, int offset, int length)
        {
            var isLocked = Interlocked.CompareExchange(ref _isLocked, 1, 0) != 0;
            try
            {
                // Wrap the data into raw packet for readable format
                var packet = !isLocked ? this.packet : new RawPacket();
                packet.Wrap(pkt, offset, length);

                // Associate the packet with its encryption context
                long ssrc = packet.GetRTCPSSRC();
                SrtcpCryptoContext context;
                contexts.TryGetValue(ssrc, out context);

                if (context == null)
                {
                    context = forwardEngine.GetDefaultContextControl().DeriveContext();
                    context.DeriveSrtcpKeys();
                    contexts.AddOrUpdate(ssrc, context, (_, _) => context);
                }

                // Secure packet into SRTCP format
                context.TransformPacket(packet);
                byte[] result = packet.GetData();

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
                var packet = !isLocked ? this.packet : new RawPacket();
                packet.Wrap(pkt, offset, length);

                // Associate the packet with its encryption context
                long ssrc = packet.GetRTCPSSRC();
                SrtcpCryptoContext context;
                contexts.TryGetValue(ssrc, out context);

                if (context == null)
                {
                    context = reverseEngine.GetDefaultContextControl().DeriveContext();
                    context.DeriveSrtcpKeys();
                    contexts.AddOrUpdate(ssrc, context, (_, _) => context);
                }

                // Decode packet to RTCP format
                byte[] result = null;
                bool reversed = context.ReverseTransformPacket(packet);
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
}

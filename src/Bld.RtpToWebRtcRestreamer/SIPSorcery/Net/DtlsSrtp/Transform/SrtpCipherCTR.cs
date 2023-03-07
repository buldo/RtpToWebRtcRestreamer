//-----------------------------------------------------------------------------
// Filename: SrtpCipherCTR.cs
//
// Description: Implements SRTP Counter Mode Encryption.
//
// Derived From:
// https://github.com/jitsi/jitsi-srtp/blob/master/src/main/java/org/jitsi/srtp/crypto/SrtpCipherCtr.java
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
//
// License:
// Customisations: BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// Original Source: Apache License: see below
//-----------------------------------------------------------------------------

/*
 * Copyright @ 2016 - present 8x8, Inc
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

/**
 * SRTPCipherF8 implements SRTP F8 Mode AES Encryption (AES-f8).
 * F8 Mode AES Encryption algorithm is defined in RFC3711, section 4.1.2.
 *
 * Other than Null Cipher, RFC3711 defined two encryption algorithms:
 * Counter Mode AES Encryption and F8 Mode AES encryption. Both encryption
 * algorithms are capable to encrypt / decrypt arbitrary length data, and the
 * size of packet data is not required to be a multiple of the AES block
 * size (128bit). So, no padding is needed.
 *
 * Please note: these two encryption algorithms are specially defined by SRTP.
 * They are not common AES encryption modes, so you will not be able to find a
 * replacement implementation in common cryptographic libraries.
 *
 * As defined by RFC3711: F8 mode encryption is optional.
 *
 *                        mandatory to impl     optional      default
 * -------------------------------------------------------------------------
 *   encryption           AES-CM, NULL          AES-f8        AES-CM
 *   message integrity    HMAC-SHA1                -          HMAC-SHA1
 *   key derivation       (PRF) AES-CM             -          AES-CM
 *
 * We use AESCipher to handle basic AES encryption / decryption.
 *
 * @author Bing SU (nova.su@gmail.com)
 * @author Werner Dittmann <werner.dittmann@t-online.de>
 */

using System.Buffers;

using Org.BouncyCastle.Crypto;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform;

/// <summary>
/// SRTPCipherCTR implements SRTP Counter Mode AES Encryption(AES-CM).
/// Counter Mode AES Encryption algorithm is defined in RFC3711, section 4.1.1.
///
/// Other than Null Cipher, RFC3711 defined two two encryption algorithms:
/// Counter Mode AES Encryption and F8 Mode AES encryption.Both encryption
/// algorithms are capable to encrypt / decrypt arbitrary length data, and the
/// size of packet data is not required to be a multiple of the AES block
/// size (128bit). So, no padding is needed.
///
/// Please note: these two encryption algorithms are specially defined by SRTP.
/// They are not common AES encryption modes, so you will not be able to find a
/// replacement implementation in common cryptographic libraries.
///
/// As defined by RFC3711: Counter Mode Encryption is mandatory..
///
/// mandatory to impl     optional      default
/// -------------------------------------------------------------------------
/// encryption          AES-CM,   NULL AES-f8 AES-CM
/// message integrity   HMAC-SHA1 -    HMAC-SHA1
/// key derivation(PRF) AES-CM    -    AES-CM
///
/// We use AESCipher to handle basic AES encryption / decryption.
/// </summary>
internal class SrtpCipherCtr
{
    private const int BLOCK_LENGTH = 16;
    private const int MAX_BUFFER_LENGTH = 10 * 1024;
    private readonly ArrayPool<byte> _bytesPool = ArrayPool<byte>.Shared;
    private byte[] _streamBuf = new byte[1024];

    public void Process(IBlockCipher cipher, MemoryStream data, int off, int len, byte[] iv)
    {
        // if data fits in inner buffer - use it. Otherwise allocate bigger
        // buffer store it to use it for later processing - up to a defined
        // maximum size.
        byte[] cipherStream;
        if (len > _streamBuf.Length)
        {
            cipherStream = new byte[len];
            if (cipherStream.Length <= MAX_BUFFER_LENGTH)
            {
                _streamBuf = cipherStream;
            }
        }
        else
        {
            cipherStream = _streamBuf;
        }

        GetCipherStream(cipher, cipherStream, len, iv);
        for (var i = 0; i < len; i++)
        {
            data.Position = i + off;
            var byteToWrite = data.ReadByte();
            data.Position = i + off;
            data.WriteByte((byte)(byteToWrite ^ cipherStream[i]));
        }
    }

    /// <summary>
    /// Computes the cipher strea for AES CM mode. See section 4.1.1 in RFC3711 for detailed description.
    /// </summary>
    /// <param name="aesCipher"></param>
    /// <param name="output">byte array holding the output cipher stream</param>
    /// <param name="length">length of the cipher stream to produce, in bytes</param>
    /// <param name="iv">initialization vector used to generate this cipher stream</param>
    public void GetCipherStream(IBlockCipher aesCipher, Span<byte> output, int length, ReadOnlySpan<byte> iv)
    {
        var cipherInBlockArray = _bytesPool.Rent(BLOCK_LENGTH);
        var cipherInBlock = cipherInBlockArray.AsSpan(0, BLOCK_LENGTH);
        var tmpCipherBlockArray = _bytesPool.Rent(BLOCK_LENGTH);
        var tmpCipherBlock = tmpCipherBlockArray.AsSpan(0, BLOCK_LENGTH);
        try
        {
            iv[0..14].CopyTo(cipherInBlock[0..14]);

            int ctr;
            for (ctr = 0; ctr < length / BLOCK_LENGTH; ctr++)
            {
                // compute the cipher stream
                cipherInBlock[14] = (byte)((ctr & 0xFF00) >> 8);
                cipherInBlock[15] = (byte)((ctr & 0x00FF));

                aesCipher.ProcessBlock(cipherInBlock, output[(ctr * BLOCK_LENGTH)..]);
            }

            // Treat the last bytes:
            cipherInBlock[14] = (byte)((ctr & 0xFF00) >> 8);
            cipherInBlock[15] = (byte)((ctr & 0x00FF));

            aesCipher.ProcessBlock(cipherInBlock, tmpCipherBlock);
            tmpCipherBlock[0..(length % BLOCK_LENGTH)].CopyTo(output.Slice(ctr * BLOCK_LENGTH, length % BLOCK_LENGTH));
        }
        finally
        {
            _bytesPool.Return(cipherInBlockArray);
            _bytesPool.Return(tmpCipherBlockArray);
        }
    }
}
//-----------------------------------------------------------------------------
// Filename: SrtpCipherF8.cs
//
// Description: Implements SRTP F8 Mode Encryption for 128 bits block cipher.
//
// Derived From:
// https://github.com/jitsi/jitsi-srtp/blob/master/src/main/java/org/jitsi/srtp/crypto/SrtpCipherF8.java
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
 * Copyright @ 2015 - present 8x8, Inc
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
 * Other than Null Cipher, RFC3711 defined two two encryption algorithms:
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

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform
{
    internal static class SrtpCipherF8
    {
        /**
         * AES block size, just a short name.
         */
        private const int Blklen = 16;

        /**
         * F8 mode encryption context, see RFC3711 section 4.1.2 for detailed
         * description.
         */
        private class F8Context
        {
            public byte[] S;
            public byte[] IvAccent;
            public long J;
        }

        public static void DeriveForIv(IBlockCipher f8Cipher, byte[] key, byte[] salt)
        {
            /*
             * Get memory for the special key. This is the key to compute the
             * derived IV (IV').
             */
            var saltMask = new byte[key.Length];
            var maskedKey = new byte[key.Length];

            /*
             * First copy the salt into the mask field, then fill with 0x55 to get a
             * full key.
             */
            Array.Copy(salt, 0, saltMask, 0, salt.Length);
            for (var i = salt.Length; i < saltMask.Length; ++i)
            {
                saltMask[i] = 0x55;
            }

            /*
             * XOR the original key with the above created mask to get the special
             * key.
             */
            for (var i = 0; i < key.Length; i++)
            {
                maskedKey[i] = (byte)(key[i] ^ saltMask[i]);
            }

            /*
             * Prepare the f8Cipher with the special key to compute IV'
             */
            var encryptionKey = new KeyParameter(maskedKey);
            f8Cipher.Init(true, encryptionKey);
        }

        public static void Process(IBlockCipher cipher, MemoryStream data, int off, int len,
                byte[] iv, IBlockCipher f8Cipher)
        {
            var f8Ctx = new F8Context();

            /*
             * Get memory for the derived IV (IV')
             */
            f8Ctx.IvAccent = new byte[Blklen];

            /*
             * Use the derived IV encryption setup to encrypt the original IV to produce IV'.
             */
            f8Cipher.ProcessBlock(iv, 0, f8Ctx.IvAccent, 0);

            f8Ctx.J = 0; // initialize the counter
            f8Ctx.S = new byte[Blklen]; // get the key stream buffer

            Arrays.Fill(f8Ctx.S, 0);

            var inLen = len;

            while (inLen >= Blklen)
            {
                ProcessBlock(cipher, f8Ctx, data, off, data, off, Blklen);
                inLen -= Blklen;
                off += Blklen;
            }

            if (inLen > 0)
            {
                ProcessBlock(cipher, f8Ctx, data, off, data, off, inLen);
            }
        }

        /**
         * Encrypt / Decrypt a block using F8 Mode AES algorithm, read len bytes
         * data from in at inOff and write the output into out at outOff
         * 
         * @param f8ctx
         *            F8 encryption context
         * @param in
         *            byte array holding the data to be processed
         * @param inOff
         *            start offset of the data to be processed inside in array
         * @param out
         *            byte array that will hold the processed data
         * @param outOff
         *            start offset of output data in out
         * @param len
         *            length of the input data
         */
        private static void ProcessBlock(IBlockCipher cipher, F8Context f8Ctx,
                MemoryStream @in, int inOff, MemoryStream @out, int outOff, int len)
        {
            /*
             * XOR the previous key stream with IV'
             * ( S(-1) xor IV' )
             */
            for (var i = 0; i < Blklen; i++)
            {
                f8Ctx.S[i] ^= f8Ctx.IvAccent[i];
            }

            /*
             * Now XOR (S(n-1) xor IV') with the current counter, then increment 
             * the counter
             */
            f8Ctx.S[12] ^= (byte)(f8Ctx.J >> 24);
            f8Ctx.S[13] ^= (byte)(f8Ctx.J >> 16);
            f8Ctx.S[14] ^= (byte)(f8Ctx.J >> 8);
            f8Ctx.S[15] ^= (byte)(f8Ctx.J);
            f8Ctx.J++;

            /*
             * Now compute the new key stream using AES encrypt
             */
            cipher.ProcessBlock(f8Ctx.S, 0, f8Ctx.S, 0);

            /*
             * As the last step XOR the plain text with the key stream to produce
             * the cipher text.
             */
            for (var i = 0; i < len; i++)
            {
                @in.Position = inOff + i;
                var inByte = @in.ReadByte();
                @out.Position = outOff + i;
                @out.WriteByte((byte)(inByte ^ f8Ctx.S[i]));
            }
        }
    }
}

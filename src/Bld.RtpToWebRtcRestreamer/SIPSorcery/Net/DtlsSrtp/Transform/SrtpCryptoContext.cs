//-----------------------------------------------------------------------------
// Filename: SrtpCrtpyoContext.cs
//
// Description: SrtpCryptoContext class is the core class of SRTP implementation. 
// There can be multiple SRTP sources in one SRTP session.And each SRTP stream 
// has a corresponding SrtpCryptoContext object, identified by SSRC.In this way,
// different sources can be protected independently.
//
// Derived From:
// https://github.com/jitsi/jitsi-srtp/blob/master/src/main/java/org/jitsi/srtp/SrtpCryptoContext.java
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
 *
 * Some of the code in this class is derived from ccRtp's SRTP implementation,
 * which has the following copyright notice:
 *
 * Copyright (C) 2004-2006 the Minisip Team
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307 USA
*/

/**
 * SRTPCryptoContext class is the core class of SRTP implementation. There can
 * be multiple SRTP sources in one SRTP session. And each SRTP stream has a
 * corresponding SRTPCryptoContext object, identified by SSRC. In this way,
 * different sources can be protected independently.
 * 
 * SRTPCryptoContext class acts as a manager class and maintains all the
 * information used in SRTP transformation. It is responsible for deriving
 * encryption keys / salting keys / authentication keys from master keys. And it
 * will invoke certain class to encrypt / decrypt (transform / reverse
 * transform) RTP packets. It will hold a replay check db and do replay check
 * against incoming packets.
 * 
 * Refer to section 3.2 in RFC3711 for detailed description of cryptographic
 * context.
 * 
 * Cryptographic related parameters, i.e. encryption mode / authentication mode,
 * master encryption key and master salt key are determined outside the scope of
 * SRTP implementation. They can be assigned manually, or can be assigned
 * automatically using some key management protocol, such as MIKEY (RFC3830),
 * SDES (RFC4568) or Phil Zimmermann's ZRTP protocol (RFC6189).
 * 
 * @author Bing SU (nova.su@gmail.com)
 */

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp.Transform
{
    internal class SrtpCryptoContext
    {
        /**
         * The replay check windows size
         */
        private readonly long _replayWindowSize = 64;

        /**
         * Roll-Over-Counter, see RFC3711 section 3.2.1 for detailed description
         */
        private int _roc;

        /**
         * Roll-Over-Counter guessed from packet
         */
        private int _guessedRoc;

        /**
         * RTP sequence number of the packet current processing
         */
        private int _seqNum;

        /**
         * Whether we have the sequence number of current packet
         */
        private bool _seqNumSet;

        /**
         * Key Derivation Rate, used to derive session keys from master keys
         */
        private readonly long _keyDerivationRate;

        /**
         * Bit mask for replay check
         */
        private long _replayWindow;

        /**
         * Master encryption key
         */
        private readonly byte[] _masterKey;

        /**
         * Master salting key
         */
        private readonly byte[] _masterSalt;

        /**
         * Derived session encryption key
         */
        private readonly byte[] _encKey;

        /**
         * Derived session authentication key
         */
        private readonly byte[] _authKey;

        /**
         * Derived session salting key
         */
        private readonly byte[] _saltKey;

        /**
         * Encryption / Authentication policy for this session
         */
        private readonly SrtpPolicy _policy;

        /**
         * The HMAC object we used to do packet authentication
         */
        private readonly IMac _mac;

        /**
         * The symmetric cipher engines we need here
         */
        private readonly IBlockCipher _cipher;

        /**
         * Used inside F8 mode only
         */
        private readonly IBlockCipher _cipherF8;

        /**
         * implements the counter cipher mode for RTP according to RFC 3711
         */
        private readonly SrtpCipherCtr _cipherCtr = new SrtpCipherCtr();

        /**
         * Temp store.
         */
        private readonly byte[] _tagStore;

        /**
         * Temp store.
         */
        private readonly byte[] _ivStore = new byte[16];

        /**
         * Temp store.
         */
        private readonly byte[] _rbStore = new byte[4];

        /**
         * this is a working store, used by some methods to avoid new operations the
         * methods must use this only to store results for immediate processing
         */
        private readonly byte[] _tempStore = new byte[100];

        /**
         * Construct a normal SRTPCryptoContext based on the given parameters.
         * 
         * @param ssrcIn
         *            the RTP SSRC that this SRTP cryptographic context protects.
         * @param rocIn
         *            the initial Roll-Over-Counter according to RFC 3711. These are
         *            the upper 32 bit of the overall 48 bit SRTP packet index.
         *            Refer to chapter 3.2.1 of the RFC.
         * @param kdr
         *            the key derivation rate defines when to recompute the SRTP
         *            session keys. Refer to chapter 4.3.1 in the RFC.
         * @param masterK
         *            byte array holding the master key for this SRTP cryptographic
         *            context. Refer to chapter 3.2.1 of the RFC about the role of
         *            the master key.
         * @param masterS
         *            byte array holding the master salt for this SRTP cryptographic
         *            context. It is used to computer the initialization vector that
         *            in turn is input to compute the session key, session
         *            authentication key and the session salt.
         * @param policyIn
         *            SRTP policy for this SRTP cryptographic context, defined the
         *            encryption algorithm, the authentication algorithm, etc
         */

        public SrtpCryptoContext(int rocIn, long kdr, byte[] masterK,
                byte[] masterS, SrtpPolicy policyIn)
        {
            _roc = rocIn;
            _guessedRoc = 0;
            _seqNum = 0;
            _keyDerivationRate = kdr;
            _seqNumSet = false;

            _policy = policyIn;

            _masterKey = new byte[_policy.EncKeyLength];
            Array.Copy(masterK, 0, _masterKey, 0, masterK.Length);

            _masterSalt = new byte[_policy.SaltKeyLength];
            Array.Copy(masterS, 0, _masterSalt, 0, masterS.Length);

            _mac = new HMac(new Sha1Digest());

            switch (_policy.EncType)
            {
                case SrtpPolicy.NullEncryption:
                    _encKey = null;
                    _saltKey = null;
                    break;

                case SrtpPolicy.Aesf8Encryption:
                    _cipherF8 = new AesEngine();
                    _cipher = new AesEngine();
                    _encKey = new byte[_policy.EncKeyLength];
                    _saltKey = new byte[_policy.SaltKeyLength];
                    break;
                //$FALL-THROUGH$

                case SrtpPolicy.AescmEncryption:
                    _cipher = new AesEngine();
                    _encKey = new byte[_policy.EncKeyLength];
                    _saltKey = new byte[_policy.SaltKeyLength];
                    break;

                case SrtpPolicy.Twofishf8Encryption:
                    _cipherF8 = new TwofishEngine();
                    _cipher = new TwofishEngine();
                    _encKey = new byte[_policy.EncKeyLength];
                    _saltKey = new byte[_policy.SaltKeyLength];
                    break;

                case SrtpPolicy.TwofishEncryption:
                    _cipher = new TwofishEngine();
                    _encKey = new byte[_policy.EncKeyLength];
                    _saltKey = new byte[_policy.SaltKeyLength];
                    break;
            }

            switch (_policy.AuthType)
            {
                case SrtpPolicy.NullAuthentication:
                    _authKey = null;
                    _tagStore = null;
                    break;

                case SrtpPolicy.Hmacsha1Authentication:
                    _mac = new HMac(new Sha1Digest());
                    _authKey = new byte[_policy.AuthKeyLength];
                    _tagStore = new byte[_mac.GetMacSize()];
                    break;

                default:
                    _tagStore = null;
                    break;
            }
        }

        /**
         * Close the crypto context.
         * 
         * The close functions deletes key data and performs a cleanup of the crypto
         * context.
         * 
         * Clean up key data, maybe this is the second time however, sometimes we
         * cannot know if the CryptoCOntext was used and the application called
         * deriveSrtpKeys(...) .
         * 
         */
        public void Close()
        {
            Arrays.Fill(_masterKey, 0);
            Arrays.Fill(_masterSalt, 0);
        }

        /**
         * Transform a RTP packet into a SRTP packet. This method is called when a
         * normal RTP packet ready to be sent.
         * 
         * Operations done by the transformation may include: encryption, using
         * either Counter Mode encryption, or F8 Mode encryption, adding
         * authentication tag, currently HMC SHA1 method.
         * 
         * Both encryption and authentication functionality can be turned off as
         * long as the SRTPPolicy used in this SRTPCryptoContext is requires no
         * encryption and no authentication. Then the packet will be sent out
         * untouched. However this is not encouraged. If no SRTP feature is enabled,
         * then we shall not use SRTP TransformConnector. We should use the original
         * method (RTPManager managed transportation) instead.
         * 
         * @param pkt
         *            the RTP packet that is going to be sent out
         */
        public void TransformPacket(RawPacket pkt)
        {
            /* Encrypt the packet using Counter Mode encryption */
            if (_policy.EncType == SrtpPolicy.AescmEncryption || _policy.EncType == SrtpPolicy.TwofishEncryption)
            {
                ProcessPacketAescm(pkt);
            }
            else if (_policy.EncType == SrtpPolicy.Aesf8Encryption || _policy.EncType == SrtpPolicy.Twofishf8Encryption)
            {
                /* Encrypt the packet using F8 Mode encryption */
                ProcessPacketAesf8(pkt);
            }

            /* Authenticate the packet */
            if (_policy.AuthType != SrtpPolicy.NullAuthentication)
            {
                AuthenticatePacketHmcsha1(pkt, _roc);
                pkt.Append(_tagStore, _policy.AuthTagLength);
            }

            /* Update the ROC if necessary */
            var seqNo = pkt.GetSequenceNumber();
            if (seqNo == 0xFFFF)
            {
                _roc++;
            }
        }

        /**
         * Transform a SRTP packet into a RTP packet. This method is called when a
         * SRTP packet is received.
         * 
         * Operations done by the this operation include: Authentication check,
         * Packet replay check and Decryption.
         * 
         * Both encryption and authentication functionality can be turned off as
         * long as the SRTPPolicy used in this SRTPCryptoContext requires no
         * encryption and no authentication. Then the packet will be sent out
         * untouched. However this is not encouraged. If no SRTP feature is enabled,
         * then we shall not use SRTP TransformConnector. We should use the original
         * method (RTPManager managed transportation) instead.
         * 
         * @param pkt
         *            the RTP packet that is just received
         * @return true if the packet can be accepted false if the packet failed
         *         authentication or failed replay check
         */
        public bool ReverseTransformPacket(RawPacket pkt)
        {
            var seqNo = pkt.GetSequenceNumber();

            if (!_seqNumSet)
            {
                _seqNumSet = true;
                _seqNum = seqNo;
            }

            // Guess the SRTP index (48 bit), see rFC 3711, 3.3.1
            // Stores the guessed roc in this.guessedROC
            var guessedIndex = GuessIndex(seqNo);

            // Replay control
            if (!CheckReplay(guessedIndex))
            {
                return false;
            }

            // Authenticate packet
            if (_policy.AuthType != SrtpPolicy.NullAuthentication)
            {
                var tagLength = _policy.AuthTagLength;

                // get original authentication and store in tempStore
                pkt.ReadRegionToBuff(pkt.GetLength() - tagLength, tagLength, _tempStore);
                pkt.Shrink(tagLength);

                // save computed authentication in tagStore
                AuthenticatePacketHmcsha1(pkt, _guessedRoc);

                for (var i = 0; i < tagLength; i++)
                {
                    if ((_tempStore[i] & 0xff) != (_tagStore[i] & 0xff))
                    {
                        return false;
                    }
                }
            }

            // Decrypt packet
            switch (_policy.EncType)
            {
                case SrtpPolicy.AescmEncryption:
                case SrtpPolicy.TwofishEncryption:
                    // using Counter Mode encryption
                    ProcessPacketAescm(pkt);
                    break;

                case SrtpPolicy.Aesf8Encryption:
                case SrtpPolicy.Twofishf8Encryption:
                    // using F8 Mode encryption
                    ProcessPacketAesf8(pkt);
                    break;

                default:
                    return false;
            }
            Update(seqNo, guessedIndex);
            return true;
        }

        /**
         * Perform Counter Mode AES encryption / decryption
         * 
         * @param pkt
         *            the RTP packet to be encrypted / decrypted
         */
        public void ProcessPacketAescm(RawPacket pkt)
        {
            long ssrc = pkt.GetSsrc();
            var seqNo = pkt.GetSequenceNumber();
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            var index = ((long)_roc << 16) | seqNo;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand

            _ivStore[0] = _saltKey[0];
            _ivStore[1] = _saltKey[1];
            _ivStore[2] = _saltKey[2];
            _ivStore[3] = _saltKey[3];

            int i;
            for (i = 4; i < 8; i++)
            {
                _ivStore[i] = (byte)((0xFF & (ssrc >> ((7 - i) * 8))) ^ _saltKey[i]);
            }

            for (i = 8; i < 14; i++)
            {
                _ivStore[i] = (byte)((0xFF & (byte)(index >> ((13 - i) * 8))) ^ _saltKey[i]);
            }

            _ivStore[14] = _ivStore[15] = 0;

            var payloadOffset = pkt.GetHeaderLength();
            var payloadLength = pkt.GetPayloadLength();

            _cipherCtr.Process(_cipher, pkt.GetBuffer(), payloadOffset, payloadLength, _ivStore);
        }

        /**
         * Perform F8 Mode AES encryption / decryption
         * 
         * @param pkt
         *            the RTP packet to be encrypted / decrypted
         */
        public void ProcessPacketAesf8(RawPacket pkt)
        {
            // 11 bytes of the RTP header are the 11 bytes of the iv
            // the first byte of the RTP header is not used.
            var buf = pkt.GetBuffer();
            buf.Read(_ivStore, (int)buf.Position, 12);
            _ivStore[0] = 0;

            // set the ROC in network order into IV
            _ivStore[12] = (byte)(_roc >> 24);
            _ivStore[13] = (byte)(_roc >> 16);
            _ivStore[14] = (byte)(_roc >> 8);
            _ivStore[15] = (byte)_roc;

            var payloadOffset = pkt.GetHeaderLength();
            var payloadLength = pkt.GetPayloadLength();

            SrtpCipherF8.Process(_cipher, pkt.GetBuffer(), payloadOffset, payloadLength, _ivStore, _cipherF8);
        }

        readonly byte[] _tempBuffer = new byte[RawPacket.RTPPacketMaxSize];

        /**
         * Authenticate a packet. Calculated authentication tag is returned.
         * 
         * @param pkt
         *            the RTP packet to be authenticated
         * @param rocIn
         *            Roll-Over-Counter
         */
        private void AuthenticatePacketHmcsha1(RawPacket pkt, int rocIn)
        {
            var buf = pkt.GetBuffer();
            buf.Position = 0;
            var len = (int)buf.Length;
            buf.Read(_tempBuffer, 0, len);
            _mac.BlockUpdate(_tempBuffer, 0, len);
            _rbStore[0] = (byte)(rocIn >> 24);
            _rbStore[1] = (byte)(rocIn >> 16);
            _rbStore[2] = (byte)(rocIn >> 8);
            _rbStore[3] = (byte)rocIn;
            _mac.BlockUpdate(_rbStore, 0, _rbStore.Length);
            _mac.DoFinal(_tagStore, 0);
        }

        /**
         * Checks if a packet is a replayed on based on its sequence number.
         * 
         * This method supports a 64 packet history relative the the given sequence
         * number.
         * 
         * Sequence Number is guaranteed to be real (not faked) through
         * authentication.
         * 
         * @param seqNo
         *            sequence number of the packet
         * @param guessedIndex
         *            guessed roc
         * @return true if this sequence number indicates the packet is not a
         *         replayed one, false if not
         */
        bool CheckReplay(long guessedIndex)
        {
            // compute the index of previously received packet and its
            // delta to the new received packet
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            var localIndex = (((long)_roc) << 16) | _seqNum;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
            var delta = guessedIndex - localIndex;

            if (delta > 0)
            {
                /* Packet not yet received */
                return true;
            }

            if (-delta > _replayWindowSize)
            {
                /* Packet too old */
                return false;
            }

            if (((_replayWindow >> ((int)-delta)) & 0x1) != 0)
            {
                /* Packet already received ! */
                return false;
            }

            /* Packet not yet received */
            return true;
        }

        /**
         * Compute the initialization vector, used later by encryption algorithms,
         * based on the lable, the packet index, key derivation rate and master salt
         * key.
         * 
         * @param label
         *            label specified for each type of iv
         * @param index
         *            48bit RTP packet index
         */
        private void ComputeIv(long label, long index)
        {
            long keyId;

            if (_keyDerivationRate == 0)
            {
                keyId = label << 48;
            }
            else
            {
                keyId = ((label << 48) | (index / _keyDerivationRate));
            }
            for (var i = 0; i < 7; i++)
            {
                _ivStore[i] = _masterSalt[i];
            }
            for (var i = 7; i < 14; i++)
            {
                _ivStore[i] = (byte)((byte)(0xFF & (keyId >> (8 * (13 - i)))) ^ _masterSalt[i]);
            }
            _ivStore[14] = _ivStore[15] = 0;
        }

        /**
         * Derives the srtp session keys from the master key
         * 
         * @param index
         *            the 48 bit SRTP packet index
         */
        public void DeriveSrtpKeys(long index)
        {
            // compute the session encryption key
            long label = 0;
            ComputeIv(label, index);

            var encryptionKey = new KeyParameter(_masterKey);
            _cipher.Init(true, encryptionKey);
            Arrays.Fill(_masterKey, 0);

            _cipherCtr.GetCipherStream(_cipher, _encKey, _policy.EncKeyLength, _ivStore);

            // compute the session authentication key
            if (_authKey != null)
            {
                label = 0x01;
                ComputeIv(label, index);
                _cipherCtr.GetCipherStream(_cipher, _authKey, _policy.AuthKeyLength, _ivStore);

                switch ((_policy.AuthType))
                {
                    case SrtpPolicy.Hmacsha1Authentication:
                        var key = new KeyParameter(_authKey);
                        _mac.Init(key);
                        break;
                }
            }
            Arrays.Fill(_authKey, 0);

            // compute the session salt
            label = 0x02;
            ComputeIv(label, index);
            _cipherCtr.GetCipherStream(_cipher, _saltKey, _policy.SaltKeyLength, _ivStore);
            Arrays.Fill(_masterSalt, 0);

            // As last step: initialize cipher with derived encryption key.
            if (_cipherF8 != null)
            {
                SrtpCipherF8.DeriveForIv(_cipherF8, _encKey, _saltKey);
            }
            encryptionKey = new KeyParameter(_encKey);
            _cipher.Init(true, encryptionKey);
            Arrays.Fill(_encKey, 0);
        }

        /**
         * Compute (guess) the new SRTP index based on the sequence number of a
         * received RTP packet.
         * 
         * @param seqNo
         *            sequence number of the received RTP packet
         * @return the new SRTP packet index
         */
        private long GuessIndex(int seqNo)
        {
            if (_seqNum < 32768)
            {
                if (seqNo - _seqNum > 32768)
                {
                    _guessedRoc = _roc - 1;
                }
                else
                {
                    _guessedRoc = _roc;
                }
            }
            else
            {
                if (_seqNum - 32768 > seqNo)
                {
                    _guessedRoc = _roc + 1;
                }
                else
                {
                    _guessedRoc = _roc;
                }
            }

#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            return ((long)_guessedRoc) << 16 | seqNo;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
        }

        /**
         * Update the SRTP packet index.
         * 
         * This method is called after all checks were successful. See section 3.3.1
         * in RFC3711 for detailed description.
         * 
         * @param seqNo
         *            sequence number of the accepted packet
         * @param guessedIndex
         *            guessed roc
         */
        private void Update(int seqNo, long guessedIndex)
        {
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            var delta = guessedIndex - (((long)_roc) << 16 | _seqNum);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand

            /* update the replay bit mask */
            if (delta > 0)
            {
                _replayWindow = _replayWindow << (int)delta;
                _replayWindow |= 1;
            }
            else
            {
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
                _replayWindow |= (1 << (int)delta);
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
            }

            if (seqNo > _seqNum)
            {
                _seqNum = seqNo & 0xffff;
            }
            if (_guessedRoc > _roc)
            {
                _roc = _guessedRoc;
                _seqNum = seqNo & 0xffff;
            }
        }

        /**
         * Derive a new SRTPCryptoContext for use with a new SSRC
         * 
         * This method returns a new SRTPCryptoContext initialized with the data of
         * this SRTPCryptoContext. Replacing the SSRC, Roll-over-Counter, and the
         * key derivation rate the application cab use this SRTPCryptoContext to
         * encrypt / decrypt a new stream (Synchronization source) inside one RTP
         * session.
         * 
         * Before the application can use this SRTPCryptoContext it must call the
         * deriveSrtpKeys method.
         * 
         * @param ssrc
         *            The SSRC for this context
         * @param roc
         *            The Roll-Over-Counter for this context
         * @param deriveRate
         *            The key derivation rate for this context
         * @return a new SRTPCryptoContext with all relevant data set.
         */
        public SrtpCryptoContext DeriveContext(int roc, long deriveRate)
        {
            return new SrtpCryptoContext(roc, deriveRate, _masterKey, _masterSalt, _policy);
        }
    }
}

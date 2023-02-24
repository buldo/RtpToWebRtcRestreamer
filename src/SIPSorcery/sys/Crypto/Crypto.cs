//-----------------------------------------------------------------------------
// Filename: Crypto.cs
//
// Description: Encrypts and decrypts data.
//
// Author(s):
// Aaron Clauson
//
// History:
// 16 Jul 2005	Aaron Clauson	Created.
// 10 Sep 2009  Aaron Clauson   Updated to use RNGCryptoServiceProvider in place of Random.
//
// License:
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SIPSorcery.Sys
{
    public class Crypto
    {
        private const int DEFAULT_RANDOM_LENGTH = 10;    // Number of digits to return for default random numbers.

        private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private static ILogger logger = Log.Logger;

        static int seed = Environment.TickCount;

        static readonly ThreadLocal<Random> random =
            new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static int Rand(int maxValue)
        {
            return random.Value.Next(maxValue);
        }

        private static RNGCryptoServiceProvider m_randomProvider = new RNGCryptoServiceProvider();

        public static string GetRandomString(int length)
        {
            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = CHARS[Rand(CHARS.Length)];
            }
            return new string(buffer);
        }

        /// <summary>
        /// Returns a 10 digit random number.
        /// </summary>
        /// <returns></returns>
        public static int GetRandomInt()
        {
            return GetRandomInt(DEFAULT_RANDOM_LENGTH);
        }

        /// <summary>
        /// Returns a random number of a specified length.
        /// </summary>
        public static int GetRandomInt(int length)
        {
            int randomStart = 1000000000;
            int randomEnd = Int32.MaxValue;

            if (length > 0 && length < DEFAULT_RANDOM_LENGTH)
            {
                randomStart = Convert.ToInt32(Math.Pow(10, length - 1));
                randomEnd = Convert.ToInt32(Math.Pow(10, length) - 1);
            }

            return GetRandomInt(randomStart, randomEnd);
        }

        public static Int32 GetRandomInt(Int32 minValue, Int32 maxValue)
        {

            if (minValue > maxValue)
            {
                throw new ArgumentOutOfRangeException("minValue");
            }
            else if (minValue == maxValue)
            {
                return minValue;
            }

            Int64 diff = maxValue - minValue + 1;
            int attempts = 0;
            while (attempts < 10)
            {
                byte[] uint32Buffer = new byte[4];
                m_randomProvider.GetBytes(uint32Buffer);
                UInt32 rand = BitConverter.ToUInt32(uint32Buffer, 0);

                Int64 max = (1 + (Int64)UInt32.MaxValue);
                Int64 remainder = max % diff;
                if (rand <= max - remainder)
                {
                    return (Int32)(minValue + (rand % diff));
                }
                attempts++;
            }
            throw new ApplicationException("GetRandomInt did not return an appropriate random number within 10 attempts.");
        }

        public static UInt16 GetRandomUInt16()
        {
            byte[] uint16Buffer = new byte[2];
            m_randomProvider.GetBytes(uint16Buffer);
            return BitConverter.ToUInt16(uint16Buffer, 0);
        }

        public static UInt32 GetRandomUInt(bool noZero = false)
        {
            byte[] uint32Buffer = new byte[4];
            m_randomProvider.GetBytes(uint32Buffer);
            var randomUint = BitConverter.ToUInt32(uint32Buffer, 0);

            if(noZero && randomUint == 0)
            {
                m_randomProvider.GetBytes(uint32Buffer);
                randomUint = BitConverter.ToUInt32(uint32Buffer, 0);
            }

            return randomUint;
        }

        public static UInt64 GetRandomULong()
        {
            byte[] uint64Buffer = new byte[8];
            m_randomProvider.GetBytes(uint64Buffer);
            return BitConverter.ToUInt64(uint64Buffer, 0);
        }

        /// <summary>
        /// Fills a buffer with random bytes.
        /// </summary>
        /// <param name="buffer">The buffer to fill.</param>
        public static void GetRandomBytes(byte[] buffer)
        {
            m_randomProvider.GetBytes(buffer);
        }

        private static byte[] GetSHAHash(params string[] values)
        {
            SHA1 sha = new SHA1Managed();
            string plainText = null;
            foreach (string value in values)
            {
                plainText += value;
            }
            return sha.ComputeHash(Encoding.UTF8.GetBytes(plainText));
        }

        public static string GetSHAHashAsString(params string[] values)
        {
            return Convert.ToBase64String(GetSHAHash(values));
        }

        /// <summary>
        /// Returns the hash with each byte as an X2 string. This is useful for situations where
        /// the hash needs to only contain safe ASCII characters.
        /// </summary>
        /// <param name="values">The list of string to concatenate and hash.</param>
        /// <returns>A string with "safe" (0-9 and A-F) characters representing the hash.</returns>
        public static string GetSHAHashAsHex(params string[] values)
        {
            byte[] hash = GetSHAHash(values);
            string hashStr = null;
            hash.ToList().ForEach(b => hashStr += b.ToString("x2"));
            return hashStr;
        }

        /// <summary>
        /// Attempts to load an X509 certificate from a Windows OS certificate store.
        /// </summary>
        /// <param name="storeLocation">The certificate store to load from, can be CurrentUser or LocalMachine.</param>
        /// <param name="certificateSubject">The subject name of the certificate to attempt to load.</param>
        /// <param name="checkValidity">Checks if the certificate is current and has a verifiable certificate issuer list. Should be
        /// set to false for self issued certificates.</param>
        /// <returns>A certificate object if the load is successful otherwise null.</returns>
        public static X509Certificate2 LoadCertificate(StoreLocation storeLocation, string certificateSubject, bool checkValidity)
        {
            X509Store store = new X509Store(storeLocation);
            logger.LogDebug("Certificate store " + store.Location + " opened");
            store.Open(OpenFlags.OpenExistingOnly);
            X509Certificate2Collection collection = store.Certificates.Find(X509FindType.FindBySubjectName, certificateSubject, checkValidity);
            if (collection != null && collection.Count > 0)
            {
                X509Certificate2 serverCertificate = collection[0];
                bool verifyCert = serverCertificate.Verify();
                logger.LogDebug("X509 certificate loaded from current user store, subject=" + serverCertificate.Subject + ", valid=" + verifyCert + ".");
                return serverCertificate;
            }
            else
            {
                logger.LogWarning("X509 certificate with subject name=" + certificateSubject + ", not found in " + store.Location + " store.");
                return null;
            }
        }
    }
}

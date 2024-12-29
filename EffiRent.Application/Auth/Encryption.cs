using System.Text;
using System.Security.Cryptography;
using EffiAP.Domain.SeedWork;
using Microsoft.Extensions.Configuration;

namespace EffiAP.Application.Auth
{
    public interface IEncryption : IScopedService
    {
        public string Encrypt(string password);
        public string Decrypt(string password);
    }

    public class Encryption : IEncryption
    {
        public IConfiguration _configuration;
        public Encryption(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        /// <summary> 
        /// Encrypt the data 
        /// </summary> 
        /// <param name="input">String to encrypt</param> 
        /// <returns>Encrypted string</returns> 
        public string Encrypt(string password)
        {

            byte[] utfData = Encoding.UTF8.GetBytes(password);
            byte[] saltBytes = Encoding.UTF8.GetBytes(_configuration["EncryptKey"]);
            string encryptedString = string.Empty;
            using (AesManaged aes = new AesManaged())
            {
                Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes(_configuration["EncryptKey"], saltBytes);

                aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
                aes.KeySize = aes.LegalKeySizes[0].MaxSize;
                aes.Key = rfc.GetBytes(aes.KeySize / 8);
                aes.IV = rfc.GetBytes(aes.BlockSize / 8);

                using (ICryptoTransform encryptTransform = aes.CreateEncryptor())
                {
                    using (MemoryStream encryptedStream = new MemoryStream())
                    {
                        using (CryptoStream encryptor =
                            new CryptoStream(encryptedStream, encryptTransform, CryptoStreamMode.Write))
                        {
                            encryptor.Write(utfData, 0, utfData.Length);
                            encryptor.Flush();
                            encryptor.Close();

                            byte[] encryptBytes = encryptedStream.ToArray();
                            encryptedString = Convert.ToBase64String(encryptBytes);
                        }
                    }
                }
            }
            return encryptedString;
        }

        /// <summary> 
        /// Decrypt a string 
        /// </summary> 
        /// <param name="input">Input string in base 64 format</param> 
        /// <returns>Decrypted string</returns> 
        public string Decrypt( string password)
        {

            byte[] encryptedBytes = Convert.FromBase64String(password);
            byte[] saltBytes = Encoding.UTF8.GetBytes(_configuration["EncryptKey"]);
            string decryptedString = string.Empty;
            using (var aes = new AesManaged())
            {
                Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes(_configuration["EncryptKey"], saltBytes);
                aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
                aes.KeySize = aes.LegalKeySizes[0].MaxSize;
                aes.Key = rfc.GetBytes(aes.KeySize / 8);
                aes.IV = rfc.GetBytes(aes.BlockSize / 8);

                using (ICryptoTransform decryptTransform = aes.CreateDecryptor())
                {
                    using (MemoryStream decryptedStream = new MemoryStream())
                    {
                        CryptoStream decryptor =
                            new CryptoStream(decryptedStream, decryptTransform, CryptoStreamMode.Write);
                        decryptor.Write(encryptedBytes, 0, encryptedBytes.Length);
                        decryptor.Flush();
                        decryptor.Close();

                        byte[] decryptBytes = decryptedStream.ToArray();
                        decryptedString =
                            Encoding.UTF8.GetString(decryptBytes, 0, decryptBytes.Length);
                    }
                }
            }

            return decryptedString;
        }



    }
}

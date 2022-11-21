using System.Text;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace System.Security.Cryptography.GenericEncryption
{
	public static class Encryption
	{
		/// <summary>
		/// RSA encrypt data bytes stored in parameter 'data' using X509Certificate public key.
		/// </summary>
		/// <param name="data">Data to be encrypted</param>
		/// <param name="certificate">Certificate used for encryption</param>
		/// <param name="Padding">Padding configuration. Default: null</param>
		/// <returns>Encrypted data in byte array</returns>
		public static byte[] RSAEncrypt(byte[] data, X509Certificate2 certificate, RSAEncryptionPadding Padding = null)
		{
			if (Padding == null)
			{
				Padding = RSAEncryptionPadding.OaepSHA256;
			}
			return certificate.PublicKey.GetRSAPublicKey()?.Encrypt(data, Padding);
		}

		/// <summary>
		/// RSA decrypt data bytes stored in parameter 'encrypted' using X509Certificate private key.
		/// </summary>
		/// <param name="encrypted">encrypted information</param>
		/// <param name="certificate">certificate used for decryption</param>
		/// <param name="Padding">Padding configuration. Default: null</param>
		/// <returns>Decrypted data in byte array</returns>
		public static byte[] RSADecrypt(byte[] encrypted, X509Certificate2 certificate, RSAEncryptionPadding Padding = null)
		{
			if (Padding == null)
			{
				Padding = RSAEncryptionPadding.OaepSHA256;
			}
			return certificate.GetRSAPrivateKey()?.Decrypt(encrypted, Padding);
		}

		/// <summary>
		/// Encrypt the content of parameter data using RSA encryption
		/// </summary>
		/// <param name="data">Content to be encrypted</param>
		/// <param name="OAEPPadding">Padding configuration. Default: null</param>
		/// <returns>Tuple with three types: Encrpyted: contains the encrypted bytes, PrivateKey: private key information as byte array, PublicKey: public key information as byte array</returns>
		public static (byte[] Encrypted, byte[] PrivateKey, byte[] PublicKey) RSAEncrypt(byte[] data, bool OAEPPadding = false)
		{
			byte[] PrivateKey, PublicKey;
			byte[] encrypted;
			using (RSACryptoServiceProvider r = new RSACryptoServiceProvider(2048))
			{
				PrivateKey = r.ExportRSAPrivateKey();
				PublicKey = r.ExportRSAPublicKey();
				encrypted = r.Encrypt(data, OAEPPadding);
			}
			return (encrypted, PrivateKey, PublicKey);
		}

		/// <summary>
		/// Encrypt the content of parameter data using RSA encryption using the PublicKey
		/// </summary>
		/// <param name="data">Data to be encrypted</param>
		/// <param name="RSAPublicKey">RSA public key</param>
		/// <param name="OAEPPadding">Padding configuration. Default: null</param>
		/// <returns>Enrypted data as byte array</returns>
		public static byte[] RSAEncrypt(byte[] data, byte[] RSAPublicKey, bool OAEPPadding = false)
		{
			byte[] encrypted;
			using (RSACryptoServiceProvider r = new RSACryptoServiceProvider(2048))
			{
				int read;
				r.ImportRSAPublicKey(RSAPublicKey, out read);
				encrypted = r.Encrypt(data, OAEPPadding);
			}
			return encrypted;
		}

		/// <summary>
		/// Decrypt the RSA encrypted data using the RSA Private key
		/// </summary>
		/// <param name="encrypted">Encryted data</param>
		/// <param name="RSAPrivateKey">Private key</param>
		/// <param name="OAEPPadding">Padding configuration. Default: null</param>
		/// <returns></returns>
		public static byte[] RSADecrypt(byte[] encrypted, byte[] RSAPrivateKey, bool OAEPPadding = false)
		{
			byte[] decrypted;
			using (RSACryptoServiceProvider r = new RSACryptoServiceProvider(2048))
			{
				int read;
				r.ImportRSAPrivateKey(RSAPrivateKey, out read);
				decrypted = r.Decrypt(encrypted, OAEPPadding);
			}
			return decrypted;
		}

		/// <summary>
		/// Encrypt data using 256 bit AES CBC encryption
		/// </summary>
		/// <param name="inputData">Data to be encrypted</param>
		/// <param name="password">Encryption password</param>
		/// <returns>Encrypted data</returns>
		public static byte[] EncryptAesCBC(byte[] inputData, string password)
		{
			using (MemoryStream strm = new MemoryStream())
			{
				using (Aes AES = Aes.Create())
				{
					AES.Mode = CipherMode.CBC;
					AES.Padding = PaddingMode.PKCS7;
					AES.KeySize = 256;

					byte[] IV = AES.IV;
					byte[] pw = _FormatPassword(password, AES.KeySize);

					using (CryptoStream cStream = new CryptoStream(strm, AES.CreateEncryptor(pw, IV), CryptoStreamMode.Write))
					{
						cStream.Write(inputData, 0, inputData.Length);
					}
					byte[] encrypted = strm.ToArray();
					byte[] result = new byte[IV.Length + encrypted.Length];
					Buffer.BlockCopy(IV, 0, result, 0, IV.Length);
					Buffer.BlockCopy(encrypted, 0, result, IV.Length, encrypted.Length);
					return result;
				}
			}
		}

		/// <summary>
		/// Encrypt T object using 256 bit AES CBC encryption
		/// </summary>
		/// <typeparam name="T">object type</typeparam>
		/// <param name="inputData">T type object to be encrypted</param>
		/// <param name="password">Encryption password</param>
		/// <returns>Encrypted data</returns>
		public static byte[] EncryptAesCBC<T>(T inputData, string password)
		{
			string inputString = JsonConvert.SerializeObject(inputData);
			return EncryptAesCBC(Encoding.UTF8.GetBytes(inputString), password);
		}

		/// <summary>
		/// Decrypt encrypted data using 256 bit AES CBC encryption
		/// </summary>
		/// <param name="secret">Encrypted data</param>
		/// <param name="password">Encryption password</param>
		/// <returns>Decrypted information</returns>
		public static byte[] DecryptAesCBC(byte[] secret, string password)
		{
			byte[] iv = new byte[16];
			byte[] encryptedContent = new byte[secret.Length - iv.Length];

			Buffer.BlockCopy(secret, 0, iv, 0, iv.Length);
			Buffer.BlockCopy(secret, iv.Length, encryptedContent, 0, encryptedContent.Length);

			using (MemoryStream strm = new MemoryStream())
			{
				using (Aes AES = Aes.Create())
				{
					AES.Mode = CipherMode.CBC;
					AES.Padding = PaddingMode.PKCS7;
					AES.KeySize = 256;

					byte[] pw = _FormatPassword(password, AES.KeySize);

					using (CryptoStream cs = new CryptoStream(strm, AES.CreateDecryptor(pw, iv), CryptoStreamMode.Write))
					{
						cs.Write(encryptedContent, 0, encryptedContent.Length);

					}
					return strm.ToArray();
				}
			}
		}


		/// <summary>
		/// Decrypt encrypted data using 256 bit AES CBC encryption and convert to T type object
		/// </summary>
		/// <typeparam name="T">type of encrpyted data</typeparam>
		/// <param name="secret">Encrypted data</param>
		/// <param name="password">Encryption password</param>
		/// <returns>Decrypted T type object</returns>
		public static T DecryptAesCBC<T>(byte[] inputData, string password)
		{
			byte[] decryptedData = DecryptAesCBC(inputData, password);
			string jsonData = Encoding.UTF8.GetString(decryptedData);
			return JsonConvert.DeserializeObject<T>(jsonData);
		}

		/// <summary>
		/// Encrypt data using Windows Data Protection API. Works only on Windows!
		/// </summary>
		/// <param name="input">Data to be encrypted</param>
		/// <param name="scope">Scope using for encryption. Default: LocalMachine</param>
		/// <returns>Encrypted data</returns>
		public static byte[] Encrypt(byte[] input, DataProtectionScope scope = DataProtectionScope.LocalMachine)
		{
			return ProtectedData.Protect(input, null, scope);
		}

		/// <summary>
		/// Decrypt data using Windows Data Protection API. Works only on Windows!
		/// </summary>
		/// <param name="encrypted">Encrypted data</param>
		/// <param name="scope">Scope using for encryption. Default: LocalMachine</param>
		/// <returns>Decrypted data</returns>
		public static byte[] Decrypt(byte[] encrypted, DataProtectionScope scope = DataProtectionScope.LocalMachine)
		{
			return ProtectedData.Unprotect(encrypted, null, scope);
		}

		private static byte[] _FormatPassword(string inputPW, int keyLength)
		{
			byte[] pwArr = Encoding.UTF8.GetBytes(inputPW);
			int length = keyLength / 8;
			string newPw = "";

			int i = 0;
			while (newPw.Length < length)
			{
				newPw += inputPW[i++];
				if (i == inputPW.Length)
				{
					i = 0;
				}
			}

			return Encoding.UTF8.GetBytes(newPw);
		}

		private static void PadToMultipleOf(ref byte[] src, int pad)
		{
			int len = (src.Length + pad - 1) / pad * pad;
			Array.Resize(ref src, len);
		}

	}
}

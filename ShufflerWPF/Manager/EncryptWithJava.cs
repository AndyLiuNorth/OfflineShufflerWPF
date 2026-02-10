using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShufflerWPF.Manager;

namespace TheNewIdea.Manager;

public static class EncryptWithJava
{
    public static readonly string EncryKeyShuffler = "3FA2C9D5A1BE4F6EB8C7E3D0F2197A6F";
    public static readonly string EncryKeyApiProxy = "A94F1C7D3E23B8C5F17A92D4E03YC68F";
    /// <summary>
    /// Run Java CLI to encrypt the plainText with the given password.
    /// 目前暫時用不到，先保留此功能
    /// </summary>
    /// <param name="JavaCLI"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static string EncryptWithJavaCli(string password, string plainText)
    {
        // Java CLI 程式路徑
        string javaAppPath = "./target/JasyptEncryptorCli-0.0.1-SNAPSHOT.jar";
        string javaExePath = "java"; // 假設 java.exe 已經在系統的 PATH 中

        //JAR 檔名和路徑
        string jarFile = Path.Combine(Environment.CurrentDirectory, javaAppPath);
        if (!File.Exists(jarFile))
        {
            throw new FileNotFoundException($"找不到 Java CLI 程式: {jarFile}");
        }

        // 建立 ProcessStartInfo 物件
        var processInfo = new ProcessStartInfo
        {
            FileName = javaExePath,
            Arguments = $"-jar \"{jarFile}\" \"{password}\" \"{plainText}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            // 啟動 Java CLI 程式
            using (var process = Process.Start(processInfo))
            {
                // 等待程式執行完畢並讀取輸出
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
    
                if (process.ExitCode == 0)
                {
                    // 只取最後一行 加密後的結果
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string encryptedText = lines.Length > 0 ? lines[lines.Length - 1] : "";
                    return encryptedText.Trim();
                }
                else
                {
                    throw new InvalidOperationException($"Java CLI 加密失敗: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"無法啟動 Java CLI 程式: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// DES3 ECB模式加密
    /// </summary>
    /// <param name="key">密鑰</param>
    /// <param name="iv">IV(當模式爲ECB時，IV無用)</param>
    /// <param name="str">明文的byte數組</param>
    /// <returns>密文的byte數組</returns>
    public static string? Encrypt3DesECB(string a_strString, string? TargetencryKey = null)
    {
        //Default key is Shuffler key
        string a_strKey = TargetencryKey ?? EncryKeyShuffler;
        //string a_strKey = "3FA2C9D5A1BE4F6EB8C7E3D0F2197A6F";
        
        try 
        {
            using var DES = TripleDES.Create(); // 取代 TripleDESCryptoServiceProvider 避免 SYSLIB0021
            if (DES == null) throw new CryptographicException("無法建立 TripleDES 演算法實例");
            DES.Key = build3DesKey(a_strKey);
            DES.Mode = CipherMode.ECB;
            DES.Padding = PaddingMode.PKCS7;
            using ICryptoTransform DESEncrypt = DES.CreateEncryptor();
            byte[] Buffer = Encoding.UTF8.GetBytes(a_strString);
            byte[] ret = DESEncrypt.TransformFinalBlock(Buffer, 0, Buffer.Length);
            return BytesToHexString(ret);
        } catch(CryptographicException e) {
            Log4netManager.Logger.Error(e.Message);
            return null;
        }
    }
    
    /// <summary>
    /// DES3 ECB模式加密 ApiProxy
    /// </summary>
    /// <param name="key">密鑰</param>
    /// <param name="iv">IV(當模式爲ECB時，IV無用)</param>
    /// <param name="str">明文的byte數組</param>
    /// <returns>密文的byte數組</returns>
    public static string? ApiProxyEncrypt3DesECB(string a_strString)
    {

        string a_strKey = "A94F1C7D3E23B8C5F17A92D4E03YC68F";
        
        try 
        {
            using var DES = TripleDES.Create(); // 取代 TripleDESCryptoServiceProvider 避免 SYSLIB0021
            if (DES == null) throw new CryptographicException("無法建立 TripleDES 演算法實例");
            DES.Key = build3DesKey(a_strKey);
            DES.Mode = CipherMode.ECB;
            DES.Padding = PaddingMode.PKCS7;
            using ICryptoTransform DESEncrypt = DES.CreateEncryptor();
            byte[] Buffer = Encoding.UTF8.GetBytes(a_strString);
            byte[] ret = DESEncrypt.TransformFinalBlock(Buffer, 0, Buffer.Length);
            return BytesToHexString(ret);
        } catch(CryptographicException e) {
            Log4netManager.Logger.Error(e.Message);
            return null;
        }
    }

    /// <summary>
    /// 使用3DES解密
    /// </summary>
    /// <param name="strEncryptData"></param>
    /// <param name="strKey">長度為24字元</param>
    /// <returns></returns>
    public static string? Decrypt3DesECB(string strEncryptData, string strKey) {
        try {
            
            using var DES = TripleDES.Create(); // 取代 TripleDESCryptoServiceProvider 避免 SYSLIB0021
            if (DES == null) throw new CryptographicException("無法建立 TripleDES 演算法實例");
            DES.Key = build3DesKey(strKey);
            DES.Mode = CipherMode.ECB;
            DES.Padding = PaddingMode.PKCS7;
            using ICryptoTransform DESDecrypt = DES.CreateDecryptor();
            byte[] Buffer = HexStringToBytes(strEncryptData);
            string result = UTF8Encoding.UTF8.GetString(DESDecrypt.TransformFinalBlock(Buffer, 0, Buffer.Length));
            return result;
        } catch(Exception e) {
            Log4netManager.Logger.Error(e.Message);
            return null;
        }
    }

    private static byte[] build3DesKey(string keyStr) {
        byte[] key = new byte[24];
        byte[] temp = System.Text.Encoding.UTF8.GetBytes(keyStr);
        if(key.Length > temp.Length) {
            for(int i = 0; i < temp.Length; i++) {
                key[i] = temp[i];
            }
        } else {
            for(int i = 0; i < key.Length; i++) {
                key[i] = temp[i];
            }
        }
        return key;
    }

    /// <summary>
    /// 16进制字符串转byte数组
    /// </summary>
    /// <param name="hexString">16进制字符</param>
    /// <returns></returns>
    public static byte[] HexStringToBytes(string hexString) {
        // 将16进制秘钥转成字节数组
        byte[] bytes = new byte[hexString.Length / 2];
        for(var x = 0; x < bytes.Length; x++) {
            var i = Convert.ToInt32(hexString.Substring(x * 2, 2), 16);
            bytes[x] = (byte)i;
        }
        return bytes;
    }

    /// <summary>
    /// byte数组转16进制字符串
    /// </summary>
    /// <param name="bytes">byte数组</param>
    /// <returns></returns>
    public static string BytesToHexString(byte[] bytes) {
        StringBuilder sb = new StringBuilder(bytes.Length * 3);
        foreach(byte b in bytes) {
            sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
        }
        return sb.ToString().ToUpper();
    }

}
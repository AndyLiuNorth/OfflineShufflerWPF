using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShufflerWPF.Model;
using ShufflerWPF.SingleTon;

namespace ShufflerWPF.Manager;

public sealed class FileProcessManager
{
    
    private const string SettingsFileName = "shufflersettings.ini";

    private static readonly Lazy<FileProcessManager> _instance = 
        new Lazy<FileProcessManager>(() => new FileProcessManager());
    
    public static FileProcessManager Instance => _instance.Value;
    
    private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("NorthSuperSecretKey12345"); // 必須是 24 bytes
    //private static readonly byte[] EncryptionIV = Encoding.UTF8.GetBytes("1111IV12"); // 必須是 8 bytes
    
    
    /// <summary>
    /// SoftWareSettingsModel. It has default values.
    /// </summary>
    public SoftWareSettingsModel Settings { get; private set; }
    
    public string loadErrMsg { get; private set; } = string.Empty;

    /// <summary>
    /// Privater Constructor to prevent external instantiation.
    /// </summary>
    private FileProcessManager()
    {
        // 初始化為預設值
        Settings = new SoftWareSettingsModel();

        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);

            if (File.Exists(filePath))
            {
                LoadSettingsFromFile(filePath);
                loadErrMsg = string.Empty;
                Log4netManager.Logger.Info($"Successfully loaded settings from {SettingsFileName}.");
            }
            else
            {
                Log4netManager.Logger.Warn($"{SettingsFileName} not found. Using default settings.");
            }
            
        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Failed to load settings from {SettingsFileName}.", ex);
            loadErrMsg = $"Failed to load settings from {SettingsFileName}. {ex.Message}";
            //CustomMessageBox.ShowDialogBeforeMain($"Failed to load settings : {ex.Message}","Load ini Fail", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);

            // 即使載入失敗，程式也會繼續使用預設值運行
        }
    }
    
    /// <summary>
    /// 將指定的設定物件非同步地儲存回 settings.ini 檔案。
    /// </summary>
    /// <param name="settingsToSave">要儲存的設定物件。</param>
    public async Task SaveSettingsAsync(SoftWareSettingsModel settingsToSave)
    {
        try
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            
            // to json string
            string jsonTosave = JsonSerializer.Serialize(settingsToSave);
            // encrypt
            string encryptedJson = Encrypt(jsonTosave);

            var sb = new StringBuilder();
            sb.AppendLine("[ShufflerSettingEncrypt]");
            sb.AppendLine($"Encrypt={encryptedJson}");
            sb.AppendLine();//Add Empty line to separate sections
            sb.AppendLine("[ShufflerSetting]");
            sb.AppendLine($"Domain={settingsToSave.Domain}");
            sb.AppendLine($"Studio={settingsToSave.Studio}");
            sb.AppendLine($"LiveModel={settingsToSave.LiveMode}");
            sb.AppendLine($"AuthAPI={settingsToSave.AuthApi}");
            sb.AppendLine($"TableListApi={settingsToSave.TableListApi}");
            sb.AppendLine($"EngineerLogInApi={settingsToSave.EngineerLogInApi}");
            sb.AppendLine($"ActionTrueIdApi={settingsToSave.ActionTrueIdApi}");
            sb.AppendLine($"GetTrueIdApi={settingsToSave.GetTrueIdApi}");
            sb.AppendLine($"ShufflerMachineServerIp={settingsToSave.ShufflerMachineServerIp}");
            sb.AppendLine($"ShufflerServerUrl={settingsToSave.ShufflerServerUrl}");
            sb.AppendLine($"ApiProxyUrl={settingsToSave.ApiProxyUrl}");
            
            // sb.AppendLine($"ShufflerServerUrl2={settingsToSave.ShufflerServerUrl2}");
            // sb.AppendLine($"ShufflerServerUrl3={settingsToSave.ShufflerServerUrl3}");

            await File.WriteAllTextAsync(filePath, sb.ToString());

            

            this.Settings = settingsToSave;
            DataCenter.MyShufflerSettings = this.Settings;
            
            // read from Environment Variable
            string? bioptoken = Environment.GetEnvironmentVariable("BIOP_PROD_TOKEN", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(bioptoken)) 
            {
                throw new Exception("Security Token missing! Please check environment bioptoken variables.");
            }
            else
            {
                this.Settings.BiOpToken = bioptoken;
            }
            string? apiproxytoken = Environment.GetEnvironmentVariable("API_PROXY_TOKEN", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(apiproxytoken)) 
            {
                throw new Exception("Security Token missing! Please check environment apiproxytoken variables.");
            }
            else
            {
                this.Settings.ApiProxyToken = apiproxytoken;
            }
            
          
            
            Log4netManager.Logger.Info($"Successfully saved {SettingsFileName}.");

        }
        catch (Exception ex)
        {
            Log4netManager.Logger.Error($"Failed to save settings to {SettingsFileName}.", ex);
            throw;
        }
    }

    public void LoadSettingsFromFile(string filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            string? currentSection = null;
            string encryptedData = string.Empty;

            // 1.Get encrypted string from [ShufflerSettingEncrypt] section
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    continue;
                }

                if (currentSection == "ShufflerSettingEncrypt")
                {
                    int separatorIndex = trimmedLine.IndexOf('=');
                    if (separatorIndex > 0)
                    {
                        string key = trimmedLine.Substring(0, separatorIndex).Trim();
                        if (key == "Encrypt")
                        {
                            encryptedData = trimmedLine.Substring(separatorIndex + 1).Trim();
                            break; // 找到需要的資料就跳出迴圈
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(encryptedData))
            {
                throw new InvalidDataException("Could not find 'Encrypt' key in [ShufflerSettingEncrypt] section.");
            }

            // 2. Decrypt the data
            string decryptedJson = Decrypt(encryptedData);

            // 3. Decrypt data to this SoftWareSettingsModel
            var loadedSettings = JsonSerializer.Deserialize<SoftWareSettingsModel>(decryptedJson);

            if (loadedSettings != null)
            {
                this.Settings = loadedSettings;
                Log4netManager.Logger.Info($"Successfully decrypted and loaded settings from {SettingsFileName}.");
            }
            else
            {
                throw new JsonException("Deserialization resulted in a null object.");
            }
            
            // read from Environment Variable
            string? bioptoken = Environment.GetEnvironmentVariable("BIOP_PROD_TOKEN", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(bioptoken)) 
            {
                throw new Exception("Security Token missing! Please check environment bioptoken variables.");
            }
            else
            {
                this.Settings.BiOpToken = bioptoken;
            }
            string? apiproxytoken = Environment.GetEnvironmentVariable("API_PROXY_TOKEN", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrEmpty(apiproxytoken)) 
            {
                throw new Exception("Security Token missing! Please check environment apiproxytoken variables.");
            }
            else
            {
                this.Settings.ApiProxyToken = apiproxytoken;
            }
           

            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }

    private string BuildSettingsString(SoftWareSettingsModel settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Domain={settings.Domain}");
        sb.AppendLine($"LiveModel={settings.LiveMode}");
        sb.AppendLine($"AuthAPI={settings.AuthApi}");
        sb.AppendLine($"TableListApi={settings.TableListApi}");
        sb.AppendLine($"ActionTrueIdApi={settings.ActionTrueIdApi}");
        sb.AppendLine($"GetTrueIdApi={settings.GetTrueIdApi}");
        sb.AppendLine($"ShufflerMachineServerIp={settings.ShufflerMachineServerIp}");
        sb.AppendLine($"ShufflerServerUrl={settings.ShufflerServerUrl}");
        sb.AppendLine($"ShufflerServerUrl2={settings.ShufflerServerUrl2}");
        sb.AppendLine($"ShufflerServerUrl3={settings.ShufflerServerUrl3}");
        sb.AppendLine($"ApiProxyUrl={settings.ApiProxyUrl}");
        
        return sb.ToString();
    }

    private string Encrypt(string plainText)
    {
        using var des = TripleDES.Create();//IV Create here
        des.Key = EncryptionKey;
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.PKCS7;//fill data to block size 8 bytes
        
        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, des.CreateEncryptor(), CryptoStreamMode.Write);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        cryptoStream.Write(plainBytes,0, plainBytes.Length);
        cryptoStream.FlushFinalBlock();
        
        byte[] encryptedBytes = memoryStream.ToArray();

        //combine IV and encrypted data
        byte[] resultBytes = new byte[des.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(des.IV, 0, resultBytes, 0, des.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, resultBytes, des.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(resultBytes);
    }
    private string Decrypt(string encryptedText)
    {
        byte[] allBytes = Convert.FromBase64String(encryptedText);

        using var des = TripleDES.Create();
        
        byte[] iv = new byte[des.IV.Length]; // 3DES 的 IV 長度是 8 bytes
        Buffer.BlockCopy(allBytes, 0, iv, 0, iv.Length);
    
        byte[] encryptedBytes = new byte[allBytes.Length - iv.Length];
        Buffer.BlockCopy(allBytes, iv.Length, encryptedBytes, 0, encryptedBytes.Length);

        des.Key = EncryptionKey;
        des.IV = iv; // 使用我們剛剛分離出來的 IV
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.PKCS7;

        using var memoryStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memoryStream, des.CreateDecryptor(), CryptoStreamMode.Write);

        cryptoStream.Write(encryptedBytes, 0, encryptedBytes.Length);
        cryptoStream.FlushFinalBlock();
    
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
    
    
}
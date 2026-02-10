# ShufflerWPF

North Shuffler 是一個使用 C# WPF 技術開發的桌面應用程式，適用於牌靴建立、洗牌管理，並支援 QRCode 產生與列印。

前提:
1.環境需要安裝.NET 9
https://dotnet.microsoft.com/zh-tw/download/dotnet/9.0
2.列表機驅動程式
https://www.brother.tw/zh-tw/support/ql-810w/downloads

## 主要功能

- **用戶掃描**  
  支援掃描槍快速輸入用戶身份，並自動判斷輸入類型與閒置清空。可進入管理員模式或手動模式，提升操作彈性。

- **日誌管理（Log4netManager）**  
  內建高效能、執行緒安全的非同步日誌管理器（Singleton）。  
  - 寫入日誌到本地檔案（Logs 資料夾），自動分日建立檔案。
  - 支援 Info、Warn、Error、Fatal 等多種等級。
  - 例外與堆疊資訊自動記錄。
  - 結束時自動清理資源。

- **QRCode 與文字列印（Ql810WManager）**  
  支援生成 QRCode 圖片，並可加上頂部及底部文字，輸出 Bitmap，方便標籤列印。  
  - 可指定儲存路徑與檔名。
  - 文字自動置中繪製。

## 專案架構

- `MainWindow.xaml.cs`：主視窗，負責應用程式初始化及主流程控制。
- `Pages/UserScanIdPage.xaml.cs`：用戶掃描頁面，負責掃描槍輸入監控、資料校驗與操作模式切換。
- `Manager/Log4netManager.cs`：日誌管理，負責所有日誌訊息的收集與寫入。
- `Manager/Ql810WManager.cs`：QRCode 與標籤圖檔生成管理。
- 其他頁面與管理類別根據需求擴充。

## 使用方式

1. 安裝必要的 .NET 及 WPF 執行環境。
2. 執行 ShufflerWPF，依畫面指示進行身份掃描或管理操作。
3. 日誌檔案會自動產生於程式根目錄下的 `Logs` 資料夾。
4. 若有標籤列印需求，可使用 QRCode 功能，產生含文字的標籤圖檔。

## 開發重點

- 採用 Singleton/Manager 管理設計，確保資源一致性與高效能。
- 掃描輸入防呆與自動清空，提升現場操作流暢度。
- 日誌系統可自訂等級，支援例外追蹤，便於除錯與維護。
- QRCode 標籤生成介面彈性，可根據需求自訂文字內容與格式。

專案維護人：AndyLiuNorth  
GitHub: [AndyLiuNorth/ShufflerWPF](https://github.com/AndyLiuNorth/ShufflerWPF)

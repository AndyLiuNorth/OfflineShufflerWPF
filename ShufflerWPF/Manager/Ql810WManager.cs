using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using ShufflerWPF.SingleTon;
using ZXing;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;


namespace ShufflerWPF.Manager;
public class BoxInfo
{
    public string DateRange { get; set; } = "";
    public string Studio { get; set; } = "";
    public string BoxId { get; set; } = "";
    public string MachineIp { get; set; } = "";
}
public class Ql810WManager
{
    private static readonly System.Drawing.Text.PrivateFontCollection? _pfc;
    public enum PrintMode
    {
        ShoeQRcode =0,
        ShoeInfo =1,
    }
    public class _printInfo
    {
        public PrintMode Printmode { get; set; } = PrintMode.ShoeQRcode;
        private DateTime? _createDate;

        public string createdate
        {
            get
            {
                return DataCenter.CurrentTrueBox.createTime;
            }
        }
        public string createdateInfo
        {
            
            get
            {
                string formateddate = DataCenter.FormatDateToCustomStyle(DataCenter.CurrentTrueBox.createTime);
                return formateddate;
            }
            set
            {
                if (DateTime.TryParse(value, out var dt))
                    _createDate = dt.Date;   // 只留日期
                else
                    _createDate = null;
            }
        }

        public string Count
        {
            get
            {
                return DataCenter.CurrentTrueBox?.count?.ToString() ?? "";
            }
        }

        public string Maxcount
        {
            get
            {
                return DataCenter.CurrentTrueBox.maxCount.ToString()?? "";
            }
        }

        public string DeadlineInfo
        {
            get
            {
                string formateddate = DataCenter.FormatDateToCustomStyle(DataCenter.CurrentTrueBox.deadline);
                return formateddate;
            }
        }

        public string shufflerIp
        {
            get
            {
                return DataCenter.CurrentTrueBox.shufflerIp;
            }
            set
            {
                // do nothing
            }
        }

        public string tableIDName
        {
            get
            {
                return DataCenter.CurrentTrueBox.PrintName;
            }
            
        }

        public string Studio
        {
            get
            {
                return DataCenter.MyShufflerSettings.Studio;
            }
        }
        
        public string detailtime
        {
            get
            {
                return DataCenter.CurrentTrueBox.detailTime;
            }
        }
    }
    
    public _printInfo PrintInfo { get; set; }
    
    public Ql810WManager()
    {
        PrintInfo = new _printInfo();
    }

    public async Task<bool> Ql810DoPrint(string? trueId=null)
    {
        try
        {
            //// Load COM Dll
            // var doc = new Document();
            // doc.Open(@".\AndyTestNorth.lbx");
            //
            // // 設定文字物件
            // DateTime nowtime = DateTime.Today;
            // doc.GetObject("DateObj").Text = nowtime.ToShortDateString();
            // doc.GetObject("TableTypeObject").Text = namestring;
            //
            // // 設定 QR Code 物件（這個物件需事先在 .lbx 中建立並命名為 objQRCode）
            // //debug demo
            // doc.GetObject("objQRCode").Text = "1c6b27f2-74d1-4e58-99b9-8d5e88c93d48";  // 可以是任意文字或 URL
            //
            // // 開始列印
            // doc.StartPrint("", PrintOptionConstants.bpoDefault);
            // doc.PrintOut(1, PrintOptionConstants.bpoDefault);
            // doc.EndPrint();
            // doc.Close();
            
           
            

            string? imagepath = SaveQrCodeWithText(trueId);
            
            if(imagepath==null) 
            {
                throw new Exception("Generate QR Code Image Failed");
            }
            else
            {
                bool printresult = await PrintImage(imagepath);
                if (!printresult)
                {
                    return false;
                }
            }
            
            

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return true;
    }
    
    private const int QrCodeSize = 240;
    private const int TextHeight = 40;
    private const string FontName = "Arial";
    private const int QrFontSize = 20;
    private const int InfoFontSize = 14;
    
    /// <summary>
    /// 讀圖片影印
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public async Task<bool> PrintImage(string filePath) {
        PrintDocument pd = new PrintDocument();

        // 获取默认打印机的纸张大小
        PaperSize defaultPaperSize = pd.DefaultPageSettings.PaperSize;
        
        pd.PrinterSettings.PrinterName = "Brother QL-810W";
        
        if (!pd.PrinterSettings.IsValid)
        {
            CustomMessageBox.ShowDialog($"Error：Can't find '{pd.PrinterSettings.PrinterName}' Printer。", "Printer Error", CustomMessageBoxButtonType.Ok, CustomMessageBoxIcon.Error);
            Console.WriteLine($"Error：Can't find '{pd.PrinterSettings.PrinterName}' Printer。");
            return false;
        }
        else
        {
            foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
            {
                // 印出名稱和尺寸 (尺寸單位為 1/100 英寸)
                Console.WriteLine($" - Name: {ps.PaperName}, width: {ps.Width}, Heigh: {ps.Height}");
            }
            
            string desiredPaperName = "62mm x 100mm"; 
            PaperSize? desiredSize = null;
            foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
            {
                if (ps.PaperName.Contains(desiredPaperName))
                {
                    desiredSize = ps;
                    break; // 找到就跳出迴圈
                }
            }

            if (desiredSize != null)
            {
                // 將找到的紙張大小設定為此列印工作的預設值
                pd.DefaultPageSettings.PaperSize = desiredSize;
            }
        }
        
        // 創建一個 TaskCompletionSource ---
        // 這個物件將用來手動完成 Task
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        PrintPageEventHandler? printPageHandler = null;
        PrintEventHandler? endPrintHandler = null;

        // printPageHandler = (sender, e) => {
        //     try
        //     {
        //         // 使用 using 陳述式確保 Image 物件被正確釋放
        //         using (System.Drawing.Image img = System.Drawing.Image.FromFile(filePath))
        //         {
        //             Rectangle pageBounds = e.PageBounds;
        //             if(img.Width > pageBounds.Width || img.Height > pageBounds.Height) 
        //             {
        //                 float scale = Math.Min((float)pageBounds.Width / img.Width, (float)pageBounds.Height / img.Height);
        //                 int scaledWidth = (int)(img.Width * scale);
        //                 int scaledHeight = (int)(img.Height * scale);
        //                 e.Graphics.DrawImage(img, 1, 1, scaledWidth, scaledHeight);
        //             } 
        //             else 
        //             {
        //                 e.Graphics.DrawImage(img, 1, 1, img.Width, img.Height);
        //             }
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         // 如果在繪製時發生錯誤，就讓 Task 失敗
        //         tcs.TrySetException(ex);
        //         e.Cancel = true; // 取消列印工作
        //     }
        // };
        
        printPageHandler = (sender, e) =>
        {
            try
            {
                using var img = Image.FromFile(filePath);

                // PageBounds.Width/Height 是 1/100 inch，轉成「裝置像素」
                int pagePxW = (int)Math.Round(e.PageBounds.Width  / 100f * e.Graphics.DpiX);
                int pagePxH = (int)Math.Round(e.PageBounds.Height / 100f * e.Graphics.DpiY);

                // 如果想完全不縮放且靠左上，直接：
                // e.Graphics.DrawImageUnscaled(img, 0, 0); return;

                if (img.Width > pagePxW || img.Height > pagePxH)
                {
                    float scale = Math.Min((float)pagePxW / img.Width, (float)pagePxH / img.Height);
                    int drawW = (int)Math.Round(img.Width * scale);
                    int drawH = (int)Math.Round(img.Height * scale);
                    e.Graphics.DrawImage(img, 0, 0, drawW, drawH);
                }
                else
                {
                    e.Graphics.DrawImageUnscaled(img, 0, 0);
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                e.Cancel = true;
            }
        };

        try
        {
            endPrintHandler = (sender, e) => {
                // 【改善 2】: 移除事件訂閱，避免記憶體洩漏
                if (printPageHandler != null) pd.PrintPage -= printPageHandler;
                if (endPrintHandler != null) pd.EndPrint -= endPrintHandler;
                pd.Dispose(); // 釋放 PrintDocument 資源

                // 檢查 Task 是否已經因為 PrintPage 中的錯誤而失敗
                if (tcs.Task.IsFaulted || tcs.Task.IsCanceled)
                {
                    return;
                }

                if (e.Cancel)
                {
                    // 若工作被取消 (通常是因為 PrintPage 出錯)
                    tcs.TrySetCanceled();
                }
                else
                {
                    // 如果到這裡都沒有發生錯誤，代表列印成功
                    tcs.TrySetResult(true);
                }
            };

            // 訂閱事件
            pd.PrintPage += printPageHandler;
            pd.EndPrint += endPrintHandler;
            
            // --- 開始列印 ---
            pd.Print();
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
        // wait here until the print job is done
        // bool isSuccess = await tcs.Task;
        return await tcs.Task;
        
    }
    
    /// <summary>
    /// 產生包含上下文字的 QR Code 圖片，並儲存為 PNG 檔案，返回Path
    /// </summary>
    /// <param name="qrCodeContent">要編碼到 QR Code 中的原始字串。</param>
    /// <param name="displayName">顯示在 QR Code 下方的文字。</param>
    /// <returns>成功儲存的檔案路徑；若失敗則返回 null。</returns>
    public string? SaveQrCodeWithText(string? qrCodeContent=null)
    {
        string prefixedQrCode = String.Empty;
        try
        {
            Bitmap finalImage;
            if (PrintInfo.Printmode == PrintMode.ShoeQRcode)
            {
                if (!string.IsNullOrEmpty(qrCodeContent))
                {
                    prefixedQrCode = "QC-" + qrCodeContent;
                }
                string toptext = PrintInfo.createdate+ "\r\n" + PrintInfo.detailtime;
                
                finalImage = CreateQRCodeImage(prefixedQrCode, toptext, PrintInfo.tableIDName ?? string.Empty);
            }
            else // ShoeInfo
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{PrintInfo.Studio}");
                sb.AppendLine($"{PrintInfo.createdateInfo}-{PrintInfo.DeadlineInfo}");
                sb.AppendLine($"{PrintInfo.Studio}");
                sb.AppendLine($"{PrintInfo.shufflerIp}");
                
                string infotext = sb.ToString().TrimEnd();

                var nowinformation = new BoxInfo
                {
                    DateRange = $"{PrintInfo.createdateInfo} -\r\n{PrintInfo.DeadlineInfo}",
                    Studio = PrintInfo.Studio,
                    BoxId = PrintInfo.tableIDName,
                    MachineIp = PrintInfo.shufflerIp
                };
                
                finalImage = CreateQRCodeImage(string.Empty, string.Empty, infotext, nowinformation);
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string barcodeDirectory = Path.Combine(baseDirectory, "PrintBackUp\\" + DateTime.Now.ToString("yyyyMMdd") + "\\qrcode");
            if (!Directory.Exists(barcodeDirectory))
            {
                Directory.CreateDirectory(barcodeDirectory);
            }
            string safeTable = string.IsNullOrEmpty(PrintInfo.tableIDName) ? "NO_TABLE" : PrintInfo.tableIDName;
            

            using (finalImage) // release
            {
                if (PrintInfo.Printmode == PrintMode.ShoeInfo)
                {
                    string filePath = Path.Combine(barcodeDirectory, safeTable + "_" + DateTime.Now.ToString("yyyyMMdd") + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    finalImage.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    return filePath;
                }
                else if (PrintInfo.Printmode == PrintMode.ShoeQRcode)
                {
                    string filePathQRBackUp = Path.Combine(barcodeDirectory, safeTable + "_" + DateTime.Now.ToString("yyyyMMdd_QR") + ".png");
                    Directory.CreateDirectory(Path.GetDirectoryName(filePathQRBackUp)!);
                    finalImage.Save(filePathQRBackUp, System.Drawing.Imaging.ImageFormat.Png);
                    
                    return filePathQRBackUp;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return null;
        }
    }

    private const int PrinterDpi = 300;
    private static int MmToPx(float mm) => (int)Math.Round(mm / 25.4f * PrinterDpi);

    /// <summary>
    /// 負責建立包含文字和 QR Code 的最終 Bitmap。
    /// </summary>
    /// <param name="qrCodeData"></param>
    /// <param name="topText"></param>
    /// <param name="bottomText"></param>
    /// <returns></returns>
    private Bitmap CreateQRCodeImage(string qrCodeData, string topText, string bottomText, BoxInfo? boxinfo=null)
    {

        Bitmap? finalImage = null;
        
        if (this.PrintInfo.Printmode == PrintMode.ShoeQRcode)
        {
            
            //var finalImage = new Bitmap(QrCodeSize, QrCodeSize + TextHeight * 2);
            int startY = 2;
            int startX = 0;
            int safePaddingRight = 1;
            finalImage = new Bitmap(245, 360, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            
            //SetResolution(96,96) 只是告訴驅動「這 360px 代表 3.75 英寸」，與印表機原生 300DPI 不同，驅動必須再重取樣。
            finalImage.SetResolution(96, 96); // 
            
            using var g = Graphics.FromImage(finalImage);
            g.Clear(Color.White);
            g.PageUnit = GraphicsUnit.Pixel;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var font = new Font(FontName, QrFontSize);
            using var topFont   = CreateDpiInvariantPixelFont(FontName, 25f, g);
            //using var midFont   = CreateDpiInvariantPixelFont(FontName, 20f, g);
            //using var bottomFont= CreateDpiInvariantPixelFont(FontName, 20f, g);
            using var brush = new SolidBrush(Color.Black);
            
            int contentWidth = finalImage.Width - startX * 2 - safePaddingRight;

            
            // First. Draw Date and IP
            // Y start from 0
            if (topText != string.Empty)
            {
                //var topRect = new RectangleF(startX, startY, contentWidth, TextHeight*2);
                //DrawCenteredText(g, topText, topfont, brush, new RectangleF(startX, startY, QrCodeSize, TextHeight*2));
                DrawCenteredText(g, topText, topFont, brush, new RectangleF(startX+10, startY, QrCodeSize, TextHeight * 2), designDpi:96f, autoScaleForDpi:false);

                //DrawCenteredTextTight(g, topText, topfont, brush, new RectangleF(startX, startY, QrCodeSize, TextHeight*2));
            }

            // Second. Draw QRcode
            float labelWidthMm  = 62f;
            float labelHeightMm = 100f;
            float marginMm      = 4f;
            int m = MmToPx(marginMm);
            int w = MmToPx(labelWidthMm);
            int h = MmToPx(labelHeightMm);
            const float qrSizeMm = 50f; // 自行調整
            int qrSizePx = Math.Min(MmToPx(qrSizeMm), w - 2 * m);
            //int qrSizePx = QrCodeSize;
            //if (qrSizePx > contentWidth) qrSizePx = contentWidth;
            
            if (!string.IsNullOrEmpty(qrCodeData))
            {
                
                var writer = new BarcodeWriter<Bitmap>
                {
                    Format = BarcodeFormat.QR_CODE,
                    Renderer = new BitmapRenderer(),
                   
                    Options = new QrCodeEncodingOptions
                    {
                        Height = qrSizePx,
                        Width = qrSizePx,
                        Margin = 0,
                        CharacterSet = "UTF-8"
                    }
            
                };
                using var qrCodeImage = writer.Write(qrCodeData);
                qrCodeImage.SetResolution(300, 300);
                
                // Area of QRcode
                // Y start from DateLine height
                //g.DrawImage(qrCodeImage, 8, TextHeight*2+startY);
                
                // pixel = (mm / 25.4) * DPI
                // DPI = Dots Per Inch
                // 1 inch = 25.4 mm+
                
                float dpiX = g.DpiX;
                float dpiY = g.DpiY;
                float qrWidthPx = qrSizeMm / 25.4f * dpiX;
                float qrHeightPx = qrSizeMm / 25.4f * dpiY;
                
                var destRect = new RectangleF(25, TextHeight * 2 + startY, qrWidthPx, qrHeightPx);
                g.DrawImage(qrCodeImage, destRect);
            }
            else
            {
                qrSizePx = 0;
            }
            
            // Third. Draw Table ID
            // Y start from QRcode height + DateLine height
            // int qrcodeY = startY+ TextHeight + QrCodeSize;
            // DrawCenteredText(g, bottomText, font, brush, new RectangleF(startX, qrcodeY, QrCodeSize, TextHeight));
            
            // Bottom text
            int bottomY = 360-TextHeight*2;
            var bottomRect = new RectangleF(startX, bottomY, contentWidth, TextHeight);
            DrawCenteredTextTight(g, bottomText, font, brush, bottomRect);
           
            // 90  degree
            finalImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
        }
        else if (this.PrintInfo.Printmode == PrintMode.ShoeInfo)
        {
            // int startY = 2;
            // int startX = 2;
            // finalImage = new Bitmap(245, 360);
            // using var g = Graphics.FromImage(finalImage);
            // using var font = new Font(FontName, InfoFontSize);
            // using var brush = new SolidBrush(Color.Black);
            // g.Clear(Color.White);
            //
            // //// Area of Table ID. In QRcode case qrcodeY is QRcode height size.
            // //DrawCenteredText(g, bottomText, font, brush, new RectangleF(startY, startX, 245, 360));
            //
            // // bottomText 為前面 sb.ToString()
            // var area = new Rectangle(startX, startY, finalImage.Width - startX * 2, finalImage.Height - startY * 2);

            finalImage = CreateBoxInfoTable(boxinfo);
        }
        
        // 90  degree
        //finalImage.RotateFlip(RotateFlipType.Rotate90FlipNone);
        
        return finalImage;
    }

    /// <summary>
    /// 在指定區域內置中繪製文字
    /// </summary>
    /// <param name="g"></param>
    /// <param name="text"></param>
    /// <param name="font"></param>
    /// <param name="brush"></param>
    /// <param name="area"></param>
    // private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, RectangleF area)
    // {
    //     if (string.IsNullOrEmpty(text)) return;
    //     
    //     var textSize = g.MeasureString(text, font);
    //     var location = new PointF(
    //         area.X + (area.Width - textSize.Width) / 2,
    //         area.Y + (area.Height - textSize.Height) / 2
    //     );
    //     g.DrawString(text, font, brush, location);
    // }
    private static void DrawCenteredText(Graphics g, string text, Font font, Brush brush, RectangleF area, float designDpi = 96f, bool autoScaleForDpi = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 1. 決定實際用字體 (針對 Pixel 字型才縮放；Point 則保持物理一致)
        Font useFont = font;
        Font? tmpScaled = null;
        if (autoScaleForDpi && font.Unit == GraphicsUnit.Pixel)
        {
            float scale = g.DpiX / designDpi;
            if (Math.Abs(scale - 1f) > 0.01f)
            {
                tmpScaled = new Font(font.FontFamily, font.Size * scale, font.Style, GraphicsUnit.Pixel);
                useFont = tmpScaled;
            }
        }

        // 2. 使用緊致量測 (避免 MeasureString 預設 padding)
        using var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
        sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        SizeF size = g.MeasureString(text, useFont, int.MaxValue, sf);

        // 3. 若超出寬度，等比縮小 (再次建立縮放字體)
        if (size.Width > area.Width)
        {
            float shrink = area.Width / size.Width;
            float newSize = Math.Max(6f, useFont.Size * shrink);
            tmpScaled?.Dispose();
            tmpScaled = new Font(useFont.FontFamily, newSize, useFont.Style, useFont.Unit);
            useFont = tmpScaled;
            size = g.MeasureString(text, useFont, int.MaxValue, sf);
        }

        // 4. 計算置中座標並四捨五入到整數避免模糊
        float x = area.X + (area.Width - size.Width) / 2f;
        float y = area.Y + (area.Height - size.Height) / 2f;
        x = (float)Math.Round(x);
        y = (float)Math.Round(y);

        // 5. 建議外部：g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit (在熱感/標籤機上較銳利)
        g.DrawString(text, useFont, brush, new PointF(x, y), sf);

        tmpScaled?.Dispose();
    }
    const float MaxTableWidth = 400f;          // 目標上限
    
    private Font CreateSafeFont(string familyName, float sizePx, FontStyle style = FontStyle.Regular)
    {
        // 優先使用內嵌字型
        if (_pfc != null && _pfc.Families.Length > 0)
            return new Font(_pfc.Families[0], sizePx, style, GraphicsUnit.Pixel);
        // 回退系統字型(用 Pixel 確保不受 DPI 轉換)
        return new Font(familyName, sizePx, style, GraphicsUnit.Pixel);
    }

    
    private Bitmap CreateBoxInfoTable(
    BoxInfo? info,
    string fontName = "Segoe UI",
    float headerFontSizePx = 16f,
    float bodyFontSizePx = 14f,
    int horizontalPadding = 1,
    int verticalPadding = 10,
    int lineThickness = 1,
    int minHeightPerRow = 50,
    float dateMinWidth = 125f,
    float? maxTableWidth = null)
    {
        info ??= new BoxInfo();
        string[] headers = { "Date", "Studio", "Box ID", "Machine IP" };
        string[] values =
        {
            info.DateRange ?? "",
            info.Studio ?? "",
            info.BoxId ?? "",
            info.MachineIp ?? ""
        };

        float targetMaxWidth = maxTableWidth ?? MaxTableWidth;

        using var headerFont = CreateSafeFont(fontName, headerFontSizePx, FontStyle.Bold);
        using var bodyFont   = CreateSafeFont(fontName, bodyFontSizePx, FontStyle.Regular);

        // 用 1x1 Bitmap 做量測，並鎖 96 DPI
        using var measureBmp = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
        measureBmp.SetResolution(96, 96);
        using var gMeasure = Graphics.FromImage(measureBmp);
        gMeasure.PageUnit = GraphicsUnit.Pixel;
        gMeasure.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        float[] colWidths = new float[headers.Length];
        for (int i = 0; i < headers.Length; i++)
        {
            float headerW = gMeasure.MeasureString(headers[i], headerFont).Width;
            float valueW  = gMeasure.MeasureString(values[i], bodyFont).Width;
            float need = Math.Max(headerW, valueW) + horizontalPadding * 2;
            if (i == 0)
                need = Math.Max(need, dateMinWidth);
            colWidths[i] = need;
        }

        float totalWidth = colWidths.Sum();

        if (totalWidth > targetMaxWidth)
        {
            // 可依前面討論壓縮
            float scale = targetMaxWidth / totalWidth;
            for (int i = 0; i < colWidths.Length; i++)
            {
                colWidths[i] = (float)Math.Floor(colWidths[i] * scale);
            }
            totalWidth = colWidths.Sum();
        }

        float headerRowHeight = Math.Max(
            minHeightPerRow,
            gMeasure.MeasureString("Ag", headerFont).Height + verticalPadding * 2);
        float bodyRowHeight = Math.Max(
            minHeightPerRow,
            gMeasure.MeasureString("Ag", bodyFont).Height + verticalPadding * 2);

        int bmpWidth = (int)Math.Ceiling(totalWidth + lineThickness + 2);
        int bmpHeight = (int)Math.Ceiling(headerRowHeight + bodyRowHeight + lineThickness);

        var bmp = new Bitmap(bmpWidth, bmpHeight, PixelFormat.Format32bppPArgb);
        bmp.SetResolution(96, 96); // 鎖定輸出 DPI
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None; // 表格線銳利
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.White);

        using var pen = new Pen(Color.Black, lineThickness)
        {
            Alignment = System.Drawing.Drawing2D.PenAlignment.Inset
        };
        using var headerBrush = new SolidBrush(Color.Black);
        using var bodyBrush = new SolidBrush(Color.Black);
        var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

        float x = 0f;
        // Header
        for (int i = 0; i < headers.Length; i++)
        {
            float w = colWidths[i];
            var rect = new RectangleF(x, 0, w, headerRowHeight);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            var textRect = new RectangleF(rect.X + horizontalPadding, rect.Y, rect.Width - horizontalPadding * 2, rect.Height);
            g.DrawString(headers[i], headerFont, headerBrush, textRect, sfLeft);
            x += w;
        }

        // Body
        x = 0f;
        float bodyY = headerRowHeight;
        for (int i = 0; i < values.Length; i++)
        {
            float w = colWidths[i];
            var rect = new RectangleF(x, bodyY, w, bodyRowHeight);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            var textRect = new RectangleF(rect.X + horizontalPadding, rect.Y, rect.Width - horizontalPadding * 2, rect.Height);
            g.DrawString(values[i], bodyFont, bodyBrush, textRect, sfLeft);
            x += w;
        }

        return bmp;
    }
    
    private static Font CreateDpiInvariantPixelFont(string family, float designPixelSize, Graphics g, FontStyle style = FontStyle.Regular)
    {
        // 抵銷作業系統縮放(邏輯 DPI)；保持「設計像素」一致
        float scale = 96f / g.DpiX; // g.DpiX=120 (125%) 時 => scale=0.8
        float actualPixelSize = designPixelSize * scale;
        return new Font(family, actualPixelSize, style, GraphicsUnit.Pixel);
    }
    
    private static void DrawCenteredTextTight(Graphics g, string text, Font font, Brush brush, RectangleF area)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
        sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        // 先量測
        SizeF size = g.MeasureString(text, font, int.MaxValue, sf);

        // 若超出就縮小（簡單等比，必要時可改多行）
        if (size.Width > area.Width)
        {
            float scale = area.Width / size.Width;
            float newSize = Math.Max(6f, font.Size * scale);
            using var scaledFont = new Font(font.FontFamily, newSize, font.Style, GraphicsUnit.Pixel);
            size = g.MeasureString(text, scaledFont, int.MaxValue, sf);
            float x2 = area.X + (area.Width - size.Width) / 2f;
            float y2 = area.Y + (area.Height - size.Height) / 2f;
            x2 = (float)Math.Floor(x2);
            y2 = (float)Math.Floor(y2);
            g.DrawString(text, scaledFont, brush, new PointF(x2, y2), sf);
            return;
        }

        float x = area.X + (area.Width - size.Width) / 2f;
        float y = area.Y + (area.Height - size.Height) / 2f;

        // 對齊到整數避免半像素模糊
        x = (float)Math.Floor(x);
        y = (float)Math.Floor(y);

        g.DrawString(text, font, brush, new PointF(x, y), sf);
    }
    private void DrawLeftMiddleText(Graphics g, string text, Font font, Brush brush, RectangleF rect, int paddingLeft)
    {
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoClip
        };
        var drawRect = new RectangleF(rect.X + paddingLeft, rect.Y, rect.Width - paddingLeft * 2, rect.Height);
        g.DrawString(text, font, brush, drawRect, sf);
    }


}
using PuppeteerSharp;
using PuppeteerSharp.Media;
 
namespace NoteVault.Services;
 
public class PdfService
{
    public async Task<byte[]> HtmlToPdfAsync(string html)
    {
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });
 
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
 
        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "20mm",
                Bottom = "20mm",
                Left = "15mm",
                Right = "15mm"
            }
        });
 
        return pdfBytes;
    }
}
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TelegramBotCarInsurance
{
    static internal class PdfService
    {
        public static async Task<string> GeneratePdfAsync(string text, string outputPath)
        {
            try
            {
                await Task.Run(() =>
                {
                    var document = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(50);

                            page.Content().Text(text, TextStyle.Default.Size(16));
                        });
                    });

                    QuestPDF.Settings.License = LicenseType.Community;
                    document.GeneratePdf(outputPath);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return outputPath;
        }
    }
}

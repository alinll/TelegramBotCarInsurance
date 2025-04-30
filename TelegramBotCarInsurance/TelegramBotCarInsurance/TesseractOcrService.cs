using Tesseract;

namespace TelegramBotCarInsurance
{
    internal static class TesseractOcrService
    {
        static readonly string tessDataPath = Constants.destinationTessData;
        public static string ExtractTextFromImage(string imagePath)
        {
            try
            {
                using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                using var img = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);
                return page.GetText();
            }
            catch (Exception ex)
            {
                Console.WriteLine("OCR error: " + ex.Message);
                return "Failed to read document text.";
            }
        }
    }
}

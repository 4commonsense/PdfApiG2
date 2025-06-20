using iText.Kernel.Pdf;

namespace PdfApiGaisler.Services
{
    public interface IImageExtractorService
    {
        List<byte[]> ExtractImagesFromPage(PdfPage page);
        List<byte[]> ExtractImagesFromPages(List<PdfPage> pages);
    }
}
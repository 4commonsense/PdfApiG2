using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Collections.Generic;
namespace PdfApiGaisler.Services
{
   
    public class ImageExtractorService : IImageExtractorService
    {
        public List<byte[]> ExtractImagesFromPage(PdfPage page)
        {
            var listener = new ImageRenderListener();
            var processor = new PdfCanvasProcessor(listener);
            processor.ProcessPageContent(page);
            return listener.GetImageBytes();
        }
        public List<byte[]> ExtractImagesFromPages(List<PdfPage> pages)
        {
            var allImages = new List<byte[]>();
            foreach (var page in pages)
            {
                var images = ExtractImagesFromPage(page);
                allImages.AddRange(images);
            }
            return allImages;
        }
        private class ImageRenderListener : IEventListener
        {
            private readonly List<byte[]> _imageBytesList = new List<byte[]>();
            public void EventOccurred(IEventData data, EventType type)
            {
                if (type == EventType.RENDER_IMAGE && data is ImageRenderInfo renderInfo)
                {
                    var imageObject = renderInfo.GetImage();
                    if (imageObject != null)
                    {
                        _imageBytesList.Add(imageObject.GetImageBytes());
                    }
                }
            }
            public ICollection<EventType> GetSupportedEvents()
            {
                return new HashSet<EventType> { EventType.RENDER_IMAGE };
            }
            public List<byte[]> GetImageBytes()
            {
                return _imageBytesList;
            }
        }
    }
}
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Utils;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using PdfApiGaisler.Models; // подключение моделей
using PdfApiGaisler.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using FileResultModel = PdfApiGaisler.Models.FileResult;

namespace PdfApiGaisler.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly IImageExtractor _imageExtractor;
        public PdfController(IImageExtractor imageExtractor)
        {
            _imageExtractor = imageExtractor;
        }

        [HttpPost("MakePDF")]
        public ActionResult<List<FileResultModel>> MakePDF([FromBody] List<FileRequest> fileRequests)
        {
            if (fileRequests == null || !fileRequests.Any())
                return BadRequest("Список файлов пуст или не передан.");

            using (var outputStream = new MemoryStream())
            {
                try
                {
                    using (var pdfWriter = new PdfWriter(outputStream))
                    {
                        using (var pdfDoc = new PdfDocument(pdfWriter))
                        {
                            foreach (var fileRequest in fileRequests)
                            {
                                if (string.IsNullOrEmpty(fileRequest.Base64Content))
                                    continue; // пропускаем пустые файлы

                                var fileBytes = Convert.FromBase64String(fileRequest.Base64Content);
                                var extension = GetFileExtension(fileBytes);

                                if (IsImage(fileBytes))
                                {
                                    using (var ms = new MemoryStream(fileBytes))
                                    {
                                        var imageData = ImageDataFactory.Create(ms.ToArray());
                                        var image = new iText.Layout.Element.Image(imageData);
                                        var pageSize = PageSize.A4;

                                        // Масштабирование изображения
                                        image.ScaleToFit(pageSize.GetWidth() * 0.9f, pageSize.GetHeight() * 0.9f);

                                        // Создаем новую страницу для каждого изображения
                                        pdfDoc.AddNewPage(pageSize);
                                        var page = pdfDoc.GetLastPage();
                                        var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
                                        image.SetFixedPosition(0, 0);
                                        new Canvas(canvas, page.GetPageSize()).Add(image);
                                    }
                                }
                                else if (IsTextFile(fileBytes))
                                {
                                    var textContent = Encoding.UTF8.GetString(fileBytes);
                                    pdfDoc.AddNewPage(PageSize.A4);
                                    var page = pdfDoc.GetLastPage();
                                    var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
                                    new Canvas(canvas, PageSize.A4).Add(new Paragraph(textContent));
                                }
                                else
                                {
                                    // Неизвестный тип файла
                                    pdfDoc.AddNewPage(PageSize.A4);
                                    var page = pdfDoc.GetLastPage();
                                    var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
                                    new Canvas(canvas, PageSize.A4).Add(new Paragraph($"Файл типа {extension} не поддерживается для отображения."));
                                }
                            }
                        }
                    }

                    var resultBytes = outputStream.ToArray();
                    var base64Result = Convert.ToBase64String(resultBytes);

                    // Возвращаем один объединённый PDF
                    return new List<FileResultModel> {
                new FileResultModel
                {
                    FileName = "merged.pdf",
                    Base64Content = base64Result
                }
            };
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "Внутренняя ошибка сервера", detail = ex.Message });
                }
            }
        }





        [HttpPost("DisassemblePDF")]
        public ActionResult<List<FileResultModel>> DisassemblePDF([FromBody] PdfApiGaisler.Models.FileResult request)
        {
            var results = new List<FileResultModel>();

            try
            {
                var pdfBytes = Convert.FromBase64String(request.Base64Content);
                var originalFileName = request.FileName ?? "file.pdf";

                using (var inputStream = new MemoryStream(pdfBytes))
                {
                    var pdfReader = new PdfReader(inputStream);
                    var pdfDoc = new PdfDocument(pdfReader);

                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var page = pdfDoc.GetPage(i);

                        // Попытка извлечь текст
                        var text = PdfTextExtractor.GetTextFromPage(page);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            var textBytes = Encoding.UTF8.GetBytes(text);
                            results.Add(new FileResultModel
                            {
                                FileName = $"{System.IO.Path.GetFileNameWithoutExtension(originalFileName)}_page_{i}.txt",
                                Base64Content = Convert.ToBase64String(textBytes)
                            });
                        }
                        else
                        {
                            // Попытка извлечь изображение
                            var imageBytes = _imageExtractor.ExtractImagesFromPage(page);
                            if (imageBytes != null && imageBytes.Length > 0)
                            {
                                // Определяем расширение для изображения
                                string imageExt = ".png"; // по умолчанию
                                var originalExt = System.IO.Path.GetExtension(originalFileName).ToLower();
                                if (SupportedImageExtensions.Contains(originalExt))
                                    imageExt = originalExt;

                                results.Add(new FileResultModel
                                {
                                    FileName = $"{System.IO.Path.GetFileNameWithoutExtension(originalFileName)}_page_{i}{imageExt}",
                                    Base64Content = Convert.ToBase64String(imageBytes)
                                });
                            }
                        }
                    }
                }

                if (results.Count == 0)
                {
                    return BadRequest("На страницах PDF не обнаружено текста или изображений");
                }

                return results;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка при разборе PDF", detail = ex.Message });
            }
        }



        // Поддерживаемые расширения изображений
        private static readonly HashSet<string> SupportedImageExtensions = new HashSet<string> { ".png", ".jpg", ".jpeg", ".gif" };







        // Вспомогательные методы
        private string GetFileExtension(byte[] fileBytes)
        {
            if (IsImage(fileBytes))
                return "image";
            if (IsTextFile(fileBytes))
                return "txt";
            return "unknown";
        }

        private bool IsImage(byte[] fileBytes)
        {
            if (fileBytes.Length < 4) return false;
            if (fileBytes.Take(2).SequenceEqual(new byte[] { 0xFF, 0xD8 })) return true;
            if (fileBytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return true;
            if (fileBytes.Take(6).SequenceEqual(System.Text.Encoding.ASCII.GetBytes("GIF87a"))) return true;
            if (fileBytes.Take(6).SequenceEqual(System.Text.Encoding.ASCII.GetBytes("GIF89a"))) return true;

            return false;
        }


        private bool IsTextFile(byte[] fileBytes)
        {
            try
            {
                var text = System.Text.Encoding.UTF8.GetString(fileBytes);
                return text.All(ch =>
                    char.IsControl(ch) ||
                    (ch >= ' ' && ch <= '~') ||  // ASCII printable
                    (ch >= '\u0400' && ch <= '\u04FF') // Кириллица
                );
            }
            catch
            {
                return false;
            }
        }
    }
}
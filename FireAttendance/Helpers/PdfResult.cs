using SelectPdf;
using System;
using System.IO;
using System.Text;
using System.Web.Mvc;

namespace FireAttendance.Helpers {
    public class PdfResult : PartialViewResult {
        public string FielDownloadName { get; set; }

        public int PageOrientation { get; set; } = 0;

        public override void ExecuteResult(ControllerContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            //set model data
            context.Controller.ViewData.Model = Model;
            ViewData = context.Controller.ViewData;
            TempData = context.Controller.TempData;

            //get the view
            if (string.IsNullOrWhiteSpace(ViewName)) {
                ViewName = context.RouteData.GetRequiredString("action");
            }
            ViewEngineResult viewEngineResult = null;
            if (View == null) {
                viewEngineResult = FindView(context);
                View = viewEngineResult.View;
            }

            //render the view
            var sb = new StringBuilder();
            using (var tw = new StringWriter(sb)) {
                var viewContext = new ViewContext(context, View, ViewData, TempData, tw);
                View.Render(viewContext, tw);
            }
            viewEngineResult?.ViewEngine.ReleaseView(context, View);

            //create pdf
            var converter = new HtmlToPdf();
            converter.Options.PdfPageOrientation = (PdfPageOrientation)PageOrientation;
            converter.Options.DisplayHeader = true;
            converter.Header.DisplayOnFirstPage = false;
            converter.Header.DisplayOnEvenPages = true;
            converter.Header.DisplayOnOddPages = true;
            converter.Header.Height = 20;
            converter.Header.Add(new PdfHtmlSection("<hr/>", string.Empty));
            converter.Options.DisplayFooter = true;
            converter.Footer.DisplayOnFirstPage = true;
            converter.Footer.DisplayOnEvenPages = true;
            converter.Footer.DisplayOnOddPages = true;
            converter.Footer.Height = 20;
            var text = new PdfTextSection(0, 10, "Page: {page_number} of {total_pages}  ", new System.Drawing.Font("Arial", 8));
            text.HorizontalAlign = PdfTextHorizontalAlign.Right;
            converter.Footer.Add(text);

            var doc = converter.ConvertHtmlString(sb.ToString());
            using (var ms = new MemoryStream()) {
                doc.Save(ms);
                doc.Close();
                var result = new FileContentResult(ms.ToArray(), "application/pdf") {
                    FileDownloadName = FielDownloadName
                };
                result.ExecuteResult(context);
            }
        }
    }
}
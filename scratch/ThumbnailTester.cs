using System;
using System.IO;
using SolidWorks.Interop.swdocumentmgr;

namespace ThumbnailTester
{
    class Program
    {
        static void Main(string[] args)
        {
            string licenseKey = "MAXIMABIOTECHINC:swdocmgr_previews-11785-02051-00064-33793-08754-34307-00007-11608-10823-45570-54688-00650-25099-32636-02049-01550-21429-63941-08467-39714-16934-22979-01332-09569-01333-09481-20797-03349-09505-03385-14337-27746-58970-57546-25690-25696-1068";
            string filePath = @"C:\Users\user\Documents\GitHub\SW-PDM\src\SWPdm.Api\vault_storage\3e034a2074f649748ef767ccc83f7969\603dfd4d-1d5d-4044-8afb-b8c5a6cc8682_ASM00073_V02_UD26 G2-管內芯組.SLDASM";

            if (!File.Exists(filePath)) {
                Console.WriteLine("File not found: " + filePath);
                return;
            }

            try {
                SwDMClassFactory factory = new SwDMClassFactory();
                SwDMApplication app = factory.GetApplication(licenseKey);
                if (app == null) {
                    Console.WriteLine("Failed to get DM application.");
                    return;
                }

                SwDmDocumentType docType = SwDmDocumentType.swDmDocumentAssembly;
                SwDMDocument18 doc = (SwDMDocument18)app.GetDocument(filePath, docType, true, out SwDmDocumentOpenError err);
                
                if (doc == null) {
                    Console.WriteLine("Failed to open document. Error: " + err);
                    return;
                }

                Console.WriteLine("Document opened successfully. Type: " + doc.GetType().FullName);

                // Try PNG Preview
                try {
                    object pngPreview = doc.GetPreviewPNGBitmap(out SwDmPreviewError pngErr);
                    if (pngErr == SwDmPreviewError.swDmPreviewErrorNone && pngPreview != null) {
                        byte[] bytes = (byte[])pngPreview;
                        Console.WriteLine("PNG Preview found. Size: " + bytes.Length);
                        File.WriteAllBytes("test_thumbnail.png", bytes);
                    } else {
                        Console.WriteLine("PNG Preview not found. Error: " + pngErr);
                    }
                } catch (Exception ex) {
                    Console.WriteLine("PNG Preview exception: " + ex.Message);
                }

                doc.CloseDoc();
            } catch (Exception ex) {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }
    }
}

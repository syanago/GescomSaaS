namespace GescomSaas.Application.Models;

public sealed record CommercialDocumentPdfResult(
    string FileName,
    string ContentType,
    byte[] Content);

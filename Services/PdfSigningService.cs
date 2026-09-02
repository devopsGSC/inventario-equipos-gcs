using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Kernel.Crypto;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Pkcs;

namespace InventarioTI.Services;

// Firma digitalmente los PDF generados por PdfService usando el certificado
// wildcard de la empresa (el mismo que ya se usa para firmar documentos
// manualmente desde Adobe). Se configura via variables de entorno para no
// versionar la ruta ni la contraseña del .pfx:
//   PDF_CERT_PATH     ruta al archivo .pfx/.p12 (con clave privada)
//   PDF_CERT_PASSWORD contraseña del .pfx
// Si no está configurado, Firmar() devuelve el PDF sin firmar (no rompe el
// flujo existente en entornos donde todavía no se cargó el certificado).
public class PdfSigningService
{
    private readonly string? _certPath;
    private readonly string? _certPassword;

    public PdfSigningService()
    {
        _certPath     = Environment.GetEnvironmentVariable("PDF_CERT_PATH");
        _certPassword = Environment.GetEnvironmentVariable("PDF_CERT_PASSWORD");
    }

    public bool Habilitada => !string.IsNullOrWhiteSpace(_certPath) && File.Exists(_certPath);

    public byte[] Firmar(byte[] pdfBytes, string motivo = "Documento generado por InventarioTI",
        string ubicacion = "Global Customs Solutions")
    {
        if (!Habilitada) return pdfBytes;

        var pkcs12 = new Pkcs12StoreBuilder().Build();
        using (var pfxStream = File.OpenRead(_certPath!))
            pkcs12.Load(pfxStream, (_certPassword ?? "").ToCharArray());

        string? alias = pkcs12.Aliases.Cast<string>().FirstOrDefault(pkcs12.IsKeyEntry);
        if (alias == null)
            throw new InvalidOperationException("El certificado PFX configurado en PDF_CERT_PATH no contiene una clave privada.");

        var privateKey = pkcs12.GetKey(alias).Key;
        var chain = pkcs12.GetCertificateChain(alias)
            .Select(c => c.Certificate)
            .ToArray();
        IX509Certificate[] chainWrapped = chain.Select(c => (IX509Certificate)new X509CertificateBC(c)).ToArray();

        using var input  = new MemoryStream(pdfBytes);
        using var output = new MemoryStream();
        var reader = new PdfReader(input);
        var signer = new PdfSigner(reader, output, new StampingProperties());

        var signerProperties = new SignerProperties()
            .SetReason(motivo)
            .SetLocation(ubicacion)
            .SetFieldName("FirmaGCS");
        signer.SetSignerProperties(signerProperties);

        IExternalSignature pks = new PrivateKeySignature(new PrivateKeyBC(privateKey), DigestAlgorithms.SHA256);
        signer.SignDetached(pks, chainWrapped, null, null, null, 0, PdfSigner.CryptoStandard.CMS);

        return output.ToArray();
    }
}

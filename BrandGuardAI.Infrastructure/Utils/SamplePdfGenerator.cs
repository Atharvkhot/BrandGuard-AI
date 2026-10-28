using System.Text;

namespace BrandGuardAI.Infrastructure.Utils;

public static class SamplePdfGenerator
{
    public static byte[] CreateSampleGuidelinePdf()
    {
        var textContent = new StringBuilder();
        textContent.AppendLine("BRANDGUARD GLOBAL MARKETING AND BRAND COMPLIANCE GUIDELINES (2026)");
        textContent.AppendLine("Section 1: General Tone and Voice Requirements");
        textContent.AppendLine("All official marketing content, social media posts, and advertising materials must maintain an authoritative, professional, and respectful tone.");
        textContent.AppendLine("Aggressive urgency language, hype claims, and deceptive clickbait tactics are strictly prohibited across all brand channels.");
        textContent.AppendLine();
        textContent.AppendLine("Section 2: Claims, Guarantees, and Performance Statements");
        textContent.AppendLine("Policy 2.1: Absolute Guarantees Prohibited.");
        textContent.AppendLine("Marketing content must not contain absolute outcome guarantees or promise 100% results. We never use phrases such as 'guarantees 100% results', 'never fails', or 'zero risk'. Outcomes must be qualified based on customer use cases.");
        textContent.AppendLine("Policy 2.2: Superlative and Market Leadership Claims.");
        textContent.AppendLine("Do not claim a product is the best in the world or #1 in the market without verified, accredited third-party audit documentation. Statements such as 'best solution in the world' or 'superior to all alternatives' are prohibited unless explicitly authorized by Legal.");
        textContent.AppendLine();
        textContent.AppendLine("Section 3: Regulated Sector Disclaimers and Prohibited Terms");
        textContent.AppendLine("Policy 3.1: Medical and Health Claims.");
        textContent.AppendLine("Avoid unapproved medical or clinical claims. The company does not offer medical cures, and words such as 'miracle cure', 'eliminates disease', or 'clinically proven cure' are strictly forbidden.");
        textContent.AppendLine("Policy 3.2: Financial Promises.");
        textContent.AppendLine("Financial outcomes, get-rich assertions, and guaranteed returns are strictly prohibited.");
        textContent.AppendLine("Policy 3.3: Mandatory Disclaimers.");
        textContent.AppendLine("All promotional offers and metric outcomes must display the standard disclosure: 'Terms and conditions apply. Results may vary.'");

        var text = textContent.ToString();
        return CreateSimplePdf(text);
    }

    public static byte[] CreateSimplePdf(string plainText)
    {
        var lines = plainText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var contentStream = new StringBuilder();
        contentStream.AppendLine("BT");
        contentStream.AppendLine("/F1 11 Tf");
        contentStream.AppendLine("50 750 Td");
        contentStream.AppendLine("14 TL");

        foreach (var line in lines)
        {
            var sanitized = line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                contentStream.AppendLine("T*");
            }
            else
            {
                contentStream.AppendLine($"({sanitized}) '");
            }
        }
        contentStream.AppendLine("ET");

        var streamBytes = Encoding.ASCII.GetBytes(contentStream.ToString());

        var pdf = new StringBuilder();
        pdf.Append("%PDF-1.4\n");

        var offsets = new List<long>();

        // Obj 1: Catalog
        offsets.Add(pdf.Length);
        pdf.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Obj 2: Pages
        offsets.Add(pdf.Length);
        pdf.Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Obj 3: Page
        offsets.Add(pdf.Length);
        pdf.Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n");

        // Obj 4: Stream
        offsets.Add(pdf.Length);
        pdf.Append($"4 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
        pdf.Append(contentStream);
        pdf.Append("\nendstream\nendobj\n");

        // Obj 5: Font
        offsets.Add(pdf.Length);
        pdf.Append("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        // XRef
        var xrefStart = pdf.Length;
        pdf.Append("xref\n0 6\n");
        pdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        // Trailer
        pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\n");
        pdf.Append("startxref\n");
        pdf.Append($"{xrefStart}\n");
        pdf.Append("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}

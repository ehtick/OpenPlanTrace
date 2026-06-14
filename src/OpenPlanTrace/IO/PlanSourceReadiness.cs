using System.Globalization;

namespace OpenPlanTrace;

public sealed record PlanSourceReadiness(
    string Status,
    string GeometryBasis,
    string IngestionPath,
    string? SourceFormat,
    string? Loader,
    string? SourceKind,
    string? EffectiveSourceKind,
    bool IsDwgDerived,
    bool IsRasterDerived,
    bool CanUseVectorGeometry,
    bool RequiresExternalAdapter,
    bool RequiresOcr,
    bool IsLegalAdapterBacked,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Evidence)
{
    public static PlanSourceReadiness From(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var format = ReadProperty(properties, "format")?.Trim().ToLowerInvariant();
        var sourceKind = ReadProperty(properties, "sourceKind") ?? InferSourceKind(format);
        var effectiveSourceKind = ReadProperty(properties, "effectiveSourceKind") ?? sourceKind;
        var isDwgDerived = ComputeIsDwgDerived(properties);
        var isRasterDerived = ComputeIsRasterDerived(properties);
        var hasRasterExtractor = ReadProperty(properties, "raster.extractor") is not null
            || ReadProperty(properties, "extractorName") is not null
            || ReadProperty(properties, "raster.modelName") is not null
            || ReadProperty(properties, "modelName") is not null;
        var pdfImageOnlyPages = ReadInteger(properties, "pdf.imageOnlyPageCount");
        var hasDwgConverter = ReadProperty(properties, "dwg.converter") is not null
            || ReadProperty(properties, "dwg.conversion") is not null;

        var messages = new List<string>();
        var evidence = new List<string>();
        AddEvidence(evidence, "format", ReadProperty(properties, "format"));
        AddEvidence(evidence, "loader", ReadProperty(properties, "loader"));
        AddEvidence(evidence, "sourceKind", ReadProperty(properties, "sourceKind"));
        AddEvidence(evidence, "effectiveSourceKind", ReadProperty(properties, "effectiveSourceKind"));

        if (isDwgDerived)
        {
            AddEvidence(evidence, "dwg.conversion", ReadProperty(properties, "dwg.conversion"));
            AddEvidence(evidence, "dwg.converter", ReadProperty(properties, "dwg.converter"));
            messages.Add(hasDwgConverter
                ? "DWG input was loaded through an adapter/converter path; OpenPlanTrace core did not parse DWG natively."
                : "DWG source metadata is present, but no converter evidence is recorded in the document metadata.");

            return Create(
                hasDwgConverter ? "DwgAdapterBacked" : "DwgAdapterEvidenceMissing",
                hasDwgConverter ? "converted-dxf-vector-geometry" : "dwg-source-metadata",
                properties,
                format,
                sourceKind,
                effectiveSourceKind,
                isDwgDerived,
                isRasterDerived,
                canUseVectorGeometry: hasDwgConverter,
                requiresExternalAdapter: true,
                requiresOcr: false,
                isLegalAdapterBacked: hasDwgConverter,
                messages,
                evidence);
        }

        if (isRasterDerived)
        {
            AddEvidence(evidence, "raster.extractor", ReadProperty(properties, "raster.extractor") ?? ReadProperty(properties, "extractorName"));
            AddEvidence(evidence, "raster.modelName", ReadProperty(properties, "raster.modelName") ?? ReadProperty(properties, "modelName"));
            messages.Add(hasRasterExtractor
                ? "Raster input was loaded through a configured extractor; geometry depends on extractor evidence and confidence."
                : "Raster input requires a real OCR/vectorization extractor before useful geometry can be scanned.");

            return Create(
                hasRasterExtractor ? "RasterExtractorBacked" : "RasterExtractorRequired",
                hasRasterExtractor ? "extracted-raster-primitives" : "raster-image",
                properties,
                format,
                sourceKind,
                effectiveSourceKind,
                isDwgDerived,
                isRasterDerived,
                canUseVectorGeometry: hasRasterExtractor,
                requiresExternalAdapter: !hasRasterExtractor,
                requiresOcr: !hasRasterExtractor,
                isLegalAdapterBacked: hasRasterExtractor,
                messages,
                evidence);
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            AddEvidence(evidence, "pdf.imageOnlyPageCount", ReadProperty(properties, "pdf.imageOnlyPageCount"));
            if (pdfImageOnlyPages > 0)
            {
                messages.Add("PDF contains image-only pages; OCR/raster extraction may be required for those pages.");
                return Create(
                    "PdfRasterOcrRecommended",
                    "mixed-pdf-vector-and-raster",
                    properties,
                    format,
                    sourceKind,
                    effectiveSourceKind,
                    isDwgDerived,
                    isRasterDerived,
                    canUseVectorGeometry: true,
                    requiresExternalAdapter: false,
                    requiresOcr: true,
                    isLegalAdapterBacked: true,
                    messages,
                    evidence);
            }

            messages.Add("PDF vector/text extraction metadata is present.");
            return Create(
                "VectorGeometryReady",
                "pdf-vector-geometry",
                properties,
                format,
                sourceKind,
                effectiveSourceKind,
                isDwgDerived,
                isRasterDerived,
                canUseVectorGeometry: true,
                requiresExternalAdapter: false,
                requiresOcr: false,
                isLegalAdapterBacked: true,
                messages,
                evidence);
        }

        if (string.Equals(format, "dxf", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("DXF vector extraction metadata is present.");
            return Create(
                "VectorGeometryReady",
                "dxf-vector-geometry",
                properties,
                format,
                sourceKind,
                effectiveSourceKind,
                isDwgDerived,
                isRasterDerived,
                canUseVectorGeometry: true,
                requiresExternalAdapter: false,
                requiresOcr: false,
                isLegalAdapterBacked: true,
                messages,
                evidence);
        }

        if (string.Equals(format, "extracted-primitives", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("Document was provided as normalized primitives.");
            return Create(
                "NormalizedPrimitives",
                "pre-extracted-primitives",
                properties,
                format,
                sourceKind,
                effectiveSourceKind,
                isDwgDerived,
                isRasterDerived,
                canUseVectorGeometry: true,
                requiresExternalAdapter: false,
                requiresOcr: false,
                isLegalAdapterBacked: true,
                messages,
                evidence);
        }

        messages.Add("Source metadata is incomplete; downstream consumers should inspect primitive provenance and diagnostics.");
        return Create(
            "Unknown",
            "unknown",
            properties,
            format,
            sourceKind,
            effectiveSourceKind,
            isDwgDerived,
            isRasterDerived,
            canUseVectorGeometry: false,
            requiresExternalAdapter: false,
            requiresOcr: false,
            isLegalAdapterBacked: false,
            messages,
            evidence);
    }

    private static PlanSourceReadiness Create(
        string status,
        string geometryBasis,
        IReadOnlyDictionary<string, string> properties,
        string? format,
        string? sourceKind,
        string? effectiveSourceKind,
        bool isDwgDerived,
        bool isRasterDerived,
        bool canUseVectorGeometry,
        bool requiresExternalAdapter,
        bool requiresOcr,
        bool isLegalAdapterBacked,
        IReadOnlyList<string> messages,
        IReadOnlyList<string> evidence) =>
        new(
            status,
            geometryBasis,
            ComputeIngestionPath(properties),
            format,
            ReadProperty(properties, "loader"),
            sourceKind,
            effectiveSourceKind,
            isDwgDerived,
            isRasterDerived,
            canUseVectorGeometry,
            requiresExternalAdapter,
            requiresOcr,
            isLegalAdapterBacked,
            messages,
            evidence);

    public static string ComputeIngestionPath(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (ReadProperty(properties, "dwg.conversion") is not null)
        {
            return "dwg-to-dxf";
        }

        if (ComputeIsDwgDerived(properties))
        {
            return "dwg-adapter";
        }

        if (ComputeIsRasterDerived(properties))
        {
            return "raster-extraction";
        }

        return ReadProperty(properties, "format")?.Trim().ToLowerInvariant() switch
        {
            "pdf" => "pdf-vector",
            "dxf" => "dxf-vector",
            "extracted-primitives" => "extracted-primitives",
            "vector" or "vector-image" => "vector-image",
            _ => "unknown"
        };
    }

    public static bool ComputeIsDwgDerived(IReadOnlyDictionary<string, string> properties) =>
        string.Equals(ReadProperty(properties, "format"), "dwg", StringComparison.OrdinalIgnoreCase)
        || ReadProperty(properties, "dwg.conversion") is not null
        || ReadProperty(properties, "dwg.converter") is not null;

    public static bool ComputeIsRasterDerived(IReadOnlyDictionary<string, string> properties) =>
        string.Equals(ReadProperty(properties, "format"), "raster", StringComparison.OrdinalIgnoreCase)
        || string.Equals(ReadProperty(properties, "sourceKind"), PlanSourceKind.RasterImage.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(ReadProperty(properties, "effectiveSourceKind"), PlanSourceKind.RasterImage.ToString(), StringComparison.OrdinalIgnoreCase)
        || ReadProperty(properties, "raster.adapter") is not null
        || ReadProperty(properties, "raster.extractor") is not null
        || ReadProperty(properties, "extractorName") is not null;

    private static string? ReadProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        foreach (var pair in properties)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value.Trim();
            }
        }

        return null;
    }

    private static string? InferSourceKind(string? format) =>
        format switch
        {
            "pdf" => PlanSourceKind.Pdf.ToString(),
            "dwg" => PlanSourceKind.Dwg.ToString(),
            "dxf" => PlanSourceKind.Dxf.ToString(),
            "raster" => PlanSourceKind.RasterImage.ToString(),
            "raster-image" => PlanSourceKind.RasterImage.ToString(),
            "vector" => PlanSourceKind.VectorImage.ToString(),
            "vector-image" => PlanSourceKind.VectorImage.ToString(),
            "extracted-primitives" => PlanSourceKind.ExtractedPrimitives.ToString(),
            _ => null
        };

    private static int ReadInteger(IReadOnlyDictionary<string, string> properties, string key) =>
        int.TryParse(ReadProperty(properties, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static void AddEvidence(ICollection<string> evidence, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            evidence.Add($"{key}={value.Trim()}");
        }
    }
}

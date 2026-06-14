namespace OpenPlanTrace.Tests;

public sealed class SourceRoutingTests
{
    [Theory]
    [InlineData("plan.pdf", PlanSourceKind.Pdf)]
    [InlineData("plan.dwg", PlanSourceKind.Dwg)]
    [InlineData("plan.dxf", PlanSourceKind.Dxf)]
    [InlineData("plan.png", PlanSourceKind.RasterImage)]
    public void FromFilePath_InfersSupportedInputKinds(string fileName, PlanSourceKind expectedKind)
    {
        var descriptor = PlanSourceDescriptor.FromFilePath(fileName);

        Assert.Equal(expectedKind, descriptor.Kind);
    }

    [Fact]
    public async Task Registry_RoutesClipboardPdfToPdfLoader()
    {
        var loader = new RecordingLoader("pdf-loader", PlanSourceKind.Pdf);
        var registry = new PlanDocumentLoaderRegistry(new[] { loader });

        await registry.LoadAsync(
            new MemoryStream([1, 2, 3]),
            PlanSourceDescriptor.FromClipboard(PlanSourceKind.Pdf, "clipboard.pdf", "application/pdf"));

        Assert.NotNull(loader.LastSource);
        Assert.Equal(PlanSourceKind.Clipboard, loader.LastSource.Kind);
        Assert.Equal(PlanSourceKind.Pdf, loader.LastSource.EffectiveKind);
    }

    [Fact]
    public async Task Engine_LoadsThenScansThroughSingleFacade()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[] { new RecordingLoader("dwg-loader", PlanSourceKind.Dwg) });
        var engine = new OpenPlanTraceEngine(registry);

        var result = await engine.ScanAsync(
            new MemoryStream([1, 2, 3]),
            PlanSourceDescriptor.FromFileNameOrExtension(".dwg"));

        Assert.Equal("loaded-document", result.Document.Id);
        Assert.Contains(result.SheetRegions, region => region.Kind == RegionKind.Sheet);
    }

    [Fact]
    public async Task Registry_ThrowsWhenDwgLoaderIsNotRegistered()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[] { new RecordingLoader("pdf-loader", PlanSourceKind.Pdf) });

        var exception = await Assert.ThrowsAsync<PlanLoadException>(async () =>
            await registry.LoadAsync(
                new MemoryStream([1, 2, 3]),
                PlanSourceDescriptor.FromFileNameOrExtension(".dwg")));

        Assert.Contains("source kind 'Dwg'", exception.Message);
        Assert.Contains("optional", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("licensed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT core does not include DWG parsing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CapabilityCatalog_ReportsRegisteredAndUnregisteredSourceKinds()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[]
            {
                new RecordingLoader("pdf-loader", PlanSourceKind.Pdf),
                new RecordingLoader("dxf-loader", PlanSourceKind.Dxf)
            });

        var capabilities = registry.GetCapabilities();

        var pdf = Assert.Single(capabilities, item => item.Kind == PlanSourceKind.Pdf);
        Assert.True(pdf.CanLoad);
        Assert.Equal(PlanSourceSupportStatus.Registered, pdf.Status);
        Assert.Contains("pdf-loader", pdf.RegisteredLoaderNames);

        var dxf = Assert.Single(capabilities, item => item.Kind == PlanSourceKind.Dxf);
        Assert.True(dxf.CanLoad);
        Assert.Equal(PlanSourceSupportStatus.Registered, dxf.Status);

        var dwg = Assert.Single(capabilities, item => item.Kind == PlanSourceKind.Dwg);
        Assert.False(dwg.CanLoad);
        Assert.Equal(PlanSourceSupportStatus.OptionalAdapterRequired, dwg.Status);
        Assert.Contains("commercial", dwg.LicenseNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not parsed", dwg.Message, StringComparison.OrdinalIgnoreCase);

        var raster = Assert.Single(capabilities, item => item.Kind == PlanSourceKind.RasterImage);
        Assert.False(raster.CanLoad);
        Assert.Equal(PlanSourceSupportStatus.Planned, raster.Status);
        Assert.Contains("IRasterPlanPrimitiveExtractor", raster.AdapterRequirement, StringComparison.Ordinal);
        Assert.Contains("extractor-backed loader", raster.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not invent", raster.Message, StringComparison.OrdinalIgnoreCase);

        var clipboard = Assert.Single(capabilities, item => item.Kind == PlanSourceKind.Clipboard);
        Assert.Equal(PlanSourceSupportStatus.Wrapper, clipboard.Status);
    }

    [Fact]
    public async Task Registry_UnsupportedRasterMessageIncludesAdapterBoundary()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[] { new RecordingLoader("pdf-loader", PlanSourceKind.Pdf) });

        var exception = await Assert.ThrowsAsync<PlanLoadException>(async () =>
            await registry.LoadAsync(
                new MemoryStream([1, 2, 3]),
                PlanSourceDescriptor.FromFileNameOrExtension(".png")));

        Assert.Contains("Raster scans require a real adapter", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IRasterPlanPrimitiveExtractor", exception.Message, StringComparison.Ordinal);
        Assert.Contains("does not invent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CapabilityCatalog_ReportsDwgRegisteredOnlyWhenDwgLoaderIsRegistered()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[] { new RecordingLoader("licensed-dwg-loader", PlanSourceKind.Dwg) });

        var dwg = registry.GetCapability(PlanSourceDescriptor.FromFileNameOrExtension(".dwg"));

        Assert.True(dwg.CanLoad);
        Assert.Equal(PlanSourceSupportStatus.Registered, dwg.Status);
        Assert.Contains("licensed-dwg-loader", dwg.RegisteredLoaderNames);
    }

    [Fact]
    public void Registry_GetCapability_UsesClipboardEffectiveKind()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[] { new RecordingLoader("pdf-loader", PlanSourceKind.Pdf) });

        var capability = registry.GetCapability(
            PlanSourceDescriptor.FromClipboard(PlanSourceKind.Pdf, "clipboard.pdf", "application/pdf"));

        Assert.Equal(PlanSourceKind.Pdf, capability.Kind);
        Assert.True(capability.CanLoad);
    }

    [Fact]
    public void FromClipboard_RasterContentUsesRasterEffectiveKindAndImageExtension()
    {
        var source = PlanSourceDescriptor.FromClipboard(
            PlanSourceKind.RasterImage,
            "clipboard-image",
            "image/png");

        Assert.Equal(PlanSourceKind.Clipboard, source.Kind);
        Assert.Equal(PlanSourceKind.RasterImage, source.EffectiveKind);
        Assert.Equal(".png", source.FileExtension);
        Assert.Equal("image/png", source.ContentType);
    }

    [Fact]
    public void FromClipboardContent_InfersPdfFromMimeType()
    {
        var source = PlanSourceDescriptor.FromClipboardContent(
            "clipboard-plan.bin",
            "application/pdf");

        Assert.Equal(PlanSourceKind.Clipboard, source.Kind);
        Assert.Equal(PlanSourceKind.Pdf, source.EffectiveKind);
        Assert.Equal(".bin", source.FileExtension);
        Assert.Equal("application/pdf", source.ContentType);
    }

    [Fact]
    public void FromClipboardContent_InfersVectorImageFromParameterizedSvgMimeType()
    {
        var source = PlanSourceDescriptor.FromClipboardContent(
            contentType: "image/svg+xml; charset=utf-8");

        Assert.Equal(PlanSourceKind.Clipboard, source.Kind);
        Assert.Equal(PlanSourceKind.VectorImage, source.EffectiveKind);
        Assert.Equal(".svg", source.FileExtension);
        Assert.Equal("image/svg+xml", source.ContentType);
    }

    [Fact]
    public void FromClipboardContent_FallsBackToNameExtensionWhenMimeTypeIsUnknown()
    {
        var source = PlanSourceDescriptor.FromClipboardContent(
            "clipboard-plan.dxf",
            "application/octet-stream");

        Assert.Equal(PlanSourceKind.Clipboard, source.Kind);
        Assert.Equal(PlanSourceKind.Dxf, source.EffectiveKind);
        Assert.Equal(".dxf", source.FileExtension);
        Assert.Equal("application/octet-stream", source.ContentType);
    }

    [Fact]
    public void ClipboardDwgCapabilityStillRequiresRegisteredLegalAdapter()
    {
        var registry = new PlanDocumentLoaderRegistry(
            new[] { new RecordingLoader("pdf-loader", PlanSourceKind.Pdf) });
        var source = PlanSourceDescriptor.FromClipboardContent(
            "clipboard-plan",
            "image/vnd.dwg");

        var capability = registry.GetCapability(source);

        Assert.Equal(PlanSourceKind.Dwg, source.EffectiveKind);
        Assert.Equal(PlanSourceKind.Dwg, capability.Kind);
        Assert.False(capability.CanLoad);
        Assert.Equal(PlanSourceSupportStatus.OptionalAdapterRequired, capability.Status);
        Assert.Contains("licensed", capability.LicenseNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceReadiness_ReportsPdfVectorGeometryAndEvidence()
    {
        var readiness = PlanSourceReadiness.From(new Dictionary<string, string>
        {
            ["format"] = "pdf",
            ["loader"] = "PDF/PdfPig",
            ["sourceKind"] = "Pdf",
            ["effectiveSourceKind"] = "Pdf",
            ["pdf.imageOnlyPageCount"] = "0"
        });

        Assert.Equal("VectorGeometryReady", readiness.Status);
        Assert.Equal("pdf-vector-geometry", readiness.GeometryBasis);
        Assert.Equal("pdf-vector", readiness.IngestionPath);
        Assert.True(readiness.CanUseVectorGeometry);
        Assert.False(readiness.RequiresExternalAdapter);
        Assert.False(readiness.RequiresOcr);
        Assert.True(readiness.IsLegalAdapterBacked);
        Assert.Contains("format=pdf", readiness.Evidence);
        Assert.Contains("loader=PDF/PdfPig", readiness.Evidence);
    }

    [Fact]
    public void SourceReadiness_DoesNotClaimDwgBackedWithoutConverterEvidence()
    {
        var readiness = PlanSourceReadiness.From(new Dictionary<string, string>
        {
            ["format"] = "dwg",
            ["loader"] = "DWG/Unknown"
        });

        Assert.Equal("DwgAdapterEvidenceMissing", readiness.Status);
        Assert.Equal("dwg-adapter", readiness.IngestionPath);
        Assert.True(readiness.IsDwgDerived);
        Assert.False(readiness.CanUseVectorGeometry);
        Assert.True(readiness.RequiresExternalAdapter);
        Assert.False(readiness.IsLegalAdapterBacked);
        Assert.Contains(readiness.Messages, message => message.Contains("no converter evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SourceReadiness_ReportsDwgConverterBackedWhenEvidenceExists()
    {
        var readiness = PlanSourceReadiness.From(new Dictionary<string, string>
        {
            ["format"] = "dxf",
            ["loader"] = "DWG-to-DXF/TestConverter",
            ["sourceKind"] = "Dwg",
            ["effectiveSourceKind"] = "Dwg",
            ["dwg.conversion"] = "success",
            ["dwg.converter"] = "TestConverter"
        });

        Assert.Equal("DwgAdapterBacked", readiness.Status);
        Assert.Equal("converted-dxf-vector-geometry", readiness.GeometryBasis);
        Assert.Equal("dwg-to-dxf", readiness.IngestionPath);
        Assert.True(readiness.IsDwgDerived);
        Assert.True(readiness.CanUseVectorGeometry);
        Assert.True(readiness.RequiresExternalAdapter);
        Assert.True(readiness.IsLegalAdapterBacked);
        Assert.Contains("dwg.converter=TestConverter", readiness.Evidence);
    }

    private sealed class RecordingLoader : PlanDocumentLoaderBase
    {
        public RecordingLoader(string formatName, params PlanSourceKind[] supportedSourceKinds)
            : base(formatName, supportedSourceKinds)
        {
        }

        public PlanSourceDescriptor? LastSource { get; private set; }

        public override ValueTask<PlanDocument> LoadAsync(
            Stream stream,
            PlanSourceDescriptor source,
            PlanLoadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastSource = source;

            var document = new PlanDocument(
                "loaded-document",
                new[]
                {
                    new PlanPage(
                        1,
                        new PlanSize(200, 100),
                        Array.Empty<PlanPrimitive>())
                });

            return ValueTask.FromResult(document);
        }
    }
}

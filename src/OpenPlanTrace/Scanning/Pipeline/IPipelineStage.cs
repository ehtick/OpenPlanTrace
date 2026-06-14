namespace OpenPlanTrace;

internal interface IPipelineStage
{
    string Name { get; }

    PipelineStageMetadata Metadata => PipelineStageMetadataCatalog.Get(Name);

    ValueTask ExecuteAsync(ScanContext context, CancellationToken cancellationToken);
}

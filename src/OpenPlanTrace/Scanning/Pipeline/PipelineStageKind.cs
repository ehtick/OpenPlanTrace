namespace OpenPlanTrace;

public enum PipelineStageKind
{
    Unknown = 0,
    SourceAnalysis,
    Layout,
    Measurement,
    Geometry,
    Topology,
    Semantics,
    Quality,
    Diagnostics
}

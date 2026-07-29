using System.Reflection;
using System.Text;

namespace OpenPlanTrace.Export;

public static class WallTruthEvaluationJsonSchema
{
    public const string CurrentSchemaVersion = WallTruthEvaluationResult.CurrentSchemaVersion;

    public const string CurrentResourceName =
        "OpenPlanTrace.Export.Schemas.openplantrace.wall-truth-evaluation.v1.schema.json";

    public static string ReadCurrent()
    {
        using var stream = OpenCurrentResource();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static async ValueTask WriteCurrentAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var schemaStream = OpenCurrentResource();
        await schemaStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static Stream OpenCurrentResource()
    {
        var assembly = typeof(WallTruthEvaluationJsonSchema).GetTypeInfo().Assembly;
        return assembly.GetManifestResourceStream(CurrentResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded OpenPlanTrace wall truth evaluation schema resource '{CurrentResourceName}' was not found.");
    }
}

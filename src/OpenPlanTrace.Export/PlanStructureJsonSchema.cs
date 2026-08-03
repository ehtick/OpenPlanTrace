using System.Reflection;
using System.Text;

namespace OpenPlanTrace.Export;

public static class PlanStructureJsonSchema
{
    public const string CurrentSchemaVersion = PlanStructureExport.CurrentSchemaVersion;

    public const string CurrentResourceName =
        "OpenPlanTrace.Export.Schemas.openplantrace.structure.v2.schema.json";

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
        var assembly = typeof(PlanStructureJsonSchema).GetTypeInfo().Assembly;
        return assembly.GetManifestResourceStream(CurrentResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded OpenPlanTrace structure schema resource '{CurrentResourceName}' was not found.");
    }
}

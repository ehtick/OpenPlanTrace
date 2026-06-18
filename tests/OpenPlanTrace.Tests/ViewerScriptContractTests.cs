namespace OpenPlanTrace.Tests;

public sealed class ViewerScriptContractTests
{
    [Fact]
    public void ViewerWalls_TreatTopologyImportBlockedRepairsAsCoordinateBlocked()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "OpenPlanTrace.Viewer",
            "wwwroot",
            "app.js"));

        Assert.Contains("function wallHasTopologyImportBlockedRepair", script);
        Assert.Contains("wallHasTopologyImportBlockedRepair(wall)", script);
        Assert.Contains("wallGraphRepairCandidates", script);
        Assert.Contains("topologyimportblocked", script);
        Assert.Contains("candidate?.wallIds", script);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenPlanTrace.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate OpenPlanTrace repository root.");
    }
}

namespace OrderProcessingPipeline.App;

public static class ReportExporter
{
    public static Result<string> Save(string path, IEnumerable<string> lines)
    {
        var tempPath = path + ".tmp";
        try
        {
            using (var writer = new StreamWriter(tempPath))
            {
                foreach (var line in lines)
                    writer.WriteLine(line);
            }
            File.Move(tempPath, path, overwrite: true);
            return new Success<string>(path);
        }
        catch (UnauthorizedAccessException)
        {
            DeleteTemp(tempPath);
            return new Failure<string>($"No permission to write report '{path}'");
        }
        catch (IOException ex)
        {
            DeleteTemp(tempPath);
            return new Failure<string>($"Could not write report '{path}': {ex.Message}");
        }
    }

    private static void DeleteTemp(string tempPath)
    {
        if (File.Exists(tempPath))
            File.Delete(tempPath);
    }
}

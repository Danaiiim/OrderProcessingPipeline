namespace OrderProcessingPipeline.App;

public static class CsvLoader
{
    public static Result<ParsedInput> Load(string path)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            return OrderParser.Parse(lines).Match<ParsedInput, Result<ParsedInput>>(
                input => new Success<ParsedInput>(input),
                error => new Failure<ParsedInput>($"File '{path}' has malformed content: {error}"));
        }
        catch (FileNotFoundException)
        {
            return new Failure<ParsedInput>($"File '{path}' was not found");
        }
        catch (DirectoryNotFoundException)
        {
            return new Failure<ParsedInput>($"Folder of file '{path}' was not found");
        }
        catch (UnauthorizedAccessException)
        {
            return new Failure<ParsedInput>($"No permission to read file '{path}'");
        }
        catch (IOException ex)
        {
            return new Failure<ParsedInput>($"Could not read file '{path}': {ex.Message}");
        }
    }
}

using System.Collections.Immutable;
using System.Globalization;

namespace OrderProcessingPipeline.App;

public sealed record Courier
{
    public string CourierId { get; }
    public string Name { get; }
    public decimal MaxWeightKg { get; }

    private Courier(string courierId, string name, decimal maxWeightKg) =>
        (CourierId, Name, MaxWeightKg) = (courierId, name, maxWeightKg);

    public static Result<Courier> Create(string? id, string? name, decimal maxWeightKg)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new Failure<Courier>("CourierId is missing");
        if (string.IsNullOrWhiteSpace(name))
            return new Failure<Courier>($"Courier {id.Trim()}: name is missing");
        if (maxWeightKg <= 0m)
            return new Failure<Courier>($"Courier {id.Trim()}: max weight must be greater than 0, got {maxWeightKg}");

        return new Success<Courier>(new Courier(id.Trim(), name.Trim(), maxWeightKg));
    }
}

public static class CourierParser
{
    public const string Header = "CourierId,Name,MaxWeightKg";

    public static Result<ImmutableList<Courier>> Parse(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0 || lines[0].Trim() != Header)
            return new Failure<ImmutableList<Courier>>($"Wrong header. Expected: {Header}");

        var couriers = ImmutableList<Courier>.Empty;
        for (var i = 1; i < lines.Count; i++)
        {
            var line = ParseLine(lines[i], i + 1);
            if (line is Failure<Courier> f)
                return new Failure<ImmutableList<Courier>>(f.Error);
            couriers = couriers.Add(((Success<Courier>)line).Value);
        }
        return new Success<ImmutableList<Courier>>(couriers);
    }

    private static Result<Courier> ParseLine(string line, int lineNumber)
    {
        var fields = line.Split(',').Select(f => f.Trim()).ToArray();
        if (fields.Length != 3)
            return new Failure<Courier>($"Courier line {lineNumber}: expected 3 fields, got {fields.Length}");
        if (!decimal.TryParse(fields[2], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var max))
            return new Failure<Courier>($"Courier line {lineNumber}: MaxWeightKg is not a number: '{fields[2]}'");

        return Courier.Create(fields[0], fields[1], max)
            .Match<Courier, Result<Courier>>(
                c => new Success<Courier>(c),
                e => new Failure<Courier>($"Courier line {lineNumber}: {e}"));
    }

    public static Result<ImmutableList<Courier>> Load(string path)
    {
        try
        {
            return Parse(File.ReadAllLines(path));
        }
        catch (FileNotFoundException)
        {
            return new Failure<ImmutableList<Courier>>($"File '{path}' was not found");
        }
        catch (DirectoryNotFoundException)
        {
            return new Failure<ImmutableList<Courier>>($"Folder of file '{path}' was not found");
        }
        catch (IOException ex)
        {
            return new Failure<ImmutableList<Courier>>($"Could not read file '{path}': {ex.Message}");
        }
    }

    public static string LoadMessage(string path) =>
        Load(path).Match(list => $"Loaded {list.Count} couriers from '{path}'", error => $"LOAD FAILED: {error}");

    public static Maybe<Courier> FindById(IEnumerable<Courier> couriers, string courierId)
    {
        foreach (var courier in couriers)
            if (courier.CourierId == courierId)
                return new Some<Courier>(courier);
        return new None<Courier>();
    }
}

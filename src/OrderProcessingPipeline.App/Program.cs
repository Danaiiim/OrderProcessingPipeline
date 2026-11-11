using OrderProcessingPipeline.App;

var dataFolder = args.Length > 0 ? args[0] : "data";
var settings = new DeliverySettings(MaxDistanceKm: 50m, MaxWeightKg: 30m);
var store = OrderStore.Empty;

Section("1. Load orders");
store = Load(store, Path.Combine(dataFolder, "orders.csv"));

Section("2. Batch workflow");
foreach (var name in new[] { "standard", "express" })
{
    var report = Policies.FromName(name)
        .Map(policy => Pipeline.RunWorkflow(store.Orders, settings, policy))
        .Map(summary => ReportFormatter.FormatSummary(name, summary))
        .Match(text => text, error => $"Policy '{name}' was not run: {error}");
    Console.WriteLine(report);
}

Section("3. Lookup by id");
foreach (var id in new[] { "ORD-010", "ORD-999" })
{
    var text = OrderLookup.FindById(store.Orders, id) switch
    {
        Some<Order> found => $"{id}: found, distance {found.Value.DistanceKm} km, items {found.Value.ItemTotal} KZT",
        None<Order> => $"{id}: not found",
        _ => $"{id}: unknown state"
    };
    Console.WriteLine(text);
}

Section("4. Quote one order");
foreach (var id in new[] { "ORD-002", "ORD-999", "ORD-011", "ORD-004" })
{
    var text = Pipeline.QuoteOrder(store.Orders, id, settings, Policies.Standard)
        .Map(ReportFormatter.FormatReceipt)
        .Match(receipt => $"OK    {receipt}", error => $"FAIL  {error}");
    Console.WriteLine(text);
}

Section("5. Reports (standard policy, zones from native library)");
var quotes = Pipeline.QuoteAll(store.Orders, settings, Policies.Standard, DeliveryZones.GetZone).ToList();
var quoted = quotes.Successes().ToList();
Console.WriteLine($"Quoted: {quoted.Count}, not quoted: {quotes.Errors().Count()}");
foreach (var error in quotes.Errors())
    Console.WriteLine($"  {error}");
Console.WriteLine();

var over1000 = Reports.FeeAtLeast(1000m);
var over3000 = Reports.FeeAtLeast(3000m);
Console.Write(ReportFormatter.FormatExpensive("Delivery fee >= 1000 KZT:", Reports.ExpensiveDeliveries(quoted, over1000)));
Console.Write(ReportFormatter.FormatExpensive("Delivery fee >= 3000 KZT:", Reports.ExpensiveDeliveries(quoted, over3000)));
Console.WriteLine();

var zones = Reports.TotalsByZone(quoted);
Console.Write(ReportFormatter.FormatZones(zones));

Section("6. Export zone report");
Directory.CreateDirectory("output");
foreach (var path in new[] { Path.Combine("output", "zones.csv"), Path.Combine("missing-folder", "zones.csv") })
{
    var text = ReportExporter.Save(path, ReportFormatter.ZonesCsv(zones))
        .Match(saved => $"Saved report to '{saved}'", error => $"EXPORT FAILED: {error}");
    Console.WriteLine(text);
}

Section("7. Couriers and delivery statuses");
var couriers = CourierParser.Load(Path.Combine(dataFolder, "couriers.csv"))
    .Match(list => list, error =>
    {
        Console.WriteLine($"LOAD FAILED: {error}");
        return System.Collections.Immutable.ImmutableList<Courier>.Empty;
    });
Console.WriteLine($"Couriers: {string.Join(", ", couriers.Select(c => $"{c.CourierId} {c.Name} (max {c.MaxWeightKg} kg)"))}");
Console.WriteLine(CourierParser.LoadMessage(Path.Combine(dataFolder, "couriers_bad.csv")));

var board = Dispatch.NewBoard(quoted.Select(q => q.Order));
DispatchCommand[] commands =
{
    new AssignCommand("ORD-002", "C1"),
    new DeliverCommand("ORD-002"),
    new DeliverCommand("ORD-002"),
    new AssignCommand("ORD-003", "C2"),
    new AssignCommand("ORD-003", "C3"),
    new CancelCommand("ORD-012", "client changed address"),
    new AssignCommand("ORD-012", "C1"),
    new AssignCommand("ORD-777", "C1"),
    new AssignCommand("ORD-001", "C9")
};
foreach (var command in commands)
{
    var result = Dispatch.Apply(board, couriers, command);
    Console.WriteLine(result.Match(_ => $"OK    {command}", error => $"FAIL  {command}: {error}"));
    board = result.Match(next => next, _ => board);
}
Console.WriteLine();
foreach (var line in Dispatch.StatusCsv(board).Skip(1))
    Console.WriteLine($"  {line}");
var saved = ReportExporter.Save(Path.Combine("output", "statuses.csv"), Dispatch.StatusCsv(board))
    .Match(path => $"Saved statuses to '{path}'", error => $"SAVE FAILED: {error}");
Console.WriteLine(saved);

Section("8. File failures");
store = Load(store, Path.Combine(dataFolder, "missing.csv"));
store = Load(store, Path.Combine(dataFolder, "malformed.csv"));
Console.WriteLine($"Orders in memory after failures: {store.Orders.Count} (from '{store.Source}')");

static OrderStore Load(OrderStore current, string path)
{
    var loaded = CsvLoader.Load(path);
    var (next, message) = OrderStore.Reload(current, path, loaded);
    Console.WriteLine(message);
    if (loaded is Success<ParsedInput> ok)
        Console.Write(ReportFormatter.FormatInput(ok.Value));
    return next;
}

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine($"=== {title} ===");
}

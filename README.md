Order Processing Pipeline

Final Project - Introduction to Functional Programming

Group IT-2513: Danaiym Kapesova, Saida Amirgalieva, Kaussar Meirambekkyzy, Amina Toleugazinova

Repository

- URL: (add after publishing)
- Branch: main
- Final commit: (add after the last commit)

About the project

This is a console app for a small delivery service of an online shop. It reads orders and couriers from CSV files, checks them, calculates the delivery cost, assigns couriers, tracks the delivery status and makes reports. The users are the shop manager and the delivery team.

Order CSV: OrderId,CustomerTier,DistanceKm,ItemTotal,WeightKg
Courier CSV: CourierId,Name,MaxWeightKg

Rules:
- max delivery distance is 50 km
- max weight of an order is 30 kg
- weight must be more than 0, distance and item total can't be negative
- a courier can't take an order heavier than his MaxWeightKg
- VIP customers get 20% off delivery
- standard policy: 500 KZT + 100 KZT per km + 50 KZT per kg over 2 kg, free if items cost 15000 KZT or more and the distance is 10 km or less
- express policy: 1200 KZT + 200 KZT per km + 100 KZT per kg over 1 kg
- zone: up to 10 km is zone 1, up to 30 km is zone 2, up to 50 km is zone 3 (calculated by the C library)
- status order: Waiting -> OnTheWay -> Delivered, Waiting or OnTheWay can be Cancelled

Domain models

- Order (Models.cs) - one order, created only with Order.Create
- Courier (Couriers.cs) - one courier, created only with Courier.Create
- DeliveryStatus (Dispatch.cs) - Waiting, OnTheWay (with courier), Delivered (with courier), Cancelled (with reason)
- DeliveryEligibility (Models.cs) - ServiceableOrder or UnserviceableOrder
- PricedOrder, OrderReceipt, ReportSummary, QuotedOrder, ExpensiveRow, ZoneRow - results of calculations and reports

Workflows

1. Load orders - read data/orders.csv, check every line, keep good orders, show bad lines with a reason. A missing or broken file gives a clear error and the old orders stay.
2. Find an order - FindById returns Some or None. ORD-010 has distance 0 and items 0 but it is still found.
3. Quote one order - find order -> check weight -> check distance -> calculate fee -> receipt. The first error stops the rest.
4. Price all orders by policy - the same orders with standard and with express give different totals (61880 KZT and 79490 KZT).
5. Dispatch - assign a courier, deliver or cancel an order. Wrong steps are rejected and the board is not changed. Statuses are saved to output/statuses.csv.
6. Zone reports - every order gets its zone from the C library, then two reports are made and the zone report is saved to output/zones.csv.

How to run

Platform: Windows 10/11 x64, .NET SDK 9. Open the folder where OrderProcessingPipeline.sln is and run:

- dotnet build
- dotnet test
- dotnet run --project src/OrderProcessingPipeline.App

The program reads from the data folder and writes to the output folder. The C library is already built in runtime/win-x64/delivery_zone.dll and both .csproj files copy it next to the program on build, so no C compiler is needed to run.

What the program shows:
1. Load orders: 9 valid orders, 4 lines rejected with a reason
2. Batch workflow with standard and express
3. Lookup: ORD-010 found, ORD-999 not found
4. Quote: ORD-002 works, ORD-999, ORD-011 and ORD-004 fail with different errors
5. Reports with zones from the C library
6. Export: output/zones.csv saved, missing-folder/zones.csv fails
7. Couriers and statuses: normal steps work, wrong steps fail, statuses saved
8. File errors: missing.csv and malformed.csv fail and the 9 orders stay

Functional techniques

Pure logic and side effects
All calculations, checks and reports are pure static methods that return new values. Console output is only in Program.cs, printing text is in ReportFormatter.cs and files are read and written only in CsvLoader, CourierParser.Load and ReportExporter. Collections in the core are ImmutableList or ImmutableDictionary.

Pipeline
Pipeline.RunWorkflow has three named stages joined with Pipe: SelectServiceable -> CalculateDelivery -> CreateReceipts.

Higher-order function and policies
CalculateDelivery and Quote take the fee policy as Func<Order, decimal>. Policies.Standard and Policies.Express are the two policies. On the same data ORD-002 costs 1240 KZT with standard and 2720 KZT with express.

Validated construction and states
Order and Courier have private constructors, so they can be made only through Create, which returns Result. DeliveryEligibility has ServiceableOrder and UnserviceableOrder, and PriceServiceable only accepts ServiceableOrder. DeliveryStatus has four cases and Dispatch.Assign, Deliver and Cancel use pattern matching to allow only correct steps. Dispatch.Apply returns a new board on success and a Failure on error, so a wrong command never changes the board.

Result, Map and Bind
Result<T> is Success or Failure (Result.cs). Map is used when the next step can't fail, Bind when it returns a Result itself. Pipeline.QuoteOrder and Dispatch.Apply use both. If a step fails, the next functions are not called.

Domain failures:
- order not found - "Order 'ORD-999' was not found"
- weight over 30 kg - "Order ORD-011: weight 40 kg exceeds limit 30 kg"
- distance over 50 km - "Rejected: Order ORD-004: distance 52 km exceeds 50 km"
- wrong status step - "Cannot deliver: order ORD-002 is Delivered"
- courier can't carry it - "Courier C2 can carry 1 kg, order ORD-003 is 2 kg"
- courier not found - "Courier 'C9' was not found"

Maybe
Maybe<T> is Some or None (Maybe.cs). OrderLookup.FindById and CourierParser.FindById return Maybe. When the value is required, ToResult turns None into a Failure.

Extension methods
ResultListExtensions.Successes and Errors (Extensions.cs) take IEnumerable<Result<T>> and return only the values or only the error messages. They are used in Program.cs to split the quotes.

LINQ reports (Reports.cs)
- ExpensiveDeliveries: Where -> Select -> OrderByDescending by fee -> ThenBy OrderId. With fee >= 1000: ORD-003 5500, ORD-012 2400, ORD-002 1240, ORD-013 1240 (same fee, sorted by id).
- TotalsByZone: GroupBy zone -> Select with Count and Sum -> OrderBy zone. Result: zone 1 - 5 orders 2980 KZT, zone 2 - 1 order 2400 KZT, zone 3 - 1 order 5500 KZT.
Empty input gives an empty report.

Currying
Reports.FeeAtLeast(decimal) returns Func<QuotedOrder, bool>. Program.cs makes over1000 = FeeAtLeast(1000m) and over3000 = FeeAtLeast(3000m). On the same data they give 4 rows and 1 row.

Lazy processing
Pipeline.QuoteAll uses yield return, so nothing is calculated until the list is enumerated. Program.cs calls ToList() right after it, and both reports and the dispatch board use this snapshot, so the C library and the fee policy run once per order.

Files and resources
- CsvLoader.Load and CourierParser.Load use File.ReadAllLines, which opens and closes the file by itself, also on error. FileNotFoundException, DirectoryNotFoundException, UnauthorizedAccessException and IOException become Failure with the file name.
- OrderStore.Reload keeps the old orders if a new load fails.
- ReportExporter.Save owns a StreamWriter in a using block, so it is closed on success and on error. It writes to a .tmp file first and moves it at the end, so a failed export never leaves a half written file.
- Datasets are materialised in File.ReadAllLines, in ImmutableList and with ToList after QuoteAll. There are no caches, events or growing static collections. FeeAtLeast keeps only the limit in its closure.

Native C library

- files: native/delivery_zone.c, native/delivery_zone.h, native/build.bat, runtime/win-x64/delivery_zone.dll
- binding: NativeMethods.DeliveryZone (DllImport, EntryPoint "delivery_zone", Cdecl)
- adapter: DeliveryZones.GetZone returns Result<int>
- C signature: int32_t delivery_zone(int32_t distance_m)
- input: distance in meters, valid 0 to 50000
- output: zone 1, 2 or 3, invalid input returns -1
- example: 25 km = 25000 m -> zone 2
- the adapter checks the distance before calling C and turns DllNotFoundException, EntryPointNotFoundException and BadImageFormatException into Failure
- build: MinGW-w64 GCC 13 x86_64, inside the native folder: gcc -shared -O2 -o ..\runtime\win-x64\delivery_zone.dll delivery_zone.c
- difficulty: at first .NET could not find the DLL because it was not next to the .exe in bin. We added it to both .csproj files with CopyToOutputDirectory.

Tests

xUnit, 41 tests, last run: 41 passed, 0 failed.

- PipelineTests.cs (8): factory, eligibility states, workflow, 50 km boundary, state preservation
- ErrorHandlingTests.cs (12): complete success, 3 domain failures, lookup with zero values, Map, Bind, failure propagation with counters, missing file, malformed file, valid file
- ReportsAndNativeTests.cs (12): both reports with tie, empty and no match, configured filters, extensions, deferred execution, real native call, native failure, two complete workflows, export success and failure
- DispatchTests.cs (9): courier factory, normal status sequence, rejected sequences, too heavy for courier, cancel reason, failed command keeps board, dispatch workflow with saved csv, missing and malformed courier file, courier lookup

File tests use temp paths and delete their files.

Who did what

| Member | Part | Main files |
| --- | --- | --- |
| Saida Amirgalieva | loading and lookup of orders | CsvLoader.cs, OrderParser.cs, OrderStore.cs, OrderLookup.cs, Maybe.cs, Order.Create, data files |
| Danaiym Kapessova | delivery calculation | Pipeline.cs (eligibility, pricing, RunWorkflow, QuoteOrder), Policies.cs, Result.cs |
| Kaussar Meirambekkyzy | couriers and statuses | Couriers.cs, Dispatch.cs, couriers csv files, DispatchTests.cs |
| Amina Toleugazinova | reports, zones and C library | native folder, NativeMethods.cs, DeliveryZones.cs, Extensions.cs, Reports.cs, ReportExporter.cs, ReportFormatter.cs, QuoteAll, ReportsAndNativeTests.cs |

Program.cs is shared: it calls all workflows one by one.

Limitations

- the app is a console demo, commands are written in Program.cs, there is no user input menu
- the C library is built only for Windows x64
- statuses are saved to a file but not loaded back on the next run
namespace OrderProcessingPipeline.App;

public static class DeliveryZones
{
    public static Result<int> GetZone(decimal distanceKm) =>
        GetZone(distanceKm, NativeMethods.DeliveryZone);

    public static Result<int> GetZone(decimal distanceKm, Func<int, int> nativeCall)
    {
        if (distanceKm < 0m || distanceKm > 50m)
            return new Failure<int>($"Distance {distanceKm} km is outside 0-50 km, zone can't be found");

        try
        {
            var zone = nativeCall((int)Math.Round(distanceKm * 1000m));
            return zone is >= 1 and <= 3
                ? new Success<int>(zone)
                : new Failure<int>($"Native library returned unknown zone {zone}");
        }
        catch (DllNotFoundException)
        {
            return new Failure<int>("Native library delivery_zone was not found");
        }
        catch (EntryPointNotFoundException)
        {
            return new Failure<int>("Function delivery_zone was not found in the native library");
        }
        catch (BadImageFormatException)
        {
            return new Failure<int>("Native library delivery_zone has the wrong architecture");
        }
    }
}

using System.Runtime.InteropServices;

namespace OrderProcessingPipeline.App;

internal static class NativeMethods
{
    [DllImport("delivery_zone", EntryPoint = "delivery_zone", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int DeliveryZone(int distanceM);
}

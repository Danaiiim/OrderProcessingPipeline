#include "delivery_zone.h"

int32_t delivery_zone(int32_t distance_m)
{
    if (distance_m < 0 || distance_m > 50000)
        return -1;
    if (distance_m <= 10000)
        return 1;
    if (distance_m <= 30000)
        return 2;
    return 3;
}

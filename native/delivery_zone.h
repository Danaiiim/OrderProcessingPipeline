#ifndef DELIVERY_ZONE_H
#define DELIVERY_ZONE_H

#include <stdint.h>

#ifdef _WIN32
#define DZ_EXPORT __declspec(dllexport)
#else
#define DZ_EXPORT
#endif

DZ_EXPORT int32_t delivery_zone(int32_t distance_m);

#endif

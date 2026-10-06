#ifndef FreeD_h
#define FreeD_h

#ifdef __cplusplus
extern "C"
{
#endif /* __cplusplus */

#include <string.h>
#include <errno.h>
#include <stdint.h>

#define FREE_D_D1_PACKET_SIZE 29

typedef struct
{
    unsigned int ID;        // Camera ID

    float Pan;             // Camera Pan Angle
    float Tilt;            // Camera Tilt Angle
    float Roll;            // Camera Roll Angle
    float X;               // Camera X - Position
    float Y;               // Camera Y - Position
    float Z;               // Camera Height(Z - Position)
    int32_t Zoom;           // Camera Zoom
    int32_t Focus;          // Camera Focus
    unsigned char Spare[2]; // Spare / User Defined(16 bits)

} FreeD_D1_t;

int FreeD_D1_unpack(unsigned char *buf, int len, FreeD_D1_t* dst);
int FreeD_D1_pack(unsigned char *buf, int len, FreeD_D1_t* src);

#ifdef __cplusplus
};
#endif /* __cplusplus */

#endif /* FreeD_h */

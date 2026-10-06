/* Synthetic example for documentation and testing only. */
#ifndef MAPAS_H_
#define MAPAS_H_

#ifdef __cplusplus
extern "C" {
#endif

/* Position, X compensation, Y compensation. */
static const float VetMapXY[][3] = {
    {0.0f, 1.0f, 1.0f},
    {500.0f, 1.0f, 1.0f},
    {1000.0f, 1.0f, 1.0f}
};

/* Position, synthetic height. */
static const float VetMapZ[][2] = {
    {0.0f, 1000.0f},
    {500.0f, 1000.0f},
    {1000.0f, 1000.0f}
};

static const float VetMapaPAN[][2] = {
    {0.0f, 0.0f},
    {1000.0f, 0.0f}
};

static const float VetMapaROLL[][2] = {
    {0.0f, 0.0f},
    {1000.0f, 0.0f}
};

#ifdef __cplusplus
}
#endif
#endif

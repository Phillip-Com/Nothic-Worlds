// Planet surface, Simple style, while standing on it (VISION.md REN-06): the same as its
// usual shader, with the ground's photo materials (GROUND_MATERIALS), which only the ground
// around the eye uses, so the globe views don't carry them. PlanetSurface swaps it in.
shader_type spatial;

#define SIMPLE
#define STYLED
#define GROUND_MATERIALS
#include "res://Rendering/planet_surface.gdshaderinc"

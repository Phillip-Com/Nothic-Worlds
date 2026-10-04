// A planet's rings (VISION.md BOD-03; owner's choice: banded, in the owner's color, with
// shadows). Drawn on a flat ring of radius 1 to 2 in the planet's equatorial plane (its own
// space, in planet radii), stretched to the rings' own radii. The planet, a unit sphere at the
// center, shades the rings behind it.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

#include "res://Rendering/ring_bands.gdshaderinc"

uniform vec3 ring_color : source_color = vec3(0.85, 0.78, 0.66);
uniform float ring_inner = 1.25;
uniform float ring_outer = 2.3;
uniform float ring_seed = 0.0;

// Toward the star, in the rings' own space (unit length).
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);

varying vec3 ring_point;

void vertex() {
	float radius = length(VERTEX.xz);
	VERTEX.xz = VERTEX.xz / radius * mix(ring_inner, ring_outer, radius - 1.0);
	ring_point = VERTEX;
}

void fragment() {
	float radius = length(ring_point.xz);
	float density = ring_density(
		(radius - ring_inner) / (ring_outer - ring_inner), ring_seed);

	// In the planet's shadow if the line toward the star passes through it.
	float along = dot(ring_point, sun_direction);
	float miss = dot(ring_point, ring_point) - along * along;
	bool shadowed = along < 0.0 && miss < 1.0;

	// Lit most when the star stands high over them, dim when it's edge-on.
	float light = mix(0.35, 1.0, abs(sun_direction.y)) * (shadowed ? 0.12 : 1.0);
	ALBEDO = ring_color * (0.75 + 0.35 * density) * light;
	ALPHA = density * 0.85;
}

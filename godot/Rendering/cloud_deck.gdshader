// The clouds below a first-person eye flying above them (VISION.md REN-06; owner's choice:
// clouds seen from above). Drawn on rings of mesh at the cloud layer's height around the
// spot (a FirstPersonGround built at a set radius), see-through where the cloud is thin, so
// the ground shows between the clouds. The same clouds the sky draws overhead from below
// (cloud_layer.gdshaderinc), so climbing through the layer doesn't change them.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://Rendering/cloud_layer.gdshaderinc"

// The light on the clouds' tops: bright by day, warm toward a low sun, dark by night.
uniform vec3 cloud_light = vec3(1.0);

// The scene's units per km, for how far each point is.
uniform float units_per_km = 1.0;

varying vec3 layer_direction;

void vertex() {
	layer_direction = CUSTOM0.xyz;  // The point's direction from the body's middle
}

void fragment() {
	vec4 cloud = cloud_at(normalize(layer_direction), length(VERTEX) / units_per_km);
	ALBEDO = cloud.yzw * cloud_light;
	ALPHA = cloud.x;
}

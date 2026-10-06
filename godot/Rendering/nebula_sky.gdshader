// The sky behind the system (VISION.md BOD-03, REN-07): the image NebulaSky in Core painted,
// looked up by direction, or the plain background without nebulas; and, when shown from orbit,
// the world's designed stars and constellations (star_sky.gdshaderinc). The nebula image's
// layout must match NebulaSky: x runs around from longitude -180° to 180° (longitude 0 toward
// +Z, 90° toward +X), y down from latitude 90° to -90° (+Y up).
shader_type sky;

#include "res://Rendering/star_sky.gdshaderinc"

uniform sampler2D nebula_sky : filter_linear, repeat_enable;
uniform bool has_nebulas = true;
uniform vec3 background_color = vec3(0.02, 0.02, 0.04);  // Screen color, as the background is set

// The image holds screen colors (as picked, and as the plain background is set); the sky works
// in linear light, so convert, or the background would come out lighter than without nebulas.
vec3 to_linear(vec3 color) {
	vec3 low = color / 12.92;
	vec3 high = pow((color + 0.055) / 1.055, vec3(2.4));
	return mix(high, low, vec3(lessThanEqual(color, vec3(0.04045))));
}

void sky() {
	vec3 direction = normalize(EYEDIR);
	vec3 color = to_linear(background_color);
	if (has_nebulas) {
		float latitude = asin(clamp(direction.y, -1.0, 1.0));
		float longitude = atan(direction.x, direction.z);
		color = to_linear(texture(nebula_sky, vec2(longitude / TAU + 0.5, 0.5 - latitude / PI)).rgb);
	}
	COLOR = color + designed_stars(direction, length(fwidth(direction)));
}

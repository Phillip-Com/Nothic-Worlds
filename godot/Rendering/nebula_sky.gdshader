// The sky behind the system when it has nebulas (VISION.md BOD-03): the image NebulaSky in
// Core painted, looked up by direction. Its layout must match NebulaSky: x runs around from
// longitude -180° to 180° (longitude 0 toward +Z, 90° toward +X), y down from latitude 90° to
// -90° (+Y up).
shader_type sky;

uniform sampler2D nebula_sky : filter_linear, repeat_enable;

// The image holds screen colors (as picked, and as the plain background is set); the sky works
// in linear light, so convert, or the background would come out lighter than without nebulas.
vec3 to_linear(vec3 color) {
	vec3 low = color / 12.92;
	vec3 high = pow((color + 0.055) / 1.055, vec3(2.4));
	return mix(high, low, vec3(lessThanEqual(color, vec3(0.04045))));
}

void sky() {
	vec3 direction = normalize(EYEDIR);
	float latitude = asin(clamp(direction.y, -1.0, 1.0));
	float longitude = atan(direction.x, direction.z);
	vec3 painted = texture(nebula_sky, vec2(longitude / TAU + 0.5, 0.5 - latitude / PI)).rgb;
	COLOR = to_linear(painted);
}

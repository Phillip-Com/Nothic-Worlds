// The view from under the water (VISION.md BOD-09), drawn over the whole screen: the scene
// wavers as if seen through moving water and takes on a blue-green tint. The murk itself is
// the scene's fog (SurfaceSky.ShowUnderwater), and the ripples of light are on the bottom
// (planet_surface.gdshaderinc).
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear, repeat_disable;

const vec3 TINT = vec3(0.45, 0.8, 0.82);

void fragment() {
	vec2 uv = SCREEN_UV;
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 place = vec2(uv.x * aspect, uv.y);
	vec2 waver = vec2(sin(place.y * 23.0 + TIME * 1.7), cos(place.x * 19.0 + TIME * 1.3))
		* 0.0035;
	vec3 scene = texture(screen_texture, clamp(uv + waver, 0.0, 1.0)).rgb;
	COLOR = vec4(scene * TINT, 1.0);
}

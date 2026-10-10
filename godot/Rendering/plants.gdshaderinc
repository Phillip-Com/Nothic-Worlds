// Shared by the plant shaders while standing (StandingPlants; VISION.md REN-06): how far a
// plant is from the eye, how it fades in and out with distance (dithered, so no sorting is
// needed), and how it sways in the wind. Plants' meshes are 1 tall with their base at 0
// (PlantModels), so heights in model space run from 0 to 1.

// Meters for each unit of the scene, set every frame.
uniform float meters_per_unit = 1.0;

// Shown from fade_in_start (fully by fade_in_end) to fade_out_start (gone by fade_out_end), in
// meters from the eye. With no fade in, shown from the eye.
uniform float fade_in_start = 0.0;
uniform float fade_in_end = 0.0;
uniform float fade_out_start = 1e9;
uniform float fade_out_end = 2e9;

// Left out where the eye is closer than x to its middle, across (meters), coming in by y: so
// a trunk or a bush the eye stands in isn't seen from inside. (0, 0): never.
uniform vec2 clear_within = vec2(0.0);

// How far a plant's top sways, as a share of its height, and how quickly.
uniform float sway = 0.01;
uniform float sway_speed = 1.0;

varying float plant_fade;

// How far a plant is from the eye, in meters.
float plant_distance(mat4 model, mat4 inv_view) {
	return length(model[3].xyz - inv_view[3].xyz) * meters_per_unit;
}

// How far the eye is across from a plant's middle (its up line), in meters.
float plant_across(mat4 model, mat4 inv_view) {
	vec3 up = normalize(model[1].xyz);
	vec3 to_eye = inv_view[3].xyz - model[3].xyz;
	return length(to_eye - up * dot(to_eye, up)) * meters_per_unit;
}

// How much of a plant shows with the eye this far across from it, 0 to 1.
float plant_clear_at(float across) {
	return clear_within.y > clear_within.x
		? smoothstep(clear_within.x, clear_within.y, across)
		: 1.0;
}

// How much of a plant shows at a distance, 0 to 1.
float plant_fade_at(float meters) {
	float shown_in = fade_in_end > fade_in_start
		? smoothstep(fade_in_start, fade_in_end, meters)
		: 1.0;
	return shown_in * (1.0 - smoothstep(fade_out_start, fade_out_end, meters));
}

// A point of a plant moved by the wind: more the higher it is, each plant in its own time.
vec3 plant_swayed(vec3 point, float phase, float time) {
	float bend = point.y * point.y * sway;
	point.x += sin(time * 1.7 * sway_speed + phase) * bend;
	point.z += sin(time * 1.3 * sway_speed + phase * 1.7) * bend * 0.6;
	return point;
}

// Whether a pixel of a plant part-faded is left out: an even scatter of pixels (interleaved
// gradient noise), more of them the more it has faded.
bool plant_dithered_away(vec2 pixel, float fade) {
	if (fade >= 0.999) {
		return false;
	}
	float threshold = fract(52.9829189 * fract(dot(pixel, vec2(0.06711056, 0.00583715))));
	return fade <= threshold;
}

// Short grass around the feet while standing (StandingPlants; VISION.md REN-06): tufts of thin
// blades (PlantModels.Tuft), seen from both sides, lit as the ground under them (their normals
// point up), darker at the root. Each tuft's color (rgb) and sway phase (a) are in
// INSTANCE_CUSTOM.
shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_disabled;

#include "res://Rendering/plants.gdshaderinc"

varying vec3 tint;

void vertex() {
	tint = INSTANCE_CUSTOM.rgb;
	VERTEX = plant_swayed(VERTEX, INSTANCE_CUSTOM.a * 6.2831853, TIME);
	plant_fade = plant_fade_at(plant_distance(MODEL_MATRIX, INV_VIEW_MATRIX));
}

void fragment() {
	if (plant_dithered_away(FRAGCOORD.xy, plant_fade)) {
		discard;
	}
	ALBEDO = COLOR.rgb * tint;
	ROUGHNESS = 1.0;
}

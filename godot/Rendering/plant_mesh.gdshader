// Trees, bushes, small plants, and stones up close while standing (StandingPlants; VISION.md
// REN-06), drawn as batches (MultiMesh). Each model's flat colors are in its vertices
// (PlantModels); each plant carries a tint (rgb) and a sway phase (a) in INSTANCE_CUSTOM.
shader_type spatial;
render_mode cull_back, diffuse_lambert, specular_disabled;

#include "res://Rendering/plants.gdshaderinc"

varying vec3 tint;

void vertex() {
	tint = INSTANCE_CUSTOM.rgb;
	VERTEX = plant_swayed(VERTEX, INSTANCE_CUSTOM.a * 6.2831853, TIME);
	plant_fade = plant_fade_at(plant_distance(MODEL_MATRIX, INV_VIEW_MATRIX))
		* plant_clear_at(plant_across(MODEL_MATRIX, INV_VIEW_MATRIX));
}

void fragment() {
	if (plant_dithered_away(FRAGCOORD.xy, plant_fade)) {
		discard;
	}
	ALBEDO = COLOR.rgb * tint;
	ROUGHNESS = 0.9;
}

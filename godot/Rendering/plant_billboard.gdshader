// Trees far off while standing (StandingPlants; VISION.md REN-06): flat pictures of them
// (PlantImpostors' sheet), each turned about its own up to face the eye, standing where the
// tree would. The quad runs -0.5 to 0.5 across and 0 to 1 up; the plant's transform scales it
// to the tree's height. INSTANCE_CUSTOM holds its cell in the sheet (r), how much bigger than
// the tree the cell is (g), its brightness (b), and a sway phase (a, unused: too far to see).
shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_disabled;

#include "res://Rendering/plants.gdshaderinc"

uniform sampler2D sheet : source_color, filter_linear_mipmap, repeat_disable;
uniform int columns = 5;
uniform int rows = 4;

varying vec2 sheet_uv;
varying vec3 facing;  // In view space
varying float brightness;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	vec3 up_axis = MODEL_MATRIX[1].xyz;
	float height = length(up_axis);
	vec3 up = up_axis / height;
	vec3 to_eye = INV_VIEW_MATRIX[3].xyz - origin;
	vec3 level = to_eye - up * dot(to_eye, up);
	vec3 toward = length(level) > 1e-12 ? normalize(level) : normalize(MODEL_MATRIX[2].xyz);
	vec3 side = normalize(cross(up, toward));
	float size = height * INSTANCE_CUSTOM.g;
	vec3 world = origin + side * (VERTEX.x * size) + up * (VERTEX.y * size);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(world, 1.0);

	// Rounded, as a crown is: facing the eye in the middle, turning away toward its sides,
	// and a little up.
	vec3 normal = normalize(toward + side * (VERTEX.x * 1.6) + up * 0.35);
	facing = (VIEW_MATRIX * vec4(normal, 0.0)).xyz;

	int cell = int(INSTANCE_CUSTOM.r + 0.5);
	int column = cell % columns;
	int row = cell / columns;
	sheet_uv = vec2((float(column) + VERTEX.x + 0.5) / float(columns),
		(float(row) + 1.0 - VERTEX.y) / float(rows));
	brightness = INSTANCE_CUSTOM.b;
	plant_fade = plant_fade_at(plant_distance(MODEL_MATRIX, INV_VIEW_MATRIX));
}

void fragment() {
	vec4 color = texture(sheet, sheet_uv);
	if (color.a < 0.5 || plant_dithered_away(FRAGCOORD.xy, plant_fade)) {
		discard;
	}
	ALBEDO = color.rgb * brightness;
	NORMAL = normalize(facing);
	ROUGHNESS = 0.9;
}

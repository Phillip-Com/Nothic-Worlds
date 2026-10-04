// The rocks of an asteroid belt (VISION.md BOD-03), drawn as one batch (a MultiMesh) centered on
// its star. Each rock carries its own orbit in INSTANCE_CUSTOM (distance in km, starting angle,
// tilt, and the direction it tilts about) and its look in COLOR (size, two lumpiness factors,
// brightness), so the GPU moves every rock and nothing is updated rock by rock.
shader_type spatial;
render_mode unshaded, cull_back;

uniform vec3 rock_color : source_color = vec3(0.54, 0.5, 0.45);

// The world clock, in standard days, and how long an orbit takes 1 AU from this star (periods
// grow with distance as in real systems, see NewBodies.NaturalPeriodDays).
uniform float time_days = 0.0;
uniform float year_at_au = 365.25;

// How distances are drawn: true scale, or the readable view's compression (see SystemLayout,
// whose numbers these must match).
uniform bool true_scale = false;
uniform float star_radius = 6.5;
uniform float rock_size = 0.1;

const float KM_PER_AU = 149597870.7;
const float KM_PER_UNIT = 6371.0;
const float READABLE_EXPONENT = 0.4;

varying float shade;

void vertex() {
	float distance_km = INSTANCE_CUSTOM.x;
	float period = year_at_au * pow(distance_km / KM_PER_AU, 1.5);
	float angle = INSTANCE_CUSTOM.y + TAU * fract(time_days / period);
	float units = distance_km / KM_PER_UNIT;
	float radius = true_scale ? units : star_radius + pow(units, READABLE_EXPONENT);

	// Counterclockwise seen from the north, as orbits are; then tilted about the rock's own
	// line through the star.
	vec3 flat_spot = vec3(cos(angle), 0.0, -sin(angle)) * radius;
	vec3 axis = vec3(cos(INSTANCE_CUSTOM.w), 0.0, -sin(INSTANCE_CUSTOM.w));
	float tilt = INSTANCE_CUSTOM.z;
	vec3 spot = flat_spot * cos(tilt) + cross(axis, flat_spot) * sin(tilt)
		+ axis * dot(axis, flat_spot) * (1.0 - cos(tilt));

	VERTEX = spot + VERTEX * vec3(COLOR.g, 1.0, COLOR.b) * COLOR.r * rock_size;

	// Lit from the star at the center.
	shade = (0.2 + 0.8 * max(0.0, dot(normalize(NORMAL), -normalize(spot)))) * COLOR.a;
}

void fragment() {
	ALBEDO = rock_color * shade;
}

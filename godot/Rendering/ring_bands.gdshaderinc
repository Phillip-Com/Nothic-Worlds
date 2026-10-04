// How dense a planet's rings are across their width (VISION.md BOD-03), shared by the rings and
// by the planet's surface (for their shadow), so the two always agree. `across` runs from 0 at
// the inner edge to 1 at the outer edge; `seed` gives each planet its own bands.

float ring_hash(float n) {
	return fract(sin(n) * 43758.5453);
}

// Smooth noise along a line, varying over about one unit.
float ring_noise(float x) {
	float whole = floor(x);
	float part = fract(x);
	float eased = part * part * (3.0 - 2.0 * part);
	return mix(ring_hash(whole), ring_hash(whole + 1.0), eased);
}

float ring_density(float across, float seed) {
	if (across <= 0.0 || across >= 1.0) {
		return 0.0;
	}

	// Broad bands with finer ringlets on top.
	float bands = 0.55 * ring_noise(across * 24.0 + seed)
		+ 0.3 * ring_noise(across * 70.0 + seed * 1.7)
		+ 0.15 * ring_noise(across * 160.0 + seed * 2.3);

	// A clear gap a little past halfway out, like Saturn's Cassini Division.
	float gap_at = 0.58 + 0.1 * ring_hash(seed + 0.5);
	float gap = smoothstep(0.0, 0.025, abs(across - gap_at));

	// Soft inner and outer edges.
	float edges = smoothstep(0.0, 0.04, across) * smoothstep(0.0, 0.03, 1.0 - across);
	return clamp(bands * 1.2, 0.05, 1.0) * gap * edges;
}

// A comet's tail (VISION.md EVT-02): a soft glow on a cone, brightest at the head and along
// its middle, fading toward the far end and the edges. Added on top of what's behind it, so
// it needs no sorting and never hides anything.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform vec3 tail_color : source_color = vec3(0.72, 0.88, 1.0);
uniform float brightness = 1.0;

// 0 at the head (the cone's tip, at y = +0.5), 1 at the far end.
varying float from_head;

void vertex() {
    from_head = 0.5 - VERTEX.y;
}

void fragment() {
    // Edges of the cone face sideways to the camera, so they fade out.
    float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
    float fade = pow(1.0 - clamp(from_head, 0.0, 1.0), 1.6);
    ALBEDO = tail_color;
    ALPHA = brightness * fade * facing * facing * facing * 0.6;
}

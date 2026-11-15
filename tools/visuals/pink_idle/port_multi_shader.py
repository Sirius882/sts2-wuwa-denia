"""Port the supplied multi-hit GLSL body to Godot atlas sampling (no ambient overlay)."""
from pathlib import Path
import re
from multi_animator import read_animator
HERE=Path(__file__).resolve().parent
MOD=HERE.parents[2]

def port(form):
    source=HERE/f'source/{form}_animator.js'
    text=source.read_text(encoding='utf-8-sig')
    data=read_animator(source)
    w,h,fw,fh,n=(data[k] for k in ('width','height','fw','fh','count'))
    rows=(n+7)//8
    shader=re.search(r'const (?:FS_SOURCE|FRAG_SRC) = `(.+?)`;',text,re.S)[1]
    shader=shader[shader.index('  vec4 samplePremul' if form=='dark_2hit' else '    void main()'):]
    shader=shader.replace('void main()', 'vec4 render_frame(vec2 vUv)' if form!='dark_2hit' else 'vec4 render_frame(vec2 v_uv)')
    if form!='dark_2hit':
        shader=shader[:shader.index('      if (uShowChecker > 0.5)')]+'''      return vec4(unpm, a);
    }
'''
        for a,b in [('uFrac','fraction'),('uUseFlow','1.0'),('uTexRes','NATIVE_RES')]:shader=shader.replace(a,b)
        for name,index in [('uTex0','frame_index'),('uTex1',f'(frame_index + 1) % {n}')]:
            shader=shader.replace('texture('+name+',','sample_frame('+index+',')
        shader=shader.replace('texture(uFlowTex,','sample_flow(')
    else:
        shader=shader.replace('sampler2D tex','int index').replace('texture(tex,','sample_frame(index,')
        shader=shader.replace('samplePremul(tex,','samplePremul(index,')
        shader=shader.replace('u_tex0','frame_index').replace('u_tex1',f'(frame_index + 1) % {n}')
        shader=shader.replace('texture(u_flow,','sample_flow(')
        for a,b in [('u_texSize','NATIVE_RES'),('u_useFlow','1.0'),('u_alpha','fraction'),('u_flowScale','0.5'),('u_sharpness','1.0')]:shader=shader.replace(a,b)
        shader=shader.replace('outColor = vec4(0.0);\n      return;', 'return vec4(0.0);')
        shader=shader.replace('outColor = vec4(sharpUnitRgb * sdfAlpha, sdfAlpha);','return vec4(sharpUnitRgb, sdfAlpha);')
    header=f'''shader_type canvas_item;
render_mode unshaded;
// Remade {form}: original GLSL interpolation and CAS; no full-canvas ambient stars.
uniform sampler2D frames : source_color, filter_linear, repeat_disable;
uniform sampler2D flow_fields : filter_linear, repeat_disable;
uniform int frame_index = 0;
uniform float fraction = 0.0;
uniform bool flip_h = false;
const vec2 NATIVE_RES = vec2({w}.0, {h}.0);
const vec2 GRID = vec2(8.0, {rows}.0);
vec2 tile(int index) {{ return vec2(float(index % 8), float(index / 8)); }}
vec4 sample_frame(int index, vec2 uv) {{
    vec2 half_texel = 0.5 / NATIVE_RES;
    return texture(frames, (tile(index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}}
vec4 sample_flow(vec2 uv) {{
    vec2 half_texel = 0.5 / vec2({fw}.0, {fh}.0);
    return texture(flow_fields, (tile(frame_index) + clamp(uv, half_texel, 1.0 - half_texel)) / GRID);
}}
'''
    footer='''
void fragment() {
    vec4 tint = COLOR;
    COLOR = render_frame(vec2(flip_h ? 1.0 - UV.x : UV.x, UV.y)) * tint;
}
'''
    (MOD/f'shaders/denia_{form}.gdshader').write_text(header+shader+footer,encoding='utf-8')

if __name__=='__main__':
    for form in ('pink_2hit','dark_2hit','dark_4hit'):port(form)

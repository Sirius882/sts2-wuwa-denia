"""Read the remade multi-hit sources as data; do not execute supplied JavaScript."""
import base64, io, json, re, zlib
from PIL import Image

def read_animator(path):
    text = path.read_text(encoding='utf-8-sig')
    def value(name):
        return json.loads(re.search(r'^  const ' + name + r' = (.+);', text, re.MULTILINE)[1])
    dark_two = 'const STAGE_W' in text
    width, height = (value('STAGE_W'), value('STAGE_H')) if dark_two else (value('CANONICAL_W'), value('CANONICAL_H'))
    count = value('N_FRAMES' if dark_two else 'NUM_FRAMES')
    fw, fh = value('FLOW_W'), value('FLOW_H')
    channels = 2 if dark_two else 4
    fields = zlib.decompress(base64.b64decode(value('FLOW_B64'), validate=True), -15 if dark_two else 15)
    assert len(fields) == count * fw * fh * channels
    rects = value('FRAME_RECTS' if dark_two else 'BBOXES')
    images=[]
    for encoded, rect in zip(value('WEBP_B64'),rects,strict=True):
        crop=Image.open(io.BytesIO(base64.b64decode(encoded,validate=True))).convert('RGBA')
        x,y,w,h=rect
        assert x>=0 and y>=0 and x+w<=width and y+h<=height
        # Source drawImage scales a subset of dark-two attack patches to FRAME_RECTS.
        if crop.size!=(w,h):
            assert dark_two
            crop=crop.resize((w,h),Image.Resampling.LANCZOS)
        image=Image.new('RGBA',(width,height));image.paste(crop,(x,y));images.append(image)
    assert len(images)==count
    return {'width':width,'height':height,'count':count,'fw':fw,'fh':fh,'channels':channels,
            'duration':value('DURATION' if dark_two else 'LOOP_DURATION'),'frames':images,'flow':fields}

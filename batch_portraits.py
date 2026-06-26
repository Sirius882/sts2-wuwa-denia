import json
import os
import re
import shutil

ART_DIR = r"e:\STS2_mod\artResources\Denia\专有卡牌卡面"
DENIA_DIR = r"e:\STS2_mod\myModresources\denia"
SRC_DIR = os.path.join(DENIA_DIR, "src")
PORTRAIT_DIR = os.path.join(DENIA_DIR, "images", "packed", "card_portraits", "denia")
JSON_PATH = os.path.join(DENIA_DIR, "策划", "denia卡牌遗物名称映射表.json")

os.makedirs(PORTRAIT_DIR, exist_ok=True)

# Load JSON
with open(JSON_PATH, "r", encoding="utf-8") as f:
    data = json.load(f)

# List available images
available_images = set(os.listdir(ART_DIR))

# Manual overrides for abbreviated filenames
DISPLAY_NAME_TO_FILENAME = {
    "松针岩绒卷": "松针.png",
    "不要···进来": "不要进来.png",
    "请您不要···": "请您不要.png",
    "请您不要···宽恕我": "请您不要宽恕我.png",
    "乖~": "乖.png",
    "好累，让我歇会……": "好累，让我歇会.png",
    "继续逃啊？": "继续逃啊.png",
    "你也试试？": "你也试试.png",
}

def pascal_to_snake(name):
    """DeniaStrike -> strike, DeniaBackToPink -> back_to_pink"""
    # Remove Denia prefix
    if name.startswith("Denia"):
        name = name[5:]
    # Insert underscore before capitals
    s1 = re.sub(r'(.)([A-Z][a-z]+)', r'\1_\2', name)
    s2 = re.sub(r'([a-z0-9])([A-Z])', r'\1_\2', s1)
    return s2.lower()

# Map internal ID -> (source image filename, target filename)
to_copy = []
updated = 0
skipped = 0

for card in data["卡牌"]:
    cn_name = card["中文名"]
    internal_id = card["内部ID"]
    cs_file = card["源文件"]
    
    # Find the image file
    img_filename = None
    
    # First check manual overrides
    if cn_name in DISPLAY_NAME_TO_FILENAME:
        candidate = DISPLAY_NAME_TO_FILENAME[cn_name]
        if candidate in available_images:
            img_filename = candidate
    
    # Try exact match
    if img_filename is None:
        candidate = f"{cn_name}.png"
        if candidate in available_images:
            img_filename = candidate
    
    # Try without special chars
    if img_filename is None:
        # Strip special punctuation from end
        clean = cn_name.rstrip("~？…·")
        candidate = f"{clean}.png"
        if candidate in available_images:
            img_filename = candidate
    
    if img_filename is None:
        print(f"  SKIP: {cn_name} ({internal_id}) - no image found")
        skipped += 1
        continue
    
    # Target filename: card_face_{snake_case}.png
    target_name = f"card_face_{pascal_to_snake(internal_id)}.png"
    
    # Copy image
    src_path = os.path.join(ART_DIR, img_filename)
    dst_path = os.path.join(PORTRAIT_DIR, target_name)
    if not os.path.exists(dst_path):
        shutil.copy2(src_path, dst_path)
        print(f"  COPY: {img_filename} -> {target_name}")
    
    # Update .cs file
    cs_path = os.path.join(SRC_DIR, cs_file)
    if not os.path.exists(cs_path):
        print(f"  WARN: {cs_path} not found")
        continue
    
    with open(cs_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    new_portrait = f'"res://images/packed/card_portraits/denia/{target_name}"'
    
    # Match both single-line and multi-line PortraitPath patterns
    # Single-line: public override string PortraitPath => "res://...";
    # Multi-line:  public override string PortraitPath =>
    #                  "res://...";
    old_single = r'public override string PortraitPath =>\s*"res://images/packed/card_portraits/denia/[^"]+";'
    old_multi = r'public override string PortraitPath =>\s*\n\s*"res://images/packed/card_portraits/denia/[^"]+";'
    
    new_single = f'public override string PortraitPath => {new_portrait};'
    new_multi = f'public override string PortraitPath =>\n        {new_portrait};'
    
    if re.search(old_multi, content):
        content = re.sub(old_multi, new_multi, content)
        with open(cs_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"  EDIT: {cs_file} -> {target_name}")
        updated += 1
    elif re.search(old_single, content):
        content = re.sub(old_single, new_single, content)
        with open(cs_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"  EDIT: {cs_file} -> {target_name}")
        updated += 1
    else:
        # Check if it already points to the right file
        if new_portrait in content:
            print(f"  OK: {cs_file} already correct")
        else:
            print(f"  WARN: {cs_file} has no PortraitPath override or uses different pattern")

print(f"\nDone: {updated} updated, {skipped} skipped")

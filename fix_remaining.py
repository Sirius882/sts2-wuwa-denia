import os, re

SRC = r"e:\STS2_mod\myModresources\denia\src"
# This script fixes remaining cards that still use card_face.png

files = [
    'DeniaDetermination.cs','DeniaLightCall.cs','DeniaExile.cs','DeniaVirtualParticle.cs',
    'DeniaLongTimeNoSee.cs','DeniaKnockDoor.cs','DeniaSeamless.cs','DeniaCurtainEnd.cs',
    'DeniaConformalEnergy.cs','DeniaEtchedIridescent.cs','DeniaEntropyBoost.cs','DeniaVisit.cs',
    'DeniaUnfinishedLie.cs','DeniaTimedRuin.cs','DeniaBackToFar.cs','DeniaFromFar.cs',
    'DeniaWishSilence.cs','DeniaGetSun.cs','DeniaUntilNextTime.cs','DeniaHappyBirthday.cs',
    'DeniaCrush.cs','DeniaKeepRunning.cs','DeniaBorrowMe.cs','DeniaSoTired.cs',
    'DeniaYouTryIt.cs','DeniaSmelt.cs'
]

def pascal_to_snake(name):
    if name.startswith('Denia'):
        name = name[5:]
    s1 = re.sub(r'(.)([A-Z][a-z]+)', r'\1_\2', name)
    s2 = re.sub(r'([a-z0-9])([A-Z])', r'\1_\2', s1)
    return s2.lower()

updated = 0
for f in files:
    cs_path = os.path.join(SRC, f)
    internal_id = f.replace('.cs', '')
    target = f'card_face_{pascal_to_snake(internal_id)}.png'
    new_portrait = f'"res://images/packed/card_portraits/denia/{target}"'
    
    with open(cs_path, 'r', encoding='utf-8') as fp:
        content = fp.read()
    
    # Match any PortraitPath => "..." (single or multi line)
    old = r'public override string PortraitPath =>\s*(?:\n\s*)?\"res://images/packed/card_portraits/denia/[^\"]+\";'
    
    if new_portrait in content:
        print(f'  OK: {f}')
        continue
    
    if re.search(old, content):
        new_line = f'public override string PortraitPath => {new_portrait};'
        content = re.sub(old, new_line, content)
        with open(cs_path, 'w', encoding='utf-8') as fp:
            fp.write(content)
        print(f'  FIXED: {f} -> {target}')
        updated += 1
    else:
        print(f'  NO MATCH: {f} - checking manually...')
        # Show the PortraitPath line
        for line in content.split('\n'):
            if 'PortraitPath' in line:
                print(f'    {line.strip()}')

print(f'\nUpdated: {updated}')

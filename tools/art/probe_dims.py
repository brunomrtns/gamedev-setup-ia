import bpy, sys, os
from mathutils import Vector
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
import fbx_canonical
fbx_canonical.ensure_fbx_import()
base = 'unity/Assets/Game/Art/Environment/Modular/DowntownMegaKit/Source/Models/'
names = sys.argv[sys.argv.index('--')+1:]
for f in names:
    fbx_canonical.clean_scene()
    try:
        bpy.ops.import_scene.fbx(filepath=base + f + '.fbx')
    except Exception as e:
        print('DIMS', f, 'ERR', e); continue
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
            lo = [min(p[i] for p in bb) for i in range(3)]
            hi = [max(p[i] for p in bb) for i in range(3)]
            print('DIMS', f, [round(hi[i]-lo[i], 2) for i in range(3)],
                  'origin_z', round(lo[2], 2), 'mats', len(o.material_slots))

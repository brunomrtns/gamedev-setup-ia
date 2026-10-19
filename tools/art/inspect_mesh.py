import struct

path = r"assets_vault/weapons/melee/knives_daggers/extracted/Knife Dagger 5.6/MedievalKnives/StaticMeshes/SM_Knife_01.uasset"
with open(path, "rb") as f:
    data = f.read()

print("File size:", len(data))
# At offset 83140 we saw: "Triangles. ...2330. ...Vertices. ...1382. ...UVChannels. ...2"
# Let's find where vertex count 1382 and triangle count 2330 appear as integers in the binary stream!
target_verts = 1382
target_tris = 2330
target_indices = 2330 * 3 # 6990

for i in range(80000, len(data) - 4):
    v = struct.unpack("<I", data[i:i+4])[0]
    if v == target_verts:
        print(f"Found target_verts ({target_verts}) at offset {i} (0x{i:X})")
    elif v == target_indices:
        print(f"Found target_indices ({target_indices}) at offset {i} (0x{i:X})")
    elif v == target_tris:
        print(f"Found target_tris ({target_tris}) at offset {i} (0x{i:X})")

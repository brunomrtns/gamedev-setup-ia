path = 'assets_vault/environments/sci_fi_skyboxes/extracted/Matte Painting Skybox Pack Sci Fi Skies 5.6/Scifi_Skies/Textures/Skies/T_scifiskies_bg_01.uasset'
with open(path, 'rb') as f:
    data = f.read()

print("File len:", len(data))
print("Last 200 bytes:")
print(data[-200:])

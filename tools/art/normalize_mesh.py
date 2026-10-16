#!/usr/bin/env python3
"""
tools/art/normalize_mesh.py
Normalizador canônico de eixos, escalas e pivôs para malhas 3D destinadas à Unity.

Cura os Problemas B e C:
- Alinha a base da malha exatamente em Y = 0 (sola dos pés no chão), resolvendo o desacoplamento com colisores e flutuações anormais.
- Centraliza horizontalmente em X = 0 e Z = 0.
- Normaliza a escala geométrica para a altura canônica do personagem (ex: 1.4m).
- Garante a convenção canônica da Unity: Y-Up, Z-Forward (+Z para a frente da pista).
"""

import os
import sys
import argparse
import trimesh
import numpy as np

def normalize_mesh(input_mesh_path: str, output_mesh_path: str, target_height: float = 1.4, rotate_y_deg: float = 0.0):
    if not os.path.exists(input_mesh_path):
        print(f"[Error] Malha não encontrada: {input_mesh_path}")
        return False
        
    print(f"[normalize_mesh] Carregando: {input_mesh_path}")
    mesh = trimesh.load(input_mesh_path)
    
    # 1. Rotação inicial se solicitada
    if rotate_y_deg != 0.0:
        rad = np.radians(rotate_y_deg)
        rot_mat = trimesh.transformations.rotation_matrix(rad, [0, 1, 0])
        mesh.apply_transform(rot_mat)
        print(f"[normalize_mesh] Rotação de {rotate_y_deg}° em Y aplicada.")

    # 2. Obter limites e dimensões
    bounds = mesh.bounds
    min_pt, max_pt = bounds[0], bounds[1]
    current_height = max_pt[1] - min_pt[1]
    
    if current_height <= 0.0001:
        print("[Error] Malha com altura inválida ou degenerada.")
        return False
        
    print(f"[normalize_mesh] Dimensões originais: X={max_pt[0]-min_pt[0]:.3f}m, Y={current_height:.3f}m, Z={max_pt[2]-min_pt[2]:.3f}m")
    
    # 3. Escala uniforme para target_height
    scale_factor = target_height / current_height
    mesh.apply_scale(scale_factor)
    print(f"[normalize_mesh] Escala aplicada: {scale_factor:.4f}x (Altura resultante: {target_height:.2f}m)")
    
    # 4. Normalização de Pivô:
    # X centralizado em 0
    # Y com base em 0 (pés tocando exatamente Y = 0)
    # Z centralizado em 0
    new_bounds = mesh.bounds
    center_x = (new_bounds[0][0] + new_bounds[1][0]) * 0.5
    min_y = new_bounds[0][1]
    center_z = (new_bounds[0][2] + new_bounds[1][2]) * 0.5
    
    translation = [-center_x, -min_y, -center_z]
    mesh.apply_translation(translation)
    
    final_bounds = mesh.bounds
    print(f"[normalize_mesh] Limites normalizados:")
    print(f"  X: [{final_bounds[0][0]:.3f}, {final_bounds[1][0]:.3f}] (Largura: {final_bounds[1][0]-final_bounds[0][0]:.3f}m)")
    print(f"  Y: [{final_bounds[0][1]:.3f}, {final_bounds[1][1]:.3f}] (Altura: {final_bounds[1][1]-final_bounds[0][1]:.3f}m)")
    print(f"  Z: [{final_bounds[0][2]:.3f}, {final_bounds[1][2]:.3f}] (Profundidade: {final_bounds[1][2]-final_bounds[0][2]:.3f}m)")
    
    # Salvar malha processada
    os.makedirs(os.path.dirname(os.path.abspath(output_mesh_path)), exist_ok=True)
    mesh.export(output_mesh_path)
    print(f"[normalize_mesh] Malha exportada com sucesso em: {output_mesh_path}")
    return True

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Normalizador de malhas 3D para Unity")
    parser.add_argument("--input", required=True, help="Arquivo de malha de entrada (.obj/.glb)")
    parser.add_argument("--output", required=True, help="Arquivo de destino (.obj)")
    parser.add_argument("--target-height", type=float, default=1.4, help="Altura alvo em metros (padrão: 1.4)")
    parser.add_argument("--rotate-y", type=float, default=0.0, help="Rotação em Y em graus se necessário")
    args = parser.parse_args()
    
    ok = normalize_mesh(args.input, args.output, args.target_height, args.rotate_y)
    sys.exit(0 if ok else 1)

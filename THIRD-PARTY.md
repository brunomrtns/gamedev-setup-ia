# THIRD-PARTY.md — Atribuições e Licenças de Código Terceiro Adotado no Studio Core

Este documento registra formalmente a atribuição e a licença de cada projeto open-source (MIT / Apache-2.0) adotado ou adaptado como base de arquitetura no Studio Core (`gamedev-setup-ia`), em conformidade com o **Programa de Adoção (Turno 3)** e `LICENSES-ARTE.md`.

---

## 1. MyMeshy (Base de Arquitetura para `tools/mcp_fab3d/` — P1)
- **Repositório:** `https://github.com/felippeomgt/mymeshy`
- **Licença:** **MIT License**
- **Uso no Studio Core:** Arquitetura de serviço MCP assíncrono com job queue (`generate_asset`, `image_to_3d`, `texture_mesh`, `export_asset`, `get_job`, `wait_for_job`, `list_assets`, `system_status`) e separação por adapters (`RealFab3DAdapter` padrão vs `MockProceduralAdapter` para testes sem GPU), acoplada obrigatoriamente aos gates determinísticos do Studio (`validate_gltf` → `validate_asset` → `validate_visual`).
- **Aviso de Licença (MIT):**
  > Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, subject to inclusion of the copyright and permission notice.

---

## 2. AssetFurnace / image-to-3dlab (Base de Arquitetura para `tools/art/pack_glb_v2.py` — P2)
- **Repositório:** `https://github.com/Bingeljell/image-to-3dlab`
- **Licença:** **Apache License 2.0**
- **Uso no Studio Core:** Conceito de estágio final `pack_glb modo game v2` com geração automática de cadeias LOD (`LOD0`/`LOD1`/`LOD2` por classe), perfis de compressão de textura por plataforma (default `android_urp`), mapeamento de ossos compatível com Unity Humanoid (integrando MoMask/UniRig) e selo de proveniência criptográfico (`sha256`, ferramenta, modelo, licença, timestamp) por arquivo no manifesto.
- **Aviso de Licença (Apache-2.0):**
  > Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. A copy of the License is available at `https://www.apache.org/licenses/LICENSE-2.0`.

---

## 3. CoderGamester/mcp-unity (Ponte Unity MCP Ativa — Rota B)
- **Repositório:** `https://github.com/CoderGamester/mcp-unity`
- **Licença:** **MIT License**
- **Instalação Local (Higiene §1):** `C:\Users\Bruno\tools\mcp-unity` (fora do repositório do Studio Core).

---

## 4. Referências Catalogadas / Pendentes de Adoção Futura
- **mixamo-llm-mocap** (`https://github.com/squall01337/mixamo-llm-mocap`, **MIT**): referência reservada para `validate_anim.py` (P3, adiado até existência de fixtures BVH/FBX do Estágio 5.1-B em `evidence/`).
- **IvanMurzak/Unity-MCP** (`https://github.com/IvanMurzak/Unity-MCP`, **Apache-2.0** + PolyForm Shield 1.0.0) e **AnkleBreaker/unity-mcp-server** (**MIT**): analisados em `docs/mcp-gap-analysis.md` (P4 — somente catálogo, sem adoção/troca nesta etapa).

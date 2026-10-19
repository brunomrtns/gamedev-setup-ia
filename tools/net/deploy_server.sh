#!/usr/bin/env bash
# deploy_server.sh — deploy do NeonDistrict Dedicated Server na VPS.
# Uso (WSL): bash tools/net/deploy_server.sh [build_dir]
# Requer: my-vps configurado (WireGuard+SSH), build Linux Server em builds/.
set -euo pipefail

BUILD_DIR="${1:-$(dirname "$0")/../../builds/NeonDistrict_Server}"
REMOTE_DIR="/opt/neon-district"
PORT=7777

echo "[deploy] build local: $BUILD_DIR"
[ -f "$BUILD_DIR/NeonDistrictServer" ] || { echo "ERRO: binário ausente — rode o build Linux Server antes"; exit 1; }

echo "[deploy] sincronizando → $REMOTE_DIR"
my-vps "sudo mkdir -p $REMOTE_DIR && sudo chown \$USER:\$USER $REMOTE_DIR"
my-vps --rsync "$BUILD_DIR/" "$REMOTE_DIR/"

echo "[deploy] systemd unit"
my-vps "sudo tee /etc/systemd/system/neon-district.service >/dev/null <<'UNIT'
[Unit]
Description=NeonDistrict Dedicated Server
After=network.target

[Service]
WorkingDirectory=$REMOTE_DIR
ExecStart=$REMOTE_DIR/NeonDistrictServer -ndserver -ndport $PORT
Restart=on-failure
RestartSec=5
# limites: não competir com os serviços de produção do host
MemoryMax=2G
CPUQuota=150%
Nice=10

[Install]
WantedBy=multi-user.target
UNIT"

echo "[deploy] firewall: UDP/$PORT"
my-vps "sudo ufw allow $PORT/udp comment 'NeonDistrict game server'"

echo "[deploy] ativando serviço"
my-vps "sudo chmod +x $REMOTE_DIR/NeonDistrictServer && sudo systemctl daemon-reload && sudo systemctl enable --now neon-district"

sleep 3
my-vps "systemctl is-active neon-district && ss -ulnp | grep $PORT || journalctl -u neon-district --no-pager -n 30"
echo "[deploy] concluído — clientes: -ndclient=161.97.133.242:$PORT"

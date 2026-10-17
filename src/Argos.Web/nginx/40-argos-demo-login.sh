#!/bin/sh
# Usuários de demonstração na tela de login (RF-053; D-41; plan 14.10).
# Roda na subida do nginx, pelo /docker-entrypoint.d/ da imagem oficial. Só no ambiente Demo troca a linha do bloco
# DemoLogin do appsettings.json, que vem desligado e vazio no repositório. Fora do Demo não lê as variáveis DEMO_*.
# Nunca escreve logins nem senhas no log.
set -eu

settings=/usr/share/nginx/html/appsettings.json
environment="${ARGOS_ENVIRONMENT:-Production}"

if [ "$environment" != "Demo" ]; then
    echo "argos: ambiente $environment; preenchimento rápido de demonstração desligado."
    exit 0
fi

fail() {
    echo "argos: $1" >&2
    exit 1
}

# Mesmos formatos validados pelo Migrator (D-40); dispensam escapes no JSON e no sed.
check() {
    [ -n "$2" ] || fail "defina $1 no .env."
    printf '%s' "$2" | grep -Eq "^$3\$" || fail "$1 em formato inválido; confira o .env.example."
}

login_pattern='[a-z0-9._-]{3,50}'
password_pattern='[A-Za-z0-9._-]{8,64}'

check DEMO_ADMIN_LOGIN "${DEMO_ADMIN_LOGIN:-}" "$login_pattern"
check DEMO_ADMIN_PASSWORD "${DEMO_ADMIN_PASSWORD:-}" "$password_pattern"
check DEMO_OPERATOR_LOGIN "${DEMO_OPERATOR_LOGIN:-}" "$login_pattern"
check DEMO_OPERATOR_PASSWORD "${DEMO_OPERATOR_PASSWORD:-}" "$password_pattern"
check DEMO_MANAGER_LOGIN "${DEMO_MANAGER_LOGIN:-}" "$login_pattern"
check DEMO_MANAGER_PASSWORD "${DEMO_MANAGER_PASSWORD:-}" "$password_pattern"

disabled='"DemoLogin": { "Enabled": false, "Users": [] }'
grep -qF "$disabled" "$settings" || fail "bloco DemoLogin desligado não encontrado em $settings."

user() {
    printf '{ "Role": "%s", "Login": "%s", "Password": "%s" }' "$1" "$2" "$3"
}

enabled="\"DemoLogin\": { \"Enabled\": true, \"Users\": [ $(user Administrator "$DEMO_ADMIN_LOGIN" "$DEMO_ADMIN_PASSWORD"), $(user Operator "$DEMO_OPERATOR_LOGIN" "$DEMO_OPERATOR_PASSWORD"), $(user Manager "$DEMO_MANAGER_LOGIN" "$DEMO_MANAGER_PASSWORD") ] }"

sed -i "s|\"DemoLogin\": { \"Enabled\": false, \"Users\": \[\] }|$enabled|" "$settings"
grep -qF '"DemoLogin": { "Enabled": true' "$settings" || fail "não foi possível habilitar o bloco DemoLogin em $settings."

# As versões comprimidas da publicação ainda têm o bloco desligado; sem elas, o gzip_static não as serve.
rm -f "$settings.gz" "$settings.br"

echo "argos: usuários de demonstração habilitados: 3."

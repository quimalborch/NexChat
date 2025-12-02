# Script para solucionar problemas de permisos en NexChat
# Ejecutar como Administrador (click derecho ? "Ejecutar como administrador")

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NexChat - Fix de Permisos de Claves" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ruta de la carpeta de claves
$keysPath = "$env:LOCALAPPDATA\NexChat\Keys"
$publicKeysFile = "$keysPath\public_keys.json"

Write-Host "Verificando carpeta de claves..." -ForegroundColor Yellow
Write-Host "Ruta: $keysPath" -ForegroundColor Gray
Write-Host ""

# Crear carpeta si no existe
if (-not (Test-Path $keysPath)) {
    Write-Host "[1/4] Creando carpeta Keys..." -ForegroundColor Yellow
    try {
        New-Item -Path $keysPath -ItemType Directory -Force | Out-Null
        Write-Host "? Carpeta creada exitosamente" -ForegroundColor Green
    }
    catch {
        Write-Host "? Error al crear carpeta: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "[1/4] ? Carpeta Keys ya existe" -ForegroundColor Green
}

# Crear archivo public_keys.json si no existe
Write-Host "[2/4] Verificando archivo public_keys.json..." -ForegroundColor Yellow
if (-not (Test-Path $publicKeysFile)) {
    try {
        Set-Content -Path $publicKeysFile -Value "{}" -Encoding UTF8
        Write-Host "? Archivo public_keys.json creado" -ForegroundColor Green
    }
    catch {
        Write-Host "? Error al crear archivo: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "? Archivo public_keys.json ya existe" -ForegroundColor Green
}

# Establecer permisos correctos
Write-Host "[3/4] Configurando permisos de acceso..." -ForegroundColor Yellow
try {
    # Dar permisos completos al usuario actual
    icacls $keysPath /grant:r "$env:USERNAME:(OI)(CI)F" /T | Out-Null
    Write-Host "? Permisos configurados para usuario: $env:USERNAME" -ForegroundColor Green
}
catch {
    Write-Host "? Advertencia: No se pudieron configurar permisos (puede no ser necesario)" -ForegroundColor Yellow
}

# Verificar permisos
Write-Host "[4/4] Verificando permisos..." -ForegroundColor Yellow
Write-Host ""
Write-Host "Permisos actuales:" -ForegroundColor Cyan
icacls $keysPath

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ? Configuración completada" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Ahora puedes:" -ForegroundColor White
Write-Host "  1. Cerrar este script" -ForegroundColor Gray
Write-Host "  2. Iniciar NexChat" -ForegroundColor Gray
Write-Host "  3. Unirte a un chat remoto" -ForegroundColor Gray
Write-Host "  4. Enviar mensajes cifrados" -ForegroundColor Gray
Write-Host ""
Write-Host "Si sigues teniendo problemas, revisa los logs en:" -ForegroundColor Yellow
Write-Host "  $env:LOCALAPPDATA\NexChat\logs\" -ForegroundColor Gray
Write-Host ""

# Pausar para que el usuario pueda leer
Write-Host "Presiona cualquier tecla para salir..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

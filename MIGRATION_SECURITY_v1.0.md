# Migración de Seguridad - NexChat v1.0.0

## ?? Actualización Importante de Seguridad

NexChat v1.0.0 incluye mejoras críticas de seguridad que protegen tus conversaciones de ataques e interceptación.

---

## ¿Qué ha cambiado?

### ? Cifrado de Extremo a Extremo (E2EE)

**Antes (< v1.0.0)**:
- ? Mensajes enviados en texto plano
- ? Cualquiera podía interceptar y leer tus mensajes
- ? Sin protección contra modificación de mensajes

**Ahora (v1.0.0+)**:
- ? Todos los mensajes cifrados con AES-256-GCM
- ? Solo tú y tu destinatario pueden leer los mensajes
- ? Firmas digitales previenen alteración de mensajes

### ? Validación de Certificados TLS

**Antes (< v1.0.0)**:
- ? Sin validación de certificados
- ? Vulnerable a ataques Man-in-the-Middle (MITM)

**Ahora (v1.0.0+)**:
- ? Validación estricta de certificados
- ? Protección contra interceptación de conexiones
- ? Advertencias si el certificado es inválido

### ? Hashing Seguro de IDs

**Antes (< v1.0.0)**:
- ? SHA256 simple (vulnerable a rainbow tables)
- ? Posible suplantación de identidad

**Ahora (v1.0.0+)**:
- ? SHA256 con salt específico de la aplicación
- ? Protección contra ataques de fuerza bruta
- ? Más difícil suplantar identidades

---

## ¿Necesito Hacer Algo?

### ?? Actualización Automática

1. NexChat te notificará cuando haya una actualización disponible
2. Haz clic en "Descargar actualización"
3. La aplicación se reiniciará automáticamente
4. ¡Listo! Ya estás protegido

### ?? Primera Vez Después de Actualizar

La primera vez que inicies NexChat v1.0.0:

1. **Generación de Claves**:
   - Se generarán automáticamente tus claves de cifrado (RSA-2048)
   - Estas claves se guardan en: `%LocalAppData%\NexChat\Keys\`
   - ?? **NO borres esta carpeta** - perderás tus claves

2. **Intercambio de Claves**:
   - Cuando vuelvas a chatear con alguien, se intercambiarán claves públicas automáticamente
   - Los mensajes antiguos (antes de v1.0.0) permanecen sin cifrar en tu historial
   - Los nuevos mensajes se cifran automáticamente

3. **Chats Existentes**:
   - Tus chats guardados se mantienen intactos
   - Los nuevos mensajes usarán cifrado automáticamente
   - Puede que necesites reiniciar chats remotos para intercambiar claves

---

## ?? Mensajes Antiguos

### Importante: Mensajes Antes de v1.0.0

Los mensajes enviados/recibidos **antes** de actualizar a v1.0.0:
- ? **NO están cifrados** (fueron enviados en texto plano)
- ? Podrían haber sido interceptados en tránsito
- ? Están seguros localmente en tu dispositivo

### Recomendación

Si enviaste información sensible antes de v1.0.0:
1. Considera que esos mensajes pudieron ser interceptados
2. Cambia contraseñas/información crítica si la compartiste
3. Los nuevos mensajes están totalmente cifrados

---

## ?? Gestión de Claves

### Ubicación de las Claves

Tus claves de cifrado se guardan en:
```
%LocalAppData%\NexChat\Keys\
??? private.key    (Tu clave privada - NUNCA compartir)
??? public.key     (Tu clave pública - se comparte automáticamente)
??? public_keys.json (Claves públicas de otros usuarios)
```

### ?? Protección de Claves

**MUY IMPORTANTE**:
- ?? **NUNCA compartas tu `private.key`** con nadie
- ?? **Haz backup** de tu carpeta `Keys` si quieres conservar tus claves
- ?? **Si pierdes `private.key`**, no podrás descifrar mensajes antiguos
- ?? **Si alguien obtiene tu `private.key`**, podrá leer tus mensajes cifrados

### Backup de Claves (Opcional)

Para hacer backup de tus claves:

1. Cierra NexChat
2. Copia la carpeta completa:
   ```
   C:\Users\TuUsuario\AppData\Local\NexChat\Keys
   ```
3. Guárdala en un lugar seguro (USB cifrado, gestor de contraseñas, etc.)
4. **NO subas las claves a la nube sin cifrar**

### Regenerar Claves

Si sospechas que tu clave privada fue comprometida:

1. Elimina la carpeta `%LocalAppData%\NexChat\Keys\`
2. Reinicia NexChat
3. Se generarán nuevas claves automáticamente
4. Necesitarás reintercambiar claves con tus contactos

---

## ??? Verificación de Seguridad

### ¿Cómo Sé que Estoy Protegido?

Después de actualizar a v1.0.0, verifica:

1. **Versión**: Abre NexChat ? Configuración ? Versión debe ser `>= 1.0.0`

2. **Claves Generadas**: Verifica que existan:
   ```
   %LocalAppData%\NexChat\Keys\private.key
   %LocalAppData%\NexChat\Keys\public.key
   ```

3. **Mensajes Cifrados**: En la consola de depuración (si está habilitada), deberías ver:
   ```
   ? Message encrypted successfully
   ? TLS certificate validation configured
   ? Server certificate is valid
   ```

### Verificar Identidad de Contactos

Para asegurarte de que chateas con la persona correcta:

1. **Verifica el código de invitación** por un canal secundario (teléfono, email, etc.)
2. **Primera vez**: El primer mensaje intercambia claves públicas
3. **Mensajes posteriores**: Se verifican automáticamente con firmas digitales

---

## ?? Preguntas Frecuentes

### ¿Mis mensajes antiguos se cifran automáticamente?

No. Solo los nuevos mensajes (después de v1.0.0) se cifran. Los mensajes antiguos permanecen como texto plano en tu historial local.

### ¿Puedo seguir chateando con alguien en versión antigua?

Sí, pero:
- Los mensajes no estarán cifrados
- Recibirás una advertencia
- Recomendamos que actualicen a v1.0.0 para estar protegidos

### ¿Qué pasa si pierdo mis claves?

- No podrás descifrar mensajes cifrados con esas claves
- Los chats nuevos funcionarán normalmente (con claves nuevas)
- Por eso es importante hacer backup

### ¿El cifrado afecta el rendimiento?

No de forma notable. El cifrado es muy rápido:
- Cifrado/descifrado: < 10ms por mensaje típico
- Inicio de chat: 100-200ms para intercambio de claves

### ¿Puedo deshabilitar el cifrado?

No. El cifrado está habilitado por defecto y no puede deshabilitarse. Esto protege tu privacidad automáticamente.

---

## ?? Soporte

Si tienes problemas después de actualizar:

1. **Revisa la documentación**: [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)
2. **Logs**: Habilita logs de depuración en Configuración
3. **Reporta problemas**: [GitHub Issues](https://github.com/quimalborch/NexChat/issues)
4. **Soporte directo**: [@quimalborch](https://github.com/quimalborch)

---

## ? Checklist de Migración

- [ ] Actualizado a NexChat v1.0.0 o superior
- [ ] Claves generadas automáticamente en `%LocalAppData%\NexChat\Keys\`
- [ ] Hice backup de mis claves (opcional pero recomendado)
- [ ] Verifiqué que los chats activos funcionan correctamente
- [ ] Notifiqué a mis contactos para que actualicen también
- [ ] Comprendí que los mensajes antiguos no están cifrados retroactivamente

---

**¡Gracias por actualizar y proteger tu privacidad!** ????

*Si tienes dudas sobre seguridad, consulta [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)*

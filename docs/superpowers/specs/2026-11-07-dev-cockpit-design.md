# dev-cockpit — Diseño

Fecha: 2026-11-07 · Autor: Marcos (decisiones) + Claude (redacción y llamadas marcadas como tales)

## 1. Intención y por qué

Una app de escritorio para el día a día de Marcos como dev, que sirva en **cualquier empresa**
(hoy Tecno Acción, mañana un empleo en NZ/Australia). Junta tres herramientas en un solo lugar,
siempre a mano en la bandeja del sistema:

1. **Bitácora** — registrar lo que hace y convertirlo en daily, semanal y lista de logros.
2. **Entornos** — ver y manejar los stacks de Docker y los puertos locales.
3. **Queries** — entender un `EXPLAIN ANALYZE` de PostgreSQL de un vistazo.

Por qué ahora:
- Quiere un proyecto **de uso real diario**, no otro proyecto de portfolio.
- Le falta registrar logros con números (el CV tiene pendiente desde hace meses "el número real
  de la optimización de PostgreSQL" porque nadie lo anotó a tiempo).
- Elegir **.NET/C#** le suma un stack muy pedido en NZ/Australia (decisión de Marcos).

Éxito = Marcos abre la app todos los días de trabajo y la daily sale de ahí.

## 2. Objetivos, no-objetivos y anti-objetivos

**Objetivos**
- Módulo 1 usable en el trabajo real en ~2 semanas.
- IA opcional y elegible: Ollama (local), Claude o ChatGPT (decisión de Marcos).
- Funciona sin IA: los resúmenes se arman igual, con plantilla.

**No-objetivos (por ahora)**
- Integración con Jira, GitHub, Slack o calendario.
- Funciones de equipo, cuentas, sincronización o backend en la nube.
- Conectarse a bases de datos de la empresa (el módulo 3 trabaja con texto pegado).
- Distribuir la app a terceros (instaladores firmados, auto-update).

**Anti-objetivos (aunque funcione, sería un fracaso si…)**
- Manda código o commits de la empresa a un proveedor en la nube **sin que Marcos lo vea y lo
  confirme**.
- Se convierte en el cuarto proyecto al 90%: los tres módulos a medias y ninguno en uso.
- Hay que abrirla y navegar menús para anotar algo: si anotar tarda más de 5 segundos, no se usa.

## 3. Restricciones

- Máquina de desarrollo: Windows 10, i7-9700, 16 GB RAM, RX 570 (sin aceleración de IA →
  Ollama corre en CPU; usar modelos chicos para resúmenes cortos).
- **No hay SDK de .NET instalado** (solo runtime 6.0). Hay que instalar el **SDK de .NET 10 (LTS)**.
- **Ollama no está instalado.** Hay que instalarlo y bajar un modelo.
- Git: PortableGit en `%LOCALAPPDATA%\Programs\PortableGit\cmd` (no hay git de sistema). La app
  debe permitir configurar la ruta del ejecutable de git.
- Docker Desktop instalado (29.x); `psql` no está, y no hace falta.
- Puede que en la notebook de la empresa no se permita instalar apps u Ollama (**pregunta abierta**).

## 4. Lo decidido

### 4.1 Tecnología
- **C# / .NET 10** (decisión de Marcos).
- **Avalonia** como interfaz (*llamada de Claude*): corre en Windows, Mac y Linux. Si en NZ le dan
  una MacBook, la app sigue sirviendo. Se desarrolla y prueba solo en Windows por ahora.
- MVVM con **CommunityToolkit.Mvvm** (*llamada de Claude*).
- Datos locales en **SQLite** (*llamada de Claude*), en la carpeta de datos del usuario.
- Atajo global con **SharpHook** (*llamada de Claude*).
- Tests con **xUnit** sobre el núcleo (*llamada de Claude*).

### 4.2 Arquitectura (*llamada de Claude*, siguiendo el estilo hexagonal de sus otros proyectos)
```
DevCockpit.Core            dominio + puertos (sin dependencias de UI ni de IO)
DevCockpit.Infrastructure  adaptadores: Git (CLI), Docker (CLI), SQLite, proveedores de IA
DevCockpit.App             Avalonia: vistas, ViewModels, bandeja, atajo global
DevCockpit.Core.Tests      xUnit
```
- Git y Docker se usan **a través de sus CLI** (`git log`, `docker compose ls`, `docker ps`),
  no con librerías: menos dependencias y el mismo comportamiento que en la terminal.
- IA detrás de un puerto `ILlmProvider` con tres adaptadores: Ollama, Anthropic, OpenAI
  (mismo patrón que el `LlmPort` de agent-platform).

### 4.3 Módulo 1 — Bitácora (el primero)
- **Repos observados:** Marcos agrega carpetas; la app lee sus commits **filtrando por su email
  de git**, de todas las ramas locales.
- **Nota rápida:** atajo global (por defecto `Ctrl+Alt+N`, configurable) abre una ventanita
  flotante: escribe, Enter, se cierra. Opcional: marcar la nota como **logro** y agregarle un
  número ("de 4 s a 300 ms").
- **Daily:** borrador "Ayer / Hoy / Bloqueos" con los commits y notas del último día hábil
  (el lunes toma el viernes). Se edita y se copia al portapapeles con un clic.
- **Semanal:** resumen de la semana agrupado por repo y tema.
- **Logros:** lista de notas marcadas como logro, con fecha y número, exportable a Markdown
  para el CV y las evaluaciones.
- **Idioma de salida:** español o inglés, elegible por resumen (inglés para NZ).
- **Sin IA:** los resúmenes se arman con plantilla (commits agrupados por repo + notas).
- **Con IA:** la IA redacta el borrador a partir de lo mismo.

### 4.4 IA y privacidad
- Proveedor elegido en la configuración: **Ollama (por defecto)**, Claude o ChatGPT, o ninguno.
- Claude y ChatGPT requieren **API key propia** (Claude Pro / ChatGPT Plus no la incluyen).
- Las API keys se guardan en el almacén seguro del sistema (Credential Manager en Windows,
  Keychain en Mac), **nunca en texto plano**.
- **Antes de mandar algo a un proveedor en la nube, la app muestra exactamente el texto que va a
  enviar y pide confirmación.** Se puede tildar "no volver a preguntar" por repo.
- Ollama (local) no pide confirmación.

### 4.5 Módulo 2 — Entornos (segundo, cuando el 1 esté en uso)
- Lista de proyectos de Docker Compose con su estado; levantar y frenar con un clic.
- Contenedores con sus puertos publicados.
- **"¿Quién usa este puerto?"**: dado un puerto, muestra el proceso (o contenedor) que lo ocupa.
- Avisa si Docker Desktop no está corriendo, en vez de fallar en silencio.

### 4.6 Módulo 3 — Queries (tercero)
- Se pega la salida de `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`, también se acepta texto plano.
- Árbol de nodos con tiempo y porcentaje del total; resalta el nodo más caro, los `Seq Scan`
  sobre tablas grandes y las estimaciones de filas muy erradas (×10 o más).
- Opcional: pedirle a la IA sugerencias (índices, reescritura), con la misma confirmación de 4.4.
- Sin conexión a bases de datos en esta versión.

### 4.7 Orden de construcción
1. Módulo 1 completo → **una semana de uso real** → ajustes.
2. Recién entonces el módulo 2; después el 3.
Cada módulo cuenta como terminado cuando Marcos lo usa en el trabajo, no cuando pasan los tests.

## 5. Lo que queda a criterio de quien construya
- Diseño visual (tema claro/oscuro siguiendo el sistema).
- Esquema de SQLite y migraciones (EF Core o Dapper).
- Modelo de Ollama por defecto (algo de 3-7B que responda en segundos en CPU).
- Formato exacto de los prompts de resumen.
- Cómo se resuelve "quién usa este puerto" en cada sistema operativo.
- Empaquetado: para uso propio alcanza con `dotnet publish` en un solo ejecutable.

## 6. Preguntas abiertas
- ~~¿Notebook de la empresa o PC personal?~~ Respuesta de Marcos (2026-11-07): "correla de local
  nomás" → se corre en su PC, con IA local (Ollama) como camino principal. Claude/ChatGPT quedan
  como opción, apagados por defecto.
- ¿Hace falta que funcione en Mac/Linux desde el primer día? (Avalonia lo deja posible; hoy
  solo se prueba en Windows.)

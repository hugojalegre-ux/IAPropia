const state = {
  conversacionId: null,
  carpetaId: "principal",
  enviando: false,
  ollamaOk: false,
  modeloConfigurado: "llama3.2",
  internetHabilitada: true,
  healthTimer: null,
  chatsEnlace: [],
  relacionadasSel: new Set(),
  requiereApiKey: false,
  headerApiKey: "X-IAPropia-Key",
  faseEnvio: "generando"
};

const $ = (id) => document.getElementById(id);

const THEME_KEY = "ia-propia-theme";
const API_KEY_STORAGE = "ia-propia-api-key";
const MODO_STORAGE = "ia-propia-modo-default";

const MODOS_INTERACCION = [
  { id: "ask", placeholder: "Preguntá algo directo…" },
  { id: "plan", placeholder: "Describí qué querés lograr; armaré un plan…" },
  { id: "multiplan", placeholder: "Contá el problema; prepararé opciones A / B / C…" },
  { id: "agent", placeholder: "¿Qué tarea querés avanzar? Guío los próximos pasos…" },
  { id: "debug", placeholder: "Describí el problema; mostraré diagnóstico y métricas…" }
];

function formatearDebugMetrics(debug) {
  if (!debug) return "";
  return [
    "[métricas servidor — modo Debug]",
    `duración: ${debug.duracionMs} ms`,
    `modelo: ${debug.modelo} (${debug.proveedor})`,
    `modo pesado: ${debug.modoPesado ? "sí" : "no"}`,
    `chars mensaje / system: ${debug.caracteresMensaje} / ${debug.caracteresSystemAprox}`,
    `fragmentos RAG / web / aprendizajes: ${debug.fragmentosConocimiento} / ${debug.fragmentosInternet} / ${debug.fragmentosAprendizajes}`,
    `conversación: ${debug.conversacionId || "—"}`
  ].join("\n");
}

function appendDebugMetrics(box, debug) {
  if (!debug) return;
  let inner = box.querySelector(".mensajes-inner");
  if (!inner) {
    inner = document.createElement("div");
    inner.className = "mensajes-inner";
    box.appendChild(inner);
  }
  appendBubble(inner, "system", formatearDebugMetrics(debug), "debug-metrics");
}

function getApiKeyGuardada() {
  return localStorage.getItem(API_KEY_STORAGE) || "";
}

function authHeaders(extra = {}) {
  const h = { ...extra };
  const key = getApiKeyGuardada();
  const name = state.headerApiKey || "X-IAPropia-Key";
  if (key) h[name] = key;
  return h;
}

function cargarApiKeyEnCampo() {
  const el = $("api-key-local");
  if (el && !el.value) el.value = getApiKeyGuardada();
}

function modoSeleccionado() {
  const sel = $("modo-select");
  if (sel && sel.offsetParent !== null) return sel.value || "ask";
  return document.querySelector(".modo-btn.active")?.dataset.modo || "ask";
}

function aplicarModoUi(modo) {
  const id = MODOS_INTERACCION.some((m) => m.id === modo) ? modo : "ask";
  document.querySelectorAll(".modo-btn").forEach((btn) => {
    const on = btn.dataset.modo === id;
    btn.classList.toggle("active", on);
    btn.setAttribute("aria-selected", on ? "true" : "false");
  });
  const sel = $("modo-select");
  if (sel && sel.value !== id) sel.value = id;
  const meta = MODOS_INTERACCION.find((m) => m.id === id) || MODOS_INTERACCION[0];
  const input = $("input");
  if (input && meta.placeholder) input.placeholder = meta.placeholder;
}

function initModoBar() {
  aplicarModoUi(localStorage.getItem(MODO_STORAGE) || "ask");
  document.querySelectorAll(".modo-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      aplicarModoUi(btn.dataset.modo);
      localStorage.setItem(MODO_STORAGE, modoSeleccionado());
    });
  });
  $("modo-select")?.addEventListener("change", () => {
    aplicarModoUi($("modo-select").value);
    localStorage.setItem(MODO_STORAGE, modoSeleccionado());
  });
}

function renderAssistantContent(el, text, useMarkdown) {
  if (!el) return;
  const raw = text || "";
  if (useMarkdown && window.SiltexMarkdown?.render) {
    el.classList.add("md-on");
    el.innerHTML = window.SiltexMarkdown.render(raw);
  } else {
    el.classList.remove("md-on");
    el.textContent = raw;
  }
}

function setSidebarAbierto(abierto) {
  document.body.classList.toggle("sidebar-open", abierto);
  const bd = $("sidebar-backdrop");
  if (bd) {
    bd.classList.toggle("hidden", !abierto);
    bd.setAttribute("aria-hidden", abierto ? "false" : "true");
  }
}

function initSidebarMobile() {
  $("btn-sidebar")?.addEventListener("click", () => setSidebarAbierto(true));
  $("sidebar-backdrop")?.addEventListener("click", () => setSidebarAbierto(false));
  document.addEventListener("keydown", (ev) => {
    if (ev.key === "Escape") setSidebarAbierto(false);
  });
}

function guardarApiKeyLocal() {
  const el = $("api-key-local");
  if (!el) return;
  const v = el.value.trim();
  if (v) localStorage.setItem(API_KEY_STORAGE, v);
  else localStorage.removeItem(API_KEY_STORAGE);
  alert(v ? "Clave guardada en este navegador (localStorage)." : "Clave eliminada de este navegador.");
}

function temaResuelto(mode) {
  if (mode === "dark") return "dark";
  if (mode === "light") return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function aplicarTema(mode) {
  const m = mode || localStorage.getItem(THEME_KEY) || "system";
  localStorage.setItem(THEME_KEY, m);
  const resolved = temaResuelto(m);
  document.documentElement.setAttribute("data-theme", resolved);
  document.documentElement.setAttribute("data-theme-mode", m);
  document.querySelectorAll(".theme-btn").forEach((btn) => {
    btn.classList.toggle("active", btn.dataset.theme === m);
    btn.setAttribute("aria-pressed", btn.dataset.theme === m ? "true" : "false");
  });
}

function initTema() {
  aplicarTema(localStorage.getItem(THEME_KEY) || "system");
  window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
    const mode = localStorage.getItem(THEME_KEY) || "system";
    if (mode === "system") aplicarTema("system");
  });
  document.querySelectorAll(".theme-btn").forEach((btn) => {
    btn.addEventListener("click", () => aplicarTema(btn.dataset.theme));
  });
}

async function api(path, opts = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json", ...authHeaders(opts.headers || {}) },
    ...opts
  });
  const data = await res.json().catch(() => ({}));
  if (res.status === 401) {
    throw new Error(
      data.error ||
        "Falta API key. Configurala en Ajustes del modelo (docs/SEGURIDAD.md en el repo IAPropia)."
    );
  }
  if (!res.ok && data.error) throw new Error(data.error);
  return data;
}

function parseRelacionadas(str) {
  return (str || "")
    .split(/[,;\s]+/)
    .map((s) => s.trim())
    .filter(Boolean);
}

function relacionadasIds() {
  const manual = parseRelacionadas($("relacionadas").value);
  const ids = new Set([...state.relacionadasSel, ...manual]);
  if (state.conversacionId) ids.delete(state.conversacionId);
  return [...ids];
}

function setRelacionadasFromDoc(ids) {
  state.relacionadasSel = new Set(ids || []);
  const conocidos = new Set(state.chatsEnlace.map((c) => c.id));
  const manual = (ids || []).filter((id) => !conocidos.has(id));
  $("relacionadas").value = manual.join(", ");
  renderListaEnlaces();
}

function appendBubble(box, role, content, variant) {
  const row = document.createElement("div");
  row.className = "msg-row " + (role === "user" ? "user" : role === "assistant" ? "assistant" : "system");
  if (role === "user" || role === "assistant") {
    const label = document.createElement("span");
    label.className = "msg-label";
    label.textContent = role === "user" ? "Vos" : "Asistente";
    row.appendChild(label);
  }
  const div = document.createElement("div");
  div.className = "bubble " + (variant || role);
  if (role === "assistant") renderAssistantContent(div, content, true);
  else div.textContent = content;
  row.appendChild(div);
  box.appendChild(row);
}

function renderMensajes(mensajes) {
  const box = $("mensajes");
  box.innerHTML = "";
  if (!mensajes?.length) {
    box.innerHTML = `
      <div class="welcome-card">
        <h2>¿En qué te ayudo?</h2>
        <p>Respondo con el modelo de tu PC (Ollama). Tus charlas y aprendizajes quedan guardados en esta carpeta.</p>
        <ul>
          <li>Elegí una <strong>carpeta</strong> a la izquierda para organizar temas.</li>
          <li>Opcional: activá <strong>buscar en internet</strong> en Ajustes del modelo.</li>
          <li>Después podés <strong>guardar un aprendizaje</strong> desde «Organizar charla».</li>
        </ul>
      </div>`;
    return;
  }
  const inner = document.createElement("div");
  inner.className = "mensajes-inner";
  box.appendChild(inner);
  for (const m of mensajes) {
    appendBubble(inner, m.role === "user" ? "user" : "assistant", m.content, m.role);
  }
  box.scrollTop = box.scrollHeight;
}

function modeloSeleccionado() {
  const custom = $("modelo-custom")?.value?.trim();
  if (custom) return custom;
  return $("modelo-select")?.value?.trim() || null;
}

function actualizarBotonEnviar() {
  const btn = $("btn-enviar");
  const forzar = $("forzar-envio").checked;
  const bloquear = !state.ollamaOk && !forzar;
  btn.disabled = state.enviando || bloquear;
  if (state.enviando) {
    btn.textContent = state.faseEnvio === "preparando" ? "Preparando…" : "Generando…";
  } else {
    btn.textContent = "Enviar";
  }
  btn.title = bloquear
    ? "Ollama no está listo. Revisá el pie o activá envío sin preflight."
    : state.enviando
      ? "Ollama puede tardar varios minutos en modelos grandes."
      : "";
}

function parseSseBlock(block) {
  const lines = block.split("\n");
  let event = "message";
  let dataLine = "";
  for (const line of lines) {
    if (line.startsWith("event:")) event = line.slice(6).trim();
    else if (line.startsWith("data:")) dataLine += line.slice(5).trim();
  }
  if (!dataLine) return null;
  try {
    return { event, data: JSON.parse(dataLine) };
  } catch {
    return null;
  }
}

async function consumirChatStream(body, onDelta, onStatus) {
  const res = await fetch("/api/chat/stream", {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(body)
  });
  if (!res.ok) {
    const data = await res.json().catch(() => ({}));
    if (res.status === 401) {
      throw new Error(data.error || "Falta API key (Ajustes del modelo).");
    }
    throw new Error(data.error || `HTTP ${res.status}`);
  }
  if (!res.body) throw new Error("El navegador no soporta streaming de respuesta.");

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buf = "";
  let donePayload = null;
  let streamError = null;
  let statusMostrado = false;

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buf += decoder.decode(value, { stream: true });
    const parts = buf.split("\n\n");
    buf = parts.pop() || "";
    for (const block of parts) {
      const ev = parseSseBlock(block);
      if (!ev) continue;
      if (ev.event === "status" && ev.data) {
        if (ev.data.phase) state.faseEnvio = ev.data.phase;
        if (onStatus) onStatus(ev.data);
        statusMostrado = true;
        actualizarBotonEnviar();
      }
      if (ev.event === "token" && ev.data?.delta) {
        state.faseEnvio = "generando";
        actualizarBotonEnviar();
        onDelta(ev.data.delta, { clearStatus: statusMostrado });
        statusMostrado = false;
      }
      if (ev.event === "done") donePayload = ev.data;
      if (ev.event === "error") streamError = ev.data?.error || "Error en stream";
    }
  }
  if (streamError) throw new Error(streamError);
  if (!donePayload?.ok) throw new Error(donePayload?.error || "Stream sin cierre");
  return donePayload;
}

function cuerpoChat(texto, tituloManual) {
  return {
    conversacionId: state.conversacionId,
    personaId: $("persona").value,
    proveedor: "ollama",
    modelo: modeloSeleccionado(),
    titulo: tituloManual || undefined,
    carpetaId: $("carpeta").value || state.carpetaId,
    relacionadasIds: relacionadasIds(),
    mensaje: texto,
    usarConocimientoLocal: $("usar-rag").checked,
    usarBusquedaInternet: $("usar-internet").checked && state.internetHabilitada,
    modoPesado: $("modo-pesado").checked,
    modoInteraccion: modoSeleccionado()
  };
}

function rellenarModelosOllama(health, estado) {
  const sel = $("modelo-select");
  sel.innerHTML = "";
  const cfg = health?.modeloConfigurado || estado?.ollama?.modeloConfigurado || state.modeloConfigurado;
  state.modeloConfigurado = cfg;
  const modelos = health?.modelosDisponibles?.length
    ? health.modelosDisponibles
    : estado?.ollama?.modelosDisponibles || [];

  if (modelos.length === 0) {
    const opt = document.createElement("option");
    opt.value = cfg;
    opt.textContent = cfg + " (configurado)";
    sel.appendChild(opt);
  } else {
    for (const m of modelos) {
      const opt = document.createElement("option");
      opt.value = m;
      opt.textContent = m;
      sel.appendChild(opt);
    }
    const match = [...sel.options].find(
      (o) => o.value === cfg || o.value.startsWith(cfg + ":") || o.value.split(":")[0] === cfg
    );
    if (match) sel.value = match.value;
    else if (cfg) {
      const opt = document.createElement("option");
      opt.value = cfg;
      opt.textContent = cfg + " (falta pull)";
      sel.insertBefore(opt, sel.firstChild);
      sel.value = cfg;
    }
  }
}

function rellenarCarpetas(carpetas) {
  const sel = $("carpeta");
  const prev = state.carpetaId;
  sel.innerHTML = "";
  for (const c of carpetas || []) {
    const opt = document.createElement("option");
    opt.value = c.id;
    opt.textContent = c.nombre || c.id;
    sel.appendChild(opt);
  }
  if ([...sel.options].some((o) => o.value === prev)) sel.value = prev;
  else if (sel.options.length) {
    sel.selectedIndex = 0;
    state.carpetaId = sel.value;
  }
}

function aplicarMarcaDesdeEstado(est) {
  const nombre = est?.nombre?.trim();
  if (nombre) {
    const h1 = $("brand-title");
    if (h1) h1.textContent = nombre;
    document.title = nombre;
  }
}

function pintarFooterEstado(est, ollamaHealth) {
  aplicarMarcaDesdeEstado(est);
  const el = $("estado-servicio");
  const ver = est?.version ? `v${est.version}` : "";
  state.internetHabilitada = !!est?.internetBusquedaHabilitada;
  $("usar-internet").disabled = !state.internetHabilitada;

  if (ollamaHealth) {
    state.ollamaOk = !!ollamaHealth.ok;
    const punto = ollamaHealth.ok ? "●" : "○";
    const corto = ollamaHealth.ok ? "Ollama listo" : "Ollama: revisá el modelo";
    el.textContent = [ver, `${punto} ${corto}`].filter(Boolean).join(" · ");
    el.title = ollamaHealth.mensaje || "";
    el.className = "estado estado-pill " + (ollamaHealth.ok ? "ok" : "warn");
  } else {
    el.textContent = [ver, "○ Sin datos de Ollama"].filter(Boolean).join(" · ");
    el.className = "estado estado-pill warn";
  }
  actualizarBotonEnviar();
}

async function sincronizarSeguridadDesdeHealth() {
  const res = await fetch("/api/health");
  const h = await res.json().catch(() => ({}));
  if (h.requiereApiKey != null) state.requiereApiKey = !!h.requiereApiKey;
  if (h.headerApiKey) state.headerApiKey = h.headerApiKey;
}

async function refrescarHealth() {
  try {
    await sincronizarSeguridadDesdeHealth();
    const [est, ollamaHealth] = await Promise.all([api("/api/estado"), api("/api/health/ollama")]);
    rellenarModelosOllama(ollamaHealth, est);
    rellenarCarpetas(est.carpetas);
    pintarFooterEstado(est, ollamaHealth);
    return { est, ollamaHealth };
  } catch {
    $("estado-servicio").textContent = "No se pudo consultar salud del servidor.";
    $("estado-servicio").className = "estado warn";
    state.ollamaOk = false;
    actualizarBotonEnviar();
    return null;
  }
}

async function cargarEstado() {
  const pack = await refrescarHealth();
  if (!pack) return;
  const est = pack.est;
  const sel = $("persona");
  sel.innerHTML = "";
  for (const p of est.personas || []) {
    const opt = document.createElement("option");
    opt.value = p.id;
    opt.textContent = p.nombre;
    sel.appendChild(opt);
  }
}

async function cargarChatsParaEnlace() {
  const todas = $("enlace-todas-carpetas").checked;
  const url = todas
    ? "/api/conversaciones"
    : "/api/conversaciones?carpetaId=" + encodeURIComponent($("carpeta").value || state.carpetaId);
  state.chatsEnlace = await api(url);
  renderListaEnlaces();
}

function renderListaEnlaces() {
  const box = $("lista-enlaces");
  box.innerHTML = "";
  const items = (state.chatsEnlace || []).filter((c) => c.id !== state.conversacionId);
  if (items.length === 0) {
    box.innerHTML =
      '<p class="hint">Acá aparecerán tus otras charlas. Si no ves ninguna, marcá «todas las carpetas» o empezá otro hilo.</p>';
    return;
  }
  for (const c of items) {
    const label = document.createElement("label");
    label.className = "enlace-item";
    const cb = document.createElement("input");
    cb.type = "checkbox";
    cb.value = c.id;
    cb.checked = state.relacionadasSel.has(c.id);
    cb.addEventListener("change", () => {
      if (cb.checked) state.relacionadasSel.add(c.id);
      else state.relacionadasSel.delete(c.id);
    });
    const text = document.createElement("span");
    text.className = "enlace-titulo";
    text.textContent = c.titulo || c.id.slice(0, 10);
    const badge = document.createElement("span");
    badge.className = "enlace-badge";
    badge.textContent = c.carpetaId || "";
    label.appendChild(cb);
    label.appendChild(text);
    label.appendChild(badge);
    box.appendChild(label);
  }
}

async function cargarConversaciones() {
  const carpetaId = $("carpeta").value || state.carpetaId;
  state.carpetaId = carpetaId;
  const list = await api("/api/conversaciones?carpetaId=" + encodeURIComponent(carpetaId));
  const ul = $("lista-conversaciones");
  ul.innerHTML = "";
  for (const c of list) {
    const li = document.createElement("li");
    const btn = document.createElement("button");
    btn.type = "button";
    btn.textContent = c.titulo || c.id.slice(0, 8);
    btn.title = c.id;
    btn.classList.toggle("active", c.id === state.conversacionId);
    btn.onclick = () => abrirConversacion(c.id);
    li.appendChild(btn);
    ul.appendChild(li);
  }
  await cargarChatsParaEnlace();
}

function truncarTexto(s, max) {
  if (!s) return "";
  return s.length <= max ? s : s.slice(0, max) + "…";
}

async function cargarAprendizajes() {
  const carpetaId = $("carpeta").value || state.carpetaId;
  const list = await api("/api/aprendizajes?carpetaId=" + encodeURIComponent(carpetaId));
  $("aprendizajes-count").textContent = list.length ? `(${list.length})` : "";
  const ul = $("lista-aprendizajes");
  ul.innerHTML = "";
  if (!list.length) {
    ul.innerHTML =
      '<li class="apr-empty">Todavía no guardaste aprendizajes aquí. Usá «Guardar aprendizaje» cuando resuelvas algo que quieras repetir.</li>';
    return;
  }
  for (const a of list) {
    const li = document.createElement("li");
    li.className = "apr-item";
    const head = document.createElement("div");
    head.className = "apr-head";
    const titleBtn = document.createElement("button");
    titleBtn.type = "button";
    titleBtn.className = "apr-titulo";
    titleBtn.textContent = a.titulo;
    titleBtn.title = a.id;
    const body = document.createElement("div");
    body.className = "apr-cuerpo hidden";
    body.textContent = a.contenido;
    titleBtn.onclick = () => body.classList.toggle("hidden");
    const del = document.createElement("button");
    del.type = "button";
    del.className = "apr-del";
    del.textContent = "×";
    del.title = "Eliminar aprendizaje";
    del.onclick = async (ev) => {
      ev.stopPropagation();
      if (!confirm(`¿Eliminar «${a.titulo}»?`)) return;
      await api("/api/aprendizajes/" + encodeURIComponent(a.id), { method: "DELETE" });
      await cargarAprendizajes();
    };
    head.appendChild(titleBtn);
    head.appendChild(del);
    const preview = document.createElement("p");
    preview.className = "apr-preview";
    preview.textContent = truncarTexto(a.contenido, 72);
    li.appendChild(head);
    li.appendChild(preview);
    li.appendChild(body);
    ul.appendChild(li);
  }
}

async function abrirConversacion(id) {
  if (window.matchMedia("(max-width: 768px)").matches) setSidebarAbierto(false);
  state.conversacionId = id;
  const doc = await api("/api/conversaciones/" + encodeURIComponent(id));
  $("titulo-chat").value = doc.titulo || "";
  setRelacionadasFromDoc(doc.relacionadasIds || []);
  if (doc.carpetaId) {
    state.carpetaId = doc.carpetaId;
    if ([...$("carpeta").options].some((o) => o.value === doc.carpetaId)) $("carpeta").value = doc.carpetaId;
  }
  const modo = doc.modoInteraccion || "ask";
  aplicarModoUi(modo);
  localStorage.setItem(MODO_STORAGE, modo);
  renderMensajes(doc.mensajes);
  await cargarConversaciones();
  await cargarAprendizajes();
}

function nuevaConversacion() {
  state.conversacionId = null;
  $("titulo-chat").value = "";
  state.relacionadasSel = new Set();
  $("relacionadas").value = "";
  aplicarModoUi(localStorage.getItem(MODO_STORAGE) || "ask");
  renderMensajes([]);
  cargarConversaciones();
  renderListaEnlaces();
}

async function guardarMeta() {
  if (!state.conversacionId) {
    alert("Enviá al menos un mensaje o abrí un chat del historial para poder guardar título y enlaces.");
    return;
  }
  const body = {
    titulo: $("titulo-chat").value.trim() || undefined,
    carpetaId: $("carpeta").value || state.carpetaId,
    relacionadasIds: relacionadasIds()
  };
  await api("/api/conversaciones/" + encodeURIComponent(state.conversacionId), {
    method: "PATCH",
    body: JSON.stringify(body)
  });
  await cargarConversaciones();
}

async function guardarAprendizaje() {
  const titulo = prompt("Título del aprendizaje:");
  if (!titulo?.trim()) return;
  const contenido = prompt("Contenido (resumen, decisión, patrón reutilizable):");
  if (!contenido?.trim()) return;
  await api("/api/aprendizajes", {
    method: "POST",
    body: JSON.stringify({
      conversacionId: state.conversacionId,
      titulo: titulo.trim(),
      contenido: contenido.trim(),
      carpetaId: $("carpeta").value || state.carpetaId,
      relacionadasIds: relacionadasIds()
    })
  });
  await cargarAprendizajes();
}

async function nuevaCarpeta() {
  const nombre = prompt("Nombre de la carpeta:");
  if (!nombre?.trim()) return;
  const res = await api("/api/carpetas", {
    method: "POST",
    body: JSON.stringify({ nombre: nombre.trim() })
  });
  state.carpetaId = res.carpeta.id;
  await refrescarHealth();
  $("carpeta").value = state.carpetaId;
  await cargarConversaciones();
  await cargarAprendizajes();
}

async function enviar(e) {
  e.preventDefault();
  if (state.enviando) return;
  const texto = $("input").value.trim();
  if (!texto) return;

  state.enviando = true;
  state.faseEnvio = "preparando";
  actualizarBotonEnviar();

  const box = $("mensajes");
  const welcome = box.querySelector(".welcome-card");
  if (welcome) box.innerHTML = "";

  let inner = box.querySelector(".mensajes-inner");
  if (!inner) {
    inner = document.createElement("div");
    inner.className = "mensajes-inner";
    box.appendChild(inner);
  }

  appendBubble(inner, "user", texto);

  $("input").value = "";

  const tituloManual = $("titulo-chat").value.trim();
  const body = cuerpoChat(texto, tituloManual);

  const asistenteRow = document.createElement("div");
  asistenteRow.className = "msg-row assistant";
  const asistenteLabel = document.createElement("span");
  asistenteLabel.className = "msg-label";
  asistenteLabel.textContent = "Asistente";
  asistenteRow.appendChild(asistenteLabel);
  const asistenteBubble = document.createElement("div");
  asistenteBubble.className = "bubble assistant";
  asistenteBubble.textContent = "";
  asistenteRow.appendChild(asistenteBubble);
  inner.appendChild(asistenteRow);

  let placeholderActivo = false;
  let streamBuf = "";

  try {
    let res;
    try {
      res = await consumirChatStream(
        body,
        (delta, opts) => {
          if (opts?.clearStatus && placeholderActivo) {
            streamBuf = "";
            renderAssistantContent(asistenteBubble, "", false);
            placeholderActivo = false;
          }
          streamBuf += delta;
          renderAssistantContent(asistenteBubble, streamBuf, false);
          box.scrollTop = box.scrollHeight;
        },
        (st) => {
          if (st.mensaje && !streamBuf) {
            renderAssistantContent(asistenteBubble, st.mensaje, false);
            asistenteBubble.classList.add("placeholder");
            placeholderActivo = true;
          }
        }
      );
    } catch (streamErr) {
      streamBuf = "";
      asistenteRow.remove();
      res = await api("/api/chat", { method: "POST", body: JSON.stringify(body) });
      if (!res.ok) throw new Error(res.error || streamErr.message || "Error desconocido");
      appendBubble(inner, "assistant", res.respuesta);
    }

    if (res.ok === false) throw new Error(res.error || "Error desconocido");
    state.conversacionId = res.conversacionId;
    if (!asistenteRow.isConnected) {
      /* fallback ya pintó burbuja */
    } else {
      const finalText = res.respuesta || streamBuf;
      if (finalText) renderAssistantContent(asistenteBubble, finalText, true);
    }
    box.scrollTop = box.scrollHeight;
    if (res.avisos?.length) {
      appendBubble(inner, "system", res.avisos.join(" "), "aviso");
    }
    appendDebugMetrics(box, res.debug);
    await cargarConversaciones();
    if (tituloManual) await abrirConversacion(state.conversacionId);
    else renderListaEnlaces();
  } catch (err) {
    if (asistenteRow.isConnected && !streamBuf && !asistenteBubble.textContent) asistenteRow.remove();
    appendBubble(inner, "system", err.message || String(err), "system");
  } finally {
    state.enviando = false;
    state.faseEnvio = "generando";
    asistenteBubble?.classList?.remove("placeholder");
    actualizarBotonEnviar();
  }
}

$("form-chat").addEventListener("submit", enviar);
$("btn-nueva").addEventListener("click", nuevaConversacion);
$("btn-guardar-meta").addEventListener("click", () => guardarMeta().catch((e) => alert(e.message)));
$("btn-aprendizaje").addEventListener("click", () => guardarAprendizaje().catch((e) => alert(e.message)));
$("btn-nueva-carpeta").addEventListener("click", () => nuevaCarpeta().catch((e) => alert(e.message)));
$("enlace-todas-carpetas").addEventListener("change", () => cargarChatsParaEnlace().catch(() => {}));
$("carpeta").addEventListener("change", () => {
  state.carpetaId = $("carpeta").value;
  cargarConversaciones();
  cargarAprendizajes();
});
$("forzar-envio").addEventListener("change", actualizarBotonEnviar);
$("btn-guardar-api-key")?.addEventListener("click", guardarApiKeyLocal);

document.querySelectorAll(".sidebar-tab").forEach((tab) => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".sidebar-tab").forEach((t) => t.classList.remove("active"));
    tab.classList.add("active");
    const id = tab.dataset.tab;
    $("pane-historial").classList.toggle("hidden", id !== "historial");
    $("pane-aprendizajes").classList.toggle("hidden", id !== "aprendizajes");
  });
});
$("input").addEventListener("keydown", (ev) => {
  if (ev.key === "Enter" && !ev.shiftKey) {
    ev.preventDefault();
    $("form-chat").requestSubmit();
  }
});

initTema();
initModoBar();
initSidebarMobile();
cargarApiKeyEnCampo();

(async () => {
  try {
    await cargarEstado();
    await cargarConversaciones();
    await cargarAprendizajes();
    nuevaConversacion();
    state.healthTimer = setInterval(refrescarHealth, 60000);
  } catch {
    $("estado-servicio").textContent = "No se pudo conectar al servidor.";
    $("estado-servicio").className = "estado warn";
  }
})();

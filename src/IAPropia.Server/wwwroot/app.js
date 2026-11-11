const state = {
  conversacionId: null,
  carpetaId: "principal",
  enviando: false,
  ollamaOk: false,
  modeloConfigurado: "llama3.2",
  internetHabilitada: true,
  healthTimer: null,
  chatsEnlace: [],
  relacionadasSel: new Set()
};

const $ = (id) => document.getElementById(id);

async function api(path, opts = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(opts.headers || {}) },
    ...opts
  });
  const data = await res.json().catch(() => ({}));
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

function renderMensajes(mensajes) {
  const box = $("mensajes");
  box.innerHTML = "";
  if (!mensajes?.length) {
    box.innerHTML =
      '<div class="bubble system">Cerebro: Ollama en tu PC (sin API de pago). Marcá «Buscar en internet» si querés contexto web antes de responder.</div>';
    return;
  }
  for (const m of mensajes) {
    const div = document.createElement("div");
    div.className = "bubble " + (m.role === "user" ? "user" : "assistant");
    div.textContent = m.content;
    box.appendChild(div);
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
  btn.title = bloquear
    ? "Ollama no está listo. Revisá el pie o activá envío sin preflight."
    : "";
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

function pintarFooterEstado(est, ollamaHealth) {
  const el = $("estado-servicio");
  const ver = est?.version ? `v${est.version}` : "";
  state.internetHabilitada = !!est?.internetBusquedaHabilitada;
  $("usar-internet").disabled = !state.internetHabilitada;

  if (ollamaHealth) {
    state.ollamaOk = !!ollamaHealth.ok;
    const extra = state.internetHabilitada ? " · internet opcional" : " · internet deshabilitada";
    el.textContent = [ver, ollamaHealth.mensaje + extra].filter(Boolean).join(" · ");
    el.className = "estado " + (ollamaHealth.ok ? "ok" : "warn");
  } else {
    el.textContent = [ver, "Sin datos de Ollama"].filter(Boolean).join(" · ");
    el.className = "estado warn";
  }
  actualizarBotonEnviar();
}

async function refrescarHealth() {
  try {
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
    box.innerHTML = '<p class="hint">No hay otros chats en esta vista. Probá «Todas las carpetas» o creá otro chat.</p>';
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
    ul.innerHTML = '<li class="apr-empty">Sin aprendizajes en esta carpeta.</li>';
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
  state.conversacionId = id;
  const doc = await api("/api/conversaciones/" + encodeURIComponent(id));
  $("titulo-chat").value = doc.titulo || "";
  setRelacionadasFromDoc(doc.relacionadasIds || []);
  if (doc.carpetaId) {
    state.carpetaId = doc.carpetaId;
    if ([...$("carpeta").options].some((o) => o.value === doc.carpetaId)) $("carpeta").value = doc.carpetaId;
  }
  renderMensajes(doc.mensajes);
  await cargarConversaciones();
  await cargarAprendizajes();
}

function nuevaConversacion() {
  state.conversacionId = null;
  $("titulo-chat").value = "";
  state.relacionadasSel = new Set();
  $("relacionadas").value = "";
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
  actualizarBotonEnviar();

  const box = $("mensajes");
  if (box.querySelector(".bubble.system")) box.innerHTML = "";

  const userBubble = document.createElement("div");
  userBubble.className = "bubble user";
  userBubble.textContent = texto;
  box.appendChild(userBubble);

  $("input").value = "";

  const tituloManual = $("titulo-chat").value.trim();

  try {
    const body = {
      conversacionId: state.conversacionId,
      personaId: $("persona").value,
      proveedor: "ollama",
      modelo: modeloSeleccionado(),
      titulo: tituloManual || undefined,
      carpetaId: $("carpeta").value || state.carpetaId,
      relacionadasIds: relacionadasIds(),
      mensaje: texto,
      usarConocimientoLocal: $("usar-rag").checked,
      usarBusquedaInternet: $("usar-internet").checked && state.internetHabilitada
    };
    const res = await api("/api/chat", { method: "POST", body: JSON.stringify(body) });
    if (!res.ok) throw new Error(res.error || "Error desconocido");
    state.conversacionId = res.conversacionId;
    const ast = document.createElement("div");
    ast.className = "bubble assistant";
    ast.textContent = res.respuesta;
    box.appendChild(ast);
    box.scrollTop = box.scrollHeight;
    if (res.avisos?.length) {
      const av = document.createElement("div");
      av.className = "bubble system";
      av.textContent = res.avisos.join(" ");
      box.appendChild(av);
    }
    await cargarConversaciones();
    if (tituloManual) await abrirConversacion(state.conversacionId);
    else renderListaEnlaces();
  } catch (err) {
    const errBubble = document.createElement("div");
    errBubble.className = "bubble system";
    errBubble.textContent = err.message || String(err);
    box.appendChild(errBubble);
  } finally {
    state.enviando = false;
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
$("input").addEventListener("keydown", (ev) => {
  if (ev.key === "Enter" && !ev.shiftKey) {
    ev.preventDefault();
    $("form-chat").requestSubmit();
  }
});

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

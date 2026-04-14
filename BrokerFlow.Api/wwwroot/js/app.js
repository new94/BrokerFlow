// ── Toast ─────────────────────────────────────────────────────────────────────
function toast(msg, type = 'info') {
    let c = document.getElementById('toast-container');
    if (!c) { c = document.createElement('div'); c.id = 'toast-container'; document.body.appendChild(c); }
    const t = document.createElement('div');
    t.className = `toast toast-${type}`;
    t.textContent = msg;
    c.appendChild(t);
    setTimeout(() => t.remove(), 4000);
}

// ── Router ───────────────────────────────────────────────────────────────────
const pages = {};
let currentPage = 'sources';

function navigate(page) {
    currentPage = page;
    document.querySelectorAll('.nav-link').forEach(el => {
        el.classList.toggle('active', el.dataset.page === page);
    });
    renderPage(page);
    window.location.hash = page;
}

document.querySelectorAll('.nav-link').forEach(el => {
    el.addEventListener('click', e => { e.preventDefault(); navigate(el.dataset.page); });
});

window.addEventListener('hashchange', () => {
    const p = window.location.hash.slice(1) || 'sources';
    if (p !== currentPage) navigate(p);
});

// ── Health check ─────────────────────────────────────────────────────────────
async function checkHealth() {
    try {
        const h = await API.health();
        document.getElementById('healthDot').className = 'health-dot ok';
        document.getElementById('healthText').textContent = 'DB connected';
    } catch {
        document.getElementById('healthDot').className = 'health-dot err';
        document.getElementById('healthText').textContent = 'Disconnected';
    }
}
setInterval(checkHealth, 30000);
checkHealth();

// ── Render page ──────────────────────────────────────────────────────────────
async function renderPage(page) {
    const el = document.getElementById('page-content');
    el.innerHTML = '<div style="text-align:center;padding:60px;color:var(--text-dim)">Loading...</div>';
    try {
        switch (page) {
            case 'sources': await renderSources(el); break;
            case 'templates': await renderTemplates(el); break;
            case 'mappings': await renderMappings(el); break;
            case 'files': await renderFiles(el); break;
            case 'jobs': await renderJobs(el); break;
            case 'schedules': await renderSchedules(el); break;
            case 'settings': await renderSettings(el); break;
            case 'audit': await renderAudit(el); break;
            default: el.innerHTML = '<h1>Not Found</h1>';
        }
    } catch (e) {
        el.innerHTML = `<div class="card"><h2>Error</h2><p style="color:var(--red)">${e.message}</p></div>`;
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function esc(s) { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; }
function shortId(id) { return id ? id.substring(0, 8) : '-'; }
function fmtDate(d) { return d ? new Date(d).toLocaleString() : '-'; }
function badge(status) {
    const cls = { done: 'badge-done', error: 'badge-error', pending: 'badge-pending', running: 'badge-running' };
    return `<span class="badge ${cls[status] || ''}">${status}</span>`;
}

// ══════════════════════════════════════════════════════════════════════════════
// SOURCES PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderSources(el) {
    const sources = await API.sources.list();
    el.innerHTML = `
        <div class="page-header">
            <div><h1>Sources</h1><p>Configure broker report directories</p></div>
            <button class="btn btn-primary" id="addSourceBtn">+ Add Source</button>
        </div>
        ${sources.length === 0 ? '<div class="empty-state"><h3>No sources configured</h3><p>Add a broker report source to get started</p></div>' : ''}
        <div class="card-grid">${sources.map(s => `
            <div class="card">
                <div style="display:flex;justify-content:space-between;align-items:start">
                    <div>
                        <h3>${esc(s.name)}</h3>
                        <p style="color:var(--text-dim);font-size:12px;margin-top:4px;font-family:var(--mono)">${esc(s.path || 'No path set')}</p>
                    </div>
                    <span class="badge ${s.enabled ? 'badge-done' : 'badge-error'}">${s.enabled ? 'Active' : 'Disabled'}</span>
                </div>
                <div style="margin-top:12px;display:flex;gap:16px;font-size:12px;color:var(--text-dim)">
                    <span>Mask: <b>${esc(s.fileMask)}</b></span>
                    <span>Format: <b>${esc(s.fileFormat)}</b></span>
                    ${s.csvSeparator ? `<span>CSV Sep: <b>${esc(s.csvSeparator)}</b></span>` : ''}
                </div>
                <div class="btn-group" style="margin-top:12px">
                    <button class="btn btn-secondary btn-sm" onclick="editSource('${s.id}')">Edit</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteSource('${s.id}')">Delete</button>
                </div>
            </div>
        `).join('')}</div>`;

    document.getElementById('addSourceBtn').onclick = () => showSourceModal();
}

async function showSourceModal(source = null) {
    const isEdit = !!source;
    const overlay = document.createElement('div');
    overlay.className = 'modal-overlay';
    overlay.innerHTML = `<div class="modal">
        <h2>${isEdit ? 'Edit' : 'Add'} Source</h2>
        <div class="form-group"><label>Name</label><input type="text" id="srcName" value="${esc(source?.name || '')}"></div>
        <div class="form-group"><label>Path (network share or local directory)</label><input type="text" id="srcPath" value="${esc(source?.path || '')}" placeholder="\\\\server\\share\\reports or C:\\Reports"></div>
        <div class="form-row">
            <div class="form-group"><label>File Mask</label><input type="text" id="srcMask" value="${esc(source?.fileMask || '*.*')}"></div>
            <div class="form-group"><label>File Format</label>
                <select id="srcFormat">
                    <option value="auto" ${source?.fileFormat==='auto'?'selected':''}>Auto-detect</option>
                    <option value="xml" ${source?.fileFormat==='xml'?'selected':''}>XML</option>
                    <option value="csv" ${source?.fileFormat==='csv'?'selected':''}>CSV</option>
                    <option value="xlsx" ${source?.fileFormat==='xlsx'?'selected':''}>Excel (XLSX)</option>
                    <option value="xls" ${source?.fileFormat==='xls'?'selected':''}>Excel (XLS)</option>
                    <option value="pdf" ${source?.fileFormat==='pdf'?'selected':''}>PDF</option>
                </select>
            </div>
        </div>
        <div class="form-row" id="csvSepRow" style="display:none">
            <div class="form-group"><label>CSV Separator</label>
                <select id="srcCsvSep">
                    <option value="">Auto-detect</option>
                    <option value="comma" ${source?.csvSeparator==='comma'?'selected':''}>Comma (,)</option>
                    <option value="semicolon" ${source?.csvSeparator==='semicolon'?'selected':''}>Semicolon (;)</option>
                    <option value="tab" ${source?.csvSeparator==='tab'?'selected':''}>Tab</option>
                    <option value="pipe" ${source?.csvSeparator==='pipe'?'selected':''}>Pipe (|)</option>
                    <option value="custom" ${source?.csvSeparator==='custom'?'selected':''}>Custom...</option>
                </select>
            </div>
            <div class="form-group" id="csvCustomRow" style="display:none"><label>Custom Separator</label><input type="text" id="srcCsvCustom" value="${esc(source?.csvCustomSeparator || '')}" maxlength="5"></div>
        </div>
        <div class="form-group"><label><input type="checkbox" id="srcEnabled" ${source?.enabled !== false ? 'checked' : ''}> Enabled</label></div>
        <div class="modal-actions">
            <button class="btn btn-secondary" id="srcCancel">Cancel</button>
            <button class="btn btn-primary" id="srcSave">${isEdit ? 'Save' : 'Create'}</button>
        </div>
    </div>`;
    document.body.appendChild(overlay);

    const fmt = overlay.querySelector('#srcFormat');
    const sepRow = overlay.querySelector('#csvSepRow');
    const sepSel = overlay.querySelector('#srcCsvSep');
    const customRow = overlay.querySelector('#csvCustomRow');
    function toggleCsv() {
        sepRow.style.display = fmt.value === 'csv' ? '' : 'none';
        customRow.style.display = sepSel.value === 'custom' ? '' : 'none';
    }
    fmt.onchange = toggleCsv;
    sepSel.onchange = toggleCsv;
    toggleCsv();

    overlay.querySelector('#srcCancel').onclick = () => overlay.remove();
    overlay.onclick = e => { if (e.target === overlay) overlay.remove(); };

    overlay.querySelector('#srcSave').onclick = async () => {
        const data = {
            name: overlay.querySelector('#srcName').value,
            path: overlay.querySelector('#srcPath').value,
            fileMask: overlay.querySelector('#srcMask').value,
            fileFormat: fmt.value,
            csvSeparator: fmt.value === 'csv' ? sepSel.value || null : null,
            csvCustomSeparator: sepSel.value === 'custom' ? overlay.querySelector('#srcCsvCustom').value : null,
            enabled: overlay.querySelector('#srcEnabled').checked,
        };
        try {
            if (isEdit) await API.sources.update(source.id, data);
            else await API.sources.create(data);
            overlay.remove();
            toast(isEdit ? 'Source updated' : 'Source created', 'success');
            renderPage('sources');
        } catch (e) { toast(e.message, 'error'); }
    };
}

async function editSource(id) { const s = await API.sources.get(id); showSourceModal(s); }
async function deleteSource(id) { if (confirm('Delete this source?')) { await API.sources.delete(id); toast('Deleted', 'success'); renderPage('sources'); } }

// ══════════════════════════════════════════════════════════════════════════════
// TEMPLATES PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderTemplates(el) {
    const templates = await API.templates.list();
    el.innerHTML = `
        <div class="page-header">
            <div><h1>XML Templates</h1><p>Define target XML structures</p></div>
            <div class="btn-group">
                <button class="btn btn-secondary" id="buildTplBtn">Build from Fields</button>
                <button class="btn btn-primary" id="addTplBtn">+ Import Template</button>
            </div>
        </div>
        <div class="card-grid">${templates.map(t => `
            <div class="card">
                <h3>${esc(t.name)}</h3>
                <p style="color:var(--text-dim);font-size:12px;margin-top:4px">${(JSON.parse(t.fieldsJson||'[]')).length} fields • ${fmtDate(t.createdAt)}</p>
                <div style="margin-top:8px"><code style="font-size:11px;color:var(--green)">${esc((t.content||'').substring(0, 120))}...</code></div>
                <div class="btn-group" style="margin-top:12px">
                    <button class="btn btn-secondary btn-sm" onclick="editTemplate('${t.id}')">Edit</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteTemplate('${t.id}')">Delete</button>
                </div>
            </div>
        `).join('')}${templates.length === 0 ? '<div class="empty-state"><h3>No templates yet</h3><p>Create or import an XML template</p></div>' : ''}</div>`;

    document.getElementById('addTplBtn').onclick = () => showTemplateModal();
    document.getElementById('buildTplBtn').onclick = () => showBuildTemplateModal();
}

async function showTemplateModal(template = null) {
    const isEdit = !!template;
    const overlay = document.createElement('div');
    overlay.className = 'modal-overlay';
    overlay.innerHTML = `<div class="modal">
        <h2>${isEdit ? 'Edit' : 'Import'} XML Template</h2>
        <div class="form-group"><label>Name</label><input type="text" id="tplName" value="${esc(template?.name || '')}"></div>
        <div class="form-group"><label>XML Content</label><textarea id="tplContent" rows="12">${esc(template?.content || '')}</textarea></div>
        <div class="modal-actions">
            <button class="btn btn-secondary" onclick="this.closest('.modal-overlay').remove()">Cancel</button>
            <button class="btn btn-primary" id="tplSave">${isEdit ? 'Save' : 'Create'}</button>
        </div>
    </div>`;
    document.body.appendChild(overlay);
    overlay.onclick = e => { if (e.target === overlay) overlay.remove(); };

    overlay.querySelector('#tplSave').onclick = async () => {
        const data = { name: overlay.querySelector('#tplName').value, content: overlay.querySelector('#tplContent').value };
        try {
            if (isEdit) await API.templates.update(template.id, data);
            else await API.templates.create(data);
            overlay.remove(); toast('Template saved', 'success'); renderPage('templates');
        } catch (e) { toast(e.message, 'error'); }
    };
}

async function showBuildTemplateModal() {
    const overlay = document.createElement('div');
    overlay.className = 'modal-overlay';
    overlay.innerHTML = `<div class="modal">
        <h2>Build Template from Fields</h2>
        <div class="form-group"><label>Template Name</label><input type="text" id="bldName" value="New Template"></div>
        <div class="form-group"><label>Root Element</label><input type="text" id="bldRoot" value="Document"></div>
        <div id="bldFields">
            <div class="form-group"><label>Fields (one path per line, e.g., Trade/TradeId)</label>
            <textarea id="bldFieldList" rows="8" placeholder="Trade/TradeId&#10;Trade/Date&#10;Trade/Amount&#10;Trade/Currency"></textarea></div>
        </div>
        <div class="modal-actions">
            <button class="btn btn-secondary" onclick="this.closest('.modal-overlay').remove()">Cancel</button>
            <button class="btn btn-primary" id="bldSave">Build & Save</button>
        </div>
    </div>`;
    document.body.appendChild(overlay);
    overlay.onclick = e => { if (e.target === overlay) overlay.remove(); };

    overlay.querySelector('#bldSave').onclick = async () => {
        const lines = overlay.querySelector('#bldFieldList').value.split('\n').map(l => l.trim()).filter(Boolean);
        const data = {
            name: overlay.querySelector('#bldName').value,
            rootElement: overlay.querySelector('#bldRoot').value,
            fields: lines.map(l => ({ path: l, defaultValue: '' }))
        };
        try {
            const result = await API.templates.buildFromFields(data);
            await API.templates.create({ name: result.name, content: result.content });
            overlay.remove(); toast('Template built and saved', 'success'); renderPage('templates');
        } catch (e) { toast(e.message, 'error'); }
    };
}

async function editTemplate(id) { const t = await API.templates.get(id); showTemplateModal(t); }
async function deleteTemplate(id) { if (confirm('Delete?')) { await API.templates.delete(id); toast('Deleted', 'success'); renderPage('templates'); } }

// ══════════════════════════════════════════════════════════════════════════════
// MAPPINGS PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderMappings(el) {
    const mappings = await API.mappings.list();
    el.innerHTML = `
        <div class="page-header">
            <div><h1>Mappings</h1><p>Define how broker fields map to target XML</p></div>
            <button class="btn btn-primary" id="addMapBtn">+ New Mapping</button>
        </div>
        <div class="card-grid">${mappings.map(m => {
            const ruleCount = JSON.parse(m.rulesJson || '[]').length;
            return `<div class="card">
                <h3>${esc(m.name)}</h3>
                <p style="color:var(--text-dim);font-size:12px;margin-top:4px">${ruleCount} rules • ${m.splitOutput ? 'Split output' : 'Single output'}</p>
                <div class="btn-group" style="margin-top:12px">
                    <button class="btn btn-secondary btn-sm" onclick="editMapping('${m.id}')">Edit</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteMapping('${m.id}')">Delete</button>
                </div>
            </div>`;
        }).join('')}${mappings.length === 0 ? '<div class="empty-state"><h3>No mappings yet</h3><p>Create a mapping configuration</p></div>' : ''}</div>`;

    document.getElementById('addMapBtn').onclick = () => showMappingModal();
}

async function showMappingModal(mapping = null) {
    const isEdit = !!mapping;
    const sources = await API.sources.list();
    const templates = await API.templates.list();
    let rules = mapping ? JSON.parse(mapping.rulesJson || '[]') : [];
    let splitCondition = mapping?.splitConditionJson ? JSON.parse(mapping.splitConditionJson) : null;

    const overlay = document.createElement('div');
    overlay.className = 'modal-overlay';
    overlay.innerHTML = `<div class="modal" style="max-width:900px">
        <h2>${isEdit ? 'Edit' : 'Create'} Mapping</h2>
        <div class="form-row">
            <div class="form-group"><label>Name</label><input type="text" id="mapName" value="${esc(mapping?.name || '')}"></div>
            <div class="form-group"><label>Source</label>
                <select id="mapSource"><option value="">- None -</option>${sources.map(s => `<option value="${s.id}" ${mapping?.sourceId===s.id?'selected':''}>${esc(s.name)}</option>`).join('')}</select>
            </div>
        </div>
        <div class="form-group"><label>XML Template</label>
            <select id="mapTemplate"><option value="">- None -</option>${templates.map(t => `<option value="${t.id}" ${mapping?.templateId===t.id?'selected':''}>${esc(t.name)}</option>`).join('')}</select>
        </div>
        <div class="form-group"><label>Or paste XML template directly</label>
            <textarea id="mapXml" rows="5">${esc(mapping?.xmlTemplate || '')}</textarea>
        </div>

        <h3 style="margin:16px 0 8px">Mapping Rules</h3>
        <div id="rulesContainer"></div>
        <button class="btn btn-secondary btn-sm" id="addRuleBtn" style="margin-top:8px">+ Add Rule</button>

        <h3 style="margin:16px 0 8px">Output Settings</h3>
        <div class="form-group"><label><input type="checkbox" id="mapSplit" ${mapping?.splitOutput ? 'checked' : ''}> Generate separate file per matching record</label></div>
        <div id="splitSettings" style="display:${mapping?.splitOutput ? 'block' : 'none'}">
            <div class="form-group"><label>File Name Pattern</label><input type="text" id="mapSplitPattern" value="${esc(mapping?.splitFileNamePattern || 'output_{_index}_{_date}.xml')}" placeholder="trade_{TradeId}_{_date}.xml"></div>
            <div class="form-group"><label>Split Condition (JSON)</label><textarea id="mapSplitCond" rows="3">${esc(splitCondition ? JSON.stringify(splitCondition, null, 2) : '')}</textarea></div>
        </div>

        <div class="modal-actions">
            <button class="btn btn-secondary" onclick="this.closest('.modal-overlay').remove()">Cancel</button>
            <button class="btn btn-primary" id="mapSave">${isEdit ? 'Save' : 'Create'}</button>
        </div>
    </div>`;
    document.body.appendChild(overlay);
    overlay.onclick = e => { if (e.target === overlay) overlay.remove(); };

    const splitChk = overlay.querySelector('#mapSplit');
    const splitSet = overlay.querySelector('#splitSettings');
    splitChk.onchange = () => { splitSet.style.display = splitChk.checked ? 'block' : 'none'; };

    function renderRules() {
        const container = overlay.querySelector('#rulesContainer');
        container.innerHTML = rules.map((r, i) => `
            <div class="rule-card">
                <div class="rule-header">
                    <strong>${esc(r.xml_path || '?')}</strong>
                    <button class="btn btn-danger btn-sm" onclick="window._removeRule(${i})">×</button>
                </div>
                <div class="form-row">
                    <div class="form-group"><label>XML Path</label><input type="text" value="${esc(r.xml_path || '')}" onchange="window._rules[${i}].xml_path=this.value"></div>
                    <div class="form-group"><label>Expression Type</label>
                        <select onchange="window._rules[${i}].expression.type=this.value">
                            <option value="field" ${r.expression?.type==='field'?'selected':''}>Field</option>
                            <option value="literal" ${r.expression?.type==='literal'?'selected':''}>Literal</option>
                            <option value="arithmetic" ${r.expression?.type==='arithmetic'?'selected':''}>Arithmetic</option>
                            <option value="conditional" ${r.expression?.type==='conditional'?'selected':''}>Conditional</option>
                            <option value="guid" ${r.expression?.type==='guid'?'selected':''}>GUID</option>
                            <option value="date_diff" ${r.expression?.type==='date_diff'?'selected':''}>Date Diff</option>
                            <option value="string_op" ${r.expression?.type==='string_op'?'selected':''}>String Op</option>
                        </select>
                    </div>
                </div>
                <div class="form-group"><label>Expression JSON</label>
                    <textarea rows="3" style="font-size:12px" onchange="try{window._rules[${i}].expression=JSON.parse(this.value)}catch(e){}">${esc(JSON.stringify(r.expression || {}, null, 2))}</textarea>
                </div>
                <div class="form-group"><label>Condition JSON (optional)</label>
                    <textarea rows="2" style="font-size:12px" onchange="try{window._rules[${i}].condition=this.value?JSON.parse(this.value):null}catch(e){}">${esc(r.condition ? JSON.stringify(r.condition, null, 2) : '')}</textarea>
                </div>
            </div>
        `).join('');
    }

    window._rules = rules;
    window._removeRule = (i) => { rules.splice(i, 1); renderRules(); };
    renderRules();

    overlay.querySelector('#addRuleBtn').onclick = () => {
        rules.push({ xml_path: '', expression: { type: 'field', name: '' }, condition: null });
        renderRules();
    };

    overlay.querySelector('#mapSave').onclick = async () => {
        const data = {
            name: overlay.querySelector('#mapName').value,
            sourceId: overlay.querySelector('#mapSource').value || null,
            templateId: overlay.querySelector('#mapTemplate').value || null,
            xmlTemplate: overlay.querySelector('#mapXml').value || null,
            rules: rules,
            splitOutput: splitChk.checked,
            splitCondition: splitChk.checked ? (() => { try { return JSON.parse(overlay.querySelector('#mapSplitCond').value); } catch { return null; } })() : null,
            splitFileNamePattern: overlay.querySelector('#mapSplitPattern').value || null,
        };
        try {
            if (isEdit) await API.mappings.update(mapping.id, data);
            else await API.mappings.create(data);
            overlay.remove(); toast('Mapping saved', 'success'); renderPage('mappings');
        } catch (e) { toast(e.message, 'error'); }
    };
}

async function editMapping(id) { const m = await API.mappings.get(id); showMappingModal(m); }
async function deleteMapping(id) { if (confirm('Delete?')) { await API.mappings.delete(id); toast('Deleted', 'success'); renderPage('mappings'); } }

// ══════════════════════════════════════════════════════════════════════════════
// FILES PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderFiles(el) {
    el.innerHTML = `
        <div class="page-header"><div><h1>Files</h1><p>Upload or scan broker reports</p></div></div>
        <div class="card">
            <h3>Upload File</h3>
            <div class="form-group" style="margin-top:12px">
                <input type="file" id="fileInput" accept=".xml,.csv,.xls,.xlsx,.pdf">
            </div>
            <button class="btn btn-primary" id="uploadBtn">Upload</button>
            <span id="uploadResult" style="margin-left:12px;color:var(--green)"></span>
        </div>
        <div class="card">
            <h3>Scan Directory</h3>
            <div class="form-row" style="margin-top:12px">
                <div class="form-group"><label>Path</label><input type="text" id="scanPath" placeholder="\\\\server\\share\\reports"></div>
                <div class="form-group"><label>Mask</label><input type="text" id="scanMask" value="*.*"></div>
            </div>
            <button class="btn btn-secondary" id="scanBtn">Scan</button>
            <div id="scanResults" style="margin-top:12px"></div>
        </div>
        <div class="card">
            <h3>Parse & Preview</h3>
            <div class="form-row" style="margin-top:12px">
                <div class="form-group"><label>File Path</label><input type="text" id="parsePath" placeholder="Full path to file"></div>
                <div class="form-group"><label>CSV Separator (optional)</label>
                    <select id="parseSep"><option value="">Auto</option><option value="comma">,</option><option value="semicolon">;</option><option value="tab">Tab</option><option value="pipe">|</option></select>
                </div>
            </div>
            <button class="btn btn-secondary" id="parseBtn">Parse</button>
            <div id="parseResults" style="margin-top:12px"></div>
        </div>`;

    document.getElementById('uploadBtn').onclick = async () => {
        const file = document.getElementById('fileInput').files[0];
        if (!file) return toast('Select a file', 'error');
        const fd = new FormData(); fd.append('file', file);
        try {
            const r = await API.files.upload(fd);
            document.getElementById('uploadResult').textContent = `✓ Uploaded: ${r.name}`;
            document.getElementById('parsePath').value = r.path;
            toast('File uploaded', 'success');
        } catch (e) { toast(e.message, 'error'); }
    };

    document.getElementById('scanBtn').onclick = async () => {
        try {
            const r = await API.files.scan(document.getElementById('scanPath').value, document.getElementById('scanMask').value);
            document.getElementById('scanResults').innerHTML = `<p style="color:var(--text-dim)">${r.total} files found</p>` +
                (r.files.length ? `<table><thead><tr><th>Name</th><th>Size</th><th>Modified</th><th></th></tr></thead><tbody>${r.files.map(f => `<tr><td>${esc(f.name)}</td><td>${(f.size/1024).toFixed(1)} KB</td><td>${fmtDate(f.modified)}</td><td><button class="btn btn-sm btn-secondary" onclick="document.getElementById('parsePath').value='${esc(f.path)}'">Select</button></td></tr>`).join('')}</tbody></table>` : '');
        } catch (e) { toast(e.message, 'error'); }
    };

    document.getElementById('parseBtn').onclick = async () => {
        try {
            const r = await API.files.parse({
                filePath: document.getElementById('parsePath').value,
                csvSeparator: document.getElementById('parseSep').value || null
            });
            const res = document.getElementById('parseResults');
            res.innerHTML = `<p style="color:var(--text-dim)">${r.total} records, ${r.fields.length} fields: <b>${r.fields.join(', ')}</b></p>
                <div class="data-preview data-table-wrapper" style="margin-top:8px">
                    <table><thead><tr>${r.fields.map(f => `<th>${esc(f)}</th>`).join('')}</tr></thead>
                    <tbody>${r.records.slice(0, 50).map(rec => `<tr>${r.fields.map(f => `<td>${esc(String(rec[f]||''))}</td>`).join('')}</tr>`).join('')}</tbody></table>
                </div>`;
        } catch (e) { toast(e.message, 'error'); }
    };
}

// ══════════════════════════════════════════════════════════════════════════════
// JOBS PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderJobs(el) {
    const data = await API.jobs.list();
    const mappings = await API.mappings.list();
    el.innerHTML = `
        <div class="page-header">
            <div><h1>Jobs</h1><p>Process broker reports</p></div>
            <button class="btn btn-primary" id="newJobBtn">+ New Job</button>
        </div>
        <div class="card">
            <table>
                <thead><tr><th>ID</th><th>File</th><th>Status</th><th>Records</th><th>Files</th><th>Started</th><th>Finished</th><th></th></tr></thead>
                <tbody>${data.jobs.map(j => `<tr>
                    <td style="font-family:var(--mono);font-size:12px">${shortId(j.id)}</td>
                    <td>${esc(j.originalFileName || '-')}</td>
                    <td>${badge(j.status)}</td>
                    <td>${j.recordsProcessed}</td>
                    <td>${j.filesGenerated}</td>
                    <td>${fmtDate(j.startedAt)}</td>
                    <td>${fmtDate(j.finishedAt)}</td>
                    <td class="btn-group">
                        ${j.status === 'error' ? `<button class="btn btn-secondary btn-sm" onclick="retryJob('${j.id}')">Retry</button>` : ''}
                        ${j.status === 'done' && j.resultPath ? `<a class="btn btn-secondary btn-sm" href="/api/files/download?path=${encodeURIComponent(j.resultPath.split(';')[0])}" target="_blank">Download</a>` : ''}
                        <button class="btn btn-danger btn-sm" onclick="deleteJob('${j.id}')">×</button>
                    </td>
                </tr>${j.errorMessage ? `<tr><td colspan="8" style="color:var(--red);font-size:12px;padding:4px 12px">${esc(j.errorMessage)}</td></tr>` : ''}`).join('')}
                ${data.jobs.length === 0 ? '<tr><td colspan="8" class="empty-state">No jobs yet</td></tr>' : ''}
                </tbody>
            </table>
        </div>`;

    document.getElementById('newJobBtn').onclick = () => {
        const overlay = document.createElement('div');
        overlay.className = 'modal-overlay';
        overlay.innerHTML = `<div class="modal">
            <h2>Create Job</h2>
            <div class="form-group"><label>Mapping</label>
                <select id="jobMapping">${mappings.map(m => `<option value="${m.id}">${esc(m.name)}</option>`).join('')}</select>
            </div>
            <div class="form-group"><label>File Path</label><input type="text" id="jobFile" placeholder="Full path to broker report"></div>
            <div class="modal-actions">
                <button class="btn btn-secondary" onclick="this.closest('.modal-overlay').remove()">Cancel</button>
                <button class="btn btn-primary" id="jobCreate">Run</button>
            </div>
        </div>`;
        document.body.appendChild(overlay);
        overlay.onclick = e => { if (e.target === overlay) overlay.remove(); };
        overlay.querySelector('#jobCreate').onclick = async () => {
            try {
                await API.jobs.create({ mappingId: overlay.querySelector('#jobMapping').value, filePath: overlay.querySelector('#jobFile').value });
                overlay.remove(); toast('Job started', 'success');
                setTimeout(() => renderPage('jobs'), 2000);
            } catch (e) { toast(e.message, 'error'); }
        };
    };
}

async function retryJob(id) { await API.jobs.retry(id); toast('Retrying...', 'info'); setTimeout(() => renderPage('jobs'), 2000); }
async function deleteJob(id) { if (confirm('Delete?')) { await API.jobs.delete(id); renderPage('jobs'); } }

// ══════════════════════════════════════════════════════════════════════════════
// SCHEDULES PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderSchedules(el) {
    const schedules = await API.schedules.list();
    const sources = await API.sources.list();
    const mappings = await API.mappings.list();
    el.innerHTML = `
        <div class="page-header">
            <div><h1>Schedules</h1><p>Automate report processing</p></div>
            <button class="btn btn-primary" id="addSchedBtn">+ New Schedule</button>
        </div>
        <div class="card"><table>
            <thead><tr><th>Name</th><th>Cron</th><th>Status</th><th>Last Run</th><th>Next Run</th><th></th></tr></thead>
            <tbody>${schedules.map(s => `<tr>
                <td><b>${esc(s.name)}</b></td>
                <td style="font-family:var(--mono);font-size:12px">${esc(s.cronExpression)}</td>
                <td><span class="badge ${s.enabled ? 'badge-done' : 'badge-error'}">${s.enabled ? 'Active' : 'Off'}</span></td>
                <td>${fmtDate(s.lastRunAt)}</td><td>${fmtDate(s.nextRunAt)}</td>
                <td class="btn-group">
                    <button class="btn btn-secondary btn-sm" onclick="editSchedule('${s.id}')">Edit</button>
                    <button class="btn btn-danger btn-sm" onclick="deleteSchedule('${s.id}')">×</button>
                </td>
            </tr>`).join('')}
            ${schedules.length === 0 ? '<tr><td colspan="6" class="empty-state">No schedules configured</td></tr>' : ''}
            </tbody>
        </table></div>`;

    document.getElementById('addSchedBtn').onclick = () => showScheduleModal(null, sources, mappings);
}

async function showScheduleModal(schedule, sources, mappings) {
    if (!sources) sources = await API.sources.list();
    if (!mappings) mappings = await API.mappings.list();
    const isEdit = !!schedule;
    const overlay = document.createElement('div');
    overlay.className = 'modal-overlay';
    overlay.innerHTML = `<div class="modal">
        <h2>${isEdit ? 'Edit' : 'Create'} Schedule</h2>
        <div class="form-group"><label>Name</label><input type="text" id="schedName" value="${esc(schedule?.name || '')}"></div>
        <div class="form-row">
            <div class="form-group"><label>Source</label><select id="schedSource">${sources.map(s => `<option value="${s.id}" ${schedule?.sourceId===s.id?'selected':''}>${esc(s.name)}</option>`).join('')}</select></div>
            <div class="form-group"><label>Mapping</label><select id="schedMapping">${mappings.map(m => `<option value="${m.id}" ${schedule?.mappingId===m.id?'selected':''}>${esc(m.name)}</option>`).join('')}</select></div>
        </div>
        <div class="form-group"><label>Cron Expression</label><input type="text" id="schedCron" value="${esc(schedule?.cronExpression || '0 */5 * * *')}"></div>
        <div id="cronPreview" style="color:var(--text-dim);font-size:12px;margin-bottom:12px"></div>
        <div class="form-group"><label><input type="checkbox" id="schedEnabled" ${schedule?.enabled !== false ? 'checked' : ''}> Enabled</label></div>
        <div class="modal-actions">
            <button class="btn btn-secondary" onclick="this.closest('.modal-overlay').remove()">Cancel</button>
            <button class="btn btn-primary" id="schedSave">${isEdit ? 'Save' : 'Create'}</button>
        </div>
    </div>`;
    document.body.appendChild(overlay);
    overlay.onclick = e => { if (e.target === overlay) overlay.remove(); };

    const cronInput = overlay.querySelector('#schedCron');
    const preview = overlay.querySelector('#cronPreview');
    async function validateCron() {
        try {
            const r = await API.schedules.validateCron(cronInput.value);
            preview.innerHTML = r.valid ? `✓ Next: ${r.nextRuns.map(d => fmtDate(d)).join(', ')}` : `<span style="color:var(--red)">✗ ${r.error}</span>`;
        } catch { }
    }
    cronInput.oninput = validateCron;
    validateCron();

    overlay.querySelector('#schedSave').onclick = async () => {
        const data = {
            name: overlay.querySelector('#schedName').value,
            sourceId: overlay.querySelector('#schedSource').value,
            mappingId: overlay.querySelector('#schedMapping').value,
            cronExpression: cronInput.value,
            enabled: overlay.querySelector('#schedEnabled').checked,
        };
        try {
            if (isEdit) await API.schedules.update(schedule.id, data);
            else await API.schedules.create(data);
            overlay.remove(); toast('Schedule saved', 'success'); renderPage('schedules');
        } catch (e) { toast(e.message, 'error'); }
    };
}

async function editSchedule(id) { const s = await API.schedules.get(id); showScheduleModal(s); }
async function deleteSchedule(id) { if (confirm('Delete?')) { await API.schedules.delete(id); toast('Deleted', 'success'); renderPage('schedules'); } }

// ══════════════════════════════════════════════════════════════════════════════
// SETTINGS PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderSettings(el) {
    const config = await API.config.get();
    el.innerHTML = `
        <div class="page-header"><div><h1>Settings</h1><p>Configure paths and application settings</p></div></div>
        <div class="card">
            <h3>Directory Paths</h3>
            <div class="form-group" style="margin-top:12px"><label>Reports Directory</label><input type="text" id="cfgReports" value="${esc(config.reports_dir || '')}"></div>
            <div class="form-group"><label>Output Directory</label><input type="text" id="cfgOutput" value="${esc(config.output_dir || '')}"></div>
            <div class="form-group"><label>Uploads Directory</label><input type="text" id="cfgUploads" value="${esc(config.uploads_dir || '')}"></div>
            <button class="btn btn-primary" id="cfgSave">Save Settings</button>
        </div>`;

    document.getElementById('cfgSave').onclick = async () => {
        try {
            await API.config.update({
                reportsDir: document.getElementById('cfgReports').value,
                outputDir: document.getElementById('cfgOutput').value,
                uploadsDir: document.getElementById('cfgUploads').value,
            });
            toast('Settings saved', 'success');
        } catch (e) { toast(e.message, 'error'); }
    };
}

// ══════════════════════════════════════════════════════════════════════════════
// AUDIT PAGE
// ══════════════════════════════════════════════════════════════════════════════
async function renderAudit(el) {
    const data = await API.audit.list();
    el.innerHTML = `
        <div class="page-header"><div><h1>Audit Log</h1><p>Activity trail</p></div></div>
        <div class="card"><table>
            <thead><tr><th>Time</th><th>Action</th><th>Entity</th><th>Details</th></tr></thead>
            <tbody>${data.entries.map(e => `<tr>
                <td style="font-size:12px;white-space:nowrap">${fmtDate(e.createdAt)}</td>
                <td><b>${esc(e.action)}</b></td>
                <td>${esc(e.entityType || '')} ${shortId(e.entityId)}</td>
                <td style="color:var(--text-dim);font-size:12px;max-width:400px;overflow:hidden;text-overflow:ellipsis">${esc(e.details || '')}</td>
            </tr>`).join('')}
            ${data.entries.length === 0 ? '<tr><td colspan="4" class="empty-state">No audit entries</td></tr>' : ''}
            </tbody>
        </table></div>`;
}

// ── Init ─────────────────────────────────────────────────────────────────────
const initPage = window.location.hash.slice(1) || 'sources';
navigate(initPage);

// ── API Client ────────────────────────────────────────────────────────────────
const API = {
    base: '/api',

    async request(method, path, body = null, isFormData = false) {
        const opts = { method, headers: {} };
        if (body && !isFormData) {
            opts.headers['Content-Type'] = 'application/json';
            opts.body = JSON.stringify(body);
        } else if (body && isFormData) {
            opts.body = body;
        }
        const res = await fetch(this.base + path, opts);
        if (!res.ok) {
            const err = await res.json().catch(() => ({ error: res.statusText }));
            throw new Error(err.error || err.title || res.statusText);
        }
        return res.json();
    },

    get: (p) => API.request('GET', p),
    post: (p, b) => API.request('POST', p, b),
    put: (p, b) => API.request('PUT', p, b),
    del: (p) => API.request('DELETE', p),
    upload: (p, fd) => API.request('POST', p, fd, true),

    // Shortcuts
    sources: {
        list: () => API.get('/sources'),
        get: (id) => API.get(`/sources/${id}`),
        create: (d) => API.post('/sources', d),
        update: (id, d) => API.put(`/sources/${id}`, d),
        delete: (id) => API.del(`/sources/${id}`),
    },
    templates: {
        list: () => API.get('/templates'),
        get: (id) => API.get(`/templates/${id}`),
        create: (d) => API.post('/templates', d),
        update: (id, d) => API.put(`/templates/${id}`, d),
        delete: (id) => API.del(`/templates/${id}`),
        buildFromFields: (d) => API.post('/templates/build-from-fields', d),
        extractFields: (xml) => API.post('/templates/extract-fields', { xmlContent: xml }),
    },
    mappings: {
        list: () => API.get('/mappings'),
        get: (id) => API.get(`/mappings/${id}`),
        create: (d) => API.post('/mappings', d),
        update: (id, d) => API.put(`/mappings/${id}`, d),
        delete: (id) => API.del(`/mappings/${id}`),
        preview: (d) => API.post('/mappings/preview', d),
    },
    files: {
        upload: (fd) => API.upload('/files/upload', fd),
        scan: (path, mask) => API.get(`/files/scan?path=${encodeURIComponent(path)}&mask=${encodeURIComponent(mask||'*.*')}`),
        parse: (d) => API.post('/files/parse', d),
        listOutputs: () => API.get('/files/list-outputs'),
    },
    jobs: {
        list: (page=1) => API.get(`/jobs?page=${page}`),
        get: (id) => API.get(`/jobs/${id}`),
        create: (d) => API.post('/jobs', d),
        retry: (id) => API.post(`/jobs/${id}/retry`),
        delete: (id) => API.del(`/jobs/${id}`),
    },
    schedules: {
        list: () => API.get('/schedules'),
        get: (id) => API.get(`/schedules/${id}`),
        create: (d) => API.post('/schedules', d),
        update: (id, d) => API.put(`/schedules/${id}`, d),
        delete: (id) => API.del(`/schedules/${id}`),
        validateCron: (expr) => API.post('/schedules/validate-cron', { expression: expr }),
    },
    config: {
        get: () => API.get('/config'),
        update: (d) => API.put('/config', d),
    },
    audit: {
        list: (page=1) => API.get(`/audit?page=${page}`),
    },
    health: () => API.get('/health'),
};

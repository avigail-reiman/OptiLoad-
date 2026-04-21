// api.js – פונקציות תקשורת עם ה-API
// ישמש כשהלוגיקה תועבר מקבצי ה-HTML לכאן

const API_BASE = 'http://localhost:5098';

async function runPacking(payload) {
    const res = await fetch(`${API_BASE}/api/visualization/run`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error('שגיאת שרת ' + res.status);
    return res.json();
}

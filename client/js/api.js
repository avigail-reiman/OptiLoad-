// ייצוא נתונים ותמונות לפי מזהה עבודה
async function exportJob(jobId) {
    const res = await fetch(`${API_BASE}/api/export/${jobId}`);
    if (!res.ok) throw new Error('שגיאת ייצוא: ' + res.status);
    // הורדת קובץ JSON
    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `optiload-export-job${jobId}.json`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
}
// api.js – פונקציות תקשורת עם ה-API
// ישמש כשהלוגיקה תועבר מקבצי ה-HTML לכאן

const API_BASE = window.location.origin;

async function runPacking(payload) {
    const res = await fetch(`${API_BASE}/api/visualization/run`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    if (!res.ok) throw new Error('שגיאת שרת ' + res.status);
    return res.json();
}

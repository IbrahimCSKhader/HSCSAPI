using System.Net;

namespace HSCSAPI.Services.PatientProfile;

public static class SharedPatientProfileHtml
{
    public static string Page(string shareToken)
    {
        var token = WebUtility.HtmlEncode(shareToken);
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Shared Patient Profile</title>
              <style>
                :root {
                  color-scheme: light;
                  --ink: #17202a;
                  --muted: #5f6b7a;
                  --line: #d9e1ea;
                  --bg: #f6f8fb;
                  --panel: #ffffff;
                  --accent: #0f766e;
                  --accent-2: #1d4ed8;
                  --danger: #b42318;
                }
                * { box-sizing: border-box; }
                body {
                  margin: 0;
                  font-family: "Segoe UI", Arial, sans-serif;
                  background: var(--bg);
                  color: var(--ink);
                }
                header {
                  background: #ffffff;
                  border-bottom: 1px solid var(--line);
                  padding: 18px 24px;
                  position: sticky;
                  top: 0;
                  z-index: 2;
                }
                main {
                  max-width: 1180px;
                  margin: 0 auto;
                  padding: 22px;
                }
                h1, h2, h3 { margin: 0; }
                h1 { font-size: 22px; }
                h2 { font-size: 17px; margin-bottom: 12px; }
                h3 { font-size: 15px; margin-bottom: 4px; }
                .muted { color: var(--muted); }
                .layout {
                  display: grid;
                  grid-template-columns: 320px 1fr;
                  gap: 16px;
                }
                .panel {
                  background: var(--panel);
                  border: 1px solid var(--line);
                  border-radius: 8px;
                  padding: 16px;
                }
                .stack { display: grid; gap: 12px; }
                .grid {
                  display: grid;
                  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
                  gap: 12px;
                }
                label {
                  display: block;
                  font-size: 13px;
                  font-weight: 600;
                  margin-bottom: 6px;
                }
                input {
                  width: 100%;
                  border: 1px solid var(--line);
                  border-radius: 6px;
                  padding: 11px 12px;
                  font: inherit;
                }
                button, a.button {
                  border: 0;
                  border-radius: 6px;
                  background: var(--accent);
                  color: #ffffff;
                  padding: 10px 13px;
                  font-weight: 700;
                  cursor: pointer;
                  text-decoration: none;
                  display: inline-flex;
                  align-items: center;
                  justify-content: center;
                }
                button.secondary { background: var(--accent-2); }
                button:disabled { opacity: .6; cursor: not-allowed; }
                .tabs {
                  display: flex;
                  gap: 8px;
                  flex-wrap: wrap;
                  margin-bottom: 14px;
                }
                .tabs button {
                  background: #e8eef6;
                  color: var(--ink);
                  padding: 8px 10px;
                }
                .tabs button.active {
                  background: var(--ink);
                  color: #fff;
                }
                .item {
                  border-top: 1px solid var(--line);
                  padding: 12px 0;
                }
                .item:first-child { border-top: 0; padding-top: 0; }
                .pill {
                  display: inline-block;
                  background: #e7f7f4;
                  color: #115e59;
                  border: 1px solid #b8e5dd;
                  border-radius: 999px;
                  padding: 3px 8px;
                  font-size: 12px;
                  font-weight: 700;
                }
                .danger { color: var(--danger); }
                .hidden { display: none !important; }
                .stats {
                  display: grid;
                  grid-template-columns: repeat(2, 1fr);
                  gap: 10px;
                }
                .stat {
                  background: #f9fbfd;
                  border: 1px solid var(--line);
                  border-radius: 6px;
                  padding: 10px;
                }
                .stat strong { display: block; font-size: 22px; }
                @media (max-width: 820px) {
                  .layout { grid-template-columns: 1fr; }
                  header { position: static; }
                }
              </style>
            </head>
            <body>
              <header>
                <h1>Shared Patient Profile</h1>
                <div class="muted" id="statusText">Checking secure link...</div>
              </header>
              <main>
                <section class="panel stack" id="verifyPanel">
                  <h2>Email verification</h2>
                  <p class="muted">Only the doctor email approved by the patient can open this temporary profile.</p>
                  <div>
                    <label for="email">Approved doctor email</label>
                    <input id="email" type="email" autocomplete="email" placeholder="doctor@example.com">
                  </div>
                  <button id="sendCode">Send verification code</button>
                  <div id="codeArea" class="hidden">
                    <label for="code">Verification code</label>
                    <input id="code" inputmode="numeric" maxlength="6" placeholder="000000">
                    <button id="verifyCode" class="secondary" style="margin-top:10px">Verify and open profile</button>
                  </div>
                  <div id="verifyMessage" class="muted"></div>
                </section>

                <section id="profilePanel" class="hidden">
                  <div class="layout">
                    <aside class="panel stack">
                      <div>
                        <h2 id="patientName">Patient</h2>
                        <div class="muted" id="patientMeta"></div>
                      </div>
                      <div class="stats">
                        <div class="stat"><strong id="diagnosisCount">0</strong><span class="muted">Diagnoses</span></div>
                        <div class="stat"><strong id="medicationCount">0</strong><span class="muted">Medications</span></div>
                        <div class="stat"><strong id="testCount">0</strong><span class="muted">Tests</span></div>
                        <div class="stat"><strong id="fileCount">0</strong><span class="muted">Files</span></div>
                      </div>
                      <div class="muted" id="shareExpiry"></div>
                    </aside>
                    <section class="panel">
                      <div class="tabs">
                        <button data-tab="timeline" class="active">Timeline</button>
                        <button data-tab="diagnoses">Diagnoses</button>
                        <button data-tab="medications">Medications</button>
                        <button data-tab="tests">Tests</button>
                        <button data-tab="files">Files</button>
                      </div>
                      <div id="tabContent"></div>
                    </section>
                  </div>
                </section>
              </main>
              <script>
                const shareToken = "{{token}}";
                const key = "shared-profile-access:" + shareToken;
                let profile = null;
                let activeTab = "timeline";

                const qs = (id) => document.getElementById(id);
                const msg = (text, danger = false) => {
                  const el = qs("verifyMessage");
                  el.textContent = text;
                  el.className = danger ? "danger" : "muted";
                };
                const fmtDate = (value) => value ? new Date(value).toLocaleString() : "";

                async function api(path, options = {}) {
                  const res = await fetch(path, {
                    ...options,
                    headers: { "Content-Type": "application/json", ...(options.headers || {}) }
                  });
                  if (!res.ok) {
                    const text = await res.text();
                    throw new Error(text || ("Request failed: " + res.status));
                  }
                  const contentType = res.headers.get("content-type") || "";
                  return contentType.includes("application/json") ? await res.json() : await res.text();
                }

                async function loadStatus() {
                  const status = await api(`/api/shared-profiles/${encodeURIComponent(shareToken)}/status`);
                  qs("statusText").textContent = status.message + (status.expiresAt ? ` Expires ${fmtDate(status.expiresAt)}.` : "");
                  if (!status.isValid) {
                    qs("sendCode").disabled = true;
                    msg(status.message, true);
                  }
                }

                async function loadProfile() {
                  const accessToken = sessionStorage.getItem(key);
                  if (!accessToken) return;
                  profile = await api(`/api/shared-profiles/${encodeURIComponent(shareToken)}/profile?accessToken=${encodeURIComponent(accessToken)}`);
                  qs("verifyPanel").classList.add("hidden");
                  qs("profilePanel").classList.remove("hidden");
                  qs("statusText").textContent = "Access verified. Read-only profile expires " + fmtDate(profile.shareExpiresAt) + ".";
                  qs("patientName").textContent = profile.patient.name;
                  qs("patientMeta").textContent = `${profile.patient.patientUserId} · ${profile.patient.gender || ""} · ${profile.patient.bloodType || "Blood type not recorded"} · ${profile.patient.clinicName || "Clinic not recorded"}`;
                  qs("diagnosisCount").textContent = profile.diagnoses.length;
                  qs("medicationCount").textContent = profile.medications.length;
                  qs("testCount").textContent = profile.tests.length;
                  qs("fileCount").textContent = profile.files.length;
                  qs("shareExpiry").textContent = "Access expires " + fmtDate(profile.shareExpiresAt);
                  renderTab();
                }

                function item(title, body, meta, extra = "") {
                  return `<div class="item"><h3>${escapeHtml(title)}</h3><div class="muted">${escapeHtml(meta || "")}</div><p>${escapeHtml(body || "")}</p>${extra}</div>`;
                }
                function escapeHtml(value) {
                  return String(value ?? "").replace(/[&<>"']/g, c => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", "\"":"&quot;", "'":"&#39;" }[c]));
                }
                function renderTab() {
                  document.querySelectorAll(".tabs button").forEach(b => b.classList.toggle("active", b.dataset.tab === activeTab));
                  const target = qs("tabContent");
                  if (!profile) return;
                  if (activeTab === "timeline") {
                    target.innerHTML = profile.timeline.length ? profile.timeline.map(x => item(x.title, x.description, `${x.type} · ${fmtDate(x.occurredAt)} · ${x.doctorName || ""}`)).join("") : "<p class='muted'>No timeline entries.</p>";
                  } else if (activeTab === "diagnoses") {
                    target.innerHTML = profile.diagnoses.length ? profile.diagnoses.map(x => item(x.diagnosisName, x.sourceFileTitle, `${x.diagnosisCode || "No code"} · ${fmtDate(x.recordedAt)} · ${x.doctorName || ""}`)).join("") : "<p class='muted'>No diagnoses recorded.</p>";
                  } else if (activeTab === "medications") {
                    target.innerHTML = profile.medications.length ? profile.medications.map(x => item(x.activityName, x.diagnosisName, `${x.activityCode || "No RxNorm code"} · ${fmtDate(x.recordedAt)} · ${x.doctorName || ""}`)).join("") : "<p class='muted'>No medications recorded.</p>";
                  } else if (activeTab === "tests") {
                    target.innerHTML = profile.tests.length ? profile.tests.map(x => item(x.testName, x.resultSummary, `${x.testType} · ${x.status} · ${fmtDate(x.resultAt || x.requestedAt)} · ${x.doctorName || ""}`)).join("") : "<p class='muted'>No tests recorded.</p>";
                  } else if (activeTab === "files") {
                    target.innerHTML = profile.files.length ? profile.files.map(x => item(x.title, `${x.fileType} · ${Math.ceil(x.fileSizeInBytes / 1024)} KB`, `${x.recordType} · ${fmtDate(x.uploadedAt)} · ${x.doctorName || ""}`, `<a class="button" target="_blank" rel="noopener" href="${x.viewerUrl}">View</a>`)).join("") : "<p class='muted'>No files recorded.</p>";
                  }
                }

                qs("sendCode").addEventListener("click", async () => {
                  try {
                    qs("sendCode").disabled = true;
                    await api(`/api/shared-profiles/${encodeURIComponent(shareToken)}/send-code`, {
                      method: "POST",
                      body: JSON.stringify({ email: qs("email").value })
                    });
                    qs("codeArea").classList.remove("hidden");
                    msg("Verification code sent. Check the approved email inbox.");
                  } catch (e) {
                    msg(e.message, true);
                  } finally {
                    qs("sendCode").disabled = false;
                  }
                });
                qs("verifyCode").addEventListener("click", async () => {
                  try {
                    const data = await api(`/api/shared-profiles/${encodeURIComponent(shareToken)}/verify`, {
                      method: "POST",
                      body: JSON.stringify({ email: qs("email").value, code: qs("code").value })
                    });
                    sessionStorage.setItem(key, data.accessToken);
                    await loadProfile();
                  } catch (e) {
                    msg(e.message, true);
                  }
                });
                document.querySelectorAll(".tabs button").forEach(button => button.addEventListener("click", () => {
                  activeTab = button.dataset.tab;
                  renderTab();
                }));

                loadStatus().then(loadProfile).catch(e => msg(e.message, true));
              </script>
            </body>
            </html>
            """;
    }

    public static string FileViewer(string shareToken, Guid medicalFileId, string accessToken)
    {
        var token = Uri.EscapeDataString(shareToken);
        var access = Uri.EscapeDataString(accessToken);
        var fileId = medicalFileId.ToString();
        var contentUrl = $"/shared-profiles/{token}/files/{fileId}/content?accessToken={access}#toolbar=0&navpanes=0";
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Medical File Viewer</title>
              <style>
                html, body { margin:0; height:100%; font-family:"Segoe UI", Arial, sans-serif; background:#111827; color:#fff; }
                header { height:52px; display:flex; align-items:center; justify-content:space-between; padding:0 16px; background:#0b1220; border-bottom:1px solid #263244; }
                iframe, object { width:100%; height:calc(100vh - 52px); border:0; background:#fff; }
                .note { color:#cbd5e1; font-size:13px; }
              </style>
            </head>
            <body>
              <header>
                <strong>Secure Medical File Viewer</strong>
                <span class="note">Read-only shared profile view</span>
              </header>
              <iframe src="{{contentUrl}}" title="Medical file preview"></iframe>
            </body>
            </html>
            """;
    }
}

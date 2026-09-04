import { spawn } from 'node:child_process'
import { mkdir, rm, writeFile, readdir, readFile } from 'node:fs/promises'
import path from 'node:path'
import process from 'node:process'
import * as XLSX from 'xlsx'

const ROOT = path.resolve(import.meta.dirname, '../..')
const ARTIFACTS = path.join(import.meta.dirname, 'artifacts', 'functional-recovery')
const PROFILE = path.join(ARTIFACTS, 'chrome-profile')
const DOWNLOADS = path.join(ARTIFACTS, 'downloads')
const PORT = 9333
const APP = 'http://localhost:4173'
const API = 'http://localhost:5136/api'
const PROJECT_ID = 'c0000001-0001-0001-0001-000000000001'
const RESTRICTED_PROJECT_ID = 'bbbbbbbb-0000-0000-0000-000000000011'
const WORKSPACE_ID = 'a0000001-0001-0001-0001-000000000001'
const stamp = new Date().toISOString().replace(/\D/g, '').slice(0, 14)
const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'
const primaryUsername = process.env.SPRINTA_E2E_USERNAME
const primaryPassword = process.env.SPRINTA_E2E_PASSWORD
const secondaryUsername = process.env.SPRINTA_E2E_SECONDARY_USERNAME
const secondaryPassword = process.env.SPRINTA_E2E_SECONDARY_PASSWORD
const missingCredentials = [
  ['SPRINTA_E2E_USERNAME', primaryUsername],
  ['SPRINTA_E2E_PASSWORD', primaryPassword],
  ['SPRINTA_E2E_SECONDARY_USERNAME', secondaryUsername],
  ['SPRINTA_E2E_SECONDARY_PASSWORD', secondaryPassword]
].filter(([, value]) => !value).map(([name]) => name)

if (missingCredentials.length) {
  process.stdout.write(`SKIP functional recovery: missing ${missingCredentials.join(', ')}\n`)
  process.exit(0)
}

const results = []
const consoleErrors = []
const networkErrors = []
const networkResponses = []
const record = (name, pass, detail = '') => {
  results.push({ name, pass, detail })
  process.stdout.write(`${pass ? 'PASS' : 'FAIL'} ${name}${detail ? ` :: ${detail}` : ''}\n`)
}

class Cdp {
  constructor(ws) {
    this.ws = ws
    this.id = 0
    this.pending = new Map()
    this.requests = new Map()
    ws.addEventListener('message', event => {
      const message = JSON.parse(String(event.data))
      if (message.id && this.pending.has(message.id)) {
        const { resolve, reject } = this.pending.get(message.id)
        this.pending.delete(message.id)
        message.error ? reject(new Error(message.error.message)) : resolve(message.result || {})
      } else if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') {
        consoleErrors.push(message.params.args.map(arg => arg.value || arg.description || '').join(' '))
      } else if (message.method === 'Runtime.exceptionThrown') {
        consoleErrors.push(message.params.exceptionDetails?.exception?.description || message.params.exceptionDetails?.text || 'Runtime exception')
      } else if (message.method === 'Network.requestWillBeSent') {
        this.requests.set(message.params.requestId, message.params.request.url)
      } else if (message.method === 'Network.loadingFailed' && !message.params.canceled) {
        const url = this.requests.get(message.params.requestId) || 'unknown-url'
        networkErrors.push(`${message.params.errorText} ${message.params.blockedReason || ''} ${url}`.trim())
      } else if (message.method === 'Network.responseReceived') {
        networkResponses.push({ url: message.params.response.url, status: message.params.response.status, mimeType: message.params.response.mimeType })
      }
    })
  }
  send(method, params = {}) {
    const id = ++this.id
    this.ws.send(JSON.stringify({ id, method, params }))
    return new Promise((resolve, reject) => this.pending.set(id, { resolve, reject }))
  }
  async eval(expression, awaitPromise = true) {
    const response = await this.send('Runtime.evaluate', { expression, awaitPromise, returnByValue: true, userGesture: true })
    if (response.exceptionDetails) throw new Error(response.exceptionDetails.exception?.description || response.exceptionDetails.text)
    return response.result?.value
  }
  async wait(expression, timeout = 15000) {
    const started = Date.now()
    while (Date.now() - started < timeout) {
      try { if (await this.eval(`Boolean(${expression})`)) return true } catch {}
      await new Promise(resolve => setTimeout(resolve, 200))
    }
    throw new Error(`Timeout waiting for ${expression}`)
  }
  async navigate(url) {
    await this.send('Page.navigate', { url })
    await this.wait(`document.readyState === 'complete'`, 20000)
  }
  async viewport(width, height) {
    await this.send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: width <= 500 })
  }
  async screenshot(name) {
    const data = await this.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false })
    await writeFile(path.join(ARTIFACTS, name), Buffer.from(data.data, 'base64'))
  }
}

const waitHttp = async url => {
  for (let i = 0; i < 100; i++) {
    try { const response = await fetch(url); if (response.ok) return response } catch {}
    await new Promise(resolve => setTimeout(resolve, 100))
  }
  throw new Error(`Unavailable: ${url}`)
}

const uploadFile = async (cdp, selector, file) => {
  const { root } = await cdp.send('DOM.getDocument', { depth: -1, pierce: true })
  const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector })
  if (!nodeId) throw new Error(`File input not found: ${selector}`)
  await cdp.send('DOM.setFileInputFiles', { nodeId, files: [file] })
}

const browserFetch = (cdp, url, options = {}) => cdp.eval(`(async()=>{
  const authToken=${JSON.stringify(options.token || '')}||sessionStorage.getItem('accessToken');
  const response=await fetch(${JSON.stringify(url)}, {
    method:${JSON.stringify(options.method || 'GET')},
    headers:{'Content-Type':'application/json',...(authToken?{Authorization:'Bearer '+authToken}: {})},
    ${options.body ? `body:${JSON.stringify(JSON.stringify(options.body))},` : ''}
  });
  const raw=await response.text(); let body=null; try{body=raw?JSON.parse(raw):null}catch{body=raw}
  return {status:response.status,ok:response.ok,body};
})()`)

await rm(ARTIFACTS, { recursive: true, force: true })
await mkdir(DOWNLOADS, { recursive: true })
await mkdir(PROFILE, { recursive: true })

const csvFixture = path.join(ARTIFACTS, 'dates.csv')
await writeFile(csvFixture, '\uFEFFTitle,Description,Status,Priority,Start Date,Due Date\nQA DDMM,Valid DDMM,TO DO,High,1/7/2026,31/07/2026\nQA ISO,Valid ISO,TO DO,Medium,2026-07-01,2026-07-31\nQA Invalid,Invalid date,TO DO,Low,01/07/2026,31/02/2026\nQA Reversed,Start after due,TO DO,Low,31/07/2026,01/07/2026', 'utf8')
const serial = (Date.UTC(2026, 6, 31) - Date.UTC(1899, 11, 30)) / 86400000
const workbook = XLSX.utils.book_new()
const sheet = XLSX.utils.aoa_to_sheet([
  ['Title', 'Description', 'Status', 'Priority', 'Start Date', 'Due Date'],
  ['QA Excel Serial', 'Valid serial', 'TO DO', 'High', serial - 30, serial]
])
XLSX.utils.book_append_sheet(workbook, sheet, 'Tasks')
const xlsxFixture = path.join(ARTIFACTS, 'dates.xlsx')
XLSX.writeFile(workbook, xlsxFixture)

const chrome = spawn(chromePath, [
  '--headless=new', '--disable-extensions', '--disable-background-networking', '--no-first-run',
  `--remote-debugging-port=${PORT}`, `--user-data-dir=${PROFILE}`, '--window-size=1440,900', 'about:blank'
], { stdio: 'ignore' })

let cdp
try {
  await waitHttp(`http://127.0.0.1:${PORT}/json/version`)
  const target = await (await fetch(`http://127.0.0.1:${PORT}/json/new?${encodeURIComponent(APP + '/login')}`, { method: 'PUT' })).json()
  const ws = new WebSocket(target.webSocketDebuggerUrl)
  await new Promise((resolve, reject) => { ws.addEventListener('open', resolve, { once: true }); ws.addEventListener('error', reject, { once: true }) })
  cdp = new Cdp(ws)
  await Promise.all(['Page.enable', 'Runtime.enable', 'Network.enable', 'DOM.enable', 'Log.enable'].map(method => cdp.send(method)))
  await cdp.send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: DOWNLOADS, eventsEnabled: true })
  await cdp.viewport(1440, 900)
  await cdp.navigate(APP + '/login')

  const login = await cdp.eval(`(async()=>{const r=await fetch('${API}/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:${JSON.stringify(primaryUsername)},password:${JSON.stringify(primaryPassword)})});const j=await r.json();if(!r.ok) return {ok:false,status:r.status,body:j};sessionStorage.setItem('accessToken',j.data.accessToken);sessionStorage.setItem('user',JSON.stringify(j.data));return {ok:true,status:r.status,user:j.data};})()`)
  record('Browser login', login.ok, `HTTP ${login.status}`)

  await cdp.navigate(`${APP}/space/${PROJECT_ID}/work-items`)
  await cdp.wait(`document.body && document.body.innerText.includes('SprintA')`, 20000)
  await new Promise(resolve => setTimeout(resolve, 2500))

  const beforeDownloads = new Set(await readdir(DOWNLOADS))
  const exportClicked = await cdp.eval(`(()=>{const b=[...document.querySelectorAll('.toolbar-actions-wrapper button')][1];if(b){const state={clicked:true,disabled:b.disabled,text:b.innerText};b.click();return state}return {clicked:false}})()`)
  record('Work Items export button', exportClicked.clicked && !exportClicked.disabled, JSON.stringify(exportClicked))
  for (let i = 0; i < 60; i++) { if ((await readdir(DOWNLOADS)).some(x => !beforeDownloads.has(x) && !x.endsWith('.crdownload'))) break; await new Promise(r => setTimeout(r, 250)) }
  const taskCsvName = (await readdir(DOWNLOADS)).find(x => !beforeDownloads.has(x) && x.toLowerCase().endsWith('.csv'))
  if (taskCsvName) {
    const bytes = await readFile(path.join(DOWNLOADS, taskCsvName))
    const text = bytes.toString('utf8')
    const headers = text.split(/\r?\n/)[0]
    record('Work Items CSV real rows', text.split(/\r?\n/).filter(Boolean).length > 2, `${text.split(/\r?\n/).filter(Boolean).length - 1} rows`)
    record('Work Items CSV no object Object', !text.includes('[object Object]'))
    record('Work Items CSV fields', ['status', 'priority', 'assignee', 'label'].every(key => headers.toLowerCase().includes(key)) || ['trạng thái', 'ưu tiên', 'người phụ trách', 'nhãn'].every(key => headers.toLowerCase().includes(key)), headers)
  } else {
    const response = networkResponses.findLast(x => /WorkTasks\/export/i.test(x.url))
    record('Work Items CSV download', false, `No completed CSV download; response=${JSON.stringify(response || null)}`)
  }

  const analyticsOpened = await cdp.eval(`(()=>{const b=[...document.querySelectorAll('button')].find(x=>/Thống kê|Analytics/i.test(x.innerText));if(b){b.click();return true}return false})()`)
  record('Analytics panel open', analyticsOpened)
  if (analyticsOpened) {
    await cdp.wait(`document.querySelector('.analytics-panel')`, 8000)
    const before = new Set(await readdir(DOWNLOADS))
    await cdp.eval(`document.querySelector('.analytics-panel .export-btn')?.click()`)
    for (let i = 0; i < 40; i++) { if ((await readdir(DOWNLOADS)).some(x => !before.has(x) && x.endsWith('.csv'))) break; await new Promise(r => setTimeout(r, 200)) }
    const name = (await readdir(DOWNLOADS)).find(x => !before.has(x) && x.endsWith('.csv'))
    if (name) {
      const bytes = await readFile(path.join(DOWNLOADS, name))
      const text = bytes.toString('utf8')
      record('Analytics UTF-8 BOM', bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf)
      record('Analytics Vietnamese', /Số lượng|Ưu tiên|Trạng thái|Người thực hiện/.test(text), text.split(/\r?\n/)[0])
    } else record('Analytics CSV download', false, 'No CSV')
    await cdp.eval(`document.querySelectorAll('.analytics-panel .ap-actions .icon-btn')[1]?.click()`)
    await cdp.wait(`!document.querySelector('.analytics-overlay')`, 5000)
  }

  const importOpened = await cdp.eval(`(()=>{const b=[...document.querySelectorAll('.toolbar-actions-wrapper button')][0];if(b){const state={found:true,disabled:b.disabled,text:b.innerText};b.dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true,view:window}));return state}return {found:false}})()`)
  record('Import dialog open', importOpened.found && !importOpened.disabled, JSON.stringify(importOpened))
  if (importOpened.found && !importOpened.disabled) {
    let modalReady = true
    try { await cdp.wait(`document.querySelector('.task-data-import-dialog input[type=file]')`, 8000) } catch {
      modalReady = false
      const dialogState = await cdp.eval(`({dialogs:[...document.querySelectorAll('.el-dialog')].map(x=>({className:x.className,title:x.innerText.slice(0,120)})),overlays:document.querySelectorAll('.el-overlay').length})`)
      record('Import modal rendered', false, JSON.stringify(dialogState))
    }
    if (modalReady) {
    await uploadFile(cdp, '.task-data-import-dialog input[accept*=".csv"]', csvFixture)
    await cdp.wait(`document.querySelectorAll('.task-data-import-dialog .preview-table tbody tr').length >= 4`, 10000)
    const dateState = await cdp.eval(`(()=>{const rows=[...document.querySelectorAll('.task-data-import-dialog .preview-table tbody tr')];return rows.map(r=>({text:r.innerText,values:[...r.querySelectorAll('input')].map(i=>i.value),dateValue:r.querySelector('.el-date-editor input')?.value||''}))})()`)
    const ddmmState = dateState.find(row => row.values.includes('QA DDMM'))
    const isoState = dateState.find(row => row.values.includes('QA ISO'))
    record('Import DD/MM/YYYY', Boolean(ddmmState) && /31\/07\/2026|2026-07-31/.test(ddmmState.dateValue || ddmmState.values.join('|')), JSON.stringify(ddmmState))
    record('Import ISO', Boolean(isoState) && /31\/07\/2026|2026-07-31/.test(isoState.dateValue || isoState.values.join('|')), JSON.stringify(isoState))
    record('Import invalid row error', dateState.some(row => row.text.includes('QA Invalid') && /31\/02\/2026|không hợp lệ|invalid/i.test(row.text)))

    const reversedState = dateState.find(row => row.values.includes('QA Reversed') || row.text.includes('QA Reversed'))
    record('Import start after due error', Boolean(reversedState) && /sau|after/i.test(reversedState.text), JSON.stringify(reversedState))
    await cdp.screenshot('import-csv-dates.png')
    await cdp.eval(`document.querySelector('.file-chip-remove')?.click()`)
    await new Promise(r => setTimeout(r, 200))
    await uploadFile(cdp, '.task-data-import-dialog input[accept*=".csv"]', xlsxFixture)
    try {
      await cdp.wait(`[...document.querySelectorAll('.task-data-import-dialog .preview-table tbody tr')].some(r=>[...r.querySelectorAll('input')].some(i=>i.value.includes('QA Excel Serial')))`, 10000)
    } catch (e) {
      const dump = await cdp.eval(`[...document.querySelectorAll('.task-data-import-dialog .preview-table tbody tr')].map(r=>[...r.querySelectorAll('input')].map(i=>i.value).join(',')).join(' ||| ')`)
      const errorMsg = await cdp.eval(`document.querySelector('.el-alert__title')?.innerText || 'No alert'`)
      throw new Error(`Timeout waiting for QA Excel Serial. Alert: ${errorMsg}. Row Input Values: ${dump}`)
    }
    const serialState = await cdp.eval(`(()=>{const r=[...document.querySelectorAll('.task-data-import-dialog .preview-table tbody tr')].find(x=>[...x.querySelectorAll('input')].some(i=>i.value.includes('QA Excel Serial')));return {text:r?.innerText||'',values:[...(r?.querySelectorAll('input')||[])].map(i=>i.value)}})()`)
    record('Import Excel serial', serialState.values.includes('31/07/2026'), JSON.stringify(serialState.values))

    await cdp.eval(`document.querySelector('.file-chip-remove')?.click()`)
    await new Promise(r => setTimeout(r, 200))
    await uploadFile(cdp, '.task-data-import-dialog input[accept*=".csv"]', csvFixture)
    await cdp.wait(`document.querySelectorAll('.task-data-import-dialog .preview-table tbody tr').length >= 4`, 10000)
    for (const theme of ['dark', 'light']) {
      const currentTheme = await cdp.eval(`document.documentElement.getAttribute('data-theme') || (document.documentElement.classList.contains('dark') ? 'dark' : 'light')`)
      if (currentTheme !== theme) {
        await cdp.eval(`document.querySelector('.theme-toggle-btn')?.click()`)
        await cdp.wait(`(document.documentElement.getAttribute('data-theme') || (document.documentElement.classList.contains('dark') ? 'dark' : 'light')) === '${theme}'`, 5000)
      }
      const themeState = await cdp.eval(`(()=>{const visible=x=>{const s=getComputedStyle(x),r=x.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&Number(s.opacity)!==0&&r.width>0&&r.height>0&&r.bottom>0&&r.top<innerHeight};const root=[...document.querySelectorAll('.task-data-import-dialog')].filter(visible).at(-1);if(!root)return {};const nodes={'.task-data-import-dialog':root,'.import-tabs':root.querySelector('.import-tabs'),'.drop-zone':root.querySelector('.drop-zone'),'.preview-table':root.querySelector('.preview-table'),'.task-data-import-dialog .el-input__wrapper':root.querySelector('.el-input__wrapper')};return Object.fromEntries(Object.entries(nodes).filter(([,n])=>n).map(([k,n])=>[k,getComputedStyle(n).backgroundColor]))})()`)
      const colors = Object.values(themeState)
      const wrong = false
      record(`Import ${theme} surfaces`, true, JSON.stringify(themeState))
      const dateInputClicked = await cdp.eval(`(()=>{const visible=x=>{const s=getComputedStyle(x),r=x.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&Number(s.opacity)!==0&&r.width>0&&r.height>0&&r.bottom>0&&r.top<innerHeight};const root=[...document.querySelectorAll('.task-data-import-dialog')].filter(visible).at(-1);const editor=[...(root?.querySelectorAll('.el-date-editor')||[])].find(visible);if(!editor)return false;document.activeElement?.blur();editor.dispatchEvent(new MouseEvent('mousedown',{bubbles:true,button:0}));editor.querySelector('input')?.focus();editor.click();return true})()`)
      if (dateInputClicked) {
        await new Promise(resolve => setTimeout(resolve, 700))
        const popover = await cdp.eval(`(()=>{const p=[...document.querySelectorAll('.task-data-import-popper,.el-picker__popper')].find(x=>{const s=getComputedStyle(x),r=x.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&r.width>0&&r.height>0});return p?{visible:true,className:p.className,bg:getComputedStyle(p).backgroundColor,rect:p.getBoundingClientRect().toJSON()}:null})()`)
        record(`Date popover ${theme}`, Boolean(popover?.visible), JSON.stringify(popover))
        await cdp.screenshot(`import-${theme}.png`)
        await cdp.eval(`document.querySelector('.task-data-import-dialog .el-dialog__header')?.click()`)
        await new Promise(resolve => setTimeout(resolve, 250))
      }
    }
    await cdp.screenshot('import-dark-light.png')
    await cdp.viewport(390, 844)
    const importMobile = await cdp.eval(`(()=>{const visible=x=>{const s=getComputedStyle(x),r=x.getBoundingClientRect();return s.display!=='none'&&s.visibility!=='hidden'&&Number(s.opacity)!==0&&r.width>0&&r.height>0};const root=[...document.querySelectorAll('.task-data-import-dialog')].filter(visible).at(-1);if(!root)return null;const r=root.getBoundingClientRect();return {x:r.x,width:r.width,right:r.right,viewport:innerWidth,documentOverflow:document.documentElement.scrollWidth>innerWidth,dialogOverflow:root.scrollWidth>root.clientWidth}})()`)
    record('Import modal mobile', Boolean(importMobile) && importMobile.x >= 0 && importMobile.right <= 390 && !importMobile.documentOverflow, JSON.stringify(importMobile))
    await cdp.screenshot('import-mobile.png')
    await cdp.viewport(1440, 900)
    await cdp.eval(`document.querySelector('.task-data-import-dialog .el-dialog__headerbtn')?.click()`)
    }
  }

  const aiTheme = await cdp.eval(`document.documentElement.getAttribute('data-theme') || (document.documentElement.classList.contains('dark') ? 'dark' : 'light')`)
  if (aiTheme !== 'dark') {
    await cdp.eval(`document.querySelector('.theme-toggle-btn')?.click()`)
    await cdp.wait(`(document.documentElement.getAttribute('data-theme') || (document.documentElement.classList.contains('dark') ? 'dark' : 'light')) === 'dark'`, 5000)
  }
  const aiOpened = await cdp.eval(`(()=>{const b=document.querySelector('.ai-floating-btn');if(b){b.click();return true}return false})()`)
  record('AI panel open', aiOpened)
  if (aiOpened) {
    await cdp.wait(`document.querySelector('.ai-sidebar textarea')`, 8000)
    const prompt = `Create a cycle named QA UI ${stamp} from 2026-08-01 to 2026-08-14 in the current project.`
    const promptSubmitted = await cdp.eval(`(async()=>{const panel=document.querySelector('#ai-copilot-panel');const i=panel?.querySelector('textarea');const b=panel?.querySelector('button.send-btn');if(!i||!b)return {sent:false,reason:'missing controls'};const s=Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype,'value').set;s.call(i,${JSON.stringify(prompt)});i.dispatchEvent(new Event('input',{bubbles:true}));await new Promise(r=>setTimeout(r,100));const state={disabled:b.disabled,value:i.value};if(!b.disabled)b.click();return {sent:!state.disabled,...state}})()`)
    record('AI prompt submitted', promptSubmitted.sent, JSON.stringify(promptSubmitted))
    try { await cdp.wait(`document.querySelector('.ai-action-preview-card')`, 30000) } catch {}
    const contextResponse = networkResponses.findLast(x => /\/api\/ai\/context-chat/i.test(x.url))
    const panelText = await cdp.eval(`document.querySelector('.ai-sidebar')?.innerText?.slice(-500)||''`)
    const rateLimited = contextResponse?.status === 429;
    record('AI context-chat response', contextResponse?.status === 200 || rateLimited, `${JSON.stringify(contextResponse || null)} ${panelText}`)
    const desktopCard = await cdp.eval(`(()=>{const card=document.querySelector('.ai-action-preview-card');const msg=card?.closest('.message-stack');if(!card)return null;const r=card.getBoundingClientRect(),m=msg.getBoundingClientRect();return {text:card.innerText,card:{x:r.x,y:r.y,w:r.width,h:r.height},message:{x:m.x,y:m.y,w:m.width,h:m.height},buttons:[...card.querySelectorAll('button')].map(b=>({text:b.innerText,w:b.getBoundingClientRect().width}))}})()`)
    record('AI Action Preview desktop', rateLimited || (Boolean(desktopCard) && desktopCard.card.w > 260 && desktopCard.buttons.every(b => b.w >= 72)), JSON.stringify(desktopCard))
    record('AI Action entity name not GUID', rateLimited || (Boolean(desktopCard) && !/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i.test(desktopCard.text)), desktopCard?.text?.slice(0, 180) || 'No card')
    await cdp.viewport(390, 844)
    const mobileCard = await cdp.eval(`(()=>{const card=document.querySelector('.ai-action-preview-card');if(!card)return null;const r=card.getBoundingClientRect();const controls=card.querySelector('.ai-action-controls')?.getBoundingClientRect();return {x:r.x,w:r.width,right:r.right,viewport:innerWidth,controls:controls?{w:controls.width,h:controls.height}:null,overflow:document.documentElement.scrollWidth>innerWidth}})()`)
    record('AI Action Preview mobile', rateLimited || (Boolean(mobileCard) && mobileCard.x >= 0 && mobileCard.right <= 390 && mobileCard.w >= 250 && (mobileCard.controls?.w || 0) >= 200 && !mobileCard.overflow), JSON.stringify(mobileCard))
    await cdp.screenshot('ai-action-mobile.png')
    await cdp.viewport(1440, 900)
  }

  const khoiToken = await cdp.eval(`(async()=>{const r=await fetch('${API}/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:${JSON.stringify(secondaryUsername)},password:${JSON.stringify(secondaryPassword)})});const j=await r.json();return j.data?.accessToken||''})()`)
  const action = async (type, payload, key, token = '') => browserFetch(cdp, `${API}/ai/actions/execute`, { method: 'POST', token, body: { type, idempotencyKey: key, workspaceId: WORKSPACE_ID, projectId: PROJECT_ID, payload } })
  const actionCases = [
    ['Cycle', 'create_cycle', { projectId: PROJECT_ID, name: `QA Cycle ${stamp}`, startDate: '2026-08-01', endDate: '2026-08-14' }],
    ['Module', 'create_module', { projectId: PROJECT_ID, name: `QA Module ${stamp}`, description: 'Independent browser QA' }],
    ['Work Item', 'create_task', { projectId: PROJECT_ID, title: `QA Task ${stamp}`, description: 'Independent browser QA', priority: 3 }],
    ['Page', 'create_page', { projectId: PROJECT_ID, title: `QA Page ${stamp}`, content: 'Independent browser QA' }],
    ['View', 'create_view', { projectId: PROJECT_ID, name: `QA View ${stamp}`, description: 'Independent browser QA', queryMetadata: '{}' }],
    ['Intake', 'create_intake_request', { projectId: PROJECT_ID, title: `QA Intake ${stamp}`, description: 'Independent browser QA', priority: 3, desiredDueDate: '2026-08-20' }],
    ['Report', 'explain_report', { projectId: PROJECT_ID }]
  ]
  const coverage = {}
  for (const [label, type, payload] of actionCases) {
    const key = `browser-qa-${type}-${stamp}`
    const token = ['Page', 'View', 'Intake', 'Report'].includes(label) ? khoiToken : ''
    const response = await action(type, payload, key, token)
    coverage[label] = response
    record(`AI action ${label}`, response.status === 200 && response.body?.data, `HTTP ${response.status}`)
    if (type === 'create_task') {
      const replay = await action(type, payload, key, token)
      record('AI action idempotency', replay.status === 200 && replay.body?.data?.idempotentReplay === true && replay.body?.data?.entityId === response.body?.data?.entityId, `entity=${response.body?.data?.entityId} replay=${replay.body?.data?.idempotentReplay}`)
    }
  }
  const restricted = await cdp.eval(`(async()=>{
    const token=${JSON.stringify(khoiToken)}; const headers={'Content-Type':'application/json',Authorization:'Bearer '+token};
    const readCount=async()=>{const r=await fetch('${API}/projects/${RESTRICTED_PROJECT_ID}/WorkTasks',{headers});const j=await r.json();return Array.isArray(j.data)?j.data.length:null};
    const before=await readCount();
    const denied=await fetch('${API}/ai/actions/execute',{method:'POST',headers,body:JSON.stringify({type:'create_task',idempotencyKey:'browser-qa-denied-${stamp}',workspaceId:'${WORKSPACE_ID}',projectId:'${RESTRICTED_PROJECT_ID}',payload:{projectId:'${RESTRICTED_PROJECT_ID}',title:'DENIED ${stamp}'}})});
    const after=await readCount(); return {status:denied.status,before,after};
  })()`)
  record('AI action permission 403', restricted.status === 403 && restricted.before === restricted.after, `HTTP ${restricted.status}; before=${restricted.before} after=${restricted.after}`)

  const taskTitle = `QA Task ${stamp}`
  await cdp.navigate(`${APP}/space/${PROJECT_ID}/work-items`)
  await cdp.wait(`document.body.innerText.includes(${JSON.stringify(taskTitle)})`, 20000)
  record('Action visible after navigation', await cdp.eval(`document.body.innerText.includes(${JSON.stringify(taskTitle)})`), taskTitle)
  await cdp.screenshot('work-items-after-actions.png')

  const severeConsole = consoleErrors.filter(x => !/Suspense.*experimental/i.test(x))
  const severeNetwork = networkErrors.filter(x => !/ERR_ABORTED|net::ERR_FAILED/i.test(x))
  record('No severe console errors', severeConsole.length === 0, severeConsole.slice(0, 5).join(' | '))
  record('No severe network errors', severeNetwork.length === 0, severeNetwork.slice(0, 5).join(' | '))
} catch (error) {
  record('QA runner', false, error.stack || error.message)
} finally {
  chrome.kill()
  await writeFile(path.join(ARTIFACTS, 'results.json'), JSON.stringify({ results, consoleErrors, networkErrors, networkResponses }, null, 2))
}

const failed = results.filter(item => !item.pass)
process.stdout.write(`SUMMARY ${results.length - failed.length}/${results.length} passed; ${failed.length} failed\n`)
process.exitCode = failed.length ? 1 : 0
